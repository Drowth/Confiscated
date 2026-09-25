using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Confiscated
{
    /// <summary>
    /// Caretaker behaviour: patrol between points (with dwell + facing), investigate noises, chase on sight,
    /// search around the last known position after losing the player, catch when close.
    /// Vision is a cone plus a physics line-of-sight check, so walls and closed doors block it.
    /// The cutout visual is a child with BillboardY; this root's forward is the facing direction.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CaretakerAI : MonoBehaviour
    {
        public enum State { Patrol, Investigate, Chase, Search, Frozen }

        [System.Serializable]
        public class PatrolPoint
        {
            public Transform point;
            public float dwellSeconds = 3f;
            [Tooltip("Optional: face this direction (world) while dwelling. Zero = keep arrival heading.")]
            public Vector3 faceDirection;
        }

        [Header("Patrol")]
        public List<PatrolPoint> patrol = new List<PatrolPoint>();
        [SerializeField] float patrolSpeed = 1.5f;
        [SerializeField] float investigateSpeed = 2.6f;
        [SerializeField] float chaseSpeed = 3.3f;
        [SerializeField] float turnSpeedDeg = 240f;

        [Header("Senses")]
        [SerializeField] float sightRange = 9.5f;
        [SerializeField] float sightConeDegrees = 90f;
        [SerializeField] float eyeHeight = 1.7f;
        [SerializeField] float suspicionSeconds = 0.6f;
        [SerializeField] LayerMask sightBlockers = ~0;
        [SerializeField] float catchDistance = 1.15f;

        [Header("Investigate / Search")]
        [SerializeField] float lookAroundSeconds = 5f;
        [SerializeField] float loseSightSeconds = 2.5f;
        [SerializeField] float searchRadius = 3f;
        [SerializeField] int searchPoints = 3;

        NavMeshAgent agent;
        CutoutMotion cutoutMotion;
        Transform player;
        State state = State.Patrol;
        int patrolIndex;
        float dwellUntil;
        bool dwelling;
        Vector3 pointOfInterest;
        float lookUntil;
        float lastSeenTime;
        Vector3 lastSeenPos;
        float suspicion;
        int searchStep;
        bool arrivedAtPoi;
        Vector3 desiredFacing;
        bool hasDesiredFacing;
        float lastGoToTime = -10f;
        const float TravelTimeout = 14f;
        float plannedTravelTimeout = TravelTimeout;
        bool pathTimeEstimated;
        float ignorePlayerUntil;
        CaretakerPassCheck passCheck;
        float glueUntil;
        bool glueHeld;
        AudioSource voice;
        float nextSpottedShout,nextVoiceAt;
        int heardTake;
        public bool IsGlued=>glueHeld&&Time.time<glueUntil;
        static readonly RaycastHit[] catchHits = new RaycastHit[12];

        public State Current => state;
        public bool Investigating(Vector3 position)=>state==State.Investigate&&(pointOfInterest-position).sqrMagnitude<4;
        public float Suspicion => suspicion;
        public float SuspicionSeconds => suspicionSeconds;
        public bool CanCurrentlySeePlayer { get; private set; }
        public float PlayerDistance { get; private set; } = float.PositiveInfinity;
        public float VisibilityCloseness => Mathf.Clamp01((sightRange-PlayerDistance)/Mathf.Max(.01f,sightRange-catchDistance));

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            passCheck=GetComponent<CaretakerPassCheck>();
            cutoutMotion = GetComponentInChildren<CutoutMotion>();
            agent.updateRotation = false; // we rotate the root ourselves so facing can be held while dwelling
        }

        void OnEnable() { NoiseEvents.OnNoise += OnNoise; }
        void OnDisable() { NoiseEvents.OnNoise -= OnNoise; CanCurrentlySeePlayer=false; }

        void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            agent.speed = patrolSpeed;
            if(state==State.Frozen)return;
            if (patrol.Count > 0) GoTo(patrol[0].point.position, patrolSpeed);
        }

        public void Freeze()
        {
            state = State.Frozen;
            if(voice!=null)voice.Stop();
            cutoutMotion?.SetFrozen(true);
            if (agent.isOnNavMesh) agent.isStopped = true;
        }

        public bool TryStickInGlue(float seconds)
        {
            if(!isActiveAndEnabled||state==State.Frozen||glueHeld||seconds<=0||agent==null||!agent.enabled||!agent.isOnNavMesh)return false;
            glueHeld=true;glueUntil=Time.time+seconds;
            agent.isStopped=true;agent.velocity=Vector3.zero;cutoutMotion?.SetFrozen(true);
            GetComponent<CaretakerGait>()?.Footsteps?.Stop();
            return true;
        }

        bool TickGlue()
        {
            if(!glueHeld)return false;
            if(Time.time<glueUntil)
            {
                if(agent.enabled&&agent.isOnNavMesh){agent.isStopped=true;agent.velocity=Vector3.zero;}
                return true;
            }
            glueHeld=false;
            // A win, catch or detention can freeze him independently while glue is active.
            if(state!=State.Frozen&&GameManager.Instance!=null&&GameManager.Instance.IsPlaying)
            {
                cutoutMotion?.SetFrozen(false);
                if(agent.enabled&&agent.isOnNavMesh)agent.isStopped=false;
            }
            return false;
        }

        void Update()
        {
            CanCurrentlySeePlayer=false;
            PlayerDistance=player!=null?Vector3.Distance(transform.position,player.position):float.PositiveInfinity;
            if(TickGlue())return;
            if (ComicDialogue.IsActive || state == State.Frozen || player == null) return;
            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) { Freeze(); return; }

            bool sees = Time.time >= ignorePlayerUntil && CanSeePlayer();
            CanCurrentlySeePlayer=sees;
            if(passCheck!=null&&passCheck.FilterSight(sees))sees=false;
            if(passCheck!=null&&(passCheck.Checking||passCheck.Approaching)){SetFacing(player.position-transform.position);TickRotation();return;}
            if (sees)
            {
                lastSeenTime = Time.time;
                lastSeenPos = player.position;
                suspicion = passCheck!=null&&passCheck.WitnessedOffence?1f:Mathf.Min(1f, suspicion + Time.deltaTime / suspicionSeconds);
                if (state != State.Chase && suspicion >= 1f) EnterChase();
            }
            else suspicion = Mathf.Max(0f, suspicion - Time.deltaTime / (suspicionSeconds * 2f));

            switch (state)
            {
                case State.Patrol: TickPatrol(); break;
                case State.Investigate: TickInvestigate(); break;
                case State.Chase: TickChase(sees); break;
                case State.Search: TickSearch(); break;
            }

            TickRotation();
        }

        // ------------------------------------------------------------------ states

        void TickPatrol()
        {
            if (patrol.Count == 0) return;
            if (dwelling)
            {
                if (Time.time >= dwellUntil)
                {
                    dwelling = false;
                    patrolIndex = (patrolIndex + 1) % patrol.Count;
                    GoTo(patrol[patrolIndex].point.position, patrolSpeed);
                }
                return;
            }
            if (Arrived())
            {
                var pp = patrol[patrolIndex];
                dwelling = true;
                dwellUntil = Time.time + pp.dwellSeconds;
                if (pp.faceDirection.sqrMagnitude > 0.001f) SetFacing(pp.faceDirection);
            }
        }

        void TickInvestigate()
        {
            if (!arrivedAtPoi)
            {
                if (Arrived())
                {
                    arrivedAtPoi = true;
                    lookUntil = Time.time + lookAroundSeconds;
                }
                return;
            }
            // Look around: slow turn.
            SetFacing(Quaternion.Euler(0f, 110f * Time.deltaTime, 0f) * transform.forward);
            if (Time.time >= lookUntil) ResumePatrol();
        }

        void TickChase(bool sees)
        {
            if (sees)
            {
                GoTo(player.position, chaseSpeed);
                SetFacing(player.position - transform.position);
                // The reaching pose begins at eight feet; capture still requires close contact.
                if (CanPhysicallyCatchPlayer(Mathf.Min(catchDistance,.75f))) { GameManager.Instance?.Caught(this); Freeze(); }
            }
            else if (Time.time - lastSeenTime > loseSightSeconds)
            {
                EnterSearch();
            }
            else GoTo(lastSeenPos, chaseSpeed);
        }

        void TickSearch()
        {
            if (!Arrived()) return;
            if (!arrivedAtPoi)
            {
                arrivedAtPoi = true;
                lookUntil = Time.time + lookAroundSeconds * 0.6f;
            }
            if (Time.time < lookUntil)
            {
                SetFacing(Quaternion.Euler(0f, 140f * Time.deltaTime, 0f) * transform.forward);
                return;
            }
            if (searchStep >= searchPoints) { ResumePatrol(); return; }
            searchStep++;
            Vector3 candidate = lastSeenPos + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * searchRadius;
            if (NavMesh.SamplePosition(candidate, out var hit, searchRadius, NavMesh.AllAreas)) GoTo(hit.position, investigateSpeed);
            arrivedAtPoi = false;
        }

        // ------------------------------------------------------------------ transitions

        void EnterChase()
        {
            state = State.Chase;
            dwelling = false;
            if(passCheck==null||!passCheck.WitnessedOffence){HudController.Instance?.SetStatus("The caretaker has seen you! LEG IT!", 2.5f);ShoutSpotted();}
            GoTo(player.position, chaseSpeed);
        }

        /// <summary>
        /// His one voice channel (Resources/Audio/&lt;clipName&gt;). Built on first use so it survives scene rebuilds.
        /// A normal line waits for the current one to finish plus a short gap; a priority line cuts in.
        /// </summary>
        public bool Say(string clipName,bool priority=false)
        {
            if(state==State.Frozen)return false;
            if(voice==null)
            {
                var mouth=new GameObject("Caretaker voice");mouth.transform.SetParent(transform,false);mouth.transform.localPosition=Vector3.up*1.7f;
                voice=mouth.AddComponent<AudioSource>();voice.playOnAwake=false;voice.loop=false;
                voice.spatialBlend=1;voice.dopplerLevel=0;voice.rolloffMode=AudioRolloffMode.Linear;voice.minDistance=6;voice.maxDistance=40;TalkingMouth.Register(voice);
            }
            if(!priority&&(voice.isPlaying||Time.time<nextVoiceAt))return false;
            bool dark=DarkModeDialogue.TryBark(clipName,out var line);
            var clip=dark?DarkModeDialogue.Voice(line):Resources.Load<AudioClip>("Audio/"+clipName);if(clip==null)return false;
            voice.clip=clip;voice.Play();nextVoiceAt=Time.time+clip.length+5f;
            if(dark)HudController.Instance?.SetBark(line.Caption,Mathf.Max(4,clip.length));
            return true;
        }

        /// <summary>"OI! YOU!" when he first spots you. The cooldown stops a flickering sighting repeating a 6 s shout.</summary>
        void ShoutSpotted()
        {
            if(Time.time<nextSpottedShout)return;
            if(Say("CaretakerSpotted",true))nextSpottedShout=Time.time+20f;
        }

        void EnterSearch()
        {
            state = State.Search;
            searchStep = 0;
            arrivedAtPoi = false;
            HudController.Instance?.SetStatus("He lost you. He's searching...", 2f);
            Say("CaretakerSearch");
            GoTo(lastSeenPos, investigateSpeed);
        }

        void ResumePatrol()
        {
            state = State.Patrol;
            dwelling = false;
            hasDesiredFacing = false;
            if (patrol.Count > 0) GoTo(patrol[patrolIndex].point.position, patrolSpeed);
        }

        /// <summary>Resumes patrol from a specific stop instead of wherever it last was (or index 0 on a fresh scene load).</summary>
        public void ResumePatrolFrom(int index, float graceSeconds)
        {
            if (patrol.Count > 0) patrolIndex = ((index % patrol.Count) + patrol.Count) % patrol.Count;
            ResumeAfterDetention(graceSeconds);
        }
        public void ResumeAfterDetention(float graceSeconds)
        {
            suspicion = 0f;
            ignorePlayerUntil = Time.time + graceSeconds;
            cutoutMotion?.SetFrozen(IsGlued);
            ResumePatrol();
        }
        public void StartSchoolRoutine(){patrolIndex=0;ResumeAfterDetention(0);}
        public void SetRunPressure(int recovered)
        {
            sightRange = 24f; sightConeDegrees = 95f;
            patrolSpeed = 1.7f + recovered * .12f;
            chaseSpeed = 3.05f + recovered * .12f;
            investigateSpeed = 2.4f + recovered * .1f;
            lookAroundSeconds = 4 + recovered;
            searchPoints = 2 + recovered;
            // Early on, a spotted glimpse gives a beat to duck out of sight; by five of five it's the tight base reaction time.
            suspicionSeconds = Mathf.Lerp(1.4f, 0.6f, recovered / 5f);
            foreach (var point in patrol) point.dwellSeconds = Mathf.Max(2, 7 - recovered);
        }
        public void InvestigateArea(Vector3 approximate)
        {
            if (state != State.Patrol || Time.time < ignorePlayerUntil || !NavMesh.SamplePosition(approximate, out var hit, 12, NavMesh.AllAreas)) return;
            OnNoise(hit.position, 300, "search sweep");
        }
        public void HearSchoolWideAlarm(Vector3 position)
        {
            if(IsGlued||state==State.Chase||state==State.Frozen)return;
            if(state==State.Investigate&&Investigating(position))return;
            // A deliberate alarm is unmissable and overrides the short grace used after detention.
            ignorePlayerUntil=0;
            OnNoise(position,float.MaxValue,"copycat");
        }
        public void PauseForPass(bool pause){if(agent.isOnNavMesh&&(pause||state!=State.Frozen))agent.isStopped=pause||IsGlued;}
        public void ApproachForPass(Vector3 position){GoTo(position,chaseSpeed);}
        public float PassApproachMemorySeconds=>loseSightSeconds;
        public void EndPassApproach(){PauseForPass(false);if(state!=State.Frozen)ResumePatrol();}
        public void PursuePlayerForOffence()
        {
            if(player==null||state==State.Frozen||GameManager.Instance==null||!GameManager.Instance.IsPlaying)return;
            lastSeenTime=Time.time;lastSeenPos=player.position;suspicion=1f;EnterChase();
        }

        void OnNoise(Vector3 pos, float radius, string source)
        {
            if (IsGlued || state == State.Chase || state == State.Frozen || Time.time < ignorePlayerUntil) return;
            if (Vector3.Distance(transform.position, pos) > radius) return;
            state = State.Investigate;
            dwelling = false;
            arrivedAtPoi = false;
            pointOfInterest = pos;
            if (NavMesh.SamplePosition(pos, out var hit, 2f, NavMesh.AllAreas)) pointOfInterest = hit.position;
            HudController.Instance?.SetStatus(source == "phone" ? "He heard the phone!" : source == "search sweep" ? "He's checking the area. Stay out of sight." : "He heard something...", 2f);
            if (source != "search sweep") Say(heardTake++ % 2 == 0 ? "CaretakerWhosThere" : "CaretakerHeardThat");
            GoTo(pointOfInterest, investigateSpeed);
        }

        // ------------------------------------------------------------------ helpers

        bool CanSeePlayer()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 target = player.position + Vector3.up * 1.2f;
            Vector3 to = target - eye;
            float dist = to.magnitude;
            if (dist > sightRange) return false;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            var office=GameManager.Instance?.officeMission;
            bool inOwnOffice=office!=null&&office.officeBounds.Contains(transform.position)&&office.officeBounds.Contains(player.position);
            // A pupil walking right beside him, or into his occupied office, is noticeable even behind his patrol heading.
            if (flat.magnitude>2.4f&&!inOwnOffice&&Vector3.Angle(transform.forward, flat) > sightConeDegrees * 0.5f) return false;
            if (Physics.Raycast(eye, to / dist, out var hit, dist, sightBlockers, QueryTriggerInteraction.Ignore))
                return hit.transform == player || hit.transform.IsChildOf(player);
            return true;
        }

        /// <summary>
        /// Sight and physical reach are deliberately separate. Windows may allow vision, but a catch also needs
        /// an unobstructed torso-level line and a short direct route on the NavMesh.
        /// </summary>
        public bool CanPhysicallyCatchPlayer(float maxDistance)
        {
            if(IsGlued)return false;
            if(player==null)
            {
                var found=GameObject.FindGameObjectWithTag("Player");
                if(found==null)return false;
                player=found.transform;
            }
            Vector3 flatDelta=player.position-transform.position;flatDelta.y=0;
            if(flatDelta.sqrMagnitude>maxDistance*maxDistance)return false;

            Vector3 origin=transform.position+Vector3.up*.82f;
            Vector3 target=player.position+Vector3.up*.82f;
            Vector3 delta=target-origin;float distance=delta.magnitude;
            if(distance<.001f)return true;
            int count=Physics.RaycastNonAlloc(origin,delta/distance,catchHits,distance+.05f,sightBlockers,QueryTriggerInteraction.Ignore);
            float nearest=float.MaxValue;Transform first=null;
            for(int i=0;i<count;i++)
            {
                var hit=catchHits[i];if(hit.collider==null||hit.transform==transform||hit.transform.IsChildOf(transform))continue;
                if(hit.distance<nearest){nearest=hit.distance;first=hit.transform;}
            }
            if(first==null||!(first==player||first.IsChildOf(player)))return false;

            if(!NavMesh.SamplePosition(transform.position,out var from,.3f,NavMesh.AllAreas)||
               !NavMesh.SamplePosition(player.position,out var to,.3f,NavMesh.AllAreas))return false;
            if(NavMesh.Raycast(from.position,to.position,out _,NavMesh.AllAreas))return false;
            var path=new NavMeshPath();
            if(!NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path)||
               path.status!=NavMeshPathStatus.PathComplete)return false;
            float route=0;
            for(int i=1;i<path.corners.Length;i++)route+=Vector3.Distance(path.corners[i-1],path.corners[i]);
            return route<=maxDistance+.35f;
        }

        void GoTo(Vector3 pos, float speed)
        {
            if (!agent.isOnNavMesh) return;
            agent.isStopped = IsGlued;
            agent.speed = speed;
            agent.SetDestination(pos);
            hasDesiredFacing = false;
            lastGoToTime = Time.time;
            pathTimeEstimated = false;
            plannedTravelTimeout = TravelTimeout;
        }

        /// <summary>
        /// True once the agent is at its destination. Guards against the frame right after SetDestination (stale
        /// remainingDistance) and gives up after a timeout so an unreachable point cannot stall a state forever.
        /// </summary>
        bool Arrived()
        {
            if (!agent.isOnNavMesh || agent.pathPending) return false;
            if (!pathTimeEstimated && agent.hasPath)
            {
                var corners = agent.path.corners;
                float length = 0f;
                for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
                plannedTravelTimeout = Mathf.Max(TravelTimeout, length / Mathf.Max(agent.speed, .1f) + 8f);
                pathTimeEstimated = true;
            }
            if (Time.time - lastGoToTime < 0.3f) return false;
            if (Time.time - lastGoToTime > plannedTravelTimeout) return true;
            if (!agent.hasPath) return Vector3.Distance(transform.position, agent.destination) < 0.6f;
            return agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, 0.35f);
        }

        void SetFacing(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            desiredFacing = dir.normalized;
            hasDesiredFacing = true;
        }

        void TickRotation()
        {
            Vector3 dir = hasDesiredFacing ? desiredFacing : agent.desiredVelocity;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            var target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeedDeg * Time.deltaTime);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.3f);
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 l = Quaternion.Euler(0f, -sightConeDegrees * 0.5f, 0f) * transform.forward * sightRange;
            Vector3 r = Quaternion.Euler(0f, sightConeDegrees * 0.5f, 0f) * transform.forward * sightRange;
            Gizmos.DrawLine(eye, eye + l);
            Gizmos.DrawLine(eye, eye + r);
        }
    }
}

using UnityEngine;
using UnityEngine.AI;

namespace Confiscated
{
    /// <summary>Whistles, charges between corridor ends, and sends a spotted pupil down one straight stretch.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PeCoach : MonoBehaviour
    {
        [System.Serializable]
        public struct Corridor
        {
            public string name;
            public Vector2 min, max; // World X and Z bounds.
        }

        public Transform[] lap;
        public Corridor[] corridors;
        public Renderer cutout;
        public Texture2D idleArt, walkArt;
        public AudioClip command, warningWhistle;
        public AudioClip[] steps;
        public float sightRange = 9f, patrolSpeed = 6.4f, coachingSpeed = 6.4f;
        public float pupilSpeed = 5.8f, cooldownSeconds = 10f;
        public float corridorStopSeconds = 2.5f;
        public const string ExerciseCommand = "Oi Smith, you need some exercise, run a length with me!";
        public bool Approaching { get; private set; }
        public bool AvoidingPlayer => Time.time < cooldownUntil;
        public bool Recovering => patrolStarted && patrolPhase == PatrolPhase.Stop && !Coaching && !Approaching;
        public float LastDrillSeconds { get; private set; }
        public bool Coaching { get; private set; }
        public bool Charging => patrolPhase == PatrolPhase.Charge && !Coaching && !Approaching;
        public bool Whistling => patrolPhase == PatrolPhase.Whistle && !Coaching && !Approaching;
        public int ChargesStarted { get; private set; }
        public int CorridorStops { get; private set; }
        public int DrillsStarted { get; private set; }
        public int DrillsFinished { get; private set; }
        public bool Paused { get; private set; }
        public Vector3 DrillDirection { get; private set; }
        public float DrillLength { get; private set; }

        NavMeshAgent agent;
        FirstPersonController pupil;
        AudioSource voice, feet;
        MaterialPropertyBlock appearance;
        Texture2D rearIdleArt, rearWalkArt, rearWalkDownArt, recoveryArt;
        Vector3 cutoutScale, cutoutPosition;
        Vector3[] fixedLap;
        float drillStartedAt, approachDeadline, lastProgressAt;
        Vector3 progressPosition;
        int lapIndex, stepIndex;
        enum PatrolPhase { Stop, Whistle, Charge }
        PatrolPhase patrolPhase;
        float nextLook, nextFoot, phaseEndsAt, cooldownUntil, coachingTimeout;
        bool patrolStarted;
        bool chargeHasTarget;
        Vector3 chargeEnd;
        Vector3 previous;

        public void Pause() { Paused = true; if (Coaching || Approaching) FinishDrill(); }
        public void Resume() { Paused = false; nextLook = Time.time + 1f; }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.spatialBlend = 1; voice.minDistance = 3; voice.maxDistance = 32;
            voice.rolloffMode = AudioRolloffMode.Linear; voice.volume = .85f;
            feet = gameObject.AddComponent<AudioSource>();
            feet.playOnAwake = false; feet.spatialBlend = 1; feet.minDistance = 5; feet.maxDistance = 40;
            feet.rolloffMode = AudioRolloffMode.Linear; feet.volume = .85f;
            appearance = new MaterialPropertyBlock(); previous = transform.position;
            rearIdleArt = Resources.Load<Texture2D>("Art/CoachRearIdle");
            rearWalkArt = Resources.Load<Texture2D>("Art/CoachRearWalk");
            rearWalkDownArt = Resources.Load<Texture2D>("Art/CoachRearWalkDown");
            recoveryArt = Resources.Load<Texture2D>("Art/CoachRecovery");
            var exerciseVoice = Resources.Load<AudioClip>("Audio/CoachExerciseCommand");
            if (exerciseVoice != null) command = exerciseVoice;
            // Older scenes saved a 34-second cooldown and a barely visible rest.
            cooldownSeconds = 10f; corridorStopSeconds = 2.5f;
            fixedLap = new Vector3[lap != null ? lap.Length : 0];
            for (int i = 0; i < fixedLap.Length; i++) fixedLap[i] = lap[i] != null ? lap[i].position : transform.position;
            if (cutout != null) { cutoutScale = cutout.transform.localScale; cutoutPosition = cutout.transform.localPosition; }
        }

        void OnDisable()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            Coaching = false;
            Approaching = false;
            pupil?.StopForcedCorridorRun();
            if (voice != null) voice.Stop();
            if (feet != null) feet.Stop();
            if (cutout != null) { cutout.transform.localScale = cutoutScale; cutout.transform.localPosition = cutoutPosition; }
        }

        void Update()
        {
            var run = SchoolRunController.Instance;
            bool live = !Paused && run != null && GameManager.Instance != null && GameManager.Instance.IsPlaying;
            if (!live)
            {
                if (Coaching || Approaching) FinishDrill();
                if (agent.isOnNavMesh) agent.isStopped = true;
                SetArt(false);
                previous = transform.position;
                return;
            }
            if (ComicDialogue.IsActive || Time.timeScale <= 0)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                SetArt(false);
                previous = transform.position;
                return;
            }
            if (pupil == null) pupil = run.period.Player.GetComponent<FirstPersonController>();
            if (!agent.isOnNavMesh)
            {
                if (!NavMesh.SamplePosition(transform.position, out var spawn, 2f, NavMesh.AllAreas) || !agent.Warp(spawn.position)) return;
            }
            if (Approaching)
            {
                if (Time.time > approachDeadline || pupil.MovementLocked || pupil.IsFallen || !TryCorridor(out var direction, out var length))
                    FinishDrill();
                else
                {
                    DrillDirection = direction; DrillLength = length;
                    agent.isStopped = false; agent.speed = Mathf.Max(coachingSpeed, pupilSpeed + 2f); agent.stoppingDistance = .05f;
                    FollowPupil();
                    Vector3 gap = pupil.transform.position - transform.position; gap.y = 0;
                    if (gap.magnitude <= 1.15f && Vector3.Dot(gap, direction) > .25f)
                        BeginDrill(direction, length);
                }
            }
            else if (Coaching)
            {
                if (agent.isOnNavMesh) agent.isStopped = false;
                if (!pupil.ForcedCorridorRun || Time.time > coachingTimeout) FinishDrill();
                else FollowPupil();
            }
            else
            {
                Patrol();
                if (Time.time >= cooldownUntil && Time.time >= nextLook)
                {
                    nextLook = Time.time + .18f;
                    if (CanSeePupil() && TryCorridor(out var direction, out var distance))
                        BeginApproach(direction, distance);
                }
            }
            AnimateSteps();
        }

        void Patrol()
        {
            if (!agent.isOnNavMesh) return;
            if (!patrolStarted)
            {
                patrolStarted = true;
                BeginWhistle();
            }
            if (patrolPhase == PatrolPhase.Stop)
            {
                agent.isStopped = true;
                if (Time.time >= phaseEndsAt) BeginWhistle();
                return;
            }
            if (patrolPhase == PatrolPhase.Whistle)
            {
                agent.isStopped = true;
                if (Time.time >= phaseEndsAt) BeginCharge();
                return;
            }
            agent.isStopped = false;
            agent.speed = patrolSpeed;
            if (!chargeHasTarget) SetLapDestination();
            if (chargeHasTarget && !agent.pathPending && Vector3.Distance(transform.position, chargeEnd) < .35f)
            {
                agent.ResetPath();
                chargeHasTarget = false;
                patrolPhase = PatrolPhase.Stop;
                CorridorStops++;
                phaseEndsAt = Time.time + corridorStopSeconds;
                return;
            }
            if (chargeHasTarget && !agent.hasPath && !agent.pathPending) agent.SetDestination(chargeEnd);
            if (Vector3.Distance(progressPosition, transform.position) > .3f)
            { progressPosition = transform.position; lastProgressAt = Time.time; }
            if (!agent.pathPending && (agent.pathStatus == NavMeshPathStatus.PathInvalid || Time.time - lastProgressAt > 3f))
            { chargeHasTarget = false; SetLapDestination(); lastProgressAt = Time.time; }
        }

        void BeginWhistle()
        {
            patrolPhase = PatrolPhase.Whistle;
            phaseEndsAt = Time.time + (warningWhistle != null ? warningWhistle.length : .5f);
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            if (warningWhistle != null) voice.PlayOneShot(warningWhistle, .8f);
            NoiseEvents.Emit(transform.position, 24f, "coach whistle");
        }

        void BeginCharge()
        {
            patrolPhase = PatrolPhase.Charge;
            ChargesStarted++;
            progressPosition = transform.position; lastProgressAt = Time.time;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                if (chargeHasTarget) agent.SetDestination(chargeEnd);
                else SetLapDestination();
            }
        }

        void SetLapDestination()
        {
            // Prefer one straight end-to-end corridor, rather than chasing child transforms
            // that move with this actor or taking a diagonal shortcut around the lap.
            Vector3 origin = transform.position;
            float best = float.NegativeInfinity;
            Vector3 selected = origin;
            if (corridors != null) foreach (var lane in corridors)
            {
                if (origin.x < lane.min.x - .2f || origin.x > lane.max.x + .2f || origin.z < lane.min.y - .2f || origin.z > lane.max.y + .2f) continue;
                bool alongX = lane.max.x - lane.min.x > lane.max.y - lane.min.y;
                for (int end = 0; end < 2; end++)
                {
                    Vector3 target = origin;
                    if (alongX) target.x = end == 0 ? lane.min.x + .9f : lane.max.x - .9f;
                    else target.z = end == 0 ? lane.min.y + .9f : lane.max.y - .9f;
                    float distance = Vector3.Distance(origin, target);
                    if (distance < 5f || !NavMesh.SamplePosition(target, out var hit, 1f, NavMesh.AllAreas) || NavMesh.Raycast(origin, hit.position, out _, NavMesh.AllAreas)) continue;
                    float score = distance;
                    if (AvoidingPlayer && pupil != null) score += Vector3.Distance(target, pupil.transform.position);
                    if (score > best) { best = score; selected = hit.position; }
                }
            }
            if (best > float.NegativeInfinity)
            { chargeEnd = selected; chargeHasTarget = agent.SetDestination(chargeEnd); return; }
            for (int i = 0; i < fixedLap.Length; i++)
            {
                int index = (lapIndex + 1 + i) % fixedLap.Length;
                if (!NavMesh.SamplePosition(fixedLap[index], out var hit, 1.5f, NavMesh.AllAreas)) continue;
                if (Vector3.Distance(transform.position, hit.position) < 1.2f) continue;
                var path = new NavMeshPath();
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                lapIndex = index;
                chargeEnd = hit.position;
                chargeHasTarget = agent.SetDestination(chargeEnd);
                return;
            }
        }

        bool CanSeePupil()
        {
            if (pupil == null || pupil.MovementLocked || pupil.IsFallen || pupil.ForcedCorridorRun) return false;
            Vector3 from = transform.position + Vector3.up * 1.65f;
            Vector3 to = pupil.transform.position + Vector3.up * 1.1f;
            Vector3 delta = to - from;
            if (delta.sqrMagnitude > sightRange * sightRange || delta.sqrMagnitude < .01f) return false;
            Vector3 flat = delta; flat.y = 0;
            if (flat.magnitude > 2.2f && Vector3.Angle(transform.forward, flat) > 65f) return false;
            var hits = Physics.RaycastAll(from, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue; Transform first = null;
            foreach (var h in hits)
            {
                if (h.transform == transform || h.transform.IsChildOf(transform)) continue;
                if (h.distance < nearest) { nearest = h.distance; first = h.transform; }
            }
            return first == null || first == pupil.transform || first.IsChildOf(pupil.transform);
        }

        public bool TryCorridor(out Vector3 direction, out float metres)
        {
            direction = Vector3.zero; metres = 0;
            if (pupil == null)
            {
                var player = SchoolRunController.Instance?.period?.Player;
                if (player == null) return false;
                pupil = player.GetComponent<FirstPersonController>();
                if (pupil == null) return false;
            }
            Vector3 pos = pupil.transform.position;
            float best = float.NegativeInfinity;
            if (corridors == null) return false;
            foreach (var lane in corridors)
            {
                if (pos.x < lane.min.x + .55f || pos.x > lane.max.x - .55f ||
                    pos.z < lane.min.y + .55f || pos.z > lane.max.y - .55f) continue;
                bool alongX = lane.max.x - lane.min.x > lane.max.y - lane.min.y;
                float coordinate = alongX ? pos.x : pos.z;
                float min = alongX ? lane.min.x : lane.min.y;
                float max = alongX ? lane.max.x : lane.max.y;
                float fromCoach = alongX ? pos.x - transform.position.x : pos.z - transform.position.z;
                foreach (int sign in new[] { fromCoach >= 0 ? 1 : -1, fromCoach >= 0 ? -1 : 1 })
                {
                    float length = sign > 0 ? max - coordinate - .9f : coordinate - min - .9f;
                    if (length < 8f) continue;
                    Vector3 dir = alongX ? Vector3.right * sign : Vector3.forward * sign;
                    var end = pos + dir * length;
                    if (NavMesh.Raycast(pos, end, out _, NavMesh.AllAreas)) continue;
                    float score = length + (Mathf.Sign(fromCoach) == sign ? 3 : 0) -
                        (alongX ? lane.max.y - lane.min.y : lane.max.x - lane.min.x) * .15f;
                    if (score > best) { best = score; direction = dir; metres = length; }
                }
            }
            return metres >= 8f;
        }

        void BeginDrill(Vector3 direction, float metres)
        {
            // Budget the full distance across at least five seconds, including short halls.
            float speed = Mathf.Min(pupilSpeed, metres / 5.1f);
            if (!pupil.StartForcedCorridorRun(direction, metres, speed, transform)) { FinishDrill(); return; }
            Approaching = false;
            Coaching = true; DrillsStarted++; DrillDirection = direction; DrillLength = metres;
            drillStartedAt = Time.time;
            coachingTimeout = Time.time + metres / speed + 15f;
            agent.speed = Mathf.Max(coachingSpeed, speed + 1.5f);
            FollowPupil();
        }

        void BeginApproach(Vector3 direction, float metres)
        {
            Approaching = true; DrillDirection = direction; DrillLength = metres;
            approachDeadline = Time.time + 15f;
            if (command != null) voice.PlayOneShot(command, 1f);
            HudController.Instance?.SetBark("Coach: " + ExerciseCommand, 6f);
            NoiseEvents.Emit(transform.position, 24f, "coach exercise shout");
        }

        void FollowPupil()
        {
            Vector3 behind = pupil.transform.position - DrillDirection * .7f;
            if (NavMesh.SamplePosition(behind, out var hit, .8f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        }

        void FinishDrill()
        {
            if (!Coaching && !Approaching) return;
            if (Coaching) { DrillsFinished++; LastDrillSeconds = Time.time - drillStartedAt; }
            Coaching = false; Approaching = false;
            pupil?.StopForcedCorridorRun();
            cooldownUntil = Time.time + cooldownSeconds;
            patrolPhase = PatrolPhase.Stop;
            chargeHasTarget = false;
            phaseEndsAt = Time.time + corridorStopSeconds;
            agent.speed = patrolSpeed;
            agent.stoppingDistance = .2f;
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            HudController.Instance?.SetStatus("Lap done. Keep moving!", 2);
        }

        void AnimateSteps()
        {
            Vector3 delta = transform.position - previous; previous = transform.position; delta.y = 0;
            float speed = Time.deltaTime > 0 ? delta.magnitude / Time.deltaTime : 0;
            bool moving = speed > .25f && speed < 12f;
            SetArt(moving);
            if (!moving || Time.time < nextFoot || steps == null || steps.Length == 0) return;
            float interval = .22f;
            nextFoot = Time.time + interval;
            var clip = steps[stepIndex % steps.Length]; stepIndex++;
            if (clip != null) feet.PlayOneShot(clip, .8f);
        }

        void SetArt(bool walking)
        {
            if (cutout == null) return;
            int frame = walking ? stepIndex % 2 : -1;
            bool down = walking && Time.time < nextFoot - .11f;
            var viewer = Camera.main;
            // CanSeePupil uses this same forward vector for the coach's field of view.
            bool rear = !ComicDialogue.IsAddressingPlayer(transform) && viewer != null && Vector3.Dot(transform.forward, viewer.transform.position - transform.position) < 0f;
            bool resting = Recovering && recoveryArt != null;
            float breath = resting ? 1f + Mathf.Sin(Time.time * 6f) * .015f : 1f;
            float height = cutoutScale.y * (resting ? .78f : 1f) * breath;
            cutout.transform.localScale = new Vector3(cutoutScale.x, height, cutoutScale.z);
            cutout.transform.localPosition = cutoutPosition + Vector3.up * ((height - cutoutScale.y) * .5f - (resting ? height * .113f : 0f));
            cutout.GetPropertyBlock(appearance);
            appearance.SetFloat("_PoseWidth", resting ? .72f : 1f);
            appearance.SetTexture("_BaseMap", rear && (frame < 0 ? rearIdleArt != null : rearWalkArt != null)
                ? (frame < 0 ? rearIdleArt : down && rearWalkDownArt != null ? rearWalkDownArt : rearWalkArt)
                : walking && walkArt != null ? walkArt : idleArt);
            appearance.SetVector("_BaseMap_ST", frame < 0 ? new Vector4(1, 1, 0, 0) :
                new Vector4(.5f, 1, frame == 0 ? 0 : .5f, 0));
            if (resting)
            {
                appearance.SetTexture("_BaseMap", recoveryArt);
                appearance.SetVector("_BaseMap_ST", new Vector4(.5f, 1, rear ? .5f : 0f, 0));
            }
            cutout.SetPropertyBlock(appearance);
        }
    }
}

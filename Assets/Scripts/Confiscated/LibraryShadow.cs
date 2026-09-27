using UnityEngine;
using UnityEngine.AI;
namespace Confiscated
{
    /// <summary>
    /// The shadow in the library maze (Docs/LibraryMaze.md). It drifts from alcove to alcove along the library floor. It
    /// notices a player who moves near it, or who has a torch on, in its line of sight: it stops, turns and shushes. Freeze
    /// (still, torch off) until the shushing ends and it drifts on past; keep moving or keep the light on and it hunts,
    /// faster than a walk, until it catches the player (thrown out of the nearest door; the handheld game goes back in its box) or the
    /// player leaves the library. The caretaker is never told. It never leaves the library's NavMesh area.
    /// </summary>
    public sealed class LibraryShadow : MonoBehaviour
    {
        public enum Phase { Wander, Linger, Notice, Hunt, Gone }
        public Transform[] alcoves;
        [Tooltip("Just inside each library door; exits[i] is just outside the same door.")]
        public Transform[] entrances,exits;
        public Renderer[] silhouette;
        public Renderer[] eyes;
        [Tooltip("Dark smoke that rolls off it and trails behind as it moves.")]
        public ParticleSystem smoke;
        [Tooltip("Rigged figure: Drift while wandering, Hunt (bool \"Hunting\") while it comes for you.")]
        public Animator animator;
        public Bounds room;
        public int areaMask=1<<3;
        public float wanderSpeed=1.1f,huntSpeed=3.3f,noticeSeconds=1.3f,reactionSeconds=.3f,lingerSeconds=3;
        public float moveSense=8,lightSense=14,movingSpeed=.35f,catchRadius=.75f,goneSeconds=2.5f;
        public const string CatchLine="GET OUT!";
        /// <summary>The library's own belonging: a catch puts it back in its box on the returns desk.</summary>
        public const int LibraryItem=2;
        public int ReturnedItems {get;private set;}
        public Phase Current {get;private set;}
        public int Catches {get;private set;}
        public int Notices {get;private set;}
        public int PassedBy {get;private set;}
        /// <summary>Tests (and the autopilot) that are about something else in the library hold it still.</summary>
        public bool Paused {get;set;}
        public float PlayerSpeed {get;private set;}
        float phaseEnds,repath;int corner,target;NavMeshPath path;Material skin;AudioSource voice,breath,sting;bool wasRound,catching;
        // Sounds: its breathing (3D loop), "I see you" when it notices you, a wire-scrape sting when it comes into view,
        // and a scream for the catch jumpscare.
        AudioClip seeYou,spotted,scream;float spottedReady,outOfViewSince;bool inView;
        public const float JumpscareSeconds=.9f,SpottedCooldown=20,SpottedRange=14;
        public int Spotted {get;private set;}
        public bool Catching=>catching;
        /// <summary>The library shushes you the first time you walk in (once per session).</summary>
        public static bool Shushed {get;private set;}
        public static void ResetSession()=>Shushed=false;
        Vector3 lastPlayer;bool havePlayer;float alpha;AudioClip catchClip;

        void Start()
        {
            path=new NavMeshPath();
            if(silhouette.Length>0&&silhouette[0]!=null){skin=new Material(silhouette[0].sharedMaterial);foreach(var r in silhouette)r.sharedMaterial=skin;}
            voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=1;voice.rolloffMode=AudioRolloffMode.Linear;
            voice.minDistance=2;voice.maxDistance=20;voice.dopplerLevel=0;
            catchClip=Resources.Load<AudioClip>("Audio/LibraryShadowGetOut");
            seeYou=Resources.Load<AudioClip>("Audio/LibraryShadowISeeYou");spotted=Resources.Load<AudioClip>("Audio/LibraryShadowSpotted");scream=Resources.Load<AudioClip>("Audio/LibraryShadowScream");
            breath=gameObject.AddComponent<AudioSource>();breath.clip=Resources.Load<AudioClip>("Audio/LibraryShadowBreathLoop");breath.loop=true;breath.playOnAwake=false;
            breath.spatialBlend=1;breath.rolloffMode=AudioRolloffMode.Linear;breath.minDistance=1.5f;breath.maxDistance=14;breath.dopplerLevel=0;breath.volume=0;
            sting=gameObject.AddComponent<AudioSource>();sting.playOnAwake=false;sting.spatialBlend=0;
            Place(0);Wander();Visual(true);
        }
        void OnDestroy(){if(skin!=null)Destroy(skin);}

        void Update()
        {
            var run=SchoolRunController.Instance;
            bool round=run!=null&&run.RoundStarted;
            // A new round (or a retry) starts it over in a random alcove.
            if(round&&!wasRound){Place(Random.Range(0,alcoves.Length));Wander();}wasRound=round;
            bool live=round&&GameManager.Instance!=null&&GameManager.Instance.IsPlaying&&!Paused&&!ComicDialogue.IsActive&&Time.timeScale>0;
            if(!live){havePlayer=false;Visual(false);Breathe(false);return;}
            if(catching){Visual(false);return;}
            var player=run.period.Player;Vector3 at=player.transform.position;
            // Walking speed from the player's own movement; warps (a throw-out, a retry) are not movement.
            Vector3 step=at-lastPlayer;step.y=0;float speed=havePlayer&&Time.deltaTime>0?step.magnitude/Time.deltaTime:0;
            PlayerSpeed=speed>12?0:Mathf.Lerp(PlayerSpeed,speed,Mathf.Clamp01(Time.deltaTime*12));lastPlayer=at;havePlayer=true;
            bool inside=Inside(at);
            if(inside&&!Shushed){Shushed=true;var shush=Resources.Load<AudioClip>("Audio/LibraryShush");if(shush!=null)sting.PlayOneShot(shush,.9f);}
            // Out of the library: it forgets the player and goes back to drifting.
            if(!inside&&(Current==Phase.Notice||Current==Phase.Hunt))Wander();
            switch(Current)
            {
                case Phase.Wander:
                    if(inside&&Senses(player,at)){Notice(at);break;}
                    if(Follow(alcoves[target].position,wanderSpeed)){Current=Phase.Linger;phaseEnds=Time.time+lingerSeconds;}
                    break;
                case Phase.Linger:
                    if(inside&&Senses(player,at)){Notice(at);break;}
                    if(Time.time>=phaseEnds)Wander();
                    break;
                case Phase.Notice:
                    Face(at-transform.position);
                    // A moment to react, then any movement or light gives the player away.
                    if(Time.time>=phaseEnds-noticeSeconds+reactionSeconds&&Gives(player,at)){Hunt();break;}
                    if(Time.time>=phaseEnds){PassedBy++;Current=Phase.Wander;repath=0;}
                    break;
                case Phase.Hunt:
                    Follow(at,huntSpeed);
                    break;
                case Phase.Gone:
                    if(Time.time>=phaseEnds)Wander();
                    break;
            }
            if(Current==Phase.Hunt){Vector3 gap=at-transform.position;gap.y=0;if(gap.magnitude<catchRadius){StartCoroutine(Catch(player));return;}}
            if(inside)Sighting(player);
            Breathe(Current!=Phase.Gone);
            Visual(false);
        }

        public bool Inside(Vector3 p)=>room.Contains(new Vector3(p.x,room.center.y,p.z));
        bool TorchOn(PlayerInteractor player){var torch=player.GetComponent<PlayerTorch>();return torch!=null&&torch.IsOn;}
        /// <summary>It can see the player, and the player is moving close by or showing a light.</summary>
        public bool Senses(PlayerInteractor player,Vector3 at)
        {
            float d=Flat(at-transform.position);
            if(d>Mathf.Max(moveSense,lightSense)||!Sight(player,at))return false;
            return TorchOn(player)&&d<=lightSense||PlayerSpeed>movingSpeed&&d<=moveSense;
        }
        /// <summary>While it is shushing: any movement at all, or a light.</summary>
        bool Gives(PlayerInteractor player,Vector3 at)=>(PlayerSpeed>movingSpeed||TorchOn(player))&&Sight(player,at);
        bool Sight(PlayerInteractor player,Vector3 at)
        {
            Vector3 eye=transform.position+Vector3.up*1.6f,head=at+Vector3.up*1.5f;
            return !Physics.Linecast(eye,head,out var hit,~(1<<2),QueryTriggerInteraction.Ignore)||hit.transform.IsChildOf(player.transform)||hit.transform.IsChildOf(transform);
        }

        void Wander()
        {
            int next=target;for(int i=0;i<8&&(next==target||alcoves.Length<2);i++){next=Random.Range(0,alcoves.Length);if(alcoves.Length<2)break;}
            target=next;Current=Phase.Wander;repath=0;path?.ClearCorners();
        }
        void Notice(Vector3 at)
        {
            Notices++;Current=Phase.Notice;phaseEnds=Time.time+noticeSeconds;Face(at-transform.position);
            if(seeYou!=null){voice.pitch=1;voice.PlayOneShot(seeYou,1);}else Whisper(1,1);
        }
        void Hunt(){Current=Phase.Hunt;repath=0;phaseEnds=Time.time+1.6f;}
        /// <summary>The jumpscare: it is suddenly right in your face, screaming; then you are out of the nearest door.</summary>
        System.Collections.IEnumerator Catch(PlayerInteractor player)
        {
            catching=true;Current=Phase.Hunt;
            var body=player.GetComponent<CharacterController>();var legs=player.GetComponent<FirstPersonController>();
            if(legs!=null){legs.MovementLocked=true;legs.LookLocked=true;}
            var eye=player.ViewCamera.transform;Vector3 look=eye.forward;look.y=0;if(look.sqrMagnitude<.01f)look=transform.forward;look.Normalize();
            // Eyes level with the player's, a hand's breadth from the face.
            transform.SetPositionAndRotation(eye.position+look*.75f+Vector3.down*1.95f,Quaternion.LookRotation(-look));
            alpha=.95f;Breathe(false);
            if(scream!=null)sting.PlayOneShot(scream,1);else if(catchClip!=null)voice.PlayOneShot(catchClip,1);else TempAudio.PlayAt(TempAudio.Caught,eye.position,.8f);
            HudController.Instance?.SetBark(CatchLine,2.5f);
            yield return new WaitForSeconds(JumpscareSeconds);
            Vector3 at=player.transform.position;int door=0;float best=float.MaxValue;
            for(int i=0;i<exits.Length;i++){float d=Flat(entrances[i].position-at);if(d<best){best=d;door=i;}}
            if(SchoolRunController.Instance!=null&&SchoolRunController.Instance.ReturnToBox(LibraryItem)){ReturnedItems++;HudController.Instance?.SetStatus("The handheld game is back in its CONFISCATED box on the returns desk.",4);}
            // Out of the nearest door, facing away from it. Everything else carried is kept; the caretaker is not told.
            if(body!=null)body.enabled=false;
            Vector3 away=exits[door].position-entrances[door].position;away.y=0;
            player.transform.SetPositionAndRotation(exits[door].position,Quaternion.LookRotation(away.sqrMagnitude>.01f?away:Vector3.forward));
            if(body!=null)body.enabled=true;Physics.SyncTransforms();lastPlayer=player.transform.position;
            if(legs!=null){legs.MovementLocked=false;legs.LookLocked=false;}
            int far=0;best=-1;for(int i=0;i<alcoves.Length;i++){float d=Flat(alcoves[i].position-exits[door].position);if(d>best){best=d;far=i;}}
            Place(far);Current=Phase.Gone;phaseEnds=Time.time+goneSeconds;Catches++;catching=false;
        }
        /// <summary>A scrape of wire the moment it comes into view (on screen, unobstructed, near); then not again for a while.</summary>
        void Sighting(PlayerInteractor player)
        {
            if(Current==Phase.Gone||spotted==null)return;
            var cam=player.ViewCamera;Vector3 chest=transform.position+Vector3.up*1.4f;Vector3 v=cam.WorldToViewportPoint(chest);
            bool seen=v.z>0&&v.z<SpottedRange&&v.x>.1f&&v.x<.9f&&v.y>.05f&&v.y<.95f&&
                (!Physics.Linecast(cam.transform.position,chest,out var hit,~(1<<2),QueryTriggerInteraction.Ignore)||hit.transform.IsChildOf(transform)||hit.transform.IsChildOf(player.transform));
            if(seen&&!inView&&Time.time>=spottedReady&&Time.time-outOfViewSince>6){Spotted++;spottedReady=Time.time+SpottedCooldown;sting.PlayOneShot(spotted,.8f);}
            if(!seen&&inView)outOfViewSince=Time.time;
            inView=seen;
        }
        void Breathe(bool on)
        {
            if(breath==null||breath.clip==null)return;
            float want=on?(Current==Phase.Hunt?1f:.7f):0;
            breath.volume=Mathf.MoveTowards(breath.volume,want,Time.deltaTime*2);breath.pitch=Current==Phase.Hunt?1.15f:1;
            if(breath.volume>0&&!breath.isPlaying)breath.Play();else if(breath.volume<=0&&breath.isPlaying)breath.Pause();
        }
        /// <summary>Glides along the library floor toward a point; true once there.</summary>
        bool Follow(Vector3 goal,float speed)
        {
            if(Time.time>=repath)
            {
                repath=Time.time+(Current==Phase.Hunt?.25f:1.5f);corner=1;
                if(!NavMesh.SamplePosition(goal,out var hit,1.5f,areaMask)||!NavMesh.CalculatePath(transform.position,hit.position,areaMask,path))path.ClearCorners();
            }
            if(path.corners.Length<=corner)return Flat(goal-transform.position)<.3f||path.corners.Length==0&&Current!=Phase.Hunt;
            Vector3 next=path.corners[corner];
            transform.position=Vector3.MoveTowards(transform.position,next,speed*Time.deltaTime);
            Face(next-transform.position);
            if((transform.position-next).sqrMagnitude<.0025f)corner++;
            return false;
        }
        void Face(Vector3 heading){heading.y=0;if(heading.sqrMagnitude>.0004f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(heading),Time.deltaTime*6);}
        void Whisper(float pitch,float volume){if(voice==null)return;voice.pitch=pitch;voice.PlayOneShot(TempAudio.Whisper,volume);}
        void Place(int index)
        {
            if(alcoves==null||alcoves.Length==0)return;
            target=Mathf.Clamp(index,0,alcoves.Length-1);transform.SetPositionAndRotation(alcoves[target].position,alcoves[target].rotation);path?.ClearCorners();
        }
        void Visual(bool snap)
        {
            float want=Current==Phase.Gone?0:Current==Phase.Hunt?.95f:Current==Phase.Notice?.85f:.7f;
            alpha=snap?want:Mathf.MoveTowards(alpha,want,Time.deltaTime*(Current==Phase.Gone?6:2));
            if(skin!=null){var c=skin.GetColor("_BaseColor");c.a=alpha;skin.SetColor("_BaseColor",c);}
            foreach(var r in silhouette)if(r!=null)r.enabled=alpha>.01f;
            if(animator!=null&&animator.runtimeAnimatorController!=null)animator.SetBool("Hunting",Current==Phase.Hunt);
            if(smoke!=null)
            {
                var emission=smoke.emission;emission.enabled=Current!=Phase.Gone;
                emission.rateOverTimeMultiplier=Current==Phase.Hunt?120:Current==Phase.Notice?85:60;
            }
            // Eyes stay lit (so it can be spotted in the dark) and blink now and then.
            bool blink=Current!=Phase.Hunt&&Mathf.PerlinNoise(Time.time*1.3f,transform.position.x)>.8f;
            foreach(var e in eyes)if(e!=null)e.enabled=Current!=Phase.Gone&&!blink;
        }
        static float Flat(Vector3 v){v.y=0;return v.magnitude;}
    }
}

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
namespace Confiscated
{
    /// <summary>
    /// Photo day. He ambles the corridors with a camera round his neck. When he spots a pupil ahead he stops, calls
    /// "Class photo!", and a moment later the flash goes: a pupil still in front of him and looking his way is blinded
    /// for blindSeconds (full white, clearing over the last second). Dodge it by getting out of his view while he lines
    /// it up, or by looking away: a flash only blinds the eyes that are on it. A member of staff caught in the flash is
    /// dazzled and rooted for flashStallSeconds, like glue, so he is a tool as well as a trap; and once the run is on,
    /// your photo tells the caretaker exactly where you were.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SchoolPhotographer : MonoBehaviour
    {
        [Tooltip("Corridor rectangles (x/z, world) he wanders between. Left empty, he borrows the PE coach's.")]
        public Rect[] corridors;
        public float walkSpeed=1.4f,spotRange=9f,warningSeconds=1.1f,blindSeconds=4f,flashStallSeconds=4f,cooldownSeconds=12f,flashNoiseRadius=20f;
        [Tooltip("He looks all round him: a pupil anywhere within spotRange with a clear line to him is spotted (180 = all directions).")]
        public float spotHalfAngle=180f;
        [Tooltip("How often a wander goes to a corridor crossing rather than a random stretch, and how long he lingers there looking round.")]
        [Range(0,1)] public float junctionBias=.7f;public float lingerSeconds=6f;
        [Tooltip("The flash only blinds a pupil whose view is within this many degrees of him: look away and it misses your eyes.")]
        public float blindHalfAngle=70f;
        public Light flashLight;
        public Renderer cutout;
        public Texture2D photoPose,walkLeft,walkRight;
        [Min(.1f)] public float strideLength=.9f;
        MaterialPropertyBlock poseProperties;Vector3 previousPosition;float strideDistance;Texture currentPose;
        AudioClip shutterFlash;AudioClip[] calls;AudioClip lovely,whereDidHeGo;
        public int Flashes {get;private set;}
        public int PlayerPhotos {get;private set;}
        public int StaffDazzled {get;private set;}
        public bool LiningUp {get;private set;}
        public bool PlayerBlinded=>Time.time<blindUntil;
        public float NextPhotoIn=>Mathf.Max(0,readyAt-Time.time);
        NavMeshAgent agent;AudioSource voice,sfx;Image whiteout;float blindUntil,readyAt,flashAt,wanderUntil,lingerUntil;Vector3 goal;bool haveGoal,goalIsJunction;int patter;
        readonly System.Collections.Generic.List<Vector3> junctions=new();
        public int JunctionCount=>junctions.Count;
        public bool Lingering=>Time.time<lingerUntil;
        static readonly string[] Patter={"Class photo!","Class photo! Big smile!","Class photo! Eyes to me!","Class photo! Hold it..."};

        void Awake(){agent=GetComponent<NavMeshAgent>();agent.speed=walkSpeed;agent.angularSpeed=240;agent.acceleration=6;agent.stoppingDistance=.3f;agent.autoBraking=true;}
        void Start()
        {
            previousPosition=transform.position;
            shutterFlash=Resources.Load<AudioClip>("Audio/PhotographerShutterFlash");
            calls=new AudioClip[4];var names=new[]{"PhotographerClassPhoto","PhotographerBigSmile","PhotographerEyesToMe","PhotographerHoldIt"};
            for(int i=0;i<calls.Length;i++)calls[i]=Resources.Load<AudioClip>("Audio/"+names[i]);
            lovely=Resources.Load<AudioClip>("Audio/PhotographerLovely");whereDidHeGo=Resources.Load<AudioClip>("Audio/PhotographerWhereDidHeGo");
            sfx=SchoolAudio.Create(gameObject,SchoolAudio.Channel.Effects,true);sfx.spatialBlend=1;sfx.minDistance=3;sfx.maxDistance=30;sfx.dopplerLevel=0;sfx.playOnAwake=false;
            voice=SchoolAudio.Create(gameObject,SchoolAudio.Channel.Voice,true);voice.spatialBlend=1;voice.minDistance=3;voice.maxDistance=24;voice.dopplerLevel=0;voice.playOnAwake=false;TalkingMouth.Register(voice);
            if(corridors==null||corridors.Length==0){var coach=FindAnyObjectByType<PeCoach>();if(coach!=null&&coach.corridors!=null)corridors=System.Array.ConvertAll(coach.corridors,c=>Rect.MinMaxRect(c.min.x,c.min.y,c.max.x,c.max.y));}
            FindJunctions();
            readyAt=Time.time+4f;
        }
        /// <summary>Where two corridors overlap is a crossing: the best place to catch pupils going past.</summary>
        void FindJunctions()
        {
            junctions.Clear();if(corridors==null)return;
            for(int i=0;i<corridors.Length;i++)for(int j=i+1;j<corridors.Length;j++)
            {
                var a=corridors[i];var b=corridors[j];
                float x0=Mathf.Max(a.xMin,b.xMin),x1=Mathf.Min(a.xMax,b.xMax),z0=Mathf.Max(a.yMin,b.yMin),z1=Mathf.Min(a.yMax,b.yMax);
                if(x1-x0<1f||z1-z0<1f)continue;
                var centre=new Vector3((x0+x1)*.5f,0,(z0+z1)*.5f);
                if(NavMesh.SamplePosition(centre,out var hit,1.5f,NavMesh.AllAreas))junctions.Add(hit.position);
            }
        }
        // From the newsletter errand on: photo day is already under way when Smith first reaches the corridors.
        bool Live
        {
            get
            {
                var run=SchoolRunController.Instance;var gm=GameManager.Instance;
                return run!=null&&gm!=null&&gm.IsPlaying&&!ComicDialogue.IsActive&&Time.timeScale>0&&run.period!=null&&(run.RoundStarted||run.period.IsRoaming);
            }
        }
        /// <summary>In front of him, in range, nothing solid in the way.</summary>
        public bool InFrame(Transform subject,float headHeight)
        {
            Vector3 flat=subject.position-transform.position;flat.y=0;
            if(flat.magnitude>spotRange)return false;
            if(flat.magnitude>.6f&&Vector3.Angle(transform.forward,flat)>spotHalfAngle)return false;
            Vector3 eye=transform.position+Vector3.up*1.5f,target=subject.position+Vector3.up*Mathf.Min(headHeight*.85f,1.2f);
            return !Physics.Linecast(eye,target,out var hit,~(1<<2),QueryTriggerInteraction.Ignore)||hit.transform.IsChildOf(subject)||hit.transform.IsChildOf(transform);
        }
        /// <summary>The pupil's eyes are on him: the flash lands. Facing away, it doesn't.</summary>
        public bool LookingAtHim(PlayerInteractor player)
        {
            Vector3 toHim=transform.position+Vector3.up*1.3f-player.ViewCamera.transform.position;
            return Vector3.Angle(player.ViewCamera.transform.forward,toHim)<=blindHalfAngle;
        }
        void Update()
        {
            if(flashLight!=null)flashLight.intensity=Mathf.MoveTowards(flashLight.intensity,0,Time.deltaTime*240);
            // Blinding: solid white, then it clears over the last second.
            if(whiteout!=null)whiteout.color=new Color(1,1,1,Mathf.Clamp01((blindUntil-Time.time)/1f));
            if(!Live){if(agent.isOnNavMesh)agent.isStopped=true;LiningUp=false;return;}
            var run=SchoolRunController.Instance;var player=run.period.Player;var legs=player.GetComponent<FirstPersonController>();
            float head=legs!=null?legs.Controller.height:FirstPersonController.ChildBodyHeight;
            if(LiningUp)
            {
                if(agent.isOnNavMesh)agent.isStopped=true;
                Face(player.transform.position);
                if(Time.time>=flashAt){Flash(run,player,head);LiningUp=false;readyAt=Time.time+cooldownSeconds;haveGoal=false;}
                return;
            }
            if(Time.time>=readyAt&&InFrame(player.transform,head)&&!(legs!=null&&legs.IsFallen))
            {
                // Spotted: he stops and lines it up. The call is the warning; get out of his view or get your head down.
                LiningUp=true;
                sfx.pitch=1;sfx.PlayOneShot(TempAudio.Warn,.55f);
                int line=patter++%Patter.Length;Say(calls[line]);
                float lead=Mathf.Max(warningSeconds,calls[line]!=null?calls[line].length+.12f:0f);flashAt=Time.time+lead;
                HudController.Instance?.SetBark("Photographer: "+Patter[line],lead+.4f);
                return;
            }
            Wander();
        }
        void Face(Vector3 at){Vector3 d=at-transform.position;d.y=0;if(d.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(d),Time.deltaTime*360);}
        void LateUpdate()
        {
            var delta=transform.position-previousPosition;previousPosition=transform.position;delta.y=0;
            float speed=Time.deltaTime>0?delta.magnitude/Time.deltaTime:0;
            bool walking=!LiningUp&&agent!=null&&agent.isOnNavMesh&&!agent.isStopped&&speed>.06f&&speed<12f&&Time.deltaTime>0;
            if(walking)strideDistance=Mathf.Repeat(strideDistance+delta.magnitude,Mathf.Max(.1f,strideLength));
            else strideDistance=0;
            SetPose(walking?(strideDistance<strideLength*.5f?walkLeft:walkRight):photoPose);
        }
        void SetPose(Texture pose)
        {
            if(cutout==null||pose==null||currentPose==pose)return;
            if(poseProperties==null)poseProperties=new MaterialPropertyBlock();
            cutout.GetPropertyBlock(poseProperties);poseProperties.SetTexture("_BaseMap",pose);cutout.SetPropertyBlock(poseProperties);currentPose=pose;
        }
        void Say(AudioClip clip){if(clip!=null){voice.Stop();voice.clip=clip;voice.Play();}}
        void Wander()
        {
            if(!agent.isOnNavMesh)return;
            bool arrived=haveGoal&&!agent.pathPending&&agent.remainingDistance<=agent.stoppingDistance+.1f;
            // At a crossing he stops and looks round for a while: that's where the pupils go past.
            if(arrived&&goalIsJunction&&lingerUntil<=0){lingerUntil=Time.time+lingerSeconds*Random.Range(.7f,1.4f);}
            if(Lingering){agent.isStopped=true;transform.Rotate(0,Time.deltaTime*35f,0);return;}
            agent.isStopped=false;agent.speed=walkSpeed;
            if(haveGoal&&!arrived&&Time.time<wanderUntil)return;
            lingerUntil=0;
            // Mostly crossings, sometimes a random stretch of corridor; a generous deadline so a blocked path doesn't hold him forever.
            for(int i=0;i<8;i++)
            {
                Vector3 want;bool junction=junctions.Count>0&&Random.value<junctionBias;
                if(junction)want=junctions[Random.Range(0,junctions.Count)];
                else if(corridors!=null&&corridors.Length>0){var r=corridors[Random.Range(0,corridors.Length)];want=new Vector3(Random.Range(r.xMin+.6f,r.xMax-.6f),0,Random.Range(r.yMin+.6f,r.yMax-.6f));}
                else want=transform.position+new Vector3(Random.Range(-12f,12f),0,Random.Range(-12f,12f));
                if(Vector3.Distance(want,transform.position)<6f||!NavMesh.SamplePosition(want,out var hit,1.5f,NavMesh.AllAreas))continue;
                if(!agent.SetDestination(hit.position))continue;
                goal=hit.position;haveGoal=true;goalIsJunction=junction;wanderUntil=Time.time+Vector3.Distance(hit.position,transform.position)/walkSpeed+8f;return;
            }
        }
        void Flash(SchoolRunController run,PlayerInteractor player,float head)
        {
            Flashes++;
            if(flashLight!=null)flashLight.intensity=60;
            sfx.pitch=1;sfx.PlayOneShot(shutterFlash!=null?shutterFlash:TempAudio.Pickup,.9f);
            NoiseEvents.Emit(transform.position,flashNoiseRadius,"camera flash");
            // Staff in the frame: dazzled, rooted to the spot.
            foreach(var staff in new[]{run.caretaker,run.secondStaff})
                if(staff!=null&&staff.isActiveAndEnabled&&InFrame(staff.transform,1.7f)&&staff.TryStickInGlue(flashStallSeconds))
                {StaffDazzled++;HudController.Instance?.SetBark((staff==run.caretaker?"Caretaker":"Mr Reed")+": Argh! My eyes!",2.5f);}
            // The pupil still in front of him is in the photo; only eyes that were on the camera get the white-out.
            if(!InFrame(player.transform,head)){Say(whereDidHeGo);HudController.Instance?.SetBark("Photographer: Oh. Where did he go?",2f);return;}
            PlayerPhotos++;
            if(LookingAtHim(player)){blindUntil=Time.time+blindSeconds;EnsureWhiteout();}
            Say(lovely);HudController.Instance?.SetBark("Photographer: Lovely! That one's going on the noticeboard.",3f);
            if(!run.RoundStarted)return;
            // Unmissable, and exact: a photo shows exactly where you were.
            run.caretaker?.HearSchoolWideAlarm(player.transform.position);
            if(run.secondStaff!=null&&run.secondStaff.enabled)run.secondStaff.HearSchoolWideAlarm(player.transform.position);
        }
        void EnsureWhiteout()
        {
            if(whiteout!=null)return;
            var g=new GameObject("Camera flash white-out",typeof(Canvas),typeof(Image));g.transform.SetParent(transform,false);
            var c=g.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=31000;
            whiteout=g.GetComponent<Image>();whiteout.color=Color.white;whiteout.raycastTarget=false;
        }
    }
}

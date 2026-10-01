using UnityEngine;
namespace Confiscated
{
    [DefaultExecutionOrder(100)]
    public sealed class DinnerTrolleyPatrol : MonoBehaviour
    {
        public Vector3 end;public float speed=1.25f,waitSeconds=2.5f;
        [Tooltip("How long Smith has to step aside after she warns him.")]
        public float warningSeconds=1.4f;
        [Tooltip("Player closer than this (metres, floor plane, in her sight) is 'going past' and gets told on.")]
        public float tellDistance=2.8f,tellNoiseRadius=22,tellCooldownSeconds=14;
        public Transform visual;
        public float patrolExtension=8f,spotRange=12f,pushSpeed=2.7f,pushCooldown=10f;
        [Tooltip("Player this close (metres) is noticed and aimed at whichever side of her they are on.")]
        public float aimRange=3.5f;
        public bool Pursuing {get;private set;}
        public Vector3 PatrolStart=>start;
        public Vector3 PatrolEnd=>patrolEnd;
        Vector3 patrolEnd,heading;
        float pushReadyAt,pushEnds,conversationUntil;
        Renderer cutout;Texture frontArt,rearArt,huntArt;MaterialPropertyBlock artBlock;
        public bool ShowingRear {get;private set;}
        public bool Waiting => Time.time<waitUntil;
        public bool Outbound => outbound;
        public int Tellings {get;private set;}
        public int Collisions {get;private set;}
        Vector3 start;bool outbound=true;float waitUntil;bool announced,told;float tellReadyAt,warnedAt;AudioSource voice;
        AudioSource freezerRequest;
        public void RequestFreezerRepair(float conversationSeconds)
        {
            Pursuing=false;conversationUntil=Time.time+conversationSeconds;
            waitUntil=Mathf.Max(waitUntil,Time.time+conversationSeconds);
            tellReadyAt=Mathf.Max(tellReadyAt,Time.time+conversationSeconds);
            var clip=Resources.Load<AudioClip>("Audio/DinnerLadyFreezerRequest");if(clip==null)return;
            if(freezerRequest==null)
            {
                freezerRequest=SchoolAudio.Create(gameObject,SchoolAudio.Channel.Voice,true);
                freezerRequest.spatialBlend=1;freezerRequest.rolloffMode=AudioRolloffMode.Linear;freezerRequest.minDistance=8;freezerRequest.maxDistance=65;freezerRequest.dopplerLevel=0;
                TalkingMouth.Register(freezerRequest);
            }
            if(voice!=null)voice.Stop();freezerRequest.clip=clip;freezerRequest.Play();
            waitUntil=Mathf.Max(waitUntil,Time.time+clip.length);
            StartCoroutine(FreezerReply(clip.length+.25f));
        }
        System.Collections.IEnumerator FreezerReply(float delay)
        {
            yield return new WaitForSeconds(delay);
            var run=SchoolRunController.Instance;
            if(run==null||run.caretaker==null||!run.caretaker.Chatting||GameManager.Instance==null||!GameManager.Instance.IsPlaying)yield break;
            var clip=Resources.Load<AudioClip>("Audio/CaretakerFreezerReply");
            if(clip!=null&&run.caretaker.Say("CaretakerFreezerReply",true))
                HudController.Instance?.SetBark("Caretaker: Just give it a swift kick, that's all I did last time. Fine, let me take a look.",clip.length);
        }
        void Start()
        {
            start=transform.position;heading=(end-start).normalized;
            patrolEnd=ClearPoint(end,heading,patrolExtension);
            start=ClearPoint(start,-heading,patrolExtension);
            waitUntil=Time.time+3;
            foreach(var r in GetComponentsInChildren<Renderer>())if(r.name=="Dinner lady cutout")cutout=r;
            if(cutout!=null){frontArt=cutout.sharedMaterial.GetTexture("_BaseMap");rearArt=Resources.Load<Texture2D>("Art/DinnerLadyRear");huntArt=Resources.Load<Texture2D>("Art/Hunt/T_DinnerLady_Hunt");artBlock=new MaterialPropertyBlock();}
            if(visual!=null)visual.localRotation=Quaternion.Euler(0,180,0);
        }
        Vector3 ClearPoint(Vector3 from,Vector3 direction,float distance)
        {
            var run=SchoolRunController.Instance;
            foreach(var hit in Physics.SphereCastAll(from+Vector3.up*.85f,.45f,direction,distance,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform)&&(run==null||!hit.transform.IsChildOf(run.period.Player.transform)))distance=Mathf.Min(distance,Mathf.Max(0,hit.distance-.15f));
            return from+direction*distance;
        }
        /// <summary>Ahead of her within spotRange; anywhere round her within aimRange, so standing to the side as she passes is no escape.</summary>
        bool Sees(Transform player)
        {
            Vector3 delta=player.position-transform.position;delta.y=0;
            if(delta.magnitude>spotRange)return false;
            if(delta.magnitude>aimRange&&Vector3.Dot(heading,delta.normalized)<.2f)return false;
            Vector3 eye=transform.position+Vector3.up*1.5f;
            Vector3 sight=player.position+Vector3.up*1.1f-eye;
            foreach(var hit in Physics.RaycastAll(eye,sight.normalized,sight.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(player)&&!hit.transform.IsChildOf(transform))return false;
            return true;
        }
        void Apologise()
        {
            var clip=Resources.Load<AudioClip>("Audio/DinnerLadyApology");
            HudController.Instance?.SetBark("Dinner lady: Oh, sorry, love! Didn't see you there.",clip!=null?clip.length:3.5f);
            if(clip==null)return;
            if(voice==null){voice=SchoolAudio.Create(gameObject,SchoolAudio.Channel.Voice,true);voice.spatialBlend=1;voice.minDistance=4;voice.maxDistance=22;TalkingMouth.Register(voice);}
            voice.Stop();voice.PlayOneShot(clip);
        }
        void Shout()
        {
            var clip=Resources.Load<AudioClip>("Audio/DinnerLadyTellTale");if(clip==null)return;
            if(voice==null)
            {
                voice=SchoolAudio.Create(gameObject,SchoolAudio.Channel.Voice,true);voice.playOnAwake=false;voice.loop=false;
                voice.spatialBlend=1;voice.rolloffMode=AudioRolloffMode.Linear;voice.minDistance=4;voice.maxDistance=tellNoiseRadius;voice.dopplerLevel=0;TalkingMouth.Register(voice);
            }
            voice.Stop();voice.clip=clip;voice.Play();
            HudController.Instance?.SetBark("Dinner lady: Does the Caretaker know you're not in class? He will be cross, like he was back that day.",clip.length);
        }
        /// <summary>She tells the caretaker as you pass her, even while she waits at either end of her round.</summary>
        void TellTale(Transform player)
        {
            if(freezerRequest!=null&&freezerRequest.isPlaying)return;
            Vector3 d=player.position-transform.position;d.y=0;
            float dist=d.magnitude;
            if(dist>tellDistance+2){told=false;return;}
            if(told||dist>tellDistance||Time.time<tellReadyAt)return;
            // Walls and shut doors hide you from her; the player's own colliders do not.
            Vector3 eye=transform.position+Vector3.up*1.5f;
            if(Physics.Linecast(eye,player.position+Vector3.up*1.2f,out var hit,~0,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(player)&&!hit.transform.IsChildOf(transform))return;
            told=true;Tellings++;tellReadyAt=Time.time+tellCooldownSeconds;
            // Emit first: the caretaker's own "He heard something..." status must not replace her line.
            NoiseEvents.Emit(transform.position,tellNoiseRadius,"dinner lady");
            Shout();
        }
        void Update()
        {
            var run=SchoolRunController.Instance;if(run==null||GameManager.Instance==null||!GameManager.Instance.IsPlaying||ComicDialogue.IsActive)return;
            var player=run.period.Player.transform;
            if(run.RoundStarted)TellTale(player);
            if(Time.time<conversationUntil)return;
            var movement=player.GetComponent<FirstPersonController>();
            if(!Pursuing&&run.RoundStarted&&Time.time>=pushReadyAt&&movement!=null&&!movement.IsFallen&&Sees(player))
            {Pursuing=true;pushEnds=Time.time+6;warnedAt=Time.time;announced=true;waitUntil=0;HudController.Instance?.SetStatus("Dinner lady: Mind out, love.",1.4f);}
            if(Pursuing&&(Time.time>=pushEnds||Vector3.Distance(player.position,transform.position)>spotRange+3))
            {Pursuing=false;pushReadyAt=Time.time+pushCooldown;}
            if(Time.time<waitUntil)return;
            Vector3 target=Pursuing?player.position:outbound?patrolEnd:start;target.y=transform.position.y;
            Vector3 direction=target-transform.position;direction.y=0;
            if(direction.sqrMagnitude>.001f)direction.Normalize();
            heading=direction;
            var step=ClearPoint(transform.position,direction,Mathf.Min(Vector3.Distance(transform.position,target),(Pursuing?pushSpeed:speed)*Time.deltaTime));
            Vector3 delta=player.position-transform.position;delta.y=0;
            float ahead=Vector3.Dot(delta,direction);
            float lateral=(delta-direction*ahead).magnitude;
            bool inLane=ahead>0&&ahead<7&&lateral<1.35f;
            if(inLane&&!announced)
            {
                HudController.Instance?.SetStatus("Dinner lady: Mind out, love.",2.4f);
                announced=true;warnedAt=Time.time;
            }
            if(ahead>0&&ahead<1.45f&&lateral<1.05f&&Time.time>=pushReadyAt&&Sees(player))
            {
                if(!announced){announced=true;warnedAt=Time.time;HudController.Instance?.SetStatus("Dinner lady: Mind out, love.",2.4f);}
                if(Time.time-warnedAt<warningSeconds)return;
                if(movement!=null&&!movement.IsFallen)
                {
                    movement.KnockDown(direction);Collisions++;
                    NoiseEvents.Emit(transform.position,28,"trolley collision");
                    Apologise();
                }
                // Back away after a collision so the trolley cannot pin the player against scenery.
                Pursuing=false;pushReadyAt=Time.time+pushCooldown;outbound=!outbound;waitUntil=Time.time+1.75f;announced=false;return;
            }
            bool blocked=Vector3.Distance(step,transform.position)<.00001f;
            transform.position=step;
            if(!Pursuing&&(Vector3.Distance(step,target)<.01f||blocked)){outbound=!outbound;waitUntil=Time.time+waitSeconds;announced=false;}
        }
        void LateUpdate()
        {
            if(visual==null||ComicDialogue.IsActive)return;
            var run=SchoolRunController.Instance;if(run==null||GameManager.Instance==null||!GameManager.Instance.IsPlaying)return;
            if(heading.sqrMagnitude>.01f)visual.rotation=Quaternion.RotateTowards(visual.rotation,Quaternion.LookRotation(heading)*Quaternion.Euler(0,90,0),Time.deltaTime*180);
            if(cutout!=null&&rearArt!=null)
            {
                ShowingRear=Vector3.Dot(heading,run.period.Player.transform.position-cutout.transform.position)<-.2f;
                bool hunt=run.GetComponent<HuntFaces>()?.CrowdTurned==true;
                cutout.GetPropertyBlock(artBlock);artBlock.SetTexture("_BaseMap",ShowingRear?rearArt:hunt&&huntArt!=null?huntArt:frontArt);
                if(ShowingRear)artBlock.SetFloat("_MouthOpen",0);
                cutout.SetPropertyBlock(artBlock);
            }
        }
    }
}

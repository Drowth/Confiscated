using UnityEngine;
namespace Confiscated
{
    public sealed class DinnerTrolleyPatrol : MonoBehaviour
    {
        public Vector3 end;public float speed=1.25f,waitSeconds=2.5f;
        [Tooltip("How long Smith has to step aside after she warns him.")]
        public float warningSeconds=1.4f;
        [Tooltip("Player closer than this (metres, floor plane, in her sight) is 'going past' and gets told on.")]
        public float tellDistance=2.8f,tellNoiseRadius=22,tellCooldownSeconds=14;
        public Transform visual;
        public bool Waiting => Time.time<waitUntil;
        public bool Outbound => outbound;
        public int Tellings {get;private set;}
        public int Collisions {get;private set;}
        Vector3 start;bool outbound=true;float waitUntil;bool announced,told;float tellReadyAt,warnedAt;AudioSource voice;
        void Start(){start=transform.position;waitUntil=Time.time+3;if(visual!=null)visual.localRotation=Quaternion.Euler(0,180,0);}
        void Shout()
        {
            if(DarkModeDialogue.Active)return; // The contextual HUD line plays its own matching voice.
            if(voice==null)
            {
                var clip=Resources.Load<AudioClip>("Audio/DinnerLadyTellTale");if(clip==null)return;
                voice=gameObject.AddComponent<AudioSource>();voice.clip=clip;voice.playOnAwake=false;voice.loop=false;
                voice.spatialBlend=1;voice.rolloffMode=AudioRolloffMode.Linear;voice.minDistance=4;voice.maxDistance=tellNoiseRadius;voice.dopplerLevel=0;TalkingMouth.Register(voice);
            }
            voice.Play();
        }
        /// <summary>She tells the caretaker as you pass her, even while she waits at either end of her round.</summary>
        void TellTale(Transform player)
        {
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
            HudController.Instance?.SetStatus("Dinner lady: Caretaker, there's a student out of class!",3);
            Shout();
        }
        void Update()
        {
            var run=SchoolRunController.Instance;if(run==null||GameManager.Instance==null||!GameManager.Instance.IsPlaying||ComicDialogue.IsActive)return;
            var player=run.period.Player.transform;
            if(run.RoundStarted)TellTale(player);
            if(Time.time<waitUntil)return;
            Vector3 target=outbound?end:start;var step=Vector3.MoveTowards(transform.position,target,speed*Time.deltaTime);
            Vector3 direction=target-transform.position;direction.y=0;
            if(direction.sqrMagnitude>.001f)direction.Normalize();
            Vector3 delta=player.position-transform.position;delta.y=0;
            float ahead=Vector3.Dot(delta,direction);
            float lateral=(delta-direction*ahead).magnitude;
            bool inLane=ahead>0&&ahead<7&&lateral<1.35f;
            if(inLane&&!announced)
            {
                HudController.Instance?.SetStatus("Dinner lady: Mind out, love.",2.4f);
                announced=true;warnedAt=Time.time;
            }
            if(ahead>0&&ahead<1.45f&&lateral<1.05f)
            {
                if(!announced){announced=true;warnedAt=Time.time;HudController.Instance?.SetStatus("Dinner lady: Mind out, love.",2.4f);}
                if(Time.time-warnedAt<warningSeconds)return;
                var movement=player.GetComponent<FirstPersonController>();
                if(movement!=null&&!movement.IsFallen)
                {
                    movement.KnockDown(direction);Collisions++;
                    NoiseEvents.Emit(transform.position,28,"trolley collision");
                    HudController.Instance?.SetBark("Dinner lady: I did warn you, love!",2.5f);
                }
                // Back away after a collision so the trolley cannot pin the player against scenery.
                outbound=!outbound;waitUntil=Time.time+1.75f;announced=false;return;
            }
            transform.position=step;
            if(Vector3.Distance(step,target)<.01f){outbound=!outbound;waitUntil=Time.time+waitSeconds;announced=false;}
        }
        void LateUpdate()
        {
            if(visual==null||ComicDialogue.IsActive)return;
            var run=SchoolRunController.Instance;if(run==null||!run.RoundStarted||GameManager.Instance==null||!GameManager.Instance.IsPlaying)return;
            Vector3 d=run.period.Player.transform.position-transform.position;d.y=0;
            if(d.magnitude<1.8f)return;
            visual.localRotation=Quaternion.RotateTowards(visual.localRotation,Quaternion.Euler(0,outbound?180:0,0),Time.deltaTime*120);
        }
    }
}

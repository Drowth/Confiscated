using UnityEngine;
using UnityEngine.AI;

namespace Confiscated
{
    // Detect contact before the caretaker's chase/catch update. No solid collider obstructs the player.
    [DefaultExecutionOrder(-200)]
    public sealed class GluePuddle : MonoBehaviour
    {
        public const float HoldSeconds=4;
        public float radius=.53f;
        public AudioClip stuckSound;
        public bool Consumed {get;private set;}
        public AudioSource StuckAudio {get;private set;}
        CaretakerAI caretaker;
        Vector3 previous;
        float removeAt;
        void Start()
        {
            caretaker=SchoolRunController.Instance?.caretaker;
            if(caretaker!=null)previous=caretaker.transform.position;
            StuckAudio=gameObject.AddComponent<AudioSource>();StuckAudio.playOnAwake=false;StuckAudio.loop=false;
            StuckAudio.clip=stuckSound;StuckAudio.spatialBlend=1;StuckAudio.minDistance=2;StuckAudio.maxDistance=22;
            StuckAudio.dopplerLevel=0;StuckAudio.volume=.85f;
        }
        void Update()
        {
            var game=GameManager.Instance;
            if(game==null)return;
            if(game.Current==GameManager.State.Won||game.Current==GameManager.State.Caught){Destroy(gameObject);return;}
            if(Consumed){if(Time.time>=removeAt)Destroy(gameObject);return;}
            if(caretaker==null)return;
            Vector3 current=caretaker.transform.position,from=previous;previous=current;
            if(!game.IsPlaying||ComicDialogue.IsActive||Time.timeScale<=0||caretaker.IsGlued||caretaker.Current==CaretakerAI.State.Frozen)return;
            if(Mathf.Abs(current.y-transform.position.y)>.3f)return;
            Vector3 segment=current-from;segment.y=0;
            // Fast ordinary movement cannot skip a small puddle; a warp is not a walk over the intervening floor.
            if(segment.sqrMagnitude>9)from=current;
            Vector3 end=current;from.y=end.y=transform.position.y;
            segment=end-from;
            float t=segment.sqrMagnitude>.00001f?Mathf.Clamp01(Vector3.Dot(transform.position-from,segment)/segment.sqrMagnitude):0;
            if((from+segment*t-transform.position).sqrMagnitude>radius*radius)return;
            if(!NavMesh.SamplePosition(transform.position,out var floor,.2f,NavMesh.AllAreas)||
                NavMesh.Raycast(current,floor.position,out _,NavMesh.AllAreas)||
                Physics.Linecast(current+Vector3.up*.2f,transform.position+Vector3.up*.2f,~0,QueryTriggerInteraction.Ignore))return;
            if(!caretaker.TryStickInGlue(HoldSeconds))return;
            Consumed=true;removeAt=Time.time+Mathf.Max(HoldSeconds,stuckSound!=null?stuckSound.length:0)+.15f;
            if(stuckSound!=null)StuckAudio.Play();
            HudController.Instance?.SetStatus("He's stuck in the glue! Four seconds — RUN!",3);
        }
    }
}

using UnityEngine;
namespace Confiscated
{
    /// <summary>The child's own alternating footsteps, paced by ground actually covered rather than by a timer or by input.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFootstepAudio : MonoBehaviour
    {
        [Min(.3f)] public float walkStride=1.05f,sprintStride=1.3f;
        // Kept under the caretaker's whistle and footsteps: those are the player's only radar.
        [Range(0,1)] public float walkVolume=.26f,sprintVolume=.42f;
        public AudioSource Feet {get;private set;}
        public int StepCount {get;private set;}
        public int LastFoot {get;private set;}=-1;
        public bool Ready=>left!=null&&right!=null;
        AudioClip left,right;FirstPersonController movement;Vector3 previous;float travelled;
        void Awake()
        {
            movement=GetComponent<FirstPersonController>();
            left=Resources.Load<AudioClip>("Audio/PlayerFootstep1");right=Resources.Load<AudioClip>("Audio/PlayerFootstep2");
            Feet=gameObject.AddComponent<AudioSource>();Feet.playOnAwake=false;Feet.spatialBlend=0;Feet.dopplerLevel=0;
            previous=transform.position;
        }
        void LateUpdate()
        {
            Vector3 delta=transform.position-previous;previous=transform.position;delta.y=0;
            bool walking=movement.enabled&&!movement.MovementLocked&&!movement.IsFallen&&movement.Controller.enabled&&
                !ComicDialogue.IsActive&&Time.timeScale>0&&(GameManager.Instance==null||GameManager.Instance.IsPlaying);
            Advance(walking?delta.magnitude:0,Time.deltaTime,movement.IsSprinting);
        }
        /// <summary>Pushing against a wall covers no ground, so it makes no sound; a warp is too fast to be a stride.</summary>
        public void Advance(float distance,float seconds,bool sprinting)
        {
            if(seconds<=0)return;
            float speed=distance/seconds;
            // Standing still re-arms the stride so the first step after a pause is not instant.
            if(speed>12||speed<.25f){if(speed<.25f)travelled=Mathf.Min(travelled,(sprinting?sprintStride:walkStride)*.5f);return;}
            travelled+=distance;
            float stride=sprinting?sprintStride:walkStride;
            if(travelled<stride)return;
            travelled-=stride;LastFoot=LastFoot==0?1:0;StepCount++;
            var clip=LastFoot==0?left:right;if(clip==null)return;
            Feet.pitch=Random.Range(.94f,1.06f)*(sprinting?1.05f:1);
            Feet.PlayOneShot(clip,(sprinting?sprintVolume:walkVolume)*Random.Range(.88f,1));
        }
        void OnDisable(){if(Feet!=null)Feet.Stop();}
        void OnDestroy(){if(Feet!=null)Destroy(Feet);}
    }
}

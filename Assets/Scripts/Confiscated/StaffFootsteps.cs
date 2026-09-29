using UnityEngine;
namespace Confiscated
{
    /// <summary>
    /// Simple footsteps for staff without a gait of their own (Mr Reed): a step for every stride actually walked, so he is
    /// silent standing still and quicker when he hurries. The player's own footstep clips, pitched down to an adult's tread,
    /// 3D so the player can tell where he is.
    /// </summary>
    public sealed class StaffFootsteps : MonoBehaviour
    {
        public float stride=.72f,volume=.55f,pitch=.82f;
        public int Steps {get;private set;}
        AudioSource source;AudioClip[] steps;Vector3 last;float walked;int next;
        void Awake()
        {
            steps=new[]{Resources.Load<AudioClip>("Audio/PlayerFootstep1"),Resources.Load<AudioClip>("Audio/PlayerFootstep2")};
            source=SchoolAudio.Create(gameObject,SchoolAudio.Channel.Effects,true);source.playOnAwake=false;source.spatialBlend=1;
            source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=1.5f;source.maxDistance=18f;source.dopplerLevel=0;
            last=transform.position;
        }
        void Update()
        {
            Vector3 moved=transform.position-last;moved.y=0;last=transform.position;
            // Warps (retry, scripted repositioning) are not walking.
            if(moved.sqrMagnitude>1||Time.timeScale<=0){walked=0;return;}
            walked+=moved.magnitude;
            if(walked<stride)return;
            walked-=stride;
            var clip=steps[next];next=1-next;if(clip==null)return;
            source.pitch=pitch*Random.Range(.94f,1.06f);source.PlayOneShot(clip,volume);Steps++;
        }
    }
}

using UnityEngine;

namespace Confiscated
{
    /// <summary>Optional memories heard when Smith pauses to examine a pupil picture.</summary>
    [DisallowMultipleComponent]
    public sealed class CopycatPictureWhisper : MonoBehaviour
    {
        public AudioClip[] clips;
        public string[] lines;
        const float InspectDistance=3.2f,DwellSeconds=1.25f,RepeatDelay=55f;
        static AudioSource activeVoice;
        AudioSource voice;
        Renderer picture;
        float dwell,nextAllowed;
        int nextLine;
        bool lookedAway=true;

        void Awake()
        {
            picture=GetComponent<Renderer>();
            voice=gameObject.AddComponent<AudioSource>();
            voice.playOnAwake=false;voice.spatialBlend=1;voice.dopplerLevel=0;
            voice.rolloffMode=AudioRolloffMode.Linear;voice.minDistance=1.2f;voice.maxDistance=5;
            voice.volume=.48f;
        }

        void Update()
        {
            if(GameManager.Instance==null||!GameManager.Instance.IsPlaying||ComicDialogue.IsActive||Time.timeScale<=0)
            {dwell=0;if(voice.isPlaying)voice.Stop();return;}
            var camera=Camera.main;
            if(!Examining(camera)){dwell=0;lookedAway=true;return;}
            if(!lookedAway||Time.time<nextAllowed||(activeVoice!=null&&activeVoice.isPlaying))return;
            if(clips==null||clips.Length==0)return;
            dwell+=Time.deltaTime;
            if(dwell<DwellSeconds)return;
            int index=nextLine%clips.Length;
            if(clips[index]==null){dwell=0;return;}
            voice.clip=clips[index];voice.Play();activeVoice=voice;
            if(lines!=null&&index<lines.Length)
                HudController.Instance?.SetBark("A faint voice: "+lines[index],voice.clip.length+.3f);
            nextLine=(index+1)%clips.Length;nextAllowed=Time.time+RepeatDelay;
            dwell=0;lookedAway=false;
        }

        bool Examining(Camera camera)
        {
            if(camera==null||picture==null||!picture.enabled)return false;
            Vector3 point=picture.bounds.center,delta=point-camera.transform.position;
            if(delta.sqrMagnitude>InspectDistance*InspectDistance||Vector3.Dot(camera.transform.forward,delta.normalized)<.975f)return false;
            // A wall or another prop must block the memory just as it blocks the picture.
            if(Physics.Linecast(camera.transform.position,point,out var hit,~0,QueryTriggerInteraction.Ignore)
                &&hit.distance<delta.magnitude-.08f)return false;
            return true;
        }

        void OnDisable(){if(voice!=null)voice.Stop();dwell=0;lookedAway=true;}
    }
}


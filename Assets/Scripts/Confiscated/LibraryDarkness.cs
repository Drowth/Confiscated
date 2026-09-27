using UnityEngine;
using UnityEngine.Rendering;
namespace Confiscated
{
    /// <summary>
    /// The library is dark: an exposure-darkening Volume that fades in as the camera walks in from a doorway (full inside,
    /// over the first metres). Driven by position rather than a trigger collider, so nothing blocks interaction rays.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public sealed class LibraryDarkness : MonoBehaviour
    {
        public Bounds room;
        public float fadeMetres=2.5f;
        [Range(0,1)] public float musicVolume=.35f;
        /// <summary>How far into the library the camera is, 0 outside to 1 inside; the school music ducks by it.</summary>
        public static float Weight {get;private set;}
        Volume volume;AudioSource music;
        void Awake()
        {
            volume=GetComponent<Volume>();volume.isGlobal=true;volume.weight=0;
            // The library's own eerie loop (seamless: LibraryMusicLoop is crossfaded at its loop point).
            music=gameObject.AddComponent<AudioSource>();music.clip=Resources.Load<AudioClip>("Audio/LibraryMusicLoop");music.loop=true;music.playOnAwake=false;
            music.spatialBlend=0;music.volume=0;music.priority=150;
        }
        void OnDisable(){Weight=0;}
        void LateUpdate()
        {
            var cam=Camera.main;if(cam==null){volume.weight=Weight=0;Music();return;}
            Vector3 p=cam.transform.position;
            // Distance in from the nearest wall of the room on the floor plane (negative outside).
            float inX=Mathf.Min(p.x-room.min.x,room.max.x-p.x),inZ=Mathf.Min(p.z-room.min.z,room.max.z-p.z);
            volume.weight=Mathf.Clamp01(Mathf.Min(inX,inZ)/fadeMetres+.25f);
            if(Mathf.Min(inX,inZ)<0)volume.weight=0;
            Weight=volume.weight;Music();
        }
        void Music()
        {
            if(music==null||music.clip==null)return;
            bool playing=GameManager.Instance!=null&&GameManager.Instance.IsPlaying;
            music.volume=Mathf.MoveTowards(music.volume,playing?Weight*musicVolume:0,Time.unscaledDeltaTime*.5f);
            if(music.volume>0&&!music.isPlaying)music.Play();else if(music.volume<=0&&music.isPlaying)music.Pause();
        }
    }
}

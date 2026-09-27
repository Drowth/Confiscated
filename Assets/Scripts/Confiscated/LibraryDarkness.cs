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
        Volume volume;
        void Awake(){volume=GetComponent<Volume>();volume.isGlobal=true;volume.weight=0;}
        void LateUpdate()
        {
            var cam=Camera.main;if(cam==null){volume.weight=0;return;}
            Vector3 p=cam.transform.position;
            // Distance in from the nearest wall of the room on the floor plane (negative outside).
            float inX=Mathf.Min(p.x-room.min.x,room.max.x-p.x),inZ=Mathf.Min(p.z-room.min.z,room.max.z-p.z);
            volume.weight=Mathf.Clamp01(Mathf.Min(inX,inZ)/fadeMetres+.25f);
            if(Mathf.Min(inX,inZ)<0)volume.weight=0;
        }
    }
}

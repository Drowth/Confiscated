using System.Collections.Generic;
using UnityEngine;
namespace Confiscated
{
    /// <summary>
    /// A window in the library's corridor wall (Docs/LibraryMaze.md). The library is dark: from outside, the caretaker can
    /// only see a player inside it who is standing in the pool of light a window throws on the floor. His sight passes
    /// through the glass (Ignore Raycast layer); the player cannot.
    /// </summary>
    public sealed class LibraryWindow : MonoBehaviour
    {
        public Bounds room;
        [Tooltip("Centre of the light pool on the library floor.")]
        public Vector3 pool;
        public float radius=2.2f;
        [Tooltip("The light the window throws in, and its brightness seen from the corridor; lifted to match the darkness when you are inside.")]
        public Light lamp;
        public float plainIntensity=5.5f;
        void LateUpdate(){if(lamp!=null)lamp.intensity=plainIntensity*LibraryDarkness.LiftFactor;}
        static readonly List<LibraryWindow> all=new();
        public static IReadOnlyList<LibraryWindow> All=>all;
        void OnEnable()=>all.Add(this);
        void OnDisable()=>all.Remove(this);
        public static bool InLibrary(Vector3 p)=>all.Count>0&&all[0].room.Contains(new Vector3(p.x,all[0].room.center.y,p.z));
        public static bool InLight(Vector3 p)
        {
            foreach(var w in all){Vector3 d=p-w.pool;d.y=0;if(d.magnitude<w.radius)return true;}
            return false;
        }
        /// <summary>In the library and out of every window's light: nobody outside can see you.</summary>
        public static bool HiddenInLibrary(Vector3 p)=>InLibrary(p)&&!InLight(p);
    }
}

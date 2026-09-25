using UnityEngine;
namespace Confiscated
{
    /// <summary>Separate pupils that ride on the curve of each modelled eyeball. The baked pupils are painted out of the texture.</summary>
    public sealed class CaretakerHeadEyes : MonoBehaviour
    {
        public Transform[] pupils=new Transform[2];
        // Head-local. Each eyeball is a fitted sphere; 'forward' points from its centre to the middle of the visible white.
        public Vector3[] centres=new Vector3[2],forwards=new Vector3[2];
        public float[] radii=new float[2];
        // Where the drawing had each pupil, as a fraction of the wander radius, so the lunge still matches the artwork.
        public Vector2[] restOffsets=new Vector2[2];
        public float wanderRadius=.03f,lift=.002f;
        public void Rest(){for(int i=0;i<pupils.Length;i++)Look(i,restOffsets[i]);}
        /// <summary>offset is -1..1 across the white: x toward the character's left, y up.</summary>
        public void Look(int eye,Vector2 offset)
        {
            if(pupils[eye]==null)return;
            Vector3 forward=forwards[eye].normalized,right=Vector3.Cross(Vector3.up,forward).normalized,up=Vector3.Cross(forward,right);
            Vector3 direction=(forward*radii[eye]+(right*offset.x+up*offset.y)*wanderRadius).normalized;
            pupils[eye].localPosition=centres[eye]+direction*(radii[eye]+lift);
            pupils[eye].localRotation=Quaternion.LookRotation(direction,up);
        }
        /// <summary>Both pupils roll, in opposite directions and slightly out of time with each other.</summary>
        public void Roll(float seconds,float turnsPerSecond=.7f,float reach=.85f)
        {
            float a=seconds*turnsPerSecond*Mathf.PI*2,b=-a*1.13f+1.1f;
            Look(0,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*reach);Look(1,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*reach);
        }
    }
}

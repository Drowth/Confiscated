using UnityEngine;

namespace Confiscated
{
    /// <summary>A reusable seated cutout with a fixed classroom facing and two authored views.</summary>
    public sealed class SeatedStudent : MonoBehaviour
    {
        public Transform artwork;
        public Transform lessonFocus;
        Vector3 restScale;
        void Awake(){if(artwork!=null)restScale=artwork.localScale;}
        void LateUpdate()
        {
            if(lessonFocus!=null)
            {
                var direction=lessonFocus.position-transform.position;direction.y=0;
                if(direction.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(direction);
            }
            if(artwork!=null)
            {
                // Quiet breathing, with no head tracking towards the player.
                float breath=Mathf.Sin(Time.time*1.7f)*.0025f;
                artwork.localScale=new Vector3(restScale.x,restScale.y*(1+breath),restScale.z);
            }
        }
    }
}

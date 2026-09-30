using UnityEngine;

namespace Confiscated
{
    /// <summary>A reusable seated cutout with a fixed classroom facing and two authored views.</summary>
    public sealed class SeatedStudent : MonoBehaviour
    {
        public Transform artwork;
        public Transform lessonFocus;
        Vector3 restScale;
        Transform glanceTarget;float glanceUntil,breathPhase;
        public bool WatchingPhone=>glanceTarget!=null&&Time.time<glanceUntil;
        public void ReactToPhone(Transform player){glanceTarget=player;glanceUntil=Time.time+4.5f;}
        void Awake(){if(artwork!=null)restScale=artwork.localScale;breathPhase=transform.position.x*1.3f+transform.position.z*.7f;}
        void LateUpdate()
        {
            var focus=WatchingPhone?glanceTarget:lessonFocus;
            if(focus!=null)
            {
                var direction=focus.position-transform.position;direction.y=0;
                if(direction.sqrMagnitude>.01f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),Time.deltaTime*240);
            }
            if(artwork!=null)
            {
                // Quiet breathing, with no head tracking towards the player.
                float breath=Mathf.Sin(Time.time*1.7f+breathPhase)*.0025f;
                artwork.localScale=new Vector3(restScale.x,restScale.y*(1+breath),restScale.z);
            }
        }
    }
}

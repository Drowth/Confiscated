using System.Collections.Generic;
using UnityEngine;

namespace Confiscated
{
    // Detect contact before the staff chase/catch update. No solid collider obstructs the player.
    [DefaultExecutionOrder(-200)]
    public sealed class GluePuddle : MonoBehaviour
    {
        public const float HoldSeconds=4;
        /// <summary>Half extents on the floor in local space: x across the corridor, z along it. Set by GlueDeployer.</summary>
        public float halfWidth=.53f,halfDepth=.53f;
        // Staff are about this wide; their centre only has to reach the glue's edge.
        const float StaffRadius=.3f;
        public AudioClip stuckSound;
        public bool Consumed {get;private set;}
        public CaretakerAI StuckStaff {get;private set;}
        public AudioSource StuckAudio {get;private set;}
        readonly Dictionary<CaretakerAI,Vector3> previous=new Dictionary<CaretakerAI,Vector3>();
        float removeAt;
        void Start()
        {
            StuckAudio=gameObject.AddComponent<AudioSource>();StuckAudio.playOnAwake=false;StuckAudio.loop=false;
            StuckAudio.clip=stuckSound;StuckAudio.spatialBlend=1;StuckAudio.minDistance=2;StuckAudio.maxDistance=22;
            StuckAudio.dopplerLevel=0;StuckAudio.volume=.85f;
        }
        void Update()
        {
            var game=GameManager.Instance;var run=SchoolRunController.Instance;
            if(game==null)return;
            if(game.Current==GameManager.State.Won||game.Current==GameManager.State.Caught){Destroy(gameObject);return;}
            if(Consumed){if(Time.time>=removeAt)Destroy(gameObject);return;}
            if(run==null)return;
            bool live=game.IsPlaying&&!ComicDialogue.IsActive&&Time.timeScale>0;
            // The caretaker and Mr Reed (once he's hunting): whoever walks into it first is held, then it's gone.
            foreach(var staff in new[]{run.caretaker,run.secondStaff})
            {
                if(staff==null||!staff.isActiveAndEnabled)continue;
                Vector3 current=staff.transform.position;
                Vector3 from=previous.TryGetValue(staff,out var last)?last:current;previous[staff]=current;
                if(!live||staff.IsGlued||staff.Current==CaretakerAI.State.Frozen)continue;
                if(Mathf.Abs(current.y-transform.position.y)>.3f)continue;
                // A warp is not a walk over the intervening floor.
                if((current-from).sqrMagnitude>9)from=current;
                if(!Crosses(from,current))continue;
                if(!staff.TryStickInGlue(HoldSeconds))continue;
                Consumed=true;StuckStaff=staff;removeAt=Time.time+Mathf.Max(HoldSeconds,stuckSound!=null?stuckSound.length:0)+.15f;
                if(stuckSound!=null)StuckAudio.Play();
                HudController.Instance?.SetStatus((staff==run.caretaker?"He's":"Mr Reed's")+" stuck in the glue! Four seconds - RUN!",3);
                return;
            }
        }
        /// <summary>Does the walk from a to b touch the glue strip (expanded by a staff member's width)?</summary>
        public bool Crosses(Vector3 a,Vector3 b)
        {
            Vector3 p=transform.InverseTransformPoint(a),q=transform.InverseTransformPoint(b);
            // InverseTransformPoint divides by scale; keep the strip's extents in metres.
            var s=transform.lossyScale;p=Vector3.Scale(p,s);q=Vector3.Scale(q,s);
            float hx=halfWidth+StaffRadius,hz=halfDepth+StaffRadius;
            // Slab test of the segment against the rectangle on the floor.
            float t0=0,t1=1;Vector2 d=new Vector2(q.x-p.x,q.z-p.z);
            if(!Slab(p.x,d.x,hx,ref t0,ref t1)||!Slab(p.z,d.y,hz,ref t0,ref t1))return false;
            return t0<=t1;
        }
        static bool Slab(float start,float dir,float half,ref float t0,ref float t1)
        {
            if(Mathf.Abs(dir)<1e-6f)return Mathf.Abs(start)<=half;
            float a=(-half-start)/dir,b=(half-start)/dir;if(a>b){var t=a;a=b;b=t;}
            t0=Mathf.Max(t0,a);t1=Mathf.Min(t1,b);return t0<=t1;
        }
    }
}

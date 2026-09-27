using UnityEngine;
namespace Confiscated
{
    public sealed class AccessToolPickup : Interactable
    {
        public enum Tool { BoltCutters, StoreKey }
        public Tool tool;
        public GameObject visual;
        void Start(){FitTarget();CollectibleMotion.Attach(visual);}
        /// <summary>
        /// The tool floats, bobs and spins above its table, but the target was a box on the table top, so aiming at the key
        /// itself missed. Grow the target to cover the table top up to the highest point of the bob, and the full spin width.
        /// </summary>
        void FitTarget()
        {
            var box=GetComponent<BoxCollider>();var parts=visual!=null?visual.GetComponentsInChildren<Renderer>(true):null;
            if(box==null||parts==null||parts.Length==0)return;
            var shape=parts[0].bounds;foreach(var r in parts)shape.Encapsulate(r.bounds);
            var motion=visual.GetComponent<CollectibleMotion>();float rise=motion!=null?motion.lift+motion.bob:.11f;
            float bottom=Mathf.Min(box.bounds.min.y,shape.min.y),top=shape.max.y+rise+.05f;
            float across=Mathf.Max(.45f,2*Mathf.Max(shape.extents.x,shape.extents.z)+.12f);
            var s=transform.lossyScale;
            box.center=transform.InverseTransformPoint(new Vector3(shape.center.x,(bottom+top)/2,shape.center.z));
            box.size=new Vector3(across/Mathf.Abs(s.x),(top-bottom)/Mathf.Abs(s.y),across/Mathf.Abs(s.z));
        }
        public bool Taken=>SchoolRunController.Instance!=null&&(tool==Tool.BoltCutters?SchoolRunController.Instance.HasBoltCutters:SchoolRunController.Instance.HasStoreKey);
        public override string GetPrompt(PlayerInteractor p)=>Taken?null:tool==Tool.BoltCutters?"F: take bolt cutters - opens the dining cage":"F: take store key - southeast store";
        public override bool CanInteract(PlayerInteractor p)=>!Taken&&SchoolRunController.Instance!=null&&SchoolRunController.Instance.RoundStarted;
        public override void Interact(PlayerInteractor p){if(!CanInteract(p))return;SchoolRunController.Instance.TakeTool(tool);visual.SetActive(false);}
    }
}

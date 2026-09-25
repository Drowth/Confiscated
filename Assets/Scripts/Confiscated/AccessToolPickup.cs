using UnityEngine;
namespace Confiscated
{
    public sealed class AccessToolPickup : Interactable
    {
        public enum Tool { BoltCutters, StoreKey }
        public Tool tool;
        public GameObject visual;
        void Start(){CollectibleMotion.Attach(visual);}
        public bool Taken=>SchoolRunController.Instance!=null&&(tool==Tool.BoltCutters?SchoolRunController.Instance.HasBoltCutters:SchoolRunController.Instance.HasStoreKey);
        public override string GetPrompt(PlayerInteractor p)=>Taken?null:tool==Tool.BoltCutters?"F: take bolt cutters - opens the dining cage":"F: take store key - southeast store";
        public override bool CanInteract(PlayerInteractor p)=>!Taken&&SchoolRunController.Instance!=null&&SchoolRunController.Instance.RoundStarted;
        public override void Interact(PlayerInteractor p){if(!CanInteract(p))return;SchoolRunController.Instance.TakeTool(tool);visual.SetActive(false);}
    }
}

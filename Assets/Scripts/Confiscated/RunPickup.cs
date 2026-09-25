using UnityEngine;
namespace Confiscated
{
    public sealed class RunPickup : Interactable
    {
        public int itemId;
        public string itemName;
        public GameObject visual;
        void Start(){CollectibleMotion.Attach(visual);}
        public override string GetPrompt(PlayerInteractor p) => SchoolRunController.Instance!=null&&SchoolRunController.Instance.Has(itemId) ? "CONFISCATED - empty" :
            CanInteract(p) ? "F: recover " + itemName : "Open this storage area first.";
        public override bool CanInteract(PlayerInteractor p) => SchoolRunController.Instance != null && SchoolRunController.Instance.CanRecover(itemId);
        public override void Interact(PlayerInteractor p)
        {
            if (!CanInteract(p)) return;
            SchoolRunController.Instance.Recover(itemId);
            if (visual != null) visual.SetActive(false);
        }
        public void Restore() { if (visual != null) visual.SetActive(true); }
    }
}



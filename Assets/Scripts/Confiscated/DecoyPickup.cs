using UnityEngine;
namespace Confiscated
{
    public sealed class DecoyPickup : Interactable
    {
        public GameObject visual;
        bool taken;
        void Start(){CollectibleMotion.Attach(visual);}
        public override string GetPrompt(PlayerInteractor p) => taken ? null : p.GetComponent<ClockworkDecoy>().Charges >= 3 ? "You already have three wind-up toys." : "F: take wind-up decoy (1 or right click to place)";
        public override bool CanInteract(PlayerInteractor p) => !taken && p.GetComponent<ClockworkDecoy>().Charges < 3;
        public override void Interact(PlayerInteractor p)
        {
            if (!CanInteract(p)) return;
            taken = true; p.GetComponent<ClockworkDecoy>().Collect(); if (visual != null) visual.SetActive(false);
        }
    }
}


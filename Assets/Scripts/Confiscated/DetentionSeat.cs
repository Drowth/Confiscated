using UnityEngine;
namespace Confiscated
{
    public class DetentionSeat : Interactable
    {
        public DetentionController detention;
        public Transform sittingPoint;
        public override bool CanInteract(PlayerInteractor player) => detention != null && detention.Active && !detention.Seated;
        public override string GetPrompt(PlayerInteractor player) => detention == null || !detention.Active ? "A detention desk." :
            "Detention desk";
        public override void Interact(PlayerInteractor player) { if (CanInteract(player)) detention.Sit(this); }
    }
}

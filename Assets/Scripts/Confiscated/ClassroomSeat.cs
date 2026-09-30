using UnityEngine;

namespace Confiscated
{
    /// <summary>Return-to-class checkpoint for the old office chapter. In the school run it is only where Smith sits for the opening.</summary>
    public class ClassroomSeat : Interactable
    {
        public OfficeMission mission;
        public Transform seatedView;
        public override string GetPrompt(PlayerInteractor player) => GameManager.Instance?.schoolPeriod!=null?null:GameManager.Instance != null && GameManager.Instance.PhoneStored ? "F: sit down" : player.HasPhone ?
            "F: sit down and hide your phone" : "Your desk. Get your phone back before the lesson starts.";
        public override bool CanInteract(PlayerInteractor player) => GameManager.Instance?.schoolPeriod!=null?false:(player.HasPhone || (GameManager.Instance != null && GameManager.Instance.PhoneStored)) && mission.CanReturnToClass;
        public override void Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return;
            if (player.HeldBall != null) player.HeldBall.Throw(Vector3.zero);
            mission.ReturnToSeat(player);
            if (seatedView != null) player.ViewCamera.transform.SetPositionAndRotation(seatedView.position, seatedView.rotation);
        }
    }
}

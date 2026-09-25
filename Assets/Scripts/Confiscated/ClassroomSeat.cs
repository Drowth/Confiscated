using UnityEngine;

namespace Confiscated
{
    /// <summary>Return-to-class checkpoint. Lesson design is kept separate from the office mission.</summary>
    public class ClassroomSeat : Interactable
    {
        public OfficeMission mission;
        public Transform seatedView;
        public override string GetPrompt(PlayerInteractor player) => GameManager.Instance?.schoolPeriod!=null?GameManager.Instance.schoolPeriod.SeatPrompt:GameManager.Instance != null && GameManager.Instance.PhoneStored ? "F: sit down" : player.HasPhone ?
            "F: sit down and hide your phone" : "Your desk. Get your phone back before the lesson starts.";
        public override bool CanInteract(PlayerInteractor player) => GameManager.Instance?.schoolPeriod!=null?GameManager.Instance.schoolPeriod.CanUseSeat:(player.HasPhone || (GameManager.Instance != null && GameManager.Instance.PhoneStored)) && mission.CanReturnToClass;
        public override void Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return;
            if(GameManager.Instance?.schoolPeriod!=null){GameManager.Instance.schoolPeriod.UseSeat();return;}
            if (player.HeldBall != null) player.HeldBall.Throw(Vector3.zero);
            mission.ReturnToSeat(player);
            if (seatedView != null) player.ViewCamera.transform.SetPositionAndRotation(seatedView.position, seatedView.rotation);
        }
    }
}

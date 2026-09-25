using UnityEngine;

namespace Confiscated
{
    /// <summary>The window the player climbed in through. Climbing out with the phone wins the round.</summary>
    public class EscapeWindow : Interactable
    {
        void Reset() { holdSeconds = 2.0f; }

        public override string GetPrompt(PlayerInteractor player) =>
            GameManager.Instance != null && GameManager.Instance.officeMission != null ? "You need to get back to class, not leave school." :
            player.HasPhone ? "Hold F: climb out of the window" : "The window you came in through. Get your phone first.";

        public override bool CanInteract(PlayerInteractor player) => player.HasPhone &&
            (GameManager.Instance == null || GameManager.Instance.officeMission == null);

        public override void Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return;
            GameManager.Instance?.Win();
        }
    }
}

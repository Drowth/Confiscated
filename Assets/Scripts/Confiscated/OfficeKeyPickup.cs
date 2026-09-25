using UnityEngine;

namespace Confiscated
{
    public class OfficeKeyPickup : Interactable
    {
        public OfficeMission mission;
        void Start(){CollectibleMotion.AttachMeshes(transform);}
        public override string GetPrompt(PlayerInteractor player) => mission.HasKey ? null :
            SchoolRunController.Instance != null ? SchoolRunController.Instance.KeyAvailable ? "Hold F: take the Caretaker's Office key" : "The caretaker is taking his trolley to the dining hall." : mission.CaretakerAway ? "F: take the Caretaker's Office key" : "Wait until the caretaker is away from the office.";
        public override bool CanInteract(PlayerInteractor player) => !mission.HasKey && (SchoolRunController.Instance != null ? SchoolRunController.Instance.KeyAvailable : mission.CaretakerAway) &&
            (player.GetComponent<PlayerInventory>() == null || player.GetComponent<PlayerInventory>().CanCollect(player.GetComponent<PlayerInventory>().keyItem));
        public override void Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return;
            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null && !inventory.Collect(inventory.keyItem)) return;
            if (!mission.TakeKey()) return;
            TempAudio.PlayAt(TempAudio.Pickup, transform.position, .5f);
            gameObject.SetActive(false);
        }
    }
}


using UnityEngine;

namespace Confiscated
{
    /// <summary>The confiscated phone in the box on the caretaker's desk.</summary>
    public class PhonePickup : Interactable
    {
        [Tooltip("Visual of the phone inside the box; hidden once collected.")]
        public GameObject phoneVisual;

        void Start(){CollectibleMotion.Attach(phoneVisual);}
        bool collected;
        public void ReturnToOffice()
        {
            collected = false;
            if (phoneVisual != null) phoneVisual.SetActive(true);
        }

        public override string GetPrompt(PlayerInteractor player) => collected ? "CONFISCATED - empty" :
            player.GetComponent<PlayerInventory>() != null && !player.GetComponent<PlayerInventory>().CanCollect(player.GetComponent<PlayerInventory>().phoneItem) ? "Your satchel is full." :
            !CanInteract(player) ? "Get the spare key and unlock the office first." : "F: take your phone back";

        public override bool CanInteract(PlayerInteractor player) => isActiveAndEnabled && !collected &&
            (player.GetComponent<PlayerInventory>() == null || player.GetComponent<PlayerInventory>().CanCollect(player.GetComponent<PlayerInventory>().phoneItem)) &&
            (GameManager.Instance == null || GameManager.Instance.officeMission == null || GameManager.Instance.officeMission.OfficeUnlocked);

        public override void Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return;
            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null && !inventory.Collect(inventory.phoneItem)) return;
            collected = true;
            if (phoneVisual != null) phoneVisual.SetActive(false);
            player.HasPhone = true;
            if(SchoolRunController.Instance==null)TempAudio.PlayAt(TempAudio.Pickup, transform.position, 0.6f);
            GameManager.Instance?.OnPhoneCollected(player);
        }
    }
}


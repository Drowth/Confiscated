namespace Confiscated
{
    public class PlayerLocker : Interactable
    {
        public LockerStorageUI storageUI;
        public override string GetPrompt(PlayerInteractor player)=>SchoolGameMode.Dark&&player.GetComponent<PlayerInventory>().Contains(InventoryContainer.Locker,InventoryItemKind.Torch)?"F: open your locker - torch inside":"F: open your locker";
        public override bool CanInteract(PlayerInteractor player)=>GameManager.Instance==null||GameManager.Instance.IsPlaying;
        public override void Interact(PlayerInteractor player)
        {
            if(CanInteract(player))storageUI.Open(player,this);
        }
    }
}

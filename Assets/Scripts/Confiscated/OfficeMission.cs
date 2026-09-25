using UnityEngine;

namespace Confiscated
{
    /// <summary>The first chapter: wait for rounds, borrow the key, recover the phone and return to class.</summary>
    public class OfficeMission : MonoBehaviour
    {
        public enum Stage { WaitForRounds, TakeKey, UnlockOffice, RecoverPhone, ReturnToClass, Complete, PhoneInLocker, KeyInLocker }
        public CaretakerAI caretaker;
        public OfficeKeyPickup key;
        public OfficeDoor door;
        public Bounds officeBounds = new Bounds(new Vector3(-1.5f, 1.5f, .15f), new Vector3(6.4f, 6f, 4.7f));
        public Stage Current { get; private set; }
        public bool HasKey { get; private set; }
        public bool OfficeUnlocked { get; private set; }
        public bool ReadyToFinish => Current == Stage.Complete;
        public bool CanReturnToClass => Current == Stage.ReturnToClass || Current == Stage.PhoneInLocker;

        public bool CaretakerAway
        {
            get
            {
                if (caretaker == null) return true;
                Vector3 p = caretaker.transform.position;
                bool inOffice = officeBounds.Contains(p);
                return !inOffice && (key == null || Vector3.Distance(p, key.transform.position) > 3.2f);
            }
        }

        public void Begin()
        {
            HasKey = false;
            OfficeUnlocked = false;
            SetStage(CaretakerAway ? Stage.TakeKey : Stage.WaitForRounds);
            HudController.Instance?.SetStatus("Staff leave confiscated property in the caretaker's locked office.", 6f);
        }

        void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying || HasKey) return;
            if (Current != Stage.WaitForRounds && Current != Stage.TakeKey) return;
            Stage next = CaretakerAway ? Stage.TakeKey : Stage.WaitForRounds;
            if (Current != next) SetStage(next);
        }

        public bool TakeKey()
        {
            if (HasKey || (SchoolRunController.Instance != null ? !SchoolRunController.Instance.KeyAvailable : !CaretakerAway)) return false;
            HasKey = true;
            SetStage(Stage.UnlockOffice);
            HudController.Instance?.SetStatus("Spare office key pocketed.", 3f);
            return true;
        }

        public bool Unlock()
        {
            var inventory=Object.FindFirstObjectByType<PlayerInventory>();
            if(inventory!=null?!inventory.HasCarried(InventoryItemKind.OfficeKey):!HasKey)return false;
            OfficeUnlocked = true;
            SetStage(Stage.RecoverPhone);
            return true;
        }

        public void PhoneRecovered() { SetStage(Stage.ReturnToClass); }
        public void PhoneStored() { SetStage(Stage.PhoneInLocker); }
        public void SetKeyLocation(bool carried, bool stored)
        {
            HasKey=carried;
            if(OfficeUnlocked)return;
            if(stored)SetStage(Stage.KeyInLocker);
            else if(carried)SetStage(Stage.UnlockOffice);
        }
        public void PhoneConfiscated()
        {
            SetStage(OfficeUnlocked ? Stage.RecoverPhone : HasKey ? Stage.UnlockOffice :
                CaretakerAway ? Stage.TakeKey : Stage.WaitForRounds);
        }
        public void RefreshObjective() { SetStage(Current); }

        public void ReturnToSeat(PlayerInteractor player)
        {
            if (!CanReturnToClass || (!player.HasPhone && !(GameManager.Instance != null && GameManager.Instance.PhoneStored))) return;
            SetStage(Stage.Complete);
            GameManager.Instance?.Win();
        }

        void SetStage(Stage stage)
        {
            Current = stage;
            // The five-item run owns the objective; legacy chapter instructions must not flash over it.
            if(SchoolRunController.Instance!=null)return;
            string text = stage switch
            {
                Stage.WaitForRounds => "GET YOUR PHONE BACK\nWait for the caretaker to leave on his rounds.\nHis spare office key is on the maintenance trolley.",
                Stage.TakeKey => "HE'S OUT ON HIS ROUNDS\nTake the spare key from the maintenance trolley.\nKeep out of his sight.",
                Stage.UnlockOffice => "Unlock the caretaker's office door.",
                Stage.RecoverPhone => "INSIDE THE OFFICE\nFind your phone in the confiscated-property box.",
                Stage.ReturnToClass => "PHONE RECOVERED\nReturn to Year 6.",
                Stage.PhoneInLocker => "PHONE STORED\nReturn to Year 6.",
                Stage.KeyInLocker => "SPARE KEY IN YOUR LOCKER",
                _ => "BACK IN CLASS\nPhone hidden. Book open. Act normal."
            };
            HudController.Instance?.SetObjective(text);
        }
    }
}

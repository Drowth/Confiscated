using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Confiscated
{
    /// <summary>Five recoveries, permanent unlocks, and one recoverable loss per detention.</summary>
    public sealed class SchoolRunController : MonoBehaviour
    {
        public static SchoolRunController Instance { get; private set; }
        public SchoolPeriodController period;
        public CaretakerAI caretaker, secondStaff;
        public Transform trolley, trolleyDock;
        public RunPickup[] pickups;
        [Tooltip("HUD checklist artwork in run order: phone, yo-yo, handheld, skateboard, robot. Assigned by ChecklistIconSetup.")]
        public Texture2D[] checklistIcons;
        public bool RoundStarted { get; private set; }
        public bool RecoveryBegun => RoundStarted || (period != null && period.PapersDelivered);
        public bool TrolleyParked { get; private set; }
        public int UnlockLevel { get; private set; }
        public int Detentions { get; private set; }
        public bool HasBoltCutters {get;private set;}
        public bool HasStoreKey {get;private set;}
        public bool CageOpen {get;set;}
        public bool CanOpenStorage(int level)=>RoundStarted&&(level<4||HasStoreKey);
        /// <summary>True during the classroom errand. Only the west wing is open until the run begins.</summary>
        public static bool LessonsInProgress => Instance != null && !Instance.RoundStarted;
        public void TakeTool(AccessToolPickup.Tool tool){if(tool==AccessToolPickup.Tool.BoltCutters)HasBoltCutters=true;else HasStoreKey=true;HudController.Instance?.SetStatus(tool==AccessToolPickup.Tool.BoltCutters?"Bolt cutters. Use them on the dining cage chain.":"Store key. Opens the southeast store.",4);}
        public readonly List<int> RecoveryOrder = new List<int>();
        public int Count => RecoveryOrder.Count;
        public bool Has(int id) => RecoveryOrder.Contains(id);
        public bool ReadyToEscape => RoundStarted && Count == 5 && period.Player.HasPhone;
        public bool KeyAvailable => TrolleyParked && period.PhoneDeposited;
        public Bounds Dining => new Bounds(new Vector3(-19.5f, 1.5f, 58.75f), new Vector3(25.1f, 4, 33.4f));
        public Bounds Serving => new Bounds(new Vector3(-20.4f, 1.5f, 44.8f), new Vector3(22, 4, 5.3f));
        public readonly RunTiming Timing = new RunTiming();
        string timingMode;
        Collider[] cartColliders;
        bool movingCart;
        float pursuitPulse;
        void Awake()
        {
            Instance = this; gameObject.AddComponent<EscapeRunFeedback>();gameObject.AddComponent<SchoolNoiseMarks>();gameObject.AddComponent<HuntVhsEffect>();gameObject.AddComponent<HuntFaces>();
            if (secondStaff != null && secondStaff.GetComponent<ReedDirectionalArt>() == null)
                secondStaff.gameObject.AddComponent<ReedDirectionalArt>();
            CopycatStudent.Install(this);
            cartColliders = trolley != null ? trolley.GetComponentsInChildren<Collider>() : new Collider[0];
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
        public bool Restricted(Vector3 p) => period.OfficeBounds.Contains(p) ||
            (!RoundStarted ? Dining.Contains(p) : Serving.Contains(p));
        /// <summary>Somewhere the caretaker treats as an offence on sight. Only before the round: after it he chases on sight everywhere.</summary>
        public bool Trespassing => !RoundStarted && period != null && period.IsRoaming && period.Player != null && Restricted(period.Player.transform.position);
        public void PrepareChaseRetry()
        {
            trolley.SetPositionAndRotation(trolleyDock.position,trolleyDock.rotation);
            TrolleyParked=true;
            foreach(var c in cartColliders)c.enabled=true;
            caretaker.GetComponent<NavMeshAgent>().Warp(trolleyDock.position);
            timingMode="Classroom start";
            BeginRound(26);
            // His usual first patrol stop ("Collect phone", index 0) sits right on the player's new post-key-grant
            // route out of the classroom. Resume further round the loop so a retry doesn't spawn him on the way out.
            caretaker.ResumePatrolFrom(5,26);
            // A retry already showed the player the dining-hall key trip once; skip repeating it every death.
            var keyPickup = Object.FindFirstObjectByType<OfficeKeyPickup>(FindObjectsInactive.Include);
            bool keyGranted = keyPickup != null && keyPickup.CanInteract(period.Player);
            if (keyGranted) keyPickup.Interact(period.Player);
            HudController.Instance?.SetStatus(keyGranted ? "You already have the office key. Recover five belongings and escape." : "Recover five belongings and escape. The caretaker is on patrol.",6);
        }
        public void BeginRound(float firstPulseDelay = 12)
        {
            if(RoundStarted)return; RoundStarted = true; Timing.Start(timingMode ?? (Has(0)?"Phone start":"Lesson start")); pursuitPulse=Time.time+firstPulseDelay;
            ClockworkDecoy.ResetRunTally();
            period.Player.GetComponent<PlayerInventory>().RemoveCarried(InventoryItemKind.HallPass);
            caretaker.ResumeAfterDetention(5);
            caretaker.GetComponent<CaretakerPassCheck>()?.ResetPermission();
            ApplyPressure();
            HudController.Instance?.SetStatus("Five things to recover. Then the main entrance. Keep moving.", 8);
        }
        public bool CanRecover(int id) => !Has(id) && (id == 0 || RoundStarted && (id!=1||CageOpen) && (id!=4||HasStoreKey));
        public void Recover(int id)
        {
            if (!CanRecover(id)) return;
            RecoveryOrder.Add(id); UnlockLevel = Mathf.Max(UnlockLevel, id + 1); if(id==0&&!RoundStarted){period.StartEscapeRun();BeginRound();}
            ApplyPressure();
            if (RoundStarted) {NoiseEvents.Emit(period.Player.transform.position, 45, "property box latch");GetComponent<EscapeRunFeedback>()?.PickupCue();}
            string[] rewards = { "Phone recovered! Four more belongings are in CONFISCATED boxes.", "Yo-yo recovered.", "Handheld game recovered.", "Skateboard recovered.", "Toy robot recovered." };
            HudController.Instance?.SetStatus(rewards[id], 6);
            if (Count == 5) Object.FindFirstObjectByType<SchoolBellSystem>()?.Ring();
        }
        public void PenalizeCatch()
        {
            Detentions++;
            if (RecoveryOrder.Count == 0) return;
            int lost = RecoveryOrder[RecoveryOrder.Count - 1]; RecoveryOrder.RemoveAt(RecoveryOrder.Count - 1);
            if (lost == 0)
            {
                period.Player.GetComponent<PlayerInventory>().RemovePhoneEverywhere();
                period.Player.HasPhone = false;
                GameManager.Instance.SetPhoneLocation(false, false);
                period.phonePickup.ReturnToOffice();
            }
            else foreach (var pickup in pickups) if (pickup != null && pickup.itemId == lost) pickup.Restore();
            ApplyPressure();
        }
        public void PauseStaff() { secondStaff?.Freeze(); Object.FindFirstObjectByType<PeCoach>()?.Pause(); }
        public void ResumeStaff()
        {
            Object.FindFirstObjectByType<PeCoach>()?.Resume();
            caretaker.GetComponent<CaretakerPassCheck>()?.ResetPermission();
            if (secondStaff != null && secondStaff.enabled) secondStaff.ResumeAfterDetention(8);
        }
        void ApplyPressure()
        {
            caretaker.SetRunPressure(Count);
            if (secondStaff != null)
            {
                if (RoundStarted && Count >= 3)
                {
                    bool starting = !secondStaff.enabled || secondStaff.Current == CaretakerAI.State.Frozen;
                    secondStaff.enabled = true; secondStaff.SetRunPressure(Count);
                    if (starting) secondStaff.ResumeAfterDetention(5);
                }
                else if (secondStaff.enabled) secondStaff.Freeze();
            }
        }
        void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
            if (!TrolleyParked && trolley != null && period.PhoneDeposited)
            {
                if (!movingCart) { movingCart = true; foreach (var c in cartColliders) c.enabled = false; }
                trolley.position = caretaker.transform.position - caretaker.transform.forward * .85f;
                trolley.rotation = caretaker.transform.rotation;
                if (Vector3.Distance(caretaker.transform.position, trolleyDock.position) < 1.8f)
                {
                    trolley.SetPositionAndRotation(trolleyDock.position, trolleyDock.rotation);
                    TrolleyParked = true;
                    foreach (var c in cartColliders) c.enabled = true;
                }
            }
            if (RoundStarted && Time.time >= pursuitPulse)
            {
                pursuitPulse = Time.time + Mathf.Lerp(28, 12, Count / 5f);
                // Investigate an approximate area, never supply a hidden player's exact position.
                Vector3 p = period.Player.transform.position;
                Vector3 approximate = new Vector3(Mathf.Round(p.x / 14) * 14, 0, Mathf.Round(p.z / 14) * 14);
                caretaker.InvestigateArea(approximate);
            }
        }
        void LateUpdate()
        {
            if (!RoundStarted || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
            string next;
            if (Count == 5) next = period.Player.HasPhone ? "Escape through the MAIN ENTRANCE." : "Retrieve your phone from your locker.";
            else if (!Has(0)) next = period.Player.GetComponent<PlayerInventory>().HasCarried(InventoryItemKind.OfficeKey)?"Use the OFFICE KEY. Recover your phone from the office box.":"Take the OFFICE KEY from the trolley in DINING HALL.";
            else if (!HasBoltCutters&&!Has(1)) next = "Take bolt cutters from the caretaker workbench.";
            else if (!Has(1)) next = CageOpen?"Find the CONFISCATED box in DINING HALL.":"Open the dining cage chain with E.";
            else if (!Has(2)) next = "Find the CONFISCATED box in RESOURCES.";
            else if (!Has(3)) next = "Find the CONFISCATED box in EQUIPMENT.";
            else next = HasStoreKey?"Find the CONFISCATED box in STORE.":"Take the store key from the EQUIPMENT desk.";
            HudController.Instance?.SetObjective("Belongings recovered: " + Count + "/5\n" + next);
        }
        public void Escape()
        {
            if (!ReadyToEscape) return;
            PauseStaff();
            GameManager.Instance.CompleteSchoolRun("All five belongings recovered.");
        }
    }
}

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
        public bool CorridorDoorsReleased {get;private set;}
        public void ReleaseCorridorDoors()=>CorridorDoorsReleased=true;
        public bool CanOpenStorage(int level)=>RoundStarted&&(level<4||HasStoreKey);
        /// <summary>True during the classroom errand. Only the west wing is open until the run begins.</summary>
        public static bool LessonsInProgress => Instance != null && !Instance.RoundStarted;
        public void TakeTool(AccessToolPickup.Tool tool){if(tool==AccessToolPickup.Tool.BoltCutters)HasBoltCutters=true;else HasStoreKey=true;ItemCodex.PickUp(tool==AccessToolPickup.Tool.BoltCutters?Items.BoltCutters:Items.StoreKey);}
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
        bool openingAtOffice;
        float trolleyPickupAt;
        float pursuitPulse;
        void Awake()
        {
            Instance = this; gameObject.AddComponent<EscapeRunFeedback>();gameObject.AddComponent<SchoolNoiseMarks>();gameObject.AddComponent<HuntVhsEffect>();gameObject.AddComponent<HuntFaces>();gameObject.AddComponent<WindowWatchers>();
            if (secondStaff != null && secondStaff.GetComponent<ReedDirectionalArt>() == null)
                secondStaff.gameObject.AddComponent<ReedDirectionalArt>();
            // Mr Reed (the lesson teacher and, from three items, the second hunter) is one object; give him footsteps.
            if (secondStaff != null && secondStaff.GetComponent<StaffFootsteps>() == null)
                secondStaff.gameObject.AddComponent<StaffFootsteps>();
            CopycatStudent.Install(this);
            cartColliders = trolley != null ? trolley.GetComponentsInChildren<Collider>() : new Collider[0];
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
        public bool Restricted(Vector3 p) => period.OfficeBounds.Contains(p) ||
            (!RoundStarted ? Dining.Contains(p) : Serving.Contains(p));
        /// <summary>Somewhere the caretaker treats as an offence on sight. Only before the round: after it he chases on sight everywhere.</summary>
        public bool Trespassing => !RoundStarted && period != null && period.IsRoaming && period.Player != null && Restricted(period.Player.transform.position);
        /// <summary>The player has taken the office key themselves at least once since the school day began. Survives the
        /// retry scene reload; cleared when a new school day starts (GameManager.BeginSchoolDay).</summary>
        public static bool KeyEarned { get; set; }
        public const float KeyWindowSeconds = 15f;
        bool keyWindowOffered,retryKeyWindowPending,diningDoorOpened;
        /// <summary>The caretaker is at the serving hatch with his back to his parked trolley.</summary>
        public bool KeyWindowOpen => keyWindowOffered && caretaker != null && caretaker.ChatArrived;
        void Start()
        {
            // Keep the opening delivery brisk enough to follow. SetRunPressure restores the chase pace and dwells.
            if (!RoundStarted && caretaker != null && caretaker.patrol.Count > 1)
            {
                caretaker.SetErrandPace(2.3f);
                caretaker.patrol[0].dwellSeconds = 2; caretaker.patrol[1].dwellSeconds = 2;
            }
        }
        /// <summary>The phone handoff starts a committed trip into the office, then to the cafeteria dock.</summary>
        public void StartOpeningRoute()
        {
            openingAtOffice=false;movingCart=false;trolleyPickupAt=0;
            caretaker.BeginOpeningTravel(period.officeDrop.position);
        }
        void TickOpeningRoute()
        {
            if(!caretaker.OpeningRouteActive||TrolleyParked||movingCart)return;
            if(!openingAtOffice)
            {
                if(Vector3.Distance(caretaker.transform.position,period.officeDrop.position)>.6f)return;
                period.DepositPhone();openingAtOffice=true;trolleyPickupAt=Time.time+1.5f;
                caretaker.PauseForPass(true);
            }
            if(Time.time<trolleyPickupAt)return;
            movingCart=true;
            foreach(var c in cartColliders)c.enabled=false;
            caretaker.BeginOpeningTravel(trolleyDock.position);
        }
        public void PrepareChaseRetry()
        {
            // Sweets and the chatterbox's bench start over; he does not repeat the rumour.
            period.Player.GetComponent<Sweets>()?.ResetRun();Object.FindFirstObjectByType<SweetJar>()?.Refill();Object.FindFirstObjectByType<ChatterboxStudent>()?.ResetRun();
            trolley.SetPositionAndRotation(trolleyDock.position,trolleyDock.rotation);
            TrolleyParked=true;
            foreach(var c in cartColliders)c.enabled=true;
            caretaker.GetComponent<NavMeshAgent>().Warp(trolleyDock.position);
            timingMode="Classroom start";
            BeginRound(26);
            // His usual first patrol stop ("Collect phone", index 0) sits right on the player's new post-key-grant
            // route out of the classroom. Resume further round the loop so a retry doesn't spawn him on the way out.
            caretaker.ResumePatrolFrom(5,26);
            // Once the player has fetched the key from the dining-hall trolley, a retry skips repeating that trip. Caught
            // before they ever had it (e.g. during the newsletter errand), they still have to go and get it.
            var keyPickup = Object.FindFirstObjectByType<OfficeKeyPickup>(FindObjectsInactive.Include);
            bool keyGranted = KeyEarned && keyPickup != null && keyPickup.CanInteract(period.Player);
            if (keyGranted) keyPickup.Interact(period.Player);
            retryKeyWindowPending=!period.Player.GetComponent<PlayerInventory>().HasCarried(InventoryItemKind.OfficeKey);
            if(retryKeyWindowPending){keyWindowOffered=false;caretaker.Freeze();}
            HudController.Instance?.SetStatus(keyGranted ? "You already have the office key. Recover five belongings and escape." : "Open the DINING HALL door. Wait for the dinner lady to distract the caretaker.",6);
        }
        public void BeginRound(float firstPulseDelay = 12)
        {
            if(RoundStarted)return; caretaker.CompleteOpeningRoute();RoundStarted = true; Timing.Start(timingMode ?? (Has(0)?"Phone start":"Lesson start")); pursuitPulse=Time.time+firstPulseDelay;
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
            // The item pop-up says what it is; only the phone needs the what-next line.
            ItemCodex.PickUp(Items.Belongings[id]);
            if (id == 0) HudController.Instance?.SetStatus("Four more belongings are in CONFISCATED boxes.", 6);
            // The phone lights up with a friend's text: what the run is about.
            if (id == 0) PhoneMessage.Show();
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
        /// <summary>Puts one recovered belonging back in its CONFISCATED box (the library shadow takes back the handheld game).</summary>
        public bool ReturnToBox(int id)
        {
            if (id == 0 || !Has(id)) return false;
            RecoveryOrder.Remove(id);
            foreach (var pickup in pickups) if (pickup != null && pickup.itemId == id) pickup.Restore();
            ApplyPressure(); return true;
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
            if(ComicDialogue.IsActive)return;
            TickOpeningRoute();
            if (!TrolleyParked && trolley != null && movingCart)
            {
                trolley.position = caretaker.transform.position - caretaker.transform.forward * .85f;
                trolley.rotation = caretaker.transform.rotation;
                if (Vector3.Distance(caretaker.transform.position, trolleyDock.position) < .6f)
                {
                    trolley.SetPositionAndRotation(trolleyDock.position, trolleyDock.rotation);
                    TrolleyParked = true;movingCart=false;
                    caretaker.PauseForPass(true);
                    foreach (var c in cartColliders) c.enabled = true;
                }
            }
            bool hasOfficeKey=period.Player.GetComponent<PlayerInventory>().HasCarried(InventoryItemKind.OfficeKey);
            if(retryKeyWindowPending&&hasOfficeKey)
            {retryKeyWindowPending=false;caretaker.ResumeAfterDetention(5);}
            if(diningDoorOpened)TryStartKeyDistraction();
            if (RoundStarted && !retryKeyWindowPending && Time.time >= pursuitPulse)
            {
                pursuitPulse = Time.time + Mathf.Lerp(28, 12, Count / 5f);
                // Investigate an approximate area, never supply a hidden player's exact position.
                Vector3 p = period.Player.transform.position;
                Vector3 approximate = new Vector3(Mathf.Round(p.x / 14) * 14, 0, Mathf.Round(p.z / 14) * 14);
                caretaker.InvestigateArea(approximate);
            }
        }
        /// <summary>Only a player opening a dining entrance starts the key distraction, once per attempt.</summary>
        public void PlayerOpenedDoor(OfficeDoor door)
        {
            // The hand-authored dining entrances share this scene naming convention.
            if(door==null||!door.name.StartsWith("Dining ",System.StringComparison.Ordinal))return;
            if(GameManager.Instance==null||!GameManager.Instance.IsPlaying)return;
            diningDoorOpened=true;
            TryStartKeyDistraction();
        }
        // Opening the door can precede trolley parking on a fresh day.
        // Keep the request until those prerequisites are ready instead of losing the one-shot interaction.
        void TryStartKeyDistraction()
        {
            bool hasOfficeKey=period.Player.GetComponent<PlayerInventory>().HasCarried(InventoryItemKind.OfficeKey);
            if (((!RoundStarted&&period.IsRoaming)||retryKeyWindowPending) && !keyWindowOffered && TrolleyParked && !hasOfficeKey)
            {
                var lady=Object.FindFirstObjectByType<DinnerTrolleyPatrol>();
                Vector3 dock=trolleyDock.position,spot=dock+new Vector3(5f,0,-3.6f);
                if(lady!=null)
                {
                    Vector3 towardDock=dock-lady.transform.position;towardDock.y=0;
                    spot=lady.transform.position+towardDock.normalized*1.8f;
                }
                if(NavMesh.SamplePosition(spot,out var hatch,1.5f,NavMesh.AllAreas))spot=hatch.position;
                float conversation=KeyWindowSeconds+Vector3.Distance(caretaker.transform.position,spot)/1.7f+10;
                retryKeyWindowPending=false;
                // Protect the conversation on fresh days as well as retries.
                caretaker.CompleteOpeningRoute();
                caretaker.ResumeAfterDetention(conversation);
                pursuitPulse=Time.time+conversation;
                keyWindowOffered = true;
                Vector3 away= lady!=null?lady.transform.position-spot:spot-dock;away.y=0;
                caretaker.Chat(spot, away.normalized, KeyWindowSeconds);
                lady?.RequestFreezerRepair(conversation);
                var request=Resources.Load<AudioClip>("Audio/DinnerLadyFreezerRequest");
                HudController.Instance?.SetBark("Dinner lady: Caretaker, have you got a minute, love? The freezer's making that noise again.",request!=null?request.length:5f);
            }
        }
        void LateUpdate()
        {
            if (!RoundStarted || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
            string next;
            if (Count == 5) next = period.Player.HasPhone ? "Escape through the MAIN ENTRANCE." : "Retrieve your phone from your locker.";
            else if (!Has(0)) next = period.Player.GetComponent<PlayerInventory>().HasCarried(InventoryItemKind.OfficeKey)?"Use the OFFICE KEY. Recover your phone from the office box.":"Take the OFFICE KEY from the trolley in DINING HALL.";
            else if (!CorridorDoorsReleased) next = "Press the BLUE DOORS button on the caretaker's workbench.";
            else if (!HasBoltCutters&&!Has(1)) next = "Take bolt cutters from the checkout desk in EQUIPMENT.";
            else if (!Has(1)) next = CageOpen?"Find the CONFISCATED box in DINING HALL.":"Hold F on the dining cage chain.";
            else if (!Has(2)) next = "Find the CONFISCATED box in the LIBRARY.";
            else if (!Has(3)) next = "Find the CONFISCATED box in EQUIPMENT.";
            else next = HasStoreKey?"Find the CONFISCATED box in STORE.":"Take the STORE key from the key cabinet in the SCHOOL OFFICE.";
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

using UnityEngine;

namespace Confiscated
{
    /// <summary>Player key lock with automatic access for the caretaker's patrol.</summary>
    public class OfficeDoor : Interactable
    {
        public Transform hinge;
        public Transform secondHinge;
        public float secondOpenAngle = 102f;
        public CaretakerAI caretaker;
        public OfficeMission mission;
        public DetentionController detention;
        bool DetentionLocked => detention != null && detention.Active;
        public float openAngle = -102f;
        public bool startsUnlocked;
        public bool closedForRun;
        [Tooltip("Shut during the classroom errand; an ordinary door once the escape run begins.")]
        public bool closedDuringLessons;
        public bool LessonLocked => closedDuringLessons && SchoolRunController.LessonsInProgress;
        [Tooltip("The main entrance: shut for the whole run, and holding E on it with all five belongings is the escape.")]
        public bool mainExit;
        bool ExitGated => mainExit && SchoolRunController.Instance != null;
        bool escaped;
        /// <summary>True once the escape has swung the main entrance open.</summary>
        public bool Escaped => escaped;
        public int runRequiredLevel;
        public string runLockName;
        public bool IsUnlocked { get; private set; }
        public bool IsOpen => openAmount > .9f;
        DoorSounds doorSounds;
        bool requestedOpen;
        float openAmount;
        Quaternion closedRotation;
        Quaternion secondClosedRotation;

        void Awake()
        {
            closedRotation = hinge.localRotation;
            if (secondHinge != null) secondClosedRotation = secondHinge.localRotation;
            IsUnlocked = startsUnlocked;
            doorSounds=DoorSounds.For(gameObject,mission!=null?DoorSounds.Kind.Office:secondHinge!=null?DoorSounds.Kind.Heavy:DoorSounds.Kind.Generic);
        }

        public override string GetPrompt(PlayerInteractor player)
        {
            if (SchoolRunController.Instance != null && closedForRun) return "Closed for this school day.";
            if (LessonLocked) return "Closed during lessons.";
            if (ExitGated)
            {
                var run = SchoolRunController.Instance;
                if (run.ReadyToEscape) return "Hold F: LEG IT - escape with all five belongings";
                if (!run.RoundStarted) return "MAIN ENTRANCE - locked during lessons.";
                return run.Count == 5 ? "MAIN ENTRANCE - fetch your phone from your locker first." : "MAIN ENTRANCE - locked. Recover all five belongings to leave (" + run.Count + " / 5).";
            }
            if (SchoolRunController.Instance != null && runRequiredLevel > 0 && !IsUnlocked)
                return SchoolRunController.Instance.CanOpenStorage(runRequiredLevel) ? "F: unlock " + runLockName : runRequiredLevel>=4?"STORE LOCKED - Find its key in EQUIPMENT.":"Lesson in progress. Recover your phone first.";
            if (DetentionLocked) return "Detention. Clear all three blackboards, then clap the rubbers clean to leave.";
            if (!IsUnlocked) return mission != null && HasCarriedKey(player) ? "F: Unlock office" : "Locked. Find the office key on the dining-hall trolley.";
            return requestedOpen ? "F: close the door" : "F: open the door";
        }

        static bool HasCarriedKey(PlayerInteractor player)=>player.GetComponent<PlayerInventory>()?.HasCarried(InventoryItemKind.OfficeKey)??false;
        public override bool CanInteract(PlayerInteractor player) => ExitGated ? SchoolRunController.Instance.ReadyToEscape : !DetentionLocked && !LessonLocked && !(SchoolRunController.Instance != null && closedForRun) && (IsUnlocked || (SchoolRunController.Instance != null && runRequiredLevel > 0 ? SchoolRunController.Instance.CanOpenStorage(runRequiredLevel) : mission != null && HasCarriedKey(player)));
        public void SetDetentionDoor(bool locked)
        {
            requestedOpen = !locked;
            if (!locked) return;
            if(openAmount>0)doorSounds?.Play(false);
            openAmount = 0;
            hinge.localRotation = closedRotation;
            if (secondHinge != null) secondHinge.localRotation = secondClosedRotation;
        }

        public override void Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return;
            if (ExitGated)
            {
                escaped = true; requestedOpen = true;
                SchoolRunController.Instance.Escape();
                return;
            }
            if (!IsUnlocked)
            {
                if (SchoolRunController.Instance != null && runRequiredLevel > 0) { }
                else if (mission == null || !mission.Unlock()) return;
                IsUnlocked = true;
                // Unlocking in front of the caretaker starts a chase. Once the round has begun he chases on sight anyway.
                SchoolRunController.Instance?.caretaker?.GetComponent<CaretakerPassCheck>()?.NoteUnlock();
                doorSounds.PlayUnlock();
                requestedOpen = true;
                HudController.Instance?.SetStatus(runRequiredLevel > 0 ? "Unlocked. This door stays available." : "Unlocked. Find your phone before he comes back.", 3f);
            }
            else requestedOpen = !requestedOpen;
        }

        void Update()
        {
            bool staffPassing = caretaker != null && Vector3.Distance(caretaker.transform.position, transform.position) < 2.2f;
            var run = SchoolRunController.Instance;
            if (run != null && run.secondStaff != null && run.secondStaff.enabled && Vector3.Distance(run.secondStaff.transform.position, transform.position) < 2.2f) staffPassing = true;
            if (run != null && (closedForRun || runRequiredLevel > 0 && !IsUnlocked)) staffPassing = false;
            if (run != null && closedForRun) requestedOpen = false;
            if (LessonLocked) { staffPassing = false; requestedOpen = false; }
            // Nobody leaves by the main entrance until the escape itself swings it open.
            if (ExitGated && !escaped) { staffPassing = false; requestedOpen = false; }
            bool shouldOpen = !DetentionLocked && (requestedOpen || staffPassing);
            // Never close a moving leaf onto a player in the doorway.
            if (!DetentionLocked && !shouldOpen && openAmount > .1f && Camera.main != null)
            {
                Vector3 p = Camera.main.transform.position - transform.position;
                p.y = 0f;
                if (p.sqrMagnitude < 1.1f * 1.1f) shouldOpen = true;
            }
            float previousAmount=openAmount;
            openAmount = Mathf.MoveTowards(openAmount, shouldOpen ? 1f : 0f, Time.deltaTime * 3f);
            doorSounds.Movement(previousAmount,openAmount);
            hinge.localRotation = closedRotation * Quaternion.Euler(0, openAngle * Mathf.SmoothStep(0, 1, openAmount), 0);
            if (secondHinge != null)
                secondHinge.localRotation = secondClosedRotation * Quaternion.Euler(0, secondOpenAngle * Mathf.SmoothStep(0, 1, openAmount), 0);
        }
    }
}


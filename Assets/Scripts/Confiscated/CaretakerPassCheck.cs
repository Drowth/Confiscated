using UnityEngine;
using UnityEngine.AI;
namespace Confiscated
{
    /// <summary>A visible warning and time to show a carried pass precede pursuit.</summary>
    public sealed class CaretakerPassCheck : Interactable
    {
        public SchoolPeriodController period;
        public float responseSeconds=14;
        public bool Checking {get;private set;}
        public bool Approaching {get;private set;}
        public float inspectionDistance=2f;
        float lastApproachSight;
        public bool HasPassedCheck {get;private set;}
        float deadline,clearedUntil,footstepAt;
        bool passShown;
        public bool WitnessedOffence {get;private set;}
        CaretakerAI ai;
        AudioSource steps;
        void Awake()
        {
            ai=GetComponent<CaretakerAI>();steps=gameObject.AddComponent<AudioSource>();
            steps.spatialBlend=1;steps.minDistance=1;steps.maxDistance=13;steps.playOnAwake=false;steps.volume=.1f;steps.pitch=1.65f;
        }
        void Update()
        {
            if(ComicDialogue.IsActive)return;
            var agent=GetComponent<NavMeshAgent>();
            if(GetComponent<CaretakerGait>()==null&&(SchoolRunController.Instance==null||!SchoolRunController.Instance.RoundStarted)&&agent.velocity.magnitude>.2f&&Time.time>=footstepAt){footstepAt=Time.time+.52f;steps.PlayOneShot(TempAudio.Thud);}
            if((Checking||Approaching) && (period==null||!period.IsRoaming||GameManager.Instance.Current!=GameManager.State.Playing))CancelCheck();
            if(GameManager.Instance!=null&&GameManager.Instance.Current==GameManager.State.Detention)
            {refused=false;passShown=false;HasPassedCheck=false;WitnessedOffence=false;clearedUntil=Time.time+8;return;}
        }
        public bool FilterSight(bool sees)
        {
            if(ComicDialogue.IsActive)return true;
            if(period==null)return false;
            var run = SchoolRunController.Instance;
            if (run != null && run.RoundStarted) { CancelCheck(); return false; }
            if(!period.IsRoaming){CancelCheck();return true;}
            // Once witnessed, stepping back over the threshold or showing a pass cannot erase an offence.
            if(WitnessedOffence)return false;
            if(SchoolPeriodController.InClass(period.Player.transform.position)&&!period.Player.HasPhone){CancelCheck();return true;}
            if(ai.Current==CaretakerAI.State.Chase)return false;
            bool unlocking=Time.time<unlockSeenUntil;
            bool offence=unlocking||period.Player.HasPhone||period.OfficeBounds.Contains(period.Player.transform.position)||(run != null && run.Restricted(period.Player.transform.position));
            if(offence&&sees)
            {
                CancelCheck();
                WitnessedOffence=true;
                HudController.Instance?.SetStatus(period.Player.HasPhone?"Caretaker: That phone was confiscated, Smith. Detention!":unlocking?"Caretaker: Get away from that door, Smith! That's my key!":"Caretaker: This area is out of bounds, Smith. Come with me.",3);
                return false;
            }
            if(passShown)return true;
            if(Time.time<clearedUntil)return true;
            if(refused)return false;
            float distance=Vector3.Distance(transform.position,period.Player.transform.position);
            if(!Checking&&!Approaching&&sees)
            {
                Approaching=true;lastApproachSight=Time.time;
                HudController.Instance?.SetStatus("The caretaker has spotted you. He's coming to check your pass.",3);
            }
            if(Approaching)
            {
                if(sees){lastApproachSight=Time.time;ai.ApproachForPass(period.Player.transform.position);}
                else if(Time.time-lastApproachSight>ai.PassApproachMemorySeconds){CancelCheck();return true;}
            }
            if(Approaching&&sees&&distance<=inspectionDistance)
            {
                Approaching=false;Checking=true;deadline=Time.time+responseSeconds;ai.PauseForPass(true);
                HudController.Instance?.SetStatus("Caretaker: Smith, why aren't you in class? Show me your hall pass.",responseSeconds);
            }
            if(Checking&&Time.time>=deadline)
            {
                CancelCheck();clearedUntil=Time.time-1;
                // A refused/ignored check must enter normal suspicion instead of starting a fresh warning.
                refused=true;return false;
            }
            if(refused)return false;
            return true;
        }
        bool refused;
        float unlockSeenUntil;
        /// <summary>A door was just unlocked with a key. If he sees the player in the next moments it is an offence no hall pass excuses.</summary>
        public void NoteUnlock(){unlockSeenUntil=Time.time+2f;}
        bool HasPass(PlayerInteractor p)=>p.GetComponent<PlayerInventory>().HasCarried(InventoryItemKind.HallPass);
        public override string GetPrompt(PlayerInteractor p)=>Checking?HasPass(p)?"F: show Mr Reed's hall pass":"You need Mr Reed's hall pass.":"Caretaker";
        public override bool CanInteract(PlayerInteractor p)=>Checking&&Vector3.Distance(transform.position,p.transform.position)<=inspectionDistance;
        public override void Interact(PlayerInteractor p)
        {
            if(!CanInteract(p)||!HasPass(p))return;
            if(p.HasPhone||period.OfficeBounds.Contains(p.transform.position)||(SchoolRunController.Instance != null && SchoolRunController.Instance.Restricted(p.transform.position))){CancelCheck();return;}
            HasPassedCheck=true;refused=false;passShown=true;clearedUntil=float.PositiveInfinity;CancelCheck();
            HudController.Instance?.SetStatus("Caretaker: Mr Reed's delivery? Straight to the tray and back. No other rooms, Smith.",5);
        }
        public void CancelCheck(){if(Checking||Approaching){Checking=false;Approaching=false;ai.EndPassApproach();}}
        public void ResetPermission() { CancelCheck(); refused=false; passShown=false; HasPassedCheck=false; WitnessedOffence=false; clearedUntil=Time.time+8; }
    }
}


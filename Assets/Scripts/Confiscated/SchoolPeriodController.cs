using System.Collections;
using UnityEngine;
using UnityEngine.AI;
namespace Confiscated
{
    public sealed class SchoolPeriodController : MonoBehaviour
    {
        public enum Phase { Worksheet, PhoneRinging, Confiscation, Volunteer, Delivery, Return, Review, Complete }
        public Phase Current {get;private set;}
        public PlayerInteractor Player;
        public ClassroomSeat seat;
        public NavMeshAgent teacher;
        public CaretakerAI caretaker;
        public Transform teacherHome,teacherAtDesk,teacherHandoff,caretakerHandoff,officeDrop,standPoint;
        public GameObject phoneProp;
        public PhonePickup phonePickup;
        public InventoryItemDefinition passItem,papersItem;
        public PeriodWorksheetUI worksheetUI;
        public PeriodInteractable deliveryTray;
        public float reminderAfter=150,lateAfter=240;
        public float AbsentSeconds {get;private set;}
        public int TeacherConcern {get;private set;}
        public bool PapersDelivered {get;private set;}
        public bool PhoneDeposited {get;private set;}
        public bool IsComplete=>Current==Phase.Complete;
        public bool IsRoaming=>Current==Phase.Delivery||Current==Phase.Return;
        public Bounds OfficeBounds=>GameManager.Instance.officeMission.officeBounds;
        public bool QuickOpening=>SchoolRunController.Instance!=null;
        public bool CanUseSeat=>!QuickOpening&&(Current==Phase.Worksheet||Current==Phase.Volunteer||Current==Phase.Return||Current==Phase.Review);
        // The quick opening never returns to the desk, so the seat must not advertise an action it ignores.
        public string SeatPrompt=>QuickOpening?null:Current==Phase.Return?"F: report the delivery and sit down":Current==Phase.Delivery?"Deliver the newsletters to the office tray first.":"F: look at your worksheet";
        FirstPersonController movement;
        PhoneRinger ringer;
        PlayerInventory inventory;
        bool carryingPhone;
        float standingHeight;
        public void Begin()
        {
            movement=Player.GetComponent<FirstPersonController>();inventory=Player.GetComponent<PlayerInventory>();ringer=Player.GetComponent<PhoneRinger>();
            standingHeight=Player.ViewCamera.transform.localPosition.y;
            // Keep spoken lines below the scene, clear of the objective at the top left.
            var status=HudController.Instance?.statusText;
            if(status!=null){var rect=status.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,15);rect.sizeDelta=new Vector2(1400,105);status.alignment=UnityEngine.TextAnchor.MiddleCenter;}
            phonePickup.enabled=false;phonePickup.phoneVisual.SetActive(false);phoneProp.SetActive(true);
            caretaker.Freeze();Current=QuickOpening?Phase.PhoneRinging:Phase.Worksheet;Sit();
            FaceBlackboard();
            if(QuickOpening)StartCoroutine(PhoneIncident());
            else HudController.Instance?.SetStatus("Mr Reed: Morning, Year 6. Look at the picture on your worksheet. What can you see?",10);
            Objective();
        }
        void Sit()
        {
            movement.Controller.enabled=false;movement.MovementLocked=true;
            Player.transform.SetPositionAndRotation(new Vector3(seat.seatedView.position.x,0,seat.seatedView.position.z),Quaternion.Euler(0,270,0));
            Player.ViewCamera.transform.localPosition=new Vector3(0,1.18f,0);movement.ResetLook();
        }
        public void Stand()
        {
            movement.Controller.enabled=false;Player.transform.position=standPoint.position;
            Player.ViewCamera.transform.localPosition=new Vector3(0,standingHeight,0);movement.MovementLocked=false;movement.LookLocked=false;
            movement.Controller.enabled=true;Player.InputLocked=false;Player.SuppressActionsThisFrame();
        }
        public string Prompt(PeriodInteractable.Role role)
        {
            if(role==PeriodInteractable.Role.Delivery)return PapersDelivered?"Newsletters delivered.":IsRoaming&&inventory.HasCarried(InventoryItemKind.Newsletters)?"F: Deliver newsletters":"NEWSLETTERS - office delivery tray";
            if(QuickOpening)return null;
            if(role==PeriodInteractable.Role.Teacher)return Current==Phase.Volunteer?"F: volunteer to deliver the newsletters":Current==Phase.Return?"F: report back to Mr Reed":"Mr Reed";
            return Current==Phase.PhoneRinging||Current==Phase.Confiscation?null:Current==Phase.Volunteer?"F: raise your hand and volunteer":"F: read the worksheet";
        }
        public bool CanInteract(PeriodInteractable.Role role)=>role==PeriodInteractable.Role.Delivery?Current==Phase.Delivery&&inventory.HasCarried(InventoryItemKind.Newsletters):!QuickOpening&&(role==PeriodInteractable.Role.Teacher?Current==Phase.Volunteer||Current==Phase.Return:Current==Phase.Worksheet||Current==Phase.Volunteer||Current==Phase.Review);
        public void Interact(PeriodInteractable.Role role)
        {
            if(!CanInteract(role))return;
            if(role==PeriodInteractable.Role.Delivery){Deliver();return;}
            if(role==PeriodInteractable.Role.Teacher&&Current==Phase.Return){ReturnToClass();return;}
            worksheetUI.Open();
        }
        public void UseSeat(){if(QuickOpening)return;if(Current==Phase.Return)ReturnToClass();else worksheetUI.Open();}
        public void ChooseSentence(int index)
        {
            if(Current==Phase.Worksheet)
            {
                if(index!=2){worksheetUI.Feedback("Mr Reed: No, Smith. Try again.");return;}
                worksheetUI.Close();Current=Phase.PhoneRinging;Objective();StartCoroutine(PhoneIncident());
            }
            else if(Current==Phase.Review)
            {
                if(index!=0){worksheetUI.Feedback("Mr Reed: That isn't right, Smith.");return;}
                worksheetUI.Close();Current=Phase.Complete;ringer.Deactivate();
                string phone=GameManager.Instance.PhoneStored?"Your phone is in your locker.":Player.HasPhone?"You recovered your phone.":"Your phone is still in the caretaker's office.";
                if (SchoolRunController.Instance != null)
                {
                    Stand(); Object.FindFirstObjectByType<SchoolBellSystem>()?.Ring();
                    SchoolRunController.Instance.BeginRound();
                }
                else GameManager.Instance.CompleteSchoolPeriod((TeacherConcern==0?"Newsletters delivered on time.":"Newsletters delivered. Late back to class.")+"\nWorksheet finished.\n"+phone);
            }
        }
        void FaceBlackboard()
        {
            var board=GameObject.Find("School/Details/Year 6 furnishings/TeachingBoard");
            Vector3 direction=board!=null?board.transform.position-teacher.transform.position:Vector3.left;
            direction.y=0;
            if(teacher.isOnNavMesh)teacher.isStopped=true;
            if(direction.sqrMagnitude>.0001f)teacher.transform.rotation=Quaternion.LookRotation(direction);
        }

        IEnumerator PhoneIncident()
        {
            FaceBlackboard();
            yield return new WaitForSeconds(1.1f);
            ringer.PlayScriptedAt(phoneProp.transform);
            // Give the ringtone a clear lead before Reed reacts.
            yield return new WaitForSeconds(2f);
            Quaternion from=teacher.transform.rotation;
            Vector3 direction=Player.transform.position-teacher.transform.position;direction.y=0;
            Quaternion towardPlayer=direction.sqrMagnitude>.0001f?Quaternion.LookRotation(direction):from;
            bool wasUpdatingRotation=teacher.updateRotation;teacher.updateRotation=false;
            const float turnSeconds=.65f;
            for(float elapsed=0;elapsed<turnSeconds;elapsed+=Time.deltaTime)
            {
                teacher.transform.rotation=Quaternion.Slerp(from,towardPlayer,Mathf.SmoothStep(0,1,elapsed/turnSeconds));
                yield return null;
            }
            teacher.transform.rotation=towardPlayer;teacher.updateRotation=wasUpdatingRotation;
            HudController.Instance?.SetStatus("Mr Reed: Master Smith. That had better not be a phone.",8);
            yield return new WaitUntil(() => !ComicDialogue.IsActive);
            yield return Travel(teacher,teacherAtDesk.position);
            ringer.Deactivate();Current=Phase.Confiscation;Objective();
            yield return Confiscate();
        }
        IEnumerator Confiscate()
        {
            // Lift the existing desk prop into Mr Reed's carrying position without a handover prompt.
            var phone=phoneProp.transform;Vector3 deskPosition=phone.position;Quaternion deskRotation=phone.rotation;
            const float pickupSeconds=.65f;
            for(float elapsed=0;elapsed<pickupSeconds;elapsed+=Time.deltaTime)
            {
                float t=Mathf.SmoothStep(0,1,elapsed/pickupSeconds);
                phone.position=Vector3.Lerp(deskPosition,teacher.transform.TransformPoint(new Vector3(.25f,1.05f,-.15f)),t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.12f);
                phone.rotation=Quaternion.Slerp(deskRotation,teacher.transform.rotation*Quaternion.Euler(80,0,0),t);
                yield return null;
            }
            phone.SetParent(teacher.transform,false);phone.localPosition=new Vector3(.25f,1.05f,-.15f);phone.localRotation=Quaternion.Euler(80,0,0);
            HudController.Instance?.SetStatus("Mr Reed: No phones in my lesson. This is going to the caretaker's office.",6);
            var agent=caretaker.GetComponent<NavMeshAgent>();caretaker.GetComponentInChildren<CutoutMotion>()?.SetFrozen(false);
            var approach=StartCoroutine(Travel(agent,caretakerHandoff.position));
            yield return Travel(teacher,teacherHandoff.position);yield return approach;
            phoneProp.transform.SetParent(caretaker.transform,false);phoneProp.transform.localPosition=new Vector3(-.3f,1.08f,0);phoneProp.transform.localRotation=Quaternion.Euler(80,0,0);
            carryingPhone=true;caretaker.StartSchoolRoutine();
            Current=Phase.Volunteer;
            if(QuickOpening)
            {
                // The handoff is the end of the introduction: release the player while Reed returns.
                Volunteer();
                StartCoroutine(Travel(teacher,teacherHome.position));
            }
            else
            {
                HudController.Instance?.SetStatus("Caretaker: It'll be in my office.\nMr Reed: These newsletters need delivering. Any volunteers?",9);
                yield return Travel(teacher,teacherHome.position);
                Objective();worksheetUI.Open();
            }
        }
        public void Volunteer()
        {
            if(Current!=Phase.Volunteer)return;
            int free=0;for(int i=0;i<inventory.Capacity(InventoryContainer.Satchel);i++)if(inventory.Get(InventoryContainer.Satchel,i)==null)free++;
            if(free<2){worksheetUI.Feedback("Make two spaces in your satchel for the pass and newsletters.");return;}
            inventory.Collect(passItem);inventory.Collect(papersItem);worksheetUI.Close();Stand();
            Current=Phase.Delivery;AbsentSeconds=0;GameManager.Instance.officeMission.Begin();Objective();
            HudController.Instance?.SetStatus("Mr Reed: Smith, since you seem so preoccupied, make yourself useful. Take these newsletters, put them in the tray outside the caretaker's office and be quick!",12);
        }
        public void Deliver()
        {
            if(Current!=Phase.Delivery||!inventory.HasCarried(InventoryItemKind.Newsletters)||Vector3.Distance(Player.transform.position,deliveryTray.transform.position)>3)return;
            inventory.RemoveCarried(InventoryItemKind.Newsletters);PapersDelivered=true;Current=Phase.Return;Objective();
            TempAudio.PlayAt(TempAudio.Pickup,Player.ViewCamera.transform.position,.65f);
            HudController.Instance?.SetStatus(QuickOpening ? "Newsletters delivered! Now find the office key on the dining-hall trolley." : "Newsletters delivered! Return to Mr Reed in Year 6.",5);
            var stack=deliveryTray.transform.Find("Delivered papers");if(stack!=null)stack.gameObject.SetActive(true);
        }
        public void PrepareChaseRetry()
        {
            StopAllCoroutines();
            Stand();
            StartEscapeRun();
            phoneProp.SetActive(false);
            carryingPhone=false;
            PhoneDeposited=true;
            phonePickup.enabled=true;
            phonePickup.ReturnToOffice();
        }
        public void StartEscapeRun(){worksheetUI.Close();Current=Phase.Complete;ringer.Deactivate();inventory.RemoveCarried(InventoryItemKind.Newsletters);inventory.RemoveCarried(InventoryItemKind.HallPass);}
        public void ReturnToClass()
        {
            if(QuickOpening)return;
            if(Current!=Phase.Return||!PapersDelivered||!InClass(Player.transform.position))return;
            GameManager.Instance.lockerUI?.Close();caretaker.GetComponent<CaretakerPassCheck>()?.CancelCheck();
            ringer.Deactivate();Current=Phase.Review;Sit();
            HudController.Instance?.SetStatus(TeacherConcern==0?"Mr Reed: Thank you, Smith. Finish your caption, please.":"Mr Reed: That took rather longer than it should. Finish your caption.",10);
            Objective();worksheetUI.Open();
        }
        public static bool InClass(Vector3 p)=>p.x>-32.11f&&p.x<-14.89f&&p.z>19.11f&&p.z<32.45f;
        void Update()
        {
            if(Player==null||movement==null)return;
            if(carryingPhone&&Vector3.Distance(caretaker.transform.position,officeDrop.position)<1.3f)
            {
                carryingPhone=false;PhoneDeposited=true;phoneProp.SetActive(false);phonePickup.enabled=true;phonePickup.ReturnToOffice();
            }
            if(IsRoaming&&!QuickOpening&&GameManager.Instance.IsPlaying)
            {
                AbsentSeconds+=Time.deltaTime;
                int concern=AbsentSeconds>=lateAfter?2:AbsentSeconds>=reminderAfter?1:0;
                if(concern>TeacherConcern){TeacherConcern=concern;HudController.Instance?.SetStatus(concern==1?"You've been gone a while. Mr Reed said straight there and back.":"You're late. Mr Reed will want an explanation.",7);}
            }
        }
        void LateUpdate(){if(movement!=null&&GameManager.Instance.IsPlaying&&!IsComplete)Objective();}
        void Objective()
        {
            if(QuickOpening)
            {
                string next=Current==Phase.PhoneRinging?"Your phone is ringing...":Current==Phase.Confiscation?"Your phone is being taken to the caretaker's office.":
                    !PapersDelivered?"Deliver the newsletters to the office tray.":
                    GameManager.Instance.officeMission.OfficeUnlocked?"Recover your phone from the CONFISCATED box.":
                    inventory.HasCarried(InventoryItemKind.OfficeKey)?"Unlock the caretaker's office door.":"Find the office key on the DINING HALL trolley.";
                HudController.Instance?.SetObjective((PapersDelivered?"Belongings recovered: 0/5\n":"")+next);
                return;
            }
            string title=Current switch
            {
                Phase.Worksheet=>"FIRST PERIOD - ENGLISH\nMr Reed is at the board. Look down at your worksheet and press F.",
                Phase.PhoneRinging=>"YOUR PHONE IS RINGING\nMr Reed is coming over.",
                Phase.Confiscation=>"CONFISCATED\nThe caretaker is collecting your phone for his office.",
                Phase.Volunteer=>"A REASON TO LEAVE CLASS\nPress E on your worksheet to raise your hand and volunteer.",
                Phase.Delivery=>"Deliver the newsletters to the office tray.",
                Phase.Return=>"Return to Mr Reed in Year 6.",
                Phase.Review=>"BACK IN CLASS\nFinish the newsletter caption on your worksheet.",
                _=>"FIRST PERIOD COMPLETE"
            };
            HudController.Instance?.SetObjective(title);
        }
        IEnumerator Travel(NavMeshAgent actor,Vector3 target)
        {
            if(!actor.isOnNavMesh){Debug.LogError("School actor is off the navigation mesh: "+actor.name);yield break;}
            var motion=actor.GetComponentInChildren<CutoutMotion>();motion?.SetFrozen(false);
            actor.isStopped=false;actor.speed=2.3f;actor.SetDestination(target);
            yield return null;
            float deadline=Time.time+40;
            while(Time.time<deadline&&(actor.pathPending||Vector3.Distance(actor.transform.position,target)>.45f))yield return null;
            actor.isStopped=true;
            motion?.SetFrozen(true);
            if(Vector3.Distance(actor.transform.position,target)>1)Debug.LogError("School actor could not reach "+target+": "+actor.name);
        }
        void OnDisable(){if(worksheetUI!=null)worksheetUI.Close();if(ringer!=null)ringer.Deactivate();}
    }
}

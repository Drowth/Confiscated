using System.Collections;
using UnityEngine;
using UnityEngine.AI;
namespace Confiscated
{
    public sealed class SchoolPeriodController : MonoBehaviour
    {
        public enum Phase { PhoneRinging, Confiscation, Volunteer, Delivery, Return, Complete }
        public Phase Current {get;private set;}
        public PlayerInteractor Player;
        public ClassroomSeat seat;
        public NavMeshAgent teacher;
        public CaretakerAI caretaker;
        public Transform teacherHome,teacherAtDesk,teacherHandoff,caretakerHandoff,officeDrop,standPoint;
        public GameObject phoneProp;
        public PhonePickup phonePickup;
        public InventoryItemDefinition passItem,papersItem;
        public PeriodInteractable deliveryTray;
        public bool PapersDelivered {get;private set;}
        public bool PhoneDeposited {get;private set;}
        public bool IsComplete=>Current==Phase.Complete;
        public bool IsRoaming=>Current==Phase.Delivery||Current==Phase.Return;
        public Bounds OfficeBounds=>GameManager.Instance.officeMission.officeBounds;
        FirstPersonController movement;
        PhoneRinger ringer;
        PlayerInventory inventory;
        bool carryingPhone;
        float standingHeight;
        // On load, not in Begin: the title intro's camera flies in through the window to this desk before the day starts.
        void Start(){PhoneModelVisuals.InstallDesk(phoneProp);PhoneModelVisuals.InstallPickup(phonePickup.phoneVisual);}
        public void Begin()
        {
            movement=Player.GetComponent<FirstPersonController>();inventory=Player.GetComponent<PlayerInventory>();ringer=Player.GetComponent<PhoneRinger>();
            standingHeight=Player.ViewCamera.transform.localPosition.y;
            // Keep spoken lines below the scene, clear of the objective at the top left.
            var status=HudController.Instance?.statusText;
            if(status!=null){var rect=status.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,15);rect.sizeDelta=new Vector2(1400,105);status.alignment=UnityEngine.TextAnchor.MiddleCenter;}
            phonePickup.enabled=false;phonePickup.phoneVisual.SetActive(false);phoneProp.SetActive(true);
            caretaker.Freeze();Current=Phase.PhoneRinging;Sit();
            FaceBlackboard();
            StartCoroutine(PhoneIncident());
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
        // Only the office delivery tray is interactive; the teacher and desk worksheet props are scenery.
        public string Prompt(PeriodInteractable.Role role)=>role!=PeriodInteractable.Role.Delivery?null:
            PapersDelivered?"Newsletters delivered.":IsRoaming&&inventory.HasCarried(InventoryItemKind.Newsletters)?"F: Deliver newsletters":"NEWSLETTERS - office delivery tray";
        public bool CanInteract(PeriodInteractable.Role role)=>role==PeriodInteractable.Role.Delivery&&Current==Phase.Delivery&&inventory.HasCarried(InventoryItemKind.Newsletters);
        public void Interact(PeriodInteractable.Role role){if(CanInteract(role))Deliver();}
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
            foreach(var pupil in Object.FindObjectsByType<SeatedStudent>(FindObjectsSortMode.None))
                if(InClass(pupil.transform.position))pupil.ReactToPhone(Player.transform);
            // Give the text tone a clear lead before Reed reacts.
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
            HudController.Instance?.SetStatus("Mr Reed: Master Smith. Is that… a phone?",8);
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
            HudController.Instance?.SetStatus("Mr Reed: You know the rules, and this isn't the first time. It will be with the caretaker until the end of term.",6);
            var agent=caretaker.GetComponent<NavMeshAgent>();caretaker.GetComponentInChildren<CutoutMotion>()?.SetFrozen(false);
            var approach=StartCoroutine(Travel(agent,caretakerHandoff.position));
            yield return Travel(teacher,teacherHandoff.position);yield return approach;
            phoneProp.transform.SetParent(caretaker.transform,false);phoneProp.transform.localPosition=new Vector3(-.3f,1.08f,0);phoneProp.transform.localRotation=Quaternion.Euler(80,0,0);
            carryingPhone=true;caretaker.StartSchoolRoutine();
            Current=Phase.Volunteer;
            HudController.Instance?.SetStatus("Smith: This is the last straw.",5);
            yield return new WaitUntil(() => !ComicDialogue.IsActive);
            bool memoryFinished=false;
            if(OpeningDiaryComic.Show(movement,Player,()=>memoryFinished=true))
                yield return new WaitUntil(() => memoryFinished);
            // The memory ends at the classroom handoff; release Smith for the errand.
            Volunteer();
            StartCoroutine(Travel(teacher,teacherHome.position));
        }
        public void Volunteer()
        {
            if(Current!=Phase.Volunteer)return;
            int free=0;for(int i=0;i<inventory.Capacity(InventoryContainer.Satchel);i++)if(inventory.Get(InventoryContainer.Satchel,i)==null)free++;
            if(free<2){HudController.Instance?.SetStatus("Make two spaces in your satchel for the pass and newsletters.",4);return;}
            inventory.Collect(passItem);inventory.Collect(papersItem);Stand();
            Current=Phase.Delivery;GameManager.Instance.officeMission.Begin();Objective();
            HudController.Instance?.SetStatus("Mr Reed: Well, since you're clearly not busy… newsletters. Tray outside the caretaker's office. And be quick about it.",12);
        }
        public void Deliver()
        {
            if(Current!=Phase.Delivery||!inventory.HasCarried(InventoryItemKind.Newsletters)||Vector3.Distance(Player.transform.position,deliveryTray.transform.position)>3)return;
            inventory.RemoveCarried(InventoryItemKind.Newsletters);PapersDelivered=true;Current=Phase.Return;Objective();
            TempAudio.PlayAt(TempAudio.Pickup,Player.ViewCamera.transform.position,.65f);
            HudController.Instance?.SetStatus("Newsletters delivered! The office key is on the caretaker's trolley - follow it, unseen.",5);
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
        public void StartEscapeRun(){Current=Phase.Complete;ringer.Deactivate();inventory.RemoveCarried(InventoryItemKind.Newsletters);inventory.RemoveCarried(InventoryItemKind.HallPass);}
        public static bool InClass(Vector3 p)=>p.x>-32.11f&&p.x<-14.89f&&p.z>19.11f&&p.z<32.45f;
        /// <summary>The classroom plus a metre or so outside each of its two doors (Year 6 north, Year 6 west): during the
        /// errand, a player standing in the doorway is still "in class" (young players hovered there and were caught).</summary>
        public static bool InClassOrDoorway(Vector3 p)=>InClass(p)||
            (p.x>-18.8f&&p.x<-16.8f&&p.z>=32.45f&&p.z<33.7f)||   // Year 6 north door
            (p.z>24.9f&&p.z<26.9f&&p.x<=-32.11f&&p.x>-33.4f);     // Year 6 west door
        void Update()
        {
            if(Player==null||movement==null)return;
            if(carryingPhone&&Vector3.Distance(caretaker.transform.position,officeDrop.position)<1.3f)
            {
                carryingPhone=false;PhoneDeposited=true;phoneProp.SetActive(false);phonePickup.enabled=true;phonePickup.ReturnToOffice();
            }
        }
        void LateUpdate(){if(movement!=null&&GameManager.Instance.IsPlaying&&!IsComplete)Objective();}
        void Objective()
        {
            string next=Current==Phase.PhoneRinging?"Your phone just buzzed...":Current==Phase.Confiscation?"Your phone is being taken to the caretaker's office.":
                !PapersDelivered?"Deliver the newsletters to the office tray.":
                GameManager.Instance.officeMission.OfficeUnlocked?"Recover your phone from the CONFISCATED box.":
                inventory.HasCarried(InventoryItemKind.OfficeKey)?"Unlock the caretaker's office door.":
                SchoolRunController.Instance!=null&&!SchoolRunController.Instance.TrolleyParked?"Follow the caretaker's trolley to the DINING HALL. Stay out of sight.":
                SchoolRunController.Instance!=null&&SchoolRunController.Instance.KeyWindowOpen?"His back is turned - take the office key from his trolley, quick!":
                "Take the office key from his trolley in the DINING HALL.";
            HudController.Instance?.SetObjective((PapersDelivered?"Belongings recovered: 0/5\n":"")+next);
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
        void OnDisable(){if(ringer!=null)ringer.Deactivate();}
    }
}

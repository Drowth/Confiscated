using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    [InitializeOnLoad]
    public static class SchoolRunSmokeTest
    {
        const string Marker="Temp/run_school_chase", Report="../Docs/SchoolRun/Validation.txt";
        static int stage,errors,lastFrame,decoyNoises;
        static double started,at;
        static bool background,chaseNoiseChecked;
        static float introStarted;
        static ChatterboxStudent Chatter=>Object.FindFirstObjectByType<ChatterboxStudent>();
        static SchoolRunController R=>Object.FindFirstObjectByType<SchoolRunController>();
        static SchoolPeriodController S=>R.period;
        static PlayerInteractor P=>S.Player;
        static FirstPersonController F=>P.GetComponent<FirstPersonController>();
        static PlayerInventory I=>P.GetComponent<PlayerInventory>();
        static CaretakerPassCheck C=>R.caretaker.GetComponent<CaretakerPassCheck>();
        static LessonCorridorDoors[] Gates=>Object.FindObjectsByType<LessonCorridorDoors>(FindObjectsSortMode.None);
        // Year 6 stand point and the south cross hall just outside its door: both inside the west wing.
        static readonly Vector3 Classroom=new Vector3(-19.6f,0,29),Corridor=new Vector3(-20,0,34.2f);
        static float Gap=>Vector3.Distance(R.caretaker.transform.position,P.transform.position);
        static SchoolRunSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Begin();}};}
        [MenuItem("Confiscated/School Run/Arm Integration Test")]
        public static void Arm(){Directory.CreateDirectory("../Docs/SchoolRun");File.WriteAllText(Marker,"armed");}
        static void Begin()
        {
            SteamLeaderboard.Suppress=true; // a warped audit run must never post a time
            stage=errors=decoyNoises=0;chaseNoiseChecked=false;lastFrame=-1;started=at=EditorApplication.timeSinceStartup;
            background=Application.runInBackground;Application.runInBackground=true;
            File.WriteAllText(Report,"School run integration: real scene, natural NPC navigation, player warped between checkpoints; direct interaction calls after ray checks.\nThe caretaker is placed down the corridor for the pass check and the capture; he then sees, walks and catches under his own AI.\n");
            Application.logMessageReceived+=Log;NoiseEvents.OnNoise+=Noise;EditorApplication.update+=Tick;
        }
        static void Noise(Vector3 p,float radius,string source){if(source=="clockwork toy")decoyNoises++;}
        static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+message+"\n");}}
        static void Need(bool value,string label){File.AppendAllText(Report,(value?"ok   ":"FAIL ")+label+"\n");if(!value)throw new Exception(label);}
        static void Next(int value){stage=value;at=EditorApplication.timeSinceStartup;}
        static void Dismiss(){for(int i=0;i<30&&ComicDialogue.IsActive;i++)ComicDialogue.Instance.Advance();}
        static void Warp(Vector3 p){F.Controller.enabled=false;P.transform.position=p;F.Controller.enabled=true;Physics.SyncTransforms();}
        // Lessons shut everything outside the west wing, so errand checkpoints must be walkable from Year 6.
        static void WingWarp(Vector3 p,string label){Path(Classroom,p,"errand checkpoint inside the west wing: "+label);Warp(p);}
        static void PlaceCaretaker(Vector3 p,Vector3 facing)
        {
            var agent=R.caretaker.GetComponent<NavMeshAgent>();
            Need(NavMesh.SamplePosition(p,out var hit,2,NavMesh.AllAreas)&&agent.Warp(hit.position),"caretaker placed on navigation "+Vector3.Distance(hit.position,facing).ToString("F1")+" m from the player");
            Vector3 look=facing-hit.position;look.y=0;R.caretaker.transform.rotation=Quaternion.LookRotation(look);
        }
        // A reload after a win or a capture skips the lesson and starts the chase from the classroom.
        static bool ChaseRetryReady=>GameManager.Instance!=null&&GameManager.Instance.IsPlaying&&R!=null&&R.RoundStarted&&R.Count==0&&Time.timeSinceLevelLoad>1;
        static void Equip(InventoryItemKind kind){Need(I.Move(InventoryContainer.Satchel,I.Find(InventoryContainer.Satchel,kind),InventoryContainer.Use,0,out _),"equip "+kind);}
        static OfficeDoor Door(string name)=>Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None).First(d=>d.name==name);
        static void Passage(Vector3 center,Vector3 forward,bool open,string label)
        {
            center.y=.05f;Warp(center-forward*1.3f);Vector3 start=P.transform.position;
            for(int i=0;i<100;i++)F.Controller.Move(forward*.026f);
            float crossed=Vector3.Dot(P.transform.position-center,forward);
            Need((crossed>F.Controller.radius)==open,label+" player collider "+(open?"passes":"blocked")+" (beyond threshold: "+crossed.ToString("F2")+"m)");
        }
        static AccessToolPickup Tool(AccessToolPickup.Tool kind)=>Object.FindObjectsByType<AccessToolPickup>(FindObjectsSortMode.None).First(t=>t.tool==kind);
        static RunPickup Item(int id)=>R.pickups.First(p=>p.itemId==id);
        static void Reach(Interactable target,Vector3 from,Vector3 aim)
        {
            Path(P.transform.position,from,"route to "+target.name);Warp(from);F.LookLocked=true;P.ViewCamera.transform.LookAt(aim);Physics.SyncTransforms();
            bool hit=Physics.Raycast(P.ViewCamera.transform.position,P.ViewCamera.transform.forward,out var h,2.6f,~0,QueryTriggerInteraction.Collide);
            Need(hit&&h.collider.GetComponentInParent<Interactable>()==target,"interaction ray reaches "+target.name+" (hit "+(hit?h.collider.name:"none")+")");
        }
        // 2026-09-19 playtest: an invisible doorway barrier swallowed the interaction ray, so locked rooms could not be unlocked
        // by pointing at the door, and the only escape trigger was an unmarked plate beside the main entrance.
        static void DoorAimChecks()
        {
            foreach(var door in Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None).Where(d=>d.closedForRun||d.runRequiredLevel>0||d.mainExit))
            {
                var leaf=door.hinge.GetComponentsInChildren<Collider>().First(c=>c.name=="Leaf");
                Vector3 centre=leaf.bounds.center;centre.y=1.2f;Vector3 normal=leaf.transform.forward;normal.y=0;normal.Normalize();
                int sides=0;
                foreach(float sign in new[]{-1f,1f})
                {
                    Vector3 eye=centre+normal*sign*1.1f;
                    // Only sides a player can stand on: some locked rooms back onto a wall or the outdoors.
                    if(!NavMesh.SamplePosition(new Vector3(eye.x,0,eye.z),out _,.5f,NavMesh.AllAreas))continue;
                    sides++;
                    var found=PlayerInteractor.Resolve(new Ray(eye,(centre-eye).normalized),2.6f,~0);
                    Need(found==door,"pointing at the middle of "+door.name+" from "+(sign<0?"behind":"in front")+" finds the door (found "+(found!=null?found.name:"nothing")+")");
                }
                Need(sides>0,door.name+" can be approached on at least one side");
            }
            Need(!Object.FindObjectsByType<RunGate>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(g=>g.kind==RunGate.Kind.Exit),"no separate escape plate: the doors are the exit");
            var exit=Door(PlaytestFixSetup.MainDoorName);
            Need(exit.mainExit&&!exit.CanInteract(P)&&!exit.IsOpen&&exit.GetPrompt(P).Contains("MAIN ENTRANCE"),"main entrance is shut and explains itself before five of five");
            Need(exit.GetComponent<ProgressPropFeedback>()!=null&&exit.GetComponent<ProgressPropFeedback>().padlock!=null,"main entrance carries a padlock that drops at five of five");
            Need(GameObject.Find("SchoolRun/"+PlaytestFixSetup.ExitBlockerName)!=null,"staff cannot route through the shut main entrance");
        }
        static void Path(Vector3 from,Vector3 to,string label)
        {
            Need(NavMesh.SamplePosition(from,out var a,2,NavMesh.AllAreas)&&NavMesh.SamplePosition(to,out _,2,NavMesh.AllAreas),label+" endpoints on navigation");
            NavMesh.SamplePosition(to,out var b,2,NavMesh.AllAreas);var path=new NavMeshPath();
            Need(NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,label+" complete route");
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish(false);return;}
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            double now=EditorApplication.timeSinceStartup;if(now-at<.4)return;
            try
            {
                if(now-started>300)throw new Exception("Timeout at stage "+stage+" phase "+S.Current);
                if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();return;}
                if(ComicDialogue.IsActive&&ComicDialogue.Instance.Actor==null){ComicDialogue.Instance.Advance();return;}
                switch(stage)
                {
                    case 0:
                        Need(S.Current==SchoolPeriodController.Phase.PhoneRinging&&!S.worksheetUI.IsOpen,"short opening starts the phone incident without a worksheet");introStarted=Time.time;
                        Need(!R.KeyAvailable&&!R.ReadyToEscape,"key and escape initially gated");
                        Need(!Item(4).CanInteract(P),"cannot skip to final item");
                        Need(!Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).Any(t=>(t is RunPickup||t is DecoyPickup||t is AccessToolPickup||t is PhonePickup||t is OfficeKeyPickup||t is ThrowableBall)&&SchoolPeriodController.InClass(t.transform.position)),"starting classroom contains no collectible pickups");
                        Need(!Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Any(t=>t.transform.parent.GetComponent<RunPickup>()!=null),"confiscated items have no floating signs");
                        DoorAimChecks();
                        Time.timeScale=4;Next(1);break;
                    case 1:
                        if(!P.GetComponent<PhoneRinger>().Emitter.isPlaying)return;
                        Need(true,"opening ringtone still plays");Next(2);break;
                    case 2:
                        if(!S.IsRoaming)return;
                        Dismiss();Need(I.HasCarried(InventoryItemKind.HallPass)&&I.HasCarried(InventoryItemKind.Newsletters)&&!S.worksheetUI.IsOpen,"handoff releases player with pass and papers automatically");
                        Need(Time.time-introStarted<60,"opening releases player within 60 simulated seconds ("+(Time.time-introStarted).ToString("F1")+"s, reading time excluded)");
                        Need(SchoolRunController.LessonsInProgress&&Gates.All(g=>!g.IsOpen&&g.barrier.activeSelf),"lesson gates hold the errand inside the west wing");
                        // He spots the player from down the corridor with his own eyes, then has to walk over before asking.
                        WingWarp(Corridor,"corridor outside Year 6");PlaceCaretaker(Corridor+Vector3.left*6.5f,Corridor);R.caretaker.ResumeAfterDetention(0);
                        C.FilterSight(true);Need(C.Approaching&&!C.Checking&&Gap>C.inspectionDistance,"caretaker spotting the player from "+Gap.ToString("F1")+" m walks over; no pass question from a distance");Next(20);break;
                    case 20:
                        if(!C.Checking)
                        {
                            if(!C.Approaching)throw new Exception("caretaker abandoned the pass approach at "+Gap.ToString("F1")+" m");
                            if(now-at>10)throw new Exception("caretaker never reached the player for the pass check ("+Gap.ToString("F1")+" m away)");
                            return;
                        }
                        Need(Gap<=C.inspectionDistance+.05f&&C.CanInteract(P),"pass question starts only once he is within "+C.inspectionDistance.ToString("F1")+" m (asked at "+Gap.ToString("F2")+" m)");
                        Dismiss();Need(!I.IsEquipped(InventoryItemKind.HallPass),"pass remains in satchel");C.Interact(P);Dismiss();Need(C.HasPassedCheck&&!C.Checking,"one interaction shows carried hall pass");
                        R.caretaker.ResumeAfterDetention(999);
                        WingWarp(new Vector3(-23,0,34.2f),"further along the corridor");C.FilterSight(true);Need(C.HasPassedCheck&&!C.WitnessedOffence&&!C.Approaching,"walking away with accepted pass is permitted");
                        WingWarp(new Vector3(-30,0,60),"dining hall");Need(!C.FilterSight(true)&&C.WitnessedOffence,"cafeteria trespass overrides accepted pass");
                        C.ResetPermission();Warp(Classroom);R.caretaker.ResumeAfterDetention(999);Next(3);break;
                    case 3:
                        if(!S.PhoneDeposited||!R.TrolleyParked)return;
                        Need(R.KeyAvailable&&Vector3.Distance(R.trolley.position,R.trolleyDock.position)<.1f,"caretaker deposits phone and brings trolley into cafeteria via real navigation");
                        var key=Object.FindFirstObjectByType<OfficeKeyPickup>();
                        Reach(key,key.transform.position+new Vector3(0,-key.transform.position.y,-1.2f),key.transform.position);key.Interact(P);
                        Need(I.HasCarried(InventoryItemKind.OfficeKey),"office key recovered from cafeteria trolley");
                        R.caretaker.Freeze();Need(!I.IsEquipped(InventoryItemKind.Newsletters),"papers remain in satchel");
                        Reach(S.deliveryTray,new Vector3(-33.8f,0,79.3f),S.deliveryTray.transform.position);S.Deliver();Need(S.PapersDelivered,"one interaction delivers carried newsletters");
                        Need(!I.IsEquipped(InventoryItemKind.OfficeKey),"office key remains in satchel");var office=Door("Caretaker office");office.Interact(P);Need(office.IsUnlocked,"carried office key unlocks original office without inventory juggling");Next(4);break;
                    case 4:
                        Passage(Door("Caretaker office").transform.position,Door("Caretaker office").transform.forward,true,"office door");
                        Reach(S.phonePickup,new Vector3(-44.6f,0,80.1f),S.phonePickup.transform.position+Vector3.up*.3f);
                        Need(S.phonePickup.CanInteract(P),"deposited phone is recoverable");S.phonePickup.Interact(P);Need(R.Count==1&&P.HasPhone&&!R.HasBoltCutters&&!R.HasStoreKey,"phone counts as item one and does not magically award tools");
                        Need(R.caretaker.SuspicionSeconds>1f,"early game gives a forgiving beat to duck out of sight when spotted ("+R.caretaker.SuspicionSeconds.ToString("F2")+"s)");
                        P.GetComponent<PhoneRinger>().Activate();Need(!P.GetComponent<PhoneRinger>().Emitter.isPlaying&&P.GetComponent<PhoneRinger>().SecondsToRing<0,"recovered phone stays silent");
                        Need(S.IsComplete,"recovering phone ends lesson errands without returning to starting room");
                        Need(R.RoundStarted&&GameManager.Instance.IsPlaying&&!F.MovementLocked,"phone recovery starts escape run immediately");R.caretaker.Freeze();Time.timeScale=1;Next(30);break;
                    case 30:
                        Need(Chatter!=null,"one chatterbox is installed");Chatter.cooldownSeconds=3;
                        Warp(Chatter.transform.position+Chatter.transform.forward*2);F.LookLocked=false;P.InputLocked=false;Next(31);break;
                    case 31:
                        Need(Chatter.Interruptions==0&&!F.IsDistracted,"passing outside arm's reach is safe");
                        Warp(Chatter.transform.position+Chatter.transform.forward*.75f);Next(32);break;
                    case 32:
                        Need(Chatter.Interruptions==1&&Chatter.Talking&&ComicDialogue.IsActive,"near pupil locks player into a brief automatic conversation");Next(33);break;
                    case 33:
                        if(Chatter.Talking){if(now-at>8)throw new Exception("chatterbox conversation did not finish");return;}
                        Need(!ComicDialogue.IsActive&&!Chatter.MouthOpen&&Chatter.Interruptions==1,"conversation releases player, closes mouth and cannot retrigger while nearby");
                        Warp(Chatter.transform.position+Chatter.transform.forward*1.8f+Vector3.forward*6);Next(34);break;
                    case 34:
                        if(now-at<3.2)return;
                        Need(Chatter.Available,"pupil rearms only after leaving and cooldown (shortened to 3s for test)");
                        NoiseEvents.Emit(Chatter.transform.position+Vector3.forward*2,38,"clockwork toy");
                        Warp(Chatter.transform.position+Chatter.transform.forward*.75f);Next(35);break;
                    case 35:
                        Need(Chatter.Distracted&&Chatter.Interruptions==1&&!F.IsDistracted,"rattling decoy allows safe passage within arm's reach");
                        Chatter.cooldownSeconds=25;Warp(new Vector3(-44.6f,0,80.1f));Next(5);break;
                    case 5:
                        if(!Gates.All(g=>g.IsOpen)){if(now-at>5)throw new Exception("lesson gates did not open after the run started");return;}
                        Need(Gates.All(g=>!g.barrier.activeSelf),"run start opens every lesson gate; the rest of the school is reachable");
                        Passage(Door("North classroom A north").transform.position,Door("North classroom A north").transform.forward,false,"closed classroom");
                        Need(Door("North room B north").CanInteract(P)&&Door("North room B south").CanInteract(P),"resources opens after lesson without an invented resources key");
                        Need(Door("East room B east").CanInteract(P)&&Door("East room B west").CanInteract(P),"equipment can be explored before other recoveries");Need(!Door("Store cupboard").CanInteract(P),"store remains locked without physical store key");
                        var gate=Object.FindObjectsByType<RunGate>(FindObjectsSortMode.None).First(g=>g.kind==RunGate.Kind.Chain);
                        Need(!gate.CanInteract(P),"phone alone cannot open cage");
                        var cutters=Tool(AccessToolPickup.Tool.BoltCutters);Reach(cutters,new Vector3(-45.5f,0,80.1f),cutters.transform.position);cutters.Interact(P);Need(R.HasBoltCutters&&R.Count==1,"physical cutters collected separately from objective items");
                        Reach(gate,new Vector3(-10,0,47),new Vector3(-10,1.1f,45.65f));gate.Interact(P);Need(gate.Cleared,"bolt cutters open physical chain gate");Next(6);break;
                    case 6:
                        Passage(new Vector3(-10,0,45.65f),Vector3.back,true,"opened cage gate");
                        Path(new Vector3(-10,0,47),new Vector3(-10,0,44.8f),"opened dining cage");
                        Reach(Item(1),new Vector3(-10,0,45.4f),Item(1).transform.position+Vector3.up*.1f);Item(1).Interact(P);Need(R.Count==2&&R.UnlockLevel==2,"yo-yo is recovered from opened cage");
                        Door("North room B north").Interact(P);Door("North room B south").Interact(P);Next(7);break;
                    case 7:
                        Passage(Door("North room B north").transform.position,Door("North room B north").transform.forward,true,"resources door");
                        Path(SchoolPlan.Point(659,467),SchoolPlan.Point(659,345),"resources room unlocked");
                        Need(Door("North room B north").IsUnlocked&&Door("North room B south").IsUnlocked,"both resources doors stay unlocked");
                        Reach(Item(2),Item(2).transform.position+new Vector3(0,-Item(2).transform.position.y,-1.3f),Item(2).transform.position+Vector3.up*.15f);Item(2).Interact(P);Need(R.Count==3&&R.secondStaff.enabled,"third recovery activates second staff patrol");R.secondStaff.Freeze();R.caretaker.Freeze();
                        Door("East room B east").Interact(P);Door("East room B west").Interact(P);Next(8);break;
                    case 8:
                        Passage(Door("East room B east").transform.position,Door("East room B east").transform.forward,true,"equipment door");
                        Path(SchoolPlan.Point(702,654),SchoolPlan.Point(892,672),"equipment room shortcut unlocked");
                        Reach(Item(3),Item(3).transform.position+new Vector3(0,-Item(3).transform.position.y,-1.3f),Item(3).transform.position+Vector3.up*.15f);Item(3).Interact(P);Need(R.Count==4&&!R.HasStoreKey,"skateboard counts but grants no magic key");Need(!Door("Store cupboard").CanInteract(P),"four items alone do not open store");
                        var storeKey=Tool(AccessToolPickup.Tool.StoreKey);Reach(storeKey,new Vector3(23.5f,0,60),storeKey.transform.position);storeKey.Interact(P);Need(R.HasStoreKey,"equipment checkout key opens the store");Door("Store cupboard").Interact(P);R.caretaker.Freeze();R.secondStaff.Freeze();Next(9);break;
                    case 9:
                        Passage(Door("Store cupboard").transform.position,Door("Store cupboard").transform.forward,true,"store door");
                        Reach(Item(4),Item(4).transform.position+new Vector3(0,-Item(4).transform.position.y,-1.25f),Item(4).transform.position+Vector3.up*.15f);Item(4).Interact(P);Need(R.Count==5&&R.ReadyToEscape,"all five enable escape");
                        Need(R.caretaker.SuspicionSeconds<0.7f,"late game keeps the tight base reaction time ("+R.caretaker.SuspicionSeconds.ToString("F2")+"s)");
                        // Only a non-caretaker captor gives detention; the caretaker's own capture is exercised after the win.
                        GameManager.Instance.Caught(R.secondStaff);Need(GameManager.Instance.Current==GameManager.State.Detention&&GameManager.Instance.detention.Active&&R.Detentions==1,"second-staff capture gives detention instead of ending the run");
                        Need(R.Count==4&&!R.Has(4)&&P.HasPhone,"detention removes only latest item and preserves earlier phone");
                        Need(R.HasStoreKey&&R.HasBoltCutters&&R.CageOpen&&Door("Store cupboard").IsUnlocked,"detention preserves tools and opened gates");
                        DetentionSmokeTest.CompleteForRegression(GameManager.Instance.detention);Next(10);break;
                    case 10:
                        if(!GameManager.Instance.IsPlaying)return;
                        R.caretaker.Freeze();R.secondStaff.Freeze();Need(!GameManager.Instance.detention.Active,"detention release resumes run");
                        Path(GameManager.Instance.detention.arrivalPoint.position,SchoolPlan.Point(702,570),"detention exit remains accessible");
                        Need(!R.ReadyToEscape&&Item(4).visual.activeSelf,"lost item restored and exit relocked");
                        var decoy=P.GetComponent<ClockworkDecoy>();decoy.Collect();Warp(new Vector3(-19.3f,0,60));F.LookLocked=false;P.ViewCamera.transform.localRotation=Quaternion.identity;P.transform.rotation=Quaternion.identity;Cursor.lockState=CursorLockMode.Locked;
                        Need(decoy.Deploy()&&decoy.Charges==0,"wind-up toy deploys and consumes one charge");
                        R.caretaker.ResumeAfterDetention(0);Warp(SchoolPlan.Point(892,300));Next(11);break;
                    case 11:
                        if(now-at<2.6)return;
                        var toy=GameObject.Find("Ticking wind-up decoy");Need(toy!=null,"decoy remains in world during delayed activation");
                        Need(decoyNoises>0&&R.caretaker.Current==CaretakerAI.State.Investigate,"delayed toy noise attracts caretaker investigation");R.caretaker.Freeze();
                        var cover=GameObject.Find("SchoolRun").GetComponentsInChildren<Transform>().First(t=>t.name=="Opaque fabric");
                        Vector3 a=cover.position-cover.forward*2,b=cover.position+cover.forward*2;
                        Need(Physics.Linecast(a,b,out var obstruction)&&obstruction.collider.transform==cover,"opaque cafeteria screen physically blocks sight");
                        foreach(var pp in R.secondStaff.patrol)Path(SchoolPlan.Point(659,874),pp.point.position,"secondary staff "+pp.point.name);
                        foreach(string name in new[]{"North classroom B east","North classroom B south","East classroom north","East classroom south","South room B north","South room B east"})Need(Door(name).IsUnlocked,"chase shortcut accessible: "+name);
                        Item(4).Interact(P);Need(R.ReadyToEscape,"all five again after detention");
                        var mainDoor=Door(PlaytestFixSetup.MainDoorName);Vector3 inside=mainDoor.transform.position+Vector3.forward*1.4f;inside.y=.05f;
                        Vector3 pane=mainDoor.hinge.GetComponentsInChildren<Collider>().First(c=>c.name=="Leaf").bounds.center;inside.x=pane.x;pane.y=1.2f;Reach(mainDoor,inside,pane);
                        Need(mainDoor.CanInteract(P)&&mainDoor.GetPrompt(P).StartsWith("Hold F"),"main entrance doors offer the escape at five of five");
                        mainDoor.Interact(P);Need(GameManager.Instance.Current==GameManager.State.Won,"holding E on the main entrance doors wins the run");
                        GameManager.Instance.Restart();Next(12);break;
                    case 12:
                        if(!ChaseRetryReady){if(now-at>10)throw new Exception("replay after the win never started");return;}
                        Need(R.UnlockLevel==0&&R.Detentions==0&&!R.HasBoltCutters&&!R.HasStoreKey&&!R.CageOpen&&R.pickups.All(p=>p.visual.activeSelf),"restart resets recoveries, keys, cage and detentions");
                        Need(S.IsComplete&&R.TrolleyParked&&S.PhoneDeposited&&!P.HasPhone&&!I.HasCarried(InventoryItemKind.HallPass)&&SchoolPeriodController.InClass(P.transform.position),"replay skips the lesson: chase starts in Year 6, phone back in the office, no hall pass");
                        Need(Gates.All(g=>g.IsOpen&&!g.barrier.activeSelf),"lesson gates are already open on a chase retry");
                        // 2026-09-20 (shorten the retry loop): the dining-hall key trip that killed almost every real
                        // playtest retry is skipped - a chase retry already carries the office key.
                        var retryKey=Object.FindFirstObjectByType<OfficeKeyPickup>(FindObjectsInactive.Include);
                        Need(I.HasCarried(InventoryItemKind.OfficeKey)&&!retryKey.gameObject.activeSelf,"chase retry already carries the office key");
                        // The dropped key trip means the player's real route out of Year 6 no longer passes the dining
                        // hall first; his old resume point ("Collect phone", -17.8,0,34.2) sat right on that new route.
                        var retryHeading=R.caretaker.GetComponent<NavMeshAgent>().destination;
                        Need(Vector3.Distance(new Vector3(retryHeading.x,0,retryHeading.z),new Vector3(-17.8f,0,34.2f))>15f,"chase retry does not resume patrol back toward the corridor outside Year 6 (heading "+retryHeading.ToString("F1")+")");
                        R.caretaker.Freeze();
                        Equip(InventoryItemKind.OfficeKey);
                        Door("Caretaker office").Interact(P);Need(Door("Caretaker office").IsUnlocked,"retry run can unlock the office again");
                        Path(P.transform.position,new Vector3(-44.6f,0,80.1f),"route to retry phone");Warp(new Vector3(-44.6f,0,80.1f));S.phonePickup.Interact(P);Need(R.Count==1&&P.HasPhone,"retry run recovers the phone as item one");
                        // Real capture: a noise turns him toward the player, he sees, chases and makes contact himself.
                        Warp(Corridor);PlaceCaretaker(Corridor+Vector3.left*5.5f,Corridor);R.caretaker.ResumeAfterDetention(0);NoiseEvents.Emit(P.transform.position,12,"dropped satchel");Next(13);break;
                    case 13:
                        if(!chaseNoiseChecked&&R.caretaker.Current==CaretakerAI.State.Chase)
                        {NoiseEvents.Emit(R.caretaker.transform.position,38,"clockwork toy");Need(R.caretaker.Current==CaretakerAI.State.Chase,"toy cannot cancel active visual pursuit");chaseNoiseChecked=true;}
                        if(GameManager.Instance.Current!=GameManager.State.Caught)
                        {
                            if(now-at>20)throw new Exception("caretaker never caught the player: "+R.caretaker.Current+" at "+Gap.ToString("F1")+" m, game "+GameManager.Instance.Current);
                            return;
                        }
                        Need(!GameManager.Instance.detention.Active&&R.Detentions==0,"caretaker capture ends the run; no detention");
                        Need(R.Count==1&&R.Has(0)&&P.HasPhone,"caretaker capture applies no item penalty");
                        Need(!F.enabled&&P.InputLocked&&R.caretaker.Current==CaretakerAI.State.Frozen,"capture stops the player and the caretaker");Next(14);break;
                    case 14:
                        // Let the lunge finish so the retry is taken from the results screen, as a player would.
                        if(now-at<CaretakerCatchScare.Duration+.4f)return;
                        Need(GameManager.Instance.Current==GameManager.State.Caught,"run stays ended until the player retries");
                        GameManager.Instance.Restart();Next(15);break;
                    case 15:
                        if(!ChaseRetryReady){if(now-at>10)throw new Exception("retry after capture never started");return;}
                        Need(!P.HasPhone&&S.PhoneDeposited&&!R.HasBoltCutters&&!R.HasStoreKey&&!R.CageOpen&&SchoolPeriodController.InClass(P.transform.position)&&F.enabled&&!P.InputLocked,"retry after capture restarts the chase from the classroom with nothing kept");
                        Need(errors==0,"no runtime errors");Finish(true);break;
                }
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL stage "+stage+": "+e+"\n");Finish(false);}
        }
        static void Finish(bool success)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;NoiseEvents.OnNoise-=Noise;Time.timeScale=1;Application.runInBackground=background;
            File.AppendAllText(Report,success?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;
        }
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Object = UnityEngine.Object;
namespace Confiscated.EditorTools
{
    /// <summary>Runtime integration checks. Warps only arrange cases; detection, pursuit and detention run normally.</summary>
    [InitializeOnLoad]
    public static class CaretakerRulesSmokeTest
    {
        const string Marker="Temp/run_caretaker_rules",Report="../Docs/CaretakerRules_Validation.txt";
        static int stage,lastFrame; static double start,at; static bool passed,background;
        static SchoolPeriodController S=>Object.FindFirstObjectByType<SchoolPeriodController>();
        static PlayerInteractor P=>S.Player;
        static FirstPersonController F=>P.GetComponent<FirstPersonController>();
        static PlayerInventory I=>P.GetComponent<PlayerInventory>();
        static CaretakerAI A=>S.caretaker;
        static CaretakerPassCheck C=>A.GetComponent<CaretakerPassCheck>();
        static GameManager G=>GameManager.Instance;
        static CaretakerRulesSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Begin();}};}
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Begin(){stage=0;lastFrame=-1;passed=true;start=at=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;File.WriteAllText(Report,"Caretaker rules integration. Opening travel is fast-forwarded; each offence uses real sight, NavMesh pursuit and catch.\n");EditorApplication.update+=Tick;}
        static void Need(bool ok,string text){File.AppendAllText(Report,(ok?"ok   ":"FAIL ")+text+"\n");if(!ok)throw new Exception(text);}
        static void Next(int n){stage=n;at=EditorApplication.timeSinceStartup;}
        static void Warp(Vector3 pos){F.Controller.enabled=false;P.transform.position=pos;F.Controller.enabled=true;F.MovementLocked=true;F.LookLocked=true;Physics.SyncTransforms();}
        static void PlaceCaretaker(Vector3 pos,Vector3 facing)
        {
            A.Freeze();var agent=A.GetComponent<NavMeshAgent>();Need(agent.Warp(pos),"caretaker placed on navigation mesh");
            var point=new GameObject("Smoke test patrol stop").transform;point.position=agent.transform.position;
            A.patrol.Clear();A.patrol.Add(new CaretakerAI.PatrolPoint{point=point,dwellSeconds=100,faceDirection=facing});
            A.transform.rotation=Quaternion.LookRotation(facing);A.StartSchoolRoutine();
        }
        static void Move(InventoryContainer from,InventoryItemKind kind,InventoryContainer to,int index)
        {Need(I.Move(from,I.Find(from,kind),to,index,out var m),m);}
        static void Finish(){EditorApplication.update-=Tick;Application.runInBackground=background;File.AppendAllText(Report,passed?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish();return;}if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            var now=EditorApplication.timeSinceStartup;if(now-at<.2)return;
            try
            {
                if(now-start>180)throw new Exception("Timeout stage "+stage+" phase "+S.Current+" AI "+A.Current+" pos "+A.transform.position+" player "+P.transform.position);
                if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();return;}
                if(ComicDialogue.IsActive){ComicDialogue.Instance.Advance();return;}
                switch(stage)
                {
                    case 0:Need(S.Current==SchoolPeriodController.Phase.Worksheet,"opening lesson is active");S.ChooseSentence(2);Next(1);break;
                    case 1:
                        foreach(var agent in new[]{S.teacher,A.GetComponent<NavMeshAgent>()})if(agent.isOnNavMesh&&agent.hasPath&&!agent.isStopped&&!agent.pathPending)agent.Warp(agent.destination);
                        if(S.Current!=SchoolPeriodController.Phase.Volunteer)return;
                        S.Volunteer();Next(2);break;
                    case 2:
                        Need(S.IsRoaming,"player released for delivery");
                        var pupil=Object.FindFirstObjectByType<SeatedStudent>();Need(pupil!=null&&pupil.lessonFocus==S.teacherHome,"seated pupil tracks the lesson, not the player");
                        var fwd=pupil.transform.forward;Warp(new Vector3(-33.8f,.05f,77));
                        Need(Vector3.Dot(fwd,pupil.transform.forward)>.999f,"pupil orientation remains fixed after player moves");
                        PlaceCaretaker(new Vector3(-33.8f,.02f,79),Vector3.back);C.FilterSight(true);
                        Need(C.Checking,"ordinary corridor encounter asks for a pass");
                        Move(InventoryContainer.Satchel,InventoryItemKind.HallPass,InventoryContainer.Use,0);C.Interact(P);
                        Need(C.HasPassedCheck&&!C.Checking,"equipped hall pass completes the inspection");Next(3);break;
                    case 3:
                        Warp(new Vector3(-33.8f,.05f,75.8f));Next(31);break;
                    case 31:
                        if(!C.WitnessedOffence)return;
                        Need(A.Current==CaretakerAI.State.Chase,"walking away after showing the hall pass starts pursuit");Next(32);break;
                    case 32:
                        if(G.Current!=GameManager.State.Detention)return;
                        Need(G.detention.Active,"caretaker catches the departing player and sends them to detention");
                        var firstBoard=G.detention.blackboard;firstBoard.Open(P);
                        for(float y=-260;y<280;y+=40)firstBoard.Rub(new Vector2(-590,y),new Vector2(590,y));
                        DetentionSmokeTest.CompleteForRegression(G.detention);Next(33);break;
                    case 33:
                        Need(!G.detention.Active&&G.Current==GameManager.State.Playing,"three lines release the hall-pass detention");
                        Warp(new Vector3(-40,.05f,80));PlaceCaretaker(new Vector3(-33.8f,.02f,80),Vector3.left);Next(4);break;
                    case 4:
                        if(now-at<1)return;
                        Need(!C.WitnessedOffence&&G.Current==GameManager.State.Playing,"solid office wall blocks detection");
                        Warp(new Vector3(-40.5f,.05f,82.5f));PlaceCaretaker(new Vector3(-38,.02f,82.5f),Vector3.right);Next(5);break;
                    case 5:
                        if(!C.WitnessedOffence)return;
                        Need(A.Current==CaretakerAI.State.Chase,"seeing an intruder in his office immediately starts pursuit despite accepted pass");Next(6);break;
                    case 6:
                        if(G.Current!=GameManager.State.Detention)return;
                        Need(G.detention.Active,"office intruder is physically caught and sent to detention");Next(7);break;
                    case 7:
                        var board=G.detention.blackboard;board.Open(P);
                        for(float y=-260;y<280;y+=40)board.Rub(new Vector2(-590,y),new Vector2(590,y));
                        Need(board.Erased,"detention board cleared with eraser");Next(8);break;
                    case 8:
                        DetentionSmokeTest.CompleteForRegression(G.detention);Next(9);break;
                    case 9:
                        Need(!G.detention.Active&&G.Current==GameManager.State.Playing,"three accepted lines release detention");
                        Need(!C.WitnessedOffence,"detention clears the witnessed offence");
                        Warp(new Vector3(-33.8f,.05f,77));PlaceCaretaker(new Vector3(-33.8f,.02f,72),Vector3.back);
                        // Arrange a legitimate recovery through the actual key, unlock and phone interaction APIs.
                        Need(G.officeMission.TakeKey(),"spare key can be taken while caretaker is away");I.Collect(I.keyItem);Move(InventoryContainer.Satchel,InventoryItemKind.OfficeKey,InventoryContainer.Use,0);
                        Need(G.officeMission.Unlock(),"equipped key unlocks office");S.phonePickup.enabled=true;S.phonePickup.ReturnToOffice();S.phonePickup.Interact(P);
                        Need(P.HasPhone&&I.HasCarried(InventoryItemKind.Phone),"phone pickup marks phone carried in satchel");
                        Move(InventoryContainer.Satchel,InventoryItemKind.Phone,InventoryContainer.Locker,0);P.GetComponent<PhoneRinger>().Deactivate();PlaceCaretaker(new Vector3(-33.8f,.02f,79),Vector3.back);Next(10);break;
                    case 10:
                        if(now-at<1)return;
                        Need(!P.HasPhone&&I.PhoneStored&&!C.WitnessedOffence,"phone stored in locker does not count as carried contraband");
                        Move(InventoryContainer.Locker,InventoryItemKind.Phone,InventoryContainer.Satchel,2);Next(11);break;
                    case 11:
                        if(!C.WitnessedOffence)return;
                        Need(A.Current==CaretakerAI.State.Chase,"recovered phone in satchel triggers pursuit even during pass grace");
                        Move(InventoryContainer.Satchel,InventoryItemKind.Phone,InventoryContainer.Use,0);
                        Need(P.HasPhone&&C.WitnessedOffence,"equipping phone does not erase the offence");Next(12);break;
                    case 12:
                        if(G.Current!=GameManager.State.Detention)return;
                        Need(G.detention.Active&&!P.HasPhone&&!I.HasCarried(InventoryItemKind.Phone),"caught phone carrier goes to detention and loses recovered phone");
                        Need(S.phonePickup.phoneVisual.activeSelf,"confiscated phone returns to office");
                        Need(I.HasCarried(InventoryItemKind.OfficeKey),"unrelated inventory item is retained");Finish();break;
                }
            }
            catch(Exception e){passed=false;File.AppendAllText(Report,e+"\n");Finish();}
        }
    }
}


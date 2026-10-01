using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace Confiscated.EditorTools
{
    // Exercises the existing hand-authored scene. Never calls a scene installer or builder.
    [InitializeOnLoad]
    public static class OpeningRouteSmokeTest
    {
        const string Marker="Temp/opening_route_test",Report="../Docs/OpeningRoute_Validation.txt";
        static int stage,lastFrame;static double started;static float parkedAt;static bool background;
        static Vector3 trolleyStart;static int doorIndex;static OfficeDoor[] doors;
        static SchoolRunController R=>SchoolRunController.Instance;
        static SchoolPeriodController P=>R.period;
        static CaretakerAI A=>R.caretaker;
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static OpeningRouteSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);stage=0;lastFrame=-1;doorIndex=0;started=EditorApplication.timeSinceStartup;File.WriteAllText(Report,DateTime.UtcNow.ToString("O")+" Opening delivery: actual navigation, no actor warps on the opening route.\n");background=Application.runInBackground;Application.runInBackground=true;EditorApplication.update+=Tick;}};}
        [MenuItem("Confiscated/School Run/Arm Opening Route Test")]
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Need(bool condition,string message){if(!condition)throw new Exception(message);File.AppendAllText(Report,"ok "+message+"\n");}
        static void Finish(bool passed,string reason="")
        {
            EditorApplication.update-=Tick;Time.timeScale=1;Application.runInBackground=background;
            File.AppendAllText(Report,(passed?"PASS":"FAIL "+reason)+"\n");EditorApplication.isPlaying=false;
        }
        static void WarpPlayer(Vector3 position)
        {
            var mover=P.Player.GetComponent<FirstPersonController>();mover.Controller.enabled=false;P.Player.transform.position=position;mover.Controller.enabled=true;Physics.SyncTransforms();
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish(false,"Play interrupted");return;}
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            try
            {
                if(EditorApplication.timeSinceStartup-started>180)throw new Exception("Timeout stage "+stage+", actor "+A.transform.position+", destination "+A.GetComponent<NavMeshAgent>().destination);
                if(R==null)return;
                if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();return;}
                if(ComicDialogue.IsActive){ComicDialogue.Instance.Advance();return;}
                var diary=Object.FindFirstObjectByType<OpeningDiaryComic>();
                if(diary!=null){typeof(OpeningDiaryComic).GetMethod("Finish",Private).Invoke(diary,new object[]{true});return;}
                Time.timeScale=3;
                switch(stage)
                {
                    case 0:
                        trolleyStart=R.trolley.position;Need(!P.PhoneDeposited&&!R.TrolleyParked,"phone and trolley start undelivered");stage=1;break;
                    case 1:
                        if(!A.OpeningRouteActive)return;
                        Need(P.phoneProp.transform.parent==A.transform,"Mr Reed hands the phone to the caretaker");
                        Need(Vector3.Distance(A.GetComponent<NavMeshAgent>().destination,P.officeDrop.position)<.6f,"first destination is inside his office, not another classroom patrol stop");
                        stage=2;break;
                    case 2:
                        NoiseEvents.Emit(A.transform.position,100,"opening route regression noise");
                        NeedQuiet();
                        if(!P.PhoneDeposited){if(Vector3.Distance(trolleyStart,R.trolley.position)>.01f)throw new Exception("Trolley moved before phone delivery");return;}
                        Need(P.OfficeBounds.Contains(A.transform.position),"phone deposited while caretaker is inside the office");
                        Need(Vector3.Distance(trolleyStart,R.trolley.position)<.01f,"trolley remains in office until phone is deposited");
                        Need(!R.TrolleyParked,"cafeteria stop follows office delivery");stage=3;break;
                    case 3:
                        NoiseEvents.Emit(A.transform.position,100,"opening route regression noise");NeedQuiet();
                        if(!R.TrolleyParked)return;
                        Need(Vector3.Distance(R.trolley.position,R.trolleyDock.position)<.01f,"trolley arrives at authored cafeteria parking spot");
                        Need(!A.Chatting&&!R.KeyWindowOpen,"caretaker arrival alone does not trigger dinner lady");
                        Need(!P.PapersDelivered,"door cue will be tested without a hidden newsletter-delivery prerequisite");
                        parkedAt=Time.time;stage=4;break;
                    case 4:
                        if(Time.time-parkedAt<3)return;
                        Need(Vector3.Distance(A.transform.position,R.trolleyDock.position)<.7f,"caretaker waits at trolley for player door cue");
                        doors=Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None).Where(d=>d.name.StartsWith("Dining ")&&!d.LessonLocked&&!d.closedForRun).ToArray();
                        Need(doors.Length==2,"both cafeteria entrances available during lessons are tested; other doors retain scene locks");stage=5;break;
                    case 5:
                        if(doorIndex>=doors.Length){stage=7;break;}
                        typeof(SchoolRunController).GetField("keyWindowOffered",Private).SetValue(R,false);
                        typeof(SchoolRunController).GetField("diningDoorOpened",Private).SetValue(R,false);
                        var door=doors[doorIndex];
                        // Set only the player at each doorway; caretaker navigation remains real.
                        WarpPlayer(door.transform.position-door.transform.forward*1.3f);
                        typeof(OfficeDoor).GetField("requestedOpen",Private).SetValue(door,false);
                        Need(door.CanInteract(P.Player),door.name+" can be opened during errand");
                        if(doorIndex==1)door.Slam(P.Player.transform.position);else door.Interact(P.Player);
                        Need(A.Chatting&&!A.OpeningRouteActive,door.name+" opening triggers the distraction");
                        var lady=Object.FindFirstObjectByType<DinnerTrolleyPatrol>();
                        Need(lady.GetComponents<AudioSource>().Any(s=>s.isPlaying&&s.clip!=null&&s.clip.name=="DinnerLadyFreezerRequest"),"dinner lady's supplied freezer request plays");
                        Need(A.Current!=CaretakerAI.State.Chase,"opening doorway is protected from immediate pursuit");
                        doorIndex++;stage=6;break;
                    case 6:
                        if(!A.ChatArrived)return;
                        Need(R.KeyWindowOpen,"caretaker reaches dinner lady and exposes the key window");stage=5;break;
                    case 7:
                        var key=Object.FindFirstObjectByType<OfficeKeyPickup>();
                        Need(key.CanInteract(P.Player),"office key is available on the parked trolley");key.Interact(P.Player);
                        Need(P.Player.GetComponent<PlayerInventory>().HasCarried(InventoryItemKind.OfficeKey),"player can take the office key");
                        R.BeginRound();Need(!A.OpeningRouteActive&&R.RoundStarted,"escape run restores normal caretaker behaviour");
                        Finish(true);break;
                }
            }
            catch(Exception e){Finish(false,e.ToString());}
        }
        static void NeedQuiet(){if(A.Current!=CaretakerAI.State.Patrol||A.GetComponent<CaretakerPassCheck>().Checking)throw new Exception("Delivery interrupted by noise or pass inspection");}
    }
}

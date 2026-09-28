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
    /// <summary>Hiding in a Twitch viewer's locker (7 s, unseen and uncatchable, "woz ere" inside) and Smith's library thoughts.</summary>
    [InitializeOnLoad]
    public static class LockerHideThoughtsSmokeTest
    {
        const string Marker="Temp/locker_hide_test",Dir="../Docs/LockerHide",Report=Dir+"/Validation.txt";
        static int step,errors;static double at,started;static bool pass,background,neverSeen,neverCaught;
        static ViewerLockerHide hide;static Vector3 outside;
        static SchoolRunController R=>SchoolRunController.Instance;
        static PlayerInteractor P=>R.period.Player;
        static FirstPersonController F=>P.GetComponent<FirstPersonController>();
        static LockerHideThoughtsSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Start();}};}
        [MenuItem("Confiscated/Play Test/Arm Locker Hide + Library Thoughts Test (runs on next Play)")]
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Start()
        {
            Directory.CreateDirectory(Dir);step=errors=0;pass=true;at=started=EditorApplication.timeSinceStartup;
            background=Application.runInBackground;Application.runInBackground=true;
            File.WriteAllText(Report,"Viewer locker hiding and library thoughts ("+DateTime.Now.ToString("s")+")\n");
            Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        }
        static void Log(string m,string t,LogType type){if(type==LogType.Error||type==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+m+"\n");}}
        static void Check(bool ok,string message){pass&=ok;File.AppendAllText(Report,(ok?"ok   ":"FAIL ")+message+"\n");}
        static void Next(int n){step=n;at=EditorApplication.timeSinceStartup;}
        static void Warp(Vector3 p){F.Controller.enabled=false;P.transform.position=p;F.Controller.enabled=true;Physics.SyncTransforms();}
        static Vector3 OnMesh(Vector3 p){return NavMesh.SamplePosition(p,out var hit,4,NavMesh.AllAreas)?hit.position:p;}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish();return;}
            double elapsed=EditorApplication.timeSinceStartup-at;
            try
            {
                if(EditorApplication.timeSinceStartup-started>90)throw new Exception("timeout at step "+step);
                switch(step)
                {
                    case 0:
                        if(elapsed<1)return;
                        if(TwitchChat.Instance!=null&&TwitchChat.Instance.State!=TwitchChat.Status.Off)TwitchChat.Instance.Disconnect();
                        if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();}
                        Next(1);break;
                    case 1:
                        if(elapsed<1)return;
                        ComicDialogue.Cancel();R.period.PrepareChaseRetry();R.PrepareChaseRetry();R.secondStaff?.Freeze();
                        var shade=Object.FindFirstObjectByType<LibraryShadow>();if(shade!=null)shade.Paused=true;
                        // A viewer claims a locker (keep claiming until one lands on a locker rather than a desk).
                        if(TwitchChaos.Instance!=null)TwitchChaos.Instance.enabled=false; // no chat votes firing mid-test
                        var names=TwitchNameCards.Instance;
                        for(int i=0;i<60&&hide==null;i++){TwitchChat.InjectForTest("HideyViewer"+i,"hi");names.Claim("HideyViewer"+i);if(names.KindFor("HideyViewer"+i)==TwitchNameCards.Kind.Locker)hide=names.HideFor("HideyViewer"+i);}
                        Check(hide!=null,"a viewer's locker has a hiding spot ("+hide?.viewer+")");
                        Next(2);break;
                    case 2:
                        if(elapsed<.5)return;
                        var body=hide.locker.GetComponent<Renderer>().bounds;var front=-hide.locker.forward;
                        outside=OnMesh(new Vector3(body.center.x,0,body.center.z)+front*1.1f);
                        Warp(outside);P.transform.rotation=Quaternion.LookRotation(-front);
                        Check(hide.GetPrompt(P).Contains(hide.viewer)&&hide.CanInteract(P),"prompt names the viewer: \""+hide.GetPrompt(P)+"\"");
                        // The caretaker is right there and coming.
                        var agent=R.caretaker.GetComponent<NavMeshAgent>();agent.Warp(OnMesh(outside+Vector3.Cross(Vector3.up,front)*3.5f));
                        R.caretaker.ResumeAfterDetention(0);R.caretaker.PursuePlayerForOffence();
                        hide.Interact(P);neverSeen=neverCaught=true;Next(3);break;
                    case 3:
                        neverSeen&=!R.caretaker.CanCurrentlySeePlayer;neverCaught&=GameManager.Instance.IsPlaying;
                        if(elapsed<1.2)return;
                        Check(ViewerLockerHide.Current==hide&&CaretakerAI.PlayerHidden&&F.MovementLocked,"player is shut in the locker");
                        var scrawl=hide.Inside!=null?hide.Inside.GetComponentInChildren<TextMesh>():null;
                        Check(scrawl!=null&&scrawl.text.Replace("\n"," ")==hide.viewer+" woz ere","inside the door: \""+scrawl?.text.Replace("\n"," ")+"\"");
                        ScreenCapture.CaptureScreenshot(Dir+"/InsideLocker.png");Next(4);break;
                    case 4:
                        neverSeen&=!R.caretaker.CanCurrentlySeePlayer||ViewerLockerHide.Current==null;neverCaught&=GameManager.Instance.IsPlaying;
                        if(ViewerLockerHide.Current!=null&&elapsed<9)return;
                        Check(neverSeen&&neverCaught,"the caretaker, right outside, never saw or caught a hidden player");
                        double total=EditorApplication.timeSinceStartup-at+1.2;
                        Check(ViewerLockerHide.Current==null&&total>6.5&&total<8.5,"let out after about seven seconds ("+total.ToString("0.0")+" s)");
                        Check(!CaretakerAI.PlayerHidden&&!F.MovementLocked&&F.Controller.enabled&&Vector3.Distance(P.transform.position,outside)<1.2f,"back out in front of the locker, free to move");
                        Check(!hide.CanInteract(P)&&hide.GetPrompt(P).Contains("rattling"),"that locker can't be used again straight away");
                        R.caretaker.Freeze();
                        Next(5);break;
                    case 5:
                        // Library: no torch, rumour not heard yet.
                        var inside=OnMesh(LibrarySetup.Interior.center);
                        Warp(OnMesh(SchoolPlan.Point(465,1100)));Next(6);break;
                    case 6:
                        if(elapsed<.3)return;
                        Warp(OnMesh(LibrarySetup.Interior.center));Next(7);break;
                    case 7:
                        if(elapsed<.6)return;
                        var th=PlayerThoughts.Instance;
                        Check(th!=null&&th.Showing==PlayerThoughts.NoTorch,"walking into the library without the torch: \""+th?.Showing+"\"");
                        ScreenCapture.CaptureScreenshot(Dir+"/LibraryThought.png");
                        // Now he has told the rumour: going back in recalls it (once).
                        var chat=Object.FindFirstObjectByType<ChatterboxStudent>();
                        typeof(ChatterboxStudent).GetField("<ToldRumour>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(chat,true);
                        Warp(OnMesh(SchoolPlan.Point(465,1100)));Next(8);break;
                    case 8:
                        if(elapsed<.3)return;
                        Warp(OnMesh(LibrarySetup.Interior.center));Next(9);break;
                    case 9:
                        var t=PlayerThoughts.Instance;
                        if(t.Showing!=PlayerThoughts.Legend&&elapsed<14)return;
                        Check(t.Showing==PlayerThoughts.Legend,"then he recalls the chatterbox's rumour: \""+t.Showing+"\"");
                        ScreenCapture.CaptureScreenshot(Dir+"/LibraryLegend.png");Next(10);break;
                    case 10:
                        if(elapsed<.4)return;
                        int before=PlayerThoughts.Instance.Count;
                        Warp(OnMesh(SchoolPlan.Point(465,1100)));Next(11);break;
                    case 11:
                        if(elapsed<.3)return;
                        Warp(OnMesh(LibrarySetup.Interior.center));Next(12);break;
                    case 12:
                        if(elapsed<8)return;
                        Check(PlayerThoughts.Instance.Showing!=PlayerThoughts.Legend,"the rumour isn't repeated on every visit");
                        // Final escape: all five back, the grinning audience appears at the windows (WindowWatchers).
                        for(int id=0;id<5;id++)if(!R.Has(id))R.RecoveryOrder.Add(id);
                        Next(13);break;
                    case 13:
                        if(elapsed<2.5)return;
                        var watchers=R.GetComponent<WindowWatchers>();
                        Check(watchers!=null&&watchers.Showing&&watchers.WindowCount==8&&watchers.HeadCount==16,"all five back: grinning heads at every exterior window ("+watchers?.WindowCount+" windows, "+watchers?.HeadCount+" heads)");
                        var w=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t=>t.name=="Garden view window 0 Wall_112");
                        var cam=new GameObject("Watcher camera").AddComponent<Camera>();cam.fieldOfView=60;
                        cam.transform.position=w.position-w.forward*2.4f;cam.transform.LookAt(w.position);
                        HallwayPropLibrary.Capture(cam,1400,900,Path.GetFullPath(Dir+"/WindowWatchers.png"));
                        cam.transform.position=w.position+w.forward*3.5f+Vector3.right*1.5f;cam.transform.LookAt(w.position);
                        HallwayPropLibrary.Capture(cam,1400,900,Path.GetFullPath(Dir+"/WindowWatchers_Outside.png"));
                        var head=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).First(r=>r.name.Contains("watching "+w.name));
                        File.AppendAllText(Report,"head "+head.name+" at "+head.transform.position.ToString("F2")+" scale "+head.transform.lossyScale.ToString("F2")+" window "+w.position.ToString("F2")+"\n");
                        foreach(var h in Physics.RaycastAll(cam.transform.position,(head.transform.position-cam.transform.position).normalized,20))File.AppendAllText(Report,"  ray hits "+h.collider.name+" at "+h.distance.ToString("F2")+"\n");
                        Object.Destroy(cam.gameObject);
                        Finish();break;
                }
            }
            catch(Exception e){Check(false,e.ToString());Finish();}
        }
        static void Finish()
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Application.runInBackground=background;TwitchChat.EndTestFeed();
            Check(errors==0,"no runtime errors");File.AppendAllText(Report,pass?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;
        }
    }
}

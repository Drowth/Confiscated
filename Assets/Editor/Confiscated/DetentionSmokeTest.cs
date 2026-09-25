using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
namespace Confiscated.EditorTools
{
    [InitializeOnLoad]
    public static class DetentionSmokeTest
    {
        const string Marker="Temp/run_detention",Report="../Docs/Blackboard_Validation.txt";
        static Mouse testMouse,originalMouse;static InputSettings.BackgroundBehavior oldBackground;static InputSettings.EditorInputBehaviorInPlayMode oldFocus;
        static int step,errors;static double at,started;static bool pass;
        static GameManager G=>GameManager.Instance;
        static PlayerInteractor P=>Object.FindFirstObjectByType<PlayerInteractor>();
        static DetentionController D=>G.detention;
        static DetentionSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Start();}};}
        [MenuItem("Confiscated/Play Test/Arm Detention Blackboard Test (runs on next Play)")]
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Start(){oldBackground=InputSystem.settings.backgroundBehavior;oldFocus=InputSystem.settings.editorInputBehaviorInPlayMode;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;originalMouse=Mouse.current;if(originalMouse!=null)InputSystem.DisableDevice(originalMouse);testMouse=InputSystem.AddDevice<Mouse>();step=errors=0;pass=true;started=at=EditorApplication.timeSinceStartup;File.WriteAllText(Report,"Three boards and rubber cleaning validation\n");Application.logMessageReceived+=Log;EditorApplication.update+=Tick;}
        static void Log(string m,string trace,LogType t){if(t==LogType.Error||t==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+m+"\n");}}
        static void Check(bool ok,string message){pass&=ok;File.AppendAllText(Report,(ok?"ok   ":"FAIL ")+message+"\n");if(!ok)throw new Exception(message);}
        static void Clear(DetentionBlackboard b,PlayerInteractor p){if(b.Erased)return;b.Open(p);for(int y=-260;y<=260&&!b.Erased;y+=25)b.Rub(new Vector2(-590,y),new Vector2(590,y));}
        static void MouseAt(RectTransform surface,Vector2 point,bool down){InputSystem.QueueStateEvent(testMouse,new MouseState{position=RectTransformUtility.WorldToScreenPoint(null,surface.TransformPoint(point))}.WithButton(MouseButton.Left,down));}
        static void Clap(RubberCleaningStation s){s.BeginDrag(true,s.LeftRubber.anchoredPosition);s.DragTo(new Vector2(-340,45),.1f);s.DragTo(new Vector2(230,45),.12f);s.EndDrag();}
        public static void CompleteForRegression(DetentionController d)
        {
            for(int i=0;i<30&&ComicDialogue.IsActive;i++)ComicDialogue.Instance.Advance();
            var p=Object.FindFirstObjectByType<PlayerInteractor>();foreach(var b in d.blackboards)Clear(b,p);
            var s=d.cleaningStation;s.Open(p);for(int i=0;i<25&&!s.Clean;i++)Clap(s);d.CompleteCleaning();
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish(false);return;}double now=EditorApplication.timeSinceStartup;if(now-at<.5)return;at=now;
            try
            {
                if(now-started>75)throw new Exception("Timeout at "+step);
                if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();return;}
                if(ComicDialogue.IsActive){ComicDialogue.Instance.Advance();return;}
                var s=D.cleaningStation;
                switch(step)
                {
                    case 0:Check(D.Ready,"three saved boards and station references");Physics.SyncTransforms();foreach(var board in D.blackboards){var origin=board.transform.TransformPoint(new Vector3(-.9f,-.15f,-1.2f));Check(Physics.Raycast(origin,board.transform.forward,out var hit,1.5f)&&hit.collider.GetComponentInParent<DetentionBlackboard>()==board,"board "+board.boardNumber+" reachable by interaction ray");}var stationOrigin=s.transform.TransformPoint(new Vector3(0,.6f,-1));Check(Physics.Raycast(stationOrigin,(s.transform.position-stationOrigin).normalized,out var sh,1.5f)&&sh.collider.GetComponentInParent<RubberCleaningStation>()==s,"station reachable by interaction ray");G.Caught();Check(D.Active,"caught starts detention");D.CompleteCleaning();Check(D.Active,"unfinished task cannot release");s.Open(P);Check(!s.IsOpen,"rubbers gated behind boards");D.blackboard.Open(P);step++;break;
                    case 1:
                        var b=D.blackboard;ScreenCapture.CaptureScreenshot("../Docs/Detention_Board_Before.png");b.Rub(new Vector2(-450,175),new Vector2(50,175));float partial=b.Ink.Cleared;Check(partial>0&&partial<.9f,"rubbing only clears touched chalk");Check(b.Dust.Emitted>0,"erasing produces particles");b.Close();b.Open(P);Check(Mathf.Abs(partial-b.Ink.Cleared)<.001f,"partial board progress persists");step++;break;
                    case 2:
                        ScreenCapture.CaptureScreenshot("../Docs/Detention_Board_Partial.png");Clear(D.blackboard,P);Check(D.BoardsCleared==1&&D.Active,"one board stays in detention");Clear(D.blackboards[1],P);Check(D.BoardsCleared==2&&D.Active,"two boards stay in detention");Clear(D.blackboards[2],P);Check(D.BoardsCleared==3&&D.Active,"all boards still require rubbers");D.CompleteCleaning();Check(D.Active,"dirty rubbers block direct release");s.Open(P);Check(s.IsOpen&&P.InputLocked,"station locks world controls");step++;break;
                    case 3:
                        ScreenCapture.CaptureScreenshot("../Docs/Detention_Rubbers_Dirty.png");s.BeginDrag(true,s.LeftRubber.anchoredPosition);s.DragTo(new Vector2(120,45),5);s.EndDrag();Check(s.Claps==0&&s.Dirt==1,"slow pressing does not count as clap");s.LeftRubber.anchoredPosition=new Vector2(-230,45);MouseAt(s.Surface,s.LeftRubber.anchoredPosition,true);step=31;break;
                    case 31:MouseAt(s.Surface,new Vector2(230,45),true);step=32;break;
                    case 32:MouseAt(s.Surface,new Vector2(230,45),false);Check(s.Claps==1&&s.Dirt<1&&s.Dust.Emitted>0,"real mouse drag claps, removes dirt and emits dust");float dirt=s.Dirt;s.Close();s.Open(P);Check(s.Dirt==dirt,"rubber progress persists when stepping away");step=4;break;
                    case 4:ScreenCapture.CaptureScreenshot("../Docs/Detention_Rubbers_Partial.png");for(int i=0;i<25&&!s.Clean;i++)Clap(s);Check(s.Clean&&D.Active,"clean finish shows feedback before release");ScreenCapture.CaptureScreenshot("../Docs/Detention_Rubbers_Clean.png");step++;break;
                    case 5:if(D.Active)return;Check(G.IsPlaying&&!s.IsOpen&&D.door.CanInteract(P),"cleaning automatically dismisses and unlocks door");var f=P.GetComponent<FirstPersonController>();Check(!P.InputLocked&&!f.MovementLocked&&!f.LookLocked,"release restores controls");G.Caught();Check(D.Active&&D.BoardsCleared==0&&s.Dirt==1&&s.Claps==0,"second detention resets all three boards and rubbers");CompleteForRegression(D);Check(!D.Active,"repeat detention is completable");Check(errors==0,"zero runtime errors");Finish(true);break;
                }
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");Finish(false);}
        }
        static void Finish(bool done){EditorApplication.update-=Tick;Application.logMessageReceived-=Log;InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldFocus;if(testMouse!=null)InputSystem.RemoveDevice(testMouse);if(originalMouse!=null)InputSystem.EnableDevice(originalMouse);File.AppendAllText(Report,(pass&&done?"PASS":"FAIL")+"\nDONE\n");if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;}
    }
}



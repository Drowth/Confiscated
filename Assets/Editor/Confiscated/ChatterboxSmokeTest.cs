using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    [InitializeOnLoad]
    public static class ChatterboxSmokeTest
    {
        const string Marker="Temp/chatterbox_test",Report="../Docs/Chatterbox_Validation.txt";
        static int stage,lastFrame;static double at,started;static bool sawOpen,sawClosed,sawVoice,background,idleCaptured;
        static Vector3 lockedPosition;static Quaternion lockedYaw;static long runStart;
        static Keyboard keys,oldKeys;static Mouse mouse,oldMouse;
        static InputSettings.BackgroundBehavior inputBackground;
        static InputSettings.EditorInputBehaviorInPlayMode inputFocus;
        static ChatterboxStudent C=>Object.FindFirstObjectByType<ChatterboxStudent>();
        static SchoolRunController R=>SchoolRunController.Instance;
        static PlayerInteractor P=>R.period.Player;
        static FirstPersonController F=>P.GetComponent<FirstPersonController>();
        static ChatterboxSmokeTest()
        {
            EditorApplication.playModeStateChanged+=s=>
            {
                if(s!=PlayModeStateChange.EnteredPlayMode||!File.Exists(Marker))return;
                File.Delete(Marker);stage=0;lastFrame=-1;sawOpen=sawClosed=sawVoice=idleCaptured=false;started=at=EditorApplication.timeSinceStartup;
                File.WriteAllText(Report,"Ginger chatterbox: actual scene, checkpoint positioning, injected keyboard/mouse input.\n");
                background=Application.runInBackground;Application.runInBackground=true;
                inputBackground=InputSystem.settings.backgroundBehavior;inputFocus=InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                oldKeys=Keyboard.current;oldMouse=Mouse.current;
                if(oldKeys!=null)InputSystem.DisableDevice(oldKeys);if(oldMouse!=null)InputSystem.DisableDevice(oldMouse);
                keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();EditorApplication.update+=Tick;
            };
        }
        [MenuItem("Confiscated/School Run/Arm Chatterbox Test")]
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Check(bool condition,string label){if(!condition)throw new Exception(label);File.AppendAllText(Report,"ok "+label+"\n");}
        static void Next(){stage++;at=EditorApplication.timeSinceStartup;}
        static void Warp(Vector3 p){F.Controller.enabled=false;P.transform.position=p;F.Controller.enabled=true;Physics.SyncTransforms();}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish(false);return;}
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            double elapsed=EditorApplication.timeSinceStartup-at;
            try
            {
                if(EditorApplication.timeSinceStartup-started>35)throw new Exception("test timeout, stage "+stage);
                switch(stage)
                {
                    case 0:
                        if(elapsed<.8)return;
                        if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();}
                        ComicDialogue.Cancel();R.period.PrepareChaseRetry();R.PrepareChaseRetry();R.PauseStaff();
                        Check(C.artwork.sharedMaterial.name=="M_Chatterbox_Ginger","hallway uses dedicated ginger artwork");
                        Check(Object.FindObjectsByType<SeatedStudent>(FindObjectsSortMode.None).Where(s=>s.GetComponent<ChatterboxStudent>()==null).All(s=>s.artwork.GetComponent<Renderer>().sharedMaterial!=C.artwork.sharedMaterial),"classroom pupils keep their own material");
                        C.cooldownSeconds=.25f;Warp(C.transform.position+C.transform.forward*2);
                        P.transform.rotation=Quaternion.LookRotation(-C.transform.forward);F.ResetLook();Next();break;
                    case 1:
                        if(elapsed<.4)return;
                        if(!idleCaptured)
                        {
                            Check(!C.Talking&&!C.MouthOpen&&C.Interruptions==0,"idle mouth stays closed and distant player is safe");
                            P.ViewCamera.transform.LookAt(C.transform.position+Vector3.up*.85f);F.LookLocked=true;
                            ScreenCapture.CaptureScreenshot("../Docs/Chatterbox_Idle.png");idleCaptured=true;at=EditorApplication.timeSinceStartup;return;
                        }
                        F.LookLocked=false;
                        Warp(C.transform.position+C.transform.forward*.75f);Next();break;
                    case 2:
                        if(!C.Talking){if(elapsed>2)throw new Exception("proximity did not start conversation");return;}
                        Check(Time.timeScale==0,"conversation pauses world and locks gameplay through ComicDialogue");
                        lockedPosition=P.transform.position;lockedYaw=P.transform.rotation;runStart=R.Timing.Milliseconds;
                        ComicDialogue.Instance.Advance();Check(C.Talking&&ComicDialogue.Instance.IsTyping,"advance input cannot bypass timed conversation");
                        InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W,Key.LeftShift,Key.F));Next();break;
                    case 3:
                        if(C.Talking)
                        {
                            InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(20,10)});
                            if(C.MouthOpen&&!sawOpen&&ComicDialogue.Instance.VisibleText.Length>32){sawOpen=true;ScreenCapture.CaptureScreenshot("../Docs/Chatterbox_Talking_Open.png");}
                            if(sawOpen&&!C.MouthOpen&&ComicDialogue.Instance.IsSpeaking&&!sawClosed){sawClosed=true;ScreenCapture.CaptureScreenshot("../Docs/Chatterbox_Talking_Closed.png");}
                            sawVoice|=C.GetComponent<AudioSource>().isPlaying;
                            if(Vector3.Distance(P.transform.position,lockedPosition)>.01f||Quaternion.Angle(P.transform.rotation,lockedYaw)>.1f)throw new Exception("movement/look escaped dialogue lock");
                            if(elapsed>7)throw new Exception("conversation failed to release");return;
                        }
                        InputSystem.QueueStateEvent(keys,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());
                        Check(sawOpen&&sawClosed&&sawVoice,"exactly two mouth states observed with audible chatter during speech");
                        Check(elapsed>=3.5&&elapsed<6,"conversation automatically ends after a few seconds");
                        Check(Time.timeScale==1&&!C.MouthOpen&&!C.GetComponent<AudioSource>().isPlaying,"time, closed mouth and silence restored");
                        Check(R.Timing.Milliseconds-runStart>=3500,"real run timer includes conversation");Next();break;
                    case 4:
                        if(elapsed<.5)return;
                        Check(C.Interruptions==1&&!C.Talking,"remaining nearby cannot immediately retrigger");
                        lockedPosition=P.transform.position;InputSystem.QueueStateEvent(keys,new KeyboardState(Key.S));Next();break;
                    case 5:
                        if(elapsed<.35)return;
                        InputSystem.QueueStateEvent(keys,new KeyboardState());
                        Check(Vector3.Distance(lockedPosition,P.transform.position)>.08f,"movement resumes after conversation");
                        Warp(C.transform.position+C.transform.forward*5);Next();break;
                    case 6:
                        if(elapsed<.3)return;
                        Check(C.Available,"leaving and cooldown rearms chatterbox");
                        NoiseEvents.Emit(C.transform.position,38,"clockwork toy");Warp(C.transform.position+C.transform.forward*.75f);Next();break;
                    case 7:
                        if(elapsed<.4)return;
                        Check(C.Distracted&&!C.Talking&&C.Interruptions==1,"decoy still prevents close-range conversation");
                        Check(ComicDialogue.TrySpeakTimed(C.transform,"Chatterbox","Wait, one more thing!",4.2f),"second timed line starts for cancellation test");
                        C.enabled=false;Check(!ComicDialogue.IsActive&&Time.timeScale==1&&!C.MouthOpen,"disabling speaker releases dialogue and closes mouth");
                        ComicDialogue.TrySpeak("Mr Reed: Hello, Smith.");Next();break;
                    case 8:
                        if(elapsed<.8)return;
                        Check(ComicDialogue.Instance.IsReedVoicePlaying,"existing teacher voice still plays");
                        ComicDialogue.Instance.Advance();Check(!ComicDialogue.Instance.IsTyping,"ordinary dialogue remains skippable");
                        ComicDialogue.Instance.Advance();Check(!ComicDialogue.IsActive&&Time.timeScale==1,"ordinary dialogue restores game");
                        Finish(true);break;
                }
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL stage "+stage+": "+e+"\n");Finish(false);}
        }
        static void Finish(bool passed)
        {
            EditorApplication.update-=Tick;ComicDialogue.Cancel();
            if(keys!=null)InputSystem.RemoveDevice(keys);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            if(oldKeys!=null)InputSystem.EnableDevice(oldKeys);if(oldMouse!=null)InputSystem.EnableDevice(oldMouse);
            InputSystem.settings.backgroundBehavior=inputBackground;InputSystem.settings.editorInputBehaviorInPlayMode=inputFocus;
            Application.runInBackground=background;File.AppendAllText(Report,passed?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;
        }
    }
}

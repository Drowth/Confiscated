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
        static int stage,lastFrame;static double at,started;static bool sawOpen,sawClosed,sawVoice,background,idleCaptured,moved;static int noises,shownAt2;
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
                File.Delete(Marker);stage=0;lastFrame=-1;sawOpen=sawClosed=sawVoice=idleCaptured=moved=false;noises=0;shownAt2=-1;NoiseEvents.OnNoise-=Heard;NoiseEvents.OnNoise+=Heard;started=at=EditorApplication.timeSinceStartup;
                File.WriteAllText(Report,"Ginger chatterbox: actual scene, checkpoint positioning, injected keyboard/mouse input. He talks in the world: the game keeps running.\n");
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
        static void Heard(Vector3 at,float radius,string source){if(source=="chatterbox")noises++;}
        static void Warp(Vector3 p){F.Controller.enabled=false;P.transform.position=p;F.Controller.enabled=true;Physics.SyncTransforms();}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish(false);return;}
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            double elapsed=EditorApplication.timeSinceStartup-at;
            try
            {
                if(EditorApplication.timeSinceStartup-started>70)throw new Exception("test timeout, stage "+stage);
                switch(stage)
                {
                    case 0:
                        if(elapsed<.8)return;
                        if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();}
                        ComicDialogue.Cancel();R.period.PrepareChaseRetry();R.PrepareChaseRetry();R.PauseStaff();
                        Check(C.artwork.sharedMaterial.name=="M_Chatterbox_Ginger","hallway uses dedicated ginger artwork");
                        Check(Object.FindObjectsByType<SeatedStudent>(FindObjectsSortMode.None).Where(s=>s.GetComponent<ChatterboxStudent>()==null).All(s=>s.artwork.GetComponent<Renderer>().sharedMaterial!=C.artwork.sharedMaterial),"classroom pupils keep their own material");
                        // Idle first: out of his first-meeting call-out for the screenshot.
                        C.cooldownSeconds=.25f;C.firstCallout=.9f;Warp(C.transform.position+C.transform.forward*2);
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
                        // First meeting: he calls you over as you pass, from 2.5 m.
                        C.firstCallout=3.5f;Warp(C.transform.position+C.transform.forward*2.5f);Next();break;
                    case 2:
                        if(!C.Talking){if(elapsed>2)throw new Exception("passing him did not start his chat");return;}
                        Check(C.Words==ChatterboxStudent.RumourLine,"the first meeting is his library rumour");
                        Check(Time.timeScale==1&&!ComicDialogue.IsActive&&!P.InputLocked&&!F.MovementLocked,"his chat does not pause the game or lock the player");
                        // Strafe along the corridor (backwards would hit the far wall).
                        lockedPosition=P.transform.position;InputSystem.QueueStateEvent(keys,new KeyboardState(Key.D));Next();break;
                    case 3:
                        if(C.Talking)
                        {
                            if(elapsed>.6&&!moved){moved=Vector3.Distance(P.transform.position,lockedPosition)>.3f;InputSystem.QueueStateEvent(keys,new KeyboardState());}
                            if(shownAt2<0&&elapsed>=2)shownAt2=C.Shown.Length;
                            if(C.MouthOpen&&!sawOpen&&C.Shown.Length>32){sawOpen=true;ScreenCapture.CaptureScreenshot("../Docs/Chatterbox_Talking_Open.png");}
                            if(sawOpen&&!C.MouthOpen&&!sawClosed){sawClosed=true;ScreenCapture.CaptureScreenshot("../Docs/Chatterbox_Talking_Closed.png");}
                            sawVoice|=C.GetComponents<AudioSource>().Any(a=>a.isPlaying); // recorded line or placeholder syllables
                            if(elapsed>20)throw new Exception("conversation failed to end");return;
                        }
                        InputSystem.QueueStateEvent(keys,new KeyboardState());
                        Check(moved,"the player can walk while he talks");
                        Check(shownAt2>=20&&shownAt2<=40,"the subtitle types slowly ("+shownAt2+" letters after 2 s)");
                        Check(sawOpen&&sawClosed&&sawVoice,"two mouth states with audible chatter during speech");
                        Check(noises>0,"his chatter is loud: the caretaker can hear it ("+noises+" noises)");
                        Check(C.ToldRumour&&!C.MouthOpen&&!C.GetComponent<AudioSource>().isPlaying,"the rumour is told in full, then quiet");Next();break;
                    case 4:
                        if(elapsed<.5)return;
                        Check(C.Interruptions==1&&!C.Talking,"remaining nearby cannot immediately retrigger");
                        // After his rumour he calls from across the corridor: leave properly (he may move to another bench).
                        Warp(SchoolPlan.Point(465,1100));Next();break;
                    case 5:
                        if(elapsed<.3)return;
                        Check(C.Available,"leaving and cooldown rearms chatterbox");
                        NoiseEvents.Emit(C.transform.position,38,"clockwork toy");Warp(C.transform.position+C.transform.forward*.75f);Next();break;
                    case 6:
                        if(elapsed<.4)return;
                        Check(!C.Distracted&&C.Talking&&C.Interruptions==2,"the wind-up toy does not distract him: he still talks");
                        // Walk off mid-sentence: he stops.
                        Warp(SchoolPlan.Point(465,1100));Next();break;
                    case 7:
                        if(elapsed<.3)return;
                        Check(!C.Talking&&C.CutOffs==1&&!C.MouthOpen,"walking away cuts him off mid-sentence");
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
            EditorApplication.update-=Tick;NoiseEvents.OnNoise-=Heard;ComicDialogue.Cancel();
            if(keys!=null)InputSystem.RemoveDevice(keys);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            if(oldKeys!=null)InputSystem.EnableDevice(oldKeys);if(oldMouse!=null)InputSystem.EnableDevice(oldMouse);
            InputSystem.settings.backgroundBehavior=inputBackground;InputSystem.settings.editorInputBehaviorInPlayMode=inputFocus;
            Application.runInBackground=background;File.AppendAllText(Report,passed?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;
        }
    }
}

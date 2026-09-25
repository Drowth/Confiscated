using System;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using Object=UnityEngine.Object;
namespace Confiscated.EditorTools
{
    [InitializeOnLoad] public static class EscapeFeelSmokeTest
    {
        const string Marker="Temp/escape_feel",Report="../Docs/SchoolRun/Controls_Atmosphere.txt";
        static int stage;static double at;static Mouse mouse,oldMouse;static Keyboard keys,oldKeys;
        static InputSettings.BackgroundBehavior background;static InputSettings.EditorInputBehaviorInPlayMode focus;
        static SchoolRunController R=>SchoolRunController.Instance;
        static PlayerInteractor P=>R.period.Player;
        static FirstPersonController F=>P.GetComponent<FirstPersonController>();
        static EscapeFeelSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);stage=0;at=EditorApplication.timeSinceStartup;File.WriteAllText(Report,"Actual input and pursuit presentation check\n");background=InputSystem.settings.backgroundBehavior;focus=InputSystem.settings.editorInputBehaviorInPlayMode;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;oldMouse=Mouse.current;oldKeys=Keyboard.current;if(oldMouse!=null)InputSystem.DisableDevice(oldMouse);if(oldKeys!=null)InputSystem.DisableDevice(oldKeys);mouse=InputSystem.AddDevice<Mouse>();keys=InputSystem.AddDevice<Keyboard>();EditorApplication.update+=Tick;}};}
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Check(bool value,string label){File.AppendAllText(Report,(value?"ok ":"FAIL ")+label+"\n");if(!value)throw new Exception(label);}
        static void Warp(Vector3 p){F.Controller.enabled=false;P.transform.position=p;F.Controller.enabled=true;Physics.SyncTransforms();}
        static void Next(){stage++;at=EditorApplication.timeSinceStartup;}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish(false);return;}if(EditorApplication.timeSinceStartup-at<.65)return;
            try
            {
                if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();return;}
                if(ComicDialogue.IsActive){ComicDialogue.Instance.Advance();return;}
                switch(stage)
                {
                    case 0:R.period.Stand();R.period.StartEscapeRun();P.HasPhone=true;R.Recover(0);R.caretaker.Freeze();Warp(new Vector3(-33.8f,0,74));P.transform.rotation=Quaternion.identity;F.ResetLook();Cursor.lockState=CursorLockMode.Locked;InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Space));Next();break;
                    case 1:Check(Vector3.Dot(P.ViewCamera.transform.forward,P.transform.forward)<-.9f,"Space looks behind without turning the player body");InputSystem.QueueStateEvent(keys,new KeyboardState());Next();break;
                    case 2:Check(Vector3.Dot(P.ViewCamera.transform.forward,P.transform.forward)>.9f,"releasing Space restores forward view");var tool=Object.FindObjectsByType<AccessToolPickup>(FindObjectsSortMode.None)[0];foreach(var t in Object.FindObjectsByType<AccessToolPickup>(FindObjectsSortMode.None))if(t.tool==AccessToolPickup.Tool.BoltCutters)tool=t;Warp(new Vector3(-45.5f,0,80.1f));F.LookLocked=true;P.ViewCamera.transform.LookAt(tool.transform.position);InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left,true));Next();break;
                    case 3:InputSystem.QueueStateEvent(mouse,new MouseState());Check(R.HasBoltCutters,"left mouse picks up physical tool");Warp(new Vector3(-33.8f,0,74));F.LookLocked=false;P.transform.rotation=Quaternion.identity;F.ResetLook();P.GetComponent<ClockworkDecoy>().Collect();Cursor.lockState=CursorLockMode.Locked;InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right,true));Next();break;
                    case 4:InputSystem.QueueStateEvent(mouse,new MouseState());Check(P.GetComponent<ClockworkDecoy>().Charges==0&&GameObject.Find("Ticking wind-up decoy")!=null,"right mouse deploys a decoy");R.caretaker.GetComponent<NavMeshAgent>().Warp(new Vector3(-33.8f,0,84));R.caretaker.transform.rotation=Quaternion.Euler(0,180,0);R.caretaker.ResumeAfterDetention(0);Next();break;
                    case 5:Check(R.caretaker.Current==CaretakerAI.State.Chase,"caretaker sees the player down the corridor and starts pursuit");Check(HudController.Instance.objectiveText.text.StartsWith("Belongings recovered: 1/5"),"HUD uses one item counter and one immediate instruction");ScreenCapture.CaptureScreenshot("../Docs/SchoolRun/Escape_HUD_Pursuit.png");Next();break;
                    case 6:Check(Object.FindFirstObjectByType<EscapeRunFeedback>()!=null,"escape sound and light feedback active");Finish(true);break;
                }
            }catch(Exception e){File.AppendAllText(Report,e+"\n");Finish(false);}
        }
        static void Finish(bool success){EditorApplication.update-=Tick;if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keys!=null)InputSystem.RemoveDevice(keys);if(oldMouse!=null)InputSystem.EnableDevice(oldMouse);if(oldKeys!=null)InputSystem.EnableDevice(oldKeys);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=focus;File.AppendAllText(Report,success?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;}
    }
}

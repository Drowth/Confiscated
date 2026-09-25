using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
namespace Confiscated.EditorTools
{
    [InitializeOnLoad]
    public static class DarkModeSmokeTest
    {
        const string Marker="Temp/dark_mode_test",Report="../Docs/DarkMode/Validation.txt";
        static int stage,frame,errors;static bool devUnlock,devSaved;
        static double started,at;
        static Keyboard keys,oldKeys;
        static bool background,asyncShaders,keyQueued;
        static InputSettings.BackgroundBehavior inputBackground;
        static InputSettings.EditorInputBehaviorInPlayMode inputFocus;
        static int[] endings;
        static GameManager previous;
        static GameManager G=>GameManager.Instance;
        static SchoolPeriodController S=>G.schoolPeriod;
        static PlayerInteractor P=>S.Player;
        static PlayerInventory I=>P.GetComponent<PlayerInventory>();
        static DarkModeController D=>G.GetComponent<DarkModeController>();
        static PlayerTorch T=>P.GetComponent<PlayerTorch>();
        static DarkModeSmokeTest()
        {
            EditorApplication.playModeStateChanged+=s=>
            {
                if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Begin();}
                if(s==PlayModeStateChange.ExitingPlayMode&&keys!=null)Finish(false);
            };
        }
        [MenuItem("Confiscated/Dark Mode/Arm Integration Test")]
        public static void Arm(){Directory.CreateDirectory("../Docs/DarkMode");File.WriteAllText(Marker,"armed");}
        static void Begin()
        {
            stage=errors=0;frame=-1;started=at=EditorApplication.timeSinceStartup;SteamLeaderboard.Suppress=true;
            asyncShaders=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            File.WriteAllText(Report,"Dark Mode integration: actual scene, natural Reed introduction; checkpoint warps and virtual T input. Unlock prefs restored; no leaderboard uploads.\n");
            endings=Enum.GetValues(typeof(EndingUnlocks.Ending)).Cast<EndingUnlocks.Ending>().Select(e=>PlayerPrefs.GetInt("Confiscated.Ending.v1."+e,-1)).ToArray();
            background=Application.runInBackground;Application.runInBackground=true;
            inputBackground=InputSystem.settings.backgroundBehavior;inputFocus=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            oldKeys=Keyboard.current;if(oldKeys!=null)InputSystem.DisableDevice(oldKeys);keys=InputSystem.AddDevice<Keyboard>();
            Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        }
        static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+message+"\n");}}
        static void Check(bool ok,string text){if(!ok)throw new Exception(text);File.AppendAllText(Report,"ok "+text+"\n");}
        static void Next(){stage++;at=EditorApplication.timeSinceStartup;keyQueued=false;}
        static void Warp(Vector3 p,Vector3 look)
        {
            var f=P.GetComponent<FirstPersonController>();f.Controller.enabled=false;P.transform.position=p;f.Controller.enabled=true;f.LookLocked=true;P.ViewCamera.transform.LookAt(look);Physics.SyncTransforms();
        }
        static void Shot(string name)=>ScreenCapture.CaptureScreenshot("../Docs/DarkMode/"+name+".png");
        static void Tick()
        {
            if(!EditorApplication.isPlaying||frame==Time.frameCount)return;frame=Time.frameCount;
            double dt=EditorApplication.timeSinceStartup-at;
            try
            {
                if(EditorApplication.timeSinceStartup-started>150)throw new Exception("Timeout at stage "+stage);
                switch(stage)
                {
                    case 0:
                        if(dt<1||G==null||!SchoolTitleMenu.IsActive)return;
                        Check(D!=null&&D.windows.Length==8&&D.torchItem!=null&&D.torchItem.icon!=null&&D.torchPrefab!=null,"builder installed controller, torch with icon and eight actual windows");
                        devUnlock=SchoolGameMode.DevUnlock;devSaved=true;SchoolGameMode.DevUnlock=false;
                        foreach(EndingUnlocks.Ending e in Enum.GetValues(typeof(EndingUnlocks.Ending)))PlayerPrefs.DeleteKey("Confiscated.Ending.v1."+e);
                        EndingUnlocks.Unlock(EndingUnlocks.Ending.Caught);Check(!SchoolGameMode.DarkUnlocked&&!SchoolGameMode.Select(true),"caught ending does not unlock Dark Mode");
                        foreach(var e in new[]{EndingUnlocks.Ending.Completed,EndingUnlocks.Ending.FastRun,EndingUnlocks.Ending.QuackEscape})
                        {EndingUnlocks.Unlock(e);Check(SchoolGameMode.DarkUnlocked,"successful ending unlocks mode: "+e);PlayerPrefs.DeleteKey("Confiscated.Ending.v1."+e);}
                        EndingUnlocks.Unlock(EndingUnlocks.Ending.Completed);
                        SchoolTitleMenu.Instance.StartDarkGame();SchoolTitleMenu.Instance.SkipIntro();
                        Check(SchoolGameMode.Dark&&I.Contains(InventoryContainer.Locker,InventoryItemKind.Torch)&&!T.HasTorch,"dark start stocks locker, not player");Next();break;
                    case 1:
                        if(ComicDialogue.IsActive)ComicDialogue.Cancel();
                        if(!S.IsRoaming)return;
                        SchoolRunController.Instance.PauseStaff();S.caretaker.Freeze();
                        Check(!D.Started&&SchoolPeriodController.InClass(P.transform.position),"natural newsletter instruction finishes before blackout");
                        Warp(new Vector3(-30,0,25.9f),new Vector3(-34,1.6f,32));Shot("01-classroom-before");Next();break;
                    case 2:
                        if(dt<1)return;Check(!D.Started,"remaining inside classroom keeps lights on");
                        Warp(new Vector3(-33.6f,0,29),new Vector3(-33.6f,1.5f,48));Next();break;
                    case 3:
                        if(!D.BlackedOut)return;
                        Check(D.Started&&D.FlickerTransitions==7,"leaving classroom flickers then fully cuts power");
                        Check(D.WindowLights.Count==8&&D.WindowLights.All(l=>l.enabled&&l.shadows!=LightShadows.None),"window light sources remain active with shadows");
                        Check(D.LockerLight!=null&&D.LockerLight.gameObject.activeInHierarchy&&D.LockerLight.shadows!=LightShadows.None,"battery light marks and illuminates the torch locker during blackout");
                        Check(D.TorchHint.Contains("AHEAD")&&D.TorchHint.Contains("amber-lit"),"classroom exit shows direction and distance to the lit locker");
                        Check(Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.enabled&&l.intensity>0).All(l=>D.WindowLights.Contains(l)||l==D.LockerLight),"all mains lights are off; windows and locker battery light remain");
                        Check(Shader.GetGlobalFloat("_SchoolDarkness")==1,"illustrated shaders follow blackout");
                        InputSystem.QueueStateEvent(keys,new KeyboardState(Key.T));Shot("02-torch-off");Next();break;
                    case 4:
                        if(dt<.3)return;InputSystem.QueueStateEvent(keys,new KeyboardState());Check(!T.IsOn,"T cannot conjure a torch before locker collection");
                        var locker=Object.FindFirstObjectByType<PlayerLocker>();Warp(locker.transform.position+Vector3.right*.9f,locker.transform.position+Vector3.up);
                        locker.Interact(P);Check(G.lockerUI.IsOpen,"player opens real locker");Shot("03-locker-torch");Next();break;
                    case 5:
                        if(dt<.5)return;
                        var ui=G.lockerUI;var source=ui.Slots.First(s=>s.container==InventoryContainer.Locker&&s.Entry?.definition.kind==InventoryItemKind.Torch);
                        var target=ui.Slots.First(s=>s.container==InventoryContainer.Satchel&&s.Entry==null);
                        ui.Click(source);ui.Click(target);Check(T.HasTorch&&!I.Contains(InventoryContainer.Locker,InventoryItemKind.Torch),"locker UI moves the unique torch into satchel");
                        ui.Close();Warp(new Vector3(-33.6f,0,29),new Vector3(-33.6f,1.5f,48));Next();break;
                    case 6:
                        if(dt<1)return;Check(T.IsOn,"collected torch lights automatically");
                        Check(!D.LockerLight.gameObject.activeInHierarchy&&!D.TorchHint.Contains("amber-lit"),"collecting torch removes battery beacon and directions");Shot("04-torch-on");
                        Next();break;
                    case 7:
                        if(dt<.5)return;if(!keyQueued){InputSystem.QueueStateEvent(keys,new KeyboardState(Key.T));keyQueued=true;return;}
                        if(dt<1)return;Check(!T.IsOn,"T key switches torch off");InputSystem.QueueStateEvent(keys,new KeyboardState());Next();break;
                    case 8:
                        if(dt<.2)return;InputSystem.QueueStateEvent(keys,new KeyboardState(Key.T));Next();break;
                    case 9:
                        if(dt<.3)return;Check(T.IsOn,"second T key press switches torch on");InputSystem.QueueStateEvent(keys,new KeyboardState());
                        P.InputLocked=true;Check(!T.Toggle(),"modal input lock blocks torch toggle");P.InputLocked=false;
                        Time.timeScale=0;Check(!T.Toggle(),"pause blocks torch toggle");Time.timeScale=1;
                        ComicDialogue.TrySpeak("Mr Reed: Wait there.");Check(!T.Toggle(),"dialogue blocks torch toggle");ComicDialogue.Cancel();
                        Check(I.Move(InventoryContainer.Satchel,I.Find(InventoryContainer.Satchel,InventoryItemKind.Torch),InventoryContainer.Locker,0,out _),"torch can be returned to locker");Next();break;
                    case 10:
                        if(dt<.3)return;Check(!T.IsOn&&!T.HasTorch,"stored torch cannot light the corridor remotely");
                        Check(D.LockerLight.gameObject.activeInHierarchy,"returning torch to locker restores its locator light");
                        Warp(new Vector3(-33.6f,0,24),new Vector3(-35.65f,1.5f,25.89f));Shot("05-window-light");Next();break;
                    case 11:
                        if(dt<1)return;
                        if(!keyQueued){Warp(new Vector3(-34.3f,0,24.3f),new Vector3(-32.0f,.65f,26.2f));Shot("05b-window-spill");keyQueued=true;return;}
                        if(dt<2)return;
                        // Exercise the actual successful escape gate, while keeping synthetic scores out of player records.
                        S.PrepareChaseRetry();var run=SchoolRunController.Instance;run.PrepareChaseRetry();run.PauseStaff();
                        G.OnPhoneCollected(P);P.HasPhone=true;run.CageOpen=true;run.TakeTool(AccessToolPickup.Tool.StoreKey);
                        for(int i=1;i<5;i++)run.Recover(i);
                        Check(run.ReadyToEscape&&run.Timing.Category.StartsWith("Dark / "),"dark run uses distinct timing category and escape requirements");
                        Next();break;
                    case 12:
                        if(dt<.6)return;
                        // Caught retry does not persist a synthetic score or unlock an ending beyond restored preferences.
                        G.Caught(S.caretaker);previous=G;G.Restart();Next();break;
                    case 13:
                        if(G==null||G==previous||!SchoolRunController.Instance.RoundStarted)return;
                        Check(SchoolGameMode.Dark&&I.Contains(InventoryContainer.Locker,InventoryItemKind.Torch)&&!D.Started,"retry retains Dark Mode, restocks torch and starts inside lit classroom");
                        SchoolRunController.Instance.PauseStaff();Warp(new Vector3(-33.6f,0,29),new Vector3(-33.6f,1.5f,48));Next();break;
                    case 14:
                        if(!D.BlackedOut)return;Check(D.BlackedOut,"retry leaving classroom triggers outage again");previous=G;G.ReturnToTitle();Next();break;
                    case 15:
                        if(G==null||G==previous||!SchoolTitleMenu.IsActive)return;
                        Check(!SchoolGameMode.Dark&&Shader.GetGlobalFloat("_SchoolDarkness")==0,"return to title clears dark lighting");Shot("06-mode-menu");Next();break;
                    case 16:
                        if(dt<1)return;
                        SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();ComicDialogue.Cancel();S.PrepareChaseRetry();SchoolRunController.Instance.PrepareChaseRetry();SchoolRunController.Instance.PauseStaff();
                        Warp(new Vector3(-33.6f,0,29),new Vector3(-33.6f,1.5f,48));Next();break;
                    case 17:
                        if(dt<1)return;
                        Check(!D.Started&&!I.Contains(InventoryContainer.Locker,InventoryItemKind.Torch)&&T==null,"normal mode has no blackout or torch");Shot("07-normal-lighting");
                        // Normal-mode ambient is deliberately dim now (SchoolLightingSetup) so the ceiling fluorescents
                        // read as the level's real light source; .5f predates that rework. Outage min is ~.025f, so
                        // .1f still clearly distinguishes "restored" from "still blacked out".
                        Check(RenderSettings.ambientLight.maxColorComponent>.1f,"normal ambient lighting restored");
                        Check(errors==0,"no runtime errors");Next();break;
                    case 18:
                        if(dt<1)return;Finish(true);break;
                }
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL stage "+stage+": "+e+"\n");Finish(false);}
        }
        static void Finish(bool success)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
            if(keys!=null){InputSystem.RemoveDevice(keys);keys=null;}if(oldKeys!=null)InputSystem.EnableDevice(oldKeys);
            InputSystem.settings.backgroundBehavior=inputBackground;InputSystem.settings.editorInputBehaviorInPlayMode=inputFocus;Application.runInBackground=background;
            ShaderUtil.allowAsyncCompilation=asyncShaders;
            if(endings!=null)foreach(EndingUnlocks.Ending e in Enum.GetValues(typeof(EndingUnlocks.Ending)))
            {string key="Confiscated.Ending.v1."+e;int v=endings[(int)e];if(v<0)PlayerPrefs.DeleteKey(key);else PlayerPrefs.SetInt(key,v);}
            if(devSaved){SchoolGameMode.DevUnlock=devUnlock;devSaved=false;}
            PlayerPrefs.Save();File.AppendAllText(Report,success?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;
        }
    }
}

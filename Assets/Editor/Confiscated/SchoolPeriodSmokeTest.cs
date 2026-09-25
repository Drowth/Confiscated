using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using Object=UnityEngine.Object;
namespace Confiscated.EditorTools
{
    [InitializeOnLoad]
    public static class SchoolPeriodSmokeTest
    {
        const string Marker="Temp/run_first_period",Report="../Docs/FirstPeriod_Validation.txt";
        static int stage,lastFrame,errors;static double start,at;static bool pass,background;
        static Keyboard kb,oldKeyboard;static Mouse mouse,oldMouse;
        static InputSettings.BackgroundBehavior oldBackground;static InputSettings.EditorInputBehaviorInPlayMode oldFocus;
        static SchoolPeriodController S=>Object.FindFirstObjectByType<SchoolPeriodController>();
        static PlayerInteractor P=>S.Player;
        static FirstPersonController F=>P.GetComponent<FirstPersonController>();
        static PlayerInventory I=>P.GetComponent<PlayerInventory>();
        static CaretakerPassCheck C=>S.caretaker.GetComponent<CaretakerPassCheck>();
        static SchoolPeriodSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Begin();}};}
        public static void Arm()
        {
            if(Object.FindFirstObjectByType<SchoolRunController>()!=null){SchoolRunSmokeTest.Arm();return;}
            File.WriteAllText(Marker,"armed");
        }
        static void Begin()
        {
            stage=errors=0;lastFrame=-1;pass=true;start=at=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;
            oldBackground=InputSystem.settings.backgroundBehavior;oldFocus=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            oldKeyboard=Keyboard.current;oldMouse=Mouse.current;if(oldKeyboard!=null)InputSystem.DisableDevice(oldKeyboard);if(oldMouse!=null)InputSystem.DisableDevice(oldMouse);
            kb=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
            File.WriteAllText(Report,"First period runtime integration test. Player warps between distant checkpoints; NPC travel uses the real NavMesh.\n");
            Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        }
        static void Log(string m,string trace,LogType t){if(t==LogType.Error||t==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+m+"\n");}}
        static void Check(bool ok,string message){pass&=ok;File.AppendAllText(Report,(ok?"ok   ":"FAIL ")+message+"\n");}
        static void Need(bool ok,string message){Check(ok,message);if(!ok)throw new Exception(message);}
        static void Next(int s){stage=s;at=EditorApplication.timeSinceStartup;}
        static void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(kb,new KeyboardState(keys));
        static void Warp(Vector3 p){F.Controller.enabled=false;P.transform.position=p;F.Controller.enabled=true;Physics.SyncTransforms();}
        static void Aim(Vector3 p){F.LookLocked=true;P.ViewCamera.transform.LookAt(p);}
        static void Equip(InventoryItemKind kind){string m;Need(I.Move(InventoryContainer.Satchel,I.Find(InventoryContainer.Satchel,kind),InventoryContainer.Use,0,out m),"equip "+kind);}
        static void Click(string name,bool down)
        {
            var b=S.worksheetUI.GetComponentsInChildren<Button>().First(x=>x.name==name);
            var pos=RectTransformUtility.WorldToScreenPoint(null,b.transform.position);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=pos}.WithButton(MouseButton.Left,down));
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish();return;}if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            double now=EditorApplication.timeSinceStartup;if(now-at<.25)return;
            try
            {
                if(now-start>240)throw new Exception("Timeout in stage "+stage+" phase "+S.Current);
                if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();return;}
                if(ComicDialogue.IsActive){Keys();ComicDialogue.Instance.Advance();return;}
                switch(stage)
                {
                    case 0:
                        Need(S.Current==SchoolPeriodController.Phase.Worksheet&&!F.Controller.enabled&&F.MovementLocked,"start seated in Year 6");
                        Need(!S.phonePickup.enabled&&!S.phonePickup.CanInteract(P),"office phone cannot be picked up before confiscation");
                        C.FilterSight(true);Need(!C.Checking,"no pass check while seated in class");
                        Aim(GameObject.Find("Newsletter worksheet").transform.position);Keys(Key.F);Next(1);break;
                    case 1:
                        Keys();Need(S.worksheetUI.IsOpen,"E opens physical worksheet");
                        S.ChooseSentence(0);Need(S.Current==SchoolPeriodController.Phase.Worksheet,"incorrect answer only gives a hint");
                        ScreenCapture.CaptureScreenshot("../Docs/FirstPeriod_Worksheet.png");Click("Sentence 2",false);Next(2);break;
                    case 2:Click("Sentence 2",true);Next(3);break;
                    case 3:Click("Sentence 2",false);Next(4);break;
                    case 4:
                        Need(S.Current==SchoolPeriodController.Phase.PhoneRinging,"real mouse answer advances worksheet");
                        Aim(S.teacherHome.position+Vector3.up*1.3f);Next(5);break;
                    case 5:
                        var audio=P.GetComponent<PhoneRinger>().Emitter;
                        if(!audio.isPlaying)return;
                        Need(audio.isPlaying&&audio.spatialBlend==1&&audio.clip.name=="PhoneRingtone","user ringtone plays in 3D during phone incident");
                        Need(Vector3.Distance(audio.transform.position,S.phoneProp.transform.position)<.01f,"audio emitter is at desk phone");
                        S.worksheetUI.Open();Need(!S.worksheetUI.IsOpen,"phone incident cannot reopen handover worksheet");Next(51);break;
                    case 51:
                        if(S.Current!=SchoolPeriodController.Phase.Confiscation)return;
                        Need(Vector3.Distance(S.teacher.transform.position,S.teacherAtDesk.position)<.5f,"teacher walks to pupil desk");
                        Need(!P.GetComponent<PhoneRinger>().Emitter.isPlaying&&!S.worksheetUI.IsOpen,"arrival automatically stops ringtone and takes phone without a handover screen");Next(52);break;
                    case 52:
                        if(S.phoneProp.transform.parent!=S.teacher.transform)return;
                        Need(!S.worksheetUI.IsOpen,"phone moves from desk into teacher's possession without player input");
                        HallwayPropLibrary.Capture(P.ViewCamera,1400,900,"D:/Confiscated/Docs/FirstPeriod_MrReed.png");Next(6);break;
                    case 6:
                        if(S.Current!=SchoolPeriodController.Phase.Volunteer)return;
                        Need(S.phoneProp.transform.parent==S.caretaker.transform,"caretaker physically receives phone");
                        Need(Vector3.Distance(S.teacher.transform.position,S.teacherHome.position)<.5f,"teacher returns to board");
                        Need(S.worksheetUI.IsOpen&&Cursor.visible&&Cursor.lockState==CursorLockMode.None,"volunteer worksheet has a visible unlocked mouse after dialogue");
                        Click("Action",true);Next(601);break;
                    case 601:Click("Action",false);Next(602);break;
                    case 602:
                        Need(I.HasCarried(InventoryItemKind.HallPass)&&I.HasCarried(InventoryItemKind.Newsletters)&&F.Controller.enabled,"real mouse volunteer click gets pass, papers, and movement");
                        Next(60);break;
                    case 60:
                        C.FilterSight(true);Need(!C.Checking,"classroom does not trigger corridor pass checks");
                        S.caretaker.GetComponent<NavMeshAgent>().Warp(new Vector3(-17.8f,.02f,34.2f));
                        Warp(new Vector3(-19.8f,.05f,34.2f));Aim(S.caretaker.transform.position+Vector3.up*1.45f);
                        C.FilterSight(true);Need(C.Checking,"caretaker pauses to request hall pass");
                        Need(I.HasCarried(InventoryItemKind.HallPass)&&!I.IsEquipped(InventoryItemKind.HallPass),"pass is carried without equipping");Next(61);break;
                    case 61:
                        var mask=new SerializedObject(P).FindProperty("hitMask").intValue;
                        var ray=new Ray(P.ViewCamera.transform.position,P.ViewCamera.transform.forward);
                        bool hit=Physics.Raycast(ray,out var h,2.6f,mask,QueryTriggerInteraction.Collide);
                        Need(hit&&h.collider.GetComponentInParent<CaretakerPassCheck>()==C,"interaction ray reaches caretaker; hit="+(hit?h.collider.name:"none")+" mask="+mask+" locked="+P.InputLocked);
                        Keys(Key.F);Next(7);break;
                    case 7:
                        Keys();Need(C.HasPassedCheck&&!C.Checking,"E shows carried pass and completes the inspection");
                        ScreenCapture.CaptureScreenshot("../Docs/FirstPeriod_Pass.png");
                        Next(71);break;
                    case 71:
                        Warp(new Vector3(-21.2f,.05f,34.2f));Next(72);break;
                    case 72:
                        Need(GameManager.Instance.IsPlaying&&!C.WitnessedOffence,"walking away after showing the pass is permitted");
                        Next(73);break;
                    case 73:
                        if(!GameManager.Instance.IsPlaying)return;
                        Warp(new Vector3(-33.8f,.05f,79.3f));Aim(S.deliveryTray.transform.position);
                        S.ReturnToClass();Need(S.Current==SchoolPeriodController.Phase.Delivery,"cannot finish before delivery");
                        Need(I.HasCarried(InventoryItemKind.Newsletters)&&!I.IsEquipped(InventoryItemKind.Newsletters),"newsletters stay in satchel for delivery");Next(8);break;
                    case 8:Keys(Key.F);Next(9);break;
                    case 9:
                        Keys();Need(S.PapersDelivered&&!I.HasCarried(InventoryItemKind.Newsletters),"E delivers carried papers and removes inventory copy");
                        ScreenCapture.CaptureScreenshot("../Docs/FirstPeriod_Delivery.png");
                        Next(91);break;
                    case 91:
                        Warp(new Vector3(-19.6f,.05f,29));S.reminderAfter=.5f;S.lateAfter=1;
                        Next(10);break;
                    case 10:
                        if(!S.PhoneDeposited)return;
                        Need(S.TeacherConcern==2,"absence thresholds produce teacher concern");
                        Need(S.phonePickup.enabled&&S.phonePickup.phoneVisual.activeSelf&&!S.phoneProp.activeSelf,"caretaker reaches office and deposits retrievable phone");
                        S.ReturnToClass();Need(S.Current==SchoolPeriodController.Phase.Review&&S.worksheetUI.IsOpen,"report back opens final caption");
                        S.ChooseSentence(2);Need(!S.IsComplete,"incorrect caption cannot finish period");
                        S.ChooseSentence(0);Next(11);break;
                    case 11:
                        Need(S.IsComplete&&GameManager.Instance.Current==GameManager.State.Won,"correct caption ends first period");
                        Need(Object.FindFirstObjectByType<SchoolBellSystem>().IsRinging,"completion plays the school bells");
                        ScreenCapture.CaptureScreenshot("../Docs/FirstPeriod_Complete.png");Next(12);break;
                    case 12:GameManager.Instance.Restart();Next(13);break;
                    case 13:
                        Need(S.Current==SchoolPeriodController.Phase.Worksheet&&!P.HasPhone&&!S.PhoneDeposited&&!F.Controller.enabled,"restart restores seated classroom and clean period state");
                        Finish();break;
                }
            }
            catch(Exception e){Check(false,e.ToString());Finish();}
        }
        static void Finish()
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
            if(kb!=null&&kb.added)InputSystem.RemoveDevice(kb);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
            if(oldKeyboard!=null&&oldKeyboard.added)InputSystem.EnableDevice(oldKeyboard);if(oldMouse!=null&&oldMouse.added)InputSystem.EnableDevice(oldMouse);
            InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldFocus;
            Application.runInBackground=background;Check(errors==0,"no runtime errors");File.AppendAllText(Report,pass?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;
        }
    }
}

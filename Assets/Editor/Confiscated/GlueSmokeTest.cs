using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    [InitializeOnLoad]
    public static class GlueSmokeTest
    {
        const string Marker="Temp/glue_test",Report="../Docs/Glue_Validation.txt";
        static int stage,lastFrame;static double at,started;static float stuckAt;static bool background;
        static Vector3 heldPosition,dropPlayerPosition;static Quaternion heldRotation;
        static Keyboard keys,oldKeys;static Mouse mouse,oldMouse;
        static InputSettings.BackgroundBehavior inputBackground;
        static InputSettings.EditorInputBehaviorInPlayMode inputFocus;
        static GluePuddle puddle;static int duckCount;
        static SchoolRunController R=>SchoolRunController.Instance;
        static PlayerInteractor P=>R.period.Player;
        static FirstPersonController F=>P.GetComponent<FirstPersonController>();
        static GlueDeployer D=>P.GetComponent<GlueDeployer>();
        static CaretakerAI C=>R.caretaker;
        static GluePickup Bottle=>Object.FindFirstObjectByType<GluePickup>();
        static GlueSmokeTest()
        {
            EditorApplication.playModeStateChanged+=s=>
            {
                if(s!=PlayModeStateChange.EnteredPlayMode||!File.Exists(Marker))return;
                File.Delete(Marker);stage=0;lastFrame=-1;started=at=EditorApplication.timeSinceStartup;
                File.WriteAllText(Report,"Glue pickup/deployment integration: actual scene and player input; checkpoint warps for setup; caretaker runs over glue using normal NavMesh chase.\n");
                background=Application.runInBackground;Application.runInBackground=true;
                inputBackground=InputSystem.settings.backgroundBehavior;inputFocus=InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                oldKeys=Keyboard.current;oldMouse=Mouse.current;
                if(oldKeys!=null)InputSystem.DisableDevice(oldKeys);if(oldMouse!=null)InputSystem.DisableDevice(oldMouse);
                keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();EditorApplication.update+=Tick;
            };
        }
        [MenuItem("Confiscated/School Run/Arm Glue Test")]
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Check(bool condition,string label){if(!condition)throw new Exception(label);File.AppendAllText(Report,"ok "+label+"\n");}
        static void Next(){stage++;at=EditorApplication.timeSinceStartup;}
        static void Warp(Vector3 position){F.Controller.enabled=false;P.transform.position=position;F.Controller.enabled=true;Physics.SyncTransforms();}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish(false);return;}
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            double elapsed=EditorApplication.timeSinceStartup-at;
            try
            {
                if(EditorApplication.timeSinceStartup-started>65)throw new Exception("test timed out at stage "+stage);
                switch(stage)
                {
                    case 0:
                        if(elapsed<.8)return;
                        if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();}
                        ComicDialogue.Cancel();R.period.PrepareChaseRetry();R.PrepareChaseRetry();R.PauseStaff();
                        Check(Bottle!=null&&Bottle.transform.position.x>-11&&Bottle.transform.position.x<1&&Bottle.transform.position.z<32,"glue lives in optional Art Room");
                        Check(!Object.FindObjectsByType<RunPickup>(FindObjectsSortMode.None).Any(p=>p.transform.position.x>-11&&p.transform.position.x<1&&p.transform.position.z>9&&p.transform.position.z<32),"no required belonging is placed in glue room");
                        var door=Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None).First(d=>d.name=="South room B north");
                        door.Interact(P);Warp(new Vector3(-4.54f,0,28.25f));
                        F.LookLocked=true;P.ViewCamera.transform.LookAt(Bottle.transform.position+Vector3.up*.22f);Next();break;
                    case 1:
                        if(elapsed<1)return;
                        var path=new NavMeshPath();
                        Check(NavMesh.CalculatePath(new Vector3(-9.11f,0,34),P.transform.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"Art Room bottle has a navigable route from corridor");
                        Check(PlayerInteractor.Resolve(new Ray(P.ViewCamera.transform.position,P.ViewCamera.transform.forward),2.6f,~0)==Bottle,"bottle can be targeted over the desk");
                        ScreenCapture.CaptureScreenshot("../Docs/Glue_Pickup.png");Next();break;
                    case 2:
                        if(elapsed<.3)return;
                        InputSystem.QueueStateEvent(keys,new KeyboardState(Key.F));Next();break;
                    case 3:
                        if(elapsed<.2)return;
                        InputSystem.QueueStateEvent(keys,new KeyboardState());
                        Check(Bottle.Taken&&D.Charges==1&&!Bottle.visual.activeSelf,"E collects FBX bottle, grants one charge and removes pickup");
                        Bottle.Interact(P);Check(D.Charges==1&&!D.Collect(),"no duplicate pickup or over-capacity charges");
                        P.InputLocked=true;Check(!D.Deploy()&&D.Charges==1,"locked interaction cannot spend glue");P.InputLocked=false;
                        ComicDialogue.TrySpeak("Mr Reed: Wait there.");Check(!D.Deploy()&&D.Charges==1,"dialogue cannot deploy glue");ComicDialogue.Cancel();
                        Warp(new Vector3(-33.8f,0,76));F.LookLocked=false;P.transform.rotation=Quaternion.identity;F.ResetLook();Cursor.lockState=CursorLockMode.Locked;
                        C.GetComponent<NavMeshAgent>().Warp(new Vector3(-33.8f,0,70));C.Freeze();
                        duckCount=P.GetComponent<ClockworkDecoy>().Charges;dropPlayerPosition=P.transform.position;
                        InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W));Next();break;
                    case 4:
                        if(elapsed<.3)return;
                        Check(Vector3.Distance(P.transform.position,dropPlayerPosition)>.1f,"player is moving before deployment");
                        InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W,Key.G));Next();break;
                    case 5:
                        if(elapsed<.12)return;
                        InputSystem.QueueStateEvent(keys,new KeyboardState());puddle=D.LastDeployed;
                        Check(puddle!=null&&D.Charges==0,"G deploys one puddle while moving");
                        Check(puddle.transform.position.z<P.transform.position.z&&puddle.transform.position.y<.08f,"glue lies on floor behind direction of travel");
                        Check(D.DropAudio.isPlaying&&D.DropAudio.clip.name=="GlueDrop"&&D.DropAudio.transform==P.transform,"GlueDrop plays at the player");
                        Check(!puddle.Consumed&&!C.IsGlued&&!puddle.StuckAudio.isPlaying,"deployment alone does not immobilise caretaker or play stuck sound");
                        Check(!D.Deploy()&&Object.FindObjectsByType<GluePuddle>(FindObjectsSortMode.None).Length==1,"empty use cannot create more puddles");
                        Check(P.GetComponent<ClockworkDecoy>().Charges==duckCount,"glue input does not consume a duck");
                        Warp(puddle.transform.position+Vector3.up*-.012f);Next();break;
                    case 6:
                        if(elapsed<.25)return;
                        Check(!puddle.Consumed&&!F.IsFallen,"player can cross own glue safely");
                        Warp(new Vector3(-33.8f,0,85));F.LookLocked=true;P.ViewCamera.transform.LookAt(puddle.transform.position);
                        C.transform.rotation=Quaternion.identity;C.ResumeAfterDetention(0);C.PursuePlayerForOffence();Next();break;
                    case 7:
                        if(!puddle.Consumed){if(elapsed>8)throw new Exception("caretaker missed puddle: "+C.transform.position);return;}
                        Check(C.IsGlued&&C.GetComponent<NavMeshAgent>().isStopped,"normal chase crosses puddle and stops caretaker");
                        Check(puddle.StuckAudio.isPlaying&&puddle.StuckAudio.clip.name=="GlueStuck"&&puddle.StuckAudio.spatialBlend==1&&puddle.StuckAudio.transform==puddle.transform,"GlueStuck plays once spatially from the puddle");
                        heldPosition=C.transform.position;heldRotation=C.transform.rotation;stuckAt=Time.time;
                        Check(!C.TryStickInGlue(4)&&!C.CanPhysicallyCatchPlayer(100),"active glue cannot retrigger or catch player");
                        NoiseEvents.Emit(C.transform.position,38,"clockwork toy");C.SetRunPressure(5);C.PursuePlayerForOffence();
                        P.ViewCamera.transform.LookAt(C.transform.position+Vector3.up*.8f);
                        ScreenCapture.CaptureScreenshot("../Docs/Glue_Caretaker_Stuck.png");Next();break;
                    case 8:
                        if(Time.time-stuckAt<3.85f)
                        {
                            if(!C.IsGlued||Vector3.Distance(C.transform.position,heldPosition)>.015f||Quaternion.Angle(C.transform.rotation,heldRotation)>.1f)throw new Exception("caretaker escaped glue early");
                            return;
                        }
                        Check(C.IsGlued,"caretaker remains rooted for the full four-second interval");Next();break;
                    case 9:
                        if(C.IsGlued){if(Time.time-stuckAt>4.3)throw new Exception("caretaker failed to release");return;}
                        Check(Time.time-stuckAt>=3.95f&&Time.time-stuckAt<4.3f,"hold expires at four gameplay seconds");Next();break;
                    case 10:
                        if(elapsed<.4)return;
                        Check(Vector3.Distance(C.transform.position,heldPosition)>.15f&&!C.GetComponent<NavMeshAgent>().isStopped,"caretaker resumes normal chase after glue");
                        Check(puddle==null,"single-use puddle is cleaned up after the hold and sound");
                        Check(C.TryStickInGlue(4),"a later independent hold can start");C.Freeze();Next();break;
                    case 11:
                        if(elapsed<4.3)return;
                        Check(!C.IsGlued&&C.Current==CaretakerAI.State.Frozen&&C.GetComponent<NavMeshAgent>().isStopped,"expiring glue never undoes an independent end-round freeze");
                        D.Collect();Check(D.Deploy(),"deployment remains available after earlier trap expires");
                        GameManager.Instance.Restart();Next();break;
                    case 12:
                        if(elapsed<1||R==null)return;
                        Check(D.Charges==0&&!Bottle.Taken&&Bottle.visual.activeSelf&&Object.FindObjectsByType<GluePuddle>(FindObjectsSortMode.None).Length==0&&!C.IsGlued,"restart restores bottle and clears carried glue, puddles and immobilisation");
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

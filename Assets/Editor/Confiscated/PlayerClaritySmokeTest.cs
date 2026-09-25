using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    [InitializeOnLoad]
    public static class PlayerClaritySmokeTest
    {
        const string Marker="Temp/test_player_clarity",Folder="../Docs/ClarityPass/",Report=Folder+"Validation.txt";
        static int stage,errors,lastFrame;
        static double began,at;
        static bool background;
        static float farRed,savedFeel;
        static SchoolRunController R=>SchoolRunController.Instance;
        static SchoolPeriodController S=>R.period;
        static PlayerInteractor P=>S.Player;
        static PlayerInventory I=>P.GetComponent<PlayerInventory>();
        static FirstPersonController F=>P.GetComponent<FirstPersonController>();
        static HuntVhsEffect V=>R.GetComponent<HuntVhsEffect>();
        static OfficeDoor Exit=>Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None).First(d=>d.mainExit);
        static PlayerClaritySmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Begin();}};}
        [MenuItem("Confiscated/Playtest/Arm Player Clarity Test")]
        public static void Arm(){Directory.CreateDirectory(Folder);File.WriteAllText(Marker,"armed");}
        static void Begin()
        {
            SteamLeaderboard.Suppress=true;
            stage=errors=0;lastFrame=-1;began=at=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;
            File.WriteAllText(Report,"Player clarity integration: live school scene; staged intro setup; real E and quickbar key input; ray-checked pickups; carried/stored item checks; VHS route proximity and escape.\n");
            Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        }
        static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+message+"\n");}}
        static void Need(bool ok,string label){File.AppendAllText(Report,(ok?"ok   ":"FAIL ")+label+"\n");if(!ok)throw new Exception(label);}
        static void Next(int value){stage=value;at=EditorApplication.timeSinceStartup;}
        static void Keys(params Key[] keys){InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(keys));}
        static void Warp(Vector3 point,Vector3 look)
        {
            point.y=.04f;F.Controller.enabled=false;P.transform.position=point;F.Controller.enabled=true;F.MovementLocked=false;F.LookLocked=true;P.InputLocked=false;
            P.ViewCamera.transform.LookAt(look);Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;Physics.SyncTransforms();
        }
        static void Reach(Interactable target,Vector3 point,Vector3 look)
        {
            Warp(point,look);Need(PlayerInteractor.Resolve(new Ray(P.ViewCamera.transform.position,P.ViewCamera.transform.forward),2.6f,~0)==target,"interaction ray reaches "+target.name);
        }
        static RunPickup Item(int id)=>R.pickups.First(p=>p.itemId==id);
        static void Take(int id)
        {
            var p=Item(id);Reach(p,p.transform.TransformPoint(new Vector3(0,0,-1.25f)),p.transform.TransformPoint(new Vector3(0,.22f,0)));
            p.Interact(P);Need(R.Has(id)&&!p.visual.activeSelf&&p.transform.Find("Confiscation box").gameObject.activeInHierarchy,"belonging "+id+" collected; box stays visibly empty");
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish(false);return;}
            if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
            if(EditorApplication.timeSinceStartup-began>90){File.AppendAllText(Report,"FAIL timeout stage "+stage+"\n");Finish(false);return;}
            if(EditorApplication.timeSinceStartup-at<.35)return;
            try
            {
                if(R==null||GameManager.Instance==null)return;
                if(stage>0){R.caretaker.Freeze();R.secondStaff?.Freeze();ComicDialogue.Cancel();}
                switch(stage)
                {
                    case 0:
                        if(!SchoolTitleMenu.IsActive)return;
                        SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();S.StopAllCoroutines();ComicDialogue.Cancel();
                        typeof(SchoolPeriodController).GetProperty("Current").SetValue(S,SchoolPeriodController.Phase.Volunteer);
                        S.Volunteer();ComicDialogue.Cancel();savedFeel=P.GetComponent<ChaseCamera>().intensity;P.GetComponent<ChaseCamera>().intensity=1;
                        Next(1);break;
                    case 1:
                        Need(HudController.Instance.objectiveText.text=="Deliver the newsletters to the office tray.","delivery has one objective and no belongings counter");
                        Need(I.HasCarried(InventoryItemKind.Newsletters)&&!I.IsEquipped(InventoryItemKind.Newsletters),"papers remain in bag");
                        Reach(S.deliveryTray,new Vector3(-33.8f,0,79.3f),S.deliveryTray.transform.position+Vector3.up*.04f);
                        Keys(Key.F);Next(2);break;
                    case 2:
                        Keys();Need(S.PapersDelivered&&!I.HasCarried(InventoryItemKind.Newsletters),"E delivers newsletters directly from bag");
                        Need(S.deliveryTray.transform.Find("Delivered papers").gameObject.activeSelf,"delivery visibly fills tray");
                        Need(HudController.Instance.objectiveText.text.StartsWith("Belongings recovered: 0/5"),"recovery counter starts after delivery");
                        Need(GameObject.Find("Quick access tools")!=null,"quick-access bar is visible");
                        ScreenCapture.CaptureScreenshot(Folder+"delivery-complete.png");
                        Next(21);break;
                    case 21:
                        S.Deliver();Need(S.PapersDelivered,"repeated delivery is harmless");
                        I.Collect(I.keyItem);GameManager.Instance.officeMission.SetKeyLocation(true,false);
                        Need(I.Move(InventoryContainer.Satchel,I.Find(InventoryContainer.Satchel,InventoryItemKind.OfficeKey),InventoryContainer.Locker,0,out _),"key can be stored");
                        Need(!GameManager.Instance.officeMission.door.CanInteract(P),"stored key cannot unlock office remotely");
                        I.Move(InventoryContainer.Locker,0,InventoryContainer.Satchel,0,out _);
                        var door=GameManager.Instance.officeMission.door;Reach(door,new Vector3(-33.95f,0,82.11f),door.transform.position+Vector3.up*1.0f);Keys(Key.F);Next(3);break;
                    case 3:
                        Keys();Need(GameManager.Instance.officeMission.OfficeUnlocked&&!I.IsEquipped(InventoryItemKind.OfficeKey),"E unlocks office with key in bag");
                        S.PrepareChaseRetry();ComicDialogue.Cancel();
                        var phone=S.phonePickup;Reach(phone,phone.transform.TransformPoint(new Vector3(0,0,-1.2f)),phone.transform.position+Vector3.up*.2f);
                        phone.Interact(P);Need(R.Count==1&&!phone.phoneVisual.activeSelf,"phone collected from first matching box");
                        R.TakeTool(AccessToolPickup.Tool.BoltCutters);R.TakeTool(AccessToolPickup.Tool.StoreKey);
                        foreach(var gate in Object.FindObjectsByType<RunGate>(FindObjectsSortMode.None))if(gate.kind==RunGate.Kind.Chain)gate.Interact(P);
                        foreach(var storageDoor in Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None))if(storageDoor.runRequiredLevel>0)storageDoor.Interact(P);
                        Take(1);Take(2);Take(3);Need(R.Count==4,"four distinct room boxes recovered");Next(4);break;
                    case 4:
                        Need(V.FinaleStarted&&V.Level>=.21f,"VHS persists at 4/5 while caretaker is not chasing");
                        Need(V.ExitRed<.01f,"red exit effect waits for 5/5");
                        ScreenCapture.CaptureScreenshot(Folder+"four-of-five-vhs.png");
                        Next(41);break;
                    case 41:
                        R.PenalizeCatch();Need(Item(3).visual.activeSelf,"lost belonging returns to its box");Next(5);break;
                    case 5:
                        Need(V.FinaleStarted&&V.Level>=.21f,"fourth-item VHS stays latched after a lost belonging");
                        Take(3);Take(4);Need(R.ReadyToEscape,"all five belongings enable escape");
                        Warp(new Vector3(-33.8f,0,78),new Vector3(-33.8f,1.5f,72));Next(6);break;
                    case 6:
                        if(EditorApplication.timeSinceStartup-at<1.5)return;
                        foreach(var box in R.pickups)
                        {
                            var approach=box.transform.TransformPoint(new Vector3(0,0,-1.25f));approach.y=.05f;
                            var route=new NavMeshPath();
                            Need(NavMesh.SamplePosition(approach,out var end,.65f,NavMesh.AllAreas)&&NavMesh.CalculatePath(P.transform.position,end.position,NavMesh.AllAreas,route)&&route.status==NavMeshPathStatus.PathComplete,"open-school navigation reaches box "+box.itemId);
                        }
                        Need(!float.IsInfinity(V.ExitDistance),"exit distance follows a complete walkable route");farRed=V.ExitRed;
                        Warp(new Vector3(0,0,1.5f),Exit.transform.position+Vector3.up*1.3f);Next(7);break;
                    case 7:
                        if(EditorApplication.timeSinceStartup-at<2)return;
                        Need(V.ExitRed>farRed+.3f&&V.ExitDistance<8,"red wash grows as player approaches exit at 5/5");
                        ScreenCapture.CaptureScreenshot(Folder+"five-of-five-exit.png");
                        Next(8);break;
                    case 8:
                        P.GetComponent<ChaseCamera>().intensity=0;Next(81);break;
                    case 81:
                        Need(Shader.GetGlobalFloat("_VhsIntensity")==0&&Shader.GetGlobalFloat("_VhsExitRed")==0,"effects-off setting disables VHS and red wash");
                        P.GetComponent<ChaseCamera>().intensity=savedFeel;
                        Need(Object.FindFirstObjectByType<ThrowableBall>(FindObjectsInactive.Include)==null,"the football has been removed from the school");
                        Next(10);break;
                    case 10:
                        var bag=GameManager.Instance.lockerUI;bag.Open(P);
                        bag.Click(bag.Slots.First(s=>s.container==InventoryContainer.Satchel&&s.Entry!=null));
                        Need(bag.Source==null&&!bag.IsDragging,"bag click inspects rather than starting a move");
                        Need(!bag.Slots.First(s=>s.container==InventoryContainer.Use).gameObject.activeInHierarchy,"bag has no Use target");
                        ScreenCapture.CaptureScreenshot(Folder+"bag-inspection.png");Next(11);break;
                    case 11:
                        GameManager.Instance.lockerUI.Close();P.GetComponent<ClockworkDecoy>().Collect();
                        Warp(new Vector3(0,0,1.5f),new Vector3(0,1.5f,7));Keys(Key.Digit1);Next(12);break;
                    case 12:
                        Keys();Need(P.GetComponent<ClockworkDecoy>().Charges==0,"1 places a wind-up distraction from quick access");
                        Exit.Interact(P);Need(GameManager.Instance.Current==GameManager.State.Won,"main exit wins at 5/5");Next(13);break;
                    case 13:
                        Need(Shader.GetGlobalFloat("_VhsExitRed")<.01f,"red wash clears after winning");Need(errors==0,"no runtime errors");Finish(true);break;
                }
            }
            catch(Exception e){File.AppendAllText(Report,"STOP "+e+"\n");Finish(false);}
        }
        static void Finish(bool passed)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Application.runInBackground=background;
            if(Keyboard.current!=null)Keys();File.AppendAllText(Report,passed?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;
        }
    }
}

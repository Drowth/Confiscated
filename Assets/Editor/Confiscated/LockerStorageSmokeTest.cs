using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using Object=UnityEngine.Object;
namespace Confiscated.EditorTools
{
    [InitializeOnLoad]
    public static class LockerStorageSmokeTest
    {
        const string Marker="Temp/run_locker",Report="../Docs/SatchelUse_Validation.txt";
        static int step,lastFrame,errors;static double started,at;static bool pass,background;
        static Keyboard originalKeyboard,kb;static Mouse originalMouse,mouse;
        static InputSettings.BackgroundBehavior oldBackground;static InputSettings.EditorInputBehaviorInPlayMode oldFocus;
        static ThrowableBall ball;static InventoryEntry key;
        static GameManager G=>GameManager.Instance;
        static PlayerInteractor P=>Object.FindFirstObjectByType<PlayerInteractor>();
        static FirstPersonController F=>P.GetComponent<FirstPersonController>();
        static PlayerInventory I=>P.GetComponent<PlayerInventory>();
        static LockerStorageUI U=>G.lockerUI;
        static InventoryContainer Bag=>InventoryContainer.Satchel;
        static InventoryContainer Safe=>InventoryContainer.Locker;
        static InventoryContainer Use=>InventoryContainer.Use;
        static LockerStorageSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Start();}};}
        [MenuItem("Confiscated/Play Test/Arm Satchel Equipment Test (runs on next Play)")]
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Start()
        {
            step=errors=0;lastFrame=-1;pass=true;started=at=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;
            oldBackground=InputSystem.settings.backgroundBehavior;oldFocus=InputSystem.settings.editorInputBehaviorInPlayMode;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            originalKeyboard=Keyboard.current;originalMouse=Mouse.current;if(originalKeyboard!=null)InputSystem.DisableDevice(originalKeyboard);if(originalMouse!=null)InputSystem.DisableDevice(originalMouse);kb=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
            File.WriteAllText(Report,"Satchel, cursor items, Use slot and world interactions\n");Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        }
        static void Log(string m,string trace,LogType t){if(t==LogType.Error||t==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+m+"\n");}}
        static void Check(bool ok,string message){pass&=ok;File.AppendAllText(Report,(ok?"ok   ":"FAIL ")+message+"\n");}
        static void Need(bool ok,string message){Check(ok,message);if(!ok)throw new Exception(message);}
        static void Next(int s){step=s;at=EditorApplication.timeSinceStartup;}
        static void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(kb,new KeyboardState(keys));
        static InventorySlotView Slot(InventoryContainer c,int n)=>U.Slots.First(s=>s.container==c&&s.index==n);
        static Vector2 Position(InventoryContainer c,int n)=>RectTransformUtility.WorldToScreenPoint(null,Slot(c,n).transform.position);
        static void MouseAt(Vector2 point,bool down)=>InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left,down));
        static void Move(InventoryContainer a,int x,InventoryContainer b,int y)
        {
            var source=Slot(a,x);var target=Slot(b,y);U.BeginDrag(source,Position(a,x));U.DropOn(target);U.EndDrag();
        }
        static void Warp(Vector3 p,Quaternion r){F.Controller.enabled=false;P.transform.SetPositionAndRotation(p,r);F.Controller.enabled=true;F.ResetLook();Physics.SyncTransforms();}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish("interrupted");return;}if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            double now=EditorApplication.timeSinceStartup;if(now-at<.23)return;
            try
            {
                if(now-started>75)throw new Exception("Timeout at "+step);
                switch(step)
                {
                    case 0:
                        G.officeMission.caretaker.GetComponent<NavMeshAgent>().Warp(SchoolPlan.Point(650,270));G.officeMission.caretaker.Freeze();G.officeMission.key.Interact(P);
                        key=I.Get(Bag,0);Need(key!=null&&I.Equipped==null,"pickup puts office key in satchel, not in hand");
                        Check(!G.officeMission.door.CanInteract(P)&&!G.officeMission.Unlock(),"key possession alone cannot unlock the office");
                        Warp(G.officeMission.door.transform.position-G.officeMission.door.transform.forward*1.3f,Quaternion.LookRotation(G.officeMission.door.transform.forward));
                        Keys(Key.I);Next(101);break;
                    case 101:
                        Keys();Need(!U.IsOpen,"I no longer opens the satchel");Next(102);break;
                    case 102:
                        Keys(Key.Tab);Next(1);break;
                    case 1:
                        Keys();Need(U.IsOpen&&!U.HasLockerAccess&&F.LookLocked&&P.InputLocked,"Tab opens satchel anywhere and locks world controls");
                        Check(U.Slots.Where(s=>s.container==Safe).All(s=>!s.gameObject.activeInHierarchy),"remote satchel hides locker contents");
                        MouseAt(Position(Bag,0),false);Next(2);break;
                    case 2:MouseAt(Position(Bag,0),true);Next(3);break;
                    case 3:MouseAt(Position(Bag,0),false);Next(4);break;
                    case 4:
                        Need(U.Source==Slot(Bag,0)&&!U.IsDragging,"single mouse click picks up item without holding the button");
                        MouseAt(Position(Use,0),false);Next(5);break;
                    case 5:
                        var ghost=U.GetComponentsInChildren<UnityEngine.UI.Image>().First(i=>i.name=="Dragged item");
                        Check(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null,ghost.transform.position),Position(Use,0))<3,"picked-up icon follows mouse cursor");
                        MouseAt(Position(Use,0),true);Next(6);break;
                    case 6:MouseAt(Position(Use,0),false);Next(7);break;
                    case 7:
                        Need(I.Equipped==key&&I.Get(Bag,0)==null&&U.Source==null,"second mouse click places key in Use");
                        ScreenCapture.CaptureScreenshot("../Docs/Satchel_Use.png");Next(8);break;
                    case 8:Keys(Key.Tab);Next(9);break;
                    case 9:
                        Keys();Need(!U.IsOpen&&!F.LookLocked&&!P.InputLocked&&I.Equipped==key,"Tab closes satchel and retains equipped item");
                        Check(GameObject.Find("Equipped office key")!=null,"equipped key is visibly held at player hand");
                        ScreenCapture.CaptureScreenshot("../Docs/Satchel_HeldKey.png");Keys(Key.F);Next(10);break;
                    case 10:
                        Keys();Need(G.officeMission.door.IsUnlocked&&G.officeMission.OfficeUnlocked,"E uses equipped key on office door");
                        G.detention.phonePickup.Interact(P);ball=Object.FindFirstObjectByType<ThrowableBall>();ball.Interact(P);
                        Check(P.HeldBall==null&&!ball.gameObject.activeSelf&&I.Contains(Bag,InventoryItemKind.Football),"football pickup remains packed until equipped");
                        Keys(Key.Tab);Next(11);break;
                    case 11:
                        Keys();Move(Bag,I.Find(Bag,InventoryItemKind.Phone),Use,0);
                        Need(I.IsEquipped(InventoryItemKind.Phone)&&I.HasCarried(InventoryItemKind.OfficeKey),"phone swaps with equipped key without losing either item");
                        Keys(Key.Tab);Next(12);break;
                    case 12:
                        Keys();Check(HudController.Instance.phoneImage.gameObject.activeSelf,"equipped phone displays held artwork");
                        Keys(Key.Tab);Next(13);break;
                    case 13:
                        Keys();Move(Bag,I.Find(Bag,InventoryItemKind.Football),Use,0);
                        Check(I.Equipped.ball==ball&&P.HeldBall==ball&&ball.transform.parent==P.HoldAnchor,"Use equips the physical football at the hand");
                        Keys(Key.Tab);Next(14);break;
                    case 14:
                        Keys();Check(!HudController.Instance.phoneImage.gameObject.activeSelf&&P.HasPhone,"packed phone hides hand artwork while remaining carried contraband");
                        MouseAt(Vector2.zero,true);Next(15);break;
                    case 15:
                        MouseAt(Vector2.zero,false);Need(P.HeldBall==null&&I.Equipped==null&&ball.gameObject.activeSelf,"left click throws only equipped football and clears Use");
                        var locker=Object.FindFirstObjectByType<PlayerLocker>();Warp(locker.transform.position-locker.transform.forward*1.6f,Quaternion.LookRotation(locker.transform.forward));locker.Interact(P);Next(16);break;
                    case 16:
                        Need(U.HasLockerAccess,"E on player locker exposes safe storage as well as Use");
                        Move(Bag,I.Find(Bag,InventoryItemKind.Phone),Safe,0);Move(Bag,I.Find(Bag,InventoryItemKind.OfficeKey),Use,0);
                        Check(G.PhoneStored&&!P.HasPhone&&I.IsEquipped(InventoryItemKind.OfficeKey),"locker stores phone while another item remains equipped");
                        U.Close();U.Open(P);Next(17);break;
                    case 17:
                        U.BeginDrag(Slot(Use,0),Position(Use,0));U.DropOn(Slot(Safe,1));
                        Check(I.Equipped==key&&I.Get(Safe,1)==null,"remote satchel cannot move items into inaccessible locker slots");
                        U.Close();U.Open(P);Next(18);break;
                    case 18:
                        U.Click(Slot(Use,0));G.Caught();
                        Check(!U.IsOpen&&U.Source==null&&G.detention.Active&&G.PhoneStored&&I.Equipped==key,"catch cancels cursor item safely and stored phone survives");
                        G.detention.blackboard.Open(P);Keys(Key.Tab);Next(19);break;
                    case 19:
                        Keys();Check(!U.IsOpen&&G.detention.blackboard.IsOpen,"Tab cannot interrupt blackboard text input");
                        DetentionSmokeTest.CompleteForRegression(G.detention);Need(G.IsPlaying,"new detention task still releases player with inventory intact");
                        Keys(Key.Tab);Next(20);break;
                    case 20:
                        Keys();U.Close();
                        for(int n=0;n<6;n++)if(I.Get(Bag,n)==null){var item=ScriptableObject.CreateInstance<InventoryItemDefinition>();item.kind=InventoryItemKind.Other;item.displayName="Capacity "+n;I.Collect(item);}
                        var before=Enumerable.Range(0,6).Select(n=>I.Get(Bag,n)).Append(I.Equipped).ToArray();
                        I.Move(Use,0,Bag,2,out _);var after=Enumerable.Range(0,6).Select(n=>I.Get(Bag,n)).Append(I.Equipped).ToArray();
                        Check(before.All(e=>after.Contains(e))&&after.Distinct().Count()==7,"full bag and Use swap conserves all seven carried entries");
                        var extra=ScriptableObject.CreateInstance<InventoryItemDefinition>();Check(!I.Collect(extra),"full satchel rejects additional pickup without losing items");
                        G.Restart();Next(21);break;
                    case 21:
                        if(now-at<1)return;Check(G.IsPlaying&&!U.IsOpen&&I.Equipped==null&&!I.HasCarried(InventoryItemKind.OfficeKey),"restart resets satchel, Use and mission");Check(errors==0,"zero runtime errors ("+errors+")");Finish("complete");break;
                }
            }
            catch(Exception e){Check(false,e.Message);Finish("exception at "+step);}
        }
        static void Finish(string why)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Application.runInBackground=background;InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldFocus;
            if(kb!=null)InputSystem.RemoveDevice(kb);if(mouse!=null)InputSystem.RemoveDevice(mouse);if(originalKeyboard!=null)InputSystem.EnableDevice(originalKeyboard);if(originalMouse!=null)InputSystem.EnableDevice(originalMouse);
            File.AppendAllText(Report,(pass&&why=="complete"?"PASS ":"FAIL ")+why+"\nDONE\n");if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
        }
    }
}

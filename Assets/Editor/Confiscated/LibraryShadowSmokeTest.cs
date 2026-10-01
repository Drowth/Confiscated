using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// The library shadow (Docs/LibraryMaze.md), in the real scene: still and dark is safe; moving or a torch gets you
    /// noticed, hunted and thrown out of the nearest door with everything you carry; freezing through the shush lets it
    /// pass; the day torch battery drains, dies and recharges; the library darkness fades in by the doors.
    /// Player movement is real CharacterController movement; the shadow runs under its own AI throughout.
    /// </summary>
    [InitializeOnLoad]
    public static class LibraryShadowSmokeTest
    {
        const string Marker="Temp/library_shadow_test",Report="../Docs/LibraryShadow_Validation.txt";
        static int stage,lastFrame;static bool jumpscare;static double at,started;static bool background;
        static Vector3 stand,shadowSpot,walk;
        static SchoolRunController R=>SchoolRunController.Instance;
        static PlayerInteractor P=>R.period.Player;
        static FirstPersonController F=>P.GetComponent<FirstPersonController>();
        static PlayerTorch T=>P.GetComponent<PlayerTorch>();
        static PlayerInventory Bag=>P.GetComponent<PlayerInventory>();
        static LibraryShadow Shade=>Object.FindFirstObjectByType<LibraryShadow>();
        static LibraryShadowSmokeTest()
        {
            EditorApplication.playModeStateChanged+=s=>
            {
                if(s!=PlayModeStateChange.EnteredPlayMode||!File.Exists(Marker))return;
                File.Delete(Marker);stage=0;lastFrame=-1;LibraryShadow.ResetSession();started=at=EditorApplication.timeSinceStartup;
                File.WriteAllText(Report,"Library shadow: real scene, real player movement, the shadow under its own AI; checkpoint warps for setup.\n");
                background=Application.runInBackground;Application.runInBackground=true;EditorApplication.update+=Tick;
            };
        }
        [MenuItem("Confiscated/Library/Arm Shadow Test")]
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Check(bool condition,string label){if(!condition)throw new Exception(label);File.AppendAllText(Report,"ok "+label+"\n");}
        static void Next(){stage++;at=EditorApplication.timeSinceStartup;}
        static void Warp(Vector3 p){F.Controller.enabled=false;P.transform.position=p;F.Controller.enabled=true;Physics.SyncTransforms();}
        static bool InLibrary(Vector3 p)=>LibrarySetup.Interior.Contains(new Vector3(p.x,1.5f,p.z));
        /// <summary>A shadow spot and a player spot 3.5 m apart down a clear aisle of the maze.</summary>
        static void Aisle(int skip)
        {
            int found=0;
            foreach(var a in Shade.alcoves)foreach(var d in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right})
            {
                Vector3 from=a.position,to=from+d*3.5f;
                if(NavMesh.Raycast(from,to,out _,Shade.areaMask))continue;
                if(Physics.Linecast(from+Vector3.up*1.5f,to+Vector3.up*1.5f,~(1<<2),QueryTriggerInteraction.Ignore))continue;
                if(found++<skip)continue;
                shadowSpot=from;stand=to;walk=d;return;
            }
            throw new Exception("no clear 3.5 m aisle next to any alcove");
        }
        static void PlaceShadow(){Shade.transform.position=shadowSpot;}
        static void Move(Vector3 direction,float speed){F.Controller.Move(direction.normalized*speed*Time.deltaTime);}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish(false);return;}
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            double elapsed=EditorApplication.timeSinceStartup-at;
            try
            {
                if(EditorApplication.timeSinceStartup-started>90)throw new Exception("test timed out at stage "+stage);
                switch(stage)
                {
                    case 0:
                        if(elapsed<.8)return;
                        if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();}
                        ComicDialogue.Cancel();R.period.PrepareChaseRetry();R.PrepareChaseRetry();R.PauseStaff();R.caretaker.Freeze();
                        Check(Shade!=null&&Shade.GetComponentsInChildren<Renderer>().Length>=5,"the shadow is installed, with its figure and eyes");
                        Check(Shade.smoke!=null&&Shade.eyes.Length==4,"it trails smoke and has two eyes, each with a glow");
                        Check(Shade.alcoves.Length>=8&&Shade.alcoves.All(a=>InLibrary(a.position)&&NavMesh.SamplePosition(a.position,out _,.3f,Shade.areaMask)),"8+ alcoves, all on the library floor ("+Shade.alcoves.Length+")");
                        Check(Shade.entrances.Length==3&&Shade.exits.Length==3&&Shade.entrances.All(e=>InLibrary(e.position))&&Shade.exits.All(e=>!InLibrary(e.position)),"three doors: a point inside and a point outside each");
                        Check(Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Count(t=>t.text==LibrarySetup.NoticeText)==3,"a SILENT STUDY notice beside each library door");
                        Check(Object.FindFirstObjectByType<LibraryDarkness>()!=null,"the library darkness is installed");
                        Check(new[]{"LibraryShadowISeeYou","LibraryShadowSpotted","LibraryShadowScream","LibraryShadowBreathLoop","LibraryMusicLoop"}.All(n=>Resources.Load<AudioClip>("Audio/"+n)!=null),"its sounds and the library music load");
                        jumpscare=false;
                        // Bookcases stay inside the room (their corner overlap used to poke through the outer walls).
                        Check(Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="Bookcase").All(r=>r.bounds.min.x>=LibrarySetup.Interior.min.x-.01f&&r.bounds.max.x<=LibrarySetup.Interior.max.x+.01f&&r.bounds.min.z>=LibrarySetup.Interior.min.z-.01f&&r.bounds.max.z<=LibrarySetup.Interior.max.z+.01f),"no bookcase pokes out through the library walls");
                        // Windows: the caretaker sees in only where a window's light falls.
                        var windows=LibraryWindow.All;
                        Check(windows.Count==LibrarySetup.WindowSpots.Length,"six windows in the library's corridor walls ("+windows.Count+")");
                        var glass=windows[0].transform.Find("Glass").GetComponent<Collider>();
                        var mask=new SerializedObject(R.caretaker).FindProperty("sightBlockers").intValue;
                        Check(glass!=null&&glass.gameObject.layer==2&&(mask&(1<<2))==0,"window glass stops the player but not the caretaker's sight");
                        Check(!LibraryWindow.HiddenInLibrary(windows[0].pool)&&LibraryWindow.HiddenInLibrary(LibrarySetup.Interior.center)&&!LibraryWindow.HiddenInLibrary(Shade.exits[0].position),"in the library only a window's light pool is visible from outside");
                        {
                            // Real sight: the caretaker outside a window, the player in its light, then in the dark beside it.
                            var w=windows[0];Vector3 outward=(w.transform.position-w.pool);outward.y=0;outward.Normalize();
                            var see=typeof(CaretakerAI).GetMethod("CanSeePlayer",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                            R.caretaker.transform.SetPositionAndRotation(w.transform.position+outward*2.5f,Quaternion.LookRotation(-outward));
                            Warp(w.pool);Physics.SyncTransforms();bool lit=(bool)see.Invoke(R.caretaker,null);
                            Vector3 dark=w.pool-outward*3.2f;Warp(dark);Physics.SyncTransforms();bool unseen=!(bool)see.Invoke(R.caretaker,null);
                            Check(lit&&unseen,"through the window he sees you in its light, not in the dark behind it (lit "+lit+", hidden "+unseen+")");
                        }
                        Check(Bag.Contains(InventoryContainer.Locker,InventoryItemKind.Torch)&&PlayerTorch.Limited,"day play: the torch waits in the locker, on a battery");
                        // Carry something, to check it survives being thrown out.
                        int slot=0;while(slot<8&&!Bag.Move(InventoryContainer.Locker,Bag.Find(InventoryContainer.Locker,InventoryItemKind.Torch),InventoryContainer.Satchel,slot,out _))slot++;
                        Check(T.HasTorch&&!T.IsOn,"torch moved into the satchel; it starts off by day");
                        // Carrying the handheld game (the library's own belonging) when caught puts it back in its box.
                        R.Recover(LibraryShadow.LibraryItem);Check(R.Has(LibraryShadow.LibraryItem),"handheld game taken from the returns desk");
                        Aisle(0);Warp(stand);PlaceShadow();Shade.Paused=false;Next();break;
                    case 1:
                        // Still, torch off, in plain sight 3.5 m away.
                        if(elapsed<3)return;
                        Check(Object.FindFirstObjectByType<LibraryDarkness>().GetComponent<UnityEngine.Rendering.Volume>().weight>.5f,"inside the library it is dark");
                        Check(LibraryWindow.All[0].lamp.intensity>20,"inside, the window light is lifted to cut through the darkness ("+LibraryWindow.All[0].lamp.intensity.ToString("F0")+")");
                        Check(Shade.Notices==0&&Shade.Catches==0&&Shade.Current!=LibraryShadow.Phase.Hunt,"standing still in the dark, it ignores you ("+Shade.Current+")");
                        Check(LibraryShadow.Shushed&&Resources.Load<AudioClip>("Audio/LibraryShush")!=null,"the library shushes you the first time you walk in");
                        Aisle(0);Warp(stand);PlaceShadow();Next();break;
                    case 2:
                        // Walk about in front of it: noticed, then hunted, then caught.
                        if(Shade.Catching&&Vector3.Distance(Shade.transform.position+Vector3.up*1.95f,P.ViewCamera.transform.position)<1)jumpscare=true;
                        if(Shade.Catches==0){Move(elapsed%1.2<.6?-walk:walk,2.2f);if(elapsed>12)throw new Exception("moving in front of it never got caught: "+Shade.Current);return;}
                        Check(Shade.Notices>=1,"moving in front of it gets you noticed (\"I see you\")");
                        Check(jumpscare,"the catch is a jumpscare: it is right in your face, screaming, before you are thrown out");
                        Check(!InLibrary(P.transform.position)&&Shade.exits.Any(x=>Vector3.Distance(x.position,P.transform.position)<.5f),"caught: thrown out of a library door");
                        var handheld=Object.FindObjectsByType<RunPickup>(FindObjectsSortMode.None).First(p=>p.itemId==LibraryShadow.LibraryItem);
                        Check(!R.Has(LibraryShadow.LibraryItem)&&handheld.visual.activeSelf&&Shade.ReturnedItems==1,"the handheld game goes back in its CONFISCATED box");
                        Check(Bag.HasCarried(InventoryItemKind.Torch),"everything else carried is kept");
                        Check(R.caretaker.Current==CaretakerAI.State.Frozen,"the caretaker is not told");
                        Next();break;
                    case 3:
                        if(elapsed<3)return; // it is gone for a moment after a catch
                        Check(Object.FindFirstObjectByType<LibraryDarkness>().GetComponent<UnityEngine.Rendering.Volume>().weight<.05f,"outside the library the darkness is gone");
                        Check(LibraryWindow.All[0].lamp.intensity<8,"from the corridor the window light is at its plain brightness ("+LibraryWindow.All[0].lamp.intensity.ToString("F1")+")");
                        {
                            // Its wandering reaches the windows (forced here: every wander goes to a window).
                            float chance=Shade.windowChance;Shade.windowChance=1;int before=Shade.WindowVisits;
                            typeof(LibraryShadow).GetMethod("Wander",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(Shade,null);
                            Check(Shade.GoingToWindow&&Shade.WindowVisits==before+1,"it sometimes wanders to a window's light, where it can be seen from the corridor");
                            Shade.windowChance=0;typeof(LibraryShadow).GetMethod("Wander",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(Shade,null);Shade.windowChance=chance;
                        }
                        Aisle(1);Warp(stand);PlaceShadow();Next();break;
                    case 4:
                        // Move until it shushes, then freeze: it passes by.
                        if(Shade.Current!=LibraryShadow.Phase.Notice&&Shade.PassedBy==0){Move(elapsed%1.2<.6?-walk:walk,2.2f);if(elapsed>10)throw new Exception("never noticed: "+Shade.Current);return;}
                        if(Shade.PassedBy==0){if(Shade.Current==LibraryShadow.Phase.Hunt)throw new Exception("froze during the shush but it hunted anyway");return;}
                        Check(Shade.Catches==1&&Shade.Current!=LibraryShadow.Phase.Hunt,"freezing through the shush: it passes you by");
                        Aisle(0);Warp(stand);PlaceShadow();Next();break;
                    case 5:
                        // Still, but deliberately holding the beam on the shadow: the light gives you away.
                        F.LookLocked=true;P.ViewCamera.transform.LookAt(Shade.transform.position+Vector3.up*1.45f);
                        if(elapsed<.3)return;
                        if(!T.IsOn){Check(T.Toggle()&&true,"T lights the torch by day");return;}
                        if(Shade.Catches<2){if(elapsed>12)throw new Exception("a lit torch never got caught: "+Shade.Current);return;}
                        Check(Shade.Catches==2,"standing still with the torch aimed at it: noticed, hunted, caught");
                        Next();break;
                    case 6:
                        // Battery: drains while on, dies, cannot relight until 25 %, then can.
                        if(!T.IsOn)T.Toggle();
                        T.SetBattery(.5f);Next();break;
                    case 7:
                        if(elapsed<1)return;
                        Check(T.Battery<.5f-.03f&&T.Battery>.5f-.09f,"the battery drains while lit ("+T.Battery.ToString("F3")+" after 1 s)");
                        T.SetBattery(.005f);Next();break;
                    case 8:
                        if(elapsed<.4)return;
                        Check(!T.IsOn&&T.Flat,"at empty the torch dies");
                        Check(!T.Toggle()&&!T.IsOn,"a flat torch will not relight");
                        T.SetBattery(.24f);Next();break;
                    case 9:
                        if(elapsed<1.2)return;
                        Check(!T.Flat&&T.Battery>=PlayerTorch.RestartFraction,"it recharges while off ("+T.Battery.ToString("F3")+")");
                        Check(T.Toggle(),"recharged past 25 %, it lights again");Next();break;
                    case 10:
                        if(elapsed<.2)return;
                        Check(T.IsOn,"the relit torch is shining");T.Toggle();
                        // A look at it in the dark, eyes and smoke, for the record.
                        Aisle(0);Warp(stand);Shade.Paused=true;Shade.transform.position=shadowSpot;Shade.transform.rotation=Quaternion.LookRotation(-walk);
                        F.LookLocked=true;P.ViewCamera.transform.LookAt(shadowSpot+Vector3.up*1.6f);Next();break;
                    case 11:
                        if(elapsed<2)return;
                        ScreenCapture.CaptureScreenshot("../Docs/LibraryShadow_Dark.png");Next();break;
                    case 12:
                        if(elapsed<.5)return;
                        T.Toggle();Next();break;
                    case 13:
                        if(elapsed<.8)return;
                        ScreenCapture.CaptureScreenshot("../Docs/LibraryShadow_Torch.png");Next();break;
                    case 14:
                        if(elapsed<.5)return;
                        Finish(true);break;
                }
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL stage "+stage+": "+e.Message+"\n");Finish(false);}
        }
        static void Finish(bool pass)
        {
            EditorApplication.update-=Tick;
            if(File.Exists(Report))File.AppendAllText(Report,pass?"PASS\n":"FAIL\n");
            Application.runInBackground=background;
            if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
        }
    }
}

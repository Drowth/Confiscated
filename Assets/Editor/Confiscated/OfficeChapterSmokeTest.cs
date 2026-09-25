using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Walks the real collision geometry and follows the caretaker's actual departure; no teleporting past locks.</summary>
    [InitializeOnLoad]
    public static class OfficeChapterSmokeTest
    {
        const string Marker="Temp/run_office_chapter";
        public const string ResultPath="Temp/office_chapter_result.txt";
        static int step, errors;
        static double began, stepAt, lastTick;
        static int frame;
        static bool pass, staffDoorOpened;
        static bool background;
        static Vector3[] route;
        static int waypoint;
        static Action arrived;
        static OfficeMission Mission=>Object.FindFirstObjectByType<OfficeMission>();
        static PlayerInteractor Player=>Object.FindFirstObjectByType<PlayerInteractor>();
        static GameManager GM=>GameManager.Instance;

        static OfficeChapterSmokeTest()
        {
            EditorApplication.playModeStateChanged+=s=>
            {
                if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker))
                {File.Delete(Marker);StartRun();}
            };
        }

        [MenuItem("Confiscated/Play Test/Arm Office Chapter Test (runs on next Play)")]
        public static void Arm(){File.WriteAllText(Marker,"armed");}

        public static void StartRun()
        {
            File.WriteAllText(ResultPath, "Office chapter integration test\n");
            step=0;errors=0;pass=true;staffDoorOpened=false;frame=-1;route=null;
            began=stepAt=lastTick=EditorApplication.timeSinceStartup;
            background=Application.runInBackground;Application.runInBackground=true;
            Application.logMessageReceived+=OnLog;
            EditorApplication.update+=Tick;
        }

        static void Check(bool ok,string text)
        {
            File.AppendAllText(ResultPath,(ok?"ok   ":"FAIL ")+text+"\n");
            if(!ok)pass=false;
        }
        static void OnLog(string message,string stack,LogType type)
        {
            if(type==LogType.Error||type==LogType.Exception){errors++;File.AppendAllText(ResultPath,"ERROR "+message+"\n");}
        }
        static void Next(int value){step=value;stepAt=EditorApplication.timeSinceStartup;}
        static void Walk(Vector3 destination,Action onArrival)
        {
            var path=new NavMeshPath();
            bool found=NavMesh.CalculatePath(Player.transform.position,destination,NavMesh.AllAreas,path);
            if(!found||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("No walkable path to "+destination);
            route=path.corners;waypoint=0;arrived=onArrival;
        }
        static void Aim(Vector3 at){Player.ViewCamera.transform.LookAt(at);}
        static bool CanSee<T>(Vector3 point) where T:Component
        {
            Aim(point);
            var camera=Player.ViewCamera;
            return Physics.Raycast(camera.transform.position,camera.transform.forward,out var hit,2.6f,~((1<<LayerMask.NameToLayer("Player"))|(1<<LayerMask.NameToLayer("Enemy"))),QueryTriggerInteraction.Collide)
                &&hit.collider.GetComponentInParent<T>()!=null;
        }

        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish("Play mode stopped");return;}
            if(frame==Time.frameCount)return;frame=Time.frameCount;
            // Editor callbacks run less often than uncapped gameplay frames. Use callback
            // elapsed time, otherwise the test player crawls while the real AI runs normally.
            float movementDelta=Mathf.Min(.1f,(float)(EditorApplication.timeSinceStartup-lastTick));
            lastTick=EditorApplication.timeSinceStartup;
            try
            {
                if(EditorApplication.timeSinceStartup-began>90)throw new Exception("Timed out at step "+step+" player="+Player.transform.position+" caretaker="+Mission.caretaker.transform.position);
                if(route!=null)
                {
                    Vector3 delta=route[waypoint]-Player.transform.position;delta.y=0;
                    if(delta.magnitude<.16f)
                    {
                        waypoint++;
                        if(waypoint==route.Length){route=null;var callback=arrived;callback();return;}
                        delta=route[waypoint]-Player.transform.position;delta.y=0;
                    }
                    Player.GetComponent<CharacterController>().Move(Vector3.ClampMagnitude(delta,3.4f*movementDelta)+Vector3.down*.06f);
                    return;
                }
                switch(step)
                {
                    case 0:
                        if(EditorApplication.timeSinceStartup-stepAt<.6)return;
                        Check(GM.IsPlaying&&Mission!=null,"chapter starts playing");
                        Player.GetComponent<FirstPersonController>().enabled=false;
                        Player.InputLocked=true;
                        Check(!Mission.key.CanInteract(Player),"key cannot be taken while caretaker is in the office");
                        Check(!Mission.door.CanInteract(Player),"office stays locked without key");
                        Check(!Object.FindFirstObjectByType<PhonePickup>().CanInteract(Player),"phone cannot bypass the key and lock sequence");
                        Check(!Object.FindFirstObjectByType<ClassroomSeat>().CanInteract(Player),"cannot finish at desk without phone");
                        Check(!Object.FindFirstObjectByType<EscapeWindow>().CanInteract(Player),"window is not this chapter's exit");
                        var ball=Object.FindFirstObjectByType<ThrowableBall>();
                        Check(ball.transform.position.z<2,"playable football is stored in office");
                        Next(1);break;
                    case 1:
                        staffDoorOpened|=Mission.door.IsOpen&&!Mission.door.IsUnlocked;
                        if(Mission.caretaker.transform.position.z<14f)return;
                        Check(staffDoorOpened,"caretaker opens locked office door for his rounds");
                        Check(Mission.CaretakerAway,"actual patrol creates key opportunity");
                        GameObject.Find("Architecture/Doors/Door_Year6_West").GetComponent<OfficeDoor>().Interact(Player);
                        Next(2);break;
                    case 2:
                        if(EditorApplication.timeSinceStartup-stepAt<.5)return;
                        Walk(new Vector3(-.10f,0,3.55f),()=>Next(3));break;
                    case 3:
                        Check(CanSee<OfficeKeyPickup>(Mission.key.transform.position),"spare key reachable by interaction ray");
                        Mission.key.Interact(Player);
                        Check(Mission.HasKey&&!Mission.key.gameObject.activeSelf,"key collected and visual removed");
                        Walk(new Vector3(.44f,0,3.23f),()=>Next(4));break;
                    case 4:
                        Check(CanSee<OfficeDoor>(Mission.door.transform.position+Vector3.up*1.1f),"office door reachable by interaction ray");
                        Mission.door.Interact(Player);
                        Check(Mission.OfficeUnlocked&&Mission.door.IsUnlocked,"key unlocks the office");
                        Next(5);break;
                    case 5:
                        if(EditorApplication.timeSinceStartup-stepAt<.6)return;
                        Walk(new Vector3(-1.9f,0,.08f),()=>Next(6));break;
                    case 6:
                        var phone=Object.FindFirstObjectByType<PhonePickup>();
                        Check(CanSee<PhonePickup>(phone.transform.position+Vector3.up*.2f),"phone box reachable through furnished room");
                        phone.Interact(Player);
                        Check(Player.HasPhone&&Mission.Current==OfficeMission.Stage.ReturnToClass,"phone advances objective to Year 6");
                        Object.FindFirstObjectByType<EscapeWindow>().Interact(Player);
                        Check(GM.IsPlaying,"window cannot prematurely win after phone pickup");
                        Walk(new Vector3(-2.7f,0,7.35f),()=>Next(7));break;
                    case 7:
                        var seat=Object.FindFirstObjectByType<ClassroomSeat>();
                        Check(GM.IsPlaying,"return journey completes before capture");
                        Check(CanSee<ClassroomSeat>(seat.transform.position+Vector3.up*.73f),"own desk reachable by interaction ray");
                        seat.Interact(Player);
                        Check(GM.Current==GameManager.State.Won&&Mission.ReadyToFinish,"sitting down completes office chapter");
                        Check(Player.GetComponent<PhoneRinger>().SecondsToRing<0&&!Player.GetComponent<PhoneRinger>().IsRinging,"phone silenced on return to class");
                        Next(8);break;
                    case 8:
                        if(EditorApplication.timeSinceStartup-stepAt<.3)return;
                        GM.Restart();Next(9);break;
                    case 9:
                        if(EditorApplication.timeSinceStartup-stepAt<1)return;
                        Check(GM.IsPlaying&&!Mission.HasKey&&!Mission.OfficeUnlocked&&!Player.HasPhone,"restart clears chapter inventory and progress");
                        Check(Mission.key.gameObject.activeSelf&&!Mission.door.IsUnlocked,"restart restores key and locked door");
                        GM.Caught();Check(GM.Current==GameManager.State.Caught,"capture still ends round");
                        GM.Restart();Next(10);break;
                    case 10:
                        if(EditorApplication.timeSinceStartup-stepAt<1)return;
                        Check(GM.IsPlaying,"restart after capture works");
                        Finish("complete");break;
                }
            }
            catch(Exception e){Check(false,e.Message);Finish("exception");}
        }
        static void Finish(string reason)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            Check(errors==0,"no runtime errors ("+errors+")");
            File.AppendAllText(ResultPath,(pass?"PASS ":"FAIL ")+reason+"\nDONE\n");
            Application.runInBackground=background;
            EditorApplication.isPlaying=false;
        }
    }
}

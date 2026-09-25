using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    [InitializeOnLoad]
    public static class SchoolLayoutSmokeTest
    {
        const string Marker="Temp/run_school_layout";
        public const string ResultPath="Temp/school_layout_result.txt";
        static OfficeDoor[] doors;
        static int index,step,errors,lastFrame;
        static double started,stageAt,lastTick;
        static Vector3 destination;
        static bool pass,previousBackground;
        static PlayerInteractor Player=>Object.FindFirstObjectByType<PlayerInteractor>();
        static OfficeMission Mission=>Object.FindFirstObjectByType<OfficeMission>();

        static SchoolLayoutSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);StartRun();}};}
        [MenuItem("Confiscated/Play Test/Arm Full School Test (runs on next Play)")]
        public static void Arm(){File.WriteAllText(Marker,"armed");}
        public static void StartRun()
        {
            File.WriteAllText(ResultPath,"Full school layout test\n");pass=true;errors=0;step=0;index=0;lastFrame=-1;
            started=stageAt=lastTick=EditorApplication.timeSinceStartup;previousBackground=Application.runInBackground;
            Application.runInBackground=true;Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        }
        static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception){errors++;File.AppendAllText(ResultPath,"ERROR "+message+"\n");}}
        static void Check(bool value,string message){File.AppendAllText(ResultPath,(value?"ok   ":"FAIL ")+message+"\n");if(!value)pass=false;}
        static void Teleport(Vector3 p)
        {
            var cc=Player.GetComponent<CharacterController>();cc.enabled=false;Player.transform.position=new Vector3(p.x,.05f,p.z);cc.enabled=true;
        }
        static bool RayTo<T>(Vector3 p) where T:Component
        {
            var cam=Player.ViewCamera;cam.transform.LookAt(p);
            return Physics.Raycast(cam.transform.position,cam.transform.forward,out var hit,2.6f,~((1<<LayerMask.NameToLayer("Player"))|(1<<LayerMask.NameToLayer("Enemy"))),QueryTriggerInteraction.Collide)&&hit.collider.GetComponentInParent<T>()!=null;
        }
        static void Next(int value){step=value;stageAt=EditorApplication.timeSinceStartup;}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish("stopped");return;}
            if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
            double now=EditorApplication.timeSinceStartup;float dt=Mathf.Min(.08f,(float)(now-lastTick));lastTick=now;
            try
            {
                if(now-started>115)throw new Exception("Test timed out");
                switch(step)
                {
                    case 0:
                        if(now-stageAt<.5)return;
                        Player.GetComponent<FirstPersonController>().enabled=false;Player.InputLocked=true;
                        Check(!Mission.door.CanInteract(Player),"office locked at start");
                        Check(!Mission.key.CanInteract(Player),"key waits for caretaker departure");
                        Check(!Object.FindFirstObjectByType<PhonePickup>().CanInteract(Player),"phone requires unlocking");
                        Check(Mission.caretaker.patrol.Count==9&&Mission.caretaker.patrol.All(p=>p.point!=null),"all nine patrol references survive scene loading");
                        var ai=Mission.caretaker;ai.GetComponent<NavMeshAgent>().Warp(SchoolPlan.Point(892,800));ai.Freeze();
                        Teleport(Mission.key.transform.position+Vector3.right*1.2f);
                        Check(RayTo<OfficeKeyPickup>(Mission.key.transform.position),"relocated key is reachable by player ray");
                        Mission.key.Interact(Player);var inventory=Player.GetComponent<PlayerInventory>();inventory.Move(InventoryContainer.Satchel,inventory.Find(InventoryContainer.Satchel,InventoryItemKind.OfficeKey),InventoryContainer.Use,0,out _);Check(Mission.HasKey,"key grants office access on full map");
                        doors=Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None).OrderBy(d=>d.name).ToArray();
                        Check(doors.Length==SchoolPlan.Doors.Length,"all 32 planned doorways exist");Next(1);break;
                    case 1:
                        if(index>=doors.Length){Next(4);break;}
                        var door=doors[index];var front=-door.transform.forward;
                        Teleport(door.transform.position+front*1.3f);destination=door.transform.position-front*1.3f;
                        Check(RayTo<OfficeDoor>(door.transform.position+Vector3.up*1.1f),"interaction ray: "+door.name);
                        door.Interact(Player);Next(2);break;
                    case 2:
                        if(now-stageAt<.5)return;
                        Check(doors[index].IsOpen,"opens: "+doors[index].name);Next(3);break;
                    case 3:
                        var delta=destination-Player.transform.position;delta.y=0;
                        if(delta.magnitude<.15f){Check(true,"player walks through: "+doors[index].name);index++;Next(1);break;}
                        if(now-stageAt>2.5){Check(false,"blocked doorway: "+doors[index].name+" at "+Player.transform.position);index++;Next(1);break;}
                        Player.GetComponent<CharacterController>().Move(Vector3.ClampMagnitude(delta,3.5f*dt)+Vector3.down*.06f);break;
                    case 4:
                        var phone=Object.FindFirstObjectByType<PhonePickup>();
                        Teleport(phone.transform.position+Vector3.forward*1.4f);
                        Check(RayTo<PhonePickup>(phone.transform.position+Vector3.up*.2f),"relocated confiscated box is reachable");
                        phone.Interact(Player);
                        Check(Player.HasPhone&&Mission.Current==OfficeMission.Stage.ReturnToClass,"phone advances to return-to-class objective");
                        var seat=Object.FindFirstObjectByType<ClassroomSeat>();Teleport(seat.transform.position+Vector3.right*1.4f);
                        Check(RayTo<ClassroomSeat>(seat.transform.position+Vector3.up*.73f),"relocated pupil desk is reachable");
                        seat.Interact(Player);Check(GameManager.Instance.Current==GameManager.State.Won,"return to class completes chapter");
                        GameManager.Instance.Restart();Next(5);break;
                    case 5:
                        if(now-stageAt<1)return;
                        Check(GameManager.Instance.IsPlaying&&!Mission.HasKey&&!Mission.OfficeUnlocked&&!Player.HasPhone,"full-school restart resets mission");
                        Check(Mission.key.gameObject.activeSelf,"key restored after restart");
                        Check(Mission.caretaker.patrol.All(p=>p.point!=null),"patrol references survive restart");
                        Finish("complete");break;
                }
            }
            catch(Exception e){Check(false,e.Message);Finish("exception");}
        }
        static void Finish(string reason)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Check(errors==0,"no runtime errors ("+errors+")");
            File.AppendAllText(ResultPath,(pass?"PASS ":"FAIL ")+reason+"\nDONE\n");
            Application.runInBackground=previousBackground;EditorApplication.isPlaying=false;
        }
    }
}

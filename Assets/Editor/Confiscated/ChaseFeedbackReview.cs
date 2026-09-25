using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEditor;
using Object=UnityEngine.Object;
namespace Confiscated.EditorTools
{
    /// <summary>Deterministic feedback checks; state changes arrange test cases, not a full playthrough.</summary>
    public static class ChaseFeedbackReview
    {
        static int stage;static double at;static float baseFov,preference;static Vector3 cartStart;static SchoolRunController run;static FirstPersonController player;
        const string Report="../Docs/ChaseFeedback_Validation.txt";
        static void Need(bool value,string label){File.AppendAllText(Report,(value?"PASS ":"FAIL ")+label+"\n");if(!value)throw new Exception(label);}
        static void State(CaretakerAI.State state){typeof(CaretakerAI).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(run.caretaker,state);}
        static void Next(){stage++;at=EditorApplication.timeSinceStartup;}
        public static void Begin(){stage=0;at=EditorApplication.timeSinceStartup;File.WriteAllText(Report,"Deterministic runtime feedback validation; chase/state and inventory cases arranged directly.\n");EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){EditorApplication.update-=Tick;return;}
            if(EditorApplication.timeSinceStartup-at<1)return;
            try
            {
                if(stage==0){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();ComicDialogue.Cancel();Next();return;}
                if(stage==1)
                {
                    run=SchoolRunController.Instance;run.period.PrepareChaseRetry();run.PrepareChaseRetry();run.caretaker.Freeze();run.caretaker.enabled=false;run.secondStaff?.Freeze();
                    player=run.period.Player.GetComponent<FirstPersonController>();((InputActionAsset)typeof(FirstPersonController).GetField("inputActions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(player)).Disable();
                    player.Controller.enabled=false;player.transform.position=new Vector3(-19,0.05f,79.8f);player.transform.rotation=Quaternion.Euler(0,180,0);player.Controller.enabled=true;player.ResetLook();
                    run.caretaker.GetComponent<NavMeshAgent>().Warp(new Vector3(-19,0,75.9f));
                    preference=player.GetComponent<ChaseCamera>().intensity;player.GetComponent<ChaseCamera>().intensity=1;baseFov=Camera.main.fieldOfView;
                    cartStart=Object.FindFirstObjectByType<DinnerTrolleyPatrol>().transform.position;Next();return;
                }
                if(stage==2)
                {
                    Need(Object.FindFirstObjectByType<PickupChecklist>().canvasRenderer!=null,"checklist has a renderer");
                    Need(Mathf.Abs(Camera.main.fieldOfView-baseFov)<.1f,"patrol retains base FOV");
                    ScreenCapture.CaptureScreenshot("../Docs/ChaseCamera_Patrol.png");State(CaretakerAI.State.Chase);Next();return;
                }
                if(stage==3)
                {
                    Need(Camera.main.fieldOfView>baseFov+4,"chase smoothly widens FOV by five degrees");
                    ScreenCapture.CaptureScreenshot("../Docs/ChaseCamera_Chase.png");player.GetComponent<ChaseCamera>().SetIntensity(0);Next();return;
                }
                if(stage==4)
                {
                    Need(Mathf.Abs(Camera.main.fieldOfView-baseFov)<.05f,"camera off restores base FOV");player.GetComponent<ChaseCamera>().SetIntensity(1);State(CaretakerAI.State.Patrol);
                    run.RecoveryOrder.Add(0);run.RecoveryOrder.Add(2);Next();return;
                }
                if(stage==5)
                {
                    Need(run.caretaker.GetComponent<CaretakerGait>().Mood==1,"angry walking artwork at two items");
                    ScreenCapture.CaptureScreenshot("../Docs/ChaseFeedback_Checklist.png");
                    run.TakeTool(AccessToolPickup.Tool.StoreKey);var door=Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None).First(d=>d.runRequiredLevel>=4);door.Interact(run.period.Player);
                    run.RecoveryOrder.Add(1);run.RecoveryOrder.Add(3);run.RecoveryOrder.Add(4);run.period.Player.HasPhone=true;Next();return;
                }
                if(stage==6)
                {
                    Need(run.caretaker.GetComponent<CaretakerGait>().Mood==2,"furious artwork at four and five items");
                    var door=Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None).First(d=>d.runRequiredLevel>=4);Need(door.IsUnlocked&&door.GetComponent<ProgressPropFeedback>().padlock.localPosition.y<.5f,"unlocked padlock drops");
                    var exit=Object.FindObjectsByType<ProgressPropFeedback>(FindObjectsSortMode.None).First(f=>f.exitPlate!=null);Need(!exit.crossedOut.activeSelf&&exit.exitText.text.Contains("GO"),"five items light the exit");
                    var lid=Object.FindObjectsByType<ProgressPropFeedback>(FindObjectsSortMode.None).First(f=>f.boxLid!=null).boxLid;Need(Quaternion.Angle(lid.localRotation,Quaternion.Euler(-110,0,0))<1,"phone box stays open");
                    run.RecoveryOrder.Remove(4);run.RecoveryOrder.Remove(3);run.RecoveryOrder.Remove(1);
                    player.MovementLocked=true;Next();return;
                }
                if(stage==7)
                {
                    Need(Mathf.Abs(Camera.main.fieldOfView-baseFov)<.05f,"locked/modal camera restores lens");player.MovementLocked=false;
                    var exit=Object.FindObjectsByType<ProgressPropFeedback>(FindObjectsSortMode.None).First(f=>f.exitPlate!=null);Need(exit.crossedOut.activeSelf,"exit relocks when a belonging is lost");
                    var path=new NavMeshPath();Need(NavMesh.CalculatePath(new Vector3(-27,0,79.6f),new Vector3(-13,0,79.6f),NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"route around dinner trolley remains available");
                    var wet=Object.FindFirstObjectByType<WetFloorHazard>();var old=player.transform.position;player.Controller.enabled=false;player.transform.position=wet.transform.position+Vector3.up*.05f;
                    var update=typeof(WetFloorHazard).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);var sprint=typeof(FirstPersonController).GetField("<IsSprinting>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic);
                    sprint.SetValue(player,false);update.Invoke(wet,null);Need(wet.SlipCount==0,"walking on wet patch is safe");float reserve=player.SprintFraction;
                    sprint.SetValue(player,true);update.Invoke(wet,null);Need(wet.SlipCount==1&&player.SprintFraction<reserve,"sprint on patch consumes stamina and stumbles");update.Invoke(wet,null);Need(wet.SlipCount==1,"wet patch cooldown prevents repeated penalties");sprint.SetValue(player,false);player.transform.position=old;player.Controller.enabled=true;
                    player.GetComponent<ChaseCamera>().SetIntensity(preference);File.AppendAllText(Report,"Complete. Inputs/state arranged directly; camera and hazard feel still need hands-on playtesting.\n");EditorApplication.update-=Tick;
                    Debug.Log("Chase feedback review passed.");
                }
            }
            catch(Exception e){File.AppendAllText(Report,e.Message+"\n");if(player!=null)player.GetComponent<ChaseCamera>().SetIntensity(preference);EditorApplication.update-=Tick;Debug.LogException(e);}
        }
    }
}

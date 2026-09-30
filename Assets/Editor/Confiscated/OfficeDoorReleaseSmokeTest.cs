using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    [InitializeOnLoad]
    public static class OfficeDoorReleaseSmokeTest
    {
        const string Marker="Temp/office_release_test",Report="../Docs/OfficeDoorReleaseValidation.txt";
        static double at;static int stage;static bool background;
        static OfficeDoorReleaseSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);File.WriteAllText(Report,DateTime.Now.ToString("O")+"\n");stage=0;at=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;EditorApplication.update+=Tick;}};}
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Need(bool ok,string text){File.AppendAllText(Report,(ok?"ok ":"FAIL ")+text+"\n");if(!ok)throw new Exception(text);}
        static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying){Finish(false);return;}
                if(EditorApplication.timeSinceStartup-at<(stage==0?2:stage==1?.25:2.7))return;
                var run=SchoolRunController.Instance;var release=Object.FindFirstObjectByType<OfficeDoorRelease>();
                var doors=Object.FindObjectsByType<LessonCorridorDoors>(FindObjectsSortMode.None);
                if(stage==0)
                {
                    Need(release!=null&&doors.Length==4,"office button and four corridor-door sets exist");
                    Need(!run.CorridorDoorsReleased&&doors.All(d=>!d.IsOpen&&d.barrier.activeSelf),"fresh run starts with all corridor doors blocked");
                    if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();}ComicDialogue.Cancel();run.period.PrepareChaseRetry();run.PrepareChaseRetry();run.PauseStaff();
                    Need(!run.CorridorDoorsReleased,"starting the chase does not auto-release doors");
                    Physics.SyncTransforms();RaycastHit hit;
                    bool aimed=Physics.Raycast(release.transform.position+release.transform.forward*.7f,-release.transform.forward,out hit,1);
                    Need(aimed&&hit.collider.GetComponentInParent<Interactable>()==release,"button reachable by normal interaction ray");
                    release.Interact(run.period.Player);Need(run.CorridorDoorsReleased&&!release.CanInteract(run.period.Player),"button releases doors once");
                    var click=release.GetComponent<AudioSource>();Need(click.clip!=null&&click.clip.name=="OfficeReleaseClick"&&click.isPlaying&&click.spatialBlend==1,"supplied button click plays at button");
                    stage=1;at=EditorApplication.timeSinceStartup;return;
                }
                if(stage==1){Need(doors.All(d=>d.GetComponent<DoorSounds>().Source.isPlaying),"all four door sounds play during the swing");stage=2;return;}
                Need(doors.All(d=>d.IsOpen&&!d.barrier.activeSelf),"all four doors swing open and remove collision/navigation barriers");
                Need(doors.All(d=>d.GetComponent<DoorSounds>().Source.clip!=null&&d.GetComponent<DoorSounds>().Source.clip.name=="BlueCorridorOpen"&&d.GetComponent<DoorSounds>().Source.spatialBlend==1),"each door uses supplied spatial opening recording");
                Need(release.transform.Find("AssetHub door release/Pressed").gameObject.activeSelf,"pressed mesh is displayed");
                var cutters=Object.FindObjectsByType<AccessToolPickup>(FindObjectsSortMode.None).First(p=>p.tool==AccessToolPickup.Tool.BoltCutters);
                Need(Vector3.Distance(cutters.transform.position,new Vector3(23.75f,.84f,61.4f))<.05f,"cutters moved to Equipment checkout desk");
                Need(cutters.visual.activeInHierarchy&&cutters.CanInteract(run.period.Player),"Equipment cutters visible and collectible");cutters.Interact(run.period.Player);Need(run.HasBoltCutters,"cutters pickup works");Finish(true);
            }
            catch(Exception e){File.AppendAllText(Report,e+"\n");Finish(false);}
        }
        static void Finish(bool pass){EditorApplication.update-=Tick;Application.runInBackground=background;File.AppendAllText(Report,pass?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;}
    }
}

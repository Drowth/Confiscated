using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Doors swing away from whoever opens them, from either side; the window and push plate sit on the latch side of both faces.</summary>
    [InitializeOnLoad]
    public static class DoorSwingSmokeTest
    {
        const string Marker="Temp/door_swing_test",Dir="../Docs/Doors",Report=Dir+"/Validation.txt";
        static int step,errors;static double at,started;static bool pass,background;
        static OfficeDoor door;static int side;
        static SchoolRunController R=>SchoolRunController.Instance;
        static PlayerInteractor P=>R.period.Player;
        static DoorSwingSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Start();}};}
        [MenuItem("Confiscated/Play Test/Arm Door Swing Test (runs on next Play)")]
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Start()
        {
            Directory.CreateDirectory(Dir);step=errors=0;pass=true;at=started=EditorApplication.timeSinceStartup;
            background=Application.runInBackground;Application.runInBackground=true;
            File.WriteAllText(Report,"Door swing direction and leaf artwork ("+DateTime.Now.ToString("s")+")\n");
            Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        }
        static void Log(string m,string t,LogType type){if(type==LogType.Error||type==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+m+"\n");}}
        static void Check(bool ok,string message){pass&=ok;File.AppendAllText(Report,(ok?"ok   ":"FAIL ")+message+"\n");}
        static void Next(int n){step=n;at=EditorApplication.timeSinceStartup;}
        static void Warp(Vector3 p){var f=P.GetComponent<FirstPersonController>();f.Controller.enabled=false;P.transform.position=p;f.Controller.enabled=true;Physics.SyncTransforms();}
        static Vector3 Standing(int s)=>door.transform.TransformPoint(new Vector3(0,0,s*1.6f/door.transform.lossyScale.z));
        // Where the leaf has swung to: its centre's side of the door plane (+ = the door's +Z side).
        static float LeafSide()=>door.transform.InverseTransformPoint(door.hinge.GetComponentsInChildren<Renderer>().First(r=>r.name=="Leaf").bounds.center).z;
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish();return;}
            double elapsed=EditorApplication.timeSinceStartup-at;
            try
            {
                if(EditorApplication.timeSinceStartup-started>60)throw new Exception("door test timeout at step "+step);
                switch(step)
                {
                    case 0:
                        if(elapsed<1)return;
                        if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();}
                        Next(1);break;
                    case 1:
                        if(elapsed<1)return;
                        ComicDialogue.Cancel();R.period.PrepareChaseRetry();R.PrepareChaseRetry();R.PauseStaff();R.caretaker.Freeze();
                        var shade=Object.FindFirstObjectByType<LibraryShadow>();if(shade!=null)shade.Paused=true;
                        // An ordinary single classroom door that is open for play.
                        door=Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None).Where(d=>d.secondHinge==null&&!d.closedForRun&&!d.mainExit&&d.mission==null&&d.runRequiredLevel==0&&!d.closedDuringLessons&&d.IsUnlocked&&d.hinge.GetComponentsInChildren<Renderer>().Any(r=>r.name=="Leaf"&&r.sharedMaterial.name=="M_Door_Classroom")).OrderBy(d=>d.name).First();
                        File.AppendAllText(Report,"door: "+door.name+"\n");
                        side=-1;Next(2);break;
                    case 2:
                        // Stand on one side, open it, it should swing to the other.
                        Warp(Standing(side));door.Interact(P);Next(3);break;
                    case 3:
                        if(elapsed<1.2)return;
                        Check(door.IsOpen&&Mathf.Sign(LeafSide())==-side,"opened from the "+(side<0?"front":"back")+", the door swings away from the player (leaf side "+LeafSide().ToString("F2")+")");
                        door.Interact(P);Next(4);break;
                    case 4:
                        if(elapsed<1.2)return;
                        Check(!door.IsOpen,"it closes again");
                        if(side<0){side=1;Next(2);}else Next(5);break;
                    case 5:
                        // Staff walking through push it away from themselves too.
                        Warp(door.transform.position+door.transform.right*6);
                        var staff=R.caretaker;var agent=staff.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        agent.Warp(Standing(1));door.caretaker=staff;Next(6);break;
                    case 6:
                        if(elapsed<1.2)return;
                        Check(door.IsOpen&&LeafSide()<0,"a caretaker on the back side pushes it open towards the front");
                        R.caretaker.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(door.transform.position+door.transform.right*12);Next(7);break;
                    case 7:
                        if(elapsed<1.5)return;
                        // Artwork: window and push plate on the latch side (away from the hinge) from both faces.
                        foreach(int s in new[]{-1,1})
                        {
                            var cam=new GameObject("Door camera").AddComponent<Camera>();cam.fieldOfView=50;
                            cam.transform.position=door.transform.TransformPoint(new Vector3(0,1.1f,s*2.2f/door.transform.lossyScale.z));
                            cam.transform.LookAt(door.transform.TransformPoint(new Vector3(0,1.1f,0)));
                            HallwayPropLibrary.Capture(cam,900,1000,Path.GetFullPath(Dir+"/Door_"+(s<0?"Front":"Back")+".png"));
                            Object.Destroy(cam.gameObject);
                        }
                        var mesh=door.hinge.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="Leaf").sharedMesh;
                        var v=mesh.vertices;var n=mesh.normals;var uv=mesh.uv;
                        bool same=Enumerable.Range(0,v.Length).Where(i=>Mathf.Abs(n[i].z)>.9f).All(i=>Mathf.Abs(uv[i].x-(v[i].x-mesh.bounds.min.x)/mesh.bounds.size.x)<.01f);
                        Check(same,"both faces map the artwork to the same edge, so the window lines up through the door");
                        Finish();break;
                }
            }
            catch(Exception e){Check(false,e.ToString());Finish();}
        }
        static void Finish()
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Application.runInBackground=background;
            Check(errors==0,"no runtime errors");File.AppendAllText(Report,pass?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;
        }
    }
}

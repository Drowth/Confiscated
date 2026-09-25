using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    [InitializeOnLoad]
    public static class DiningHallSmokeTest
    {
        const string Marker="Temp/run_dining_hall",Report="../Docs/DiningHall_Validation.txt";
        static int stage,index,errors,lastFrame;static double start,at,last;static bool pass,background;
        static PlayerInteractor player;static CharacterController cc;static OfficeDoor[] doors;
        static Vector3 destination;static List<Vector3> route;
        static DiningHallSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Begin();}};}
        [MenuItem("Confiscated/Play Test/Arm Dining Hall Test (runs on next Play)")]
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Begin(){stage=index=errors=0;lastFrame=-1;pass=true;start=at=last=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;File.WriteAllText(Report,"Dining hall furniture and navigation runtime test\n");Application.logMessageReceived+=Log;EditorApplication.update+=Tick;}
        static void Log(string m,string trace,LogType t){if(t==LogType.Error||t==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+m+"\n");}}
        static void Check(bool ok,string message){pass&=ok;File.AppendAllText(Report,(ok?"ok   ":"FAIL ")+message+"\n");}
        static void Teleport(Vector3 p){cc.enabled=false;player.transform.position=p+Vector3.up*.05f;cc.enabled=true;}
        static void Next(int s){stage=s;at=EditorApplication.timeSinceStartup;}
        static bool Walk(float dt)
        {
            var delta=destination-player.transform.position;delta.y=0;
            if(delta.magnitude<.15f)return true;
            if(EditorApplication.timeSinceStartup-at>14)throw new Exception("Player blocked walking toward "+destination+" from "+player.transform.position);
            cc.Move(Vector3.ClampMagnitude(delta,7*dt)+Vector3.down*.06f);return false;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish();return;}
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            double now=EditorApplication.timeSinceStartup;float dt=Mathf.Min(.05f,(float)(now-last));last=now;
            try
            {
                if(now-start>110)throw new Exception("Dining test timed out");
                switch(stage)
                {
                    case 0:
                        if(now-at<1)return;
                        var root=GameObject.Find("DiningHallFurniture");
                        Check(root.transform.Find("Seating - 144 places").childCount==24,"24 six-seat table sets, 48 separate benches");
                        Check(root.GetComponentsInChildren<MeshFilter>().All(m=>m.sharedMesh!=null),"all furniture meshes loaded");
                        Check(root.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader!=null)),"no missing materials or shaders");
                        var menus=root.GetComponentsInChildren<TextMesh>().Where(t=>t.transform.parent.name.Contains("MenuBoard"));
                        Check(menus.All(t=>t.GetComponent<Renderer>().bounds.size.x<1.25f),"chalk lettering fits menu boards");
                        player=Object.FindFirstObjectByType<PlayerInteractor>();cc=player.GetComponent<CharacterController>();
                        player.GetComponent<FirstPersonController>().enabled=false;Object.FindFirstObjectByType<CaretakerAI>().Freeze();
                        doors=Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None).Where(d=>d.name.Contains("Dining")).ToArray();
                        Check(doors.Length==5,"all five dining entrances retained");foreach(var d in doors)if(!d.IsOpen)d.Interact(player);
                        route=new List<Vector3>{new(-19.5f,0,72),new(-19.5f,0,47.2f),new(-30.2f,0,47.2f),new(-30.2f,0,70.4f),new(-8.5f,0,70.4f),new(-8.5f,0,48.5f),new(-19.5f,0,48.5f)};
                        for(int i=1;i<route.Count;i++)
                        {
                            var path=new NavMeshPath();bool a=NavMesh.SamplePosition(route[i-1],out var h0,.5f,NavMesh.AllAreas),b=NavMesh.SamplePosition(route[i],out var h1,.5f,NavMesh.AllAreas);
                            Check(a&&b&&NavMesh.CalculatePath(h0.position,h1.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"AI navigation reaches cafeteria aisle "+i);
                        }
                        Teleport(route[0]);index=1;destination=route[index];Next(1);break;
                    case 1:
                        if(!Walk(dt))return;Check(true,"player walks cafeteria aisle "+index);index++;
                        if(index<route.Count){destination=route[index];Next(1);}else{index=0;Next(2);}break;
                    case 2:
                        if(index>=doors.Length){DiningHallSetup.Capture();GameManager.Instance.Restart();Next(4);break;}
                        var door=doors[index];Check(door.IsOpen,"dining door opens: "+door.name);
                        Teleport(door.transform.position-door.transform.forward*1.3f);destination=door.transform.position+door.transform.forward*1.3f;Next(3);break;
                    case 3:
                        if(!Walk(dt))return;Check(true,"player walks through: "+doors[index].name);index++;Next(2);break;
                    case 4:
                        if(now-at<1)return;Check(GameObject.Find("DiningHallFurniture")!=null&&GameManager.Instance.IsPlaying,"furnishings persist and normal gameplay returns after restart");Finish();break;
                }
            }
            catch(Exception e){Check(false,e.ToString());Finish();}
        }
        static void Finish(){EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Application.runInBackground=background;Check(errors==0,"no runtime errors");File.AppendAllText(Report,pass?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;}
    }
}

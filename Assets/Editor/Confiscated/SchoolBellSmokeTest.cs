using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    [InitializeOnLoad]
    public static class SchoolBellSmokeTest
    {
        const string Marker="Temp/run_school_bells",Report="../Docs/SchoolBells_Validation.txt";
        static int step,errors;static double at,started;static bool pass,background,moved,marks,output;
        static SchoolBellSystem system;static SchoolBell bell;static Vector3 restPosition;static Quaternion restRotation;
        static Camera camera;static float[] samples=new float[256];
        static SchoolBellSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Start();}};}
        [MenuItem("Confiscated/Play Test/Arm School Bells Test (runs on next Play)")]
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Start()
        {
            step=errors=0;pass=true;moved=marks=output=false;at=started=EditorApplication.timeSinceStartup;
            background=Application.runInBackground;Application.runInBackground=true;
            File.WriteAllText(Report,"School bells: physical placement, audio, vibration, event and restart\n");
            Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        }
        static void Log(string m,string trace,LogType t){if(t==LogType.Error||t==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+m+"\n");}}
        static void Check(bool ok,string message){pass&=ok;File.AppendAllText(Report,(ok?"ok   ":"FAIL ")+message+"\n");}
        static void Next(int n){step=n;at=EditorApplication.timeSinceStartup;}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish();return;}
            double elapsed=EditorApplication.timeSinceStartup-at;
            try
            {
                if(EditorApplication.timeSinceStartup-started>40)throw new Exception("Bell test timeout");
                switch(step)
                {
                    case 0:
                        if(elapsed<1)return;
                        system=Object.FindFirstObjectByType<SchoolBellSystem>();bell=system.bells.First(b=>b.name.Contains("North west"));
                        restPosition=bell.gong.localPosition;restRotation=bell.gong.localRotation;
                        Check(system.bells.Length==15,"15 reusable wall bells loaded");
                        Check(system.bells.All(b=>!b.Source.isPlaying&&!b.ringingMarks.activeSelf),"silent and still on scene start");
                        Physics.SyncTransforms();
                        foreach(var b in system.bells)
                        {
                            var wall=Physics.RaycastAll(b.transform.position-b.transform.forward*.3f,b.transform.forward,1f).Any(h=>h.collider.name.StartsWith("Wall_"));
                            var bounds=b.GetComponent<BoxCollider>().bounds;
                            Check(wall&&bounds.min.y>=2.09f&&bounds.max.y<3,"mounted on wall above player clearance: "+b.name);
                            Check(b.Source.spatialBlend==1&&b.Source.dopplerLevel==0&&!b.Source.loop&&!b.Source.playOnAwake&&b.Source.clip.channels==1,"mono positional emitter: "+b.name);
                        }
                        var curve=bell.Source.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
                        Check(curve.Evaluate(3f/38)>curve.Evaluate(15f/38)&&curve.Evaluate(15f/38)>curve.Evaluate(30f/38)&&curve.Evaluate(1)==0,"audio falls with distance and reaches silence at 38 m");
                        Object.FindFirstObjectByType<CaretakerAI>().Freeze();
                        var player=Object.FindFirstObjectByType<FirstPersonController>();player.enabled=false;
                        var listener=Object.FindFirstObjectByType<AudioListener>();
                        listener.transform.position=bell.transform.position+new Vector3(.7f,-.3f,-1.5f);listener.transform.LookAt(bell.transform.position);
                        camera=new GameObject("Bell test camera").AddComponent<Camera>();
                        camera.transform.SetPositionAndRotation(listener.transform.position,listener.transform.rotation);camera.fieldOfView=38;
                        system.Ring();Next(1);break;
                    case 1:
                        if(elapsed<.35)return;
                        Check(system.bells.All(b=>b.IsRinging),"all 15 sources start together");
                        Check(system.bells.Max(b=>b.Source.time)-system.bells.Min(b=>b.Source.time)<.025f,"playback clocks synchronized within 25 ms");
                        float before=bell.Source.time;system.Ring();Check(bell.Source.time>=before-.025f,"repeat event does not restart/overlap current ring");
                        Next(2);break;
                    case 2:
                        moved|=Vector3.Distance(bell.gong.localPosition,restPosition)>.0002f||Quaternion.Angle(bell.gong.localRotation,restRotation)>.05f;
                        marks|=bell.ringingMarks.activeSelf;
                        bell.Source.GetOutputData(samples,0);output|=samples.Any(s=>Mathf.Abs(s)>.00001f);
                        if(elapsed<.5)return;
                        HallwayPropLibrary.Capture(camera,1200,1000,"D:/Confiscated/Docs/SchoolBell_Ringing.png");
                        camera.transform.position=bell.transform.position+new Vector3(-3,-.99f,-2.2f);
                        camera.transform.LookAt(bell.transform.position+new Vector3(5,-.75f,-.6f));camera.fieldOfView=78;
                        HallwayPropLibrary.Capture(camera,1600,1000,"D:/Confiscated/Docs/SchoolBells_Corridor.png");
                        Check(moved&&marks,"gong physically vibrates and drawn ringing marks pulse");
                        Check(output,"emitter produces nonzero decoded audio samples");
                        Next(3);break;
                    case 3:
                        if(system.IsRinging)return;
                        // Editor update can observe DSP completion before this frame's LateUpdate.
                        // Let the gameplay animation update run before inspecting the settled pose.
                        Next(30);break;
                    case 30:
                        if(elapsed<.1)return;
                        Check(system.bells.All(b=>!b.IsRinging&&!b.ringingMarks.activeSelf),"clip completion stops every animation and sound mark");
                        Check(Vector3.Distance(bell.gong.localPosition,restPosition)<.00001f&&Quaternion.Angle(bell.gong.localRotation,restRotation)<.001f,"gong returns exactly to rest");
                        system.Ring();Next(4);break;
                    case 4:
                        if(elapsed<.4)return;
                        system.Stop();Check(system.bells.All(b=>!b.Source.isPlaying&&!b.ringingMarks.activeSelf),"explicit stop resets all bells immediately");
                        var p=Object.FindFirstObjectByType<PlayerInteractor>();p.HasPhone=true;
                        GameManager.Instance.officeMission.PhoneRecovered();GameManager.Instance.officeMission.ReturnToSeat(p);
                        Next(5);break;
                    case 5:
                        if(elapsed<.4)return;
                        Check(GameManager.Instance.Current==GameManager.State.Won&&system.bells.All(b=>b.IsRinging),"returning to class rings physical school bells through the real mission event");
                        GameManager.Instance.Restart();Next(6);break;
                    case 6:
                        if(elapsed<1)return;
                        system=Object.FindFirstObjectByType<SchoolBellSystem>();
                        Check(system.bells.Length==15&&!system.IsRinging&&GameManager.Instance.IsPlaying,"restart restores 15 silent bells and normal gameplay");
                        Finish();break;
                }
            }
            catch(Exception e){Check(false,e.ToString());Finish();}
        }
        static void Finish()
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
            Application.runInBackground=background;
            Check(errors==0,"no runtime errors");
            File.AppendAllText(Report,pass?"PASS\n":"FAIL\n");
            EditorApplication.isPlaying=false;
        }
    }
}

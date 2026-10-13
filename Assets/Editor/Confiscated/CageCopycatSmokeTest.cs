using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace Confiscated.EditorTools
{
    [InitializeOnLoad]
    public static class CageCopycatSmokeTest
    {
        const string Marker="Temp/cage_copycat_test",Report="../Docs/CageCopycatValidation.txt";
        static int step;static double at,started;static bool passed,background;static RunGate gate;static CopycatStudent pupil;
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static CageCopycatSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Begin();}};}
        [MenuItem("Confiscated/Play Test/Arm Cage and Copycat Test")]
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Begin(){step=0;passed=true;started=at=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;File.WriteAllText(Report,"Cage and Copycat: "+DateTime.Now.ToString("s")+"\n");EditorApplication.update+=Tick;}
        static void Check(bool ok,string msg){passed&=ok;File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+msg+"\n");}
        static void Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Flags).Invoke(target,args);
        static void Set(object target,string field,object value)=>target.GetType().GetField(field,Flags).SetValue(target,value);
        static void Next(){step++;at=EditorApplication.timeSinceStartup;}
        static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying){Finish();return;}
                if(EditorApplication.timeSinceStartup-started>45)throw new Exception("test timeout");
                var r=SchoolRunController.Instance;if(r==null||r.period==null||r.period.Player==null)return;
                double elapsed=EditorApplication.timeSinceStartup-at;
                if(step==0)
                {
                    if(elapsed<1)return;
                    if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();}
                    Next();return;
                }
                if(step==1)
                {
                    if(elapsed<1)return;
                    ComicDialogue.Cancel();r.period.PrepareChaseRetry();r.PrepareChaseRetry();r.caretaker.Freeze();r.secondStaff?.Freeze();
                    gate=Object.FindObjectsByType<RunGate>(FindObjectsSortMode.None).First(g=>g.kind==RunGate.Kind.Chain);
                    r.TakeTool(AccessToolPickup.Tool.BoltCutters);gate.Interact(r.period.Player);
                    Check(gate.Cleared&&r.CageOpen,"chain interaction unlocks cage");
                    Check(gate.GetComponents<AudioSource>().Any(a=>a.clip!=null&&a.clip.name=="CageChainCut"),"supplied metal-rattle source plays on chain cut");
                    Next();return;
                }
                if(step==2)
                {
                    if(elapsed<2)return;
                    Check(gate.obstacle.gameObject.activeInHierarchy&&Mathf.Abs(Mathf.DeltaAngle(0,gate.obstacle.parent.localEulerAngles.y))>100,"gate remains visible and swings open");
                    Check(gate.GetComponentsInChildren<Transform>().Where(t=>t.name=="STORAGE"||t.name=="Gate notice").All(t=>t.IsChildOf(gate.obstacle)),"notice and lettering move with gate");
                    pupil=Object.FindAnyObjectByType<CopycatStudent>();Check(pupil!=null,"Copycat installed");
                    pupil.enabled=false;Call(pupil,"LightAndBlink",r.period.Player);
                    Check(pupil.GetComponentInChildren<Renderer>().enabled&&pupil.LitByTorch,"Copycat visible without requiring torch exposure");
                    Call(pupil,"Reveal",r.period.Player.transform);Check(pupil.RevealCount==1,"first betrayal");
                    Call(pupil,"BeginFlee",r.period.Player.transform.position);
                    // Look away and finish retreat deterministically, then verify another encounter can trigger.
                    r.period.Player.transform.rotation=Quaternion.LookRotation(r.period.Player.transform.position-pupil.transform.position);
                    Set(pupil,"fleeDeadline",Time.time-1);Call(pupil,"Flee");
                    Check(!pupil.Completed&&!pupil.Fleeing&&!pupil.Triggered&&pupil.Hiding,"retreat rearms instead of deleting Copycat");
                    Call(pupil,"Reveal",r.period.Player.transform);Check(pupil.RevealCount==2,"second betrayal in same run");
                    Finish();
                }
            }
            catch(Exception e){Check(false,e.ToString());Finish();}
        }
        static void Finish(){EditorApplication.update-=Tick;Application.runInBackground=background;File.AppendAllText(Report,passed?"RESULT PASS\n":"RESULT FAIL\n");EditorApplication.isPlaying=false;}
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using Object=UnityEngine.Object;
namespace Confiscated.EditorTools
{
    /// <summary>
    /// Plays repeated chase runs with real keyboard input (W, Shift, F) along NavMesh paths and logs where each attempt
    /// ends. A careless route-runner, not a skilled player: it sprints when chased and never hides, so capture counts are
    /// pessimistic. Use it to find where runs die and where the route stalls, not to judge difficulty.
    /// </summary>
    [InitializeOnLoad] public static class AutoRunBot
    {
        const string Marker="Temp/auto_run_bot",Dir="../Docs/Autopilot",Report=Dir+"/bot_runs.txt",Csv=Dir+"/bot_events.csv";
        const int Attempts=8;const float AttemptCap=240;
        static readonly string[] Prefs={"Confiscated.RunBest.v1.Phone start","Confiscated.RunBest.v1.Lesson start","Confiscated.RunBest.v1.Classroom start","Confiscated.Ending.v1.Caught","Confiscated.Ending.v1.Completed","Confiscated.Ending.v1.FastRun","Confiscated.Ending.v1.QuackEscape"};
        static List<string> hadPrefs;
        static Keyboard keys,oldKeys;static Mouse mouse,oldMouse;
        static InputSettings.BackgroundBehavior background;static InputSettings.EditorInputBehaviorInPlayMode focus;
        static int attempt,phase;static double phaseAt,attemptAt,goalAt,lastMoveAt,holdUntil,tapAt;static Vector3 lastPos;
        static OfficeDoor lastDoor;static double lastDoorAt;static int dawdle;static bool equipmentFirst;
        static string goalName,lastGoal;static CaretakerAI.State lastState;static bool pressing,wasFallen;static int stuckCount;
        static SchoolRunController R=>SchoolRunController.Instance;
        static PlayerInteractor P=>R!=null&&R.period!=null?R.period.Player:null;
        static FirstPersonController F=>P.GetComponent<FirstPersonController>();
        static double Now=>EditorApplication.timeSinceStartup;
        static double T=>Now-attemptAt;

        static AutoRunBot(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Begin();}};}
        [MenuItem("Confiscated/Playtest/Arm Auto Run Bot (runs on next Play)")] public static void Arm()=>File.WriteAllText(Marker,"armed");

        static void Begin()
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Report,"Auto run bot "+DateTime.Now.ToString("yyyy-MM-dd HH:mm")+" - real W/Shift/F input along NavMesh paths, chase-retry start (office key carried), "+Attempts+" attempts\n");
            File.WriteAllText(Csv,"attempt,t,event,x,z,detail\n");
            hadPrefs=Prefs.Where(PlayerPrefs.HasKey).ToList();
            background=InputSystem.settings.backgroundBehavior;focus=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            oldKeys=Keyboard.current;oldMouse=Mouse.current;if(oldKeys!=null)InputSystem.DisableDevice(oldKeys);if(oldMouse!=null)InputSystem.DisableDevice(oldMouse);
            keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
            attempt=0;phase=0;phaseAt=Now;EditorApplication.update+=Tick;
        }

        static void Log(string e,string detail=""){var p=P!=null?P.transform.position:Vector3.zero;File.AppendAllText(Csv,$"{attempt},{T:0.0},{e},{p.x:0.0},{p.z:0.0},\"{detail}\"\n");}
        static void Line(string s)=>File.AppendAllText(Report,s+"\n");
        static void Keys(params Key[] k){var s=new KeyboardState();foreach(var x in k)s.Set(x,true);InputSystem.QueueStateEvent(keys,s);}

        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish();return;}
            try{Step();}catch(Exception e){Line("bot error: "+e.Message);Log("error",e.Message);Keys();phase=3;phaseAt=Now;}
        }

        static void Step()
        {
            var gm=GameManager.Instance;
            if(SchoolTitleMenu.IsActive){if(Now-phaseAt>1){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();phaseAt=Now;}return;}
            if(ComicDialogue.IsActive){Keys();if(Now-phaseAt>.5){ComicDialogue.Instance.Advance();phaseAt=Now;}return;}
            switch(phase)
            {
                case 0: // get into a clean chase retry, the state every death returns the player to
                    if(gm==null||R==null||Now-phaseAt<1.5)return;
                    gm.RestartRun();phase=1;phaseAt=Now;return;
                case 1:
                    if(gm==null||R==null||P==null||!R.RoundStarted||Now-phaseAt<1.5)return;
                    attempt++;attemptAt=Now;goalAt=Now;lastMoveAt=Now;lastPos=P.transform.position;lastGoal=null;stuckCount=0;
                    lastState=R.caretaker.Current;wasFallen=false;Cursor.lockState=CursorLockMode.Locked;F.ResetLook();
                    // vary each attempt so runs do not replay identically: a first-timer dawdles, and picks a room order
                    var rng=new System.Random(attempt*7919);dawdle=rng.Next(0,4)*6;equipmentFirst=rng.Next(2)==1;
                    Log("start",$"dawdle {dawdle}s, {(equipmentFirst?"equipment":"resources")} first");phase=2;return;
                case 2: Play(gm);return;
                case 3: // attempt over: wait on the results screen, then retry
                    Keys();if(Now-phaseAt<3)return;
                    if(attempt>=Attempts){EditorApplication.isPlaying=false;return;}
                    if(gm!=null&&(gm.Current==GameManager.State.Caught||gm.Current==GameManager.State.Won)){Keys(Key.R);phase=4;phaseAt=Now;}
                    else{gm?.RestartRun();phase=1;phaseAt=Now;}
                    return;
                case 4: Keys();phase=1;phaseAt=Now;return;
            }
        }

        static void End(string how){Keys();F.LookLocked=false;Log("end",how);
            Line($"attempt {attempt}: {how} at {T:0}s, items {R.Count}/5 [{string.Join(",",R.RecoveryOrder)}], cutters {R.HasBoltCutters}, cage {R.CageOpen}, store key {R.HasStoreKey}, stuck events {stuckCount}");
            phase=3;phaseAt=Now;}

        static void Play(GameManager gm)
        {
            var c=R.caretaker;
            if(gm.Current==GameManager.State.Caught){End("CAUGHT by "+(lastCaptor()??"?")+$" (caretaker {c.Current}, {c.PlayerDistance:0.0} m)");return;}
            if(gm.Current==GameManager.State.Won){End("ESCAPED");return;}
            if(gm.Current==GameManager.State.Detention){End("DETENTION (Mr Reed catch)");return;}
            if(T>AttemptCap){End("TIMEOUT on goal "+goalName);return;}
            if(c.Current!=lastState){Log("caretaker",lastState+"->"+c.Current+$" d={c.PlayerDistance:0.0} sees={c.CanCurrentlySeePlayer}");lastState=c.Current;}
            if(F.IsFallen&&!wasFallen)Log("slip");wasFallen=F.IsFallen;

            if(T<dawdle){Keys();return;}
            var goal=Goal(out bool hold);
            goalName=goal!=null?goal.name:"none";
            if(goalName!=lastGoal){Log("goal",goalName);lastGoal=goalName;goalAt=Now;}
            if(goal==null){Keys();return;}
            if(Now-goalAt>75){End("STALLED on "+goalName);return;}

            var pos=P.transform.position;
            Vector3 aim=AimPoint(goal,pos);
            float flat=Vector2.Distance(new Vector2(pos.x,pos.z),new Vector2(aim.x,aim.z));
            if(flat<1.7f){Interact(goal,aim,hold);return;}
            F.LookLocked=false;pressing=false;

            // walk the NavMesh path; sprint while hunted
            Vector3 next=aim;
            if(NavMesh.SamplePosition(pos,out var a,2,NavMesh.AllAreas)&&NavMesh.SamplePosition(aim,out var b,2.5f,NavMesh.AllAreas))
            {var path=new NavMeshPath();NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path);
             if(path.corners.Length>1){next=path.corners[1];if(Flat(next-pos).magnitude<.35f&&path.corners.Length>2)next=path.corners[2];}}
            var dir=Flat(next-pos);if(dir.sqrMagnitude>.001f)P.transform.rotation=Quaternion.LookRotation(dir);
            // a shut door straight ahead: press F like a player would
            var ahead=PlayerInteractor.Resolve(new Ray(P.ViewCamera.transform.position,P.transform.forward),1.6f,~0) as OfficeDoor;
            if(ahead!=null&&!ahead.IsOpen&&ahead.CanInteract(P)&&!ahead.mainExit&&!(ahead==lastDoor&&Now-lastDoorAt<4)){Keys(Key.F);tapAt=Now;lastDoor=ahead;lastDoorAt=Now;Log("door",ahead.name);return;}
            bool hunted=c.Current==CaretakerAI.State.Chase||(R.secondStaff!=null&&R.secondStaff.enabled&&R.secondStaff.Current==CaretakerAI.State.Chase);
            // stuck: nudge sideways
            if(Flat(pos-lastPos).magnitude>.5f){lastPos=pos;lastMoveAt=Now;}
            else if(Now-lastMoveAt>2.5){stuckCount++;Log("stuck",goalName);lastMoveAt=Now;holdUntil=Now+.6;}
            if(Now<holdUntil){Keys(Key.S,stuckCount%2==0?Key.A:Key.D);return;}
            if(hunted&&F.SprintFraction>.05f)Keys(Key.W,Key.LeftShift);else Keys(Key.W);
        }

        static string lastCaptor(){var s=R.secondStaff;return s!=null&&s.enabled&&s.PlayerDistance<R.caretaker.PlayerDistance?"Mr Reed":"caretaker";}

        static void Interact(Interactable goal,Vector3 aim,bool hold)
        {
            F.LookLocked=true;P.ViewCamera.transform.LookAt(aim);
            var dir=Flat(aim-P.transform.position);if(dir.sqrMagnitude>.001f)P.transform.rotation=Quaternion.LookRotation(dir);
            var seen=PlayerInteractor.Resolve(new Ray(P.ViewCamera.transform.position,P.ViewCamera.transform.forward),2.6f,~0);
            if(seen!=goal){Keys(Key.W);if(Now-tapAt>3){Log("aim-miss",goal.name+" sees "+(seen!=null?seen.name:"nothing"));tapAt=Now;}return;}
            if(hold){Keys(Key.F);return;}
            if(!pressing){Keys(Key.F);pressing=true;tapAt=Now;}else{Keys();if(Now-tapAt>.4)pressing=false;}
        }

        static Vector3 AimPoint(Interactable goal,Vector3 from)
        {
            var cols=goal.GetComponentsInChildren<Collider>().Where(x=>x.enabled&&x.gameObject.activeInHierarchy).ToArray();
            if(cols.Length==0)return goal.transform.position;
            var door=goal as OfficeDoor;
            if(door!=null){var h=door.hinge!=null?door.hinge.GetComponentsInChildren<Collider>():cols;var best=h.Length>0?h[0]:cols[0];foreach(var x in h)if((x.bounds.center-from).sqrMagnitude<(best.bounds.center-from).sqrMagnitude)best=x;return best.bounds.center;}
            var box=cols[0].bounds;foreach(var x in cols)box.Encapsulate(x.bounds);return box.center;
        }

        static Interactable Goal(out bool hold)
        {
            hold=false;Interactable target;
            var all=Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None);
            // A retry only hands over the office key if it was fetched before; otherwise get it from the dining-hall trolley.
            if(!R.Has(0)&&!P.GetComponent<PlayerInventory>().HasCarried(InventoryItemKind.OfficeKey)){target=all.OfType<OfficeKeyPickup>().FirstOrDefault();hold=target!=null&&target.holdSeconds>0;}
            else if(!R.Has(0))target=R.period.phonePickup;
            else if(!R.HasBoltCutters&&!R.Has(1))target=all.OfType<AccessToolPickup>().FirstOrDefault(t=>t.tool==AccessToolPickup.Tool.BoltCutters);
            else if(!R.CageOpen&&!R.Has(1)){target=all.OfType<RunGate>().FirstOrDefault(g=>g.kind==RunGate.Kind.Chain);hold=true;}
            else if(!R.Has(1))target=Pickup(all,1);
            else if(!R.Has(equipmentFirst?3:2))target=Pickup(all,equipmentFirst?3:2);
            else if(!R.Has(equipmentFirst?2:3))target=Pickup(all,equipmentFirst?2:3);
            else if(!R.HasStoreKey)target=all.OfType<AccessToolPickup>().FirstOrDefault(t=>t.tool==AccessToolPickup.Tool.StoreKey);
            else if(!R.Has(4))target=Pickup(all,4);
            else{target=all.OfType<OfficeDoor>().FirstOrDefault(d=>d.mainExit);hold=true;}
            if(target==null)return null;
            if(target is RunGate g2)hold=g2.holdSeconds>0;
            // a locked/closed door in the way comes first
            var pos=P.transform.position;
            if(!Reachable(pos,target.transform.position))
            {
                var door=all.OfType<OfficeDoor>().Where(d=>!d.mainExit&&!d.IsUnlocked&&d.CanInteract(P)&&(d.transform.position-target.transform.position).sqrMagnitude<400).OrderBy(d=>(d.transform.position-target.transform.position).sqrMagnitude).FirstOrDefault();
                if(door!=null){hold=door.holdSeconds>0;return door;}
            }
            return target;
        }
        static Interactable Pickup(Interactable[] all,int id)=>all.OfType<RunPickup>().FirstOrDefault(p=>p.itemId==id);
        static bool Reachable(Vector3 a,Vector3 b)
        {
            if(!NavMesh.SamplePosition(a,out var x,2,NavMesh.AllAreas)||!NavMesh.SamplePosition(b,out var y,2.5f,NavMesh.AllAreas))return true;
            var path=new NavMeshPath();NavMesh.CalculatePath(x.position,y.position,NavMesh.AllAreas,path);
            return path.status==NavMeshPathStatus.PathComplete;
        }
        static Vector3 Flat(Vector3 v){v.y=0;return v;}

        static void Finish()
        {
            EditorApplication.update-=Tick;
            if(keys!=null&&keys.added)InputSystem.RemoveDevice(keys);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
            if(oldKeys!=null)InputSystem.EnableDevice(oldKeys);if(oldMouse!=null)InputSystem.EnableDevice(oldMouse);
            InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=focus;
            foreach(var k in Prefs)if(!hadPrefs.Contains(k))PlayerPrefs.DeleteKey(k);PlayerPrefs.Save();
            Line("done");keys=null;mouse=null;
        }
    }
}

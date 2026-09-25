using System;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Confiscated;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    [InitializeOnLoad]
    public static class PeCoachSmokeTest
    {
        const string Marker = "Temp/pe_coach_test";
        const string Report = "../Docs/PECoach/Validation.txt";
        static int stage, lastFrame, errors;
        static double started, at;
        static bool background;
        static PeCoach coach;
        static FirstPersonController movement;
        static PlayerInteractor player;
        static Vector3 start;
        static Vector3 patrolStart;
        static float expected;
        static bool heardWarning;
        static bool sawApproach;
        static Vector3 expectedDirection;
        static int finishedDrills;
        const string BatchKey = "Confiscated.PeCoach.BatchValidation";
        static Light startingShadowLight;

        static PeCoachSmokeTest()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !File.Exists(Marker)) return;
                File.Delete(Marker); Begin();
            };
        }

        [MenuItem("Confiscated/School Run/Arm PE Coach Test")]
        public static void Arm() { Directory.CreateDirectory("../Docs/PECoach"); File.WriteAllText(Marker, "armed"); }
        public static void RunBatch()
        {
            SessionState.SetBool(BatchKey, true);
            EditorSceneManager.OpenScene("Assets/Scenes/SchoolLayout.unity");
            Arm(); EditorApplication.isPlaying = true;
        }

        static void Begin()
        {
            SteamLeaderboard.Suppress = true;
            stage = lastFrame = errors = 0; heardWarning = sawApproach = false; started = at = EditorApplication.timeSinceStartup;
            background = Application.runInBackground; Application.runInBackground = true;
            File.WriteAllText(Report, "PE coach real-scene smoke test. Player and coach warped to an open corridor; movement and sight remain live.\n");
            Application.logMessageReceived += Error; EditorApplication.update += Tick;
        }

        static void Error(string message, string trace, LogType type)
        {
            if (trace.Contains("UnityEditor.Search.SearchDatabase"))
            { File.AppendAllText(Report, "EDITOR NOTE: Unity search-index startup exception (outside gameplay).\n"); return; }
            if (type == LogType.Error || type == LogType.Exception) { errors++; File.AppendAllText(Report, "ERROR " + message + "\n"); }
        }
        static void Check(bool okay, string label)
        { File.AppendAllText(Report, (okay ? "ok " : "FAIL ") + label + "\n"); if (!okay) throw new Exception(label); }
        static void Next() { stage++; at = EditorApplication.timeSinceStartup; }
        static Light NearestFixture(Vector3 position)
        {
            Light best = null; float distance = float.MaxValue;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type != LightType.Point || light.transform.parent == null ||
                    light.transform.parent.name != "P_CeilingLight") continue;
                float d = (light.transform.position - position).sqrMagnitude;
                if (d < distance) { best = light; distance = d; }
            }
            return best;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { Finish(false); return; }
            if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
            double elapsed = EditorApplication.timeSinceStartup - at;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 90) throw new Exception("Timeout at stage " + stage);
                switch (stage)
                {
                    case 0:
                        if (elapsed < .8) return;
                        if (SchoolTitleMenu.IsActive) { SchoolTitleMenu.Instance.StartGame(); SchoolTitleMenu.Instance.SkipIntro(); }
                        ComicDialogue.Cancel();
                        var run = SchoolRunController.Instance;
                        run.period.PrepareChaseRetry(); run.PrepareChaseRetry(); run.caretaker.Freeze();
                        coach = Object.FindFirstObjectByType<PeCoach>();
                        player = run.period.Player; movement = player.GetComponent<FirstPersonController>();
                        Check(coach != null && coach.steps.Length == 5 && coach.command != null && coach.warningWhistle != null,
                            "coach art and all supplied sounds installed");
                        var agent = coach.GetComponent<NavMeshAgent>();
                        Check(agent.isOnNavMesh, "coach starts on corridor navigation");
                        patrolStart = coach.transform.position;
                        startingShadowLight = NearestFixture(patrolStart);
                        Check(startingShadowLight != null, "coach has a nearby shadow-casting fixture");
                        var panel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_CeilingLight.mat");
                        Check(panel.IsKeywordEnabled("_EMISSION") && panel.GetColor("_EmissionColor").maxColorComponent > 1f,
                            "ceiling panels have bright HDR emission while fixtures keep their point lights");
                        Check(Resources.Load<Texture2D>("Art/ReedFrontWalk") != null && Resources.Load<Texture2D>("Art/ReedHuntWalk") != null &&
                            Resources.Load<Texture2D>("Art/CoachRecovery") != null, "front steps and recovery artwork load at runtime");
                        Check(coach.command.name == "CoachExerciseCommand", "new exercise shout is installed");
                        Next(); break;
                    case 1:
                        heardWarning |= coach.Whistling;
                        if (coach.ChargesStarted == 0 || Vector3.Distance(patrolStart, coach.transform.position) < .8f)
                        { if (elapsed > 7) throw new Exception("Coach did not whistle and charge down the corridor"); return; }
                        Check(heardWarning && coach.Charging && coach.warningWhistle.name == "CoachWhistle",
                            "supplied whistle precedes the corridor charge");
                        Check(Vector3.Distance(patrolStart, coach.transform.position) > .8f,
                            "coach runs his lap under NavMesh control");
                        agent = coach.GetComponent<NavMeshAgent>();
                        Check(agent.Warp(new Vector3(18f, 0, 79.56f)), "coach can return to the open corridor");
                        coach.transform.rotation = Quaternion.LookRotation(Vector3.left);
                        movement.Controller.enabled = false; player.transform.position = new Vector3(14f, 0, 79.56f);
                        movement.Controller.enabled = true; Physics.SyncTransforms();
                        Check(coach.TryCorridor(out expectedDirection, out expected) && expected > 14f,
                            "drill reaches the corridor end beyond the former 14-metre cap");
                        start = player.transform.position; Next(); break;
                    case 2:
                        if (coach.DrillsStarted == 0)
                        {
                            if (coach.Approaching && !sawApproach) { Check(!movement.ForcedCorridorRun, "approach does not push the pupil remotely"); sawApproach = true; }
                            if (elapsed > 10) throw new Exception("Coach did not reach the pupil and start a drill"); return;
                        }
                        Check(coach.Coaching && movement.ForcedCorridorRun && Vector3.Distance(coach.transform.position, player.transform.position) < 1.7f,
                            "physical contact begins the pushed run");
                        Next(); break;
                    case 3:
                        if (elapsed < .5) return;
                        if (Vector3.Dot(player.transform.position - start, expectedDirection) <= 1.5f && elapsed < 2f) return;
                        File.AppendAllText(Report, "Push progress: " + player.transform.position + " start " + start + " direction " + expectedDirection +
                            " coach " + coach.transform.position + " remaining " + movement.ForcedRunRemaining + "\n");
                        Check(Vector3.Dot(player.transform.position - start, expectedDirection) > 1.5f &&
                            Mathf.Abs(player.transform.position.z - start.z) < .35f,
                            "player travels forward along corridor without movement input");
                        Check(movement.IsSprinting && movement.ForcedRunRemaining < expected - 1.5f,
                            "forced run drives sprint feedback and counts actual distance");
                        Next(); break;
                    case 4:
                        if (coach.DrillsFinished == 0)
                        { if (elapsed > 30) throw new Exception("Corridor drill never finished"); return; }
                        Check(!coach.Coaching && !movement.ForcedCorridorRun, "controls return at corridor end");
                        Check(Vector3.Dot(player.transform.position - start, expectedDirection) >= expected - 1f,
                            "drill covered its intended corridor length");
                        Check(coach.LastDrillSeconds >= 5f, "pushed exercise lasts at least five seconds");
                        File.AppendAllText(Report, "Completed drill: " + coach.LastDrillSeconds.ToString("F2") + " seconds; " + expected.ToString("F2") + " metres.\n");
                        Check(coach.Recovering && coach.AvoidingPlayer, "coach catches breath and starts ten-second cooldown");
                        finishedDrills = coach.DrillsStarted;
                        Next(); break;
                    case 5:
                        if (elapsed < 9.5)
                        {
                            if (coach.DrillsStarted != finishedDrills || coach.Approaching) throw new Exception("Coach re-engaged during cooldown");
                            return;
                        }
                        Check(coach.ChargesStarted >= 2, "coach resumes whistle-run cycle during player cooldown");
                        var currentShadowLight = NearestFixture(coach.transform.position);
                        Check(currentShadowLight != null && currentShadowLight != startingShadowLight &&
                            currentShadowLight.shadows != LightShadows.None,
                            "character-shaped shadow lighting follows the moving coach");
                        movement.MovementLocked = true; Next(); break;
                    case 6:
                        if (!coach.Recovering || coach.CorridorStops < 1)
                        { if (elapsed > 25) throw new Exception("Coach never completed his next natural corridor run"); return; }
                        var appearance = new MaterialPropertyBlock(); coach.cutout.GetPropertyBlock(appearance);
                        Check(appearance.GetTexture("_BaseMap") == Resources.Load<Texture2D>("Art/CoachRecovery"),
                            "natural corridor stop displays bent-over recovery art");
                        patrolStart = coach.transform.position; heardWarning = false; Next(); break;
                    case 7:
                        heardWarning |= coach.Whistling;
                        if (!coach.Charging || Vector3.Distance(coach.transform.position, patrolStart) < 1f)
                        { if (elapsed > 10) throw new Exception("Coach did not resume after catching his breath"); return; }
                        Check(heardWarning && coach.ChargesStarted >= 3, "recovery is followed by another whistle and corridor run");
                        Check(errors == 0, "no runtime errors");
                        Finish(true); break;
                }
            }
            catch (Exception e) { File.AppendAllText(Report, "FAIL stage " + stage + ": " + e + "\n"); Finish(false); }
        }

        static void Finish(bool passed)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Error;
            Application.runInBackground = background;
            File.AppendAllText(Report, passed ? "PASS\n" : "FAIL\n");
            EditorApplication.isPlaying = false;
            if (SessionState.GetBool(BatchKey, false))
            { SessionState.SetBool(BatchKey, false); EditorApplication.Exit(passed ? 0 : 1); }
        }
    }
}

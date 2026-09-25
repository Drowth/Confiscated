using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// Play-mode smoke test of the loop that drives components directly (no synthetic input, so it is immune to
    /// editor focus). Armed via marker file Temp/run_smoketest; writes Temp/smoketest_result.txt and exits Play.
    /// Steps: agent on NavMesh -> noise lures caretaker into the corridor -> capture screenshots -> player placed in
    /// view -> chase -> caught -> restart -> phone pickup + window escape -> won -> restart -> playing again.
    /// </summary>
    [InitializeOnLoad]
    public static class GameplaySmokeTest
    {
        public const string MarkerPath = "Temp/run_smoketest";
        public const string ResultPath = "Temp/smoketest_result.txt";

        enum Step { Init, WaitInvestigate, WaitArrive, Capture, PlaceInView, WaitCaught, Restart1, WaitReload1, PhoneAndWindow, Restart2, WaitReload2, BallPickup, BallCarry, BallThrow, WaitBallNoise, Done }

        static int carryFrames, lastCarryFrame;
        static float maxCarryDrift;

        static int noiseCount;
        static string lastNoiseSource;
        static void OnNoise(Vector3 p, float r, string s) { noiseCount++; lastNoiseSource = s; }

        static Step step;
        static double stepStart;
        static int errors;
        static bool pass = true;
        static bool agentDiagLogged;

        static GameplaySmokeTest()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("Confiscated/Play Test/Arm Gameplay Smoke Test (runs on next Play)")]
        public static void Arm()
        {
            File.WriteAllText(MarkerPath, "armed");
            Debug.Log("[SmokeTest] Armed. Enter Play mode to run.");
        }

        [MenuItem("Confiscated/Play Test/Arm Football Carry Test (runs on next Play)")]
        public static void ArmFootballCarry()
        {
            File.WriteAllText(MarkerPath, "football");
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredPlayMode || !File.Exists(MarkerPath)) return;
            bool footballOnly = File.ReadAllText(MarkerPath) == "football";
            File.Delete(MarkerPath);
            if (!footballOnly && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == SchoolLayoutBuilder.ScenePath)
            {
                SchoolLayoutSmokeTest.StartRun();
                return;
            }
            if (!footballOnly && Object.FindFirstObjectByType<OfficeMission>() != null)
            {
                OfficeChapterSmokeTest.StartRun();
                return;
            }
            if (File.Exists(ResultPath)) File.Delete(ResultPath);
            errors = 0; pass = true; agentDiagLogged = false;
            Application.logMessageReceived += OnLog;
            step = footballOnly ? Step.BallPickup : Step.Init;
            stepStart = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            Application.runInBackground = true;
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) { errors++; Append("ERROR: " + msg); }
        }

        static void Append(string line) { File.AppendAllText(ResultPath, line + "\n"); }

        static void Check(bool ok, string what)
        {
            Append((ok ? "ok   " : "FAIL ") + what);
            if (!ok) pass = false;
        }

        static double T => EditorApplication.timeSinceStartup - stepStart;
        static void Next(Step s) { step = s; stepStart = EditorApplication.timeSinceStartup; }

        static CaretakerAI Caretaker => Object.FindFirstObjectByType<CaretakerAI>();
        static GameManager GM => GameManager.Instance;
        static PlayerInteractor Player => Object.FindFirstObjectByType<PlayerInteractor>();

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { Finish("play mode left unexpectedly"); return; }
            try { TickInner(); }
            catch (System.Exception e) { Append("EXCEPTION: " + e); pass = false; Finish("exception"); }
        }

        static void TickInner()
        {
            switch (step)
            {
                case Step.Init:
                    if (T < 1.5) return;
                    {
                        var ai = Caretaker;
                        Check(GM != null && GM.IsPlaying, "GameManager present and playing");
                        Check(ai != null, "CaretakerAI present");
                        if (ai == null) { Finish("no caretaker"); return; }
                        var agent = ai.GetComponent<NavMeshAgent>();
                        Check(agent.isOnNavMesh, "caretaker agent is on the NavMesh");
                        Check(ai.Current == CaretakerAI.State.Patrol, "caretaker starts in Patrol (was " + ai.Current + ")");
                        Check(HudController.Instance != null && !HudController.Instance.phoneImage.gameObject.activeSelf, "phone HUD hidden before pickup");
                        // Collect the phone now so the runtime reference screenshot shows the held-phone HUD.
                        var firstPickup = Object.FindFirstObjectByType<PhonePickup>();
                        if (firstPickup != null) firstPickup.Interact(Player);
                        Check(Player != null && Player.HasPhone, "phone pickup via Interact()");
                        // Tuck the player into the office's south-east corner (behind the caretaker's exit path).
                        Teleport(new Vector3(1.0f, 0f, -1.4f), 0f);
                        NoiseEvents.Emit(new Vector3(0f, 0f, 4.4f), 30f, "test");
                        Next(Step.WaitInvestigate);
                    }
                    break;

                case Step.WaitInvestigate:
                    if (Caretaker.Current == CaretakerAI.State.Investigate) { Check(true, "noise -> Investigate"); Next(Step.WaitArrive); }
                    else if (T > 1.0) { Check(false, "noise -> Investigate (state " + Caretaker.Current + ")"); Next(Step.WaitArrive); }
                    break;

                case Step.WaitArrive:
                    {
                        var ai = Caretaker;
                        var agent = ai.GetComponent<NavMeshAgent>();
                        if (!agentDiagLogged && T > 1.0)
                        {
                            agentDiagLogged = true;
                            Append(string.Format("diag agent: pos={0} dest={1} hasPath={2} pathPending={3} status={4} remaining={5:F2} vel={6:F2} speed={7} stopped={8} onNavMesh={9} state={10}",
                                ai.transform.position, agent.destination, agent.hasPath, agent.pathPending, agent.pathStatus,
                                agent.remainingDistance, agent.velocity.magnitude, agent.speed, agent.isStopped, agent.isOnNavMesh, ai.Current));
                            var p = Player;
                            Append(string.Format("diag player: pos={0} yaw={1:F0} prompt='{2}' objective='{3}' phoneHud={4}",
                                p.transform.position, p.transform.eulerAngles.y, HudController.Instance.promptText.text.Replace("\n", " "),
                                HudController.Instance.objectiveText.text.Replace("\n", " "), HudController.Instance.phoneImage.gameObject.activeSelf));
                        }
                        float d = Vector3.Distance(ai.transform.position, new Vector3(0f, 0f, 4.4f));
                        if (d < 0.8f || T > 10.0)
                        {
                            Check(d < 0.8f, string.Format("caretaker walked to the noise (dist {0:F2} m after {1:F1} s, state {2})", d, T, ai.Current));
                            Next(Step.Capture);
                        }
                    }
                    break;

                case Step.Capture:
                    if (T < 0.6) return;
                    VisualTestSceneBuilder.CaptureScreenshots();
                    Check(File.Exists(Path.GetFullPath("../Screenshots/01_ReferenceView.png")), "runtime screenshots captured");
                    Next(Step.PlaceInView);
                    break;

                case Step.PlaceInView:
                    // Put the player 3 m down the corridor from the caretaker, in his cone as he looks around.
                    Teleport(new Vector3(0f, 0f, 7.4f), 180f);
                    Next(Step.WaitCaught);
                    break;

                case Step.WaitCaught:
                    if (GM.Current == GameManager.State.Caught)
                    {
                        Check(true, string.Format("caretaker saw, chased and caught the player ({0:F1} s)", T));
                        Check(HudController.Instance.overlay.activeSelf, "caught overlay shown");
                        Next(Step.Restart1);
                    }
                    else if (T > 12.0)
                    {
                        Check(false, "caught within 12 s (state " + Caretaker.Current + ", suspicion " + Caretaker.Suspicion.ToString("F2") + ", game " + GM.Current + ")");
                        Next(Step.Restart1);
                    }
                    break;

                case Step.Restart1:
                    if (T < 0.5) return;
                    GM.Restart();
                    Next(Step.WaitReload1);
                    break;

                case Step.WaitReload1:
                    if (T < 1.5) return;
                    Check(GM != null && GM.IsPlaying, "restart after caught -> playing again");
                    Check(Caretaker != null && Caretaker.Current == CaretakerAI.State.Patrol, "caretaker back on patrol after restart");
                    Check(Player != null && !Player.HasPhone, "player has no phone after restart");
                    Next(Step.PhoneAndWindow);
                    break;

                case Step.PhoneAndWindow:
                    {
                        var p = Player;
                        var pickup = Object.FindFirstObjectByType<PhonePickup>();
                        var window = Object.FindFirstObjectByType<EscapeWindow>();
                        Check(pickup != null && window != null, "phone pickup and escape window present");
                        Check(window != null && !window.CanInteract(p), "window refuses escape without the phone");
                        pickup.Interact(p);
                        Check(p.HasPhone, "phone collected");
                        Check(HudController.Instance.phoneImage.gameObject.activeSelf, "phone HUD visible after pickup");
                        Check(window.CanInteract(p), "window allows escape with the phone");
                        window.Interact(p);
                        Check(GM.Current == GameManager.State.Won, "escape -> Won");
                        Next(Step.Restart2);
                    }
                    break;

                case Step.Restart2:
                    if (T < 0.5) return;
                    GM.Restart();
                    Next(Step.WaitReload2);
                    break;

                case Step.WaitReload2:
                    if (T < 1.5) return;
                    Check(GM != null && GM.IsPlaying, "restart after win -> playing again");
                    Next(Step.BallPickup);
                    break;

                case Step.BallPickup:
                    if (T < 0.2) return;
                    {
                        var ball = Object.FindFirstObjectByType<ThrowableBall>();
                        var p = Player;
                        Check(ball != null, "football present after restart");
                        if (ball == null) { Finish("no ball"); return; }
                        Teleport(new Vector3(-0.6f, 0f, 8.0f), 0f); // face down the corridor (+Z)
                        Check(ball.CanInteract(p), "football can be picked up");
                        ball.Interact(p);
                        Check(p.HeldBall == ball && ball.transform.parent == p.HoldAnchor, "football held at the hold anchor");
                        Caretaker.Freeze();
                        p.GetComponent<FirstPersonController>().enabled = false;
                        carryFrames = 0; lastCarryFrame = -1; maxCarryDrift = 0f;
                        Next(Step.BallCarry);
                    }
                    break;

                case Step.BallCarry:
                    {
                        if (lastCarryFrame == Time.frameCount) return;
                        lastCarryFrame = Time.frameCount;
                        var p = Player;
                        var ball = p.HeldBall;
                        maxCarryDrift = Mathf.Max(maxCarryDrift, Vector3.Distance(ball.transform.position, p.HoldAnchor.position));
                        if (carryFrames++ < 120)
                        {
                            p.GetComponent<CharacterController>().Move(new Vector3(0f, 0f, 0.015f));
                            p.transform.rotation = Quaternion.Euler(0f, carryFrames * 3f, 0f);
                            p.ViewCamera.transform.localRotation = Quaternion.Euler(Mathf.Sin(carryFrames * 0.1f) * 30f, 0f, 0f);
                            return;
                        }
                        Check(maxCarryDrift < 0.002f, "football follows movement, full turn and camera pitch across 120 frames; max drift=" + maxCarryDrift);
                        Check(ball.GetComponent<Rigidbody>().isKinematic && !ball.GetComponent<Collider>().enabled, "held ball stays out of collision simulation");
                        Teleport(new Vector3(-0.6f, 0f, 8f), 0f);
                        p.ViewCamera.transform.localRotation = Quaternion.identity;
                        Next(Step.BallThrow);
                    }
                    break;

                case Step.BallThrow:
                    if (T < 0.1) return;
                    {
                        var p = Player;
                        var ball = p.HeldBall;
                        noiseCount = 0; lastNoiseSource = null;
                        NoiseEvents.OnNoise += OnNoise;
                        var ai = Caretaker;
                        ai.enabled = false; // keep the caretaker out of it; we only test the ball physics + noise here
                        // Same throw the interactor performs on Attack: camera forward plus a little lift.
                        var releasePosition = p.HoldAnchor.position;
                        ball.Throw(p.ViewCamera.transform.forward * 9f + Vector3.up * 2.2f);
                        Check(p.HeldBall == null && ball.transform.parent == null, "football released on throw");
                        Check(Vector3.Distance(ball.GetComponent<Rigidbody>().position, releasePosition) < 0.002f, "throw starts from current hand position");
                        Check(ball.GetComponent<Rigidbody>().interpolation == RigidbodyInterpolation.Interpolate && !ball.GetComponent<Rigidbody>().isKinematic && ball.GetComponent<Collider>().enabled, "throw restores interpolated physics and collisions");
                        Next(Step.WaitBallNoise);
                    }
                    break;

                case Step.WaitBallNoise:
                    if (noiseCount > 0 || T > 4.0)
                    {
                        NoiseEvents.OnNoise -= OnNoise;
                        var ball = Object.FindFirstObjectByType<ThrowableBall>();
                        Check(noiseCount > 0 && lastNoiseSource == "football", string.Format("thrown football bounced and made noise ({0} events, source {1})", noiseCount, lastNoiseSource));
                        Check(ball != null && ball.transform.position.z > 9f, string.Format("football travelled down the corridor (z={0:F1})", ball != null ? ball.transform.position.z : 0f));
                        Finish("complete");
                    }
                    break;
            }
        }

        static void Teleport(Vector3 pos, float yaw)
        {
            var p = Player;
            var cc = p.GetComponent<CharacterController>();
            cc.enabled = false;
            p.transform.position = pos;
            p.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cc.enabled = true;
        }

        static void Finish(string why)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            NoiseEvents.OnNoise -= OnNoise;
            Check(errors == 0, "no console errors during the run (" + errors + ")");
            Append((pass ? "PASS" : "FAIL") + " (" + why + ")");
            Append("DONE");
            EditorApplication.isPlaying = false;
        }
    }
}

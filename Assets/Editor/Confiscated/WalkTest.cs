using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// Scripted play-mode smoke test for the first-person controller: feeds synthetic keyboard/mouse events
    /// through the Input System and checks that the player is grounded, walks, is stopped by collision,
    /// and turns with mouse look. Run from Confiscated > Play Test > Run Walk Test while in Play mode.
    /// Results are logged to the console with the [WalkTest] prefix.
    /// </summary>
    public static class WalkTest
    {
        enum Step { Settle, Forward, ReleaseW, Backward, Look, Done }

        static Step step;
        static double stepStart;
        static Transform player;
        static CharacterController controller;
        static Vector3 posAfterSettle, posAfterForward, posAfterBackward;
        static float yawBeforeLook;
        static bool grounded;
        static bool diagnosticsLogged;

        /// <summary>Snapshot of the input plumbing one second into the W press, for diagnosing dropped input.</summary>
        static void LogDiagnostics(Keyboard kb)
        {
            var asset = InputSystem.actions;
            var move = asset != null ? asset.FindAction("Player/Move", false) : null;
            var fpc = player != null ? player.GetComponent<Confiscated.FirstPersonController>() : null;
            Debug.Log(string.Format(
                "[WalkTest] diag: kb.enabled={0} wKey.isPressed={1} Application.isFocused={2} bgBehavior={3} editorBehavior={4} " +
                "projectWideAsset={5} assetEnabled={6} moveAction={7} moveEnabled={8} moveValue={9} fpcEnabled={10} cc.enabled={11}",
                kb.enabled, kb.wKey.isPressed, Application.isFocused, InputSystem.settings.backgroundBehavior,
                InputSystem.settings.editorInputBehaviorInPlayMode,
                asset != null ? asset.name : "null", asset != null && asset.enabled,
                move != null ? move.name : "null", move != null && move.enabled, move != null ? move.ReadValue<Vector2>() : Vector2.zero,
                fpc != null && fpc.enabled, controller != null && controller.enabled));
        }

        /// <summary>Raised when the test finishes (pass, fail, or abort).</summary>
        public static event System.Action Completed;

        [MenuItem("Confiscated/Play Test/Run Walk Test")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[WalkTest] Enter Play mode first.");
                return;
            }
            var go = GameObject.Find("Player");
            if (go == null) { Debug.LogError("[WalkTest] No 'Player' in scene."); return; }
            player = go.transform;
            controller = go.GetComponent<CharacterController>();
            step = Step.Settle;
            diagnosticsLogged = false;
            stepStart = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("[WalkTest] Started at " + player.position);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || player == null) { Finish(); return; }
            double t = EditorApplication.timeSinceStartup - stepStart;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) { Debug.LogError("[WalkTest] No keyboard device."); Finish(); return; }

            switch (step)
            {
                case Step.Settle:
                    if (t > 0.8)
                    {
                        posAfterSettle = player.position;
                        grounded = controller != null && controller.isGrounded;
                        InputSystem.QueueStateEvent(kb, new KeyboardState(Key.W));
                        Next(Step.Forward);
                    }
                    break;

                case Step.Forward:
                    if (!diagnosticsLogged && t > 1.0)
                    {
                        diagnosticsLogged = true;
                        LogDiagnostics(kb);
                    }
                    // 3 s of walking = ~7 m unobstructed; the caretaker capsule at z=2.7 should stop us near z~2.
                    if (t > 3.0)
                    {
                        InputSystem.QueueStateEvent(kb, new KeyboardState());
                        Next(Step.ReleaseW);
                    }
                    break;

                case Step.ReleaseW:
                    // Give the release its own frame(s) before pressing S; queuing both in one frame lost the S press.
                    if (t > 0.3)
                    {
                        posAfterForward = player.position;
                        InputSystem.QueueStateEvent(kb, new KeyboardState(Key.S));
                        Next(Step.Backward);
                    }
                    break;

                case Step.Backward:
                    if (t > 1.0)
                    {
                        InputSystem.QueueStateEvent(kb, new KeyboardState());
                        posAfterBackward = player.position;
                        yawBeforeLook = player.eulerAngles.y;
                        if (mouse != null) InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(300f, 0f));
                        Next(Step.Look);
                    }
                    break;

                case Step.Look:
                    if (t > 0.5)
                    {
                        float yawAfter = player.eulerAngles.y;
                        Report(yawAfter);
                        Finish();
                    }
                    break;
            }
        }

        static void Next(Step s) { step = s; stepStart = EditorApplication.timeSinceStartup; }

        static void Report(float yawAfter)
        {
            // Distances are measured along the player's facing direction (spawn faces -Z toward the office).
            Vector3 f = player.forward; f.y = 0f; f.Normalize();
            float fwd = Vector3.Dot(posAfterForward - posAfterSettle, f);
            float back = Vector3.Dot(posAfterForward - posAfterBackward, f);
            float yawDelta = Mathf.DeltaAngle(yawBeforeLook, yawAfter);
            bool okGround = grounded && Mathf.Abs(posAfterSettle.y) < 0.15f;
            bool okForward = fwd > 1.5f;
            // 3 s at 2.4 m/s would be 7.2 m; the office south wall stops the player after ~5.3 m.
            bool okBlocked = fwd > 4.3f && fwd < 6.2f;
            bool okBack = back > 1.0f;
            bool okLook = Mathf.Abs(yawDelta) > 5f;
            bool pass = okGround && okForward && okBlocked && okBack && okLook;
            Debug.Log(string.Format(
                "[WalkTest] {0}\n grounded={1} y={2:F2} (ok={3})\n forward {4:F2} m in 3 s, end pos {5} (moved ok={6}, blocked by office wall ok={7})\n backward {8:F2} m in 1 s (ok={9})\n mouse look yaw delta={10:F1} deg (ok={11})",
                pass ? "PASS" : "FAIL", grounded, posAfterSettle.y, okGround, fwd, posAfterForward, okForward, okBlocked, back, okBack, yawDelta, okLook));
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            var kb = Keyboard.current;
            if (kb != null) InputSystem.QueueStateEvent(kb, new KeyboardState());
            var handlers = Completed;
            Completed = null;
            handlers?.Invoke();
        }
    }
}

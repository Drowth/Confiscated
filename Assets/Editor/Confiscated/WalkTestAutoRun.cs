using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// Headless driver for WalkTest: if the marker file Temp/run_walktest exists when Play mode is entered,
    /// the walk test runs automatically and its report is also written to Temp/walktest_result.txt.
    /// Lets the test run without the editor bridge being responsive during Play mode.
    /// </summary>
    [InitializeOnLoad]
    public static class WalkTestAutoRun
    {
        public const string MarkerPath = "Temp/run_walktest";
        public const string ResultPath = "Temp/walktest_result.txt";

        static InputSettings.EditorInputBehaviorInPlayMode savedInputBehavior;
        static InputSettings.BackgroundBehavior savedBackgroundBehavior;
        static bool savedRunInBackground;
        static bool inputBehaviorChanged;

        static WalkTestAutoRun()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!File.Exists(MarkerPath)) return;
            File.Delete(MarkerPath);
            if (File.Exists(ResultPath)) File.Delete(ResultPath);
            Application.logMessageReceived += CaptureLog;
            WalkTest.Completed += OnCompleted;

            // Synthetic keyboard/mouse events are dropped unless the Game view has focus (default Input System
            // behaviour in the editor). Focus it and, for the duration of the test, route all input to the game.
            FocusGameView();
            savedInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            savedBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            savedRunInBackground = Application.runInBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // When the Unity window is not the foreground OS window, the default background behaviour disables
            // keyboard/mouse devices and drops their events. Ignore focus for the duration of the test.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground = true;
            inputBehaviorChanged = true;
            File.AppendAllText(ResultPath, "Info: editorInputBehavior was " + savedInputBehavior + ", backgroundBehavior was "
                + savedBackgroundBehavior + ", runInBackground was " + savedRunInBackground
                + ", Application.isFocused=" + Application.isFocused + "\n");

            EditorApplication.delayCall += () => WalkTest.Run();
        }

        static void FocusGameView()
        {
            var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return;
            var window = EditorWindow.GetWindow(gameViewType, false, null, true);
            if (window != null) window.Focus();
        }

        static void RestoreInputBehavior()
        {
            if (!inputBehaviorChanged) return;
            InputSystem.settings.editorInputBehaviorInPlayMode = savedInputBehavior;
            InputSystem.settings.backgroundBehavior = savedBackgroundBehavior;
            Application.runInBackground = savedRunInBackground;
            inputBehaviorChanged = false;
        }

        static void CaptureLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || message.StartsWith("[WalkTest]"))
                File.AppendAllText(ResultPath, type + ": " + message + "\n");
        }

        static void OnCompleted()
        {
            WalkTest.Completed -= OnCompleted;
            Application.logMessageReceived -= CaptureLog;
            RestoreInputBehavior();
            File.AppendAllText(ResultPath, "DONE\n");
            EditorApplication.isPlaying = false;
        }

        [MenuItem("Confiscated/Play Test/Arm Auto Walk Test (runs on next Play)")]
        public static void Arm()
        {
            File.WriteAllText(MarkerPath, "armed");
            Debug.Log("[WalkTest] Armed. Enter Play mode to run.");
        }
    }
}

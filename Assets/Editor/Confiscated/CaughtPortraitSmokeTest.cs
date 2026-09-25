using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Gets caught, stays on the results screen, and checks the modelled portrait is alive: turning, pupils rolling.</summary>
    [InitializeOnLoad]
    public static class CaughtPortraitSmokeTest
    {
        const string Marker = "Temp/run_caught_portrait", Report = "../Docs/CaughtPortrait_Validation.txt", Shots = "D:/Confiscated/Docs/CaughtHead/";
        static int stage, errors, lastFrame, shot; static bool background; static double started, at;
        static Quaternion firstTurn; static Vector3 firstPupil;
        static CaughtPortraitSmokeTest() { EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode && File.Exists(Marker)) { File.Delete(Marker); Begin(); } }; }
        [MenuItem("Confiscated/Caught Sequence/Arm Results Portrait Test")]
        public static void Arm() { File.WriteAllText(Marker, "armed"); }
        static void Begin()
        {
            stage = errors = shot = 0; lastFrame = -1; started = at = EditorApplication.timeSinceStartup;
            background = Application.runInBackground; Application.runInBackground = true; Directory.CreateDirectory(Shots);
            File.WriteAllText(Report, "Caught results portrait: a real catch, then the results screen held open.\n");
            Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        static void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception) { errors++; File.AppendAllText(Report, "ERROR " + message + "\n"); } }
        static void Need(bool value, string label) { File.AppendAllText(Report, (value ? "ok   " : "FAIL ") + label + "\n"); if (!value) throw new Exception(label); }
        static void Next(int value) { stage = value; at = EditorApplication.timeSinceStartup; }
        static Transform Head => GameObject.Find("Caught portrait stage")?.GetComponentInChildren<CaretakerHeadEyes>()?.transform;

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { Finish(false); return; }
            if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
            double now = EditorApplication.timeSinceStartup;
            try
            {
                if (now - started > 90) throw new Exception("Timeout at stage " + stage);
                if (stage == 0)
                {
                    if (SchoolTitleMenu.IsActive) { SchoolTitleMenu.Instance.StartGame(); SchoolTitleMenu.Instance.SkipIntro(); return; }
                    if (ComicDialogue.IsActive) { ComicDialogue.Instance.Advance(); return; }
                    if (now - at < 1.5 || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
                    var run = Object.FindFirstObjectByType<SchoolRunController>();
                    GameManager.Instance.Caught(run.caretaker);
                    Need(GameManager.Instance.Current == GameManager.State.Caught, "a catch by the caretaker ends the run");
                    Next(1); return;
                }
                if (stage == 1)
                {
                    if (now - at < CaretakerCatchScare.Duration + .35f) return;
                    var portrait = GameObject.Find("Caught face portrait");
                    Need(portrait != null && portrait.GetComponent<CaughtPortraitHead>() != null, "results portrait is the modelled head, not the flat picture");
                    Need(portrait.GetComponent<RawImage>().texture is RenderTexture, "portrait shows a live render");
                    Need(Head != null, "portrait stage exists with the head and its eyes");
                    Need(GameObject.Find("Caretaker caught close-up") == null, "lunge has cleared, leaving the results screen");
                    firstTurn = Head.localRotation; firstPupil = Head.GetComponent<CaretakerHeadEyes>().pupils[0].localPosition;
                    Next(2); return;
                }
                if (stage == 2)
                {
                    double elapsed = now - at; float[] moments = { .3f, 1.1f, 1.9f, 2.7f };
                    if (shot < moments.Length && elapsed >= moments[shot]) { ScreenCapture.CaptureScreenshot(Shots + "results_" + shot + ".png"); shot++; }
                    if (elapsed < 3.2) return;
                    Need(Quaternion.Angle(firstTurn, Head.localRotation) > 3, "head is looking from side to side while the game is stopped");
                    Need(Vector3.Distance(firstPupil, Head.GetComponent<CaretakerHeadEyes>().pupils[0].localPosition) > .002f, "pupils are rolling");
                    Need(errors == 0, "no runtime errors"); Finish(true);
                }
            }
            catch (Exception e) { File.AppendAllText(Report, "FAIL stage " + stage + ": " + e.Message + "\n"); Finish(false); }
        }
        static void Finish(bool success)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log; Time.timeScale = 1; Application.runInBackground = background;
            File.AppendAllText(Report, success ? "PASS\n" : "FAIL\n"); EditorApplication.isPlaying = false;
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Import settings for the hunt-face artwork, and the Play-mode test that photographs the change.</summary>
    [InitializeOnLoad]
    public static class HuntFacesSetup
    {
        static readonly string[] Names = { "T_Mr_Reed", "T_DinnerLady", "T_Student_Seated_Views", "T_Student_Girl_Seated_Views" };

        /// <summary>Each hunt texture must import exactly like the drawing it replaces, or the swap would shift or blur.</summary>
        [MenuItem("Confiscated/Chase Feedback/Match Hunt Face Import Settings")]
        public static void MatchImports()
        {
            AssetDatabase.Refresh();
            foreach (string name in Names)
            {
                var source = (TextureImporter)AssetImporter.GetAtPath("Assets/Art/Textures/" + name + ".png");
                var target = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/Art/Hunt/" + name + "_Hunt.png");
                if (source == null || target == null) throw new InvalidOperationException("Missing artwork for " + name);
                var settings = new TextureImporterSettings(); source.ReadTextureSettings(settings); target.SetTextureSettings(settings);
                target.maxTextureSize = source.maxTextureSize; target.textureCompression = source.textureCompression; target.SetPlatformTextureSettings(source.GetDefaultPlatformTextureSettings());
                target.SaveAndReimport();
            }
            Debug.Log("[HuntFaces] Import settings matched for " + Names.Length + " hunt textures.");
        }

        // ---- Play-mode test -------------------------------------------------------------------------------------------
        const string Marker = "Temp/run_hunt_faces", Report = "../Docs/HuntFaces_Validation.txt", Shots = "D:/Confiscated/Docs/HuntFaces/";
        static int stage, errors, lastFrame; static bool background; static double started, at;
        static HuntFacesSetup() { EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode && File.Exists(Marker)) { File.Delete(Marker); Begin(); } }; }
        [MenuItem("Confiscated/Chase Feedback/Arm Hunt Faces Test")]
        public static void Arm() { File.WriteAllText(Marker, "armed"); }
        static void Begin()
        {
            stage = errors = 0; lastFrame = -1; started = at = EditorApplication.timeSinceStartup; Directory.CreateDirectory(Shots);
            background = Application.runInBackground; Application.runInBackground = true;
            File.WriteAllText(Report, "Hunt faces: artwork follows the caretaker's real state; Mr Reed follows his own.\n");
            Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        static void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception) { errors++; File.AppendAllText(Report, "ERROR " + message + "\n"); } }
        static void Need(bool value, string label) { File.AppendAllText(Report, (value ? "ok   " : "FAIL ") + label + "\n"); if (!value) throw new Exception(label); }
        static void Next(int value) { stage = value; at = EditorApplication.timeSinceStartup; }
        static SchoolRunController R => Object.FindFirstObjectByType<SchoolRunController>();
        static HuntFaces Faces => R.GetComponent<HuntFaces>();
        static FirstPersonController F => R.period.Player.GetComponent<FirstPersonController>();
        static void Force(string method) { typeof(CaretakerAI).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(R.caretaker, null); }
        static void Stand(Vector3 position, Vector3 lookAt)
        {
            F.Controller.enabled = false; Vector3 flat = lookAt - position; flat.y = 0;
            F.transform.SetPositionAndRotation(position, Quaternion.LookRotation(flat)); F.Controller.enabled = true; F.LookLocked = true;
            R.period.Player.ViewCamera.transform.localRotation = Quaternion.identity; Physics.SyncTransforms();
        }
        static bool All(bool teacher, bool hunt) => Faces.Renderers(teacher).All(r => Faces.Showing(r, hunt));

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { Finish(false); return; }
            if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
            double now = EditorApplication.timeSinceStartup;
            try
            {
                if (now - started > 120) throw new Exception("Timeout at stage " + stage);
                if (SchoolTitleMenu.IsActive) { SchoolTitleMenu.Instance.StartGame(); SchoolTitleMenu.Instance.SkipIntro(); return; }
                if (ComicDialogue.IsActive) { ComicDialogue.Instance.Advance(); return; }
                switch (stage)
                {
                    case 0:
                        if (now - at < 1.5) return;
                        Need(Faces != null && Faces.CrowdCount >= 3 && Faces.TeacherCount >= 1, "found " + Faces.CrowdCount + " pupils/dinner lady and " + Faces.TeacherCount + " Mr Reed with hunt artwork");
                        Need(!Faces.CrowdTurned && !Faces.TeacherTurned && All(false, false) && All(true, false), "everyone is their ordinary self during the lesson");
                        R.period.PrepareChaseRetry(); R.PrepareChaseRetry(); R.caretaker.Freeze();
                        // In front of the Year 6 class, looking back at the pupils' faces.
                        // Close in front of the two pupils, at a child's eye height, looking back at their faces.
                        var pupils = Object.FindObjectsByType<SeatedStudent>(FindObjectsSortMode.None); Vector3 middle = Vector3.zero, facing = Vector3.zero;
                        foreach (var pupil in pupils) { middle += pupil.transform.position / pupils.Length; facing += pupil.transform.forward / pupils.Length; }
                        middle.y = 0; facing.y = 0; Stand(middle + facing.normalized * 2.4f, middle); Next(1); break;
                    case 1:
                        if (now - at < .8) return;
                        Need(!Faces.CrowdTurned, "run under way but nobody hunting: faces still ordinary");
                        ScreenCapture.CaptureScreenshot(Shots + "class_1_ordinary.png"); Next(2); break;
                    case 2:
                        if (now - at < .4) return;
                        R.caretaker.ResumeAfterDetention(0); Force("EnterChase"); Next(3); break;
                    case 3:
                        if (R.caretaker.Current != CaretakerAI.State.Chase) Force("EnterChase");
                        if (now - at < 1.1) return;
                        Need(Faces.CrowdTurned && All(false, true), "caretaker hunting: every pupil and the dinner lady shows the grinning artwork");
                        Need(!Faces.TeacherTurned && All(true, false), "Mr Reed has not joined the hunt, so he is unchanged");
                        ScreenCapture.CaptureScreenshot(Shots + "class_2_hunted.png"); Next(4); break;
                    case 4:
                        if (R.caretaker.Current != CaretakerAI.State.Chase) Force("EnterChase");
                        if (now - at < .4) return;
                        var lady = Object.FindFirstObjectByType<DinnerTrolleyPatrol>();
                        Stand(lady.transform.position + new Vector3(-3.2f, 0, 0) - Vector3.up * lady.transform.position.y, lady.transform.position); Next(5); break;
                    case 5:
                        if (R.caretaker.Current != CaretakerAI.State.Chase) Force("EnterChase");
                        if (now - at < .6) return;
                        ScreenCapture.CaptureScreenshot(Shots + "dinner_lady_hunted.png"); Next(6); break;
                    case 6:
                        if (now - at < .3) return; R.caretaker.Freeze(); Next(7); break;
                    case 7:
                        if (now - at < 1.8) return;
                        Need(!Faces.CrowdTurned && All(false, false), "he gives up: everyone is ordinary again");
                        // Third belonging: Mr Reed joins the hunt.
                        R.CageOpen = true; R.Recover(0); R.Recover(1); R.Recover(2); R.caretaker.Freeze();
                        Next(8); break;
                    case 8:
                        if (now - at < .5) return;
                        Need(R.Count == 3 && R.secondStaff.enabled && Faces.TeacherTurned && All(true, true), "third belonging: Mr Reed joins the hunt wearing his hunting face");
                        R.secondStaff.Freeze(); var reed = Faces.Renderers(true).First().transform.position;
                        Stand(new Vector3(reed.x + 2.6f, 0, reed.z + .4f), reed); Next(9); break;
                    case 9:
                        if (now - at < .6) return;
                        Need(Faces.TeacherTurned, "he keeps that face for the rest of the run");
                        ScreenCapture.CaptureScreenshot(Shots + "mr_reed_hunting.png"); Next(10); break;
                    case 10:
                        if (now - at < .4) return;
                        R.TakeTool(AccessToolPickup.Tool.StoreKey); R.Recover(3); R.Recover(4); R.caretaker.Freeze(); R.secondStaff.Freeze(); Next(11); break;
                    case 11:
                        if (now - at < .6) return;
                        Need(R.Count == 5 && Faces.CrowdTurned && All(false, true), "all five recovered: the whole school stays turned for the dash to the exit, hunted or not");
                        Need(errors == 0, "no runtime errors"); Finish(true); break;
                }
            }
            catch (Exception e) { File.AppendAllText(Report, "FAIL stage " + stage + ": " + (e.InnerException ?? e).Message + "\n"); Finish(false); }
        }
        static void Finish(bool success)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log; Time.timeScale = 1; Application.runInBackground = background;
            File.AppendAllText(Report, success ? "PASS\n" : "FAIL\n"); EditorApplication.isPlaying = false;
        }
    }
}

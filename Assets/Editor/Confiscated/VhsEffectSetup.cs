using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Installs the hunt VHS full-screen pass on every URP renderer in the project, and tests it in Play.</summary>
    [InitializeOnLoad]
    public static class VhsEffectSetup
    {
        const string FeatureName = "VHS Hunt", MaterialPath = "Assets/Art/Materials/M_VHS_Hunt.mat";

        [MenuItem("Confiscated/Chase Feedback/Install VHS Hunt Effect")]
        public static void Install()
        {
            var shader = Shader.Find("Confiscated/VHS Hunt"); if (shader == null) throw new InvalidOperationException("VHS Hunt shader missing or failed to compile.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
            material.shader = shader; EditorUtility.SetDirty(material);
            int added = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid)); if (data == null) continue;
                var existing = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f != null && f.name == FeatureName);
                if (existing == null)
                {
                    existing = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>(); existing.name = FeatureName;
                    AssetDatabase.AddObjectToAsset(existing, data);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(existing, out _, out long localId);
                    // The same two lists the renderer inspector maintains when a feature is added by hand.
                    var serialized = new SerializedObject(data);
                    var features = serialized.FindProperty("m_RendererFeatures"); features.arraySize++; features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = existing;
                    var map = serialized.FindProperty("m_RendererFeatureMap"); map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                    serialized.ApplyModifiedPropertiesWithoutUndo(); added++;
                }
                existing.passMaterial = material; existing.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
                existing.fetchColorBuffer = true; existing.requirements = ScriptableRenderPassInput.None; existing.passIndex = 0;
                EditorUtility.SetDirty(existing); EditorUtility.SetDirty(data);
            }
            Shader.SetGlobalFloat("_VhsIntensity", 0);
            AssetDatabase.SaveAssets();
            Debug.Log("[VHS] Hunt effect installed on the project's URP renderers (" + added + " newly added).");
        }

        [MenuItem("Confiscated/Chase Feedback/Remove VHS Hunt Effect")]
        public static void Remove()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid)); if (data == null) continue;
                var serialized = new SerializedObject(data); var features = serialized.FindProperty("m_RendererFeatures"); var map = serialized.FindProperty("m_RendererFeatureMap");
                for (int i = features.arraySize - 1; i >= 0; i--)
                {
                    var feature = features.GetArrayElementAtIndex(i).objectReferenceValue as FullScreenPassRendererFeature; if (feature == null || feature.name != FeatureName) continue;
                    features.GetArrayElementAtIndex(i).objectReferenceValue = null; features.DeleteArrayElementAtIndex(i); if (i < map.arraySize) map.DeleteArrayElementAtIndex(i);
                    serialized.ApplyModifiedPropertiesWithoutUndo(); Object.DestroyImmediate(feature, true);
                }
                EditorUtility.SetDirty(data);
            }
            Shader.SetGlobalFloat("_VhsIntensity", 0); AssetDatabase.SaveAssets();
        }

        // ---- Play-mode test -------------------------------------------------------------------------------------------
        const string Marker = "Temp/run_vhs_hunt", Report = "../Docs/VhsHunt_Validation.txt", Shots = "D:/Confiscated/Docs/VhsHunt/";
        static int stage, errors, lastFrame; static bool background; static double started, at;
        static VhsEffectSetup() { EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode && File.Exists(Marker)) { File.Delete(Marker); Begin(); } }; }
        [MenuItem("Confiscated/Chase Feedback/Arm VHS Hunt Test")]
        public static void Arm() { File.WriteAllText(Marker, "armed"); }
        static void Begin()
        {
            stage = errors = 0; lastFrame = -1; started = at = EditorApplication.timeSinceStartup; Directory.CreateDirectory(Shots);
            background = Application.runInBackground; Application.runInBackground = true;
            File.WriteAllText(Report, "VHS hunt effect: tied to the caretaker's real state, clean otherwise.\n");
            Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        static void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception) { errors++; File.AppendAllText(Report, "ERROR " + message + "\n"); } }
        static void Need(bool value, string label) { File.AppendAllText(Report, (value ? "ok   " : "FAIL ") + label + "\n"); if (!value) throw new Exception(label); }
        static void Next(int value) { stage = value; at = EditorApplication.timeSinceStartup; }
        static SchoolRunController R => Object.FindFirstObjectByType<SchoolRunController>();
        static HuntVhsEffect Effect => R.GetComponent<HuntVhsEffect>();
        static float Global => Shader.GetGlobalFloat("_VhsIntensity");
        static void Force(string method) { typeof(CaretakerAI).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(R.caretaker, null); }

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
                        Need(Effect != null, "effect driver is attached to the run");
                        Need(HuntVhsEffect.TargetFor(CaretakerAI.State.Patrol, 1, .75f, 0) == 0 && HuntVhsEffect.TargetFor(CaretakerAI.State.Frozen, 1, .75f, 0) == 0 && HuntVhsEffect.TargetFor(CaretakerAI.State.Investigate, 1, .75f, 0) == 0, "patrolling, frozen and checking a noise leave the picture clean");
                        Need(Effect.chase==.28f&&Effect.search==.1f,"chase distortion is restrained and search is lighter");
                        Need(Effect.Level == 0 && Global == 0, "picture is clean during the lesson");
                        ScreenCapture.CaptureScreenshot(Shots + "clean.png");
                        // The run must be live for him to hunt; start it the way a chase retry does.
                        R.period.PrepareChaseRetry(); R.PrepareChaseRetry(); Next(1); break;
                    case 1:
                        if (now - at < 1) return;
                        R.caretaker.ResumeAfterDetention(0); Force("EnterChase"); Next(2); break;
                    case 2:
                        if (R.caretaker.Current != CaretakerAI.State.Chase) Force("EnterChase");
                        if (now - at < .9) return;
                        Need(R.caretaker.Current == CaretakerAI.State.Chase && Mathf.Abs(Effect.Level-.28f)<.02f && Global > .05f&&Global<.31f, "chase: readable distortion (level " + Effect.Level.ToString("F2") + ", shader " + Global.ToString("F2") + ")");
                        ScreenCapture.CaptureScreenshot(Shots + "chase.png"); Next(3); break;
                    case 3:
                        if (now - at < .3) return; Force("EnterSearch"); Next(4); break;
                    case 4:
                        if (R.caretaker.Current != CaretakerAI.State.Search) Force("EnterSearch");
                        if (now - at < 1.2) return;
                        Need(Mathf.Abs(Effect.Level-.1f)<.02f, "search: effect eases to its searching strength (" + Effect.Level.ToString("F2") + ")");
                        ScreenCapture.CaptureScreenshot(Shots + "search.png"); R.caretaker.Freeze(); Next(5); break;
                    case 5:
                        if (now - at < 1.6) return;
                        Need(Effect.Level < .02f && Global < .02f, "he gives up: picture drains back to clean");
                        Need(errors == 0, "no runtime errors"); Finish(true); break;
                }
            }
            catch (Exception e) { File.AppendAllText(Report, "FAIL stage " + stage + ": " + (e.InnerException ?? e).Message + "\n"); Finish(false); }
        }
        static void Finish(bool success)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log; Time.timeScale = 1; Application.runInBackground = background;
            Shader.SetGlobalFloat("_VhsIntensity", 0);
            File.AppendAllText(Report, success ? "PASS\n" : "FAIL\n"); EditorApplication.isPlaying = false;
        }
    }
}

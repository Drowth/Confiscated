using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Builds the modelled caretaker head used by the caught lunge, and the switch that turns it on.</summary>
    [InitializeOnLoad]
    public static class CaretakerHeadSetup
    {
        const string ShotMarker = "Temp/capture_caught_head";
        static double seenAt; static int shots;
        static CaretakerHeadSetup() { EditorApplication.update += WatchForCatch; }
        /// <summary>Armed once: grabs real Game-view frames (overlay included) of the next catch in Play mode.</summary>
        [MenuItem("Confiscated/Caught Sequence/Arm In-Play Screenshots")]
        public static void ArmShots() { File.WriteAllText(ShotMarker, "armed"); }
        static void WatchForCatch()
        {
            if (!EditorApplication.isPlaying || !File.Exists(ShotMarker)) return;
            // The lunge object starts the clock; shots continue onto the results screen that follows it.
            if (seenAt == 0) { if (GameObject.Find("Caretaker caught close-up") == null) return; seenAt = EditorApplication.timeSinceStartup; shots = 0; }
            double elapsed = EditorApplication.timeSinceStartup - seenAt;
            float[] moments = { .45f, .75f, 1.0f, 1.6f, 2.4f, 3.2f };
            if (shots >= moments.Length) { File.Delete(ShotMarker); seenAt = 0; shots = 0; return; }
            if (shots < moments.Length && elapsed >= moments[shots])
            {
                Directory.CreateDirectory("D:/Confiscated/Docs/CaughtHead");
                ScreenCapture.CaptureScreenshot("D:/Confiscated/Docs/CaughtHead/inplay_" + shots + ".png"); shots++;
            }
        }

        const string Model = "Assets/Art/Models/Characters/CaretakerHead.fbx", Maps = "Assets/Art/Models/Characters/Textures/CaretakerHead";
        const string MaterialPath = "Assets/Art/Materials/M_CaretakerHead.mat", PrefabPath = "Assets/Resources/Art/CaretakerLungeHead3D.prefab";
        const string Toggle = "Confiscated/Caught Sequence/Use Modelled Head";

        [MenuItem("Confiscated/Caught Sequence/Build Modelled Head")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            // The eye pass ray-casts the mesh for surface points and texture coordinates, which needs readable geometry.
            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(Model);
            if (modelImporter != null && !modelImporter.isReadable) { modelImporter.isReadable = true; modelImporter.SaveAndReimport(); }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Model); if (asset == null) throw new InvalidOperationException("Missing " + Model);
            if (!Directory.Exists(Maps) || Directory.GetFiles(Maps, "Color_*.jpg").Length == 0)
            {
                Directory.CreateDirectory(Maps); AssetDatabase.Refresh();
                if (!((ModelImporter)AssetImporter.GetAtPath(Model)).ExtractTextures(Maps)) throw new InvalidOperationException("No embedded textures in the head model.");
                AssetDatabase.Refresh();
                foreach (var normal in Directory.GetFiles(Maps, "NormalGL_*.jpg")) AssetDatabase.DeleteAsset(normal.Replace('\\', '/'));
            }
            string colour = Directory.GetFiles(Maps, "Color_*.jpg").First().Replace('\\', '/');
            // At the end of the lunge this texture fills the whole screen.
            var importer = (TextureImporter)AssetImporter.GetAtPath(colour);
            if (importer.maxTextureSize != 4096) { importer.maxTextureSize = 4096; importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport(); }
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, MaterialPath); }
            material.shader = Shader.Find("Universal Render Pipeline/Unlit"); material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(colour)); material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);

            // Root: one unit tall, centred, face looking along +Z. The importer's axis fix stays on the model beneath the holder.
            var root = new GameObject("CaretakerLungeHead3D");
            try
            {
                var holder = new GameObject("Head model").transform; holder.SetParent(root.transform, false); holder.localRotation = Quaternion.Euler(0, 90, 0);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder);
                foreach (var r in instance.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
                var bounds = Bounds(holder); holder.localScale = Vector3.one / bounds.size.y;
                bounds = Bounds(holder); holder.position -= bounds.center;
                AddEyes(root, colour, material);
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[CaughtHead] Built " + PrefabPath + ". It is only used while '" + Toggle + "' is ticked.");
        }
        // Measured on the dead-on orthographic capture (Capture Head Front Orthographic), in head units: one unit = head height,
        // origin at the centre, +X to the viewer's left, face along +Z. Re-measure if the model is regenerated.
        static readonly Vector2[] PupilCentres = { new Vector2(.1313f, .0625f), new Vector2(-.1279f, .0610f) };
        static readonly Vector2[] WhiteCentres = { new Vector2(.1367f, .0610f), new Vector2(-.1318f, .0610f) };
        const float PupilRadius = .0132f, WhiteRadius = .05f, WanderRadius = .031f;

        static void AddEyes(GameObject root, string colourPath, Material material)
        {
            var colliders = root.GetComponentsInChildren<MeshFilter>().Select(f => { var c = f.gameObject.AddComponent<MeshCollider>(); c.sharedMesh = f.sharedMesh; return c; }).ToArray();
            Physics.SyncTransforms();
            bool Hit(Vector2 at, out RaycastHit best)
            {
                best = default; bool any = false; var ray = new Ray(root.transform.TransformPoint(new Vector3(at.x, at.y, 2)), -root.transform.forward);
                foreach (var c in colliders) if (c.Raycast(ray, out var hit, 4) && (!any || hit.distance < best.distance)) { best = hit; any = true; }
                return any;
            }
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false); source.LoadImage(File.ReadAllBytes(colourPath));
            int w = source.width, h = source.height; var original = source.GetPixels32(); var painted = (Color32[])original.Clone();
            var eyes = root.GetComponent<CaretakerHeadEyes>(); if (eyes == null) eyes = root.AddComponent<CaretakerHeadEyes>();
            eyes.wanderRadius = WanderRadius;
            var pupilMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_CaretakerPupil.mat");
            if (pupilMaterial == null) { pupilMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(pupilMaterial, "Assets/Art/Materials/M_CaretakerPupil.mat"); }
            pupilMaterial.SetColor("_BaseColor", new Color(.055f, .05f, .085f)); EditorUtility.SetDirty(pupilMaterial);
            for (int eye = 0; eye < 2; eye++)
            {
                // 1. Fit a sphere to the visible white, so a moving pupil follows the bulge of the eyeball.
                var points = new System.Collections.Generic.List<Vector3>();
                for (int i = -10; i <= 10; i++) for (int j = -10; j <= 10; j++)
                {
                    var offset = new Vector2(i, j) / 10f * WhiteRadius; if (offset.magnitude > WhiteRadius) continue;
                    if (Hit(WhiteCentres[eye] + offset, out var hit)) points.Add(root.transform.InverseTransformPoint(hit.point));
                }
                FitSphere(points, out var centre, out float radius);
                if (!Hit(WhiteCentres[eye], out var middle)) throw new Exception("Eye " + eye + " not found on the mesh.");
                Vector3 front = root.transform.InverseTransformPoint(middle.point);
                // A nearly flat lens fits a huge sphere; that still works, but cap it so the numbers stay sane.
                if (float.IsNaN(radius) || radius > 2 || radius < WhiteRadius * .5f) { radius = 2; centre = front - Vector3.forward * radius; }
                eyes.centres[eye] = centre; eyes.radii[eye] = radius; eyes.forwards[eye] = (front - centre).normalized;
                eyes.restOffsets[eye] = (PupilCentres[eye] - WhiteCentres[eye]) / WanderRadius;
                Debug.Log("[CaughtHead] Eye " + eye + ": fitted radius " + radius.ToString("F3") + " from " + points.Count + " surface points.");

                // 2. Paint the baked pupil out, borrowing the pencil texture of the white around it, angle for angle.
                const int grid = 150; float reach = PupilRadius * 1.45f; int paintedTexels = 0;
                for (int i = 0; i < grid; i++) for (int j = 0; j < grid; j++)
                {
                    var offset = new Vector2((i + .5f) / grid * 2 - 1, (j + .5f) / grid * 2 - 1) * reach; float r = offset.magnitude; if (r > reach) continue;
                    Vector2 outward = r > 1e-5f ? offset / r : Vector2.right;
                    if (!Hit(PupilCentres[eye] + offset, out var here) || !Hit(PupilCentres[eye] + outward * (PupilRadius * 1.6f + r / reach * PupilRadius * .7f), out var there)) continue;
                    var colour = original[Mathf.Clamp((int)(there.textureCoord.y * h), 0, h - 1) * w + Mathf.Clamp((int)(there.textureCoord.x * w), 0, w - 1)];
                    float blend = 1 - Mathf.InverseLerp(PupilRadius * 1.2f, reach, r);
                    int tx = (int)(here.textureCoord.x * w), ty = (int)(here.textureCoord.y * h);
                    for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                    {
                        int x = tx + dx, y = ty + dy; if (x < 0 || y < 0 || x >= w || y >= h) continue;
                        painted[y * w + x] = Color32.Lerp(original[y * w + x], colour, blend); paintedTexels++;
                    }
                }
                Debug.Log("[CaughtHead] Eye " + eye + ": repainted about " + paintedTexels / 9 + " texture samples.");

                // 3. The movable pupil: a flattened dark disc lying on the eyeball.
                var pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere); pupil.name = eye == 0 ? "Pupil A" : "Pupil B"; Object.DestroyImmediate(pupil.GetComponent<Collider>());
                pupil.transform.SetParent(root.transform, false); pupil.transform.localScale = new Vector3(PupilRadius * 2, PupilRadius * 2, PupilRadius * .35f);
                var renderer = pupil.GetComponent<MeshRenderer>(); renderer.sharedMaterial = pupilMaterial; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                eyes.pupils[eye] = pupil.transform;
            }
            foreach (var c in colliders) Object.DestroyImmediate(c);
            source.SetPixels32(painted); source.Apply();
            string cleanPath = Maps + "/CaretakerHead_NoPupils.jpg"; File.WriteAllBytes(cleanPath, source.EncodeToJPG(96)); Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(cleanPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(cleanPath);
            if (importer.maxTextureSize != 4096) { importer.maxTextureSize = 4096; importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport(); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(cleanPath)); EditorUtility.SetDirty(material);
            eyes.Rest();
        }
        /// <summary>Algebraic least squares: |p|^2 = 2 c.p + (r^2 - |c|^2).</summary>
        static void FitSphere(System.Collections.Generic.List<Vector3> points, out Vector3 centre, out float radius)
        {
            var a = new double[4, 5];
            foreach (var p in points)
            {
                double[] row = { 2 * p.x, 2 * p.y, 2 * p.z, 1 }; double rhs = p.sqrMagnitude;
                for (int i = 0; i < 4; i++) { for (int j = 0; j < 4; j++) a[i, j] += row[i] * row[j]; a[i, 4] += row[i] * rhs; }
            }
            for (int i = 0; i < 4; i++)
            {
                int pivot = i; for (int k = i + 1; k < 4; k++) if (Math.Abs(a[k, i]) > Math.Abs(a[pivot, i])) pivot = k;
                for (int j = 0; j < 5; j++) { double t = a[i, j]; a[i, j] = a[pivot, j]; a[pivot, j] = t; }
                if (Math.Abs(a[i, i]) < 1e-12) { centre = Vector3.zero; radius = float.NaN; return; }
                for (int k = 0; k < 4; k++) if (k != i) { double f = a[k, i] / a[i, i]; for (int j = i; j < 5; j++) a[k, j] -= f * a[i, j]; }
            }
            centre = new Vector3((float)(a[0, 4] / a[0, 0]), (float)(a[1, 4] / a[1, 1]), (float)(a[2, 4] / a[2, 2]));
            radius = Mathf.Sqrt(Mathf.Max(0, (float)(a[3, 4] / a[3, 3]) + centre.sqrMagnitude));
        }

        static Bounds Bounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds); return bounds;
        }

        [MenuItem(Toggle)]
        public static void Switch() { PlayerPrefs.SetInt(CaretakerCatchScare.ModelledHeadKey, PlayerPrefs.GetInt(CaretakerCatchScare.ModelledHeadKey, 0) == 1 ? 0 : 1); PlayerPrefs.Save(); }
        [MenuItem(Toggle, true)]
        public static bool SwitchState() { Menu.SetChecked(Toggle, PlayerPrefs.GetInt(CaretakerCatchScare.ModelledHeadKey, 0) == 1); return true; }

        /// <summary>Dead-on orthographic view, one head-height tall, for measuring where the eyes are.</summary>
        [MenuItem("Confiscated/Caught Sequence/Capture Head Front Orthographic")]
        public static void CaptureFront()
        {
            Directory.CreateDirectory("D:/Confiscated/Docs/CaughtHead");
            var head = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)); head.transform.position = new Vector3(0, -700, 0);
            var go = new GameObject("Head ortho camera"); var camera = go.AddComponent<Camera>();
            try
            {
                camera.orthographic = true; camera.orthographicSize = .5f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta;
                camera.nearClipPlane = .01f; camera.farClipPlane = 5; camera.transform.position = head.transform.position + Vector3.forward * 2; camera.transform.LookAt(head.transform.position);
                HallwayPropLibrary.Capture(camera, 2048, 2048, "D:/Confiscated/Docs/CaughtHead/front_ortho.png");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(head); }
        }

        /// <summary>Stills of the results-screen portrait, posed by the same code the game runs.</summary>
        [MenuItem("Confiscated/Caught Sequence/Capture Portrait Frames")]
        public static void CapturePortrait()
        {
            Directory.CreateDirectory("D:/Confiscated/Docs/CaughtHead");
            var camera = CaughtPortraitHead.BuildStage(out var head);
            try
            {
                float[] moments = { 0f, .7f, 1.4f, 2.1f, 2.8f, 3.5f, 4.2f, 4.9f };
                for (int i = 0; i < moments.Length; i++) { CaughtPortraitHead.Pose(head, moments[i]); HallwayPropLibrary.Capture(camera, 1024, 1024, "D:/Confiscated/Docs/CaughtHead/portrait_" + i + ".png"); }
            }
            finally { Object.DestroyImmediate(camera.transform.parent.gameObject); }
        }

        /// <summary>Stills of the modelled lunge at fixed moments, posed by the same code the game runs.</summary>
        [MenuItem("Confiscated/Caught Sequence/Capture Modelled Lunge Frames")]
        public static void Capture()
        {
            Directory.CreateDirectory("D:/Confiscated/Docs/CaughtHead");
            var camera = CaretakerCatchScare.BuildLungeRig(out var head);
            try
            {
                float[] moments = { .10f, .40f, .60f, .72f, .80f, .88f, 1.05f };
                for (int i = 0; i < moments.Length; i++)
                {
                    CaretakerCatchScare.PoseLungeRig(camera, head, moments[i]);
                    HallwayPropLibrary.Capture(camera, 1600, 1000, "D:/Confiscated/Docs/CaughtHead/modelled_" + i + ".png");
                }
            }
            finally { Object.DestroyImmediate(camera.transform.parent.gameObject); }
        }
    }
}

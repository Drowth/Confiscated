using System;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// Builds the CONFISCATED! visual test scene from modular prefabs and reusable materials.
    ///
    /// Asset replacement contract:
    ///   * Placeholder textures are written to Assets/Art/Textures/*.png ONLY if the file does not already exist.
    ///     Dropping a real PNG with the same filename over the placeholder swaps it everywhere (same GUID, same .meta).
    ///   * Materials are created only if missing, so re-running the builder never resets hand-tweaked materials.
    ///   * Prefabs and the scene are regenerated on every run (they are procedural).
    /// </summary>
    public static class VisualTestSceneBuilder
    {
        // ---------- Paths ----------
        public const string ScenePath = "Assets/Scenes/VisualTest.unity";
        const string TexDir = "Assets/Art/Textures";
        const string MatDir = "Assets/Art/Materials";
        const string MeshDir = "Assets/Art/Meshes";
        const string ModularDir = "Assets/Prefabs/Modular";
        const string PropsDir = "Assets/Prefabs/Props";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        // ---------- Module dimensions (metres) ----------
        const float WallLen = 2f;      // P_Wall_2m
        const float WallH = 3f;
        const float WallT = 0.15f;
        const float Tile = 1f;         // floor / ceiling tile
        const float DoorH = 2.0f;      // door leaf height (texture = 1 m x 2 m)
        static readonly Vector2 WallTexSize = new Vector2(WallLen, WallH); // metres covered by one wall texture
        static readonly Vector2 TileTexSize = new Vector2(Tile, Tile);     // metres covered by one floor/ceiling texture

        // Corridor: X in [-1.5, 1.5], Z in [2, 20]
        const float CorX0 = -1.5f, CorX1 = 1.5f, CorZ0 = 2f, CorZ1 = 20f;
        // Office (open onto the corridor head): X in [-4.5, 1.5], Z in [-2, 2]
        const float OffX0 = -4.5f, OffX1 = 1.5f, OffZ0 = -2f, OffZ1 = 2f;

        // Reference camera pose (matches the concept image: office head looking down the corridor).
        static readonly Vector3 RefCamPos = new Vector3(0f, 1.55f, -0.6f);
        const float EyeHeight = FirstPersonController.ChildEyeHeight;
        const float RefPitch = 4f;
        const float RefFov = 60f;
        // Player spawn: just inside the corridor head, beside the window they climbed in through.
        static readonly Vector3 SpawnPos = new Vector3(1.0f, 0f, 3.8f);
        const float SpawnYaw = 180f;
        static readonly Vector3 WindowPos = new Vector3(CorX1 - 0.02f, 1.45f, 3.6f); // on the corridor east wall

        // ---------- Texture specs (the ChatGPT asset contract lives here) ----------
        const string TexWall = TexDir + "/T_Wall_Corridor.png";       // 1024x1536, opaque, tiles left<->right
        const string TexFloor = TexDir + "/T_Floor_Tiles.png";        // 512x512, opaque, tiles all edges (1m x 1m)
        const string TexCeiling = TexDir + "/T_Ceiling_Panels.png";   // 512x512, opaque, tiles all edges (1m x 1m)
        const string TexDoor = TexDir + "/T_Door_Classroom.png";      // 1024x2048, opaque, no tiling (1m x 2m leaf)
        const string TexCaretaker = TexDir + "/T_Caretaker_Idle.png"; // 1024x1536, alpha, no tiling (1.4m x 2.1m quad)
        const string PostProfilePath = "Assets/Settings/VisualTest_PostProfile.asset";
        static readonly Vector2 CaretakerQuadSize = new Vector2(1.4f, 2.1f);
        const string TexPhone = TexDir + "/T_Phone_Held.png";         // 1024x1024, alpha, UI sprite
        const string TexSign = TexDir + "/T_Sign_Confiscated.png";    // 512x256, alpha, label decal
        // ---- Batch 2 slots (placeholders until art arrives) ----
        const string TexWindow = TexDir + "/T_Window_Escape.png";     // 1024x1024, opaque, whole window incl. frame, 1.1 m x 1.1 m
        const string TexDoorOffice = TexDir + "/T_Door_Office.png";   // 1024x2048, opaque, 1 m x 2 m leaf
        const string TexDoorFire = TexDir + "/T_Door_Fire.png";       // 1024x1024, opaque, both leaves, 2 m x 2 m
        const string TexBoardA = TexDir + "/T_Noticeboard_A.png";     // 1024x1024, opaque, whole board incl. frame, 1 m x 1 m
        const string TexBoardB = TexDir + "/T_Noticeboard_B.png";     // 1024x1024, opaque, whole board incl. frame, 1 m x 1 m
        const string TexWood = TexDir + "/T_Wood_Pencil.png";         // 1024x1024, opaque, tiles all edges, 0.5 m x 0.5 m
        static readonly Vector2 WoodTexSize = new Vector2(0.5f, 0.5f);
        const float WindowSize = 1.1f;
        const float BoardSize = 1.0f;

        static bool overwriteMaterials;

        // =====================================================================
        // Menu items
        // =====================================================================

        [MenuItem("Confiscated/Build Visual Test Scene")]
        public static void Build()
        {
            overwriteMaterials = false;
            BuildInternal();
        }

        [MenuItem("Confiscated/Build Visual Test Scene (reset materials)")]
        public static void BuildResetMaterials()
        {
            overwriteMaterials = true;
            BuildInternal();
        }

        [MenuItem("Confiscated/Capture Reference Screenshots")]
        public static void CaptureScreenshots()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Screenshots"));
            Directory.CreateDirectory(dir);
            var root = GameObject.Find("CaptureCameras");
            if (root == null) { Debug.LogError("[Confiscated] No 'CaptureCameras' object in the open scene."); return; }

            var billboards = Object.FindObjectsByType<BillboardY>(FindObjectsInactive.Exclude);
            var hud = GameObject.Find("HUD");
            var hudCanvas = hud != null ? hud.GetComponent<Canvas>() : null;
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            {
                foreach (var b in billboards) b.FaceTarget(cam.transform);
                // Overlay canvases are not rendered by an off-screen camera render; temporarily bind the HUD to the
                // reference camera so the held phone shows up in the reference capture (and only there).
                bool showHud = hudCanvas != null && cam.gameObject.name.Contains("Reference");
                if (showHud)
                {
                    hudCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    hudCanvas.worldCamera = cam;
                    hudCanvas.planeDistance = 0.5f;
                    foreach (var g in hudCanvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                }
                string file = Path.Combine(dir, cam.gameObject.name + ".png");
                try { RenderCameraToPng(cam, 1920, 1080, file); }
                finally
                {
                    if (showHud)
                    {
                        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        hudCanvas.worldCamera = null;
                    }
                }
                Debug.Log("[Confiscated] Saved " + file);
            }
            // Restore billboards to face the player camera.
            var main = Camera.main;
            if (main != null) foreach (var b in billboards) b.FaceTarget(main.transform);
        }

        static void RenderCameraToPng(Camera cam, int w, int h, string file)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            bool wasEnabled = cam.enabled;
            var prevTarget = cam.targetTexture;
            try
            {
                cam.enabled = true;
                cam.targetTexture = rt;
                var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req))
                    RenderPipeline.SubmitRenderRequest(cam, req);
                else
                    cam.Render();

                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                cam.enabled = wasEnabled;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        // =====================================================================
        // Build
        // =====================================================================

        static void BuildInternal()
        {
            EnsureFolder("Assets/Art");
            EnsureFolder(TexDir);
            EnsureFolder(MatDir);
            EnsureFolder(MeshDir);
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(ModularDir);
            EnsureFolder(PropsDir);
            EnsureFolder("Assets/Scenes");

            CreatePlaceholderTextures();
            var mats = CreateMaterials();
            var prefabs = CreatePrefabs(mats);
            IllustratedArtSetup.Apply();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildScene(mats, prefabs);
            OfficeMissionSetup.ApplyToScene();
            EditorSceneManager.SaveScene(scene, ScenePath);   // NavMesh data is stored next to the saved scene
            BakeNavMesh();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Confiscated] Visual test scene built and saved to " + ScenePath);
        }

        // ---------------------------------------------------------------------
        // Placeholder textures (never overwrite existing files)
        // ---------------------------------------------------------------------

        static void CreatePlaceholderTextures()
        {
            // Wall: 1024 x 1536 represents 2 m wide x 3 m tall. Dado band baked in.
            EnsureTexture(TexWall, 1024, 1536, false, (u, v) =>
            {
                Color cream = new Color(0.93f, 0.91f, 0.86f);
                Color teal = new Color(0.36f, 0.64f, 0.61f);
                Color blue = new Color(0.18f, 0.26f, 0.52f);
                Color c = v < 0.36f ? teal : (v < 0.385f ? blue : cream);
                c *= 1f + 0.03f * Hatch(u * 40f, v * 60f);
                return c;
            }, TextureWrapMode.Repeat, TextureWrapMode.Clamp);

            // Floor: 512 x 512 represents 1 m x 1 m (2x2 tiles of 0.5 m).
            EnsureTexture(TexFloor, 512, 512, false, (u, v) =>
            {
                Color tile = new Color(0.80f, 0.80f, 0.78f);
                Color grout = new Color(0.55f, 0.55f, 0.53f);
                float fu = Mathf.Repeat(u * 2f, 1f), fv = Mathf.Repeat(v * 2f, 1f);
                bool isGrout = fu < 0.03f || fv < 0.03f;
                Color c = isGrout ? grout : tile;
                c *= 1f + 0.04f * Hatch(u * 30f, v * 30f);
                return c;
            }, TextureWrapMode.Repeat, TextureWrapMode.Repeat);

            // Ceiling: 512 x 512 represents 1 m x 1 m (2x2 suspended panels).
            EnsureTexture(TexCeiling, 512, 512, false, (u, v) =>
            {
                Color panel = new Color(0.92f, 0.92f, 0.90f);
                Color grid = new Color(0.70f, 0.70f, 0.68f);
                float fu = Mathf.Repeat(u * 2f, 1f), fv = Mathf.Repeat(v * 2f, 1f);
                bool isGrid = fu < 0.025f || fv < 0.025f;
                return isGrid ? grid : panel;
            }, TextureWrapMode.Repeat, TextureWrapMode.Repeat);

            // Door leaf: 1024 x 2048 represents 1 m x 2 m, front view, hinge on the left.
            EnsureTexture(TexDoor, 1024, 2048, false, (u, v) =>
            {
                Color yellow = new Color(0.86f, 0.70f, 0.20f);
                Color edge = new Color(0.55f, 0.42f, 0.10f);
                Color glass = new Color(0.30f, 0.38f, 0.42f);
                bool border = u < 0.03f || u > 0.97f || v < 0.015f || v > 0.985f;
                bool window = u > 0.60f && u < 0.78f && v > 0.50f && v < 0.85f;
                bool windowFrame = u > 0.58f && u < 0.80f && v > 0.485f && v < 0.865f;
                if (border) return edge;
                if (window) return glass;
                if (windowFrame) return edge;
                return yellow * (1f + 0.03f * Hatch(u * 20f, v * 40f));
            }, TextureWrapMode.Clamp, TextureWrapMode.Clamp);

            // Caretaker: 1024 x 1536 with alpha (2:3, matches the quad's 1.4 m x 2.1 m). Feet at bottom edge, centred.
            EnsureTexture(TexCaretaker, 1024, 1536, true, (u, v) =>
            {
                Color coat = new Color(0.16f, 0.38f, 0.33f);
                Color skin = new Color(0.90f, 0.78f, 0.66f);
                Color dark = new Color(0.08f, 0.08f, 0.10f);
                float du = (u - 0.5f) * 1.5f;               // work in a 1:2-ish frame so the figure is not squat
                float hy = 0.875f;
                float hr2 = du * du * 4f + (v - hy) * (v - hy);
                if (hr2 < 0.085f * 0.085f && v > hy + 0.03f) return dark;     // hair cap
                if (hr2 < 0.08f * 0.08f) return skin;                          // head
                if (Mathf.Abs(du) < 0.30f - 0.10f * Mathf.Clamp01((v - 0.35f) / 0.45f) && v > 0.28f && v < 0.81f) return coat;
                if (v <= 0.28f && v > 0.03f && (Mathf.Abs(du + 0.09f) < 0.05f || Mathf.Abs(du - 0.09f) < 0.05f)) return dark;
                if (v <= 0.03f && (Mathf.Abs(du + 0.10f) < 0.10f || Mathf.Abs(du - 0.10f) < 0.10f)) return dark;
                return Color.clear;
            }, TextureWrapMode.Clamp, TextureWrapMode.Clamp);

            // Phone: 1024 x 1024 with alpha. Hand + phone anchored bottom-right, bleeding off the bottom/right edges.
            EnsureTexture(TexPhone, 1024, 1024, true, (u, v) =>
            {
                Color skin = new Color(0.92f, 0.80f, 0.68f);
                Color body = new Color(0.20f, 0.20f, 0.22f);
                Color screen = new Color(0.62f, 0.80f, 0.58f);
                if (u > 0.40f && u < 0.72f && v > 0.30f && v < 0.92f)
                {
                    if (u > 0.44f && u < 0.68f && v > 0.62f && v < 0.86f) return screen;
                    return body;
                }
                float hx = u - 0.72f, hy = v - 0.25f;
                if (hx * hx + hy * hy * 1.6f < 0.34f * 0.34f && v < 0.62f) return skin;
                if (u > 0.55f && v < 0.30f) return skin;
                return Color.clear;
            }, TextureWrapMode.Clamp, TextureWrapMode.Clamp, sprite: true);

            // Sign: 512 x 256 with alpha. Paper label with a dark bar standing in for the word.
            EnsureTexture(TexSign, 512, 256, true, (u, v) =>
            {
                Color paper = new Color(0.94f, 0.92f, 0.85f);
                Color ink = new Color(0.12f, 0.12f, 0.14f);
                if (u < 0.02f || u > 0.98f || v < 0.04f || v > 0.96f) return Color.clear;
                if (u < 0.05f || u > 0.95f || v < 0.10f || v > 0.90f) return ink;
                if (u > 0.15f && u < 0.85f && v > 0.38f && v < 0.62f) return ink;
                return paper;
            }, TextureWrapMode.Clamp, TextureWrapMode.Clamp);
        }

        static float Hatch(float x, float y)
        {
            // Cheap deterministic "pencil" noise so placeholders are not dead-flat.
            return Mathf.Sin(x * 1.7f + y * 0.9f) * Mathf.Sin(y * 2.3f - x * 0.4f);
        }

        static void EnsureTexture(string path, int w, int h, bool alpha, Func<float, float, Color> paint,
            TextureWrapMode wrapU, TextureWrapMode wrapV, bool sprite = false)
        {
            string full = Path.GetFullPath(path);
            if (!File.Exists(full))
            {
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    float v = (y + 0.5f) / h;
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w;
                        Color c = paint(u, v);
                        if (!alpha)
                        {
                            c.a = 1f;
                            // Placeholder marker: magenta square in the top-left corner of every opaque placeholder.
                            if (x < 40 && y > h - 40) c = (x < 4 || y > h - 4 || x > 35 || y < h - 35) ? Color.white : Color.magenta;
                        }
                        px[y * w + x] = c;
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(full, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("[Confiscated] Wrote placeholder texture " + path);
            }

            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) { Debug.LogError("[Confiscated] No TextureImporter for " + path); return; }
            bool dirty = false;
            var wantType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (imp.textureType != wantType) { imp.textureType = wantType; dirty = true; }
            if (!imp.sRGBTexture) { imp.sRGBTexture = true; dirty = true; }
            if (imp.alphaIsTransparency != alpha) { imp.alphaIsTransparency = alpha; dirty = true; }
            var wantAlphaSrc = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            if (imp.alphaSource != wantAlphaSrc) { imp.alphaSource = wantAlphaSrc; dirty = true; }
            if (imp.wrapModeU != wrapU) { imp.wrapModeU = wrapU; dirty = true; }
            if (imp.wrapModeV != wrapV) { imp.wrapModeV = wrapV; dirty = true; }
            if (imp.maxTextureSize < 2048) { imp.maxTextureSize = 2048; dirty = true; }
            if (imp.mipmapEnabled != !sprite) { imp.mipmapEnabled = !sprite; dirty = true; }
            if (sprite)
            {
                if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (Math.Abs(imp.spritePixelsPerUnit - 100f) > 0.01f) { imp.spritePixelsPerUnit = 100f; dirty = true; }
            }
            if (dirty) imp.SaveAndReimport();
        }

        // ---------------------------------------------------------------------
        // Materials (create-if-missing)
        // ---------------------------------------------------------------------

        public class Mats
        {
            public Material Wall, Floor, Ceiling, DoorClassroom, DoorOffice, DoorFire, Frame,
                Wood, Cardboard, Sign, Caretaker, LightPanel, White, Red, Board, Dark, Glass;
        }

        static PhysicsMaterial GetOrCreatePhysicsMaterial()
        {
            const string path = MatDir + "/PM_Football.physicMaterial";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm != null) return pm;
            pm = new PhysicsMaterial("PM_Football") { bounciness = 0.55f, dynamicFriction = 0.5f, staticFriction = 0.5f,
                bounceCombine = PhysicsMaterialCombine.Maximum };
            AssetDatabase.CreateAsset(pm, path);
            return pm;
        }

        static Mats CreateMaterials()
        {
            // Obsolete from an earlier build: UV scaling now lives in the meshes, not in a second material.
            if (AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_Wall_Corridor_Half.mat") != null)
                AssetDatabase.DeleteAsset(MatDir + "/M_Wall_Corridor_Half.mat");

            var m = new Mats
            {
                Wall = LitTex("M_Wall_Corridor", TexWall, Vector2.one),
                Floor = LitTex("M_Floor_Tiles", TexFloor, Vector2.one),
                Ceiling = LitTex("M_Ceiling_Panels", TexCeiling, Vector2.one),
                DoorClassroom = LitTex("M_Door_Classroom", TexDoor, Vector2.one),
                DoorOffice = LitColor("M_Door_Office", new Color(0.56f, 0.59f, 0.63f)),
                DoorFire = LitColor("M_Door_Fire", new Color(0.55f, 0.13f, 0.13f)),
                Frame = LitColor("M_DoorFrame_Grey", new Color(0.42f, 0.44f, 0.47f)),
                Wood = LitColor("M_Wood_Desk", new Color(0.42f, 0.26f, 0.15f)),
                Cardboard = LitColor("M_Cardboard", new Color(0.70f, 0.54f, 0.35f)),
                Sign = UnlitCutout("M_Sign_Confiscated", TexSign),
                Caretaker = UnlitCutout("M_Caretaker_Cutout", TexCaretaker, cameraFacing: true),
                LightPanel = UnlitColor("M_CeilingLight", new Color(0.96f, 0.98f, 1f)),
                White = LitColor("M_White", new Color(0.92f, 0.92f, 0.90f)),
                Red = LitColor("M_Red_Toy", new Color(0.75f, 0.15f, 0.12f)),
                Board = LitColor("M_Noticeboard", new Color(0.28f, 0.22f, 0.36f)),
                Dark = LitColor("M_Dark", new Color(0.12f, 0.12f, 0.14f)),
                Glass = UnlitColor("M_Window_Glass", new Color(0.74f, 0.80f, 0.86f)),
            };
            return m;
        }

        static Material GetOrCreate(string name, string shaderName, out bool created)
        {
            string path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            created = false;
            if (mat != null && !overwriteMaterials) return mat;
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new Exception("Shader not found: " + shaderName);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }
            created = true;
            return mat;
        }

        static Material LitTex(string name, string texPath, Vector2 tiling)
        {
            var mat = GetOrCreate(name, "Universal Render Pipeline/Lit", out bool created);
            if (!created) return mat;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.05f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.SetFloat("_EnvironmentReflections", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material LitColor(string name, Color c)
        {
            var mat = GetOrCreate(name, "Universal Render Pipeline/Lit", out bool created);
            if (!created) return mat;
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Smoothness", 0.05f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.SetFloat("_EnvironmentReflections", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material UnlitColor(string name, Color c)
        {
            var mat = GetOrCreate(name, "Universal Render Pipeline/Unlit", out bool created);
            if (!created) return mat;
            mat.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material UnlitCutout(string name, string texPath, bool cameraFacing = false)
        {
            var mat = GetOrCreate(name, cameraFacing ? "Confiscated/Character Cutout" : "Universal Render Pipeline/Unlit", out bool created);
            if (!created) return mat;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Cutoff", 0.5f);
            if (cameraFacing) mat.shaderKeywords = new string[0];
            else
            {
                mat.SetFloat("_Surface", 0f);      // opaque + alpha clip = cutout
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cull", (float)CullMode.Off);
                mat.EnableKeyword("_ALPHATEST_ON");
            }
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------------
        // Prefabs (regenerated every build)
        // ---------------------------------------------------------------------

        public class Prefabs
        {
            public GameObject Wall2m, Wall1m, FloorTile, CeilingTile, CeilingLight,
                DoorClassroom, DoorOffice, DoorFire, Desk, Chair, Box, Caretaker, Noticeboard, Football, Window;
        }

        static Prefabs CreatePrefabs(Mats m)
        {
            var p = new Prefabs();

            // Wall segments: front (textured, upright) face is local -Z. Origin at floor, centre of the run.
            // The 1 m piece shows the left half of the wall texture (UVs are in metres / texture size).
            p.Wall2m = SavePrefab(BuildWall("P_Wall_2m", WallLen, m.Wall), ModularDir);
            p.Wall1m = SavePrefab(BuildWall("P_Wall_1m", WallLen * 0.5f, m.Wall), ModularDir);

            // Floor tile 1m: top surface at y = 0. Ceiling tile: underside at y = WallH.
            var floor = new GameObject("P_FloorTile_1m");
            Box("Slab", floor.transform, new Vector3(0, -0.05f, 0), new Vector3(Tile, 0.1f, Tile), TileTexSize, m.Floor);
            p.FloorTile = SavePrefab(floor, ModularDir);

            var ceil = new GameObject("P_CeilingTile_1m");
            Box("Slab", ceil.transform, new Vector3(0, WallH + 0.05f, 0), new Vector3(Tile, 0.1f, Tile), TileTexSize, m.Ceiling);
            p.CeilingTile = SavePrefab(ceil, ModularDir);

            // Ceiling light strip: long axis along local Z, sits just under the ceiling with a point light.
            var light = new GameObject("P_CeilingLight");
            Cube("Panel", light.transform, new Vector3(0, WallH - 0.03f, 0), new Vector3(0.3f, 0.05f, 1.2f), m.LightPanel, collider: false);
            var pl = new GameObject("PointLight");
            pl.transform.SetParent(light.transform, false);
            pl.transform.localPosition = new Vector3(0, WallH - 0.35f, 0);
            var l = pl.AddComponent<Light>();
            l.type = LightType.Point;
            // Matches SchoolLightingSetup's fluorescent tuning for the live SchoolLayout scene, so rebuilding this
            // (old office chapter) scene doesn't regenerate P_CeilingLight.prefab back to the old warm/dim defaults.
            l.color = new Color(0.92f, 0.97f, 1f);
            l.intensity = 2.4f;
            l.range = 7.5f;
            l.shadows = LightShadows.None;
            p.CeilingLight = SavePrefab(light, ModularDir);

            // Doors: origin at floor, centre of opening, front face local -Z.
            p.DoorClassroom = SavePrefab(BuildDoor("P_Door_Classroom", 1.0f, m.DoorClassroom, m.Frame, m.White, 0f, 1), ModularDir);
            p.DoorOffice = SavePrefab(BuildDoor("P_Door_Office", 0.95f, m.DoorOffice, m.Frame, null, 0f, 1), ModularDir);
            p.DoorFire = SavePrefab(BuildDoor("P_Door_Fire_Double", 0.9f, m.DoorFire, m.Frame, null, 0f, 2), ModularDir);

            // Desk: 1.6 x 0.8, top at 0.75, back panel toward local +Z.
            var desk = new GameObject("P_Desk");
            Cube("Top", desk.transform, new Vector3(0, 0.73f, 0), new Vector3(1.6f, 0.04f, 0.8f), m.Wood);
            Cube("SideL", desk.transform, new Vector3(-0.78f, 0.355f, 0), new Vector3(0.04f, 0.71f, 0.76f), m.Wood);
            Cube("SideR", desk.transform, new Vector3(0.78f, 0.355f, 0), new Vector3(0.04f, 0.71f, 0.76f), m.Wood);
            Cube("Modesty", desk.transform, new Vector3(0, 0.45f, 0.37f), new Vector3(1.52f, 0.5f, 0.03f), m.Wood);
            p.Desk = SavePrefab(desk, PropsDir);

            // Chair: seat at 0.45, back toward local +Z.
            var chair = new GameObject("P_Chair");
            Cube("Seat", chair.transform, new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.04f, 0.42f), m.Wood);
            Cube("Back", chair.transform, new Vector3(0, 0.72f, 0.19f), new Vector3(0.42f, 0.5f, 0.04f), m.Wood);
            foreach (var sx in new[] { -0.18f, 0.18f })
                foreach (var sz in new[] { -0.18f, 0.18f })
                    Cube("Leg", chair.transform, new Vector3(sx, 0.215f, sz), new Vector3(0.04f, 0.43f, 0.04f), m.Wood);
            p.Chair = SavePrefab(chair, PropsDir);

            // Confiscated box: open cardboard box with a label on the front (-Z) and a few placeholder contents.
            var box = new GameObject("P_ConfiscatedBox");
            Cube("Bottom", box.transform, new Vector3(0, 0.01f, 0), new Vector3(0.6f, 0.02f, 0.45f), m.Cardboard);
            Cube("Front", box.transform, new Vector3(0, 0.2f, -0.215f), new Vector3(0.6f, 0.4f, 0.02f), m.Cardboard);
            Cube("Back", box.transform, new Vector3(0, 0.2f, 0.215f), new Vector3(0.6f, 0.4f, 0.02f), m.Cardboard);
            Cube("Left", box.transform, new Vector3(-0.29f, 0.2f, 0), new Vector3(0.02f, 0.4f, 0.45f), m.Cardboard);
            Cube("Right", box.transform, new Vector3(0.29f, 0.2f, 0), new Vector3(0.02f, 0.4f, 0.45f), m.Cardboard);
            var label = GameObject.CreatePrimitive(PrimitiveType.Quad);
            label.name = "Label_CONFISCATED";
            Object.DestroyImmediate(label.GetComponent<Collider>());
            label.transform.SetParent(box.transform, false);
            label.transform.localPosition = new Vector3(0, 0.2f, -0.228f);
            label.transform.localScale = new Vector3(0.44f, 0.22f, 1f);
            label.GetComponent<MeshRenderer>().sharedMaterial = m.Sign;
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Placeholder_Football_Static";
            ball.transform.SetParent(box.transform, false);
            ball.transform.localPosition = new Vector3(-0.15f, 0.25f, 0.05f);
            ball.transform.localScale = Vector3.one * 0.22f;
            ball.GetComponent<MeshRenderer>().sharedMaterial = m.White;
            var car = GameObject.CreatePrimitive(PrimitiveType.Cube);
            car.name = "Placeholder_ToyCar";
            car.transform.SetParent(box.transform, false);
            car.transform.localPosition = new Vector3(0.14f, 0.36f, -0.05f);
            car.transform.localRotation = Quaternion.Euler(0, 30, 0);
            car.transform.localScale = new Vector3(0.18f, 0.07f, 0.09f);
            car.GetComponent<MeshRenderer>().sharedMaterial = m.Red;
            // The player's phone (placeholder slab) sitting in the box; PhonePickup lives on the box root so
            // looking at any part of the box offers the pickup.
            var phoneItem = GameObject.CreatePrimitive(PrimitiveType.Cube);
            phoneItem.name = "Phone_Item";
            phoneItem.transform.SetParent(box.transform, false);
            phoneItem.transform.localPosition = new Vector3(0.05f, 0.42f, 0.12f);
            phoneItem.transform.localRotation = Quaternion.Euler(0, -20, 0);
            phoneItem.transform.localScale = new Vector3(0.05f, 0.015f, 0.11f);
            phoneItem.GetComponent<MeshRenderer>().sharedMaterial = m.Dark;
            var pickup = box.AddComponent<PhonePickup>();
            pickup.phoneVisual = phoneItem;
            p.Box = SavePrefab(box, PropsDir);

            // Caretaker: root carries collision body, NavMeshAgent and AI (root forward = facing).
            // The cutout quad is a child that billboards toward the camera independently.
            var care = new GameObject("P_Caretaker");
            care.layer = LayerMask.NameToLayer("Enemy");
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Cutout";
            quad.layer = care.layer;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(care.transform, false);
            quad.transform.localPosition = new Vector3(0, CaretakerQuadSize.y * 0.5f, 0);
            quad.transform.localScale = new Vector3(CaretakerQuadSize.x, CaretakerQuadSize.y, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = m.Caretaker;
            quad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            quad.AddComponent<BillboardY>();
            var cc = care.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0, CaretakerQuadSize.y * 0.5f, 0);
            cc.radius = 0.35f;
            cc.height = CaretakerQuadSize.y;
            var agent = care.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2.1f;
            agent.speed = 1.5f;
            agent.acceleration = 6f;
            agent.angularSpeed = 0f;      // CaretakerAI rotates the root itself
            agent.stoppingDistance = 0.2f;
            agent.autoBraking = true;
            var ai = care.AddComponent<CaretakerAI>();
            care.AddComponent<CaretakerWalkAudio>().keysClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/JanglingKeys.mp3");
            var aiSo = new SerializedObject(ai);
            aiSo.FindProperty("sightBlockers").intValue = ~((1 << LayerMask.NameToLayer("Enemy")) | (1 << 2 /* Ignore Raycast */));
            aiSo.ApplyModifiedPropertiesWithoutUndo();
            CutoutMotionSetup.Configure(care);
            p.Caretaker = SavePrefab(care, PropsDir);

            // Football: physics ball the player can pick up and throw. Ignored by the NavMesh bake.
            var football = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            football.name = "P_Football";
            football.transform.localScale = Vector3.one * 0.22f;
            football.GetComponent<MeshRenderer>().sharedMaterial = m.White;
            var rb = football.AddComponent<Rigidbody>();
            rb.mass = 0.43f;
            rb.linearDamping = 0.25f;
            rb.angularDamping = 0.6f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            football.GetComponent<SphereCollider>().sharedMaterial = GetOrCreatePhysicsMaterial();
            football.AddComponent<ThrowableBall>();
            var mod = football.AddComponent<NavMeshModifier>();
            mod.ignoreFromBuild = true;
            p.Football = SavePrefab(football, PropsDir);

            // Window: frame + pale glass, front face local -Z, origin at the centre of the pane. Escape point.
            var win = new GameObject("P_Window_Escape");
            Cube("Glass", win.transform, Vector3.zero, new Vector3(1.0f, 1.1f, 0.02f), m.Glass, collider: false);
            Cube("FrameTop", win.transform, new Vector3(0, 0.575f, -0.01f), new Vector3(1.12f, 0.05f, 0.06f), m.White, collider: false);
            Cube("FrameBottom", win.transform, new Vector3(0, -0.575f, -0.01f), new Vector3(1.12f, 0.05f, 0.06f), m.White, collider: false);
            Cube("FrameL", win.transform, new Vector3(-0.535f, 0, -0.01f), new Vector3(0.05f, 1.2f, 0.06f), m.White, collider: false);
            Cube("FrameR", win.transform, new Vector3(0.535f, 0, -0.01f), new Vector3(0.05f, 1.2f, 0.06f), m.White, collider: false);
            Cube("Mullion", win.transform, new Vector3(0, 0, -0.01f), new Vector3(0.04f, 1.1f, 0.05f), m.White, collider: false);
            var winCol = win.AddComponent<BoxCollider>();
            winCol.size = new Vector3(1.12f, 1.2f, 0.08f);
            var esc = win.AddComponent<EscapeWindow>();
            esc.holdSeconds = 2.0f;
            p.Window = SavePrefab(win, PropsDir);

            // Noticeboard: thin panel, front face local -Z. Placeholder colour until a texture is supplied.
            var board = new GameObject("P_Noticeboard");
            Cube("Board", board.transform, new Vector3(0, 0, 0), new Vector3(1.0f, 0.8f, 0.03f), m.Board, collider: false);
            Cube("FrameTop", board.transform, new Vector3(0, 0.41f, 0), new Vector3(1.06f, 0.03f, 0.04f), m.Wood, collider: false);
            Cube("FrameBottom", board.transform, new Vector3(0, -0.41f, 0), new Vector3(1.06f, 0.03f, 0.04f), m.Wood, collider: false);
            Cube("FrameL", board.transform, new Vector3(-0.515f, 0, 0), new Vector3(0.03f, 0.85f, 0.04f), m.Wood, collider: false);
            Cube("FrameR", board.transform, new Vector3(0.515f, 0, 0), new Vector3(0.03f, 0.85f, 0.04f), m.Wood, collider: false);
            p.Noticeboard = SavePrefab(board, PropsDir);

            return p;
        }

        static GameObject BuildWall(string name, float length, Material mat)
        {
            var go = new GameObject(name);
            Box("Panel", go.transform, new Vector3(0, WallH * 0.5f, WallT * 0.5f), new Vector3(length, WallH, WallT), WallTexSize, mat);
            return go;
        }

        /// <summary>Door: frame posts + lintel + leaf/leaves. Origin at floor centre of opening; front = local -Z.</summary>
        static GameObject BuildDoor(string name, float leafW, Material leaf, Material frame, Material signMat, float openAngle, int leaves)
        {
            const float H = DoorH;
            float openingW = leafW * leaves;
            var go = new GameObject(name);
            Cube("PostL", go.transform, new Vector3(-openingW * 0.5f - 0.04f, H * 0.5f, 0), new Vector3(0.08f, H, 0.12f), frame);
            Cube("PostR", go.transform, new Vector3(openingW * 0.5f + 0.04f, H * 0.5f, 0), new Vector3(0.08f, H, 0.12f), frame);
            Cube("Lintel", go.transform, new Vector3(0, H + 0.04f, 0), new Vector3(openingW + 0.16f, 0.08f, 0.12f), frame);
            for (int i = 0; i < leaves; i++)
            {
                float hingeX = -openingW * 0.5f + i * leafW;
                var hinge = new GameObject("Hinge" + (i + 1));
                hinge.transform.SetParent(go.transform, false);
                hinge.transform.localPosition = new Vector3(hingeX, 0, 0);
                hinge.transform.localRotation = Quaternion.Euler(0, openAngle, 0);
                // Leaf texture covers exactly one leaf (1 x 2 m); edges show a thin slice of the texture border.
                Box("Leaf", hinge.transform, new Vector3(leafW * 0.5f, H * 0.5f, 0.0f), new Vector3(leafW, H, 0.05f), new Vector2(leafW, H), leaf);
            }
            if (signMat != null)
            {
                // Small sign plate beside the door (placeholder for "Year 6" etc.).
                Cube("SignPlate", go.transform, new Vector3(openingW * 0.5f + 0.30f, 1.55f, -0.005f), new Vector3(0.28f, 0.12f, 0.01f), signMat, collider: false);
            }
            return go;
        }

        static GameObject Cube(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, bool collider = true)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.name = name;
            c.transform.SetParent(parent, false);
            c.transform.localPosition = localPos;
            c.transform.localScale = size;
            c.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(c.GetComponent<Collider>());
            return c;
        }

        /// <summary>
        /// Textured box whose UVs are in metres divided by the texture's real-world size, upright and unmirrored on
        /// every face. Unity's built-in cube flips V on its side faces and squashes the full texture onto end caps,
        /// so all textured architecture uses this instead. The mesh is saved as an asset so prefabs can reference it.
        /// </summary>
        public static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Vector2 texWorldSize, Material mat, bool collider = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, texWorldSize);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        static Mesh BoxMesh(Vector3 size, Vector2 texWorldSize)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            string name = string.Format(ci, "Box_{0}x{1}x{2}_Tex{3}x{4}", size.x, size.y, size.z, texWorldSize.x, texWorldSize.y);
            string path = MeshDir + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = name;

            var verts = new System.Collections.Generic.List<Vector3>(24);
            var norms = new System.Collections.Generic.List<Vector3>(24);
            var uvs = new System.Collections.Generic.List<Vector2>(24);
            var tris = new System.Collections.Generic.List<int>(36);
            Vector3 h = size * 0.5f;

            // (outward normal, screen-up when looking at that face from outside)
            var faces = new[]
            {
                (n: Vector3.back, up: Vector3.up), (n: Vector3.forward, up: Vector3.up),
                (n: Vector3.right, up: Vector3.up), (n: Vector3.left, up: Vector3.up),
                (n: Vector3.up, up: Vector3.forward), (n: Vector3.down, up: Vector3.forward),
            };
            foreach (var f in faces)
            {
                Vector3 r = Vector3.Cross(f.up, -f.n);           // screen-right for a viewer facing this face
                float hr = Mathf.Abs(Vector3.Dot(h, r));
                float hu = Mathf.Abs(Vector3.Dot(h, f.up));
                float hn = Mathf.Abs(Vector3.Dot(h, f.n));
                Vector3 c = f.n * hn;
                int b = verts.Count;
                Vector3[] corners =
                {
                    c - r * hr - f.up * hu,   // bottom-left
                    c - r * hr + f.up * hu,   // top-left
                    c + r * hr + f.up * hu,   // top-right
                    c + r * hr - f.up * hu,   // bottom-right
                };
                foreach (var v in corners)
                {
                    verts.Add(v);
                    norms.Add(f.n);
                    uvs.Add(new Vector2((Vector3.Dot(v - c, r) + hr) / texWorldSize.x, (Vector3.Dot(v - c, f.up) + hu) / texWorldSize.y));
                }
                tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 }); // clockwise = front face in Unity
            }
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            if (isNew) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static GameObject SavePrefab(GameObject temp, string dir)
        {
            string path = dir + "/" + temp.name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        // ---------------------------------------------------------------------
        // Scene assembly
        // ---------------------------------------------------------------------

        static void BuildScene(Mats m, Prefabs p)
        {
            // ----- Lighting / environment -----
            RenderSettings.ambientMode = AmbientMode.Flat;
            // Bright, flat ambient keeps the pencil artwork readable; lights add only gentle modelling.
            RenderSettings.ambientLight = new Color(0.68f, 0.67f, 0.70f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            var sun = new GameObject("KeyLight");
            var sl = sun.AddComponent<Light>();
            sl.type = LightType.Directional;
            sl.color = new Color(1f, 0.97f, 0.92f);
            sl.intensity = 0.28f;
            sl.shadows = LightShadows.Soft;
            sl.shadowStrength = 0.35f;
            sun.transform.rotation = Quaternion.Euler(55f, -25f, 0f);

            // Post-processing: warm, slightly under-exposed, vignetted, to sit closer to the reference mood.
            var volGo = new GameObject("PostProcessVolume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = GetOrCreatePostProfile();

            // ----- Architecture -----
            var arch = new GameObject("Architecture").transform;
            var floors = Group("Floor", arch);
            var ceilings = Group("Ceiling", arch);
            var walls = Group("Walls", arch);
            var lights = Group("CeilingLights", arch);

            // Floor + ceiling tiles (corridor + office)
            TileArea(p.FloorTile, floors, CorX0, CorX1, CorZ0, CorZ1);
            TileArea(p.FloorTile, floors, OffX0, OffX1, OffZ0, OffZ1);
            TileArea(p.CeilingTile, ceilings, CorX0, CorX1, CorZ0, CorZ1);
            TileArea(p.CeilingTile, ceilings, OffX0, OffX1, OffZ0, OffZ1);

            // Corridor walls. Each run: start point, direction, length, yaw so that the textured face looks into the room.
            // Inside corners: one wall runs through into the other's thickness so no two exposed faces are coplanar.
            // A run may overrun by up to 1 m; the overrun always ends inside another wall or outside the rooms.
            WallRun(p, walls, new Vector3(CorX0, 0, CorZ0 + WallT), Vector3.forward, CorZ1 - CorZ0, -90f, "Corridor_West");
            WallRun(p, walls, new Vector3(CorX1, 0, CorZ0), Vector3.forward, CorZ1 - CorZ0 + WallT, 90f, "Corridor_East");
            WallRun(p, walls, new Vector3(CorX0 - WallT, 0, CorZ1), Vector3.right, CorX1 - CorX0 + 2f * WallT, 0f, "Corridor_End");

            // Office walls. The north wall runs 1 m past the corridor's west wall so the office opening is 2 m wide:
            // this hides the spawn/window from the caretaker's desk and gives the player a corner to hide in.
            WallRun(p, walls, new Vector3(OffX0, 0, OffZ1), Vector3.right, (CorX0 + 1f) - OffX0, 0f, "Office_North");
            WallRun(p, walls, new Vector3(OffX0, 0, OffZ0), Vector3.right, OffX1 - OffX0, 180f, "Office_South");
            WallRun(p, walls, new Vector3(OffX0, 0, OffZ0), Vector3.forward, OffZ1 - OffZ0, -90f, "Office_West");
            WallRun(p, walls, new Vector3(OffX1, 0, OffZ0), Vector3.forward, OffZ1 - OffZ0, 90f, "Office_East");

            // Ceiling lights
            foreach (float z in new[] { 4f, 8f, 12f, 16f, 19f })
                Place(p.CeilingLight, lights, new Vector3(0, 0, z), 0f, "CeilingLight_Corridor_" + z);
            Place(p.CeilingLight, lights, new Vector3(-2.5f, 0, 0f), 90f, "CeilingLight_Office_A");
            Place(p.CeilingLight, lights, new Vector3(0.3f, 0, 0f), 90f, "CeilingLight_Office_B");

            // ----- Doors -----
            var doors = Group("Doors", arch);
            float wx = CorX0 + 0.005f; // flush with the west wall's inner face
            float ex = CorX1 - 0.005f;
            // West wall doors: front must face +X -> yaw -90 (local -Z -> +X)
            Place(p.DoorClassroom, doors, new Vector3(wx, 0, 5.5f), -90f, "Door_Year6_West");
            Place(p.DoorClassroom, doors, new Vector3(wx, 0, 12.5f), -90f, "Door_Year4_West");
            // East wall doors: front must face -X -> yaw 90
            Place(p.DoorClassroom, doors, new Vector3(ex, 0, 9f), 90f, "Door_Year5_East");
            Place(p.DoorClassroom, doors, new Vector3(ex, 0, 16f), 90f, "Door_Year3_East");
            // Fire doors on the end wall: front faces -Z -> yaw 0
            Place(p.DoorFire, doors, new Vector3(0, 0, CorZ1 - 0.005f), 0f, "Door_FireExit_End");
            // Office door on the office south wall: front faces +Z -> yaw 180
            Place(p.DoorOffice, doors, new Vector3(-1.2f, 0, OffZ0 + 0.005f), 180f, "Door_Office_South");

            // ----- Props -----
            var props = new GameObject("Props").transform;
            Place(p.Desk, props, new Vector3(-2.3f, 0, 1.55f), 0f, "Desk");
            Place(p.Box, props, new Vector3(-1.75f, 0.75f, 1.45f), -12f, "ConfiscatedBox");
            var mug = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mug.name = "Placeholder_Mug";
            mug.transform.SetParent(props, false);
            mug.transform.position = new Vector3(-2.7f, 0.80f, 1.35f);
            mug.transform.localScale = new Vector3(0.09f, 0.05f, 0.09f);
            mug.GetComponent<MeshRenderer>().sharedMaterial = m.White;
            Place(p.Chair, props, new Vector3(1.0f, 0, 9.5f), -30f, "Chair_Corridor");
            Place(p.Chair, props, new Vector3(-3.4f, 0, 0.9f), 200f, "Chair_Office");
            // Noticeboard on the office east wall, front faces -X -> yaw 90
            Place(p.Noticeboard, props, new Vector3(CorX1 - 0.02f, 1.6f, 1.2f), 90f, "Noticeboard_Office");
            // Poster-sized noticeboards down the corridor for scale/rhythm (placeholder colour)
            Place(p.Noticeboard, props, new Vector3(CorX0 + 0.02f, 1.7f, 8.5f), -90f, "Noticeboard_Corridor_West");
            Place(p.Noticeboard, props, new Vector3(CorX1 - 0.02f, 1.7f, 13f), 90f, "Noticeboard_Corridor_East");
            // Escape window on the corridor east wall beside the spawn, front faces -X -> yaw 90
            Place(p.Window, props, WindowPos, 90f, "Window_Escape");

            // ----- Dynamic gameplay objects (not static) -----
            var dynamic = new GameObject("Gameplay").transform;
            // Football on the corridor's west side, out of the caretaker's sight line from his desk.
            Place(p.Football, dynamic, new Vector3(-1.0f, 0.12f, 6.5f), 0f, "Football");

            // Caretaker starts at his desk. Patrol: desk (facing the desk) <-> office door (facing the door).
            var patrolRoot = Group("CaretakerPatrol", dynamic);
            var pDesk = Group("P0_Desk", patrolRoot); pDesk.position = new Vector3(-2.3f, 0f, 0.45f);
            var pDoor = Group("P1_OfficeDoor", patrolRoot); pDoor.position = new Vector3(-1.2f, 0f, -1.25f);
            var pCorridor = Group("P2_CorridorPeek", patrolRoot); pCorridor.position = new Vector3(-0.6f, 0f, 2.4f);
            var caretakerGo = Place(p.Caretaker, dynamic, pDesk.position, 0f, "Caretaker");
            var ai = caretakerGo.GetComponent<CaretakerAI>();
            ai.patrol.Add(new CaretakerAI.PatrolPoint { point = pDesk, dwellSeconds = 7f, faceDirection = new Vector3(-0.35f, 0f, 1f) });
            ai.patrol.Add(new CaretakerAI.PatrolPoint { point = pDoor, dwellSeconds = 3f, faceDirection = Vector3.back });
            ai.patrol.Add(new CaretakerAI.PatrolPoint { point = pDesk, dwellSeconds = 5f, faceDirection = new Vector3(-0.35f, 0f, 1f) });
            ai.patrol.Add(new CaretakerAI.PatrolPoint { point = pCorridor, dwellSeconds = 2.5f, faceDirection = Vector3.forward });

            // ----- Player -----
            var player = new GameObject("Player");
            player.tag = "Player";
            player.layer = LayerMask.NameToLayer("Player");
            player.transform.position = SpawnPos;
            player.transform.rotation = Quaternion.Euler(0f, SpawnYaw, 0f);
            var cc = player.AddComponent<CharacterController>();
            cc.height = FirstPersonController.ChildBodyHeight;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, FirstPersonController.ChildBodyHeight*.5f, 0);
            cc.stepOffset = 0.22f;
            cc.skinWidth = 0.05f;
            var camGo = new GameObject("PlayerCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0, EyeHeight, 0);
            camGo.transform.localRotation = Quaternion.Euler(RefPitch, 0, 0);
            var cam = camGo.AddComponent<Camera>();
            SetupCamera(cam);
            camGo.AddComponent<AudioListener>();
            var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            var fpc = player.AddComponent<FirstPersonController>();
            var so = new SerializedObject(fpc);
            so.FindProperty("inputActions").objectReferenceValue = inputAsset;
            so.FindProperty("cameraPivot").objectReferenceValue = camGo.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var interactor = player.AddComponent<PlayerInteractor>();
            var iso = new SerializedObject(interactor);
            iso.FindProperty("inputActions").objectReferenceValue = inputAsset;
            iso.FindProperty("viewCamera").objectReferenceValue = cam;
            iso.FindProperty("hitMask").intValue = ~((1 << LayerMask.NameToLayer("Enemy")) | (1 << LayerMask.NameToLayer("Player")));
            iso.ApplyModifiedPropertiesWithoutUndo();

            var ringer = player.AddComponent<PhoneRinger>();

            // ----- HUD -----
            var hud = BuildHud();

            // ----- Game manager -----
            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            var gso = new SerializedObject(gm);
            gso.FindProperty("player").objectReferenceValue = fpc;
            gso.FindProperty("interactor").objectReferenceValue = interactor;
            gso.FindProperty("phoneRinger").objectReferenceValue = ringer;
            gso.FindProperty("caretaker").objectReferenceValue = ai;
            gso.ApplyModifiedPropertiesWithoutUndo();

            // ----- NavMesh surface (baked after the scene is saved) -----
            var navGo = new GameObject("NavMesh");
            var surface = navGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = 1 << 0; // Default layer only: architecture + props. Player/Enemy excluded.
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.08f;

            // ----- Capture cameras (disabled; used by Confiscated/Capture Reference Screenshots) -----
            var capRoot = new GameObject("CaptureCameras");
            AddCaptureCamera(capRoot.transform, "01_ReferenceView", RefCamPos, new Vector3(RefPitch, 0, 0));
            AddCaptureCamera(capRoot.transform, "02_OfficeDesk", new Vector3(1.0f, 1.6f, 1.6f), new Vector3(12f, -120f, 0));
            AddCaptureCamera(capRoot.transform, "03_CorridorLookingBack", new Vector3(0.6f, 1.55f, 18.5f), new Vector3(3f, 182f, 0));

            // ----- Static flags for batching -----
            SetStaticRecursive(arch.gameObject);
            SetStaticRecursive(props.gameObject);
        }

        static void BakeNavMesh()
        {
            var surface = Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null) { Debug.LogError("[Confiscated] No NavMeshSurface to bake."); return; }
            surface.BuildNavMesh();
            Debug.Log("[Confiscated] NavMesh baked: " + (surface.navMeshData != null ? surface.navMeshData.name : "null"));
        }

        static void SetupCamera(Camera cam)
        {
            cam.fieldOfView = RefFov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f);
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
        }

        static VolumeProfile GetOrCreatePostProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProfilePath);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.34f);
            vig.smoothness.Override(0.6f);
            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(-0.12f);
            ca.saturation.Override(-12f);
            ca.colorFilter.Override(new Color(1f, 0.96f, 0.90f));
            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.Neutral);
            // Volume components must be sub-assets of the profile or they are lost on reload.
            foreach (var comp in profile.components) AssetDatabase.AddObjectToAsset(comp, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static void AddCaptureCamera(Transform parent, string name, Vector3 pos, Vector3 euler)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(euler);
            var cam = go.AddComponent<Camera>();
            SetupCamera(cam);
            cam.enabled = false;
        }

        static HudController BuildHud()
        {
            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f; // match height so the phone keeps its size on wide screens
            var hud = canvasGo.AddComponent<HudController>();
            var font = SchoolTypography.Font;

            // Held phone: the art has the forearm running off the bottom and right edges, so the image is pinned
            // to the bottom-right corner at its native square aspect (720 px on a 1080 px tall reference screen).
            var phone = new GameObject("HeldPhone");
            phone.transform.SetParent(canvasGo.transform, false);
            var img = phone.AddComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexPhone);
            img.preserveAspect = true;
            img.raycastTarget = false;
            var rt = phone.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.sizeDelta = new Vector2(720, 720);
            rt.anchoredPosition = new Vector2(6, -6);
            hud.phoneImage = img;

            hud.phoneText = MakeText(canvasGo.transform, "PhoneText", font, 30, TextAnchor.LowerRight,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 730), new Vector2(700, 50), new Color(1f, 0.35f, 0.3f));
            hud.objectiveText = MakeText(canvasGo.transform, "Objective", font, 28, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -24), new Vector2(760, 140), new Color(0.98f, 0.95f, 0.88f));
            hud.statusText = MakeText(canvasGo.transform, "Status", font, 34, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(1200, 60), new Color(1f, 0.85f, 0.4f));
            hud.promptText = MakeText(canvasGo.transform, "Prompt", font, 30, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(1200, 50), Color.white);

            // Hold bar (for the window climb, and any other hold-to-interact prompt)
            var barBg = new GameObject("HoldBarBg");
            barBg.transform.SetParent(canvasGo.transform, false);
            var bgImg = barBg.AddComponent<Image>();
            bgImg.color = new Color(0.055f, 0.075f, 0.105f, 0.88f);
            bgImg.raycastTarget = false;
            var bgRt = barBg.GetComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0);
            bgRt.anchoredPosition = new Vector2(0, 122);
            bgRt.sizeDelta = new Vector2(360, 32);
            var borderGo = new GameObject("Pencil edge");
            borderGo.transform.SetParent(barBg.transform, false);
            var border = borderGo.AddComponent<SketchBorder>();
            border.color = new Color(0.98f, 0.95f, 0.88f);
            border.raycastTarget = false;
            var borderRt = borderGo.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero; borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero; borderRt.offsetMax = Vector2.zero;
            var bar = new GameObject("HoldBar");
            bar.transform.SetParent(barBg.transform, false);
            var barImg = bar.AddComponent<Image>();
            barImg.color = new Color(1f, 0.87f, 0.51f);
            barImg.raycastTarget = false;
            barImg.type = Image.Type.Simple;
            barImg.fillAmount = 0f;
            var barRt = bar.GetComponent<RectTransform>();
            barRt.anchorMin = Vector2.zero; barRt.anchorMax = Vector2.one;
            barRt.offsetMin = new Vector2(5, 5); barRt.offsetMax = new Vector2(-5, -5);
            barRt.pivot = new Vector2(0, .5f);
            barRt.localScale = new Vector3(0, 1, 1);
            hud.holdBar = barImg;
            barBg.SetActive(false);

            // Crosshair
            var dot = new GameObject("Crosshair");
            dot.transform.SetParent(canvasGo.transform, false);
            var dimg = dot.AddComponent<Image>();
            dimg.color = new Color(1, 1, 1, 0.85f);
            dimg.raycastTarget = false;
            var drt = dot.GetComponent<RectTransform>();
            drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(6, 6);

            // End-of-round overlay
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(canvasGo.transform, false);
            var oimg = overlay.AddComponent<Image>();
            oimg.color = new Color(0.05f, 0.05f, 0.08f, 0.82f);
            oimg.raycastTarget = false;
            var ort = overlay.GetComponent<RectTransform>();
            ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one;
            ort.offsetMin = ort.offsetMax = Vector2.zero;
            hud.overlay = overlay;
            hud.overlayTitle = MakeText(overlay.transform, "Title", font, 96, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(1400, 140), new Color(0.98f, 0.95f, 0.88f));
            hud.overlayBody = MakeText(overlay.transform, "Body", font, 36, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1400, 200), Color.white);
            overlay.SetActive(false);
            return hud;
        }

        static Text MakeText(Transform parent, string name, Font font, int size, TextAnchor anchor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(2, -2);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(anchorMin.x, anchorMin.y);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return t;
        }

        // ---------------------------------------------------------------------
        // Placement helpers
        // ---------------------------------------------------------------------

        static Transform Group(string name, Transform parent)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g.transform;
        }

        static void TileArea(GameObject prefab, Transform parent, float x0, float x1, float z0, float z1)
        {
            for (float x = x0; x < x1 - 0.001f; x += Tile)
                for (float z = z0; z < z1 - 0.001f; z += Tile)
                    Place(prefab, parent, new Vector3(x + Tile * 0.5f, 0, z + Tile * 0.5f), 0f, null);
        }

        /// <summary>Lay wall segments from 'start' along 'dir' for 'length' metres using 2 m pieces and a 1 m piece for the remainder.</summary>
        static void WallRun(Prefabs p, Transform parent, Vector3 start, Vector3 dir, float length, float yaw, string runName)
        {
            var run = Group(runName, parent);
            float placed = 0f;
            int i = 0;
            while (placed < length - 0.001f)
            {
                float remaining = length - placed;
                bool half = remaining < WallLen - 0.001f;
                float segLen = half ? WallLen * 0.5f : WallLen;
                Vector3 centre = start + dir * (placed + segLen * 0.5f);
                Place(half ? p.Wall1m : p.Wall2m, run, centre, yaw, runName + "_" + (i++));
                placed += segLen;
            }
        }

        static GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float yaw, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (!string.IsNullOrEmpty(name)) go.name = name;
            return go;
        }

        static void SetStaticRecursive(GameObject go)
        {
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes) if (s.path == scenePath) return;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);
            list.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}

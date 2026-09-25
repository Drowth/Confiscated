using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Art batch 3: corridor door leaves, collectible and tool cutouts, the cage mesh and padlock, storage unit fronts.</summary>
    public static class Batch3ArtSetup
    {
        const string Textures = "Assets/Art/Textures/", Materials = "Assets/Art/Materials/", Prefabs = "Assets/Prefabs/SchoolRun/";
        const string CardName = "Cutout card";
        public static bool Available => File.Exists(Textures + "T_Item_YoYo.png") && File.Exists(Textures + "T_Door_Corridor_Double.png");

        [MenuItem("Confiscated/School Run/Apply Art Batch 3")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play before applying art.");
            if (EditorSceneManager.GetActiveScene().path != SchoolLayoutBuilder.ScenePath) throw new InvalidOperationException("Open SchoolLayout first.");
            ApplyToScene();
            EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            Debug.Log("[ArtBatch3] Applied corridor doors, item/tool cutouts, cage mesh, padlock and storage fronts.");
        }

        /// <summary>Repeatable. Does not save; callers own that.</summary>
        public static void ApplyToScene()
        {
            if (!Available) throw new InvalidOperationException("Art batch 3 textures are missing from " + Textures);
            AssetDatabase.Refresh();
            ApplyCorridorDoors();
            // The decoy prefab first: scene instances and the deployed toy both follow it.
            RebuildToyPrefab(); RebuildStoragePrefab();
            string[] items = { null, "T_Item_YoYo", "T_Item_HandheldGame", "T_Item_Skateboard", "T_Item_ToyRobot" };
            float[] heights = { 0, .40f, .42f, .60f, .62f };
            foreach (var pickup in Object.FindObjectsByType<RunPickup>(FindObjectsSortMode.None))
            {
                if (pickup.itemId < 1 || pickup.itemId > 4 || pickup.visual == null) continue;
                Card(pickup.visual.transform, items[pickup.itemId], heights[pickup.itemId], .02f, true); EditorUtility.SetDirty(pickup);
            }
            foreach (var tool in Object.FindObjectsByType<AccessToolPickup>(FindObjectsSortMode.None))
                if (tool.visual != null) Card(tool.visual.transform, tool.tool == AccessToolPickup.Tool.BoltCutters ? "T_Tool_BoltCutters" : "T_Tool_StoreKey", tool.tool == AccessToolPickup.Tool.BoltCutters ? .45f : .42f, .02f, true);
            ApplyCage();
            // Cutouts are the fallback; any generated model present replaces its card.
            if (PickupModelSetup.Available) PickupModelSetup.ApplyToScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        public static void ApplyCorridorDoors()
        {
            if (!File.Exists(Textures + "T_Door_Corridor_Double.png")) return;
            string path = Materials + "M_Door_Corridor.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) == null && !AssetDatabase.CopyAsset(Materials + "M_Door_Fire.mat", path)) throw new Exception("Could not derive the corridor door material.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            material.SetTexture("_BaseMap", Import("T_Door_Corridor_Double", false, false));
            // Leaf edges sample plain slate-blue hatching, clear of the kick plate and glass.
            material.SetVector("_SideRect", new Vector4(.06f, .2f, .26f, .24f)); EditorUtility.SetDirty(material);
            foreach (var doors in Object.FindObjectsByType<LessonCorridorDoors>(FindObjectsSortMode.None))
                foreach (var leaf in doors.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Leaf")) { leaf.sharedMaterial = material; EditorUtility.SetDirty(leaf); }
        }

        static void RebuildToyPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(Prefabs + "P_WindUpToy.prefab");
            try { Card(root.transform, "T_Item_WindUpToy", .42f, 0, true); PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "P_WindUpToy.prefab"); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void RebuildStoragePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(Prefabs + "P_TallStorage.prefab");
            try
            {
                foreach (var old in root.transform.Cast<Transform>().Where(t => t.name == "Shelf" || t.name == "Box of supplies" || t.name == "Illustrated front" || t.name == "Cabinet top").ToArray()) Object.DestroyImmediate(old.gameObject);
                var wood = AssetDatabase.LoadAssetAtPath<Material>(Materials + "M_Wood_Desk.mat");
                var top = GameObject.CreatePrimitive(PrimitiveType.Cube); top.name = "Cabinet top"; top.transform.SetParent(root.transform, false);
                top.transform.localPosition = new Vector3(0, 1.965f, 0); top.transform.localScale = new Vector3(1.8f, .07f, .72f);
                IllustratedArtSetup.Tiled(top.GetComponent<MeshRenderer>(), wood, "run", 1);
                // The open face looks along local -Z; the illustrated front closes it as one solid cabinet.
                var front = GameObject.CreatePrimitive(PrimitiveType.Quad); front.name = "Illustrated front"; front.transform.SetParent(root.transform, false);
                // Six millimetres proud of the side panels and top: sharing their front plane makes the edges flicker.
                front.transform.localPosition = new Vector3(0, 1, -.366f); front.transform.localScale = new Vector3(1.8f, 2, 1);
                Object.DestroyImmediate(front.GetComponent<Collider>());
                var solid = front.AddComponent<BoxCollider>(); solid.size = new Vector3(1, 1, .04f); solid.center = new Vector3(0, 0, .02f);
                front.GetComponent<MeshRenderer>().sharedMaterial = Surface("M_Storage_Shelves", "T_Storage_Shelves", false, false);
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "P_TallStorage.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void ApplyCage()
        {
            var gate = Object.FindObjectsByType<RunGate>(FindObjectsSortMode.None).FirstOrDefault(g => g.kind == RunGate.Kind.Chain);
            if (gate == null || gate.obstacle == null) return;
            var leaf = gate.obstacle; var scale = leaf.localScale;
            foreach (var old in leaf.Cast<Transform>().Where(t => t.name == "Chain" || t.name == "Mesh face" || t.name == "Gate frame").ToArray()) Object.DestroyImmediate(old.gameObject);
            // The solid leaf keeps its collider (sight and passage); only its drawing changes.
            leaf.GetComponent<MeshRenderer>().enabled = false;
            var mesh = Surface("M_Cage_Mesh", "T_Cage_Mesh", true, true); mesh.SetTextureScale("_BaseMap", new Vector2(scale.x, scale.y)); EditorUtility.SetDirty(mesh);
            foreach (float yaw in new[] { 0f, 180f })
            {
                var face = GameObject.CreatePrimitive(PrimitiveType.Quad); face.name = "Mesh face"; Object.DestroyImmediate(face.GetComponent<Collider>());
                face.transform.SetParent(leaf, false); face.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                var r = face.GetComponent<MeshRenderer>(); r.sharedMaterial = mesh; r.shadowCastingMode = ShadowCastingMode.Off;
            }
            var grey = AssetDatabase.LoadAssetAtPath<Material>(Materials + "M_Chapter_Grey.mat");
            var frame = new GameObject("Gate frame").transform; frame.SetParent(leaf, false);
            const float bar = .06f;
            foreach (float x in new[] { -1f, 1f }) Bar(frame, new Vector3(x * (.5f - bar * .5f / scale.x), 0, 0), new Vector3(bar / scale.x, 1, 1), grey);
            foreach (float y in new[] { -1f, 0f, 1f }) Bar(frame, new Vector3(0, y * (.5f - bar * .5f / scale.y), 0), new Vector3(1, bar / scale.y, 1), grey);

            var feedback = gate.GetComponent<ProgressPropFeedback>();
            if (feedback != null && feedback.padlock != null)
            {
                // Outside face of the gate, below the notice, where the player actually stands.
                feedback.padlock.localPosition = new Vector3(0, .78f, .1f); feedback.padlock.localRotation = Quaternion.identity;
                Card(feedback.padlock, "T_Padlock_Chain", .5f, 0, false); EditorUtility.SetDirty(feedback);
            }
        }
        static void Bar(Transform parent, Vector3 local, Vector3 size, Material material)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = "Frame bar"; Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false); g.transform.localPosition = local; g.transform.localScale = size; IllustratedArtSetup.Tiled(g.GetComponent<MeshRenderer>(), material, "run", 1);
        }

        /// <summary>Replaces everything under <paramref name="visual"/> with a two-sided cutout standing on <paramref name="baseY"/>.</summary>
        static void Card(Transform visual, string texture, float canvasHeight, float baseY, bool unlit)
        {
            foreach (var old in visual.Cast<Transform>().ToArray()) Object.DestroyImmediate(old.gameObject);
            Import(texture, true, false);
            var probe = new Texture2D(2, 2); probe.LoadImage(File.ReadAllBytes(Textures + texture + ".png"));
            var pixels = probe.GetPixels32(); int w = probe.width, h = probe.height, lowest = h;
            for (int y = 0; y < h && lowest == h; y++) for (int x = 0; x < w; x++) if (pixels[y * w + x].a > 32) { lowest = y; break; }
            Object.DestroyImmediate(probe);
            float width = canvasHeight * w / h, padding = canvasHeight * lowest / h;
            var card = new GameObject(CardName).transform; card.SetParent(visual, false); card.localPosition = new Vector3(0, baseY + canvasHeight * .5f - padding, 0);
            var material = Surface("M_Cutout_" + texture.Substring(2), texture, true, false, unlit);
            foreach (float yaw in new[] { 0f, 180f })
            {
                var face = GameObject.CreatePrimitive(PrimitiveType.Quad); face.name = yaw == 0 ? "Front" : "Back"; Object.DestroyImmediate(face.GetComponent<Collider>());
                face.transform.SetParent(card, false); face.transform.localRotation = Quaternion.Euler(0, yaw, 0); face.transform.localScale = new Vector3(width, canvasHeight, 1);
                var r = face.GetComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            }
        }

        static Texture2D Import(string name, bool alpha, bool repeat)
        {
            string path = Textures + name + ".png"; var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new Exception("Texture not imported: " + path);
            bool changed = importer.alphaIsTransparency != alpha || importer.wrapMode != (repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp) || importer.maxTextureSize != 2048 || importer.mipMapsPreserveCoverage != alpha;
            if (changed)
            {
                importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.alphaIsTransparency = alpha; importer.maxTextureSize = 2048;
                importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; importer.mipmapEnabled = true;
                // Keeps thin wires and outlines from dissolving at corridor distances.
                importer.mipMapsPreserveCoverage = alpha; importer.alphaTestReferenceValue = .4f; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Surface(string name, string texture, bool alpha, bool repeat, bool unlit = false)
        {
            string path = Materials + name + ".mat"; var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.SetTexture("_BaseMap", Import(texture, alpha, repeat)); material.SetColor("_BaseColor", Color.white);
            if (!unlit) { material.SetFloat("_Smoothness", 0); material.SetFloat("_Metallic", 0); }
            material.SetFloat("_AlphaClip", alpha ? 1 : 0); material.SetFloat("_Cutoff", .4f);
            if (alpha) material.EnableKeyword("_ALPHATEST_ON"); else material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = alpha ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            EditorUtility.SetDirty(material); return material;
        }

        [MenuItem("Confiscated/School Run/Capture Art Batch 3")]
        public static void Capture()
        {
            Directory.CreateDirectory("D:/Confiscated/Docs/ArtBatch3");
            var go = new GameObject("Art batch review camera"); var camera = go.AddComponent<Camera>(); camera.fieldOfView = 60;
            try
            {
                foreach (var pickup in Object.FindObjectsByType<RunPickup>(FindObjectsSortMode.None).Where(p => p.itemId > 0)) Shot("Item_" + pickup.itemId + "_" + pickup.itemName.Replace(' ', '_'), pickup.visual.transform, .25f, 1.7f);
                foreach (var tool in Object.FindObjectsByType<AccessToolPickup>(FindObjectsSortMode.None)) Shot("Tool_" + tool.tool, tool.visual.transform, .2f, 1.5f);
                var decoy = Object.FindObjectsByType<DecoyPickup>(FindObjectsSortMode.None).FirstOrDefault(); if (decoy != null) Shot("Decoy", decoy.visual.transform, .2f, 1.5f);
                var gate = Object.FindObjectsByType<RunGate>(FindObjectsSortMode.None).First(g => g.kind == RunGate.Kind.Chain);
                camera.transform.position = gate.transform.position + new Vector3(.6f, 1.25f, 3); camera.transform.LookAt(gate.transform.position + Vector3.up * 1.1f);
                HallwayPropLibrary.Capture(camera, 1500, 950, "D:/Confiscated/Docs/ArtBatch3/Cage.png");
                var shelf = GameObject.Find("SchoolRun").transform.Cast<Transform>().First(t => t.name == "P_TallStorage");
                camera.transform.position = shelf.position - shelf.forward * 3 + shelf.right * 1.2f + Vector3.up * 1.25f; camera.transform.LookAt(shelf.position + Vector3.up * 1.1f);
                HallwayPropLibrary.Capture(camera, 1500, 950, "D:/Confiscated/Docs/ArtBatch3/Storage.png");
                var door = LessonGateSetup.Gates[1];
                camera.transform.position = door.Position + door.wing * 4.5f + Vector3.up * 1.25f; camera.transform.LookAt(door.Position + Vector3.up * 1.25f);
                HallwayPropLibrary.Capture(camera, 1500, 950, "D:/Confiscated/Docs/ArtBatch3/CorridorDoors.png");
            }
            finally { Object.DestroyImmediate(go); }

            // Child-height view from the nearest walkable spot with a clear line to the subject.
            void Shot(string name, Transform visual, float lift, float distance)
            {
                Vector3 target = visual.position + Vector3.up * lift;
                // Start square-on to the card, as it would be at some moment of its spin, then fan outwards.
                for (int i = 0; i < 16; i++)
                {
                    float a = ((i + 1) / 2) * (i % 2 == 0 ? 1 : -1) * 22.5f; Vector3 from = target + Quaternion.Euler(0, a, 0) * -visual.forward * distance;
                    if (!NavMesh.SamplePosition(new Vector3(from.x, 0, from.z), out var hit, .4f, NavMesh.AllAreas)) continue;
                    Vector3 eye = hit.position + Vector3.up * 1.25f; Vector3 toward = target - eye;
                    if (Physics.Raycast(eye, toward.normalized, toward.magnitude - .45f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    camera.transform.position = eye; camera.transform.LookAt(target);
                    HallwayPropLibrary.Capture(camera, 1500, 950, "D:/Confiscated/Docs/ArtBatch3/" + name + ".png"); return;
                }
                Debug.LogWarning("[ArtBatch3] No clear viewpoint for " + name);
            }
        }
    }
}

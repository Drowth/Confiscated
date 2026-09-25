using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// Replaces the cutout cards with the generated 3D models, drawn unlit from their colour map so they keep the flat
    /// pencil look. A model that is missing from the folder simply leaves its cutout card in place.
    /// </summary>
    public static class PickupModelSetup
    {
        const string Models = "Assets/Art/Models/SchoolProps/Pickups/", Materials = "Assets/Art/Materials/", Prefabs = "Assets/Prefabs/SchoolRun/";
        const string HolderName = "Pickup model";
        public static bool Has(string model) => File.Exists(Models + model + ".fbx");
        public static bool Available => Directory.Exists(Models) && Directory.GetFiles(Models, "*.fbx").Length > 0;

        [MenuItem("Confiscated/School Run/Apply 3D Pickup Models")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play before applying models.");
            if (EditorSceneManager.GetActiveScene().path != SchoolLayoutBuilder.ScenePath) throw new InvalidOperationException("Open SchoolLayout first.");
            int count = ApplyToScene();
            EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            Debug.Log("[PickupModels] " + count + " cutouts replaced with unlit 3D models.");
        }

        /// <summary>Repeatable. Does not save; callers own that.</summary>
        public static int ApplyToScene()
        {
            AssetDatabase.Refresh(); int count = 0;
            // Longest side in metres. Deliberately larger than life so a child-height player reads them across a room.
            var items = new (string model, float size)[] { (null, 0), ("YoYo", .32f), ("HandheldGame", .36f), ("Skateboard", .8f), ("ToyRobot", .52f) };
            foreach (var pickup in Object.FindObjectsByType<RunPickup>(FindObjectsSortMode.None))
                if (pickup.itemId >= 1 && pickup.itemId <= 4 && pickup.visual != null && Place(pickup.visual.transform, items[pickup.itemId].model, items[pickup.itemId].size, .02f, 270)) count++;
            foreach (var tool in Object.FindObjectsByType<AccessToolPickup>(FindObjectsSortMode.None))
                if (tool.visual != null && (tool.tool == AccessToolPickup.Tool.BoltCutters ? Place(tool.visual.transform, "BoltCutters", .6f, .02f, 270) : Place(tool.visual.transform, "StoreKey", .36f, .02f, 270))) count++;
            if (Has("WindUpDuck"))
            {
                var root = PrefabUtility.LoadPrefabContents(Prefabs + "P_WindUpToy.prefab");
                try { if (Place(root.transform, "WindUpDuck", .40f, 0, 270)) count++; PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "P_WindUpToy.prefab"); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var gate = Object.FindObjectsByType<RunGate>(FindObjectsSortMode.None).FirstOrDefault(g => g.kind == RunGate.Kind.Chain);
            var feedback = gate != null ? gate.GetComponent<ProgressPropFeedback>() : null;
            if (feedback != null && feedback.padlock != null && Place(feedback.padlock, "Padlock", .42f, 0, 90))
            {
                // Hang it just proud of the gate's outer face so the chain never sinks into the wire mesh.
                var holder = feedback.padlock.Find(HolderName); var bounds = Bounds(holder);
                float back = Vector3.Dot(bounds.center, gate.transform.forward) - Mathf.Abs(Vector3.Dot(bounds.extents, gate.transform.forward));
                float face = Vector3.Dot(gate.transform.position, gate.transform.forward) + .085f;
                holder.position += gate.transform.forward * (face - back); count++;
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return count;
        }

        /// <summary>Everything under <paramref name="visual"/> becomes one centred model standing on <paramref name="baseY"/>.</summary>
        internal static bool Place(Transform visual, string model, float longestSide, float baseY, float yaw)
        {
            if (model == null || !Has(model)) return false;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Models + model + ".fbx"); if (asset == null) return false;
            var material = Material(model); if (material == null) return false;
            foreach (var old in visual.Cast<Transform>().ToArray()) Object.DestroyImmediate(old.gameObject);
            // The holder carries yaw, scale and offset; the model beneath keeps the importer's own axis correction.
            var holder = new GameObject(HolderName).transform; holder.SetParent(visual, false); holder.localRotation = Quaternion.Euler(0, yaw, 0);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder);
            foreach (var r in instance.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            foreach (var c in instance.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var size = Bounds(holder).size; holder.localScale = Vector3.one * (longestSide / Mathf.Max(size.x, size.y, size.z));
            var bounds = Bounds(holder); Vector3 origin = visual.position;
            holder.position += new Vector3(origin.x - bounds.center.x, origin.y + baseY - bounds.min.y, origin.z - bounds.center.z);
            return true;
        }
        static Bounds Bounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds); return bounds;
        }

        /// <summary>The Blender export does not link its embedded maps, so the colour map is extracted and used directly.</summary>
        static Material Material(string model)
        {
            string folder = Models + "Textures/" + model;
            if (!Directory.Exists(folder) || Directory.GetFiles(folder, "Color_*.jpg").Length == 0)
            {
                Directory.CreateDirectory(folder); AssetDatabase.Refresh();
                var importer = (ModelImporter)AssetImporter.GetAtPath(Models + model + ".fbx");
                if (importer == null || !importer.ExtractTextures(folder)) { Debug.LogWarning("[PickupModels] No embedded textures in " + model); return null; }
                AssetDatabase.Refresh();
                // Unlit drawing has no use for the normal map; the FBX still carries it if that ever changes.
                foreach (var normal in Directory.GetFiles(folder, "NormalGL_*.jpg")) AssetDatabase.DeleteAsset(normal.Replace('\\', '/'));
            }
            string colour = Directory.GetFiles(folder, "Color_*.jpg").FirstOrDefault()?.Replace('\\', '/'); if (colour == null) return null;
            string path = Materials + "M_Pickup_" + model + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, path); }
            material.shader = Shader.Find("Universal Render Pipeline/Unlit"); material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(colour)); material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material); return material;
        }
    }
}

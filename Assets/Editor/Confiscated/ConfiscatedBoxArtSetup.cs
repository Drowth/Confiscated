using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Installs the generated, permanently open box without replacing its phone interaction.</summary>
    public static class ConfiscatedBoxArtSetup
    {
        const string Folder = "Assets/Art/Models/ConfiscatedBox/";
        const string ModelPath = Folder + "confiscated_box.fbx";
        const string MaterialPath = Folder + "M_ConfiscatedBox.mat";
        public const string VisualPrefabPath = "Assets/Prefabs/Props/P_ConfiscationBox_Open.prefab";
        const string PickupPrefabPath = "Assets/Prefabs/Props/P_ConfiscatedBox.prefab";
        const string VisualName = "Generated confiscation box";
        static readonly string[] LegacyParts = { "Bottom", "Front", "Back", "Left", "Right",
            "Label_CONFISCATED", "Placeholder_Football_Static", "Placeholder_ToyCar", "Box lid hinge" };

        [MenuItem("Confiscated/Art/Apply Generated Confiscation Box")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play before installing box art.");
            BuildVisualPrefab();
            var prefab = PrefabUtility.LoadPrefabContents(PickupPrefabPath);
            try
            {
                Apply(prefab.GetComponent<PhonePickup>());
                PrefabUtility.SaveAsPrefabAsset(prefab, PickupPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            ApplyToScene();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        static void BuildVisualPrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (model == null || material == null) throw new InvalidOperationException("Import the box model and its colour material first.");
            var root = new GameObject("P_ConfiscationBox_Open");
            try
            {
                var holder = new GameObject("Model orientation").transform;
                holder.SetParent(root.transform, false);
                holder.localRotation = Quaternion.Euler(0, 270, 0); // Label faces local -Z, like the old box.
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                var bounds = Bounds(holder);
                holder.localScale = Vector3.one * (.6f / bounds.size.x);
                bounds = Bounds(holder);
                holder.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                // Static mesh collision leaves the cavity open and lets any visible part target the pickup.
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                }
                PrefabUtility.SaveAsPrefabAsset(root, VisualPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Bounds Bounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        public static void ApplyToScene()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath) == null) return;
            foreach (var pickup in Object.FindObjectsByType<PhonePickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Apply(pickup);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        static void Apply(PhonePickup pickup)
        {
            if (pickup == null) return;
            // Retain the original children as an easy rollback, but disable their renderers and collision together.
            foreach (var name in LegacyParts)
            {
                var part = pickup.transform.Find(name);
                if (part != null) part.gameObject.SetActive(false);
            }
            if (pickup.transform.Find(VisualName) == null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath), pickup.transform);
                visual.name = VisualName;
            }
            if (pickup.phoneVisual != null)
            {
                pickup.phoneVisual.transform.localPosition = new Vector3(.02f, .16f, -.05f);
                EditorUtility.SetDirty(pickup.phoneVisual.transform);
                if (PrefabUtility.IsPartOfPrefabInstance(pickup.phoneVisual.transform))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(pickup.phoneVisual.transform);
            }
            var feedback = pickup.GetComponent<ProgressPropFeedback>();
            if (feedback != null)
            {
                // The FBX has one combined mesh; its lid is already fully open.
                feedback.boxLid = null;
                EditorUtility.SetDirty(feedback);
                if (PrefabUtility.IsPartOfPrefabInstance(feedback)) PrefabUtility.RecordPrefabInstancePropertyModifications(feedback);
            }
            EditorUtility.SetDirty(pickup);
        }
    }
}

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Confiscated.EditorTools
{
    public static class CutoutMotionSetup
    {
        [MenuItem("Confiscated/Apply Caretaker Cutout Animation")]
        public static void Apply()
        {
            const string path = "Assets/Prefabs/Props/P_Caretaker.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            // The old ExecuteAlways billboard wrote scene overrides on Cutout before it was reparented.
            // Reparenting the prefab preserves those overrides, so repair the loaded instances as well.
            foreach (var ai in Object.FindObjectsByType<CaretakerAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!ai.gameObject.scene.IsValid()) continue;
                var pivot = ai.GetComponentInChildren<CutoutMotion>(true);
                var image = pivot != null ? pivot.transform.Find("Cutout") : null;
                if (image == null) continue;
                Undo.RecordObject(image, "Repair caretaker billboard offset");
                var serialized = new SerializedObject(image);
                var rotation = serialized.FindProperty("m_LocalRotation");
                if (PrefabUtility.IsPartOfPrefabInstance(image) && rotation.prefabOverride)
                    PrefabUtility.RevertPropertyOverride(rotation, InteractionMode.AutomatedAction);
                image.localRotation = Quaternion.identity;
                EditorSceneManager.MarkSceneDirty(ai.gameObject.scene);
            }
        }

        /// <summary>Reusable for another character with a direct child named Cutout, centred above its feet.</summary>
        public static void Configure(GameObject root)
        {
            var existing = root.GetComponentInChildren<CutoutMotion>(true);
            if (existing != null)
            {
                var image = existing.transform.Find("Cutout");
                if (image != null) image.localRotation = Quaternion.identity;
                return;
            }
            var cutout = root.transform.Find("Cutout");
            if (cutout == null) throw new System.InvalidOperationException(root.name + " needs a Cutout child.");
            var oldBillboard = cutout.GetComponent<BillboardY>();
            var target = oldBillboard != null ? oldBillboard.targetOverride : null;
            if (oldBillboard != null) Object.DestroyImmediate(oldBillboard);
            var billboard = new GameObject("VisualFacing");
            billboard.layer = root.layer;
            billboard.transform.SetParent(root.transform, false);
            billboard.AddComponent<BillboardY>().targetOverride = target;
            var motion = new GameObject("MotionPivot");
            motion.layer = root.layer;
            motion.transform.SetParent(billboard.transform, false);
            // Both new pivots are at local origin, so the existing feet-aligned image keeps its placement.
            cutout.SetParent(motion.transform, false);
            // The former billboard may have left a camera-facing yaw on this image in edit mode.
            // Only VisualFacing owns yaw; the image must remain neutral beneath it.
            cutout.localRotation = Quaternion.identity;
            motion.AddComponent<CutoutMotion>().movementSource = root.transform;
        }
    }
}

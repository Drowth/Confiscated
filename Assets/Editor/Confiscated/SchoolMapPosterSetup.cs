using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Builds and mounts the west-corridor "you are here" school-map poster.</summary>
    public static class SchoolMapPosterSetup
    {
        const string TexturePath = "Assets/Art/Textures/Props/T_SchoolMapPoster_West.png";
        const string MaterialPath = "Assets/Art/Materials/M_SchoolMapPoster_West.mat";
        const string PrefabPath = "Assets/Prefabs/Hallway/P_SchoolMapPoster_West.prefab";
        const string RootName = "Hallway landmarks";
        const string InstanceName = "West corridor school map poster";
        const string ReviewPath = "D:/Confiscated/output/reviews/west-corridor-map-poster.png";

        [MenuItem("Confiscated/Hallway Props/Install West Corridor Map Poster")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play before installing the map poster.");
            BuildAssets();
            ApplyToScene();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[SchoolMapPoster] Mounted beside the library-cart junction in the west corridor.");
        }

        static void BuildAssets()
        {
            Directory.CreateDirectory("Assets/Prefabs/Hallway");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null) throw new InvalidOperationException("Missing poster texture: " + TexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Cull", 0f);
            material.doubleSidedGI = true;
            EditorUtility.SetDirty(material);

            var root = new GameObject("P_SchoolMapPoster_West");
            try
            {
                var paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
                paper.name = "Sketched school map poster";
                paper.transform.SetParent(root.transform, false);
                paper.transform.localScale = new Vector3(1.35f, 1.652f, 1f);
                Object.DestroyImmediate(paper.GetComponent<Collider>());
                var renderer = paper.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>Repeatable placement. The poster faces west into the corridor, opposite the library cart.</summary>
        public static void ApplyToScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { BuildAssets(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); }
            var parent = GameObject.Find(RootName);
            if (parent == null) parent = new GameObject(RootName);
            var old = parent.transform.Find(InstanceName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var poster = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            poster.name = InstanceName;
            // The dining-hall exterior is the east wall of the west corridor. This point is level with the map's marker.
            // Wall segments are 15 cm thick, so clear the 7.5 cm half-depth before drawing the paper.
            var position = SchoolPlan.Point(296, 760, 1.55f) + Vector3.left * .11f;
            poster.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 90, 0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(poster.transform);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("Confiscated/Hallway Props/Capture West Corridor Map Poster")]
        public static void CaptureReview()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReviewPath));
            var cameraObject = new GameObject("Temporary map-poster review camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = SchoolPlan.Point(268, 760, 1.55f);
                camera.transform.rotation = Quaternion.Euler(0, 90, 0);
                camera.fieldOfView = 42f;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 12f;
                HallwayPropLibrary.Capture(camera, 900, 700, ReviewPath);
                Debug.Log("[SchoolMapPoster] Review captured to " + ReviewPath);
            }
            finally { Object.DestroyImmediate(cameraObject); }
        }
    }
}

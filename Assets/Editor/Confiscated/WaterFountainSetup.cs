using System;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Imports the generated water fountain and installs it as an east-corridor landmark.</summary>
    public static class WaterFountainSetup
    {
        const string Folder = "Assets/Art/Models/WaterFountain/";
        const string ModelPath = Folder + "water_fountain.fbx";
        const string TextureFolder = Folder + "Textures";
        const string MaterialPath = Folder + "M_WaterFountain.mat";
        public const string PrefabPath = "Assets/Prefabs/Hallway/P_Hall_WaterFountain_Generated.prefab";
        const string RootName = "Hallway landmarks";
        const string InstanceName = "East corridor water fountain";

        [MenuItem("Confiscated/Hallway Props/Install Generated Water Fountain")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play before installing the fountain.");
            BuildPrefab();
            ApplyToScene();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[WaterFountain] Installed the generated fountain in the east perimeter hall.");
        }

        static void BuildPrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("Missing " + ModelPath);

            var sourceMaterial = model.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m != null);
            if (sourceMaterial == null) throw new InvalidOperationException("The fountain FBX has no material.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0);
            material.SetFloat("_Metallic", 0);
            material.SetTexture("_BaseMap", FindTexture("Color"));
            var normal = FindTexture("NormalGL");
            if (normal != null)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal));
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                    normal = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(normal));
                }
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", .55f);
                material.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material);

            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceMaterial.name), material);
            modelImporter.importCameras = false;
            modelImporter.importLights = false;
            modelImporter.importAnimation = false;
            modelImporter.bakeAxisConversion = true;
            modelImporter.SaveAndReimport();
            model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

            var root = new GameObject("P_Hall_WaterFountain_Generated");
            try
            {
                var holder = new GameObject("Model orientation").transform;
                holder.SetParent(root.transform, false);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
                instance.name = "Generated water fountain";
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                var bounds = RenderBounds(holder);
                holder.localScale = Vector3.one * (1.18f / Mathf.Max(.001f, bounds.size.y));
                bounds = RenderBounds(holder);
                holder.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                bounds = RenderBounds(root.transform);

                var collider = root.AddComponent<BoxCollider>();
                collider.center = root.transform.InverseTransformPoint(bounds.center);
                collider.size = bounds.size;
                var obstacle = root.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = collider.center;
                obstacle.size = collider.size;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Texture2D FindTexture(string prefix)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }))
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                if (texture != null && texture.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return texture;
            }
            return null;
        }

        static Bounds RenderBounds(Transform transform)
        {
            var renderers = transform.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(transform.position, Vector3.one);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        /// <summary>Repeatable scene placement. Callers own saving.</summary>
        public static void ApplyToScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return;
            var root = GameObject.Find(RootName);
            if (root == null) root = new GameObject(RootName);
            var old = root.transform.Find(InstanceName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var fountain = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            fountain.name = InstanceName;
            // Against the east wall, front facing west into a long, otherwise sparse corridor.
            fountain.transform.SetPositionAndRotation(SchoolPlan.Point(904, 560), Quaternion.Euler(0, 270, 0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(fountain.transform);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}

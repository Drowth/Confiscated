using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Fits the AssetHub waiting-room props onto the existing hand-built scene objects.</summary>
    public static class WaitingRoomAssetHubSetup
    {
        const string Folder = "Assets/Art/Models/SchoolReplacements/";

        [MenuItem("Confiscated/School Run/Apply Waiting Room AssetHub Props")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play mode first.");
            if (EditorSceneManager.GetActiveScene().path != SchoolLayoutBuilder.ScenePath)
                throw new InvalidOperationException("Open SchoolLayout first.");

            AssetDatabase.Refresh();
            var waiting = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "Waiting area" && t.Find("Low table") != null);
            if (waiting == null) throw new InvalidOperationException("School office waiting area is missing.");
            ApplyToWaitingArea(waiting);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[WaitingRoomAssetHub] Replaced the low table and centre school magazine.");
        }

        // SchoolOfficeSetup calls this after placing its fallback primitives on a future rebuild.
        internal static void ApplyToWaitingArea(Transform waiting)
        {
            var table = waiting.Find("Low table");
            if (table == null) throw new InvalidOperationException("Waiting-room low table is missing.");
            Replace(table, "WaitingRoomLowTable", new Vector3(8f, 0f, 15.2f),
                new Vector3(1.2f, .44f, .6f), true);

            // The two cream leaflets stay as paper; the green centre issue is the magazine.
            var magazines = waiting.Cast<Transform>().Where(t => t.name == "Magazine")
                .OrderBy(t => t.position.x).ToArray();
            if (magazines.Length != 3) throw new InvalidOperationException("Expected three waiting-area reading props.");
            Replace(magazines[1], "WaitingRoomMagazine",
                new Vector3(7.98f, .44f, 15.23f), new Vector3(.21f, .012f, .28f), false);
        }

        static void Replace(Transform target, string assetName, Vector3 basePosition, Vector3 targetSize, bool furniture)
        {
            var path = Folder + assetName + "/";
            var mesh = AssetDatabase.LoadAllAssetsAtPath(path + assetName + ".fbx")
                .OfType<Mesh>().Where(m => m.vertexCount > 0)
                .OrderByDescending(m => m.vertexCount).FirstOrDefault();
            var colour = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "Color.png");
            if (mesh == null || colour == null)
            {
                Debug.LogWarning("[WaitingRoomAssetHub] Missing " + assetName + " mesh or colour map; keeping fallback.");
                return;
            }

            // These tiny props do not need the 4K maps produced by AssetHub.
            var texturePath = path + "Color.png";
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            var maxSize = furniture ? 2048 : 1024;
            if (importer != null && importer.maxTextureSize != maxSize)
            {
                importer.maxTextureSize = maxSize;
                importer.SaveAndReimport();
                colour = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }

            var materialPath = path + "M_" + assetName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", colour);
            material.SetColor("_BaseColor", furniture ? new Color(.68f, .64f, .59f) : Color.white);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(material);

            var filter = target.GetComponent<MeshFilter>();
            var renderer = target.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null) throw new InvalidOperationException("Fallback prop lost its mesh components: " + target.name);
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            var bounds = mesh.bounds;
            target.localScale = new Vector3(targetSize.x / bounds.size.x,
                targetSize.y / bounds.size.y, targetSize.z / bounds.size.z);
            // Keep the fallback object's established yaw. Blender has baked the model's axis correction.
            var scale = target.lossyScale;
            target.position = new Vector3(basePosition.x - bounds.center.x * scale.x,
                basePosition.y - bounds.min.y * scale.y,
                basePosition.z - bounds.center.z * scale.z);

            var collider = target.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.center = bounds.center;
                collider.size = bounds.size;
                EditorUtility.SetDirty(collider);
            }
            if (furniture)
            {
                var obstacle = target.GetComponent<NavMeshObstacle>();
                if (obstacle != null)
                {
                    obstacle.center = bounds.center;
                    obstacle.size = bounds.size;
                    EditorUtility.SetDirty(obstacle);
                }
            }
            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(target);
        }
    }
}

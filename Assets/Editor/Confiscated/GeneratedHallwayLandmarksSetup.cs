using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Builds and places the generated trophy, library and science landmarks.</summary>
    public static class GeneratedHallwayLandmarksSetup
    {
        const string RootName = "Hallway landmarks";
        readonly struct Landmark
        {
            public readonly string folder, model, prefab, material, instance;
            public readonly float height, x, y, yaw;
            public Landmark(string folder, string model, string prefab, string material, string instance,
                float height, float x, float y, float yaw)
            {
                this.folder=folder;this.model=model;this.prefab=prefab;this.material=material;this.instance=instance;
                this.height=height;this.x=x;this.y=y;this.yaw=yaw;
            }
        }

        static readonly Landmark[] Landmarks =
        {
            new("TrophyCabinet","TrophyCabinet.fbx","P_Hall_TrophyCabinet_Generated.prefab","M_TrophyCabinet.mat",
                "North hall trophy cabinet",1.75f,540,257,90),
            new("LibraryReturnCart","LibraryReturnCart.fbx","P_Hall_LibraryReturnCart_Generated.prefab","M_LibraryReturnCart.mat",
                "West hall library return cart",1.45f,268,760,0),
            new("SkeletonCase","SkeletonCase.fbx","P_Hall_SkeletonCase_Generated.prefab","M_SkeletonCase.mat",
                "North cross hall skeleton case",1.90f,690,480,270)
        };

        [MenuItem("Confiscated/Hallway Props/Install Generated Landmark Set")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play before installing corridor landmarks.");
            foreach(var landmark in Landmarks)BuildPrefab(landmark);
            ApplyToScene();
            EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            Debug.Log("[HallwayLandmarks] Installed trophy cabinet, library return cart and skeleton case.");
        }

        static void BuildPrefab(Landmark landmark)
        {
            string folder="Assets/Art/Models/"+landmark.folder+"/";
            string modelPath=folder+landmark.model;
            string textureFolder=folder+"Textures";
            string materialPath=folder+landmark.material;
            string prefabPath="Assets/Prefabs/Hallway/"+landmark.prefab;
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if(model==null)throw new InvalidOperationException("Missing "+modelPath);
            var source=model.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).FirstOrDefault(m=>m!=null);
            if(source==null)throw new InvalidOperationException(landmark.model+" has no material.");

            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath);}
            material.shader=Shader.Find("Universal Render Pipeline/Lit");material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Smoothness",0);material.SetFloat("_Metallic",0);
            material.SetTexture("_BaseMap",FindTexture(textureFolder,"Color"));
            var normal=FindTexture(textureFolder,"NormalGL");
            if(normal!=null)
            {
                string normalPath=AssetDatabase.GetAssetPath(normal);var textureImporter=(TextureImporter)AssetImporter.GetAtPath(normalPath);
                if(textureImporter!=null&&textureImporter.textureType!=TextureImporterType.NormalMap)
                {textureImporter.textureType=TextureImporterType.NormalMap;textureImporter.SaveAndReimport();normal=AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);}
                material.SetTexture("_BumpMap",normal);material.SetFloat("_BumpScale",.55f);material.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material);

            var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),source.name),material);
            importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;importer.bakeAxisConversion=true;
            importer.SaveAndReimport();model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

            var root=new GameObject(landmark.prefab.Replace(".prefab",""));
            try
            {
                var holder=new GameObject("Model orientation").transform;holder.SetParent(root.transform,false);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(model,holder);instance.name="Generated "+landmark.instance;
                foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;}
                var bounds=RenderBounds(holder);holder.localScale=Vector3.one*(landmark.height/Mathf.Max(.001f,bounds.size.y));
                bounds=RenderBounds(holder);holder.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
                bounds=RenderBounds(root.transform);
                var collider=root.AddComponent<BoxCollider>();collider.center=root.transform.InverseTransformPoint(bounds.center);collider.size=bounds.size;
                var obstacle=root.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=collider.center;
                obstacle.size=collider.size;obstacle.carving=true;obstacle.carveOnlyStationary=true;
                PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            }
            finally{Object.DestroyImmediate(root);}
        }

        static Texture2D FindTexture(string folder,string prefix)
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{folder}))
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                if(texture!=null&&texture.name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))return texture;
            }
            return null;
        }

        static Bounds RenderBounds(Transform root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0)return new Bounds(root.position,Vector3.one);
            var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);return bounds;
        }

        /// <summary>Repeatable scene placement. Callers own saving.</summary>
        public static void ApplyToScene()
        {
            var root=GameObject.Find(RootName);if(root==null)root=new GameObject(RootName);
            foreach(var landmark in Landmarks)
            {
                var old=root.transform.Find(landmark.instance);if(old!=null)Object.DestroyImmediate(old.gameObject);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hallway/"+landmark.prefab);if(prefab==null)continue;
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform);instance.name=landmark.instance;
                instance.transform.SetPositionAndRotation(SchoolPlan.Point(landmark.x,landmark.y),Quaternion.Euler(0,landmark.yaw,0));
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}

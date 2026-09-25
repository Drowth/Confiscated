using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    public static class GlueSetup
    {
        const string PuddlePath="Assets/Prefabs/SchoolRun/P_GluePuddle.prefab";
        [MenuItem("Confiscated/School Run/Install Glue Pickup")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play before installing glue.");
            ApplyToScene();EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
        public static void ApplyToScene()
        {
            var run=Object.FindFirstObjectByType<SchoolRunController>();
            if(run==null)throw new InvalidOperationException("Open SchoolLayout first.");
            var floor=ImportTexture("Assets/Art/Textures/Glue/T_Glue_Puddle_v1.png",false);
            ImportTexture("Assets/Art/Textures/Glue/T_Glue_Bottle_v1.png",true);
            foreach(string clip in new[]{"GlueDrop","GlueStuck"})
            {
                var importer=(AudioImporter)AssetImporter.GetAtPath("Assets/Resources/Audio/"+clip+".mp3");
                importer.forceToMono=true;var settings=importer.defaultSampleSettings;settings.preloadAudioData=true;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            }
            const string matPath="Assets/Art/Materials/M_GluePuddle.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(material,matPath);}
            material.SetTexture("_BaseMap",floor);material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.5f);material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull",0);material.renderQueue=(int)RenderQueue.AlphaTest;EditorUtility.SetDirty(material);
            var puddle=new GameObject("Deployed glue puddle");
            try
            {
                var trap=puddle.AddComponent<GluePuddle>();trap.stuckSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/GlueStuck.mp3");
                var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="Sketched glue on floor";quad.transform.SetParent(puddle.transform,false);
                quad.transform.localRotation=Quaternion.Euler(90,0,0);quad.transform.localScale=new Vector3(1.4f,1.2f,1);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                var renderer=quad.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(puddle,PuddlePath);
            }
            finally{Object.DestroyImmediate(puddle);}
            var deployer=run.period.Player.GetComponent<GlueDeployer>();
            if(deployer==null)deployer=run.period.Player.gameObject.AddComponent<GlueDeployer>();
            deployer.puddlePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PuddlePath);
            deployer.dropSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/GlueDrop.mp3");
            deployer.icon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/Glue/T_Glue_Bottle_v1.png");
            EditorUtility.SetDirty(deployer);PrefabUtility.RecordPrefabInstancePropertyModifications(deployer);
            var existing=run.GetComponentInChildren<GluePickup>(true);
            GameObject station=existing!=null?existing.gameObject:new GameObject("Optional glue bottle - Art Room");
            if(existing==null)station.transform.SetParent(run.transform,false);
            // Right end of the Art Room teacher's desk, beside the optional duck, off the five-item route.
            station.transform.position=new Vector3(-4.54f,.92f,29.5f);
            var visual=station.transform.Find("Glue bottle display");
            if(visual==null){visual=new GameObject("Glue bottle display").transform;visual.SetParent(station.transform,false);}
            if(!PickupModelSetup.Place(visual,"Glue",.4f,.015f,270))throw new InvalidOperationException("Glue FBX or its embedded colour map is missing.");
            var pickup=existing!=null?existing:station.AddComponent<GluePickup>();pickup.visual=visual.gameObject;
            var collider=station.GetComponent<BoxCollider>();if(collider==null)collider=station.AddComponent<BoxCollider>();
            collider.center=new Vector3(0,.24f,0);collider.size=new Vector3(.38f,.55f,.38f);
            EditorUtility.SetDirty(station);EditorUtility.SetDirty(pickup);
            EditorSceneManager.MarkSceneDirty(run.gameObject.scene);
        }
        static Texture2D ImportTexture(string path,bool sprite)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=sprite?TextureImporterType.Sprite:TextureImporterType.Default;
            if(sprite)importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
            importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}

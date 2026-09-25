using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace Confiscated.EditorTools
{
    public static class DarkModeSetup
    {
        const string Folder="Assets/Art/DarkMode/";
        [MenuItem("Confiscated/Dark Mode/Install")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
            ApplyToScene();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
        public static void ApplyToScene()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var gm=Object.FindFirstObjectByType<GameManager>();if(gm==null)throw new InvalidOperationException("Open the school scene first.");
            var dark=gm.GetComponent<DarkModeController>();if(dark==null)dark=gm.gameObject.AddComponent<DarkModeController>();
            dark.windows=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Garden view window ")).OrderBy(t=>t.name).ToArray();
            dark.torchPrefab=BuildTorch();
            const string path="Assets/Data/Inventory/I_Torch.asset";
            var item=AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);
            if(item==null){item=ScriptableObject.CreateInstance<InventoryItemDefinition>();AssetDatabase.CreateAsset(item,path);}
            item.displayName="Torch";item.kind=InventoryItemKind.Torch;item.description="Move from your locker to your satchel, then press T to switch on / off. Keep it in your satchel while using other items. No batteries needed.";
            item.icon=BuildIcon();EditorUtility.SetDirty(item);dark.torchItem=item;EditorUtility.SetDirty(dark);
            if(PrefabUtility.IsPartOfPrefabInstance(dark))PrefabUtility.RecordPrefabInstancePropertyModifications(dark);
            Debug.Log("[Dark Mode] Installed unlockable mode, locker torch and "+dark.windows.Length+" existing window light sources.");
        }
        static Material Material(string name,Color tint)
        {
            string path=Folder+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null)
            {
                var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_Painted_Trim_Pencil.mat");
                m=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if(source!=null)m.SetTexture("_BaseMap",source.mainTexture);
                AssetDatabase.CreateAsset(m,path);
            }
            m.SetColor("_BaseColor",tint);m.SetColor("_EmissionColor",tint*.035f);m.EnableKeyword("_EMISSION");
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.15f);EditorUtility.SetDirty(m);return m;
        }
        static GameObject BuildTorch()
        {
            var body=Material("Torch mustard",new Color(.78f,.49f,.11f));var ink=Material("Torch navy",new Color(.045f,.065f,.09f));var lens=Material("Torch lens",new Color(.8f,.9f,.93f));
            var root=new GameObject("School torch");
            try
            {
                Part("Mustard barrel",0,.17f,.070f,body);Part("Back cap",-.095f,.024f,.077f,ink);
                for(int i=0;i<5;i++)Part("Pencil grip band",-.055f+i*.024f,.007f,.074f,ink);
                Part("Lamp head",.10f,.065f,.107f,body);Part("Navy rim",.14f,.018f,.115f,ink);Part("Glass lens",.151f,.007f,.092f,lens);
                return PrefabUtility.SaveAsPrefabAsset(root,Folder+"P_Torch.prefab");
            }
            finally{Object.DestroyImmediate(root);}
            void Part(string name,float z,float length,float width,Material mat)
            {
                var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(root.transform,false);
                g.transform.localPosition=new Vector3(0,0,z);g.transform.localRotation=Quaternion.Euler(90,0,0);g.transform.localScale=new Vector3(width,length*.5f,width);
                Object.DestroyImmediate(g.GetComponent<Collider>());var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
        static Sprite BuildIcon()
        {
            const string path=Folder+"Torch.png";var texture=new Texture2D(128,128,TextureFormat.RGBA32,false);
            var pixels=new Color[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float u=(x-64)*.82f+(y-64)*.57f,v=-(x-64)*.57f+(y-64)*.82f;
                bool barrel=u>-38&&u<23&&Mathf.Abs(v)<14,head=u>=23&&u<42&&Mathf.Abs(v)<22;
                if(!barrel&&!head)continue;
                bool edge=barrel?(u<-34||Mathf.Abs(v)>11||((int)(u+38)%13<3)):(u>37||Mathf.Abs(v)>19);
                float pencil=.92f+.08f*Mathf.Sin(x*2.2f+y*3.1f);
                pixels[y*128+x]=(edge?new Color(.07f,.10f,.15f):new Color(.90f,.65f,.23f))*pencil;pixels[y*128+x].a=1;
            }
            texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace Confiscated.EditorTools
{
    public static class PlayerLockerSetup
    {
        public const string PrefabPath="Assets/Prefabs/Props/P_PlayerLocker.prefab";
        const string Art="Assets/Art/UI/", Data="Assets/Data/Inventory/";
        static Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+n+".mat");
        [MenuItem("Confiscated/Locker/Build Player Locker and Inventory")]
        public static void Build()
        {
            if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().path!=SchoolLayoutBuilder.ScenePath)throw new InvalidOperationException("Open SchoolLayout in Edit mode first.");
            BuildAssets();ApplyToScene();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Locker] One mustard player locker near Year 6; illustrated satchel/storage UI and item definitions saved.");
        }
        static void BuildAssets()
        {
            Directory.CreateDirectory(Data);AssetDatabase.Refresh();
            var bg=Texture(Art+"T_Satchel_Locker.png");SpriteAsset("S_Satchel_Locker",bg,new Rect(0,0,bg.width,bg.height));
            var atlas=Texture(Art+"T_Inventory_Icons.png");float cell=atlas.width/3f;
            var paper=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/T_Painted_Trim_Pencil.png");SpriteAsset("S_Inventory_Paper",paper,new Rect(0,0,paper.width,paper.height));
            string[] names={"Phone","Football","Office key"};var kinds=new[]{InventoryItemKind.Phone,InventoryItemKind.Football,InventoryItemKind.OfficeKey};
            string[] descriptions={"Your recovered phone. Quiet and safe when stored in your locker.","Press 1 to hold or put away. Left click to throw a noisy distraction.","Carry this to the caretaker's office door and press F to unlock it."};
            for(int i=0;i<3;i++)
            {
                var sprite=SpriteAsset("S_Item_"+kinds[i],atlas,new Rect(cell*i,0,cell,atlas.height));
                string path=Data+"I_"+kinds[i]+".asset";var definition=AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);
                if(definition==null){definition=ScriptableObject.CreateInstance<InventoryItemDefinition>();AssetDatabase.CreateAsset(definition,path);}
                definition.name="I_"+kinds[i];definition.displayName=names[i];definition.kind=kinds[i];definition.icon=sprite;definition.description=descriptions[i];EditorUtility.SetDirty(definition);
            }
            var material=Mat("M_Player_Locker");
            if(material==null){material=new Material(Mat("M_Painted_Trim_Pencil"));material.name="M_Player_Locker";AssetDatabase.CreateAsset(material,"Assets/Art/Materials/M_Player_Locker.mat");}
            material.SetColor("_BaseColor",new Color(.87f,.63f,.22f));EditorUtility.SetDirty(material);
            var root=new GameObject("P_PlayerLocker");root.AddComponent<PlayerLocker>();
            try
            {
                Box("Locker",root.transform,new Vector3(0,.92f,0),new Vector3(.56f,1.84f,.36f),material,true);
                Box("Handle",root.transform,new Vector3(.17f,.96f,-.196f),new Vector3(.025f,.12f,.027f),Mat("M_Chapter_Ink"),false);
                Box("Nameplate",root.transform,new Vector3(0,1.45f,-.194f),new Vector3(.41f,.25f,.024f),Mat("M_Chapter_Paper"),false);
                var textObject=new GameObject("Your locker lettering");textObject.transform.SetParent(root.transform,false);textObject.transform.localPosition=new Vector3(0,1.45f,-.21f);
                var text=textObject.AddComponent<TextMesh>();text.font=SchoolTypography.Font;text.fontSize=64;text.characterSize=.007f;
                text.text="YOUR\nLOCKER";text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontStyle=FontStyle.Bold;text.color=new Color(.1f,.16f,.23f);textObject.AddComponent<WorldLabel>();
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{Object.DestroyImmediate(root);}
        }
        public static void ApplyToScene()
        {
            var player=Object.FindFirstObjectByType<PlayerInteractor>();
            var inventory=player.GetComponent<PlayerInventory>();if(inventory==null)inventory=player.gameObject.AddComponent<PlayerInventory>();
            inventory.phoneItem=Definition(InventoryItemKind.Phone);inventory.footballItem=Definition(InventoryItemKind.Football);inventory.keyItem=Definition(InventoryItemKind.OfficeKey);
            EditorUtility.SetDirty(inventory);if(PrefabUtility.IsPartOfPrefabInstance(inventory))PrefabUtility.RecordPrefabInstancePropertyModifications(inventory);
            var old=GameObject.Find("PlayerLockerSystems");if(old!=null)Object.DestroyImmediate(old);
            var systems=new GameObject("PlayerLockerSystems");var ui=systems.AddComponent<LockerStorageUI>();
            ui.backgroundArt=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"S_Satchel_Locker.asset");ui.slotPaper=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"S_Inventory_Paper.asset");
            var locker=Object.FindFirstObjectByType<PlayerLocker>();
            if(locker==null)
            {
                var banks=GameObject.Find("School/Details").transform.Cast<Transform>().Where(t=>t.name=="Locker bank");
                var bank=banks.OrderBy(t=>Vector3.Distance(t.position,SchoolPlan.Point(296,950))).First();
                var body=bank.Cast<Transform>().Where(t=>t.name=="Locker").OrderBy(t=>Mathf.Abs(t.localPosition.x)).First();
                var position=body.position-Vector3.up*.92f;float x=body.localPosition.x;
                var handle=bank.Cast<Transform>().FirstOrDefault(t=>t.name=="Handle"&&Mathf.Abs(t.localPosition.x-x-.17f)<.01f);
                if(handle!=null)Object.DestroyImmediate(handle.gameObject);Object.DestroyImmediate(body.gameObject);
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),bank);go.name="Player locker";
                go.transform.SetPositionAndRotation(position,bank.rotation);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);locker=go.GetComponent<PlayerLocker>();
            }
            locker.storageUI=ui;EditorUtility.SetDirty(locker);PrefabUtility.RecordPrefabInstancePropertyModifications(locker);
            var gm=Object.FindFirstObjectByType<GameManager>();gm.lockerUI=ui;EditorUtility.SetDirty(gm);if(PrefabUtility.IsPartOfPrefabInstance(gm))PrefabUtility.RecordPrefabInstancePropertyModifications(gm);
            EditorUtility.SetDirty(ui);
        }
        static InventoryItemDefinition Definition(InventoryItemKind kind)=>AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(Data+"I_"+kind+".asset");
        static Texture2D Texture(string path)
        {
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=4096;
            importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Sprite SpriteAsset(string name,Texture2D texture,Rect rect)
        {
            var sprite=Sprite.Create(texture,rect,new Vector2(.5f,.5f),100);sprite.name=name;string path=Art+name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(existing==null){AssetDatabase.CreateAsset(sprite,path);return sprite;}
            EditorUtility.CopySerialized(sprite,existing);Object.DestroyImmediate(sprite);EditorUtility.SetDirty(existing);return existing;
        }
        static void Box(string name,Transform parent,Vector3 p,Vector3 size,Material material,bool collide)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;
            if(!collide)Object.DestroyImmediate(g.GetComponent<Collider>());IllustratedArtSetup.Tiled(g.GetComponent<MeshRenderer>(),material,"PlayerLocker",1);
        }
    }
}

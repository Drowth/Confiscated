using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;
namespace Confiscated.EditorTools
{
    public static class SchoolPeriodSetup
    {
        public const string TeacherPath="Assets/Prefabs/Characters/P_Mr_Reed.prefab";
        static Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+n+".mat");
        [MenuItem("Confiscated/First Period/Build Classroom Period")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play first.");
            Teacher();ApplyToScene();DiningHallSetup.Rebake();EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
        static void Teacher()
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Art/Textures/T_Mr_Reed.png");
            importer.alphaIsTransparency=true;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            var material=Mat("M_Mr_Reed");if(material==null){material=new Material(Shader.Find("Confiscated/Character Cutout"));AssetDatabase.CreateAsset(material,"Assets/Art/Materials/M_Mr_Reed.mat");}
            material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/T_Mr_Reed.png");material.SetFloat("_MagentaKey",0);EditorUtility.SetDirty(material);
            var go=new GameObject("P_Mr_Reed");
            try
            {
                var agent=go.AddComponent<NavMeshAgent>();agent.radius=.23f;agent.height=1.9f;agent.speed=2.3f;agent.stoppingDistance=.25f;agent.angularSpeed=240;
                go.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
                var capsule=go.AddComponent<CapsuleCollider>();capsule.radius=.25f;capsule.height=1.95f;capsule.center=Vector3.up*.975f;
                go.AddComponent<PeriodInteractable>().role=PeriodInteractable.Role.Teacher;
                var visual=GameObject.CreatePrimitive(PrimitiveType.Quad);visual.name="Cutout";visual.transform.SetParent(go.transform,false);Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.transform.localPosition=Vector3.up*1.02f;visual.transform.localScale=new Vector3(1.36f,2.04f,1);visual.GetComponent<Renderer>().sharedMaterial=material;
                CutoutMotionSetup.Configure(go);PrefabUtility.SaveAsPrefabAsset(go,TeacherPath);
            }
            finally{Object.DestroyImmediate(go);}
        }
        public static void ApplyToScene()
        {
            var old=GameObject.Find("FirstPeriod");if(old!=null)Undo.DestroyObjectImmediate(old);
            var root=new GameObject("FirstPeriod");Undo.RegisterCreatedObjectUndo(root,"Build first classroom period");
            var period=root.AddComponent<SchoolPeriodController>();var gm=Object.FindFirstObjectByType<GameManager>();gm.schoolPeriod=period;EditorUtility.SetDirty(gm);
            period.Player=Object.FindFirstObjectByType<PlayerInteractor>();period.seat=Object.FindFirstObjectByType<ClassroomSeat>();period.caretaker=Object.FindFirstObjectByType<CaretakerAI>();period.phonePickup=Object.FindFirstObjectByType<PhonePickup>();
            period.teacherHome=Point("Mr Reed at board",new Vector3(-30.2f,0,27.2f));period.teacherAtDesk=Point("Mr Reed beside desk",new Vector3(-22.55f,0,29));
            period.teacherHandoff=Point("Teacher collection point",new Vector3(-19.15f,0,30.9f));period.caretakerHandoff=Point("Caretaker collection point",new Vector3(-17.8f,0,31));
            period.officeDrop=Point("Office property drop",new Vector3(-41.5f,0,82.5f));period.standPoint=Point("Stand beside pupil desk",new Vector3(-19.6f,.05f,29));
            var teacher=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TeacherPath),root.transform);
            teacher.name="Mr Reed";teacher.transform.position=period.teacherHome.position;period.teacher=teacher.GetComponent<NavMeshAgent>();teacher.GetComponent<PeriodInteractable>().period=period;
            PrefabUtility.RecordPrefabInstancePropertyModifications(teacher.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(teacher.GetComponent<PeriodInteractable>());
            period.worksheetUI=root.AddComponent<PeriodWorksheetUI>();period.worksheetUI.period=period;period.worksheetUI.paper=gm.lockerUI.slotPaper;
            period.passItem=Item("HallPass","Hall pass",InventoryItemKind.HallPass,"Mr Reed's signed pass. Press F on the caretaker when he stops you.");
            period.papersItem=Item("Newsletters","Newsletters",InventoryItemKind.Newsletters,"Follow the OFFICE signs to the yellow NEWSLETTERS tray. Press F to deliver.");
            var sheet=Box("Newsletter worksheet",new Vector3(-21.35f,.795f,29),new Vector3(.38f,.025f,.27f),"M_Chapter_Paper");
            var work=sheet.AddComponent<PeriodInteractable>();work.period=period;work.role=PeriodInteractable.Role.Worksheet;
            var prop=new GameObject("Smith's phone");prop.transform.SetParent(root.transform,false);prop.transform.position=new Vector3(-21.35f,.821f,29.36f);period.phoneProp=prop;
            Part("Phone case",Vector3.zero,new Vector3(.085f,.014f,.155f),"M_Chapter_Ink");Part("Phone screen",new Vector3(0,.008f,0),new Vector3(.072f,.003f,.13f),"M_Chapter_Grey");
            var table=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Dining/P_Dining_Table.prefab"),root.transform);
            table.name="Office deliveries stand";table.transform.position=new Vector3(-35.12f,0,79.3f);table.transform.localScale=new Vector3(.34f,1,.74f);PrefabUtility.RecordPrefabInstancePropertyModifications(table.transform);
            var tray=Box("Office delivery tray",new Vector3(-35.10f,.84f,79.3f),new Vector3(.48f,.08f,.40f),"M_Chapter_Green");
            period.deliveryTray=tray.AddComponent<PeriodInteractable>();period.deliveryTray.role=PeriodInteractable.Role.Delivery;period.deliveryTray.period=period;
            var papers=Box("Delivered papers",new Vector3(-35.10f,.90f,79.3f),new Vector3(.33f,.04f,.26f),"M_Chapter_Paper");papers.transform.SetParent(tray.transform,true);papers.SetActive(false);
            var plaque=Box("Office delivery sign",new Vector3(-35.54f,1.42f,79.3f),new Vector3(.035f,.48f,1.18f),"M_Painted_Trim_Pencil");
            Text("OFFICE DELIVERIES\nMr Reed - Year 6",new Vector3(-35.515f,1.43f,79.3f),270,.018f);
            var board=GameObject.Find("School/Details/Year 6 furnishings/TeachingBoard");
            foreach(var text in board.GetComponentsInChildren<TextMesh>()){text.text="ENGLISH\nLook at the picture.\nWhat can you see?";EditorUtility.SetDirty(text);}
            // A route with distinct, named pauses: first safe pass check, office, corridor inspection, dining hall.
            var ai=period.caretaker;var agent=ai.GetComponent<NavMeshAgent>();ai.transform.position=new Vector3(-17.8f,0,34.2f);EditorUtility.SetDirty(ai.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(ai.transform);
            ai.patrol.Clear();Route("Year 6 pass checkpoint",new Vector3(-17.8f,0,34.2f),28,Vector3.back);
            Route("Put confiscated property away",period.officeDrop.position,8,Vector3.left);
            Route("Inspect west corridor",new Vector3(-33.8f,0,76),6,Vector3.back);
            Route("Check dining hall",new Vector3(-30.1f,0,59.8f),10,Vector3.right);
            Route("Return to office",period.officeDrop.position,8,Vector3.left);
            EditorUtility.SetDirty(ai);PrefabUtility.RecordPrefabInstancePropertyModifications(ai);
            var check=ai.GetComponent<CaretakerPassCheck>();if(check==null)check=ai.gameObject.AddComponent<CaretakerPassCheck>();check.period=period;EditorUtility.SetDirty(check);
            var interactor=new SerializedObject(period.Player);interactor.FindProperty("hitMask").intValue|=1<<ai.gameObject.layer;interactor.ApplyModifiedProperties();PrefabUtility.RecordPrefabInstancePropertyModifications(period.Player);
            var ringImporter=(AudioImporter)AssetImporter.GetAtPath("Assets/Audio/SFX/PhoneRingtone.mp3");ringImporter.forceToMono=true;var settings=ringImporter.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;ringImporter.defaultSampleSettings=settings;ringImporter.SaveAndReimport();
            var ringer=Object.FindFirstObjectByType<PhoneRinger>();var so=new SerializedObject(ringer);so.FindProperty("ringClip").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/PhoneRingtone.mp3");so.ApplyModifiedProperties();PrefabUtility.RecordPrefabInstancePropertyModifications(ringer);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Transform Point(string name,Vector3 p){var g=new GameObject(name);g.transform.SetParent(root.transform,false);g.transform.position=p;return g.transform;}
            GameObject Box(string name,Vector3 p,Vector3 size,string material){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root.transform,false);g.transform.position=p;g.transform.localScale=size;IllustratedArtSetup.Tiled(g.GetComponent<MeshRenderer>(),Mat(material),"trim",1);return g;}
            void Part(string name,Vector3 p,Vector3 size,string material){var g=Box(name,prop.transform.position+p,size,material);Object.DestroyImmediate(g.GetComponent<Collider>());g.transform.SetParent(prop.transform,true);}
            InventoryItemDefinition Item(string id,string name,InventoryItemKind kind,string description){Directory.CreateDirectory("Assets/Inventory");string path="Assets/Inventory/"+id+".asset";var item=AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);if(item==null){item=ScriptableObject.CreateInstance<InventoryItemDefinition>();AssetDatabase.CreateAsset(item,path);}item.displayName=name;item.kind=kind;item.description=description;item.icon=AssetDatabase.LoadAssetAtPath<Sprite>(PaperInventoryArtSetup.SpritePath(id))??gm.lockerUI.slotPaper;EditorUtility.SetDirty(item);return item;}
            void Route(string name,Vector3 p,float seconds,Vector3 facing){ai.patrol.Add(new CaretakerAI.PatrolPoint{point=Point(name,p),dwellSeconds=seconds,faceDirection=facing});}
            void Text(string value,Vector3 p,float yaw,float size){var g=new GameObject("Delivery lettering");g.transform.SetParent(root.transform,false);g.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));var t=g.AddComponent<TextMesh>();t.font=SchoolTypography.Font;t.text=value;t.fontSize=64;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=Color.black;g.AddComponent<WorldLabel>();}
        }
    }
}




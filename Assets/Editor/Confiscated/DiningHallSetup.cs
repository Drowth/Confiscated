using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    public static class DiningHallSetup
    {
        const string Models="Assets/Art/Models/Dining/",Prefabs="Assets/Prefabs/Dining/";
        public const string TablePath=Prefabs+"P_Dining_Table.prefab";
        static HallwayPropLibrary.Manifest Manifest=>JsonUtility.FromJson<HallwayPropLibrary.Manifest>(File.ReadAllText(Models+"DiningManifest.json"));
        static Vector3 V(float[] a)=>new(a[0],a[1],a[2]);
        [MenuItem("Confiscated/Dining Hall/Import Blender Assets")]
        public static void Import()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play first.");
            Directory.CreateDirectory(Prefabs);AssetDatabase.Refresh();
            Surface("M_Dining_Ceramic","T_Ceramic_Pencil",Color.white);
            Surface("M_Dining_Water","T_DrinkingWater_Pencil",Color.white);
            Surface("M_Dining_Glass","T_Ceramic_Pencil",new Color(.78f,.89f,.92f));
            foreach(var prop in Manifest.props)
            {
                string path=Models+prop.name+".fbx";
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.bakeAxisConversion=true;importer.globalScale=1;importer.useFileScale=true;
                importer.generateSecondaryUV=false;importer.importCameras=false;importer.importLights=false;
                importer.importAnimation=false;importer.isReadable=true;importer.meshCompression=ModelImporterMeshCompression.Off;
                importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach(var mat in model.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct())
                {
                    var shared=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+mat.name+".mat");
                    if(shared==null)throw new Exception("Missing material "+mat.name);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),mat.name),shared);
                }
                importer.SaveAndReimport();
                var root=new GameObject(prop.name.Replace("SM_","P_"));
                try
                {
                    var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.transform);
                    visual.name="Blender model";visual.transform.localRotation=Quaternion.Euler(0,180,0)*visual.transform.localRotation;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                    foreach(var c in prop.colliders){var box=root.AddComponent<BoxCollider>();box.center=V(c.center);box.size=V(c.size);}
                    foreach(var l in prop.labels)Text(root.transform,l.text,V(l.position),l.size,false);
                    PrefabUtility.SaveAsPrefabAsset(root,Prefabs+root.name+".prefab");
                }
                finally{Object.DestroyImmediate(root);}
            }
            // Complete six-seat unit for future room dressing; table and benches remain nested prefabs.
            var set=new GameObject("P_Dining_TableSet");
            try
            {
                Put("Table",set.transform,Vector3.zero);
                Put("Bench",set.transform,new Vector3(0,0,-.79f));Put("Bench",set.transform,new Vector3(0,0,.79f));
                PrefabUtility.SaveAsPrefabAsset(set,Prefabs+"P_Dining_TableSet.prefab");
            }
            finally{Object.DestroyImmediate(set);}
            AssetDatabase.SaveAssets();
        }
        static void Surface(string name,string texture,Color tint)
        {
            string path="Assets/Art/Materials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_Painted_Trim_Pencil.mat"));mat.name=name;AssetDatabase.CreateAsset(mat,path);}
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/"+texture+".png"));
            mat.SetColor("_BaseColor",tint);mat.SetFloat("_EdgeWidth",.002f);EditorUtility.SetDirty(mat);
        }
        static Transform Text(Transform parent,string value,Vector3 p,float size,bool chalk)
        {
            var go=new GameObject("Lettering - "+value.Replace('\n',' '));go.transform.SetParent(parent,false);go.transform.localPosition=p;
            var t=go.AddComponent<TextMesh>();t.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/FrederickatheGreat-Regular.ttf");
            t.fontSize=96;t.characterSize=size*.14f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;
            t.text=value;t.color=chalk?new Color(.9f,.85f,.67f):new Color(.13f,.19f,.28f);go.AddComponent<WorldLabel>();return go.transform;
        }
        static GameObject Put(string name,Transform parent,Vector3 p,float yaw=0,bool hallway=false)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(hallway?"Assets/Prefabs/Hallway/P_Hall_"+name+".prefab":Prefabs+"P_Dining_"+name+".prefab");
            if(prefab==null)throw new Exception("Missing furniture prefab "+name);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);return go;
        }
        [MenuItem("Confiscated/Dining Hall/Furnish Dining Hall")]
        public static void FurnishAndSave(){ApplyToScene();Rebake();EditorSceneManager.SaveOpenScenes();}
        public static void ApplyToScene()
        {
            if(EditorApplication.isPlaying||SceneManager.GetActiveScene().path!=SchoolLayoutBuilder.ScenePath)
                throw new InvalidOperationException("Open SchoolLayout in Edit mode first.");
            var old=GameObject.Find("DiningHallFurniture");if(old!=null)Undo.DestroyObjectImmediate(old);
            var root=new GameObject("DiningHallFurniture");Undo.RegisterCreatedObjectUndo(root,"Furnish dining hall");
            var seating=new GameObject("Seating - 144 places").transform;seating.SetParent(root.transform,false);
            float[] columns={-27.8f,-23.2f,-15.8f,-11.2f};
            for(int row=0;row<6;row++)for(int col=0;col<4;col++)
            {
                float z=50.5f+row*3.7f;
                var set=Put("TableSet",seating,new Vector3(columns[col],0,z));set.name="Table "+(row*4+col+1).ToString("00")+" - six seats";
                if((row+col)%3==0)
                {
                    Put("MealTray",root.transform,new Vector3(columns[col]-.62f,.789f,z-.15f),180);
                    if(row%2==0)Put("MealTray",root.transform,new Vector3(columns[col]+.60f,.789f,z+.15f));
                }
            }
            var service=new GameObject("Serving and clearing").transform;service.SetParent(root.transform,false);
            foreach(float x in new[]{-25.1f,-22.6f,-20.1f})Put("ServingCounter",service,new Vector3(x,0,44.1f),180);
            Put("Table",service,new Vector3(-28.65f,0,44.1f),90);
            for(int i=0;i<3;i++)Put("TrayStack",service,new Vector3(-28.65f,.789f,43.4f+i*.66f),90);
            Put("DrinksStation",service,new Vector3(-13.2f,0,44.1f),180);
            Put("TrayReturn",service,new Vector3(-13.2f,0,73.7f));
            Put("TrayReturn",service,new Vector3(-14.4f,0,73.7f));
            Put("LitterBin",service,new Vector3(-11.65f,0,73.7f),0,true);
            Put("RecyclingBin",service,new Vector3(-10.7f,0,73.7f),0,true);
            var menu=Put("MenuBoard",service,new Vector3(-24.4f,2.32f,42.12f),180);
            Text(menu.transform,"TODAY'S LUNCH",new Vector3(0,.33f,-.071f),.104f,true);
            Text(menu.transform,"Jacket potatoes\nBeans & cheese\nFruit crumble",new Vector3(0,-.05f,-.071f),.093f,true);
            var manners=Put("MenuBoard",service,new Vector3(-20.8f,2.32f,42.12f),180);
            Text(manners.transform,"PLEASE REMEMBER",new Vector3(0,.33f,-.071f),.077f,true);
            Text(manners.transform,"Queue patiently\nClear your table\nReturn your tray",new Vector3(0,-.06f,-.071f),.09f,true);
            Put("Clock",service,new Vector3(-28.5f,2.35f,75.3f),0,true);
            Put("Radiator",service,new Vector3(-31.91f,0,47.6f),-90,true);
            Put("Radiator",service,new Vector3(-7.09f,0,71.8f),90,true);
            var lighting=new GameObject("Dining ceiling fixtures").transform;lighting.SetParent(root.transform,false);
            foreach(float x in new[]{-27f,-19.5f,-12f})foreach(float z in new[]{50f,57f,64f,71f})
            {
                var fixture=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Modular/P_CeilingLight.prefab"),lighting);
                fixture.transform.localPosition=new Vector3(x,0,z);PrefabUtility.RecordPrefabInstancePropertyModifications(fixture.transform);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        public static void Rebake()
        {
            var surface=Object.FindFirstObjectByType<NavMeshSurface>();var saved=surface.navMeshData;
            surface.RemoveData();surface.navMeshData=null;surface.BuildNavMesh();var fresh=surface.navMeshData;
            fresh.name=Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(saved));
            surface.RemoveData();EditorUtility.CopySerialized(fresh,saved);Object.DestroyImmediate(fresh);
            surface.navMeshData=saved;surface.AddData();EditorUtility.SetDirty(surface);EditorUtility.SetDirty(saved);AssetDatabase.SaveAssets();
        }
        [MenuItem("Confiscated/Dining Hall/Capture Furnished Hall")]
        public static void Capture()
        {
            var go=new GameObject("Dining review camera");var camera=go.AddComponent<Camera>();camera.fieldOfView=72;
            try
            {
                camera.transform.position=new Vector3(-29.9f,1.65f,72.5f);camera.transform.LookAt(new Vector3(-18.9f,.9f,55));
                HallwayPropLibrary.Capture(camera,1700,1050,"D:/Confiscated/Docs/DiningHall_Overview.png");
                camera.transform.position=new Vector3(-25.5f,1.5f,54);camera.transform.LookAt(new Vector3(-27.7f,.56f,50.5f));camera.fieldOfView=57;
                HallwayPropLibrary.Capture(camera,1400,1000,"D:/Confiscated/Docs/DiningHall_Table.png");
                camera.transform.position=new Vector3(-26.8f,1.65f,48.3f);camera.transform.LookAt(new Vector3(-22.2f,1.4f,43.8f));camera.fieldOfView=70;
                HallwayPropLibrary.Capture(camera,1600,1000,"D:/Confiscated/Docs/DiningHall_Serving.png");
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    public static class DetentionRoomSetup
    {
        public const string NpcPath="Assets/Prefabs/Characters/P_Miss_D_Tenison.prefab";
        static Vector3 Origin=>SchoolPlan.Point(748.5f,518);
        static Transform root;
        static Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+n+".mat");
        [MenuItem("Confiscated/Detention/Build Miss Tenison and Detention Room")]
        public static void Build()
        {
            if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().path!=SchoolLayoutBuilder.ScenePath)
                throw new InvalidOperationException("Open SchoolLayout in Edit mode.");
            CreateTeacher();ApplyToScene();Rebake();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            Debug.Log("[Detention] Miss D Tenison and five-desk classroom saved; caretaker catches now start detention.");
        }
        static void CreateTeacher()
        {
            const string texturePath="Assets/Art/Textures/T_Miss_D_Tenison.png";
            AssetDatabase.ImportAsset(texturePath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.wrapMode=TextureWrapMode.Clamp;
            importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=true;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
            var material=Mat("M_Miss_D_Tenison");
            if(material==null){material=new Material(Shader.Find("Confiscated/Character Cutout"));AssetDatabase.CreateAsset(material,"Assets/Art/Materials/M_Miss_D_Tenison.mat");}
            material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_MagentaKey",1);EditorUtility.SetDirty(material);
            var npc=new GameObject("P_Miss_D_Tenison");
            try
            {
                npc.AddComponent<MissTenison>();var capsule=npc.AddComponent<CapsuleCollider>();capsule.radius=.24f;capsule.height=1.98f;capsule.center=Vector3.up*.99f;
                var cutout=GameObject.CreatePrimitive(PrimitiveType.Quad);cutout.name="Cutout";cutout.transform.SetParent(npc.transform,false);
                Object.DestroyImmediate(cutout.GetComponent<Collider>());cutout.transform.localPosition=Vector3.up*1.04f;cutout.transform.localScale=new Vector3(1.39f,2.08f,1);
                cutout.GetComponent<MeshRenderer>().sharedMaterial=material;
                CutoutMotionSetup.Configure(npc);
                Directory.CreateDirectory("Assets/Prefabs/Characters");PrefabUtility.SaveAsPrefabAsset(npc,NpcPath);
            }
            finally {Object.DestroyImmediate(npc);}
        }
        public static void ApplyToScene()
        {
            var old=GameObject.Find("DetentionRoom");if(old!=null)Object.DestroyImmediate(old);
            root=new GameObject("DetentionRoom").transform;root.position=Origin;
            var detention=root.gameObject.AddComponent<DetentionController>();
            detention.roomBounds=new Bounds(Origin+Vector3.up*1.5f,new Vector3(6.78f,3.1f,7.56f));
            // Existing north and west walls are reused. These two partitions leave the rest of East room A accessible.
            Box("East partition",new Vector3(3.389f,1.5f,0),new Vector3(.12f,3,7.56f),Mat("M_Wall_Corridor"),true);
            Box("South partition",new Vector3(0,1.5f,-3.778f),new Vector3(6.9f,3,.12f),Mat("M_Wall_Corridor"),true);
            var ai=Object.FindFirstObjectByType<CaretakerAI>();
            var door=Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None).First(d=>Vector3.Distance(d.transform.position,SchoolPlan.Point(718,530))<.2f);
            door.detention=detention;door.startsUnlocked=true;door.mission=null;
            EditorUtility.SetDirty(door);PrefabUtility.RecordPrefabInstancePropertyModifications(door);
            detention.door=door;detention.phonePickup=Object.FindFirstObjectByType<PhonePickup>();
            var existingSign=GameObject.Find("School/Details/East room A west sign");
            if(existingSign!=null){var lettering=existingSign.GetComponentInChildren<TextMesh>();lettering.text="DETENTION";EditorUtility.SetDirty(lettering);}
            var arrival=Group("Arrival",new Vector3(-1.15f,.04f,-3.05f));detention.arrivalPoint=arrival;
            var teacher=Prefab(NpcPath,"Miss D Tenison",new Vector3(.2f,0,2.55f));
            detention.teacher=teacher.GetComponent<MissTenison>();detention.teacher.detention=detention;
            EditorUtility.SetDirty(detention.teacher);PrefabUtility.RecordPrefabInstancePropertyModifications(detention.teacher);
            int number=0;
            foreach(var p in new[]{new Vector3(-1.15f,0,1),new Vector3(1.15f,0,1),new Vector3(-1.15f,0,-.7f),new Vector3(1.15f,0,-.7f),new Vector3(1.15f,0,-2.4f)})
            {
                number++;var desk=Prefab("Assets/Prefabs/Props/P_Desk.prefab","Detention desk "+number,p);
                var chair=Prefab("Assets/Prefabs/Props/P_Chair.prefab","Detention chair "+number,p+new Vector3(0,0,-.72f),180);
                var station=Group("Detention place "+number,p);
                desk.transform.SetParent(station,true);chair.transform.SetParent(station,true);
                PrefabUtility.RecordPrefabInstancePropertyModifications(desk.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(chair.transform);
                var seat=station.gameObject.AddComponent<DetentionSeat>();seat.detention=detention;
                seat.sittingPoint=Group("Seat "+number,p+new Vector3(0,.04f,-.72f));
                EditorUtility.SetDirty(seat);
                var book=Box("Exercise book "+number,p+new Vector3(0,.766f,-.02f),new Vector3(.29f,.014f,.22f),Mat("M_Chapter_Paper"));book.transform.SetParent(station,true);
            }
            Box("Noticeboard frame",new Vector3(0,1.8f,3.655f),new Vector3(3.05f,1.04f,.09f),Mat("M_Wood_Desk"));
            Box("Noticeboard inset",new Vector3(0,1.8f,3.599f),new Vector3(2.87f,.88f,.024f),Mat("M_Chapter_Green"));
            Text("DETENTION",new Vector3(0,2.02f,3.579f),.045f);
            Text("Miss D Tenison\nTake a seat. Stay quiet.\nWait to be dismissed.",new Vector3(0,1.65f,3.579f),.021f);
            Prefab("Assets/Prefabs/Hallway/P_Hall_Clock.prefab","Detention clock",new Vector3(2.43f,2.35f,3.63f));
            Prefab("Assets/Prefabs/Hallway/P_Hall_Radiator.prefab","Detention radiator",new Vector3(-2.15f,0,3.57f));
            Prefab("Assets/Prefabs/Hallway/P_Hall_LitterBin.prefab","Detention bin",new Vector3(2.9f,0,3.2f));
            // Shallow register shelf; five pupil desks remain the only desks in this room.
            Box("Register shelf",new Vector3(2.9f,1.35f,1.4f),new Vector3(.40f,.065f,1.05f),Mat("M_Wood_Desk"));
            for(int i=0;i<4;i++)Box("Register folder",new Vector3(2.9f,1.52f,1.13f+i*.14f),new Vector3(.28f,.3f,.10f),Mat(i%2==0?"M_School_Locker":"M_Chapter_Green"));
            // Corridor-side plaque faces west into the hall, separate from moving door geometry.
            var plaque=Group("Detention door plaque",new Vector3(-3.52f,1.68f,-2.60f));plaque.localRotation=Quaternion.Euler(0,90,0);
            var sign=Box("Door plaque",Vector3.zero,new Vector3(.90f,.36f,.035f),Mat("M_Painted_Trim_Pencil"));sign.transform.SetParent(plaque,false);sign.transform.localPosition=Vector3.zero;
            var title=Text("DETENTION\nMiss D Tenison",Vector3.zero,.013f);title.SetParent(plaque,false);title.localPosition=new Vector3(0,0,-.024f);
            title.GetComponent<TextMesh>().color=Color.black;
            var gm=Object.FindFirstObjectByType<GameManager>();gm.detention=detention;EditorUtility.SetDirty(gm);
            if(PrefabUtility.IsPartOfPrefabInstance(gm))PrefabUtility.RecordPrefabInstancePropertyModifications(gm);
            EditorUtility.SetDirty(detention);
            BlackboardSetup.Apply(detention);
        }
        static GameObject Prefab(string path,string name,Vector3 position,float yaw=0)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),root);go.name=name;
            go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);return go;
        }
        static Transform Group(string name,Vector3 p){var g=new GameObject(name);g.transform.SetParent(root,false);g.transform.localPosition=p;return g.transform;}
        static Transform Text(string value,Vector3 position,float size)
        {
            var t=Group("Lettering",position);var text=t.gameObject.AddComponent<TextMesh>();text.font=SchoolTypography.Font;
            text.fontSize=64;text.characterSize=size;text.text=value;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            text.color=new Color(.83f,.80f,.63f);text.fontStyle=FontStyle.Bold;t.gameObject.AddComponent<WorldLabel>();return t;
        }
        static GameObject Box(string name,Vector3 p,Vector3 size,Material mat,bool wall=false)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root,false);g.transform.localPosition=p;
            g.transform.localScale=size;g.GetComponent<MeshRenderer>().sharedMaterial=mat;
            if(!wall)IllustratedArtSetup.Tiled(g.GetComponent<MeshRenderer>(),mat,"Detention",1);
            else
            {
                var mf=g.GetComponent<MeshFilter>();var mesh=Object.Instantiate(mf.sharedMesh);var vertices=mesh.vertices;var uv=mesh.uv;var normals=mesh.normals;
                for(int i=0;i<uv.Length;i++)uv[i]=new Vector2((Mathf.Abs(normals[i].x)>.5f?vertices[i].z*size.z:vertices[i].x*size.x)/2,(vertices[i].y+.5f));
                mesh.uv=uv;mesh.name=name.Replace(" ","_");Directory.CreateDirectory("Assets/Art/Meshes/Detention");string path="Assets/Art/Meshes/Detention/"+mesh.name+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);}mf.sharedMesh=saved;
            }
            return g;
        }
        static void Rebake()
        {
            var surface=Object.FindFirstObjectByType<NavMeshSurface>();var saved=surface.navMeshData;surface.RemoveData();surface.navMeshData=null;surface.BuildNavMesh();
            var fresh=surface.navMeshData;fresh.name=saved.name;surface.RemoveData();EditorUtility.CopySerialized(fresh,saved);Object.DestroyImmediate(fresh);
            surface.navMeshData=saved;surface.AddData();EditorUtility.SetDirty(surface);EditorUtility.SetDirty(saved);
        }
    }
}


using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Imports the Blender library, creates reusable prefabs and places a restrained school sample.</summary>
    public static class HallwayPropLibrary
    {
        const string Models="Assets/Art/Models/Hallway/", Prefabs="Assets/Prefabs/Hallway/";
        const string GalleryPath="Assets/Scenes/HallwayAssetGallery.unity";
        [Serializable] public class Manifest { public Prop[] props; }
        [Serializable] public class Prop { public string name; public Collision[] colliders; public Label[] labels; public int triangles; }
        [Serializable] public class Collision { public float[] center,size; }
        [Serializable] public class Label { public string text; public float[] position; public float size; }
        static Vector3 V(float[] a)=>new(a[0],a[1],a[2]);
        static Manifest Read=>JsonUtility.FromJson<Manifest>(File.ReadAllText(Models+"HallwayManifest.json"));
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+name+".mat");

        [MenuItem("Confiscated/Hallway Props/Import Blender Library")]
        public static void Import()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play before importing props.");
            Directory.CreateDirectory(Prefabs); AssetDatabase.Refresh();
            foreach(var p in Read.props)
            {
                string path=Models+p.name+".fbx";
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.bakeAxisConversion=true;
                importer.globalScale=1; importer.useFileScale=true;
                importer.generateSecondaryUV=false; importer.importCameras=false; importer.importLights=false;
                importer.importAnimation=false; importer.isReadable=true; importer.meshCompression=ModelImporterMeshCompression.Off;
                importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach(var material in model.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct())
                {
                    var shared=Mat(material.name);
                    if(shared==null)throw new Exception("Missing shared pencil material "+material.name);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),material.name),shared);
                }
                importer.SaveAndReimport();
                model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var root=new GameObject(p.name.Replace("SM_","P_"));
                try
                {
                    var visual=(GameObject)PrefabUtility.InstantiatePrefab(model,root.transform);
                    visual.name="Blender model";
                    visual.transform.localRotation=Quaternion.Euler(0,180,0)*visual.transform.localRotation;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                    foreach(var c in p.colliders)
                    {
                        var go=new GameObject("Collision");go.transform.SetParent(root.transform,false);
                        var box=go.AddComponent<BoxCollider>();box.center=V(c.center);box.size=V(c.size);
                    }
                    foreach(var l in p.labels)
                    {
                        var lettering=Text(root.transform,l.text,V(l.position),l.size);
                        if(p.name=="SM_Hall_WetFloorSign")lettering.localRotation=Quaternion.Euler(19,0,0);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,Prefabs+root.name+".prefab");
                }
                finally { Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Hallway] Imported 9 Blender props with metric UVs and shared pencil materials.");
        }

        static Transform Text(Transform parent,string value,Vector3 position,float size)
        {
            var go=new GameObject("Lettering - "+value);go.transform.SetParent(parent,false);go.transform.localPosition=position;
            var text=go.AddComponent<TextMesh>();text.font=SchoolTypography.Font;
            text.fontSize=64;text.characterSize=size*.28f;text.text=value;text.anchor=TextAnchor.MiddleCenter;
            text.alignment=TextAlignment.Center;text.fontStyle=FontStyle.Bold;text.color=new Color(.13f,.19f,.28f);
            go.AddComponent<WorldLabel>(); return go.transform;
        }
        static GameObject Place(string name,Transform parent,Vector3 p,float yaw=0)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"P_Hall_"+name+".prefab");
            if(prefab==null)throw new Exception("Import missing prefab "+name);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            go.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);return go;
        }

        [MenuItem("Confiscated/Hallway Props/Place School Examples")]
        public static void DressSchool()
        {
            if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=SchoolLayoutBuilder.ScenePath)
                throw new InvalidOperationException("Open SchoolLayout in Edit mode first.");
            var old=GameObject.Find("HallwayProps");if(old!=null)Undo.DestroyObjectImmediate(old);
            var root=new GameObject("HallwayProps");Undo.RegisterCreatedObjectUndo(root,"Place hallway library examples");
            // Pixel coordinates reference the school plan; local -Z is the prop front.
            Put("Bench",379,257,0);Put("LitterBin",364,257,0);Put("RecyclingBin",359,257,0);
            Put("Clock",379,255,0,2.35f);Put("CoatRail",395,255,0,1.6f);
            Put("Radiator",612,256,0);Put("Extinguisher",650,255,0,.55f);
            Put("DrinkingFountain",735,257,0);
            Put("Bench",269,483,-90);Put("Clock",265.8f,483,-90,2.35f);
            Put("Radiator",294,595,90);Put("LitterBin",291.8f,605,90);
            Put("Extinguisher",294,572,90,.55f);Put("WetFloorSign",270,578,-75);
            Put("CoatRail",265.8f,735,-90,1.65f);
            Put("Bench",653,1178,180);Put("RecyclingBin",670,1178,180);
            Put("Radiator",891,256,0);Put("Clock",904.8f,742,90,2.35f);
            Rebake(); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Hallway] Placed "+root.transform.childCount+" reusable prop instances in SchoolLayout.");
            void Put(string n,float x,float y,float yaw,float height=0)=>Place(n,root.transform,SchoolPlan.Point(x,y,height),yaw);
        }
        static void Rebake()
        {
            var surface=Object.FindFirstObjectByType<NavMeshSurface>();
            var saved=surface.navMeshData;surface.RemoveData();surface.navMeshData=null;surface.BuildNavMesh();
            var fresh=surface.navMeshData;fresh.name=saved.name;surface.RemoveData();
            EditorUtility.CopySerialized(fresh,saved);Object.DestroyImmediate(fresh);
            surface.navMeshData=saved;surface.AddData();EditorUtility.SetDirty(surface);EditorUtility.SetDirty(saved);AssetDatabase.SaveAssets();
        }

        [MenuItem("Confiscated/Hallway Props/Build Gallery and Capture")]
        public static void Gallery()
        {
            var school=SceneManager.GetActiveScene();
            var gallery=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var root=new GameObject("Hallway library gallery");SceneManager.MoveGameObjectToScene(root,gallery);
            // Separate scene at origin, isolated from the live school by camera layer 30.
            var floor=Box(root.transform,"Gallery floor",new Vector3(0,-.05f,-1),new Vector3(9,.1f,7),Mat("M_Floor_Tiles"));
            var fm=floor.GetComponent<MeshFilter>().sharedMesh;fm.uv=fm.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();EditorUtility.SetDirty(fm);
            var wall=Box(root.transform,"Gallery wall",new Vector3(0,1.6f,.42f),new Vector3(9,3.2f,.1f),Mat("M_Wall_Corridor"));
            var wallMesh=wall.GetComponent<MeshFilter>().sharedMesh;
            // Use the existing school wall UV convention: one full wall illustration over its 3m height.
            var verts=wallMesh.vertices;var uv=wallMesh.uv;
            for(int i=0;i<uv.Length;i++)uv[i]=new Vector2((verts[i].x+4.5f)/2,(verts[i].y+1.6f)/3.2f);
            wallMesh.uv=uv;EditorUtility.SetDirty(wallMesh);
            Place("Bench",root.transform,new Vector3(-1.15f,0,0));
            Place("LitterBin",root.transform,new Vector3(-2.5f,0,0));Place("RecyclingBin",root.transform,new Vector3(-3.10f,0,0));
            Place("Radiator",root.transform,new Vector3(.72f,0,.20f));
            Place("DrinkingFountain",root.transform,new Vector3(2.1f,0,.14f));
            Place("WetFloorSign",root.transform,new Vector3(.7f,0,-.8f),-12);
            Place("Clock",root.transform,new Vector3(-1.15f,2.13f,.28f));
            Place("CoatRail",root.transform,new Vector3(-1.15f,1.42f,.30f));
            Place("Extinguisher",root.transform,new Vector3(3.13f,.42f,.22f));
            Text(root.transform,"CONFISCATED  /  HALLWAY OBJECTS",new Vector3(0,2.86f,.345f),.10f);
            var light=new GameObject("Gallery soft light");light.transform.SetParent(root.transform);light.transform.rotation=Quaternion.Euler(38,-28,0);
            var sun=light.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.1f;sun.cullingMask=1<<30;sun.shadows=LightShadows.Soft;
            var cameraGO=new GameObject("Gallery camera");cameraGO.transform.SetParent(root.transform);var camera=cameraGO.AddComponent<Camera>();
            camera.CopyFrom(Camera.main);camera.enabled=false;camera.cullingMask=1<<30;camera.fieldOfView=53;
            camera.orthographic=true;camera.orthographicSize=2.25f;
            camera.transform.position=new Vector3(-.2f,2.15f,-8.4f);camera.transform.LookAt(new Vector3(0,1.4f,0));camera.ResetWorldToCameraMatrix();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            var dir=Path.GetFullPath("../Screenshots/HallwayProps");Directory.CreateDirectory(dir);
            Capture(camera,1600,1000,dir+"/Hallway_Library.png");
            camera.orthographic=false;
            // Single-object close-ups also inspect the side and rear UVs.
            var props=root.GetComponentsInChildren<Transform>().Where(t=>t.parent==root.transform&&t.name.StartsWith("P_Hall_")).ToArray();
            foreach(var target in props)
            {
                foreach(var p in props)p.gameObject.SetActive(p==target);
                var b=new Bounds(target.position,Vector3.zero);
                foreach(var r in target.GetComponentsInChildren<MeshRenderer>())b.Encapsulate(r.bounds);
                float distance=Mathf.Max(.85f,b.size.magnitude*1.1f);
                camera.transform.position=b.center+new Vector3(distance*.42f,distance*.22f,-distance);
                camera.transform.LookAt(b.center);Capture(camera,900,900,dir+"/"+target.name+".png");
                wall.SetActive(false);camera.transform.position=b.center+new Vector3(-distance*.52f,distance*.3f,distance);
                camera.transform.LookAt(b.center);Capture(camera,700,700,dir+"/"+target.name+"_Rear.png");wall.SetActive(true);
            }
            foreach(var p in props)p.gameObject.SetActive(true);
            camera.orthographic=true;camera.transform.position=new Vector3(-.2f,2.15f,-8.4f);camera.transform.LookAt(new Vector3(0,1.4f,0));
            // Make the saved gallery usable by normal cameras without requiring a project layer name.
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=0;
            camera.cullingMask=-1;camera.enabled=true;sun.cullingMask=-1;
            EditorSceneManager.SaveScene(gallery,GalleryPath);EditorSceneManager.CloseScene(gallery,true);
            SceneManager.SetActiveScene(school);Debug.Log("[Hallway] Saved gallery scene and 19 preview images.");
        }
        static GameObject Box(Transform parent,string name,Vector3 p,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;
            var filter=go.GetComponent<MeshFilter>();var mesh=Object.Instantiate(filter.sharedMesh);var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++)vertices[i]=Vector3.Scale(vertices[i],size);mesh.vertices=vertices;mesh.RecalculateBounds();
            mesh.name=name.Replace(" ","_");
            Directory.CreateDirectory("Assets/Art/Meshes/Hallway");string path="Assets/Art/Meshes/Hallway/"+mesh.name+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);}
            filter.sharedMesh=saved;go.GetComponent<BoxCollider>().size=size;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        public static void Capture(Camera camera,int width,int height,string path)
        {
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.Create();var previous=RenderTexture.active;
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
        }

        [MenuItem("Confiscated/Hallway Props/Validate Library and Navigation")]
        public static void Validate()
        {
            int meshes=0;int tris=0;
            foreach(var p in Read.props)
            {
                var root=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+p.name.Replace("SM_","P_")+".prefab");
                if(root==null)throw new Exception("Missing prefab "+p.name);
                foreach(var f in root.GetComponentsInChildren<MeshFilter>())
                {
                    var m=f.sharedMesh;if(m==null||m.uv.Length!=m.vertexCount||m.uv2.Length!=m.vertexCount||m.uv3.Length!=m.vertexCount)
                        throw new Exception("Missing pencil UV channels on "+p.name);
                    if(m.uv3.Any(uv=>uv.x<=0||uv.y<=0))throw new Exception("Invalid face dimensions "+p.name);
                    if(m.colors.Length!=m.vertexCount||m.colors.Any(c=>c.r>.01f))throw new Exception("Wrong pencil side-art flags "+p.name);
                    meshes++;tris+=m.triangles.Length/3;
                }
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponent<TextMesh>()==null))
                    if(r.sharedMaterials.Any(m=>m==null||m.shader.name!="Confiscated/Pencil Surface"))throw new Exception("Wrong pencil material "+p.name);
            }
            int connected=0;var start=Object.FindFirstObjectByType<FirstPersonController>().transform.position;
            if(!NavMesh.SamplePosition(start,out var from,3,NavMesh.AllAreas))throw new Exception("Player off NavMesh");
            foreach(var a in SchoolPlan.Areas)
            {
                var target=SchoolPlan.Point(a.rect.center.x,a.rect.center.y);
                if(!NavMesh.SamplePosition(target,out var hit,3,NavMesh.AllAreas))throw new Exception("No NavMesh in "+a.name);
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(from.position,hit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)
                    throw new Exception("Blocked route "+a.name);connected++;
            }
            string report="PASS: 9 prefabs, "+meshes+" model meshes, "+tris+" triangles, all three pencil UV channels, shared materials.\nNavigation: "+connected+"/49 areas connected.\n";
            File.WriteAllText("../Docs/HallwayProps_Validation.txt",report);Debug.Log(report);
        }
    }
}

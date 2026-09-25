using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;
namespace Confiscated.EditorTools
{
    public static class OutdoorArtSetup
    {
        const string Art="Assets/Art/Textures/Outdoor/",Materials="Assets/Art/Materials/",Meshes="Assets/Art/Meshes/Outdoor/";
        public const string WindowPath="Assets/Prefabs/Props/P_Window_Garden.prefab";
        static Transform root;
        static int meshId;
        static Material trim,glass,scenery,grass;
        [MenuItem("Confiscated/Outdoor/Build sketched outdoors")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play before changing outdoor artwork.");
            Directory.CreateDirectory(Meshes);
            var old=Object.FindFirstObjectByType<OutdoorEnvironment>();
            if(old!=null)
            {
                foreach(var r in old.hiddenWallRenderers)if(r!=null)r.enabled=true;
                Undo.DestroyObjectImmediate(old.gameObject);
            }
            root=new GameObject("Sketched Outdoors").transform;Undo.RegisterCreatedObjectUndo(root.gameObject,"Build sketched outdoors");
            var state=root.gameObject.AddComponent<OutdoorEnvironment>();root.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
            meshId=0;
            var skyTexture=Import(Art+"T_Pencil_Sky.png",false,true);
            var trees=Import(Art+"T_Pencil_Treeline.png",true,false);
            var lawn=Import(Art+"T_Pencil_Grass.png",false,true);
            var sky=Material("M_Pencil_Sky","Confiscated/Pencil Sky");sky.SetTexture("_MainTex",skyTexture);sky.SetFloat("_Exposure",.9f);EditorUtility.SetDirty(sky);
            RenderSettings.skybox=sky;
            scenery=Material("M_Pencil_Treeline","Confiscated/Pencil Scenery");scenery.mainTexture=trees;
            grass=Material("M_Pencil_Grass","Confiscated/Pencil Scenery");grass.mainTexture=lawn;
            trim=AssetDatabase.LoadAssetAtPath<Material>(Materials+"M_Painted_Trim_Pencil.mat");
            glass=Material("M_Sketched_WindowGlass","Confiscated/Pencil Glass");
            glass.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/T_Glass_Pencil.png");
            glass.SetTexture("_EdgeMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/T_Navy_Pencil_Edge.png"));
            glass.SetFloat("_Opacity",.065f);
            foreach(var m in new[]{scenery,grass,glass})EditorUtility.SetDirty(m);
            WindowPrefab();
            // A low ground sheet fills views beyond the playable garden walls; it adds no walkable space.
            var ground=Quad("Distant sketched lawn",new Vector3(0,-.13f,57),new Vector2(400,400),Quaternion.Euler(90,0,0),grass);
            var groundMesh=Object.Instantiate(ground.GetComponent<MeshFilter>().sharedMesh);
            var uv=groundMesh.uv;for(int i=0;i<uv.Length;i++)uv[i]*=100;groundMesh.uv=uv;
            ground.GetComponent<MeshFilter>().sharedMesh=SaveMesh(groundMesh,"OuterLawn");
            foreach(var floor in GameObject.Find("School/Floors").GetComponentsInChildren<MeshRenderer>().Where(x=>x.name.StartsWith("Floor_-")))
            {
                var mesh=Object.Instantiate(floor.GetComponent<MeshFilter>().sharedMesh);var vertices=mesh.vertices;var tex=mesh.uv;
                for(int i=0;i<tex.Length;i++){var p=floor.transform.TransformPoint(vertices[i]);tex[i]=new Vector2(p.x,p.z)/4;}
                mesh.uv=tex;floor.GetComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,floor.name+"_"+meshId++);floor.sharedMaterial=grass;
            }
            // Layered fixed cutouts give parallax from the garden without spinning towards the camera.
            for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI*2/16;var p=new Vector3(Mathf.Sin(angle)*106,7.0f,57+Mathf.Cos(angle)*126);
                Quad("Distant treeline "+i,p,new Vector2(53,18+(i%3)),Quaternion.LookRotation(new Vector3(p.x,0,p.z-57)),scenery);
            }
            Backdrop("West garden trees",new Vector3(-70,4.0f,58),90);
            Backdrop("East garden trees",new Vector3(74,4.0f,57),-90);
            Backdrop("North garden trees",new Vector3(-12,4.0f,146),0);
            Backdrop("South garden trees",new Vector3(0,4.0f,-29),180);
            var positions=new[]{
                SchoolPlan.Point(264,950,1.675f),SchoolPlan.Point(264,1070,1.675f),
                SchoolPlan.Point(908,340,1.675f),SchoolPlan.Point(908,570,1.675f),
                SchoolPlan.Point(365,253,1.675f),SchoolPlan.Point(780,253,1.675f),
                SchoolPlan.Point(350,1183,1.675f),SchoolPlan.Point(720,1183,1.675f)};
            var walls=GameObject.Find("School/Walls").GetComponentsInChildren<MeshRenderer>();
            var groups=new Dictionary<MeshRenderer,List<Vector3>>();
            foreach(var p in positions)
            {
                var wall=walls.FirstOrDefault(w=>w.bounds.Contains(p));
                if(wall==null)throw new InvalidOperationException("No exterior wall at "+p);
                if(!groups.ContainsKey(wall))groups[wall]=new List<Vector3>();
                groups[wall].Add(p);
            }
            foreach(var pair in groups)OpenWall(pair.Key,pair.Value);
            state.hiddenWallRenderers=groups.Keys.Cast<Renderer>().ToArray();
            // Retain transparency when the original escape-window prefab is used in other scenes.
            var escape=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Props/P_Window_Escape.prefab");
            try{foreach(var r in escape.GetComponentsInChildren<MeshRenderer>())if(r.name=="Glass")r.sharedMaterial=glass;PrefabUtility.SaveAsPrefabAsset(escape,"Assets/Prefabs/Props/P_Window_Escape.prefab");}
            finally{PrefabUtility.UnloadPrefabContents(escape);}
            foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(camera.orthographic)continue;
                camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=Mathf.Max(camera.farClipPlane,350);
                EditorUtility.SetDirty(camera);if(PrefabUtility.IsPartOfPrefabInstance(camera))PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            Debug.Log("[Outdoors] Sketched sky, four gardens, layered treelines and eight transparent exterior windows saved.");
        }
        static void Backdrop(string name,Vector3 p,float yaw)=>Quad(name,p,new Vector2(36,12),Quaternion.Euler(0,yaw,0),scenery);
        static void WindowPrefab()
        {
            var go=new GameObject("P_Window_Garden");go.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
            var hold=root;root=go.transform;
            try
            {
                Box("Left painted frame",new Vector3(-1.18f,0,0),new Vector3(.09f,1.45f,.23f),trim);
                Box("Right painted frame",new Vector3(1.18f,0,0),new Vector3(.09f,1.45f,.23f),trim);
                Box("Top painted frame",new Vector3(0,.68f,0),new Vector3(2.45f,.09f,.23f),trim);
                Box("Bottom painted frame",new Vector3(0,-.68f,0),new Vector3(2.45f,.09f,.23f),trim);
                Box("Centre mullion",Vector3.zero,new Vector3(.065f,1.32f,.25f),trim);
                Box("Window sill",new Vector3(0,-.72f,0),new Vector3(2.60f,.055f,.40f),trim);
                Quad("Pencil glass",Vector3.zero,new Vector2(2.32f,1.32f),Quaternion.identity,glass);
                go.AddComponent<BoxCollider>().size=new Vector3(2.45f,1.45f,.16f);
                PrefabUtility.SaveAsPrefabAsset(go,WindowPath);
            }
            finally{root=hold;Object.DestroyImmediate(go);}
        }
        static void OpenWall(MeshRenderer original,List<Vector3> points)
        {
            var b=original.bounds;bool alongZ=b.size.z>b.size.x;
            float start=alongZ?b.min.z:b.min.x,end=alongZ?b.max.z:b.max.x;
            foreach(var p in points.OrderBy(p=>alongZ?p.z:p.x))
            {
                float c=alongZ?p.z:p.x;Segment(start,c-1.225f,b.min.y,b.max.y);
                Segment(c-1.225f,c+1.225f,b.min.y,.95f);Segment(c-1.225f,c+1.225f,2.4f,b.max.y);start=c+1.225f;
                var window=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WindowPath),root);
                window.name="Garden view window "+points.IndexOf(p)+" "+original.name;window.transform.position=p;
                window.transform.rotation=Quaternion.Euler(0,alongZ?90:0,0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(window.transform);
            }
            Segment(start,end,b.min.y,b.max.y);original.enabled=false;EditorUtility.SetDirty(original);
            // Keep the existing collision shell and baked navigation intact; only its rendering is replaced.
            void Segment(float a,float z,float low,float high)
            {
                if(z-a<.001f||high-low<.001f)return;
                var size=alongZ?new Vector3(b.size.x,high-low,z-a):new Vector3(z-a,high-low,b.size.z);
                var centre=alongZ?new Vector3(b.center.x,(low+high)/2,(a+z)/2):new Vector3((a+z)/2,(low+high)/2,b.center.z);
                var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=original.name+" window surround";g.transform.SetParent(root,false);g.transform.position=centre;
                Object.DestroyImmediate(g.GetComponent<Collider>());
                var mesh=Object.Instantiate(g.GetComponent<MeshFilter>().sharedMesh);var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
                for(int i=0;i<vertices.Length;i++)
                {
                    vertices[i]=Vector3.Scale(vertices[i],size);var w=centre+vertices[i];
                    uv[i]=Mathf.Abs(normals[i].y)>.5f?new Vector2(w.x,w.z):new Vector2((Mathf.Abs(normals[i].x)>.5f?w.z:w.x)/2,w.y/3);
                }
                mesh.vertices=vertices;mesh.uv=uv;mesh.RecalculateBounds();mesh.RecalculateTangents();
                g.GetComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,"WindowWall_"+meshId++);
                g.GetComponent<MeshRenderer>().sharedMaterial=original.sharedMaterial;
            }
        }
        static GameObject Box(string name,Vector3 p,Vector3 size,Material material)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root,false);g.transform.localPosition=p;g.transform.localScale=size;
            Object.DestroyImmediate(g.GetComponent<Collider>());IllustratedArtSetup.Tiled(g.GetComponent<MeshRenderer>(),material,"OutdoorTrim",1);return g;
        }
        static GameObject Quad(string name,Vector3 p,Vector2 size,Quaternion rotation,Material material)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Quad);g.name=name;g.transform.SetParent(root,false);
            g.transform.localPosition=p;g.transform.localRotation=rotation;g.transform.localScale=new Vector3(size.x,size.y,1);
            Object.DestroyImmediate(g.GetComponent<Collider>());var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;return g;
        }
        static Mesh SaveMesh(Mesh mesh,string name)
        {
            string path=Meshes+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);return existing;
        }
        static Material Material(string name,string shader)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(Materials+name+".mat");
            if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,Materials+name+".mat");}
            else m.shader=Shader.Find(shader);
            return m;
        }
        static Texture2D Import(string path,bool alpha,bool repeat)
        {
            AssetDatabase.ImportAsset(path);var i=(TextureImporter)AssetImporter.GetAtPath(path);
            i.alphaIsTransparency=alpha;i.alphaSource=alpha?TextureImporterAlphaSource.FromInput:TextureImporterAlphaSource.None;
            i.maxTextureSize=4096;i.npotScale=TextureImporterNPOTScale.None;i.mipmapEnabled=true;
            i.wrapMode=repeat?TextureWrapMode.Repeat:TextureWrapMode.Clamp;i.filterMode=FilterMode.Trilinear;i.anisoLevel=4;i.textureCompression=TextureImporterCompression.Uncompressed;
            if(path.Contains("Sky"))i.wrapModeV=TextureWrapMode.Clamp;
            i.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        [MenuItem("Confiscated/Outdoor/Capture outdoor views")]
        public static void Capture()
        {
            var source=Object.FindFirstObjectByType<PlayerInteractor>().ViewCamera;
            var g=new GameObject("Outdoor review camera");var c=g.AddComponent<Camera>();c.CopyFrom(source);c.enabled=false;c.fieldOfView=70;c.clearFlags=CameraClearFlags.Skybox;c.farClipPlane=350;
            try
            {
                Shot("Garden_West",SchoolPlan.Point(155,660,1.6f),new Vector3(-80,7,58));
                Shot("Garden_North",SchoolPlan.Point(480,165,1.6f),new Vector3(-12,7,148));
                Shot("Garden_East",SchoolPlan.Point(1010,680,1.6f),new Vector3(80,7,57));
                Shot("Garden_South",SchoolPlan.Point(585,1250,1.6f),new Vector3(0,7,-35));
                Shot("Window_Inside",SchoolPlan.Point(280,950,1.6f),SchoolPlan.Point(245,950,1.9f));
                Shot("Window_Outside",SchoolPlan.Point(240,950,1.7f),SchoolPlan.Point(275,950,1.7f));
            }
            finally{Object.DestroyImmediate(g);}
            void Shot(string name,Vector3 p,Vector3 target){g.transform.position=p;g.transform.LookAt(target);HallwayPropLibrary.Capture(c,1400,900,"D:/Confiscated/Docs/Outdoor_"+name+".png");}
        }
    }
}

using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    public static class MainEntranceSetup
    {
        const string Prefabs="Assets/Prefabs/Entrance/";
        static Material trim,teal,wood,ink,paper,glass,mat;
        static Material M(string n)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+n+".mat");

        [MenuItem("Confiscated/Build Main School Entrance")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play before building the entrance.");
            ApplyToScene();DiningHallSetup.Rebake();EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
        public static void ApplyToScene()
        {
            var door=GameObject.Find("School/Doors/South yard doors");if(door==null)return;
            Directory.CreateDirectory(Prefabs);
            trim=M("M_Painted_Trim_Pencil");wood=M("M_Wood_Desk");ink=M("M_Chapter_Ink");paper=M("M_Chapter_Paper");glass=M("M_Sketched_WindowGlass");
            teal=Tint("M_Entrance_Teal",trim,new Color(.35f,.57f,.54f));
            mat=Tint("M_Entrance_Mat",trim,new Color(.48f,.43f,.33f));
            var old=GameObject.Find("Main School Entrance");if(old!=null)Object.DestroyImmediate(old);
            var root=new GameObject("Main School Entrance").transform;
            // Clear central approach and doorway; all posts, seats and furniture sit to the sides.
            Box(root,"Entrance paving",new Vector3(0,.012f,-7.8f),new Vector3(3.35f,.024f,7.7f),trim,false);
            for(int i=0;i<10;i++)Box(root,"Paving joint",new Vector3(0,.026f,-4.15f-i*.77f),new Vector3(3.35f,.004f,.016f),ink,false);
            Box(root,"Paving centre joint",new Vector3(0,.027f,-7.8f),new Vector3(.015f,.004f,7.7f),ink,false);
            for(int side=-1;side<=1;side+=2)
            {
                Box(root,"Cream entrance facade",new Vector3(side*2.84f,1.5f,-4.005f),new Vector3(2.72f,3,.065f),trim,false);
                Box(root,"Facade pilaster",new Vector3(side*4.13f,1.5f,-4.10f),new Vector3(.18f,3,.20f),trim);
                Box(root,"Canopy pillar",new Vector3(side*3.70f,1.43f,-6.10f),new Vector3(.18f,2.86f,.18f),teal);
                Box(root,"Pillar foot",new Vector3(side*3.70f,.15f,-6.10f),new Vector3(.30f,.30f,.30f),trim);
                Box(root,"Entrance lamp backing",new Vector3(side*1.68f,2.30f,-4.10f),new Vector3(.22f,.38f,.12f),ink,false);
                Box(root,"Entrance lamp glass",new Vector3(side*1.68f,2.30f,-4.17f),new Vector3(.15f,.27f,.035f),paper,false);
            }
            Box(root,"Canopy roof",new Vector3(0,3.10f,-5.14f),new Vector3(8.75f,.18f,2.60f),teal);
            Box(root,"Canopy cream fascia",new Vector3(0,2.97f,-6.43f),new Vector3(8.75f,.38f,.09f),trim,false);
            Text(root,"MAIN ENTRANCE",new Vector3(0,2.98f,-6.49f),0,.028f);
            Sign(root,"WELCOME",new Vector3(0,2.60f,-4.08f),new Vector2(2.90f,.46f),0,.025f);
            Sign(root,"VISITORS\nPlease sign in",new Vector3(-2.60f,1.66f,-4.12f),new Vector2(1.50f,.69f),0,.017f);
            Noticeboard(root,new Vector3(2.82f,1.58f,-4.14f));
            Mat(root,new Vector3(0,.035f,-4.95f));
            Mat(root,new Vector3(0,.035f,-2.65f));
            Bench(root,new Vector3(-5.0f,0,-7.40f),270);
            Bench(root,new Vector3(-3.56f,0,-1.70f),270);
            Planter(root,new Vector3(-2.30f,0,-5.90f));Planter(root,new Vector3(2.30f,0,-5.90f));
            // A small reception ledge and visitors' book establish the entrance on the inside too.
            var desk=new GameObject("Visitor sign-in desk").transform;desk.SetParent(root,false);desk.localPosition=new Vector3(3.05f,0,-1.7f);
            Box(desk,"Writing top",new Vector3(0,.83f,0),new Vector3(1.35f,.07f,.72f),trim);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Box(desk,"Wooden leg",new Vector3(x*.56f,.40f,z*.25f),new Vector3(.075f,.80f,.075f),wood);
            Box(desk,"Front panel",new Vector3(0,.55f,.28f),new Vector3(1.17f,.35f,.05f),teal);
            Sign(desk,"VISITORS",new Vector3(0,.58f,.32f),new Vector2(.84f,.21f),180,.012f);
            Box(desk,"Visitor book cover",new Vector3(-.13f,.88f,0),new Vector3(.53f,.035f,.38f),wood,false);
            Box(desk,"Open visitor book",new Vector3(-.13f,.904f,0),new Vector3(.49f,.012f,.35f),paper,false);
            for(int i=0;i<5;i++)Box(desk,"Book ruled line",new Vector3(-.13f,.912f,-.12f+i*.055f),new Vector3(.43f,.002f,.0025f),ink,false);
            Box(desk,"Book centre fold",new Vector3(-.13f,.914f,0),new Vector3(.007f,.003f,.35f),ink,false);
            var pen=Box(desk,"Pencil",new Vector3(.29f,.89f,.025f),new Vector3(.012f,.012f,.22f),ink,false);pen.transform.localRotation=Quaternion.Euler(0,15,0);
            Sign(root,"SCHOOL OFFICE\nVisitor sign-in",new Vector3(3.05f,1.72f,-3.77f),new Vector2(1.68f,.65f),180,.017f);
            foreach(var hinge in new[]{door.GetComponent<OfficeDoor>().hinge,door.GetComponent<OfficeDoor>().secondHinge})
            {
                var existing=hinge.Find("Entrance glazing");if(existing!=null)Object.DestroyImmediate(existing.gameObject);
                var leaf=hinge.Find("Leaf");leaf.GetComponent<Renderer>().enabled=false;EditorUtility.SetDirty(leaf.GetComponent<Renderer>());
                var frame=new GameObject("Entrance glazing").transform;frame.SetParent(hinge,false);frame.localPosition=leaf.localPosition;
                // Cancel the door root's scale so surface hatching remains at the same world scale.
                frame.localScale=new Vector3(1/door.transform.localScale.x,1/door.transform.localScale.y,1);
                float w=1.30f,h=2.20f;
                Box(frame,"Left stile",new Vector3(-w/2+.055f,0,0),new Vector3(.11f,h,.085f),teal,false);
                Box(frame,"Right stile",new Vector3(w/2-.055f,0,0),new Vector3(.11f,h,.085f),teal,false);
                Box(frame,"Top rail",new Vector3(0,h/2-.05f,0),new Vector3(w,.10f,.085f),teal,false);
                Box(frame,"Lower panel",new Vector3(0,-.82f,0),new Vector3(w,.56f,.085f),teal,false);
                Box(frame,"Middle rail",new Vector3(0,-.23f,0),new Vector3(w,.10f,.085f),teal,false);
                var pane=GameObject.CreatePrimitive(PrimitiveType.Quad);pane.name="Pencil-streaked glass";pane.transform.SetParent(frame,false);pane.transform.localPosition=new Vector3(0,.26f,0);pane.transform.localScale=new Vector3(1.08f,1.55f,1);Object.DestroyImmediate(pane.GetComponent<Collider>());pane.GetComponent<Renderer>().sharedMaterial=glass;
                float handle=hinge==door.GetComponent<OfficeDoor>().hinge ? .43f : -.43f;
                for(int side=-1;side<=1;side+=2)
                {
                    Box(frame,"Handle",new Vector3(handle,-.14f,side*.09f),new Vector3(.035f,.34f,.035f),ink,false);
                    Box(frame,"Handle fixing",new Vector3(handle,-.29f,side*.055f),new Vector3(.065f,.055f,.065f),trim,false);
                    Box(frame,"Handle fixing",new Vector3(handle,.01f,side*.055f),new Vector3(.065f,.055f,.065f),trim,false);
                }
            }
            var sign=GameObject.Find("School/Details/South yard doors sign");if(sign!=null){sign.GetComponentInChildren<TextMesh>().text="MAIN ENTRANCE";sign.GetComponentInChildren<TextMesh>().color=Color.black;}
            PrefabUtility.SaveAsPrefabAsset(root.gameObject,Prefabs+"P_MainEntranceFurnishings.prefab");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
        static GameObject Box(Transform parent,string name,Vector3 p,Vector3 size,Material material,bool collision=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;
            if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());
            IllustratedArtSetup.Tiled(g.GetComponent<MeshRenderer>(),material,"Entrance",1);return g;
        }
        static void Text(Transform parent,string value,Vector3 p,float yaw,float size)
        {
            var g=new GameObject(value);g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var t=g.AddComponent<TextMesh>();t.font=SchoolTypography.Font;t.text=value;t.fontSize=80;t.characterSize=size;t.color=Color.black;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;g.AddComponent<WorldLabel>();
        }
        static void Sign(Transform parent,string text,Vector3 p,Vector2 size,float yaw,float letters)
        {Box(parent,text+" plaque",p,new Vector3(size.x,size.y,.04f),trim,false);Text(parent,text,p+Quaternion.Euler(0,yaw,0)*Vector3.back*.027f,yaw,letters);}
        static void Mat(Transform root,Vector3 p)
        {
            Box(root,"Entrance mat",p,new Vector3(2.60f,.025f,1.15f),mat,false);
            for(int i=0;i<17;i++)Box(root,"Mat rib",p+new Vector3(-1.2f+i*.15f,.014f,0),new Vector3(.015f,.003f,1.02f),ink,false);
        }
        static void Bench(Transform root,Vector3 p,float yaw)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hallway/P_Hall_Bench.prefab");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root);go.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }
        static void Planter(Transform root,Vector3 p)
        {
            var group=new GameObject("Entrance planter").transform;group.SetParent(root,false);group.localPosition=p;
            Box(group,"Wooden planter",new Vector3(0,.25f,0),new Vector3(.64f,.5f,.64f),wood);
            Box(group,"Soil",new Vector3(0,.507f,0),new Vector3(.54f,.02f,.54f),ink,false);
            // Flat overlapping pencil leaves keep the same illustrated, angular treatment as the props.
            for(int i=0;i<9;i++){float angle=i*2.4f;var leaf=Box(group,"Painted leaf",new Vector3(Mathf.Sin(angle)*.16f,.64f+(i%3)*.09f,Mathf.Cos(angle)*.16f),new Vector3(.12f,.43f,.04f),teal,false);leaf.transform.localRotation=Quaternion.Euler(20,i*43,20);}
        }
        static void Noticeboard(Transform root,Vector3 p)
        {
            Box(root,"School noticeboard",p,new Vector3(1.65f,1.25f,.075f),wood,false);
            Box(root,"Cork inset",p+Vector3.back*.044f,new Vector3(1.48f,1.08f,.02f),mat,false);
            Sign(root,"SCHOOL NOTICES",p+new Vector3(0,.42f,-.07f),new Vector2(1.32f,.22f),0,.013f);
            Sign(root,"TERM DATES",p+new Vector3(-.35f,0,-.07f),new Vector2(.62f,.52f),0,.009f);
            Sign(root,"SCHOOL\nNEWS",p+new Vector3(.36f,-.02f,-.07f),new Vector2(.60f,.62f),0,.012f);
        }
        static Material Tint(string name,Material source,Color colour)
        {
            var result=M(name);if(result==null){result=new Material(source);result.name=name;AssetDatabase.CreateAsset(result,"Assets/Art/Materials/"+name+".mat");}
            result.SetColor("_BaseColor",colour);EditorUtility.SetDirty(result);return result;
        }
    }
}



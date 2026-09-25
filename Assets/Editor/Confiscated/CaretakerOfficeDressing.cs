using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Confiscated.EditorTools
{
    public static class CaretakerOfficeDressing
    {
        static Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+n+".mat");
        static Transform Group(string n,Transform parent,Vector3 p){var t=new GameObject(n).transform;t.SetParent(parent,false);t.localPosition=p;return t;}
        static Transform Box(string n,Transform parent,Vector3 p,Vector3 s,string mat,bool collision=false)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=s;IllustratedArtSetup.Tiled(g.GetComponent<MeshRenderer>(),Mat(mat),"OfficeDressing",.7f);if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());return g.transform;}
        static void Block(Transform root,Vector3 center,Vector3 size)
        {
            var c=root.gameObject.AddComponent<BoxCollider>();c.center=center;c.size=size;
            var mod=root.gameObject.AddComponent<NavMeshModifier>();mod.ignoreFromBuild=true;
            var nav=root.gameObject.AddComponent<NavMeshObstacle>();nav.center=center;nav.size=size;nav.carving=true;
        }
        static void Shelf(Transform parent,Vector3 position,float yaw)
        {
            var p=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SchoolRun/P_TallStorage.prefab");var shelf=(GameObject)PrefabUtility.InstantiatePrefab(p,parent);shelf.name="Maintenance stores";shelf.transform.localPosition=position;shelf.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var mod=shelf.GetComponent<NavMeshModifier>();if(mod==null)mod=shelf.AddComponent<NavMeshModifier>();mod.ignoreFromBuild=true;
            var nav=shelf.GetComponent<NavMeshObstacle>();if(nav==null)nav=shelf.AddComponent<NavMeshObstacle>();nav.center=new Vector3(0,1,0);nav.size=new Vector3(1.8f,2,.75f);nav.carving=true;
        }
        static Transform Bench(Transform parent,string name,Vector3 position,Vector3 size)
        {
            var b=Group(name,parent,position);Box("Scarred timber worktop",b,new Vector3(0,.85f,0),new Vector3(size.x,.1f,size.z),"M_Wood_Desk");
            foreach(float x in new[]{-size.x*.43f,size.x*.43f})foreach(float z in new[]{-size.z*.36f,size.z*.36f})Box("Painted bench leg",b,new Vector3(x,.41f,z),new Vector3(.09f,.82f,.09f),"M_Chapter_Green");
            Box("Lower shelf",b,new Vector3(0,.23f,0),new Vector3(size.x-.12f,.06f,size.z-.08f),"M_Wood_Desk");Block(b,new Vector3(0,.45f,0),new Vector3(size.x,.9f,size.z));return b;
        }
        [MenuItem("Confiscated/School Run/Dress Caretaker Office")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play before dressing the office.");
            if(GameObject.Find("Caretaker office workshop")!=null)return;
            var root=new GameObject("Caretaker office workshop").transform;Undo.RegisterCreatedObjectUndo(root.gameObject,"Dress caretaker office");
            foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))if(t.name=="ConfiscationPolicy"){var p=t.position;p.x=-46.5f;t.position=p;EditorUtility.SetDirty(t);}
            // Storage backs face the outer walls. Leave the doorway and the phone/tool desk untouched.
            Shelf(root,new Vector3(-49.6f,0,86.1f),-90);Shelf(root,new Vector3(-49.6f,0,88.2f),-90);
            Shelf(root,new Vector3(-36.15f,0,86.6f),90);Shelf(root,new Vector3(-36.15f,0,88.7f),90);
            var bench=Bench(root,"Repair bench",new Vector3(-42.3f,0,89.85f),new Vector3(4,.9f,.9f));
            for(int i=0;i<3;i++)Box("Spare fittings crate",bench,new Vector3(-1.25f+i*1.2f,.43f,0),new Vector3(.9f,.35f,.65f),"M_Chapter_Cardboard");
            Box("Toolbox",bench,new Vector3(1.2f,1.02f,0),new Vector3(.65f,.25f,.35f),"M_Chapter_ToyRed");
            Box("Toolbox handle",bench,new Vector3(1.2f,1.19f,0),new Vector3(.25f,.06f,.05f),"M_Chapter_Ink");
            Box("Repair mat",bench,new Vector3(-.8f,.91f,0),new Vector3(1.2f,.012f,.65f),"M_Chapter_Green");
            var board=Group("Workshop tool board",root,new Vector3(-42.3f,1.95f,90.55f));
            Box("Timber frame",board,Vector3.zero,new Vector3(3.8f,1.25f,.075f),"M_Wood_Desk");Box("Cork backing",board,new Vector3(0,0,-.05f),new Vector3(3.62f,1.08f,.025f),"M_Chapter_Cardboard");
            for(int i=0;i<6;i++){float x=-1.45f+i*.56f;Box("Hanging tool shaft",board,new Vector3(x,-.07f,-.085f),new Vector3(.035f,.5f,.035f),"M_Chapter_Grey");Box("Tool grip",board,new Vector3(x,-.23f,-.1f),new Vector3(.07f,.21f,.055f),"M_Chapter_ToyRed");Box("Tool head",board,new Vector3(x,.19f,-.1f),new Vector3(i%2==0?.23f:.075f,.085f,.07f),"M_Chapter_Grey");}
            // Low central return gives the oversized room a front office and rear workshop.
            var divider=Group("Low workshop cabinet",root,new Vector3(-43,0,84.3f));
            Box("Cabinet body",divider,new Vector3(0,.55f,0),new Vector3(3.8f,1.1f,.65f),"M_Chapter_Green");Box("Wooden cabinet top",divider,new Vector3(0,1.13f,0),new Vector3(3.9f,.08f,.72f),"M_Wood_Desk");
            for(int i=0;i<5;i++){float x=-1.48f+i*.74f;Box("Cupboard door",divider,new Vector3(x,.55f,-.335f),new Vector3(.7f,1.01f,.018f),"M_Chapter_Green");Box("Brass handle",divider,new Vector3(x+.22f,.72f,-.36f),new Vector3(.035f,.14f,.025f),"M_Chapter_Brass");}
            Block(divider,new Vector3(0,.59f,0),new Vector3(3.9f,1.18f,.72f));
            for(int i=0;i<2;i++)Box("Maintenance log folder",divider,new Vector3(-.65f+i*.65f,1.19f,0),new Vector3(.4f,.035f,.5f),"M_Chapter_Paper");
            var rail=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hallway/P_Hall_CoatRail.prefab");if(rail!=null){var g=(GameObject)PrefabUtility.InstantiatePrefab(rail,root);g.name="Caretaker coat hooks";g.transform.position=new Vector3(-38.2f,1.65f,90.55f);}
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
    }
}

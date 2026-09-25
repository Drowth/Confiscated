using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Confiscated.EditorTools
{
    /// <summary>School dinner trolley dressed with the school's existing pencil textures.</summary>
    public static class TrolleyArtSetup
    {
        static Material enamel,paper,ink,metal;
        static Material Surface(string name,Color tint,string texture)
        {
            string path="Assets/Art/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Confiscated/Pencil Surface"));AssetDatabase.CreateAsset(m,path);}
            m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/"+texture+".png"));
            m.SetTexture("_EdgeMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/T_Navy_Pencil_Edge.png"));
            m.SetColor("_BaseColor",tint);m.SetColor("_EdgeColor",new Color(.045f,.06f,.1f));m.SetFloat("_EdgeWidth",.008f);m.SetFloat("_EdgeRepeat",.35f);
            EditorUtility.SetDirty(m);return m;
        }
        static Transform Part(Transform parent,string name,Vector3 position,Vector3 scale,Material material,PrimitiveType type=PrimitiveType.Cube)
        {
            var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=scale;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            IllustratedArtSetup.Tiled(g.GetComponent<MeshRenderer>(),material,"DinnerTrolley_"+type,.6f);
            return g.transform;
        }
        static void Rail(Transform parent,string name,Vector3 a,Vector3 b,float width,Material material)
        {var t=Part(parent,name,(a+b)*.5f,new Vector3(width,Vector3.Distance(a,b)*.5f,width),material,PrimitiveType.Cylinder);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        public static void Apply(DinnerTrolleyPatrol trolley)
        {
            if(trolley==null)return;
            ApplyVisual(trolley.transform);
            DinnerLadySetup.Configure(trolley);
        }
        public static void ApplyVisual(Transform target,bool supplies=false)
        {
            enamel=Surface("M_Trolley_SketchedTeal",new Color(.2f,.44f,.39f),"T_Painted_Trim_Pencil");
            paper=Surface("M_Trolley_SketchedCream",new Color(.98f,.94f,.8f),"T_Painted_Trim_Pencil");
            metal=Surface("M_Trolley_SketchedSteel",new Color(.56f,.62f,.64f),"T_Painted_Trim_Pencil");
            ink=Surface("M_Trolley_SketchedInk",new Color(.095f,.11f,.15f),"T_Painted_Trim_Pencil");
            // Only replace visual children made by the trolley builders. Root collision and movement remain intact.
            foreach(var child in target.Cast<Transform>().ToArray())
                if(new[]{"Tray shelf","Upright","Wheel","Covered lunch trays","Sketched trolley visual"}.Contains(child.name))Object.DestroyImmediate(child.gameObject);
            var root=new GameObject("Sketched trolley visual").transform;root.SetParent(target,false);
            foreach(float h in new[]{.25f,.82f})
            {
                Part(root,"Teal enamel shelf",new Vector3(0,h,0),new Vector3(1.24f,.05f,.65f),enamel);
                foreach(float z in new[]{-.32f,.32f})Part(root,"Raised pencil edged rim",new Vector3(0,h+.045f,z),new Vector3(1.25f,.08f,.035f),enamel);
                foreach(float x in new[]{-.61f,.61f})Part(root,"Shelf end rim",new Vector3(x,h+.045f,0),new Vector3(.035f,.08f,.65f),enamel);
            }
            foreach(float x in new[]{-.55f,.55f})foreach(float z in new[]{-.265f,.265f})
            {
                Rail(root,"Tubular frame",new Vector3(x,.17f,z),new Vector3(x,.94f,z),.045f,metal);
                Part(root,"Caster fork",new Vector3(x,.14f,z),new Vector3(.1f,.12f,.09f),metal);
                var wheel=Part(root,"Round rubber caster",new Vector3(x,.077f,z),new Vector3(.15f,.035f,.15f),ink,PrimitiveType.Cylinder);wheel.localRotation=Quaternion.Euler(90,0,0);
                var hub=Part(root,"Caster hub",new Vector3(x,.077f,z-.038f),new Vector3(.045f,.007f,.045f),metal,PrimitiveType.Cylinder);hub.localRotation=Quaternion.Euler(90,0,0);
            }
            foreach(float z in new[]{-.25f,.25f})Rail(root,"Handle upright",new Vector3(.55f,.88f,z),new Vector3(.55f,1.02f,z),.04f,metal);
            Rail(root,"Dark push handle",new Vector3(.55f,1.02f,-.25f),new Vector3(.55f,1.02f,.25f),.055f,ink);
            for(int i=0;i<3;i++)
            {
                var tray=new GameObject("Stacked cream dinner tray").transform;tray.SetParent(root,false);tray.localPosition=new Vector3(-.12f,.87f+i*.038f,0);tray.localRotation=Quaternion.Euler(0,i==1?-3:2,0);
                Part(tray,"Tray base",Vector3.zero,new Vector3(.63f,.022f,.43f),paper);
                foreach(float z in new[]{-.21f,.21f})Part(tray,"Tray lip",new Vector3(0,.017f,z),new Vector3(.64f,.024f,.018f),paper);
                foreach(float x in new[]{-.31f,.31f})Part(tray,"Tray lip",new Vector3(x,.017f,0),new Vector3(.018f,.024f,.43f),paper);
            }
            Part(root,"Folded tea towel",new Vector3(-.2f,.296f,.06f),new Vector3(.48f,.036f,.38f),paper);
            if(supplies)
            {
                foreach(var child in root.Cast<Transform>().ToArray())
                {
                    if(child.name=="Stacked cream dinner tray"||child.name=="Folded tea towel"){Object.DestroyImmediate(child.gameObject);continue;}
                    var p=child.localPosition;p.x*=2.25f;child.localPosition=p;
                    if(child.name=="Teal enamel shelf"||child.name=="Raised pencil edged rim"){var s=child.localScale;s.x*=2.25f;child.localScale=s;}
                }
                var card=Surface("M_Trolley_SketchedCardboard",new Color(.72f,.49f,.28f),"T_Painted_Trim_Pencil");
                foreach(float x in new[]{-.86f,0,.83f})
                {
                    float height=x==0?.36f:.23f;
                    Part(root,"Box of exercise books",new Vector3(x,.85f+height*.5f,0),new Vector3(.61f,height,.48f),card);
                    Part(root,"Paper packing tape",new Vector3(x,.856f+height,0),new Vector3(.07f,.006f,.49f),paper);
                    Part(root,"Spare paper bundle",new Vector3(x,.33f,.02f),new Vector3(.53f,.11f,.4f),paper);
                }
            }
            EditorUtility.SetDirty(target);
        }
        [MenuItem("Confiscated/Chase Feedback/Refresh Trolley Art")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Exit Play before refreshing trolley art.");
            foreach(var t in Object.FindObjectsByType<DinnerTrolleyPatrol>(FindObjectsSortMode.None))Apply(t);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
    }
}

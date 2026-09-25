using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
namespace Confiscated.EditorTools
{
    public static class ChaseFeedbackSetup
    {
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+(name=="M_Chapter_Grey"?"M_Chase_Steel":name)+".mat");
        static Transform Group(string name,Transform parent,Vector3 pos){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=pos;return t;}
        static Transform Box(string name,Transform parent,Vector3 pos,Vector3 scale,string material,bool solid=false)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=Mat(material);if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());return g.transform;}
        static LineRenderer Stroke(Transform parent,string name,Vector3[] points,string material,float width=.013f)
        {var g=new GameObject(name);g.transform.SetParent(parent,false);var l=g.AddComponent<LineRenderer>();l.useWorldSpace=false;l.positionCount=points.Length;l.SetPositions(points);l.startWidth=l.endWidth=width;l.sharedMaterial=Mat(material);l.numCapVertices=2;return l;}
        internal static void Lock(ProgressPropFeedback feedback,Transform parent,Vector3 local)
        {
            var t=Group("Drawn padlock",parent,local);feedback.padlock=t;
            Box("Lock body",t,Vector3.zero,new Vector3(.16f,.14f,.055f),"M_Chapter_Brass");
            Stroke(t,"Shackle",new[]{new Vector3(-.05f,.07f,0),new Vector3(-.05f,.15f,0),new Vector3(0,.175f,0),new Vector3(.05f,.15f,0),new Vector3(.05f,.07f,0)},"M_Chapter_Grey");
            Box("Keyhole",t,new Vector3(0,0,-.03f),new Vector3(.015f,.035f,.005f),"M_Chapter_Ink");
        }
        [MenuItem("Confiscated/Chase Feedback/Install")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play before installing.");
            const string steelPath="Assets/Art/Materials/M_Chase_Steel.mat";
            if(AssetDatabase.LoadAssetAtPath<Material>(steelPath)==null){var steel=new Material(Mat("M_Chapter_Cardboard"));steel.name="M_Chase_Steel";steel.SetColor("_BaseColor",new Color(.58f,.65f,.64f));AssetDatabase.CreateAsset(steel,steelPath);}
            const string wetPath="Assets/Art/Materials/M_Chase_Wet.mat";if(AssetDatabase.LoadAssetAtPath<Material>(wetPath)==null){var wet=new Material(Mat("M_Chapter_Cardboard"));wet.name="M_Chase_Wet";wet.SetColor("_BaseColor",new Color(.22f,.6f,.85f));AssetDatabase.CreateAsset(wet,wetPath);}
            const string path="Assets/Resources/Art/CaretakerExpressions.png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer!=null){importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}
            foreach(var phase in new[]{2,3,4})foreach(var foot in new[]{"LeftUp","RightUp"})
            {
                var frameImporter=AssetImporter.GetAtPath($"Assets/Resources/Art/CaretakerPhase{phase}_{foot}.png") as TextureImporter;
                if(frameImporter==null)continue;
                frameImporter.alphaIsTransparency=true;frameImporter.mipmapEnabled=false;
                frameImporter.npotScale=TextureImporterNPOTScale.None;frameImporter.wrapMode=TextureWrapMode.Clamp;
                frameImporter.maxTextureSize=2048;frameImporter.textureCompression=TextureImporterCompression.Uncompressed;
                frameImporter.SaveAndReimport();
            }
            var root=GameObject.Find("Chase feedback props");if(root==null)root=new GameObject("Chase feedback props");
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())if(renderer.sharedMaterial!=null&&renderer.sharedMaterial.name=="M_Chapter_Grey")renderer.sharedMaterial=Mat("M_Chapter_Grey");
            if(root.transform.childCount==0)
            {
                Wet(root.transform,new Vector3(-25,0,78),new Vector2(2,2.4f));
                Wet(root.transform,new Vector3(-34,0,63),new Vector2(1.7f,2.6f));
                var cart=Group("Dinner service trolley",root.transform,new Vector3(-27,0,78));
                foreach(float h in new[]{.25f,.85f})Box("Tray shelf",cart,new Vector3(0,h,0),new Vector3(1.3f,.07f,.7f),"M_Chapter_Grey");
                foreach(float x in new[]{-.58f,.58f})foreach(float z in new[]{-.27f,.27f}){Box("Upright",cart,new Vector3(x,.52f,z),new Vector3(.045f,.8f,.045f),"M_Chapter_Grey");Box("Wheel",cart,new Vector3(x,.08f,z),new Vector3(.12f,.16f,.09f),"M_Chapter_Ink");}
                Box("Covered lunch trays",cart,new Vector3(0,.94f,0),new Vector3(.88f,.12f,.5f),"M_Chapter_Paper");
                var hit=cart.gameObject.AddComponent<BoxCollider>();hit.center=new Vector3(0,.5f,0);hit.size=new Vector3(1.3f,1,.7f);
                var nav=cart.gameObject.AddComponent<NavMeshObstacle>();nav.center=hit.center;nav.size=hit.size;nav.carving=true;nav.carveOnlyStationary=false;
                var patrol=cart.gameObject.AddComponent<DinnerTrolleyPatrol>();patrol.end=new Vector3(-13,0,78);
            }
            foreach(var wet in root.GetComponentsInChildren<WetFloorHazard>())
            {
                if(wet.transform.position.x>-30){wet.transform.position=new Vector3(-25,0,79.5f);var stand=wet.transform.Find("Yellow caution stand");if(stand!=null)stand.localRotation=Quaternion.Euler(0,90,0);}
                WetFloorArtSetup.Apply(wet);
            }
            var dinner=root.GetComponentInChildren<DinnerTrolleyPatrol>();
            // A moving obstacle must never be baked into the navigation mesh as a permanent hole.
            if(dinner!=null&&dinner.GetComponent<NavMeshModifier>()==null)dinner.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
            if(dinner!=null){dinner.transform.position=new Vector3(-27,0,79.4f);dinner.end=new Vector3(-13,0,79.4f);dinner.speed=1.25f;dinner.waitSeconds=2.5f;}
            foreach(var door in Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None))
            {
                if(door.closedForRun||door.startsUnlocked||door.mission==null&&door.runRequiredLevel==0||door.GetComponent<ProgressPropFeedback>()!=null)continue;
                var f=door.gameObject.AddComponent<ProgressPropFeedback>();f.door=door;Lock(f,door.hinge,new Vector3(.8f,.9f,-.067f));EditorUtility.SetDirty(door);
            }
            foreach(var gate in Object.FindObjectsByType<RunGate>(FindObjectsSortMode.None))
            {
                if(gate.GetComponent<ProgressPropFeedback>()!=null)continue;
                if(gate.kind==RunGate.Kind.Chain){var f=gate.gameObject.AddComponent<ProgressPropFeedback>();f.gate=gate;Lock(f,gate.transform,new Vector3(0,1,-.1f));}
                // The main entrance doors are the exit now (PlaytestFixSetup); there is no separate escape plate to dress.
            }
            var box=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="ConfiscatedBox");
            if(box!=null&&box.GetComponent<ProgressPropFeedback>()==null)
            {
                var f=box.gameObject.AddComponent<ProgressPropFeedback>();var lid=Group("Box lid hinge",box,new Vector3(0,.405f,.225f));f.boxLid=lid;
                Box("Cardboard lid",lid,new Vector3(0,0,-.225f),new Vector3(.61f,.018f,.46f),"M_Chapter_Cardboard");lid.localRotation=Quaternion.Euler(-45,0,0);
            }
            var additions=Object.FindObjectsByType<ProgressPropFeedback>(FindObjectsSortMode.None).SelectMany(f=>new[]{f.padlock,f.boxLid}).Where(t=>t!=null);
            var surfaces=root.GetComponentsInChildren<MeshRenderer>().Concat(additions.SelectMany(t=>t.GetComponentsInChildren<MeshRenderer>())).Distinct();
            foreach(var renderer in surfaces)if(renderer.GetComponent<MeshFilter>()!=null&&renderer.sharedMaterial!=null&&renderer.sharedMaterial.shader.name=="Confiscated/Pencil Surface")IllustratedArtSetup.Tiled(renderer,renderer.sharedMaterial,"chase",1);
            if(dinner!=null)TrolleyArtSetup.Apply(dinner);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
        static void Wet(Transform parent,Vector3 position,Vector2 size)
        {
            var root=Group("Marked wet floor",parent,position);root.gameObject.AddComponent<WetFloorHazard>().size=size;
            var puddle=GameObject.CreatePrimitive(PrimitiveType.Cylinder);puddle.name="Pencil blue damp patch";puddle.transform.SetParent(root,false);puddle.transform.localPosition=Vector3.up*.008f;puddle.transform.localScale=new Vector3(size.x,.006f,size.y);puddle.GetComponent<Renderer>().sharedMaterial=Mat("M_Chapter_Grey");Object.DestroyImmediate(puddle.GetComponent<Collider>());
            var sign=Group("Yellow caution stand",root,new Vector3(-size.x*.5f-.18f,0,0));
            foreach(float a in new[]{-18f,18f}){var board=Box("Yellow folding leaf",sign,new Vector3(0,.32f,0),new Vector3(.38f,.64f,.035f),"M_Chapter_Brass");board.localRotation=Quaternion.Euler(a,0,0);}
            var text=Group("Wet floor warning",sign,new Vector3(0,.38f,-.12f)).gameObject.AddComponent<TextMesh>();text.font=SchoolTypography.Font;text.text="CAUTION\nWET FLOOR";text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.characterSize=.035f;text.fontSize=48;text.color=new Color(.1f,.12f,.1f);text.GetComponent<Renderer>().sharedMaterial=SchoolTypography.Font.material;
            var bucket=GameObject.CreatePrimitive(PrimitiveType.Cylinder);bucket.name="Mop bucket";bucket.transform.SetParent(sign,false);bucket.transform.localPosition=new Vector3(-.36f,.17f,0);bucket.transform.localScale=new Vector3(.32f,.17f,.32f);bucket.GetComponent<Renderer>().sharedMaterial=Mat("M_Chapter_Green");Object.DestroyImmediate(bucket.GetComponent<Collider>());
            var mop=Box("Mop handle",sign,new Vector3(-.36f,.65f,0),new Vector3(.025f,1.15f,.025f),"M_Wood_Desk");mop.localRotation=Quaternion.Euler(0,0,8);
        }
    }
}

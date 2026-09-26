using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// The caretaker's office was a 14 x 15 m hall with a desk in it. A partition cuts it to a cramped front office by
    /// the door (x -40.8..-35.7, z 75.6..84.2): a plywood wall with a wired-glass window onto a dusty plant room (a
    /// painted backdrop lit by its own bare bulb and boiler glow), and a plain wall to the north. The furniture moves in
    /// and the Tripo office props dress it. Repeatable; chained last in SchoolRunSetup.Build so it wins over the
    /// builders that place the old layout. Art: Docs/ASSET_REQUESTS_Astra_Office.md.
    /// </summary>
    public static class OfficeRefitSetup
    {
        public const string RootName="Office refit";
        const string Textures="Assets/Art/Textures/",Models="Assets/Art/Models/SchoolProps/Office/",Materials="Assets/Art/Materials/";
        // Front office interior, world space.
        public const float West=-40.8f,East=-35.7f,South=75.6f,North=84.2f,Ceiling=3.05f;
        public static Bounds FrontOffice=>new Bounds(new Vector3((West+East)/2,1.5f,(South+North)/2),new Vector3(East-West+.4f,6,North-South+.4f));
        // The plant room: everything of the old office behind the partition.
        static readonly Bounds OldOffice=new Bounds(new Vector3(-42.89f,1.5f,83.11f),new Vector3(14.74f,6f,15.41f));
        // Floor to ceiling, so the painted bulb (and its light) sits below the real ceiling, and close enough behind the glass
        // that the window never shows past its edges from inside the office (stretched ~25% wide to span the partition).
        const float BackdropX=-42.6f,BackdropWidth=8.4f,BackdropHeight=3.4f,BackdropBottom=-.3f;

        [MenuItem("Confiscated/School Run/Apply Office Refit")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing.");
            ApplyToScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            Debug.Log("[OfficeRefit] Office partitioned, furnished and the plant room lit.");
        }

        public static void ApplyToScene()
        {
            var run=Object.FindFirstObjectByType<SchoolRunController>();
            var game=Object.FindFirstObjectByType<GameManager>();
            if(run==null||game==null||game.officeMission==null)throw new InvalidOperationException("Open the SchoolLayout scene first.");
            var old=run.transform.Find(RootName);if(old!=null)Object.DestroyImmediate(old.gameObject);
            var root=new GameObject(RootName).transform;root.SetParent(run.transform,false);

            Partitions(root);
            PlantRoom(root);
            MoveFurniture();
            Props(root);
            RetireOldFixtures();

            game.officeMission.officeBounds=FrontOffice;EditorUtility.SetDirty(game.officeMission);
            // The caretaker drops the confiscated phone at the desk, which has moved: the drop marker the opening checks,
            // and every staff route stop that walks to it, follow the desk (the old spot is now behind the partition).
            var box=run.period.phonePickup.transform.position;Vector3 drop=new Vector3(box.x,0,box.z+1.1f);
            var period=run.period;Vector3 oldDrop=period.officeDrop.position;
            period.officeDrop.position=drop;EditorUtility.SetDirty(period.officeDrop);
            foreach(var staff in Object.FindObjectsByType<CaretakerAI>(FindObjectsInactive.Include))
            {
                foreach(var p in staff.patrol.Where(p=>p.point!=null&&(p.point.name=="Deposit phone"||Flat(p.point.position-oldDrop)<.6f||!FrontOffice.Contains(p.point.position+Vector3.up)&&OldOffice.Contains(p.point.position+Vector3.up))))
                {p.point.position=drop;p.faceDirection=Vector3.back;EditorUtility.SetDirty(p.point);}
                EditorUtility.SetDirty(staff);
            }
        }

        static void Partitions(Transform root)
        {
            var wallMaterial=GameObject.Find("Wall_141").GetComponent<Renderer>().sharedMaterial;
            // West: the plywood partition, window centred, seen from the office. One texture repeat is ~6.1 m wide.
            float length=North-South;
            var west=Quad("Partition - plant room window",root,new Vector3(West,Ceiling/2,(South+North)/2),Quaternion.LookRotation(Vector3.left),new Vector2(length,Ceiling),Artwork("M_Office_Partition","T_Office_Partition",true,new Vector2(1.4f,1),new Vector2(-.2f,0)));
            west.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.On;
            Solid(root,"Partition - plant room window collision",new Vector3(West-.05f,Ceiling/2,(South+North)/2),new Vector3(.1f,Ceiling,length),null);
            // North: a plain school wall.
            Solid(root,"Partition - north wall",new Vector3((West+East)/2,Ceiling/2,North+.075f),new Vector3(East-West+.1f,Ceiling,.15f),wallMaterial);
        }

        static void PlantRoom(Transform root)
        {
            var room=new GameObject("Plant room").transform;room.SetParent(root,false);
            float centreZ=(South+North)/2,bottom=BackdropBottom,top=bottom+BackdropHeight,leftZ=centreZ-BackdropWidth/2;
            Quad("Plant room backdrop",room,new Vector3(BackdropX,bottom+BackdropHeight/2,centreZ),Quaternion.LookRotation(Vector3.left),new Vector2(BackdropWidth,BackdropHeight),Artwork("M_Office_PlantRoom","T_Office_PlantRoom_View",false,Vector2.one,Vector2.zero));
            // Lights where the painting has them (u across from the viewer's left, v down from the top).
            Vector3 At(float u,float v)=>new Vector3(BackdropX+.35f,top-v*BackdropHeight,leftZ+u*BackdropWidth);
            Lamp(room,"Bare bulb",At(.51f,.15f),new Color(1f,.8f,.55f),1.6f,5.5f,LightFlicker.Mode.Bulb,.2f,LightShadows.Soft);
            Lamp(room,"Boiler firebox glow",At(.30f,.72f),new Color(1f,.45f,.15f),.9f,2.6f,LightFlicker.Mode.Glow,.45f,LightShadows.None);
        }

        static void Lamp(Transform parent,string name,Vector3 at,Color colour,float intensity,float range,LightFlicker.Mode mode,float depth,LightShadows shadows)
        {
            var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.position=at;
            var l=g.AddComponent<Light>();l.type=LightType.Point;l.color=colour;l.intensity=intensity;l.range=range;l.shadows=shadows;
            var f=g.AddComponent<LightFlicker>();f.mode=mode;f.baseIntensity=intensity;f.depth=depth;
        }

        /// <summary>Old furniture moves into the front office. Absolute targets, so re-running changes nothing.</summary>
        static void MoveFurniture()
        {
            // Desk set: desk, its clutter, the phone box, the bolt cutters and the chair keep their layout, against the south wall.
            var desk=Find("Desk");Vector3 delta=new Vector3(-38.6f,0,76.45f)-desk.position;
            var cutters=Object.FindObjectsByType<AccessToolPickup>(FindObjectsInactive.Include).First(t=>t.tool==AccessToolPickup.Tool.BoltCutters).transform;
            foreach(var t in new[]{desk,Find("DeskDetails"),Find("ConfiscatedBox"),cutters})Move(t,t.position+delta);
            // Chair pushed to the desk's west end, clear of where you stand to take the phone and the cutters.
            Move(Find("Chair_Office"),new Vector3(-39.95f,0,77.15f));
            Move(Find("FilingCabinet"),new Vector3(-36.25f,0,75.95f));
            Move(Find("OfficeClock"),new Vector3(-37.8f,2.32f,75.66f));
            var shelving=Find("MaintenanceShelving");Move(shelving,new Vector3(-38.9f,0,83.93f));shelving.rotation=Quaternion.Euler(0,0,0);
            Move(Find("Noticeboard_Office"),new Vector3(-36.7f,1.72f,North-.1f));
            Move(Find("ConfiscationPolicy"),new Vector3(-36.7f,1.72f,North-.13f));
        }
        static float Flat(Vector3 v){v.y=0;return v.magnitude;}
        static Transform Find(string name){var g=GameObject.Find(name);if(g==null)throw new InvalidOperationException(name+" is missing from the office.");return g.transform;}
        static void Move(Transform t,Vector3 to){t.position=to;EditorUtility.SetDirty(t);PrefabUtility.RecordPrefabInstancePropertyModifications(t);}

        static void Props(Transform root)
        {
            var props=new GameObject("Office props").transform;props.SetParent(root,false);
            // Workbench against the partition, pegboard over it, beside the window.
            Prop(props,"Workbench","OfficeToolTable",new Vector3(West+.44f,0,82.7f),0,1.5f,true);
            Prop(props,"Tool pegboard","OfficeToolBoard",new Vector3(West+.06f,1.2f,82.7f),180,1.0f,false);
            // Lost property under the window, key cabinet on the wall beside the door.
            Prop(props,"Lost property crate","OfficeLostProperty",new Vector3(West+.4f,0,78.9f),0,.65f,true);
            Prop(props,"Key cabinet","OfficeKeybox",new Vector3(East-.13f,1.25f,78.7f),0,.55f,false);
        }
        static void Prop(Transform parent,string name,string model,Vector3 at,float yaw,float size,bool solid)
        {
            var visual=new GameObject(name).transform;visual.SetParent(parent,false);visual.position=at;
            if(!PickupModelSetup.Place(visual,model,size,0,yaw,Models)){Debug.LogWarning("[OfficeRefit] Missing model or texture: "+model);return;}
            // Furniture follows the room's light, unlike collectibles, which stay unlit so they read from across a room.
            foreach(var r in visual.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;SchoolLightingSetup.ConfigureArtworkMaterial(r.sharedMaterial,false);}
            if(!solid)return;
            var rs=visual.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            var box=visual.gameObject.AddComponent<BoxCollider>();box.center=visual.InverseTransformPoint(b.center);box.size=b.size;
            var nav=visual.gameObject.AddComponent<NavMeshObstacle>();nav.shape=NavMeshObstacleShape.Box;nav.center=box.center;nav.size=box.size;nav.carving=true;
        }

        /// <summary>Ceiling fixtures now inside the plant room would light it like an office; only the front one stays.</summary>
        static void RetireOldFixtures()
        {
            var front=FrontOffice;
            foreach(var fixture in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t=>t.name.StartsWith("P_CeilingLight")&&OldOffice.Contains(t.position)&&!front.Contains(t.position)))
            {fixture.gameObject.SetActive(false);EditorUtility.SetDirty(fixture.gameObject);}
        }

        static GameObject Quad(string name,Transform parent,Vector3 at,Quaternion facing,Vector2 size,Material material)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Quad);g.name=name;g.transform.SetParent(parent,false);
            g.transform.SetPositionAndRotation(at,facing);g.transform.localScale=new Vector3(size.x,size.y,1);
            Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<MeshRenderer>().sharedMaterial=material;return g;
        }
        static void Solid(Transform parent,string name,Vector3 at,Vector3 size,Material material)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.position=at;g.transform.localScale=size;
            if(material!=null)g.GetComponent<MeshRenderer>().sharedMaterial=material;else Object.DestroyImmediate(g.GetComponent<MeshRenderer>());
            var nav=g.AddComponent<NavMeshObstacle>();nav.shape=NavMeshObstacleShape.Box;nav.center=Vector3.zero;nav.size=Vector3.one;nav.carving=true;
        }
        static Material Artwork(string name,string texture,bool cutout,Vector2 tiling,Vector2 offset)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(Textures+texture+".png");
            if(importer==null)throw new InvalidOperationException("Missing "+Textures+texture+".png");
            if(importer.alphaIsTransparency!=cutout||importer.wrapMode!=(cutout?TextureWrapMode.Repeat:TextureWrapMode.Clamp))
            {importer.alphaIsTransparency=cutout;importer.wrapMode=cutout?TextureWrapMode.Repeat:TextureWrapMode.Clamp;importer.SaveAndReimport();}
            string path=Materials+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Textures+texture+".png"));m.SetColor("_BaseColor",Color.white);
            m.SetTextureScale("_BaseMap",tiling);m.SetTextureOffset("_BaseMap",offset);
            SchoolLightingSetup.ConfigureArtworkMaterial(m,cutout);return m;
        }
    }
}

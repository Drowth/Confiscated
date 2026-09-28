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
    /// The school office: the closed "South classroom" (CLASSROOM 4, next to the main entrance and the store) opens for the
    /// run as the office. A reception counter with a glass screen and a serving hatch splits a small waiting area by the
    /// door from the back office, with a staff gap at its east end. The store key lives in the key cabinet on the back wall:
    /// ten tagged keys, the STORE hook shuffled each run (KeyCabinet), a wrong key jangles. Replaces the old floating store
    /// key on the EQUIPMENT desk. Primitives and existing props for now. Chained into SchoolRunSetup.Build.
    /// </summary>
    public static class SchoolOfficeSetup
    {
        public const string RootName="School office",DoorName="South classroom north";
        // Inner faces of the room walls; the door gap is x 13.01..14.77 on the north wall.
        public const float West=4.41f,East=19.81f,South=3.63f,North=17.26f;
        public const float CounterZ=12.6f,GapWest=15.2f,GapEast=16.4f,CabinetX=12f,CabinetY=1.45f;
        public static readonly Bounds Interior=new Bounds(new Vector3((West+East)/2,1.5f,(South+North)/2),new Vector3(East-West,4f,North-South));
        static Transform root;
        static Material wood,paper,ink,grey,brass,teal,trim,glass,screen,blue,tag;

        [MenuItem("Confiscated/School Run/Build School Office")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing.");
            ApplyToScene();DiningHallSetup.Rebake();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            Debug.Log("[SchoolOffice] Office open, furnished, key cabinet installed; navigation rebaked.");
        }

        public static void ApplyToScene()
        {
            var run=Object.FindFirstObjectByType<SchoolRunController>();
            if(run==null)throw new InvalidOperationException("Open the SchoolLayout scene first.");
            var old=run.transform.Find(RootName);if(old!=null)Object.DestroyImmediate(old.gameObject);
            root=new GameObject(RootName).transform;root.SetParent(run.transform,false);
            wood=M("M_Wood_Desk");paper=M("M_Chapter_Paper");ink=M("M_Chapter_Ink");grey=M("M_Chapter_Grey");brass=M("M_Chapter_Brass");
            teal=M("M_Entrance_Teal");trim=M("M_Painted_Trim_Pencil");blue=Tint("M_Office_Carpet",grey,new Color(.36f,.42f,.52f));
            glass=Glass();tag=Tint("M_Office_KeyTag",paper,new Color(1f,.95f,.8f));screen=Tint("M_Office_Screen",ink,new Color(.16f,.22f,.26f));

            OpenDoor(run);RetireEquipmentKey();
            Waiting();Counter();BackOffice();Cabinet();CorridorHatch();Lighting();
            // Tripo models over the corridor hatch and photocopier; the furniture pass ran before this office was rebuilt.
            FurnitureModelSetup.ApplyToScene(FurnitureModelSetup.SchoolOfficeModels);
            foreach(var sign in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.text=="CLASSROOM 4"))
            {sign.text="OFFICE";EditorUtility.SetDirty(sign);}
        }

        static void OpenDoor(SchoolRunController run)
        {
            var door=Object.FindObjectsByType<OfficeDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(d=>d.name==DoorName);
            if(door==null)throw new InvalidOperationException("Office door "+DoorName+" is missing.");
            // Shut while the errand is on, an ordinary door for the run.
            door.closedForRun=false;door.closedDuringLessons=true;door.startsUnlocked=true;door.runRequiredLevel=0;
            EditorUtility.SetDirty(door);PrefabUtility.RecordPrefabInstancePropertyModifications(door);
            var blocker=run.transform.Find("Door blocker - "+DoorName);if(blocker!=null)Object.DestroyImmediate(blocker.gameObject);
            foreach(var t in door.GetComponentsInChildren<Transform>(true).Where(t=>t.name==ClosedDoorSignageSetup.GroupName||t.name=="School door notice").ToArray())
                Object.DestroyImmediate(t.gameObject);
        }

        /// <summary>The store key used to float over the EQUIPMENT desk; it lives in the cabinet now.</summary>
        static void RetireEquipmentKey()
        {
            foreach(var tool in Object.FindObjectsByType<AccessToolPickup>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.tool==AccessToolPickup.Tool.StoreKey).ToArray())
                Object.DestroyImmediate(tool.gameObject);
        }

        // ---------- Waiting area (north strip, by the door) ----------
        static void Waiting()
        {
            var g=Group("Waiting area");
            Box(g,"Waiting carpet",new Vector3(9.2f,.006f,15.1f),new Vector3(8.6f,.012f,3.6f),blue,false);
            for(int i=0;i<5;i++)Prefab("Assets/Prefabs/Props/P_Chair.prefab",g,"Waiting chair "+(i+1),new Vector3(6.1f+i*.95f,0,16.72f),0);
            Solid(g,"Low table",new Vector3(8.0f,.22f,15.2f),new Vector3(1.2f,.44f,.6f),wood);
            for(int i=0;i<3;i++){var mag=Box(g,"Magazine",new Vector3(7.7f+i*.28f,.452f,15.15f+(i%2)*.08f),new Vector3(.21f,.008f,.28f),i==1?teal:paper,false);mag.transform.rotation=Quaternion.Euler(0,-12+i*14,0);}
            Prefab("Assets/Prefabs/Props/P_Noticeboard.prefab",g,"Office noticeboard",new Vector3(8.5f,1.65f,North-.03f),180);
            Prefab("Assets/Prefabs/Hallway/P_Hall_TrophyCabinet_Generated.prefab",g,"Office trophy cabinet",new Vector3(18.3f,0,North-.39f),90); // as in the north hall: yaw 90 puts its back (0.78 m deep) on a north wall
            Prefab("Assets/Prefabs/Hallway/P_Hall_LitterBin.prefab",g,"Waiting bin",new Vector3(11.8f,0,16.8f),0);
            // The reception counter model paints its own wait sign, but the generated texture misspells it ("PLEISS WNT
            // TO BE SEEN"); cover it with a correctly-spelled plaque in the same spot instead of the box counter's own.
            bool counterModel=PickupModelSetup.Has("OfficeReceptionCounter",FurnitureModelSetup.Folder);
            Plaque(g,"PLEASE WAIT\nTO BE SEEN",counterModel?new Vector3(14.15f,.66f,CounterZ+.4f):new Vector3(6.0f,.72f,CounterZ+.325f),new Vector2(counterModel?1.3f:.95f,counterModel?.4f:.36f),180,.011f);
        }

        /// <summary>
        /// The layout's light grid gives the office four fittings with a 7.8 m dark strip between them; two more go down the
        /// middle, set like the others. A small warm lamp lights the trophies. Dark Mode and blackouts gather every light.
        /// </summary>
        static void Lighting()
        {
            var g=Group("Office lighting");
            var like=Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Where(t=>t.name=="P_CeilingLight"&&!t.IsChildOf(root)&&Interior.Contains(t.position+Vector3.up))
                .Select(t=>t.GetComponentInChildren<Light>()).FirstOrDefault(l=>l!=null);
            foreach(float z in new[]{7.0f,13.9f})
            {
                var fitting=Prefab("Assets/Prefabs/Modular/P_CeilingLight.prefab",g,"P_CeilingLight",new Vector3((West+East)/2,0,z),0);
                var l=fitting.GetComponentInChildren<Light>();
                if(l!=null&&like!=null){l.intensity=like.intensity;l.range=like.range;l.color=like.color;l.shadows=like.shadows;l.transform.localPosition=like.transform.localPosition;EditorUtility.SetDirty(l);PrefabUtility.RecordPrefabInstancePropertyModifications(l);PrefabUtility.RecordPrefabInstancePropertyModifications(l.transform);}
            }
            var trophies=new GameObject("Trophy cabinet light").AddComponent<Light>();trophies.transform.SetParent(g,false);
            trophies.transform.position=new Vector3(18.3f,1.45f,North-.35f);
            trophies.type=LightType.Point;trophies.color=new Color(1f,.88f,.66f);trophies.intensity=.9f;trophies.range=1.1f;trophies.shadows=LightShadows.None;
        }

        // ---------- Reception counter with a glass screen and a serving hatch ----------
        static void Counter()
        {
            var g=Group("Reception counter");
            Segment(g,"West counter",West,GapWest);Segment(g,"East counter",GapEast,East);
            // Glass screen over the west run, open at the hatch.
            const float hatchWest=7.6f,hatchEast=8.8f,top=2.05f;
            foreach(var (a,b) in new[]{(West,hatchWest),(hatchEast,GapWest)})
            {
                Box(g,"Screen glass",new Vector3((a+b)/2,(1.08f+top)/2,CounterZ),new Vector3(b-a,top-1.08f,.02f),glass,true).GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            }
            foreach(float x in new[]{West+.03f,hatchWest,hatchEast,GapWest-.03f})Box(g,"Screen post",new Vector3(x,(1.08f+top)/2,CounterZ),new Vector3(.05f,top-1.08f,.06f),trim,false);
            Box(g,"Screen top rail",new Vector3((West+GapWest)/2,top+.025f,CounterZ),new Vector3(GapWest-West,.05f,.06f),trim,false);
            Box(g,"Hatch lintel",new Vector3((hatchWest+hatchEast)/2,1.72f,CounterZ),new Vector3(hatchEast-hatchWest,.04f,.06f),trim,false);
            Box(g,"Hatch glass",new Vector3((hatchWest+hatchEast)/2,(1.74f+top)/2,CounterZ),new Vector3(hatchEast-hatchWest,top-1.74f,.02f),glass,true);
            Plaque(g,"RECEPTION",new Vector3((hatchWest+hatchEast)/2,2.25f,CounterZ),new Vector2(1.3f,.28f),180,.02f);
            // Bell and sign-in book at the hatch.
            Box(g,"Bell base",new Vector3(8.4f,1.1f,CounterZ+.12f),new Vector3(.1f,.02f,.1f),ink,false);
            var bell=GameObject.CreatePrimitive(PrimitiveType.Sphere);bell.name="Desk bell";bell.transform.SetParent(g,false);bell.transform.position=new Vector3(8.4f,1.13f,CounterZ+.12f);bell.transform.localScale=new Vector3(.085f,.05f,.085f);
            Object.DestroyImmediate(bell.GetComponent<Collider>());bell.GetComponent<MeshRenderer>().sharedMaterial=brass;
            Box(g,"Sign-in book",new Vector3(8.0f,1.1f,CounterZ+.08f),new Vector3(.4f,.02f,.28f),paper,false);
            SweetJar(g,new Vector3(9.35f,1.09f,CounterZ+.2f));
            Plaque(g,"STAFF\nONLY",new Vector3(GapWest-.3f,1.6f,CounterZ+.025f),new Vector2(.34f,.26f),180,.009f);
            // Staff side: a monitor at each desk position behind the screen.
            Box(g,"Counter monitor",new Vector3(6.0f,1.28f,CounterZ-.12f),new Vector3(.46f,.32f,.04f),screen,false);
            Box(g,"Counter monitor",new Vector3(11.0f,1.28f,CounterZ-.12f),new Vector3(.46f,.32f,.04f),screen,false);
        }
        /// <summary>A jar of sweets on the public side of the counter: one bag per run, for the chatterbox (SweetJar).</summary>
        static void SweetJar(Transform parent,Vector3 at)
        {
            var jar=new GameObject("Sweet jar").transform;jar.SetParent(parent,false);jar.position=at;
            var pick=jar.gameObject.AddComponent<global::Confiscated.SweetJar>();
            var box=jar.gameObject.AddComponent<BoxCollider>();box.center=new Vector3(0,.15f,0);box.size=new Vector3(.24f,.32f,.24f);
            Cylinder(jar,"Jar glass",new Vector3(0,.13f,0),new Vector3(.17f,.13f,.17f),glass);
            Cylinder(jar,"Jar lid",new Vector3(0,.275f,0),new Vector3(.15f,.02f,.15f),Tint("M_Office_JarLid",paper,new Color(.75f,.18f,.16f)));
            var contents=new GameObject("Sweets").transform;contents.SetParent(jar,false);
            var colours=new[]{Tint("M_Office_Sweet_Red",paper,new Color(.9f,.2f,.25f)),Tint("M_Office_Sweet_Yellow",paper,new Color(.95f,.8f,.2f)),Tint("M_Office_Sweet_Green",paper,new Color(.35f,.75f,.3f)),Tint("M_Office_Sweet_Purple",paper,new Color(.55f,.3f,.75f))};
            var rng=new System.Random(4);
            for(int i=0;i<22;i++)
            {
                float a=(float)rng.NextDouble()*Mathf.PI*2,r=(float)rng.NextDouble()*.055f,y=.03f+i*.0075f;
                var sweet=GameObject.CreatePrimitive(PrimitiveType.Sphere);sweet.name="Sweet";sweet.transform.SetParent(contents,false);
                sweet.transform.localPosition=new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);sweet.transform.localScale=new Vector3(.04f,.03f,.04f);
                Object.DestroyImmediate(sweet.GetComponent<Collider>());sweet.GetComponent<MeshRenderer>().sharedMaterial=colours[i%colours.Length];
            }
            pick.contents=contents.gameObject;EditorUtility.SetDirty(pick);
            Plaque(jar,"SWEETS\nfor good\nbehaviour",at+new Vector3(0,.13f,.09f),new Vector2(.12f,.09f),180,.0035f,true);
        }
        static void Cylinder(Transform parent,string name,Vector3 local,Vector3 size,Material m)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=local;g.transform.localScale=size;
            Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<MeshRenderer>().sharedMaterial=m;
        }
        static void Segment(Transform g,string name,float a,float b)
        {
            Solid(g,name,new Vector3((a+b)/2,.525f,CounterZ),new Vector3(b-a,1.05f,.62f),teal);
            Box(g,name+" top",new Vector3((a+b)/2,1.07f,CounterZ),new Vector3(b-a+.04f,.04f,.7f),wood,false);
        }

        // ---------- Back office ----------
        static void BackOffice()
        {
            var g=Group("Back office");
            foreach(float x in new[]{8.2f,11.6f})
            {
                Prefab("Assets/Prefabs/Props/P_Desk.prefab",g,"Office desk",new Vector3(x,0,9.4f),0);
                Prefab("Assets/Prefabs/Props/P_Chair.prefab",g,"Office chair",new Vector3(x,0,8.68f),180);
                Box(g,"Desk monitor",new Vector3(x,.98f,9.55f),new Vector3(.42f,.28f,.035f),screen,false);
                Box(g,"Monitor stand",new Vector3(x,.8f,9.6f),new Vector3(.05f,.1f,.05f),ink,false);
                Box(g,"Keyboard",new Vector3(x,.77f,9.25f),new Vector3(.36f,.015f,.12f),ink,false);
                Box(g,"Paper tray",new Vector3(x+.34f,.78f,9.35f),new Vector3(.24f,.04f,.3f),paper,false);
            }
            // Filing cabinets down the west wall, grouped so FurnitureModelSetup can swap the whole run for one bank-of-4 model.
            var bank=new GameObject("Filing cabinet bank").transform;bank.SetParent(g,false);
            for(int i=0;i<4;i++)
            {
                var at=new Vector3(West+.3f,.66f,4.6f+i*.62f);Solid(bank,"Filing cabinet",at,new Vector3(.58f,1.32f,.58f),grey);
                for(int d=0;d<4;d++){Box(bank,"Drawer line",new Vector3(at.x+.292f,.33f+d*.32f,at.z),new Vector3(.005f,.008f,.52f),ink,false);Box(bank,"Drawer handle",new Vector3(at.x+.3f,.2f+d*.32f,at.z),new Vector3(.02f,.025f,.14f),brass,false);}
            }
            // Photocopier and a stack of newsletters against the east wall.
            Solid(g,"Photocopier",new Vector3(East-.42f,.5f,7.2f),new Vector3(.8f,1.0f,.7f),grey);
            Box(g,"Photocopier lid",new Vector3(East-.42f,1.02f,7.2f),new Vector3(.74f,.04f,.62f),ink,false);
            Box(g,"Newsletter stack",new Vector3(East-.42f,1.08f,7.05f),new Vector3(.21f,.08f,.3f),paper,false);
            // Staff pigeonholes on the east wall, a meeting table in the south-east corner.
            var holes=new Vector3(East-.2f,1.3f,10.3f);Solid(g,"Pigeonholes",new Vector3(holes.x,.9f,holes.z),new Vector3(.4f,1.8f,1.6f),wood);
            for(int r=0;r<4;r++)for(int k=0;k<4;k++)
            {
                Box(g,"Pigeonhole",new Vector3(holes.x-.2f,.55f+r*.36f,holes.z-.6f+k*.4f),new Vector3(.01f,.3f,.34f),ink,false);
                if((r+k)%3!=0)Box(g,"Post",new Vector3(holes.x-.15f,.46f+r*.36f,holes.z-.6f+k*.4f),new Vector3(.1f,.08f,.24f),paper,false);
            }
            Solid(g,"Meeting table",new Vector3(16.6f,.37f,5.4f),new Vector3(1.6f,.74f,.9f),wood);
            Prefab("Assets/Prefabs/Props/P_Chair.prefab",g,"Meeting chair",new Vector3(16.1f,0,6.1f),0);
            Prefab("Assets/Prefabs/Props/P_Chair.prefab",g,"Meeting chair",new Vector3(17.1f,0,6.1f),0);
            Box(g,"Mug",new Vector3(16.3f,.79f,5.3f),new Vector3(.08f,.1f,.08f),teal,false);
            Box(g,"Register folder",new Vector3(16.9f,.755f,5.5f),new Vector3(.32f,.03f,.24f),Tint("M_Office_Folder",paper,new Color(.8f,.3f,.25f)),false);
            Prefab("Assets/Prefabs/Hallway/P_Hall_Clock.prefab",g,"Office clock",new Vector3(16.2f,2.35f,South+.03f),0);
            Plaque(g,"FIRE DRILL\nMeet on the yard",new Vector3(17.6f,1.62f,South+.03f),new Vector2(.8f,.44f),0,.011f,true);
        }

        // ---------- Key cabinet on the back (south) wall ----------
        static void Cabinet()
        {
            var c=new GameObject("Key cabinet").transform;c.SetParent(root,false);c.position=new Vector3(CabinetX,CabinetY,South);c.rotation=Quaternion.identity;
            var cabinet=c.gameObject.AddComponent<KeyCabinet>();
            Local(c,"Back board",new Vector3(0,0,.02f),new Vector3(1.4f,.9f,.04f),wood,true);
            foreach(int s in new[]{-1,1})
            {
                Local(c,"Side",new Vector3(s*.72f,0,.08f),new Vector3(.04f,.94f,.16f),wood,false);
                Local(c,"Rail",new Vector3(0,s*.47f,.08f),new Vector3(1.48f,.04f,.16f),wood,false);
                // Doors hang wide open against the wall.
                Local(c,"Open door",new Vector3(s*1.1f,0,.02f),new Vector3(.7f,.9f,.03f),wood,false);
                Local(c,"Door knob",new Vector3(s*1.38f,0,.045f),new Vector3(.03f,.03f,.03f),brass,false);
            }
            Plaque(c,"KEYS",c.position+new Vector3(0,.62f,.02f),new Vector2(.5f,.18f),180,.014f);
            // A strip light over the cabinet so the tags can be read.
            Local(c,"Strip light",new Vector3(0,.85f,.1f),new Vector3(1.1f,.05f,.1f),Tint("M_Office_StripLight",paper,new Color(1f,.98f,.9f)),false);
            var lamp=new GameObject("Cabinet light").AddComponent<Light>();lamp.transform.SetParent(c,false);lamp.transform.localPosition=new Vector3(0,.7f,.55f);
            lamp.type=LightType.Point;lamp.range=2.6f;lamp.intensity=1.6f;lamp.color=new Color(1f,.95f,.85f);lamp.shadows=LightShadows.None;
            var hooks=new KeyHook[10];
            for(int i=0;i<10;i++)
            {
                int col=i%5,row=i/5;
                var h=new GameObject("Key hook "+(i+1)).transform;h.SetParent(c,false);h.localPosition=new Vector3(-.48f+col*.24f,.22f-row*.42f,.06f);
                Local(h,"Peg",new Vector3(0,0,-.01f),new Vector3(.014f,.014f,.05f),brass,false);
                var key=new GameObject("Key").transform;key.SetParent(h,false);key.localPosition=new Vector3(0,0,.012f);
                var ring=GameObject.CreatePrimitive(PrimitiveType.Cylinder);ring.name="Ring";ring.transform.SetParent(key,false);
                ring.transform.localPosition=new Vector3(0,-.03f,0);ring.transform.localRotation=Quaternion.Euler(90,0,0);ring.transform.localScale=new Vector3(.045f,.003f,.045f);
                Object.DestroyImmediate(ring.GetComponent<Collider>());ring.GetComponent<MeshRenderer>().sharedMaterial=brass;
                Local(key,"Shaft",new Vector3(0,-.09f,0),new Vector3(.012f,.075f,.006f),brass,false);
                Local(key,"Bit",new Vector3(.011f,-.118f,0),new Vector3(.02f,.018f,.006f),brass,false);
                Local(key,"String",new Vector3(0,-.14f,.004f),new Vector3(.003f,.03f,.003f),ink,false);
                Local(key,"Tag",new Vector3(0,-.182f,.005f),new Vector3(.17f,.065f,.004f),tag,false);
                var text=Text(key,KeyCabinet.Labels[i],new Vector3(0,-.182f,.0075f),.0045f);
                var hook=h.gameObject.AddComponent<KeyHook>();hook.cabinet=cabinet;hook.key=key;hook.tagText=text;hook.holdSeconds=.6f;
                var box=h.gameObject.AddComponent<BoxCollider>();box.center=new Vector3(0,-.11f,.02f);box.size=new Vector3(.21f,.26f,.06f);
                hooks[i]=hook;EditorUtility.SetDirty(hook);
            }
            cabinet.hooks=hooks;CabinetModel(c,hooks);KeyModels(cabinet);EditorUtility.SetDirty(cabinet);
        }

        const string CabinetModelName="OfficeKeyCabinet";
        const float CabinetHeight=.96f;
        /// <summary>
        /// The Tripo cabinet (doors open, ten pegs in two rows of five) over the box-built one, whose parts keep their
        /// colliders but stop drawing. The hooks move onto the model's own pegs, found by ray-casting its front for bumps
        /// standing proud of the back panel. If the pegs can't be read, the hooks stay where they were.
        /// </summary>
        static void CabinetModel(Transform c,KeyHook[] hooks)
        {
            if(!PickupModelSetup.Has(CabinetModelName,FurnitureModelSetup.Folder))return;
            var visual=new GameObject("Cabinet model").transform;visual.SetParent(c,false);
            // Front (open face) on the model's -X: +90 turns it to the room.
            if(!PickupModelSetup.Place(visual,CabinetModelName,1,0,90,FurnitureModelSetup.Folder)){Object.DestroyImmediate(visual.gameObject);return;}
            var holder=visual.GetChild(0);var rs=holder.GetComponentsInChildren<Renderer>();
            Bounds B(){var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
            holder.localScale*=CabinetHeight/B().size.y;
            // Back on the wall, centred on the cabinet's middle.
            var b0=B();holder.position+=new Vector3(c.position.x-b0.center.x,c.position.y-b0.center.y,South-b0.min.z);
            foreach(var r in rs){r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;SchoolLightingSetup.ConfigureArtworkMaterial(r.sharedMaterial,false);}
            foreach(var r in c.GetComponentsInChildren<Renderer>(true).Where(r=>new[]{"Back board","Side","Rail","Open door","Door knob","Peg"}.Contains(r.name))){r.enabled=false;EditorUtility.SetDirty(r);}

            // Depth map of the front, 5 mm grid, local to the cabinet (x across, y up, z out of the wall).
            var colliders=holder.GetComponentsInChildren<MeshFilter>().Select(f=>{var m=f.gameObject.AddComponent<MeshCollider>();m.sharedMesh=f.sharedMesh;return m;}).ToList();
            const float step=.005f;int nx=Mathf.CeilToInt(1.6f/step),ny=Mathf.CeilToInt(CabinetHeight/step);
            var depth=new float[nx,ny];
            for(int i=0;i<nx;i++)for(int j=0;j<ny;j++)
            {
                var from=c.TransformPoint(new Vector3(-.8f+i*step,-CabinetHeight/2+j*step,1));float best=float.NaN;
                foreach(var col in colliders)if(col.Raycast(new Ray(from,-c.forward),out var hit,2)){float z=c.InverseTransformPoint(hit.point).z;if(float.IsNaN(best)||z>best)best=z;}
                depth[i,j]=best;
            }
            foreach(var col in colliders)Object.DestroyImmediate(col);
            // The back panel is the deepest surface the middle of the cabinet shows.
            var middle=new System.Collections.Generic.List<float>();
            for(int i=nx/3;i<2*nx/3;i++)for(int j=ny/3;j<2*ny/3;j++)if(!float.IsNaN(depth[i,j]))middle.Add(depth[i,j]);
            if(middle.Count==0){Debug.LogWarning("[SchoolOffice] Key cabinet model: no back panel found; hooks left as built.");return;}
            float back=middle.Min();
            // Interior = the box around every back-panel cell (the frame and doors stand far proud of it); pegs are bumps
            // inside it, 2 cm clear of its edges.
            int x0=nx,x1=-1,y0=ny,y1=-1;
            for(int i=0;i<nx;i++)for(int j=0;j<ny;j++)if(!float.IsNaN(depth[i,j])&&depth[i,j]<back+.004f){x0=Mathf.Min(x0,i);x1=Mathf.Max(x1,i);y0=Mathf.Min(y0,j);y1=Mathf.Max(y1,j);}
            int inset=4;var seen=new bool[nx,ny];var pegs=new System.Collections.Generic.List<Vector3>();
            for(int i=x0+inset;i<=x1-inset;i++)for(int j=y0+inset;j<=y1-inset;j++)
            {
                if(seen[i,j]||float.IsNaN(depth[i,j])||depth[i,j]<back+.01f)continue;
                // Flood one bump; keep its middle across, its top, and its front.
                var stack=new System.Collections.Generic.Stack<(int,int)>();stack.Push((i,j));seen[i,j]=true;float sx=0,top=float.MinValue,front=back;int n=0;
                while(stack.Count>0)
                {
                    var (a,d)=stack.Pop();sx+=a;n++;top=Mathf.Max(top,d);front=Mathf.Max(front,depth[a,d]);
                    foreach(var (da,dd) in new[]{(1,0),(-1,0),(0,1),(0,-1)})
                    {int p=a+da,q=d+dd;if(p<x0+inset||p>x1-inset||q<y0+inset||q>y1-inset||seen[p,q]||float.IsNaN(depth[p,q])||depth[p,q]<back+.006f)continue;seen[p,q]=true;stack.Push((p,q));}
                }
                if(n>=3)pegs.Add(new Vector3(-.8f+sx/n*step,-CabinetHeight/2+top*step,front));
            }
            if(pegs.Count!=hooks.Length){Debug.LogWarning("[SchoolOffice] Key cabinet model: found "+pegs.Count+" pegs, not "+hooks.Length+"; hooks left as built.");return;}
            // Two rows of five: hook i = row i/5 (top first), column i%5 from -x, as built.
            var rows=pegs.OrderByDescending(p=>p.y).ToList();
            for(int i=0;i<hooks.Length;i++)
            {
                int row=i/5,col=i%5;var peg=rows.Skip(row*5).Take(5).OrderBy(p=>p.x).ElementAt(col);
                // The key's ring hangs on the peg's top; the key stands just proud of the back panel.
                hooks[i].transform.localPosition=new Vector3(peg.x,peg.y-.012f,back+.012f);EditorUtility.SetDirty(hooks[i].transform);
            }
        }

        const string KeyFolder="Assets/Art/Models/SchoolProps/Keys/";
        const float KeyLength=.28f; // a little over life size, so the painted tags read from arm's length
        /// <summary>Tripo key per label, its tag painted on. A label without one keeps the drawn key and tag.</summary>
        static readonly (string label,string model)[] KeyArt={("STORE","KeyStore"),("PE SHED","KeyPEShed"),("KITCHEN","KeyKitchen"),("HALL","KeyHall"),("MINIBUS","KeyMinibus"),("LIBRARY","KeyLibrary"),("ROOF","KeyRoof"),("GATES","KeyGates"),("BOILER","KeyBoiler"),("STAFF","KeyStaff")};
        /// <summary>Hook i starts with Labels[i]; KeyCabinet moves each model to wherever the shuffle puts its label.</summary>
        static void KeyModels(KeyCabinet cabinet)
        {
            var models=new Transform[KeyCabinet.Labels.Length];
            for(int i=0;i<cabinet.hooks.Length;i++)
            {
                var hook=cabinet.hooks[i];string label=KeyCabinet.Labels[i];
                hook.placeholder=hook.key.GetComponentsInChildren<Renderer>(true);EditorUtility.SetDirty(hook);
                string model=KeyArt.FirstOrDefault(k=>k.label==label).model;
                if(model==null||!PickupModelSetup.Has(model,KeyFolder))continue;
                var visual=new GameObject("Key model "+label).transform;visual.SetParent(hook.key,false);
                // The models stand upright with the tag's writing on their -X: +90 turns that to face the room.
                if(!PickupModelSetup.Place(visual,model,KeyLength,0,90,KeyFolder)){Object.DestroyImmediate(visual.gameObject);continue;}
                // Hang it by its ring: the middle of the top sliver of the mesh goes just under the peg.
                var holder=visual.GetChild(0);
                var points=holder.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(f.transform.TransformPoint)).ToList();
                float top=points.Max(p=>p.y),height=top-points.Min(p=>p.y);
                var sliver=points.Where(p=>p.y>top-height*.06f).ToList();var ring=sliver.Aggregate(Vector3.zero,(a,p)=>a+p)/sliver.Count;
                var peg=hook.transform.position;var across=cabinet.transform.right;
                holder.position+=across*Vector3.Dot(peg-ring,across)+Vector3.up*(peg.y+.012f-top);
                foreach(var r in holder.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=ShadowCastingMode.Off;SchoolLightingSetup.ConfigureArtworkMaterial(r.sharedMaterial,false);}
                foreach(var r in hook.placeholder)r.enabled=false;
                models[i]=visual;
            }
            cabinet.labelModels=models;
        }

        /// <summary>From the corridor by the main entrance: a shut serving hatch in the office's south wall and a sign.</summary>
        static void CorridorHatch()
        {
            var g=Group("Corridor hatch");const float face=3.48f,x=7.4f;
            Box(g,"Hatch frame",new Vector3(x,1.35f,face-.02f),new Vector3(1.3f,.9f,.04f),trim,false);
            Box(g,"Hatch frosted glass",new Vector3(x,1.35f,face-.045f),new Vector3(1.14f,.74f,.01f),Tint("M_Office_Frosted",paper,new Color(.82f,.86f,.86f)),false);
            Box(g,"Hatch ledge",new Vector3(x,.9f,face-.13f),new Vector3(1.4f,.04f,.26f),wood,true);
            Plaque(g,"SCHOOL OFFICE",new Vector3(x,2.08f,face-.03f),new Vector2(1.5f,.3f),0,.019f);
            Plaque(g,"Hatch closed.\nDoor is round the back.",new Vector3(x,1.35f,face-.06f),new Vector2(.95f,.3f),0,.01f,true);
            CutHatchOpening(x,1.35f);
        }

        /// <summary>
        /// The hatch was always just a panel stood in front of the solid south wall (the "glass" was frosted, and the
        /// Tripo model swap hides these box parts anyway) -- a two-sided model looking into a solid wall still shows a
        /// solid wall from the office side. Cuts a real opening the same width and height as the frosted pane, using the
        /// corridor wall's own world-space mapping (LibrarySetup.WallPiece) so the four remaining wall pieces match it.
        /// </summary>
        static void CutHatchOpening(float x,float y)
        {
            const float openW=1.14f,openH=.74f;
            float ox0=x-openW/2,ox1=x+openW/2,oy0=y-openH/2,oy1=y+openH/2;
            var wall=Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude)
                .FirstOrDefault(r=>r.name.StartsWith("Wall_")&&r.bounds.Contains(new Vector3(x,y,South-.03f)));
            if(wall==null){Debug.LogWarning("[SchoolOffice] Corridor hatch: south wall not found under the frame; leaving it a blind panel.");return;}
            var b=wall.bounds;var mat=wall.sharedMaterial;float z=b.center.z,depth=b.size.z;
            wall.gameObject.SetActive(false);EditorUtility.SetDirty(wall.gameObject);
            var group=new GameObject("Corridor hatch wall").transform;group.SetParent(root,false);group.position=new Vector3(0,0,z);
            void Piece(float x0,float x1,float y0,float y1)
            {
                var p=new GameObject("Wall piece").transform;p.SetParent(group,false);
                p.gameObject.AddComponent<MeshFilter>().sharedMesh=LibrarySetup.WallPiece(x0,x1,y0,y1,depth);
                var r=p.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=wall.shadowCastingMode;r.receiveShadows=wall.receiveShadows;
                var box=p.gameObject.AddComponent<BoxCollider>();box.center=new Vector3((x0+x1)/2,(y0+y1)/2,0);box.size=new Vector3(x1-x0,y1-y0,depth);
                GameObjectUtility.SetStaticEditorFlags(p.gameObject,GameObjectUtility.GetStaticEditorFlags(wall.gameObject));
            }
            Piece(b.min.x,ox0,b.min.y,b.max.y); // left of the opening
            Piece(ox1,b.max.x,b.min.y,b.max.y); // right of the opening
            Piece(ox0,ox1,b.min.y,oy0); // below
            Piece(ox0,ox1,oy1,b.max.y); // above
        }

        // ---------- Helpers ----------
        static Transform Group(string name){var g=new GameObject(name).transform;g.SetParent(root,false);return g;}
        static GameObject Box(Transform parent,string name,Vector3 at,Vector3 size,Material m,bool collide)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.position=at;g.transform.localScale=size;
            if(!collide)Object.DestroyImmediate(g.GetComponent<Collider>());TilePencil(g,m);return g;
        }
        static GameObject Local(Transform parent,string name,Vector3 at,Vector3 size,Material m,bool collide)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=at;g.transform.localScale=size;
            if(!collide)Object.DestroyImmediate(g.GetComponent<Collider>());TilePencil(g,m);return g;
        }
        /// <summary>
        /// A plain CreatePrimitive cube has no per-face world-metre UVs or side flag, which the pencil shaders read for
        /// their edge strips and side crop; left unset (all zero) that reads the built-in cube's own lightmap UV2 as if it
        /// were world metres, drawing a tartan of edge strips across every face. Non-pencil materials (glass, screens) are
        /// untouched: they only ever use UV0, which a plain cube already has.
        /// </summary>
        static void TilePencil(GameObject g,Material m)
        {
            var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=m;
            if(m!=null&&m.shader.name=="Confiscated/Pencil Surface")IllustratedArtSetup.Tiled(r,m,"office",1f);
        }
        /// <summary>Floor furniture: solid, and carved out of the NavMesh so staff walk round it.</summary>
        static void Solid(Transform parent,string name,Vector3 at,Vector3 size,Material m)
        {
            var g=Box(parent,name,at,size,m,true);
            var nav=g.AddComponent<NavMeshObstacle>();nav.shape=NavMeshObstacleShape.Box;nav.center=Vector3.zero;nav.size=Vector3.one;nav.carving=true;
        }
        static GameObject Prefab(string path,Transform parent,string name,Vector3 at,float yaw)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)throw new InvalidOperationException("Missing prefab "+path);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);go.name=name;
            go.transform.SetPositionAndRotation(at,Quaternion.Euler(0,yaw,0));PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);return go;
        }
        static TextMesh Text(Transform parent,string value,Vector3 local,float size)
        {
            var g=new GameObject("Tag lettering");g.transform.SetParent(parent,false);g.transform.localPosition=local;g.transform.localRotation=Quaternion.Euler(0,180,0);
            var t=g.AddComponent<TextMesh>();t.font=SchoolTypography.Font;t.text=value;t.fontSize=80;t.characterSize=size;t.color=new Color(.12f,.12f,.16f);
            t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;g.AddComponent<WorldLabel>();return t;
        }
        /// <summary>A plaque with lettering; yaw 180 reads from the north (+z) side, 0 from the south.</summary>
        static void Plaque(Transform parent,string text,Vector3 at,Vector2 size,float yaw,float letters,bool plain=false)
        {
            var facing=Quaternion.Euler(0,yaw,0);
            var p=Box(parent,text.Split('\n')[0]+" plaque",at,new Vector3(size.x,size.y,.02f),plain?paper:trim,false);p.transform.rotation=facing;
            var g=new GameObject(text.Split('\n')[0]);g.transform.SetParent(parent,false);g.transform.SetPositionAndRotation(at+facing*Vector3.back*.014f,facing);
            var t=g.AddComponent<TextMesh>();t.font=SchoolTypography.Font;t.text=text;t.fontSize=80;t.characterSize=letters;t.color=Color.black;
            t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;g.AddComponent<WorldLabel>();
        }
        static Material M(string n)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+n+".mat");
            if(m==null)throw new InvalidOperationException("Missing material "+n);return m;
        }
        static Material Tint(string name,Material source,Color colour)
        {
            string path="Assets/Art/Materials/"+name+".mat";var result=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(result==null){result=new Material(source);result.name=name;AssetDatabase.CreateAsset(result,path);}
            result.SetColor("_BaseColor",colour);EditorUtility.SetDirty(result);return result;
        }
        /// <summary>URP Lit, transparent: the reception screen.</summary>
        static Material Glass()
        {
            const string path="Assets/Art/Materials/M_Office_ScreenGlass.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_ZWrite",0);m.SetFloat("_Smoothness",.85f);m.SetFloat("_Metallic",0);
            m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);m.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType","Transparent");m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=(int)RenderQueue.Transparent;
            m.SetColor("_BaseColor",new Color(.78f,.88f,.92f,.16f));EditorUtility.SetDirty(m);return m;
        }
    }
}

using System;
using System.Collections.Generic;
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
    /// <summary>Additive, repeatable dressing and progression pass on the existing school.</summary>
    public static class SchoolRunSetup
    {
        const string Prefabs = "Assets/Prefabs/SchoolRun/";
        const string Art = "Assets/Art/Textures/SchoolRun/";
        static Transform root;
        static SchoolRunController run;
        static readonly List<RunPickup> pickups = new List<RunPickup>();
        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/" + name + ".mat");
        static Vector3 P(float x,float y,float h=0) => SchoolPlan.Point(x,y,h);

        [MenuItem("Confiscated/School Run/Build Chase Loop and Dress School")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play before rebuilding.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != SchoolLayoutBuilder.ScenePath) throw new InvalidOperationException("Open SchoolLayout first.");
            Directory.CreateDirectory(Prefabs); Directory.CreateDirectory(Art);
            var old = GameObject.Find("SchoolRun"); if (old != null) Object.DestroyImmediate(old);
            root = new GameObject("SchoolRun").transform;
            run = root.gameObject.AddComponent<SchoolRunController>();
            run.period = Object.FindFirstObjectByType<SchoolPeriodController>();
            run.caretaker = run.period.caretaker; pickups.Clear();
            BuildLibrary(); DressDining(); DressRooms(); DressHalls(); ConfigureDoors(); LessonGateSetup.ApplyToScene(); ConfigureTrolleyAndStaff();
            run.pickups = pickups.ToArray();
            var decoy = run.period.Player.GetComponent<ClockworkDecoy>();
            if (decoy == null) decoy = run.period.Player.gameObject.AddComponent<ClockworkDecoy>();
            decoy.toyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "P_WindUpToy.prefab");
            EditorUtility.SetDirty(decoy); PrefabUtility.RecordPrefabInstancePropertyModifications(decoy);
            AddDecoy(new Vector3(-40.1f,1.05f,81.8f));
            AddDecoy(P(527,950,.85f)); AddDecoy(P(812,819,.85f)); AddDecoy(P(790,330,.85f));
            // The escape is Hold F on the main entrance doors themselves; PlaytestFixSetup (chained below) configures them.
            InstallEscapeTools();
            CaretakerGaitSetup.Configure(run.caretaker);
            ArrangeArtRoom();
            ArrangeShelves();
            var surface = Object.FindFirstObjectByType<NavMeshSurface>(); surface.BuildNavMesh();
            var data = surface.navMeshData;
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(data)))
            {
                const string path="Assets/Scenes/SchoolRun_NavMesh.asset";
                var saved=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
                surface.RemoveData();
                data.name="SchoolRun_NavMesh";
                if(saved==null){AssetDatabase.CreateAsset(data,path);saved=data;}else{EditorUtility.CopySerialized(data,saved);Object.DestroyImmediate(data);}
                surface.navMeshData=saved;surface.AddData();EditorUtility.SetDirty(saved);
            }
            // Rebuilding this root discards padlocks, puddles and the dinner trolley; restore them, then dress with art.
            ChaseFeedbackSetup.Install(); DinnerLadySetup.Install();
            if (Batch3ArtSetup.Available) Batch3ArtSetup.ApplyToScene();
            QuickDemoSetup.ApplyToScene();
            GlueSetup.ApplyToScene();
            ChecklistIconSetup.ApplyToScene();
            RemoveFootball();
            PlaytestFixSetup.ApplyToScene();
            PlayerClaritySetup.ApplyToScene();
            WaterFountainSetup.ApplyToScene();
            GeneratedHallwayLandmarksSetup.ApplyToScene();
            SchoolMapPosterSetup.ApplyToScene();
            DarkModeSetup.ApplyToScene();
            SchoolLightingSetup.ApplyToScene();
            PeCoachSetup.ApplyToScene();
            CharacterShadowSetup.ApplyToScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            Debug.Log("[SchoolRun] Built five-item loop, cafeteria cover, locked rooms, furnished shortcuts and generated-art displays.");
        }

        static Transform Group(string name,Transform parent,Vector3 local)
        { var g=new GameObject(name).transform;g.SetParent(parent,false);g.localPosition=local;return g; }
        static GameObject Box(string name,Transform parent,Vector3 local,Vector3 size,string material,bool collider=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=local;g.transform.localScale=size;
            IllustratedArtSetup.Tiled(g.GetComponent<MeshRenderer>(),Mat(material),"run",1);
            if(!collider)Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        static void Text(Transform parent,string value,Vector3 pos,float yaw,float size)
        {
            var g=Group(value.Replace('\n',' '),parent,pos);g.localRotation=Quaternion.Euler(0,yaw,0);
            var t=g.gameObject.AddComponent<TextMesh>();t.font=SchoolTypography.Font;t.fontSize=72;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.text=value;t.color=new Color(.07f,.1f,.15f);
            g.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;g.gameObject.AddComponent<WorldLabel>();
        }
        static GameObject Place(string prefab,Vector3 p,float yaw=0,Transform parent=null)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(prefab.StartsWith("Assets/")?prefab:Prefabs+prefab+".prefab");
            if(asset==null)throw new Exception("Missing prefab "+prefab);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent!=null?parent:root);go.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));return go;
        }
        static void Save(Transform t,string name) { PrefabUtility.SaveAsPrefabAsset(t.gameObject,Prefabs+name+".prefab");Object.DestroyImmediate(t.gameObject); }
        /// <summary>The football was dropped from the game (weak art, no real effect on the caretaker). The layout builder still carries it over from the old office chapter, so take it out here.</summary>
        [MenuItem("Confiscated/School Run/Remove Football")]
        public static void RemoveFootballMenu(){if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");RemoveFootball();EditorSceneManager.SaveOpenScenes();}
        static void RemoveFootball()
        {
            foreach(var ball in Object.FindObjectsByType<ThrowableBall>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.DestroyImmediate(ball.gameObject);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        static void BuildLibrary()
        {
            var screen=Group("P_DiningScreen",null,Vector3.zero);
            Box("Opaque fabric",screen,new Vector3(0,1.1f,0),new Vector3(2.4f,1.8f,.12f),"M_Chapter_Green");
            foreach(float x in new[]{-1.23f,1.23f}){Box("Timber upright",screen,new Vector3(x,1.03f,0),new Vector3(.07f,2.06f,.09f),"M_Wood_Desk");Box("Stable foot",screen,new Vector3(x,.06f,0),new Vector3(.12f,.12f,.7f),"M_Wood_Desk");}
            Save(screen,"P_DiningScreen");
            var shelf=Group("P_TallStorage",null,Vector3.zero);
            Box("Solid back - sight cover",shelf,new Vector3(0,1,0.34f),new Vector3(1.8f,2,.1f),"M_Chapter_Green");
            foreach(float x in new[]{-.86f,.86f})Box("Shelf end",shelf,new Vector3(x,1,0),new Vector3(.08f,2,.72f),"M_Wood_Desk");
            for(int j=0;j<5;j++)
            {
                Box("Shelf",shelf,new Vector3(0,.12f+j*.45f,0),new Vector3(1.8f,.07f,.72f),"M_Wood_Desk");
                if(j<4)for(int i=0;i<5;i++)Box("Box of supplies",shelf,new Vector3(-.67f+i*.32f,.31f+j*.45f,.04f),new Vector3(.27f,.29f,.48f),i%2==0?"M_Chapter_Cardboard":"M_Chapter_Paper",false);
            }
            Save(shelf,"P_TallStorage");
            var toy=Group("P_WindUpToy",null,Vector3.zero);
            Box("Tin body",toy,new Vector3(0,.15f,0),new Vector3(.24f,.22f,.3f),"M_Chapter_ToyRed",false);
            Box("Head",toy,new Vector3(0,.3f,.07f),new Vector3(.2f,.1f,.14f),"M_Chapter_Brass",false);
            foreach(float x in new[]{-.07f,.07f})Box("Eye",toy,new Vector3(x,.31f,.145f),new Vector3(.035f,.035f,.015f),"M_Chapter_Ink",false);
            foreach(float x in new[]{-.12f,.12f})Box("Foot",toy,new Vector3(x,.025f,0),new Vector3(.1f,.05f,.3f),"M_Chapter_Ink",false);
            Box("Winding key",toy,new Vector3(.19f,.16f,0),new Vector3(.16f,.035f,.07f),"M_Chapter_Brass",false);
            Save(toy,"P_WindUpToy");
            var board=Group("P_ArtDisplay",null,Vector3.zero);
            Box("Timber frame",board,Vector3.zero,new Vector3(2.56f,1.78f,.1f),"M_Wood_Desk",false);
            var face=GameObject.CreatePrimitive(PrimitiveType.Quad);face.name="Generated pupil artwork";face.transform.SetParent(board,false);face.transform.localPosition=new Vector3(0,0,-.06f);face.transform.localScale=new Vector3(2.4f,1.6f,1);Object.DestroyImmediate(face.GetComponent<Collider>());
            face.GetComponent<MeshRenderer>().sharedMaterial=ArtMaterial("PupilArt");Save(board,"P_ArtDisplay");
        }
        static Material ArtMaterial(string name)
        {
            string path="Assets/Art/Materials/M_Run_"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(mat,path);}
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+name+".png");
            if(texture==null)throw new Exception("Generated art missing: "+name);
            mat.SetTexture("_BaseMap",texture);EditorUtility.SetDirty(mat);return mat;
        }
        static void Display(Vector3 pos,float yaw,string art="PupilArt",string title=null)
        {
            var board=Place("P_ArtDisplay",pos,yaw);
            board.transform.Find("Generated pupil artwork").GetComponent<MeshRenderer>().sharedMaterial=ArtMaterial(art);
            if(art=="Nature")board.transform.localScale=new Vector3(.55f,1.24f,1);
            if(title!=null)Text(board.transform,title,new Vector3(0,.96f,-.08f),0,.032f);
        }
        static void DressDining()
        {
            // Replace selected western seats with readable, full-height cover.
            var seats=GameObject.Find("DiningHallFurniture/Seating - 144 places");
            if(seats!=null)foreach(Transform table in seats.transform)
                if(table.position.x < -26 && table.position.z < 63)table.gameObject.SetActive(false);
            Place("P_TallStorage",new Vector3(-29.1f,0,61.4f),25);
            Place("P_TallStorage",new Vector3(-27.1f,0,58.7f),-25);
            Place("P_DiningScreen",new Vector3(-28.3f,0,55.8f),18);
            Place("P_DiningScreen",new Vector3(-26.7f,0,51.4f),-18);
            Place("P_TallStorage",new Vector3(-26.2f,0,46.8f),90);
            Place("P_DiningScreen",new Vector3(-30.2f,0,48.5f),0);
            Display(new Vector3(-31.9f,1.7f,67.5f),-90,"Nature","OUR SCHOOL GARDEN");
            Display(new Vector3(-7.2f,1.8f,55),90,"Sports","PLAY FAIR");
            var cage=Group("Dining property enclosure",root,new Vector3(-10.0f,0,44.6f));
            Box("Rear partition",cage,new Vector3(0,1.1f,-1.05f),new Vector3(3.4f,2.2f,.12f),"M_Chapter_Grey");
            foreach(float x in new[]{-1.65f,1.65f})Box("Side partition",cage,new Vector3(x,1.1f,0),new Vector3(.12f,2.2f,2.2f),"M_Chapter_Grey");
            // Front wall around a 1.3m opening. Gate slides entirely into its own enclosure.
            foreach(float x in new[]{-1.2f,1.2f})Box("Front panel",cage,new Vector3(x,1.1f,1.05f),new Vector3(.95f,2.2f,.12f),"M_Chapter_Grey");
            var gateRoot=Group("Chained property gate",cage,new Vector3(0,0,1.05f));
            var gate=gateRoot.gameObject.AddComponent<RunGate>();gate.kind=RunGate.Kind.Chain;gate.holdSeconds=2;
            var leaf=Box("Gate leaf",gateRoot,new Vector3(0,1,0),new Vector3(1.35f,2,.14f),"M_Chapter_Green");
            gate.obstacle=leaf.transform;gate.clearedLocalPosition=new Vector3(1.45f,1,-.65f);gate.clearedLocalEuler=new Vector3(0,90,0);
            Box("Chain",leaf.transform,new Vector3(0,0,-.65f),new Vector3(.95f,.025f,.04f),"M_Chapter_Brass",false);
            Box("Gate notice",gateRoot,new Vector3(0,1.55f,.11f),new Vector3(1.08f,.55f,.03f),"M_Chapter_Paper",false);
            Text(gateRoot,"STORAGE",new Vector3(0,1.55f,.14f),180,.032f);
            AddDynamicBlock(gateRoot.gameObject,new Vector3(0,1,0),new Vector3(1.4f,2,.25f));
            AddPickup(1,"yo-yo",new Vector3(-10,1,44.3f));
        }
        static void DressRooms()
        {
            FurnishClass(5,"CLASSROOM 4");FurnishClass(9,"CLASSROOM 3");FurnishClass(12,"ART ROOM");
            foreach(int zone in new[]{4,8})
            {
                var r=SchoolPlan.Room(zone).rect;var c=P(r.center.x,r.center.y);
                for(int j=0;j<3;j++)Place("P_TallStorage",c+new Vector3(-2.4f,0,-3+j*3),90);
                Place("P_TallStorage",c+new Vector3(2,0,1),-90);
                Display(P(r.center.x,r.yMin+.9f,1.75f),0,zone==4?"Nature":"Sports",zone==4?"RESOURCES":"EQUIPMENT");
                AddPickup(zone==4?2:3,zone==4?"handheld game":"skateboard",c+new Vector3(2.5f,.85f,-2.5f));
            }
            AddPickup(4,"toy robot",P(824,1091,.85f));
            var ar=SchoolPlan.Room(12).rect;
            Place("P_DiningScreen",P(ar.center.x-20,ar.center.y),0);
            Place("P_TallStorage",P(ar.center.x+22,ar.center.y+38),90);
        }
        static void FurnishClass(int zone,string title)
        {
            var r=SchoolPlan.Room(zone).rect;
            int rows=zone==12?3:3;
            for(int row=0;row<rows;row++)for(int col=0;col<3;col++)
            {
                var pos=P(r.center.x+(col-1)*31,r.center.y+(row-1)*28);
                var desk=Group(title+" pupil station",root,pos);
                Box("Pencil desktop",desk,new Vector3(0,.74f,0),new Vector3(1.25f,.09f,.72f),"M_Wood_Desk");
                foreach(float x in new[]{-.5f,.5f})foreach(float z in new[]{-.25f,.25f})Box("Leg",desk,new Vector3(x,.35f,z),new Vector3(.05f,.7f,.05f),"M_Chapter_Green",false);
                Box("Chair seat",desk,new Vector3(0,.42f,-.65f),new Vector3(.46f,.06f,.43f),"M_Wood_Desk");
                Box("Chair back",desk,new Vector3(0,.66f,-.85f),new Vector3(.46f,.45f,.06f),"M_Wood_Desk");
                foreach(float x in new[]{-.18f,.18f})foreach(float z in new[]{-.8f,-.5f})Box("Chair leg",desk,new Vector3(x,.2f,z),new Vector3(.04f,.4f,.04f),"M_Chapter_Green",false);
                Box("Exercise book",desk,new Vector3(.2f,.8f,.04f),new Vector3(.27f,.025f,.34f),row%2==0?"M_Chapter_Green":"M_Chapter_ToyRed",false);
                Box("Pencil",desk,new Vector3(-.2f,.8f,.1f),new Vector3(.16f,.012f,.014f),"M_Chapter_Brass",false);
            }
            // The art-room north entrance occupies the left section of this wall.
            var front=P(zone==12?r.center.x:r.xMin+29,r.yMin+1.2f,1.7f);
            var board=Group(title+" teaching board",root,front);
            Box("Board frame",board,Vector3.zero,new Vector3(3.1f,1.5f,.1f),"M_Wood_Desk",false);
            Box("Chalk face",board,new Vector3(0,0,-.065f),new Vector3(2.9f,1.3f,.025f),"M_Chapter_Green",false);
            Text(board,title+"\nLOOK, THINK, MAKE",new Vector3(0,0,-.09f),0,.042f);
            Place("P_TallStorage",P(r.xMin+13,r.center.y),90);
            Display(zone==12?P(r.xMin+1,r.yMin+58,1.75f):P(r.xMax-1,r.center.y,1.75f),zone==12?-90:90,zone==12?"PupilArt":"Nature","OUR WORK");
        }
        static void AddPickup(int id,string name,Vector3 pos)
        {
            var station=Group("Confiscated "+name,root,pos);
            Box("Property shelf",station,new Vector3(0,-.08f,0),new Vector3(1,.12f,.65f),"M_Wood_Desk");
            foreach(float x in new[]{-.4f,.4f})foreach(float z in new[]{-.24f,.24f})Box("Display stand leg",station,new Vector3(x,-pos.y*.5f-.05f,z),new Vector3(.06f,pos.y-.1f,.06f),"M_Chapter_Green",false);
            var interact=station.gameObject.AddComponent<RunPickup>();interact.itemId=id;interact.itemName=name;
            var visual=Group(name+" visual",station,Vector3.zero);interact.visual=visual.gameObject;
            if(id==1)
            {
                foreach(float x in new[]{-.06f,.06f})Box("Yo-yo disc",visual,new Vector3(x,.15f,0),new Vector3(.09f,.23f,.23f),"M_Chapter_ToyRed",false);
                Box("String",visual,new Vector3(.15f,.05f,.12f),new Vector3(.3f,.015f,.02f),"M_Chapter_Paper",false);
            }
            else if(id==2)
            {
                Box("Console",visual,new Vector3(0,.1f,0),new Vector3(.34f,.12f,.47f),"M_Chapter_Grey",false);
                Box("Screen",visual,new Vector3(0,.168f,.07f),new Vector3(.24f,.015f,.18f),"M_Chapter_Green",false);
                foreach(float x in new[]{-.1f,.1f})Box("Button",visual,new Vector3(x,.168f,-.12f),new Vector3(.06f,.02f,.06f),"M_Chapter_ToyRed",false);
            }
            else if(id==3)
            {
                Box("Skateboard deck",visual,new Vector3(0,.15f,0),new Vector3(.85f,.06f,.3f),"M_Chapter_ToyRed",false);
                foreach(float x in new[]{-.3f,.3f})foreach(float z in new[]{-.16f,.16f})Box("Wheel",visual,new Vector3(x,.07f,z),new Vector3(.1f,.12f,.07f),"M_Chapter_Ink",false);
            }
            else Place("P_WindUpToy",pos,0,visual);
            // A whole station collider is intentionally reachable from outside the visual without requiring pixel precision.
            var hit=station.gameObject.AddComponent<BoxCollider>();hit.center=new Vector3(0,.15f,0);hit.size=new Vector3(.9f,.45f,.6f);
            pickups.Add(interact);
        }
        static void AddDecoy(Vector3 pos)
        {
            int zone=pos.x< -30?4:pos.z>80?5:pos.z>35?9:12;
            var room=SchoolPlan.Room(zone).rect;
            var desk=Group("Decoy teacher desk",root,P(room.center.x,room.yMin+16));
            Box("Teacher desktop",desk,new Vector3(0,.82f,0),new Vector3(1.9f,.1f,.9f),"M_Wood_Desk");
            foreach(float x in new[]{-.8f,.8f})foreach(float z in new[]{-.33f,.33f})Box("Desk leg",desk,new Vector3(x,.39f,z),new Vector3(.075f,.78f,.075f),"M_Chapter_Green");
            Box("Desk drawers",desk,new Vector3(.59f,.6f,0),new Vector3(.43f,.34f,.72f),"M_Wood_Desk");
            Box("Marking book",desk,new Vector3(-.6f,.885f,.04f),new Vector3(.3f,.03f,.4f),"M_Chapter_ToyRed",false);
            pos=desk.position+new Vector3(0,.92f,0);
            var station=Group("Spare wind-up decoy",root,pos);
            var toy=Place("P_WindUpToy",pos,0,station);var pickup=station.gameObject.AddComponent<DecoyPickup>();pickup.visual=toy;
            var hit=station.gameObject.AddComponent<BoxCollider>();hit.center=new Vector3(0,.24f,0);hit.size=new Vector3(.6f,.55f,.6f);
        }
        [MenuItem("Confiscated/School Run/Move Decoys to Teacher Desks")]
        public static void RefreshDecoyDesks()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play before dressing desks.");
            root=GameObject.Find("SchoolRun").transform;
            foreach(var old in root.Cast<Transform>().Where(t=>t.name=="Spare wind-up decoy"||t.name=="Decoy teacher desk").ToArray())Undo.DestroyObjectImmediate(old.gameObject);
            AddDecoy(new Vector3(-40.1f,1.05f,81.8f));AddDecoy(P(527,950,.85f));AddDecoy(P(812,819,.85f));AddDecoy(P(790,330,.85f));
            ArrangeArtRoom();
            SaveRoomNavigation();
        }
        static void ArrangeArtRoom()
        {
            var objects=root.Cast<Transform>().Where(t=>t.position.x>-11.8f&&t.position.x<1.45f&&t.position.z>9.4f&&t.position.z<32.5f).ToArray();
            var desks=objects.Where(t=>t.name=="ART ROOM pupil station").OrderByDescending(t=>t.position.z).ThenBy(t=>t.position.x).ToArray();
            float[] rows={26,23,17};
            for(int i=0;i<desks.Length;i++){desks[i].position=new Vector3(-8.4f+(i%3)*3.2f,0,rows[i/3]);desks[i].rotation=Quaternion.identity;}
            foreach(var t in objects)
            {
                Undo.RecordObject(t,"Arrange art room");
                if(t.name=="ART ROOM teaching board")t.position=new Vector3(-5.17f,2,32.31f);
                else if(t.name=="P_ArtDisplay")t.SetPositionAndRotation(new Vector3(-11.66f,1.75f,26),Quaternion.Euler(0,-90,0));
                else if(t.name=="P_DiningScreen")t.position=new Vector3(-8,0,13);
                else if(t.name=="Decoy teacher desk")t.position=new Vector3(-5.17f,0,29.5f);
                else if(t.name=="Spare wind-up decoy")t.position=new Vector3(-5.17f,.92f,29.5f);
            }
            var storage=objects.Where(t=>t.name=="P_TallStorage").OrderBy(t=>t.position.x).ToArray();
            for(int i=0;i<storage.Length;i++){storage[i].position=new Vector3(i==0?-11.25f:.9f,0,14);storage[i].rotation=Quaternion.Euler(0,i==0?-90:90,0);}
        }
        [MenuItem("Confiscated/School Run/Arrange Art Room")]
        public static void RefreshArtRoomLayout()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play before arranging the room.");
            root=GameObject.Find("SchoolRun").transform;ArrangeArtRoom();SaveRoomNavigation();
        }
        static void SaveRoomNavigation()
        {
            ArrangeShelves();
            var surface=Object.FindFirstObjectByType<NavMeshSurface>();surface.BuildNavMesh();
            var data=surface.navMeshData;data.name="SchoolRun_NavMesh";
            const string path="Assets/Scenes/SchoolRun_NavMesh.asset";
            var saved=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);surface.RemoveData();
            if(saved==null){AssetDatabase.CreateAsset(data,path);saved=data;}else{EditorUtility.CopySerialized(data,saved);Object.DestroyImmediate(data);}
            surface.navMeshData=saved;surface.AddData();EditorUtility.SetDirty(saved);EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
        static void ArrangeShelves()
        {
            // Shelf backs face the wall; open faces look into usable circulation space.
            var shelves=root.Cast<Transform>().Where(t=>t.name=="P_TallStorage").GroupBy(t=>SchoolPlan.ZoneAt(t.position.x*9+585,1183-t.position.z*9));
            foreach(var group in shelves)
            {
                var items=group.OrderBy(t=>t.position.x).ThenBy(t=>t.position.z).ToArray();
                for(int i=0;i<items.Length;i++)
                {
                    Vector3 p=items[i].position;float yaw=items[i].eulerAngles.y;
                    switch(group.Key)
                    {
                        case 4: // Resources: paired wall cabinets, broad central sorting aisle.
                            p=new Vector3(i<2?2.83f:13.39f,0,88+(i%2)*2);yaw=i<2?-90:90;break;
                        case 8: // Equipment: north/south storage banks leave the east-west route open.
                            p=new Vector3(21.5f+(i%2)*2,0,i<2?63.61f:50.83f);yaw=i<2?0:180;break;
                        case 5: p=new Vector3(16.94f,0,84.5f+i*2);yaw=-90;break;
                        case 9: p=new Vector3(15.28f,0,38.5f+i*2);yaw=-90;break;
                        case 12: p=new Vector3(i==0?-11.28f:.94f,0,14);yaw=i==0?-90:90;break;
                        case 6: // Dining supplies: one straight service bank, clear of the west door.
                            p=new Vector3(-31.61f,0,52.8f+i*2);yaw=-90;break;
                        default: continue;
                    }
                    Undo.RecordObject(items[i],"Arrange school shelving");items[i].SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));
                    EditorUtility.SetDirty(items[i]);PrefabUtility.RecordPrefabInstancePropertyModifications(items[i]);
                }
            }
        }
        [MenuItem("Confiscated/School Run/Arrange School Shelves")]
        public static void RefreshShelfLayout()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play before arranging shelves.");
            root=GameObject.Find("SchoolRun").transform;SaveRoomNavigation();
        }
        static void DressHalls()
        {
            Display(P(295.1f,978,1.8f),90,"PupilArt","YEAR 6 GALLERY");
            Display(P(264.9f,835,2),-90,"Nature","OUR SCHOOL");
            Display(P(340,483.1f,1.8f),180,"Sports","LUNCH & PLAY");
            Display(P(635,253.9f,1.75f),0,"Nature","EXPLORE & DISCOVER");
            Display(P(817,253.9f,1.8f),0,"Sports","SCHOOL SPORT");
            Display(P(876.9f,704,1.8f),-90,"Sports","EQUIPMENT");
            Display(P(907.1f,825,2),90,"PupilArt","CLASS GALLERY");
            Display(P(695,1182.1f,1.8f),180,"PupilArt","WELCOME TO OUR SCHOOL");
            Notice(P(264.9f,529,1.7f),-90,"CARETAKER'S ROUND\nOFFICE - DINING HALL\nOFFICE KEY ON TROLLEY");
            Notice(P(295.1f,627,1.8f),90,"DINING HALL\nCLOSED DURING LESSONS");
            Notice(P(718.9f,547,1.8f),-90,"DETENTION\nWALK. LISTEN. THINK.");
            Notice(P(548,1182.1f,1.8f),180,"VISITORS\nPLEASE REPORT TO RECEPTION");
            Place("Assets/Prefabs/Hallway/P_Hall_Bench.prefab",P(278,802),-90);
            Place("Assets/Prefabs/Hallway/P_Hall_RecyclingBin.prefab",P(278,816),-90);
            Place("Assets/Prefabs/Hallway/P_Hall_Bench.prefab",P(891,950),90);
            Place("Assets/Prefabs/Hallway/P_Hall_RecyclingBin.prefab",P(891,964),90);
            // Face the south-wall seat into the hall; keep its bin beside the seat and against the wall.
            Place("Assets/Prefabs/Hallway/P_Hall_Bench.prefab",P(563,1160),0);
            Place("Assets/Prefabs/Hallway/P_Hall_RecyclingBin.prefab",P(548,1156),0);
            var block=Group("Movable maintenance shortcut",root,P(617,654));
            var gate=block.gameObject.AddComponent<RunGate>();gate.kind=RunGate.Kind.Shortcut;gate.holdSeconds=2.5f;
            var barrier=Box("Supply trolley",block,new Vector3(0,.65f,0),new Vector3(.75f,1.3f,3.35f),"M_Chapter_Cardboard");
            gate.obstacle=barrier.transform;gate.clearedLocalPosition=new Vector3(0,.65f,1.3f);gate.clearedLocalEuler=new Vector3(0,90,0);
            // The cleared trolley folds against the edge instead of remaining full corridor width.
            AddDynamicBlock(block.gameObject,new Vector3(0,.7f,0),new Vector3(.8f,1.4f,3.4f));
            ShortcutTrolleySetup.Configure(gate);
        }
        static void InstallEscapeTools()
        {
            foreach(var old in root.Cast<Transform>().Where(t=>t.name=="Access tools").ToArray())Object.DestroyImmediate(old.gameObject);
            var kit=Group("Access tools",root,Vector3.zero);
            CreateTool(kit,AccessToolPickup.Tool.BoltCutters,new Vector3(-45.5f,.84f,78.9f));
            var desk=Group("Equipment checkout desk",kit,new Vector3(23.5f,0,61.4f));
            Box("Desktop",desk,new Vector3(0,.78f,0),new Vector3(1.5f,.08f,.8f),"M_Wood_Desk");
            foreach(float x in new[]{-.6f,.6f})foreach(float z in new[]{-.3f,.3f})Box("Leg",desk,new Vector3(x,.38f,z),new Vector3(.06f,.76f,.06f),"M_Chapter_Green");
            CreateTool(kit,AccessToolPickup.Tool.StoreKey,new Vector3(23.5f,.91f,61.4f));
            foreach(var pickup in root.GetComponentsInChildren<RunPickup>())
                foreach(var child in pickup.transform.Cast<Transform>().Where(t=>t.name=="Property label"||t.GetComponent<TextMesh>()!=null).ToArray())Object.DestroyImmediate(child.gameObject);
            // No collectibles or decoy rewards in the opening classroom.
            foreach(var decoy in root.GetComponentsInChildren<DecoyPickup>())if(SchoolPeriodController.InClass(decoy.transform.position))decoy.transform.position=new Vector3(8.11f,.92f,97.89f);
            foreach(var deskRoot in root.Cast<Transform>().Where(t=>t.name=="Decoy teacher desk"&&SchoolPeriodController.InClass(t.position)))deskRoot.position=new Vector3(8.11f,0,97.89f);
        }
        static void CreateTool(Transform parent,AccessToolPickup.Tool kind,Vector3 position)
        {
            var host=Group(kind.ToString(),parent,position);var pickup=host.gameObject.AddComponent<AccessToolPickup>();pickup.tool=kind;
            var visual=Group("Collectible tool",host,Vector3.zero);pickup.visual=visual.gameObject;
            if(kind==AccessToolPickup.Tool.BoltCutters)
            {
                foreach(float side in new[]{-1f,1f})
                {
                    var arm=Group("Cutter arm",visual,Vector3.zero);arm.localRotation=Quaternion.Euler(0,side*17,0);
                    Box("Red grip",arm,new Vector3(side*.05f,0,-.14f),new Vector3(.045f,.045f,.24f),"M_Chapter_ToyRed",false);
                    Box("Steel jaw",arm,new Vector3(side*.035f,0,.06f),new Vector3(.055f,.055f,.19f),"M_Chapter_Grey",false);
                }
                Box("Pivot bolt",visual,Vector3.zero,new Vector3(.11f,.065f,.055f),"M_Chapter_Brass",false);
            }
            else
            {
                Box("Key bow",visual,new Vector3(0,0,.065f),new Vector3(.13f,.025f,.1f),"M_Chapter_Brass",false);
                Box("Key shaft",visual,new Vector3(0,0,-.05f),new Vector3(.035f,.025f,.16f),"M_Chapter_Brass",false);
                Box("Key teeth",visual,new Vector3(.03f,0,-.1f),new Vector3(.08f,.025f,.04f),"M_Chapter_Brass",false);
            }
            var hit=host.gameObject.AddComponent<BoxCollider>();hit.center=new Vector3(0,.06f,0);hit.size=new Vector3(.45f,.3f,.5f);
        }
        public static void RefreshEscapeLoop()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play first.");
            root=GameObject.Find("SchoolRun").transform;InstallEscapeTools();if(Batch3ArtSetup.Available)Batch3ArtSetup.ApplyToScene();SaveRoomNavigation();
        }
        internal static Transform Notice(Vector3 p,float yaw,string text)
        {
            if(root==null)root=GameObject.Find("SchoolRun").transform;
            var board=Group("School notice",root,p);board.rotation=Quaternion.Euler(0,yaw,0);
            bool closed=text=="CLOSED TODAY"||text=="CLOSED";
            float width=closed?.42f:.65f,height=closed?.19f:.42f;
            Box("Paper notice",board,Vector3.zero,new Vector3(width,height,.012f),"M_Chapter_Paper",false);
            if(closed)
            {
                foreach(float y in new[]{-height*.5f+.014f,height*.5f-.014f})
                    Box("Burgundy border",board,new Vector3(0,y,-.009f),new Vector3(width-.022f,.009f,.004f),"M_Chapter_ToyRed",false);
                foreach(float x in new[]{-width*.5f+.014f,width*.5f-.014f})
                    Box("Burgundy border",board,new Vector3(x,0,-.009f),new Vector3(.009f,height-.022f,.004f),"M_Chapter_ToyRed",false);
                text="CLOSED";
            }
            else foreach(float x in new[]{-width*.5f+.025f,width*.5f-.025f})
                Box("Drawing pin",board,new Vector3(x,height*.5f-.024f,-.011f),new Vector3(.012f,.012f,.008f),"M_Chapter_Brass",false);
            Text(board,text,new Vector3(0,0,-.014f),0,closed?.018f:.016f);
            var lettering=board.GetComponentInChildren<TextMesh>();
            // Fit the actual font geometry to the card, including multiline notices.
            var bounds=lettering.GetComponent<MeshRenderer>().localBounds.size;
            float fit=Mathf.Min(1,(width-.065f)/Mathf.Max(.001f,bounds.x),(height-.055f)/Mathf.Max(.001f,bounds.y));
            lettering.transform.localScale=Vector3.one*fit;
            return board;
        }
        static void DoorNotice(OfficeDoor door,string text)
        {
            var leaf=door.hinge.GetComponentsInChildren<MeshRenderer>().First(r=>r.name=="Leaf");
            var bounds=leaf.localBounds;
            // Below the window and handle decals, with half a millimetre clearance.
            var point=leaf.transform.TransformPoint(new Vector3(bounds.center.x,bounds.min.y+bounds.size.y*.4f,bounds.min.z));
            var notice=Notice(point-leaf.transform.forward*.0011f,leaf.transform.eulerAngles.y,text);
            notice.name="School door notice";
            notice.localScale=new Vector3(1,1,.1f);
            notice.SetParent(leaf.transform,true);
            foreach(var renderer in notice.GetComponentsInChildren<Renderer>())renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        [MenuItem("Confiscated/School Run/Resize Door and Paper Notices")]
        public static void RefreshNotices()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play to save the notices.");
            var schoolRun=GameObject.Find("SchoolRun");if(schoolRun==null)throw new InvalidOperationException("Open the dressed school scene.");
            root=schoolRun.transform;int count=0;
            var doors=Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None);
            foreach(var door in doors)
                foreach(var old in door.GetComponentsInChildren<Transform>().Where(t=>t.name=="School door notice").ToArray())Undo.DestroyObjectImmediate(old.gameObject);
            foreach(var old in root.Cast<Transform>().Where(t=>t.name=="School notice").ToArray())
            {
                var label=old.GetComponentInChildren<TextMesh>();if(label==null)continue;
                Vector3 position=old.position;float yaw=old.eulerAngles.y;string value=label.text;
                Undo.DestroyObjectImmediate(old.gameObject);
                if(value!="CLOSED"&&value!="CLOSED TODAY"&&!value.Contains("LOCKED STORAGE"))Notice(position,yaw,value);
                count++;
            }
            foreach(var door in doors)if(door.closedForRun||door.runRequiredLevel>0)DoorNotice(door,door.closedForRun?"CLOSED":door.runLockName+"\nLOCKED STORAGE");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            Debug.Log("[SchoolRun] Resized "+count+" door and paper notices.");
        }
        static void ConfigureDoors()
        {
            var doors=Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None);
            foreach(var door in doors)
            {
                foreach(var old in door.GetComponentsInChildren<Transform>().Where(t=>t.name=="School door notice").ToArray())Object.DestroyImmediate(old.gameObject);
                string n=door.name;
                door.closedForRun=n.StartsWith("North room A")||n.StartsWith("North classroom A")||n.StartsWith("South room A")||n.StartsWith("South classroom")||n=="East room A north"||n=="East room A south"||n=="Dining west A"||n=="Dining east A"||n=="North yard doors"||n=="East yard doors";
                door.runRequiredLevel=n.StartsWith("North room B")?2:n.StartsWith("East room B")?3:n=="Store cupboard"?4:0;
                door.runLockName=door.runRequiredLevel==2?"RESOURCES":door.runRequiredLevel==3?"EQUIPMENT":"STORE";
                if(door.closedForRun||door.runRequiredLevel>0)door.startsUnlocked=false;
                if(door.closedForRun||door.runRequiredLevel>0)
                {
                    var marker=Group("Door blocker - "+n,root,door.transform.position);marker.rotation=door.transform.rotation;
                    var barrier=Box("Locked doorway collision",marker,new Vector3(0,1.1f,0),new Vector3(n.Contains("yard")?2.6f:1.5f,2.2f,.18f),"M_Chapter_Grey");
                    barrier.GetComponent<MeshRenderer>().enabled=false;
                    // Invisible, so it must never stand between the player's interaction ray and the door it guards.
                    barrier.layer=PlaytestFixSetup.IgnoreRaycastLayer;
                    if(!door.closedForRun)
                    {
                        AddDynamicBlock(barrier,new Vector3(0,0,0),Vector3.one);
                        var ctrl=marker.gameObject.AddComponent<RunDoorBlocker>();ctrl.door=door;ctrl.barrier=barrier;
                    }
                    DoorNotice(door,door.closedForRun?"CLOSED":door.runLockName+"\nLOCKED STORAGE");
                }
                EditorUtility.SetDirty(door);PrefabUtility.RecordPrefabInstancePropertyModifications(door);
            }
        }
        static void AddDynamicBlock(GameObject g,Vector3 center,Vector3 size)
        {
            var modifier=g.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;
            var obstacle=g.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=center;obstacle.size=size;obstacle.carving=true;obstacle.carveOnlyStationary=false;
        }
        static void ConfigureTrolleyAndStaff()
        {
            var cart=GameObject.Find("School/Details/Spare key trolley").transform;
            // Re-centre the existing group's pivot without changing child world positions.
            var children=cart.Cast<Transform>().ToArray();foreach(var child in children)child.SetParent(null,true);
            cart.position=run.period.Player.GetComponent<PlayerInventory>().keyItem!=null?Object.FindFirstObjectByType<OfficeKeyPickup>().transform.position: P(269,477);cart.position=new Vector3(cart.position.x,0,cart.position.z);
            foreach(var child in children)child.SetParent(cart,true);
            // Starts inside the office; the caretaker takes it to the dining dock after depositing the phone.
            cart.position=new Vector3(-38f,0,78.384445f);
            run.trolley=cart;run.trolleyDock=Group("Trolley parking place",root,new Vector3(-29.1f,0,46.2f));
            var key=Object.FindFirstObjectByType<OfficeKeyPickup>();key.holdSeconds=1.2f;EditorUtility.SetDirty(key);
            foreach(var label in cart.GetComponentsInChildren<TextMesh>())label.text="CARETAKER'S OFFICE KEY";
            run.caretaker.patrol.Clear();
            Route(run.caretaker,"Collect phone",new Vector3(-17.8f,0,34.2f),7,Vector3.back);
            Route(run.caretaker,"Deposit phone",run.period.officeDrop.position,4,Vector3.left);
            Route(run.caretaker,"Park trolley",run.trolleyDock.position,4,Vector3.forward);
            Route(run.caretaker,"Check dining tables",new Vector3(-19.3f,0,61),7,Vector3.forward);
            Route(run.caretaker,"Check dining entrance",new Vector3(-18.9f,0,74),7,Vector3.right);
            Route(run.caretaker,"Inspect east tables",new Vector3(-8.6f,0,60),7,Vector3.back);
            Route(run.caretaker,"Return to trolley",new Vector3(-28.5f,0,48),7,Vector3.left);
            var senses=new SerializedObject(run.caretaker);senses.FindProperty("sightRange").floatValue=24;senses.FindProperty("sightConeDegrees").floatValue=95;senses.ApplyModifiedProperties();
            EditorUtility.SetDirty(run.caretaker);PrefabUtility.RecordPrefabInstancePropertyModifications(run.caretaker);
            var teacher=run.period.teacher.gameObject;var second=teacher.GetComponent<CaretakerAI>();if(second==null)second=teacher.AddComponent<CaretakerAI>();
            second.enabled=false;second.patrol.Clear();run.secondStaff=second;
            Route(second,"South cross hall patrol",P(659,874),4,Vector3.right);
            Route(second,"East perimeter patrol",P(892,654),4,Vector3.forward);
            Route(second,"North cross hall patrol",P(659,467),4,Vector3.left);
            Route(second,"Central spine patrol",P(538,710),4,Vector3.back);
            EditorUtility.SetDirty(second);PrefabUtility.RecordPrefabInstancePropertyModifications(second);
        }
        static void Route(CaretakerAI ai,string name,Vector3 pos,float dwell,Vector3 facing)
        { ai.patrol.Add(new CaretakerAI.PatrolPoint{point=Group(name,root,pos),dwellSeconds=dwell,faceDirection=facing}); }

        [MenuItem("Confiscated/School Run/Capture Dressed School")]
        public static void Capture()
        {
            Directory.CreateDirectory("D:/Confiscated/Docs/SchoolRun");
            var go=new GameObject("SchoolRun review camera");var camera=go.AddComponent<Camera>();camera.fieldOfView=68;
            try
            {
                Shot("CafeteriaCover",new Vector3(-30.7f,1.65f,62),new Vector3(-27,1.2f,49));
                Shot("DiningStorage",new Vector3(-17,1.65f,49),new Vector3(-10,1.3f,44.6f));
                Shot("ResourcesRoom",P(681,420,1.65f),P(637,357,1.1f));
                Shot("Classroom",P(855,843,1.65f),P(776,780,1));
                Shot("HallwayArt",P(692,1167,1.65f),P(695,1182,2));
            }
            finally{Object.DestroyImmediate(go);}
            void Shot(string name,Vector3 pos,Vector3 look){camera.transform.position=pos;camera.transform.LookAt(look);HallwayPropLibrary.Capture(camera,1500,950,"D:/Confiscated/Docs/SchoolRun/"+name+".png");}
        }
    }
}

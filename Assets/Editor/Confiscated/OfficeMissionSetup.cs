using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Adds the furnished office and classroom to the existing illustrated corridor without rebuilding its art.</summary>
    public static class OfficeMissionSetup
    {
        static Transform chapter;
        static Material wood, trim, ink, paper, steel, green, gold, wall;
        const string MatDir = "Assets/Art/Materials/";
        const string MeshDir = "Assets/Art/Meshes/";

        [MenuItem("Confiscated/Build Office Chapter")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play mode before changing the level.");
            ApplyToScene();
            var surface = Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface != null) surface.BuildNavMesh();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[Confiscated] Office chapter saved: furnished office, spare key, locked door, patrol and return to class.");
        }

        [MenuItem("Confiscated/Refresh Caretaker Timetable")]
        public static void RefreshTimetable()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play mode before changing the timetable.");
            ApplyTimetableToScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            Debug.Log("[Confiscated] Replaced the trolley board with a small handwritten A4 timetable beside the office door.");
        }

        public static void ApplyTimetableToScene()
        {
            Transform trolley=null,door=null;
            foreach(var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(transform.name=="MaintenanceTrolley")trolley=transform;
                if(transform.name=="Caretaker office"||transform.name=="Door_Office_South")door=transform;
            }
            if(door==null)throw new InvalidOperationException("The caretaker's office door is missing.");
            foreach(var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(transform==null||transform.gameObject==null)continue;
                if(transform.name=="CaretakerTimetable"||(trolley!=null&&transform.parent==trolley&&(transform.name=="Clipboard"||transform.name=="RoundsList")))
                    Object.DestroyImmediate(transform.gameObject);
            }
            wood=Load("M_Wood_Desk");paper=Load("M_Chapter_Paper");ink=Load("M_Chapter_Ink");green=Load("M_Chapter_Green");gold=Load("M_Chapter_Brass");steel=Load("M_Chapter_Grey");
            if(trolley!=null){var polish=trolley.Find("Polish");if(polish!=null)polish.localPosition=new Vector3(-.19f,.48f,0);}
            TimetableNotice(door.parent,door);
        }

        public static void ApplyToScene()
        {
            var old = GameObject.Find("OfficeChapter");
            if (old != null) Object.DestroyImmediate(old);
            chapter = new GameObject("OfficeChapter").transform;
            wood = Load("M_Wood_Desk"); trim = Load("M_Painted_Trim_Pencil"); wall = Load("M_Wall_Corridor");
            ink = Tint("M_Chapter_Ink", new Color(.13f,.19f,.28f));
            paper = Tint("M_Chapter_Paper", new Color(1f,.99f,.94f));
            steel = Tint("M_Chapter_Grey", new Color(.62f,.67f,.69f));
            green = Tint("M_Chapter_Green", new Color(.25f,.41f,.36f));
            gold = Tint("M_Chapter_Brass", new Color(.78f,.57f,.20f));

            GameObject.Find("Architecture/Walls/Office_North")?.SetActive(false);
            GameObject.Find("Architecture/Walls/Corridor_West")?.SetActive(false);
            // An actual opening in each wall, with the existing illustrated frame and door leaf.
            Wall("Office_Entrance_Left", new Vector3(-2.45f,1.5f,2.075f), new Vector3(4.36f,3,.15f));
            Wall("Office_Entrance_Right", new Vector3(1.4f,1.5f,2.075f), new Vector3(.50f,3,.15f));
            Wall("Office_Entrance_Above", new Vector3(.44f,2.64f,2.075f), new Vector3(1.44f,.72f,.15f));
            // West corridor wall now has a usable Year 6 opening at z=5.5.
            Wall("Corridor_West_BeforeClass", new Vector3(-1.575f,1.5f,3.37f), new Vector3(.15f,3,2.74f));
            Wall("Corridor_West_AfterClass", new Vector3(-1.575f,1.5f,13.2f), new Vector3(.15f,3,13.90f));
            Wall("Classroom_Entrance_Above", new Vector3(-1.575f,2.64f,5.5f), new Vector3(.15f,.72f,1.52f));

            var ai = Object.FindFirstObjectByType<CaretakerAI>();
            var gm = Object.FindFirstObjectByType<GameManager>();
            var mission = chapter.gameObject.AddComponent<OfficeMission>();
            mission.caretaker = ai;
            gm.officeMission = mission;
            var officeDoor = ConfigureDoor(GameObject.Find("Architecture/Doors/Door_Office_South"), new Vector3(.44f,0,2.04f), 180f, false);
            officeDoor.mission = mission; officeDoor.caretaker = ai; mission.door = officeDoor;
            Sign("CaretakerDoorSign", chapter, new Vector3(-.73f,1.65f,2.18f), 180f, new Vector2(.68f,.32f), "CARETAKER\nSITE OFFICE", .056f);

            FurnishOffice();
            mission.key = MaintenanceTrolley(mission);
            TimetableNotice(chapter,officeDoor.transform);
            BuildClassroom(mission);
            ConfigurePatrol(ai);
            var player = Object.FindFirstObjectByType<FirstPersonController>();
            player.transform.SetPositionAndRotation(new Vector3(-2.35f,0,5.5f), Quaternion.Euler(0,90,0));
            var camera = player.GetComponentInChildren<Camera>();
            camera.transform.localRotation = Quaternion.identity;
            EditorUtility.SetDirty(gm);
            AssetDatabase.SaveAssets();
        }

        static OfficeDoor ConfigureDoor(GameObject root, Vector3 position, float yaw, bool unlocked)
        {
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0,yaw,0));
            root.transform.localScale = new Vector3(1.32f,1.1f,1);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            var door = root.GetComponent<OfficeDoor>();
            if (door == null) door = root.AddComponent<OfficeDoor>();
            door.hinge = root.transform.Find("Hinge1");
            door.hinge.localRotation = Quaternion.identity;
            door.startsUnlocked = unlocked;
            door.caretaker = null;
            door.mission = null;
            var modifier = door.hinge.GetComponent<NavMeshModifier>();
            if (modifier == null) modifier = door.hinge.gameObject.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            // A clear handle on either side, sized in the door's local coordinates.
            var handle = door.hinge.Find("ChapterHandle");
            if (handle != null) Object.DestroyImmediate(handle.gameObject);
            var h = Group("ChapterHandle", door.hinge, Vector3.zero);
            foreach (float z in new[] { -.055f, .055f })
                Box("Handle", h, new Vector3(.77f,.97f,z), new Vector3(.13f,.025f,.035f), gold, false);
            return door;
        }

        static void FurnishOffice()
        {
            Move("Props/Desk", new Vector3(-2.65f,0,-1.13f), 180);
            Move("Props/ConfiscatedBox", new Vector3(-2.12f,.75f,-1.20f), 175);
            var propertyBox=GameObject.Find("Props/ConfiscatedBox");
            var cardboard=Tint("M_Chapter_Cardboard",new Color(.74f,.54f,.34f));
            var toyRed=Tint("M_Chapter_ToyRed",new Color(.65f,.28f,.22f));
            foreach(var r in propertyBox.GetComponentsInChildren<MeshRenderer>())
            {
                if(r.name=="Bottom"||r.name=="Front"||r.name=="Back"||r.name=="Left"||r.name=="Right")
                    IllustratedArtSetup.Tiled(r,cardboard,"Chapter",1f);
                if(r.name=="Placeholder_ToyCar") IllustratedArtSetup.Tiled(r,toyRed,"Chapter",1f);
            }
            Move("Props/Chair_Office", new Vector3(-2.8f,0,-.23f), 0);
            Move("Props/Noticeboard_Office", new Vector3(-1.68f,1.75f,1.89f), 0);
            var mug = GameObject.Find("Props/Placeholder_Mug");
            if (mug != null) mug.SetActive(false);
            // All confiscated toys, including the playable football, live inside the office.
            Move("Gameplay/Football", new Vector3(.85f,.14f,-1.0f), 0);
            Sign("ConfiscationPolicy", chapter, new Vector3(-1.68f,1.72f,1.855f), 0, new Vector2(.78f,.58f),
                "STAFF NOTICE\n\nConfiscated property\nLeave with the caretaker\nfor safekeeping.\n\nCollection: end of day", .043f);

            var storage = Group("MaintenanceShelving", chapter, new Vector3(-4.12f,0,-.05f), -90);
            Box("SideL", storage, new Vector3(-.98f,1.02f,0), new Vector3(.065f,2.04f,.48f), wood);
            Box("SideR", storage, new Vector3(.98f,1.02f,0), new Vector3(.065f,2.04f,.48f), wood);
            Box("Back", storage, new Vector3(0,1.02f,.22f), new Vector3(2f,2.04f,.035f), wood);
            foreach (float y in new[] {.12f,.68f,1.25f,1.9f}) Box("Shelf", storage, new Vector3(0,y,0), new Vector3(2f,.055f,.48f), wood);
            for (int i=0;i<3;i++)
            {
                var crate = Group("StoresBox"+i, storage, new Vector3(-.65f+i*.65f,.71f,0));
                Box("Box", crate, new Vector3(0,.18f,0), new Vector3(.55f,.33f,.35f), trim);
                Sign("Label", crate, new Vector3(0,.19f,-.183f), 0, new Vector2(.43f,.13f), new[] {"BULBS", "FIXINGS", "LOST PROPERTY"}[i], .032f);
            }
            for (int i=0;i<5;i++) Box("MaintenanceBinder", storage, new Vector3(-.65f+i*.17f,1.49f,0), new Vector3(.12f,.42f,.3f), i%2==0?green:steel);
            Box("ToolCase", storage, new Vector3(.58f,.32f,0), new Vector3(.66f,.32f,.36f), green);
            Box("ToolCaseHandle", storage, new Vector3(.58f,.51f,0), new Vector3(.23f,.045f,.07f), ink, false);

            var filing = Group("FilingCabinet", chapter, new Vector3(-.73f,0,-1.64f), 180);
            Box("Cabinet", filing, new Vector3(0,.69f,0), new Vector3(.8f,1.38f,.52f), steel);
            for (int i=0;i<3;i++)
            {
                float y=.27f+i*.43f;
                Box("Drawer", filing, new Vector3(0,y,-.28f), new Vector3(.74f,.38f,.04f), trim);
                Box("Handle", filing, new Vector3(0,y,-.32f), new Vector3(.23f,.035f,.045f), ink, false);
                Sign("DrawerLabel", filing, new Vector3(0,y+.11f,-.307f), 0, new Vector2(.34f,.09f), new[] {"REPAIRS", "ROOM KEYS", "INCIDENTS"}[i], .026f);
            }
            var clock = Group("OfficeClock", chapter, new Vector3(-.72f,2.32f,-1.88f), 180);
            Box("Case", clock, Vector3.zero, new Vector3(.5f,.5f,.07f), wood, false);
            Box("Face", clock, new Vector3(0,0,-.045f), new Vector3(.43f,.43f,.012f), paper, false);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;
                var mark=Box("Tick",clock,new Vector3(Mathf.Sin(a)*.17f,Mathf.Cos(a)*.17f,-.055f),new Vector3(.012f,.035f,.007f),ink,false);
                mark.transform.localRotation=Quaternion.Euler(0,0,-i*30);
            }
            Box("MinuteHand",clock,new Vector3(0,.067f,-.065f),new Vector3(.012f,.14f,.007f),ink,false);
            Box("HourHand",clock,new Vector3(.05f,0,-.068f),new Vector3(.105f,.018f,.007f),ink,false);

            var deskDetails = Group("DeskDetails", chapter, new Vector3(-2.65f,.75f,-1.13f));
            Box("DailyLog",deskDetails,new Vector3(-.30f,.026f,.12f),new Vector3(.36f,.045f,.29f),green,false);
            var log=Sign("LogTitle",deskDetails,new Vector3(-.30f,.052f,.12f),0,new Vector2(.29f,.21f),"SITE LOG\nFriday",.033f);
            log.localRotation=Quaternion.Euler(90,0,0);
            var cup=Group("TeaMug",deskDetails,new Vector3(-.56f,.065f,-.14f));
            Box("Cup",cup,Vector3.zero,new Vector3(.105f,.13f,.105f),trim,false);
            Box("Tea",cup,new Vector3(0,.066f,0),new Vector3(.082f,.006f,.082f),wood,false);
            Box("HandleTop",cup,new Vector3(.073f,.033f,0),new Vector3(.055f,.02f,.026f),trim,false);
            Box("HandleSide",cup,new Vector3(.093f,0,0),new Vector3(.017f,.07f,.026f),trim,false);
            Box("HandleBottom",cup,new Vector3(.073f,-.033f,0),new Vector3(.055f,.02f,.026f),trim,false);
            var lamp=Group("DeskLamp",deskDetails,new Vector3(-.55f,0,.25f));
            Box("Base",lamp,new Vector3(0,.018f,0),new Vector3(.19f,.035f,.16f),green,false);
            Box("Stem",lamp,new Vector3(0,.19f,0),new Vector3(.027f,.35f,.027f),ink,false);
            var shade=Box("Shade",lamp,new Vector3(0,.36f,-.03f),new Vector3(.24f,.11f,.18f),green,false);
            shade.transform.localRotation=Quaternion.Euler(-15,0,0);
            Box("PencilPot",deskDetails,new Vector3(.04f,.065f,-.21f),new Vector3(.095f,.13f,.095f),steel,false);
            for(int i=0;i<3;i++) Box("Pencil",deskDetails,new Vector3(.01f+i*.028f,.18f,-.21f),new Vector3(.014f,.19f,.014f),gold,false);

            var tools=Group("CleaningCorner",chapter,new Vector3(.95f,0,-.35f));
            var broom=Box("BroomHandle",tools,new Vector3(0,.77f,0),new Vector3(.035f,1.4f,.035f),wood,false);
            broom.transform.localRotation=Quaternion.Euler(0,0,-8);
            Box("BroomHead",tools,new Vector3(-.09f,.07f,0),new Vector3(.33f,.12f,.12f),gold);
            Box("Bucket",tools,new Vector3(-.31f,.17f,-.32f),new Vector3(.3f,.34f,.3f),steel);
            Box("BucketInterior",tools,new Vector3(-.31f,.342f,-.32f),new Vector3(.26f,.01f,.26f),ink,false);
            Sign("PropertyShelfSign",chapter,new Vector3(.55f,1.37f,-1.87f),180,new Vector2(.65f,.28f),"CONFISCATED\nSPORTS EQUIPMENT",.038f);
        }

        static OfficeKeyPickup MaintenanceTrolley(OfficeMission mission)
        {
            var trolley=Group("MaintenanceTrolley",chapter,new Vector3(-.99f,0,3.32f),180);
            foreach(float y in new[]{.18f,.82f}) Box("Tray",trolley,new Vector3(0,y,0),new Vector3(.62f,.045f,.44f),steel);
            foreach(float x in new[]{-.27f,.27f}) foreach(float z in new[]{-.18f,.18f})
            {
                Box("Upright",trolley,new Vector3(x,.5f,z),new Vector3(.035f,.78f,.035f),ink);
                Box("Wheel",trolley,new Vector3(x,.07f,z),new Vector3(.08f,.14f,.055f),ink,false);
            }
            Box("Polish",trolley,new Vector3(-.19f,.48f,0),new Vector3(.11f,.24f,.11f),green,false);
            // Lying flat on the trolley's top tray (tray at local y=.82, top surface ~y=.8425), not standing upright.
            // Keep it near the original resting height: too low and the interaction ray clips the player's own body.
            var keyRoot=Group("SpareOfficeKey",chapter,new Vector3(-.93f,.855f,3.24f),180);
            keyRoot.localRotation=Quaternion.Euler(90,180,0);
            // Angular ring and stem use the same drawn brass material as the door handles.
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;
                var segment=Box("Ring",keyRoot,new Vector3(Mathf.Sin(a)*.042f,Mathf.Cos(a)*.042f+.073f,0),new Vector3(.028f,.012f,.012f),gold,false);
                segment.transform.localRotation=Quaternion.Euler(0,0,-i*30);
            }
            Box("Stem",keyRoot,new Vector3(0,-.021f,0),new Vector3(.018f,.15f,.018f),gold,false);
            foreach(float y in new[]{-.053f,-.088f}) Box("Tooth",keyRoot,new Vector3(.02f,y,0),new Vector3(.048f,.017f,.018f),gold,false);
            var hit=keyRoot.gameObject.AddComponent<BoxCollider>();hit.size=new Vector3(.25f,.29f,.16f);
            var key=keyRoot.gameObject.AddComponent<OfficeKeyPickup>();key.mission=mission;
            Sign("SpareKeyLabel",chapter,new Vector3(-.96f,.65f,3.57f),180,new Vector2(.49f,.16f),"CARETAKER'S OFFICE KEY",.031f);
            return key;
        }

        static void TimetableNotice(Transform parent,Transform door)
        {
            var notice=Group("CaretakerTimetable",parent,Vector3.zero);
            Vector3 position=door.position-door.forward*.105f-door.right*1.13f+Vector3.up*1.48f;
            notice.SetPositionAndRotation(position,door.rotation*Quaternion.Euler(0,0,4.5f));
            Box("A4 paper",notice,Vector3.zero,new Vector3(.31f,.44f,.009f),paper,false);
            Box("Top tape",notice,new Vector3(-.065f,.218f,-.009f),new Vector3(.085f,.035f,.006f),gold,false).transform.localRotation=Quaternion.Euler(0,0,-4);
            Box("Bottom tape",notice,new Vector3(.10f,-.215f,-.009f),new Vector3(.072f,.030f,.006f),gold,false).transform.localRotation=Quaternion.Euler(0,0,7);
            Box("Title underline",notice,new Vector3(0,.126f,-.012f),new Vector3(.245f,.006f,.005f),ink,false);
            Box("Dining hall pencil mark",notice,new Vector3(.018f,-.080f,-.013f),new Vector3(.245f,.038f,.004f),gold,false).transform.localRotation=Quaternion.Euler(0,0,-1.5f);
            Hand("Heading",notice,new Vector3(-.125f,.165f,-.017f),"TODAY",.0145f,true);
            string[] lines={"8:45  Year 6","9:05  Office","9:15  West corridor","9:25  Dining hall","9:40  Office"};
            for(int i=0;i<lines.Length;i++)Hand("Note "+i,notice,new Vector3(-.125f,.082f-i*.054f,-.017f),lines[i],.0117f,false);
        }

        static void Hand(string name,Transform parent,Vector3 position,string text,float size,bool bold)
        {
            Label(name,parent,position,text,size,new Color(.035f,.045f,.065f));
            var mesh=parent.Find(name).GetComponent<TextMesh>();mesh.anchor=TextAnchor.MiddleLeft;mesh.alignment=TextAlignment.Left;mesh.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;
        }

        static void BuildClassroom(OfficeMission mission)
        {
            var room=Group("Year6Classroom",chapter,Vector3.zero);
            foreach(string prefab in new[]{"P_Floor_Tile_1m","P_Ceiling_Tile_1m"})
            {
                // Resolve the existing module name through its scene instance, preserving the established artwork.
                var source=GameObject.Find(prefab.Contains("Floor")?"Architecture/Floor":"Architecture/Ceiling").transform.GetChild(0).gameObject;
                var asset=PrefabUtility.GetCorrespondingObjectFromSource(source);
                for(int x=0;x<6;x++)for(int z=0;z<6;z++)
                {
                    var tile=(GameObject)PrefabUtility.InstantiatePrefab(asset,room);
                    tile.transform.localPosition=new Vector3(-7f+x,0,3.5f+z);
                }
            }
            Wall("Classroom_West",new Vector3(-7.575f,1.5f,6),new Vector3(.15f,3,6.15f));
            Wall("Classroom_South",new Vector3(-4.55f,1.5f,2.925f),new Vector3(6.1f,3,.15f));
            Wall("Classroom_North",new Vector3(-4.55f,1.5f,9.075f),new Vector3(6.1f,3,.15f));
            var classroomDoor=ConfigureDoor(GameObject.Find("Architecture/Doors/Door_Year6_West"),new Vector3(-1.495f,0,5.5f),-90,true);
            classroomDoor.openAngle=-102;
            Sign("ClassroomSign",chapter,new Vector3(-1.405f,1.7f,6.46f),-90,new Vector2(.53f,.27f),"YEAR 6\nCLASSROOM",.05f);
            var board=Group("TeachingBoard",room,new Vector3(-7.39f,1.72f,6),-90);
            Box("Frame",board,Vector3.zero,new Vector3(2.35f,1.25f,.07f),wood,false);
            Box("WritingSurface",board,new Vector3(0,0,-.044f),new Vector3(2.20f,1.10f,.018f),green,false);
            Label("BoardWriting",board,new Vector3(0,.13f,-.058f),"WELCOME BACK\n\nFind your seat.\nBook open. Phone away.",.10f,new Color(.92f,.90f,.76f));
            var desks=Group("PupilDesks",room,Vector3.zero);
            for(int row=0;row<2;row++)for(int col=0;col<2;col++)
            {
                Vector3 pos=new Vector3(-5.85f+row*2.1f,0,4.3f+col*3.15f);
                var desk=Prefab("Props/P_Desk",desks,pos,90,"PupilDesk_"+row+col);
                desk.transform.localScale=new Vector3(.72f,1,.8f);
                Prefab("Props/P_Chair",desks,pos+Vector3.right*.78f,90,"PupilChair_"+row+col);
                Box("ExerciseBook",desk.transform,new Vector3(-.18f,.775f,0),new Vector3(.42f,.035f,.31f),green,false);
                Box("Pencil",desk.transform,new Vector3(.14f,.79f,-.09f),new Vector3(.2f,.014f,.014f),gold,false);
                if(row==1&&col==1)
                {
                    var seat=desk.AddComponent<ClassroomSeat>();seat.mission=mission;
                    seat.seatedView=Group("SeatedView",room,pos+new Vector3(.73f,1.18f,0),-90);
                    var name=Sign("YourDesk",desk.transform,new Vector3(0,.79f,.19f),0,new Vector2(.44f,.13f),"YOUR DESK",.04f);
                    name.localRotation=Quaternion.Euler(90,0,0);
                }
            }
            var sourceLight=GameObject.Find("Architecture/CeilingLights").transform.GetChild(0).gameObject;
            var lightAsset=PrefabUtility.GetCorrespondingObjectFromSource(sourceLight);
            foreach(float z in new[]{4.5f,7.5f})
            {
                var l=(GameObject)PrefabUtility.InstantiatePrefab(lightAsset,room);
                l.transform.position=new Vector3(-4.5f,0,z);
            }
        }

        static void ConfigurePatrol(CaretakerAI ai)
        {
            var root=Group("OfficeRounds",chapter,Vector3.zero);
            ai.patrol.Clear();
            AddStop("Desk",new Vector3(-1.5f,0,-.4f),9,Vector3.left);
            AddStop("OutsideOffice",new Vector3(.44f,0,3.1f),.25f,Vector3.forward);
            AddStop("FireDoorCheck",new Vector3(.3f,0,17.4f),9,Vector3.forward);
            AddStop("CorridorCheck",new Vector3(-.35f,0,10.5f),3,Vector3.left);
            AddStop("OfficeReturn",new Vector3(.44f,0,3.1f),.2f,Vector3.back);
            ai.transform.SetPositionAndRotation(new Vector3(-1.5f,.02f,-.4f),Quaternion.Euler(0,-90,0));
            EditorUtility.SetDirty(ai);
            void AddStop(string name,Vector3 position,float dwell,Vector3 facing)
            {
                var point=Group(name,root,position);
                ai.patrol.Add(new CaretakerAI.PatrolPoint{point=point,dwellSeconds=dwell,faceDirection=facing});
            }
        }

        static void Wall(string name,Vector3 position,Vector3 size)
        {
            var go=VisualTestSceneBuilder.Box(name,chapter,position,size,new Vector2(2,3),wall);
            float bottom=position.y-size.y*.5f;
            if(bottom<.01f)return;
            var filter=go.GetComponent<MeshFilter>();var mesh=Object.Instantiate(filter.sharedMesh);
            mesh.name="Chapter_"+name;
            var uv=mesh.uv;var normals=mesh.normals;
            for(int i=0;i<uv.Length;i++)if(Mathf.Abs(normals[i].y)<.5f)uv[i].y+=bottom/3f;
            mesh.uv=uv;
            string path=MeshDir+"Chapter_"+name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);filter.sharedMesh=mesh;}
            else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);filter.sharedMesh=existing;}
        }

        static GameObject Box(string name,Transform parent,Vector3 position,Vector3 size,Material material,bool collider=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());
            IllustratedArtSetup.Tiled(go.GetComponent<MeshRenderer>(),material,"Chapter",1f);
            return go;
        }

        static Transform Sign(string name,Transform parent,Vector3 position,float yaw,Vector2 size,string text,float letterSize)
        {
            var root=Group(name,parent,position,yaw);
            Box("Paper",root,Vector3.zero,new Vector3(size.x,size.y,.009f),paper,false);
            Label("Lettering",root,new Vector3(0,0,-.009f),text,letterSize,new Color(.10f,.15f,.24f));
            return root;
        }

        static void Label(string name,Transform parent,Vector3 position,string text,float size,Color colour)
        {
            var root=Group(name,parent,position);
            var mesh=root.gameObject.AddComponent<TextMesh>();mesh.text=text;
            mesh.font=SchoolTypography.Font;
            mesh.fontSize=64;mesh.characterSize=size*.25f;mesh.anchor=TextAnchor.MiddleCenter;
            mesh.alignment=TextAlignment.Center;mesh.color=colour;mesh.fontStyle=FontStyle.Bold;
            root.gameObject.AddComponent<WorldLabel>();
        }

        static Material Load(string name)=>AssetDatabase.LoadAssetAtPath<Material>(MatDir+name+".mat");
        static Material Tint(string name,Color colour)
        {
            var mat=Load(name);
            if(mat==null){mat=new Material(trim);mat.name=name;AssetDatabase.CreateAsset(mat,MatDir+name+".mat");}
            mat.SetColor("_BaseColor",colour);mat.SetFloat("_EdgeWidth",.010f);EditorUtility.SetDirty(mat);return mat;
        }
        static Transform Group(string name,Transform parent,Vector3 position,float yaw=0)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);return go.transform;
        }
        static GameObject Prefab(string path,Transform parent,Vector3 position,float yaw,string name)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+path+".prefab");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);go.name=name;go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);return go;
        }
        static void Move(string path,Vector3 position,float yaw)
        {
            var go=GameObject.Find(path);if(go==null)return;
            go.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
        }
    }
}

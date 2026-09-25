using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    public static class SchoolLayoutBuilder
    {
        public const string ScenePath="Assets/Scenes/SchoolLayout.unity";
        const string MeshDir="Assets/Art/Meshes/School/";
        static Transform school,floors,ceilings,walls,doors,details;
        static Material wallMat,floorMat,ceilingMat,grassMat,wood,trim;
        static int meshIndex,wallCount;
        static float[] xs,ys;
        static int[,] zones;
        static OfficeMission mission;
        static CaretakerAI caretaker;
        static readonly Dictionary<string,OfficeDoor> builtDoors=new();

        [MenuItem("Confiscated/Build Full School From Plan")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play mode before building the school.");
            EditorSceneManager.SaveOpenScenes();
            var scene=EditorSceneManager.OpenScene(VisualTestSceneBuilder.ScenePath);
            EditorSceneManager.SaveScene(scene,ScenePath);
            System.IO.Directory.CreateDirectory(MeshDir);
            wallMat=Mat("M_Wall_Corridor");floorMat=Mat("M_Floor_Tiles");ceilingMat=Mat("M_Ceiling_Panels");
            grassMat=Mat("M_Chapter_Green");wood=Mat("M_Wood_Desk");trim=Mat("M_Painted_Trim_Pencil");
            meshIndex=0;wallCount=0;builtDoors.Clear();
            school=Group("School",null);floors=Group("Floors",school);ceilings=Group("Ceilings",school);
            walls=Group("Walls",school);doors=Group("Doors",school);details=Group("Details",school);
            mission=Object.FindFirstObjectByType<OfficeMission>();caretaker=mission.caretaker;
            MoveExistingGameplay();
            BuildGrid();BuildFloors();BuildWalls();BuildDoors();BuildLights();BuildLockers();ConfigureMission();
            if(AssetDatabase.LoadAssetAtPath<GameObject>(DetentionRoomSetup.NpcPath)!=null)DetentionRoomSetup.ApplyToScene();
            if(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerLockerSetup.PrefabPath)!=null)PlayerLockerSetup.ApplyToScene();
            if(AssetDatabase.LoadAssetAtPath<GameObject>(SchoolBellSetup.PrefabPath)?.GetComponent<SchoolBell>()!=null)SchoolBellSetup.ApplyToScene();
            if(AssetDatabase.LoadAssetAtPath<GameObject>(DiningHallSetup.TablePath)!=null)DiningHallSetup.ApplyToScene();
            if(AssetDatabase.LoadAssetAtPath<GameObject>(SchoolPeriodSetup.TeacherPath)!=null)SchoolPeriodSetup.ApplyToScene();
            SchoolMusicSetup.ApplyToScene();
            OfficeMissionSetup.ApplyTimetableToScene();
            ClassroomReadabilitySetup.ApplyToScene();
            SeatedStudentSetup.ApplyToScene();
            MainEntranceSetup.ApplyToScene();
            var surface=Object.FindFirstObjectByType<NavMeshSurface>();
            surface.RemoveData();surface.navMeshData=null;
            surface.BuildNavMesh();
            const string navPath="Assets/Scenes/SchoolLayout_NavMesh.asset";
            var data=surface.navMeshData;data.name="SchoolLayout_NavMesh";surface.RemoveData();
            var saved=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if(saved==null){AssetDatabase.CreateAsset(data,navPath);saved=data;}
            else {EditorUtility.CopySerialized(data,saved);Object.DestroyImmediate(data);}
            surface.navMeshData=saved;surface.AddData();
            AddReviewCameras();
            if(AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_Pencil_Sky.mat")!=null)OutdoorArtSetup.Build();
            SchoolTitleSetup.ApplyToScene();
            SchoolLetteringSetup.ApplyToScene();
            if(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/SchoolRun/Nature.png")!=null)SchoolRunSetup.Build();
            ConfiscatedBoxArtSetup.ApplyToScene();
            foreach(var t in details.GetComponentsInChildren<Transform>(true))
                if(PrefabUtility.IsPartOfPrefabInstance(t))PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            PrefabUtility.RecordPrefabInstancePropertyModifications(caretaker.transform);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);
            var builds=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).ToList();
            builds.Insert(0,new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=builds.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[School] Saved full plan: "+SchoolPlan.Doors.Length+" functioning entrances, 14 rooms, 4 open-air yards; "+wallCount+" wall sections.");
        }

        static void BuildGrid()
        {
            xs=SchoolPlan.Areas.SelectMany(a=>new[]{a.rect.xMin,a.rect.xMax}).Distinct().OrderBy(v=>v).ToArray();
            ys=SchoolPlan.Areas.SelectMany(a=>new[]{a.rect.yMin,a.rect.yMax}).Distinct().OrderBy(v=>v).ToArray();
            zones=new int[xs.Length-1,ys.Length-1];
            for(int x=0;x<xs.Length-1;x++)for(int y=0;y<ys.Length-1;y++)
                zones[x,y]=SchoolPlan.ZoneAt((xs[x]+xs[x+1])*.5f,(ys[y]+ys[y+1])*.5f);
        }

        static void BuildFloors()
        {
            var used=new bool[xs.Length-1,ys.Length-1];
            for(int y=0;y<ys.Length-1;y++)for(int x=0;x<xs.Length-1;x++)
            {
                int zone=zones[x,y];if(zone==int.MinValue||used[x,y])continue;
                int endX=x+1;while(endX<xs.Length-1&&zones[endX,y]==zone&&!used[endX,y])endX++;
                int endY=y+1;
                while(endY<ys.Length-1)
                {
                    bool fits=true;for(int k=x;k<endX;k++)if(zones[k,endY]!=zone||used[k,endY]){fits=false;break;}
                    if(!fits)break;endY++;
                }
                for(int i=x;i<endX;i++)for(int j=y;j<endY;j++)used[i,j]=true;
                var centre=SchoolPlan.Point((xs[x]+xs[endX])*.5f,(ys[y]+ys[endY])*.5f);
                var size=new Vector3((xs[endX]-xs[x])/9,.10f,(ys[endY]-ys[y])/9);
                Solid("Floor_"+zone,floors,centre+Vector3.down*.05f,size,zone<0?grassMat:floorMat,false);
                if(zone>=0)Solid("Ceiling",ceilings,centre+Vector3.up*3.05f,size,ceilingMat,false);
            }
        }
        static int Zone(int x,int y)=>x<0||y<0||x>=xs.Length-1||y>=ys.Length-1?int.MinValue:zones[x,y];
        static float BoundaryHeight(int a,int b)
        {
            if(a==b)return 0;
            if((a==int.MinValue&&b<0)||(b==int.MinValue&&a<0))return 1.35f;
            return 3;
        }
        static void BuildWalls()
        {
            for(int x=0;x<xs.Length;x++)
            {
                int start=0;float height=0;
                for(int y=0;y<ys.Length;y++)
                {
                    float h=y==ys.Length-1?0:BoundaryHeight(Zone(x-1,y),Zone(x,y));
                    if(!Mathf.Approximately(h,height))
                    {if(height>0)WallLine(true,xs[x],ys[start],ys[y],height);start=y;height=h;}
                }
            }
            for(int y=0;y<ys.Length;y++)
            {
                int start=0;float height=0;
                for(int x=0;x<xs.Length;x++)
                {
                    float h=x==xs.Length-1?0:BoundaryHeight(Zone(x,y-1),Zone(x,y));
                    if(!Mathf.Approximately(h,height))
                    {if(height>0)WallLine(false,ys[y],xs[start],xs[x],height);start=x;height=h;}
                }
            }
        }
        static void WallLine(bool vertical,float line,float begin,float end,float height)
        {
            var openings=SchoolPlan.Doors.Where(d=>d.vertical==vertical&&Mathf.Abs(d.line-line)<.01f&&d.centre>begin&&d.centre<end).OrderBy(d=>d.centre);
            float cursor=begin;
            foreach(var d in openings)
            {
                float half=d.Gap*9*.5f;
                if(d.centre-half<begin||d.centre+half>end)throw new Exception("Door does not fit wall: "+d.name);
                WallPart(vertical,line,cursor,d.centre-half,0,height);
                if(height>2.29f)WallPart(vertical,line,d.centre-half,d.centre+half,2.29f,height);
                cursor=d.centre+half;
            }
            WallPart(vertical,line,cursor,end,0,height);
        }
        static void WallPart(bool vertical,float line,float begin,float end,float bottom,float top)
        {
            if(end-begin<.01f)return;
            var p=vertical?SchoolPlan.Point(line,(begin+end)*.5f):SchoolPlan.Point((begin+end)*.5f,line);
            var size=vertical?new Vector3(.15f,top-bottom,(end-begin)/9):new Vector3((end-begin)/9,top-bottom,.15f);
            Solid("Wall_"+wallCount++,walls,p+Vector3.up*(bottom+top)*.5f,size,wallMat,true);
        }

        static void BuildDoors()
        {
            foreach(var d in SchoolPlan.Doors)
            {
                int before=d.vertical?SchoolPlan.ZoneAt(d.line-.1f,d.centre):SchoolPlan.ZoneAt(d.centre,d.line-.1f);
                int after=d.vertical?SchoolPlan.ZoneAt(d.line+.1f,d.centre):SchoolPlan.ZoneAt(d.centre,d.line+.1f);
                if(before==after||before==int.MinValue||after==int.MinValue)throw new Exception("Door must connect two spaces: "+d.name);
                float yaw=d.vertical?(before==0?90:-90):(before==0?180:0);
                string prefab=d.doubleDoor?"P_Door_Fire_Double":d.name=="Caretaker office"||d.name=="Store cupboard"?"P_Door_Office":"P_Door_Classroom";
                var go=Prefab("Modular/"+prefab,doors,d.Position,yaw,d.name);
                PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                float width=d.doubleDoor?2.6f:1.5f;
                float original=d.doubleDoor?1.8f:prefab=="P_Door_Office"?.95f:1f;
                go.transform.localScale=new Vector3(width/original,1.1f,1);
                var control=go.AddComponent<OfficeDoor>();control.hinge=go.transform.Find("Hinge1");control.caretaker=caretaker;
                control.startsUnlocked=d.name!="Caretaker office";control.openAngle=-102;
                if(d.doubleDoor)
                {
                    control.secondHinge=go.transform.Find("Hinge2");
                    control.secondHinge.localPosition=new Vector3(.9f,0,0);
                    control.secondHinge.Find("Leaf").localPosition=new Vector3(-.45f,1,0);
                }
                foreach(Transform hinge in new[]{control.hinge,control.secondHinge})
                {
                    if(hinge==null)continue;
                    var mod=hinge.gameObject.AddComponent<NavMeshModifier>();mod.ignoreFromBuild=true;
                }
                if(d.name=="Caretaker office"){control.mission=mission;mission.door=control;}
                builtDoors[d.name]=control;
                // Separate world-space placard above the frame, so its scale stays consistent.
                var sign=Group(d.name+" sign",details);sign.position=d.Position+Vector3.up*2.55f;sign.rotation=Quaternion.Euler(0,yaw,0);
                sign.position-=sign.forward*.10f;
                string caption=d.name.StartsWith("Year 6")?"YEAR 6":d.name=="Caretaker office"?"CARETAKER'S OFFICE":d.name.Contains("yard")?"COURTYARD":d.name.Contains("Dining")?"DINING HALL":d.name=="Store cupboard"?"STORE":"CLASSROOM";
                PencilBox("Plate",sign,Vector3.zero,new Vector3(1.16f,.24f,.02f),trim,false);
                Label(sign,caption,new Vector3(0,0,-.017f),.010f);
            }
        }

        static Vector3 MapRoom(Vector3 old,Vector3 oldCentre,Vector2 oldSize,SchoolPlan.Area room)
        {
            var centre=SchoolPlan.Point(room.rect.center.x,room.rect.center.y,old.y);
            return centre+new Vector3((old.x-oldCentre.x)*room.rect.width/9/oldSize.x,0,(old.z-oldCentre.z)*room.rect.height/9/oldSize.y);
        }
        static void MoveExistingGameplay()
        {
            var office=SchoolPlan.Room(1);var classroom=SchoolPlan.Room(10);
            var officeRoot=Group("Office furnishings",details);var classroomRoot=Group("Year 6 furnishings",details);
            // Desks and chairs must obstruct the route, never become steps for Mr Reed.
            var classroomNavigation=classroomRoot.gameObject.AddComponent<NavMeshModifier>();
            classroomNavigation.overrideArea=true;
            classroomNavigation.area=NavMesh.GetAreaFromName("Not Walkable");
            Vector3 oldDesk=new Vector3(-2.65f,0,-1.13f);
            Vector3 newDesk=SchoolPlan.Point(office.rect.xMin+45,office.rect.yMax-30);
            foreach(string path in new[]{"Props/Desk","Props/ConfiscatedBox","Props/Chair_Office","OfficeChapter/DeskDetails"})
            {
                var t=GameObject.Find(path).transform;Vector3 p=t.position;t.SetParent(officeRoot,true);t.position=newDesk+(p-oldDesk);
            }
            foreach(string name in new[]{"MaintenanceShelving","FilingCabinet","OfficeClock","CleaningCorner","PropertyShelfSign","ConfiscationPolicy"})
            {
                var t=GameObject.Find("OfficeChapter/"+name).transform;var p=MapRoom(t.position,new Vector3(-1.5f,0,0),new Vector2(6,4),office);
                if(name=="MaintenanceShelving")p.x=SchoolPlan.Point(office.rect.xMin,0).x+.3f;
                if(name=="FilingCabinet"||name=="OfficeClock"||name=="PropertyShelfSign")p.z=SchoolPlan.Point(0,office.rect.yMax).z+(name=="FilingCabinet"?.32f:.10f);
                if(name=="ConfiscationPolicy")p.z=SchoolPlan.Point(0,office.rect.yMin).z-.10f;
                t.SetParent(officeRoot,true);t.position=p;
            }
            var notice=GameObject.Find("Props/Noticeboard_Office").transform;
            notice.SetParent(officeRoot,true);notice.position=GameObject.Find("School/Details/Office furnishings/ConfiscationPolicy").transform.position+Vector3.forward*.035f;
            var trolleyRoot=Group("Spare key trolley",details);
            Vector3 oldTrolley=new Vector3(-.99f,0,3.32f);var target=SchoolPlan.Point(243,477);
            foreach(string name in new[]{"MaintenanceTrolley","SpareOfficeKey","SpareKeyLabel"})
            {
                var t=GameObject.Find("OfficeChapter/"+name).transform;var p=t.position;t.SetParent(trolleyRoot,true);
                t.position=target+Quaternion.Euler(0,90,0)*(p-oldTrolley);t.rotation=Quaternion.Euler(0,90,0)*t.rotation;
            }
            var oldClass=GameObject.Find("OfficeChapter/Year6Classroom").transform;
            var pupil=oldClass.Find("PupilDesks");
            foreach(Transform t in pupil.Cast<Transform>().ToArray())
            {var p=t.position;t.SetParent(classroomRoot,true);t.position=MapRoom(p,new Vector3(-4.5f,0,6),new Vector2(6,6),classroom);}
            foreach(Transform t in classroomRoot)
                if(t.name.StartsWith("PupilChair_"))
                    t.position=classroomRoot.Find(t.name.Replace("PupilChair_","PupilDesk_")).position+Vector3.right*.78f;
            var seat=Object.FindFirstObjectByType<ClassroomSeat>();var view=seat.seatedView;
            // Preserve the seated viewpoint's offset from its own desk rather than stretching it with the room.
            view.SetParent(classroomRoot,true);view.position=seat.transform.position+new Vector3(.73f,1.18f,0);
            var board=oldClass.Find("TeachingBoard");var bp=MapRoom(board.position,new Vector3(-4.5f,0,6),new Vector2(6,6),classroom);
            bp.x=SchoolPlan.Point(classroom.rect.xMin,0).x+.1f;board.SetParent(classroomRoot,true);board.position=bp;
            var football=Object.FindFirstObjectByType<ThrowableBall>();
            football.transform.SetParent(officeRoot,true);football.transform.position=SchoolPlan.Point(office.rect.xMax-12,office.rect.yMax-12,.15f);
            // Retain the mission component and its references, replacing the former small-level architecture.
            foreach(Transform child in mission.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
            foreach(string name in new[]{"Architecture","Props","CaptureCameras"}){var go=GameObject.Find(name);if(go!=null)Object.DestroyImmediate(go);}
            var oldPatrol=GameObject.Find("Gameplay/CaretakerPatrol");if(oldPatrol!=null)Object.DestroyImmediate(oldPatrol);
            mission.officeBounds=new Bounds(SchoolPlan.Point(office.rect.center.x,office.rect.center.y,1.5f),new Vector3(office.rect.width/9+.3f,6,office.rect.height/9+.3f));
            caretaker.transform.position=newDesk+new Vector3(1.15f,.02f,.73f);
            var player=Object.FindFirstObjectByType<PlayerInteractor>();
            player.transform.SetPositionAndRotation(SchoolPlan.Point(309,950),Quaternion.Euler(0,-90,0));
            player.ViewCamera.transform.localRotation=Quaternion.identity;
        }

        static void ConfigureMission()
        {
            var patrol=Group("School patrol",school);caretaker.patrol.Clear();
            Stop("Office desk",caretaker.transform.position,8,Vector3.left);
            Stop("Outside office",SchoolPlan.Point(280,444),.3f,Vector3.forward);
            Stop("North west",SchoolPlan.Point(280,270),2,Vector3.right);
            Stop("North east",SchoolPlan.Point(892,270),6,Vector3.back);
            Stop("East junction",SchoolPlan.Point(892,466),2,Vector3.left);
            Stop("Central junction",SchoolPlan.Point(539,467),3,Vector3.back);
            Stop("South junction",SchoolPlan.Point(539,874),3,Vector3.left);
            Stop("West junction",SchoolPlan.Point(280,874),3,Vector3.forward);
            Stop("Office approach",SchoolPlan.Point(280,444),.3f,Vector3.left);
            EditorUtility.SetDirty(caretaker);
            PrefabUtility.RecordPrefabInstancePropertyModifications(caretaker);
            EditorUtility.SetDirty(mission);
            void Stop(string name,Vector3 p,float wait,Vector3 facing)
            {var t=Group(name,patrol);t.position=p;caretaker.patrol.Add(new CaretakerAI.PatrolPoint{point=t,dwellSeconds=wait,faceDirection=facing});}
        }
        // Room fixtures now use a spacing grid (matching the existing corridor spacing constant) instead of one
        // fixture per room, so a dimmed ambient/KeyLight leaves the ceiling fluorescents as the real light source
        // across large rooms, not just their centre. Dining hall (zone 6) keeps its own dedicated dense grid from
        // DiningHallSetup, so it's skipped here to avoid a redundant overlapping fixture.
        const float LightSpacing=54;
        static void BuildLights()
        {
            var lighting=Group("Ceiling lights",school);var placed=new List<Vector3>();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Modular/P_CeilingLight.prefab");
            if(source==null)throw new Exception("Existing ceiling-light prefab missing");
            foreach(var area in SchoolPlan.Areas.Where(a=>a.zone>=0&&a.zone!=6))
            {
                var r=area.rect;
                if(area.zone==0)
                {
                    int count=Mathf.Max(1,Mathf.FloorToInt(Mathf.Max(r.width,r.height)/LightSpacing));
                    for(int i=0;i<count;i++)
                    {
                        float f=(i+.5f)/count;var p=SchoolPlan.Point(r.width>r.height?Mathf.Lerp(r.xMin,r.xMax,f):r.center.x,r.width>r.height?r.center.y:Mathf.Lerp(r.yMin,r.yMax,f));
                        Place(p,r);
                    }
                }
                else
                {
                    int cols=Mathf.Max(1,Mathf.FloorToInt(r.width/LightSpacing));
                    int rows=Mathf.Max(1,Mathf.FloorToInt(r.height/LightSpacing));
                    for(int cx=0;cx<cols;cx++)for(int cy=0;cy<rows;cy++)
                    {
                        var p=SchoolPlan.Point(Mathf.Lerp(r.xMin,r.xMax,(cx+.5f)/cols),Mathf.Lerp(r.yMin,r.yMax,(cy+.5f)/rows));
                        Place(p,r);
                    }
                }
            }
            void Place(Vector3 p,Rect r)
            {
                if(placed.Any(q=>Vector3.Distance(q,p)<4))return;placed.Add(p);
                var go=(GameObject)PrefabUtility.InstantiatePrefab(source,lighting);go.transform.position=p;
                if(r.width>r.height)go.transform.rotation=Quaternion.Euler(0,90,0);
            }
        }
        // Regenerates just the ceiling-light fixtures in the already-built live scene, without a full
        // SchoolLayoutBuilder.Build() rebuild (the live scene has diverged from the builder in other respects).
        public static void RefreshLights()
        {
            if(school==null)school=GameObject.Find("School")?.transform;
            if(school==null)throw new Exception("School root not found - open SchoolLayout and build it first.");
            var existing=new List<GameObject>();
            foreach(Transform t in school)if(t.name=="Ceiling lights")existing.Add(t.gameObject);
            foreach(var go in existing)Object.DestroyImmediate(go);
            BuildLights();
        }
        static void BuildLockers()
        {
            var mat=new Material(trim);mat.SetColor("_BaseColor",new Color(.57f,.22f,.20f));mat.name="M_School_Locker";
            const string path="Assets/Art/Materials/M_School_Locker.mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(old==null)AssetDatabase.CreateAsset(mat,path);else{EditorUtility.CopySerialized(mat,old);Object.DestroyImmediate(mat);mat=old;}
            Bank(326,257,0,7);Bank(834,257,0,8);Bank(902,516,90,9);Bank(902,800,90,8);
            Bank(269,843,-90,9);Bank(269,1115,-90,8);Bank(400,1177,180,7);Bank(815,1177,180,8);
            void Bank(float px,float py,float yaw,int count)
            {
                var row=Group("Locker bank",details);row.position=SchoolPlan.Point(px,py);row.rotation=Quaternion.Euler(0,yaw,0);
                for(int i=0;i<count;i++)
                {
                    float x=(i-(count-1)*.5f)*.58f;
                    PencilBox("Locker",row,new Vector3(x,.92f,0),new Vector3(.56f,1.84f,.36f),mat,true);
                    PencilBox("Handle",row,new Vector3(x+.17f,.96f,-.196f),new Vector3(.025f,.12f,.027f),Mat("M_Chapter_Ink"),false);
                }
            }
        }
        static void AddReviewCameras()
        {
            var group=Group("SchoolReviewCameras",null);
            var overhead=new GameObject("Plan overhead");overhead.transform.SetParent(group);var camera=overhead.AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=76;camera.nearClipPlane=.1f;camera.farClipPlane=250;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.25f,.34f);camera.enabled=false;
            overhead.transform.SetPositionAndRotation(SchoolPlan.Point(585,667,180),Quaternion.Euler(90,0,0));
            Add("North hallway",SchoolPlan.Point(320,270,1.55f),SchoolPlan.Point(880,270,1.55f));
            Add("Central crossroads",SchoolPlan.Point(539,654,1.55f),SchoolPlan.Point(703,654,1.55f));
            Add("Office approach",SchoolPlan.Point(281,493,1.55f),SchoolPlan.Point(264,444,1.2f));
            Add("South hallway",SchoolPlan.Point(320,1167,1.55f),SchoolPlan.Point(880,1167,1.55f));
            void Add(string name,Vector3 position,Vector3 target)
            {
                var go=new GameObject(name);go.transform.SetParent(group);var cam=go.AddComponent<Camera>();cam.CopyFrom(Camera.main);
                go.transform.position=position;go.transform.LookAt(target);cam.ResetWorldToCameraMatrix();cam.enabled=false;cam.fieldOfView=68;
                go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
            }
        }

        static GameObject Solid(string name,Transform parent,Vector3 p,Vector3 size,Material mat,bool vertical)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=p;go.transform.localScale=Vector3.one;
            var filter=go.GetComponent<MeshFilter>();var mesh=Object.Instantiate(filter.sharedMesh);mesh.name="School_"+meshIndex++;
            var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
            for(int i=0;i<vertices.Length;i++)
            {
                vertices[i]=Vector3.Scale(vertices[i],size);Vector3 w=p+vertices[i];
                uv[i]=Mathf.Abs(normals[i].y)>.5f?new Vector2(w.x,w.z):new Vector2((Mathf.Abs(normals[i].x)>.5f?w.z:w.x)/2,w.y/3);
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.RecalculateBounds();mesh.RecalculateTangents();
            string path=MeshDir+mesh.name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);}
            filter.sharedMesh=saved;go.GetComponent<BoxCollider>().size=size;go.GetComponent<MeshRenderer>().sharedMaterial=mat;
            if(mat.shader.name=="Confiscated/Pencil Surface")
                IllustratedArtSetup.Tiled(go.GetComponent<MeshRenderer>(),mat,"SchoolGround",1f);
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic);
            return go;
        }
        static GameObject PencilBox(string name,Transform parent,Vector3 p,Vector3 size,Material mat,bool collider)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;
            if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());
            IllustratedArtSetup.Tiled(go.GetComponent<MeshRenderer>(),mat,"SchoolDetail",1);return go;
        }
        static void Label(Transform parent,string text,Vector3 p,float size)
        {
            var t=Group("Lettering",parent);t.localPosition=p;var mesh=t.gameObject.AddComponent<TextMesh>();
            mesh.font=SchoolTypography.Font;mesh.fontSize=64;mesh.characterSize=size;mesh.text=text;
            mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.color=new Color(.1f,.16f,.24f);mesh.fontStyle=FontStyle.Bold;t.gameObject.AddComponent<WorldLabel>();
        }
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+name+".mat");
        static Transform Group(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static GameObject Prefab(string path,Transform parent,Vector3 p,float yaw,string name)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+path+".prefab");var go=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);
            go.name=name;go.transform.position=p;go.transform.rotation=Quaternion.Euler(0,yaw,0);return go;
        }
    }
}


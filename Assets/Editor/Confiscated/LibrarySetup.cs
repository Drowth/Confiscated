using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// The library (Docs/LibraryMaze.md): the old classrooms 9 and 10 and the North link A strip between them, already one
    /// open 32 x 18 m room. Opens three of its four doors (shut during lessons, open for the run), marks its floor as a
    /// NavMesh area no staff agent may walk (caretaker, Mr Reed, coach), fills it with a dim maze of bookcases on a
    /// fixed-seed grid, and moves the handheld game (returns desk in the centre) and the glue (a west-side dead end) in.
    /// Placeholder bookcases until the Astra shelf art arrives. Chained last in SchoolRunSetup.Build.
    /// </summary>
    public static class LibrarySetup
    {
        public const string RootName="Library";
        public static readonly string[] Doors={"North room A west","North room A north","North classroom A south"};
        public const string ShutDoor="North classroom A north";
        public const int NavArea=3;
        // Inner faces of the room walls.
        const float X0=-32.04f,X1=-.28f,Z0=81.52f,Z1=99.59f;
        public static readonly Bounds Interior=new Bounds(new Vector3((X0+X1)/2,1.5f,(Z0+Z1)/2),new Vector3(X1-X0,4f,Z1-Z0));
        public const int Cols=16,Rows=9,Seed=20260926;
        static float CellW=>(X1-X0)/Cols;
        static float CellH=>(Z1-Z0)/Rows;
        public static Vector3 Cell(int c,int r,float y=0)=>new Vector3(X0+(c+.5f)*CellW,y,Z0+(r+.5f)*CellH);
        // Shelves reach the 3 m ceiling (a 5 cm gap keeps the tops out of it): nothing to see over, so the maze is a maze.
        const float ShelfDepth=.4f,ShelfHeight=2.95f;
        // Centre clearing (returns desk) and where each door comes in.
        static readonly RectInt Clearing=new RectInt(7,3,2,3);

        [MenuItem("Confiscated/Library/Apply Library Room")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing.");
            ApplyToScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            Debug.Log("[Library] Library doors, staff area, maze (seed "+ChosenSeed+"), lighting and pickups applied; navigation rebaked.");
        }

        public static void ApplyToScene()
        {
            var run=Object.FindFirstObjectByType<SchoolRunController>();
            if(run==null)throw new InvalidOperationException("Open the SchoolLayout scene first.");
            var old=run.transform.Find(RootName);if(old!=null)Object.DestroyImmediate(old.gameObject);
            var root=new GameObject(RootName).transform;root.SetParent(run.transform,false);

            OpenDoors(run);
            StaffArea(root);
            LoadBookcase();
            var walls=BuildMaze(root,out var desk,out var glueSpot);
            Lighting(root,desk);
            Windows(root);
            MovePickups(run,desk,glueSpot);
            // Door signs said CLASSROOM 9 / 10.
            foreach(var sign in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include).Where(t=>t.text=="CLASSROOM 9"||t.text=="CLASSROOM 10"))
            {sign.text="LIBRARY";EditorUtility.SetDirty(sign);}
            DiningHallSetup.Rebake();
            Shadow(root,run);
        }

        static void OpenDoors(SchoolRunController run)
        {
            foreach(var name in Doors)
            {
                var door=Object.FindObjectsByType<OfficeDoor>(FindObjectsInactive.Include).FirstOrDefault(d=>d.name==name);
                if(door==null)throw new InvalidOperationException("Library door "+name+" is missing.");
                // Shut while the errand is on (the west wing only), then an ordinary door for the run.
                door.closedForRun=false;door.closedDuringLessons=true;door.startsUnlocked=true;door.runRequiredLevel=0;
                EditorUtility.SetDirty(door);PrefabUtility.RecordPrefabInstancePropertyModifications(door);
                var blocker=run.transform.Find("Door blocker - "+name);if(blocker!=null)Object.DestroyImmediate(blocker.gameObject);
                foreach(var t in door.GetComponentsInChildren<Transform>(true).Where(t=>t.name==ClosedDoorSignageSetup.GroupName||t.name=="School door notice").ToArray())
                    Object.DestroyImmediate(t.gameObject);
            }
        }

        /// <summary>The floor is its own NavMesh area, left out of every staff agent's mask: they stop at the doors; the
        /// player (not a NavMesh agent) and path queries on all areas still go in.</summary>
        static void StaffArea(Transform root)
        {
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            var areaName=settings.FindProperty("areas").GetArrayElementAtIndex(NavArea).FindPropertyRelative("name");
            if(areaName.stringValue!="Library"){areaName.stringValue="Library";settings.ApplyModifiedPropertiesWithoutUndo();}
            var volume=new GameObject("Library NavMesh area").AddComponent<NavMeshModifierVolume>();volume.transform.SetParent(root,false);
            volume.transform.position=new Vector3(Interior.center.x,0,Interior.center.z);
            volume.center=new Vector3(0,1,0);volume.size=new Vector3(Interior.size.x,2.5f,Interior.size.z);volume.area=NavArea;
            foreach(var agent in Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include))
            {agent.areaMask=NavMesh.AllAreas&~(1<<NavArea);EditorUtility.SetDirty(agent);PrefabUtility.RecordPrefabInstancePropertyModifications(agent);}
        }

        // ---------- Maze ----------
        // Walls between cells: east[c,r] separates (c,r) and (c+1,r); north[c,r] separates (c,r) and (c,r+1).
        // Door entry cells (each door straddles two cells).
        static readonly Vector2Int[][] DoorCells={new[]{new Vector2Int(0,3),new Vector2Int(0,4)},new[]{new Vector2Int(7,Rows-1),new Vector2Int(8,Rows-1)},new[]{new Vector2Int(11,0),new Vector2Int(12,0)}};
        public const int MinDoorToDesk=12,MinDoorToGlue=9;
        public static int ChosenSeed{get;private set;}
        static bool[,] mazeEast,mazeNorth;static Vector2Int mazeGlue;

        static List<(Vector3 centre,bool alongX)> BuildMaze(Transform root,out Vector3 desk,out Vector3 glueSpot)
        {
            // Try seeds in order until no door is a short walk from the returns desk; deterministic, so rebuilds match.
            bool[,] east=null,north=null;Vector2Int glueCell=default;ChosenSeed=0;
            for(int seed=Seed;seed<Seed+2000;seed++)
            {
                Generate(seed,out east,out north);
                int toDesk=DoorCells.Min(d=>{var dist=Distance(east,north,d);var cells=new List<Vector2Int>();foreach(var p in Clearing.allPositionsWithin)cells.Add(p);return cells.Min(p=>dist[p.x,p.y]);});
                if(toDesk<MinDoorToDesk)continue;
                if(!FarDeadEnd(east,north,out glueCell,out int toGlue)||toGlue<MinDoorToGlue)continue;
                ChosenSeed=seed;mazeEast=east;mazeNorth=north;mazeGlue=glueCell;break;
            }
            if(ChosenSeed==0)throw new InvalidOperationException("No maze seed met the route-length rules; relax MinDoorToDesk.");

            var maze=new GameObject("Maze").transform;maze.SetParent(root,false);
            var shelf=Placeholder("M_Library_Shelf_Placeholder",new Color(.58f,.44f,.3f));
            var walls=new List<(Vector3,bool)>();
            for(int c=0;c<Cols;c++)for(int r=0;r<Rows;r++)
            {
                if(east[c,r]){var p=new Vector3(X0+(c+1)*CellW,ShelfHeight/2,Z0+(r+.5f)*CellH);Shelf(maze,p,new Vector3(ShelfDepth,ShelfHeight,CellH+ShelfDepth),shelf);walls.Add((p,false));}
                if(north[c,r]){var p=new Vector3(X0+(c+.5f)*CellW,ShelfHeight/2,Z0+(r+1)*CellH);Shelf(maze,p,new Vector3(CellW+ShelfDepth,ShelfHeight,ShelfDepth),shelf);walls.Add((p,true));}
            }
            // Returns desk in the clearing, open on its south side.
            desk=new Vector3(X0+Clearing.center.x*CellW,0,Z0+(Clearing.center.y+.35f)*CellH);
            Box(root,"Returns desk (placeholder)",desk+Vector3.up*.45f,new Vector3(1.6f,.9f,.7f),Placeholder("M_Library_Desk_Placeholder",new Color(.45f,.32f,.2f)));
            glueSpot=Cell(glueCell.x,glueCell.y);
            return walls;
        }

        /// <summary>A bookcase, trimmed to the room: the overlap that joins shelves at corners must not poke through the outer walls.</summary>
        static void Shelf(Transform parent,Vector3 at,Vector3 size,Material m)
        {
            float x0=Mathf.Max(at.x-size.x/2,X0+.02f),x1=Mathf.Min(at.x+size.x/2,X1-.02f),z0=Mathf.Max(at.z-size.z/2,Z0+.02f),z1=Mathf.Min(at.z+size.z/2,Z1-.02f);
            var box=Box(parent,"Bookcase",new Vector3((x0+x1)/2,at.y,(z0+z1)/2),new Vector3(x1-x0,size.y,z1-z0),m);
            // The box stays as the wall's collision and NavMesh shape; the model bookcases dress both faces.
            if(bookcase==null)return;
            box.GetComponent<MeshRenderer>().enabled=false;
            bool alongX=x1-x0>z1-z0;float length=alongX?x1-x0:z1-z0,thick=alongX?z1-z0:x1-x0;
            Vector3 dir=alongX?Vector3.right:Vector3.forward,normal=alongX?Vector3.forward:Vector3.right,centre=box.transform.position;centre.y=0;
            int bays=Mathf.Max(1,Mathf.RoundToInt(length/BayWidth));float bay=length/bays;
            foreach(float side in new[]{-1f,1f})for(int k=0;k<bays;k++)
            {
                var g=(GameObject)PrefabUtility.InstantiatePrefab(bookcase,box.transform.parent);g.name="Bookcase model";
                // Pivot at the bottom centre of its back: back to back on the wall's centre line, fronts facing out.
                g.transform.SetPositionAndRotation(centre+dir*(-length/2+(k+.5f)*bay),Quaternion.LookRotation(normal*side));
                g.transform.localScale=new Vector3(bay/bookcaseSize.x,ShelfHeight/bookcaseSize.y,(thick/2)/bookcaseSize.z);
                var lods=g.GetComponent<LODGroup>();
                if(lods!=null){var r0=g.GetComponentsInChildren<Renderer>().Where(r=>r.name.EndsWith("LOD0")).ToArray();var r1=g.GetComponentsInChildren<Renderer>().Where(r=>r.name.EndsWith("LOD1")).ToArray();
                    lods.SetLODs(new[]{new LOD(.28f,r0),new LOD(.01f,r1)});}
                foreach(var r in g.GetComponentsInChildren<Renderer>())r.sharedMaterial=r.name.EndsWith("LOD1")?bookcaseCard:bookcaseLit;
            }
        }
        // The Tripo bookcase (Docs/LibraryMaze.md): LOD0 the full model, LOD1 a box carrying a flat render of it.
        const string BookcaseModel="Assets/Art/Models/Library/Bookcase.fbx",BookcaseTextures="Assets/Art/Models/Library/Textures/";
        const float BayWidth=1.2f;
        static GameObject bookcase;static Material bookcaseLit,bookcaseCard;static Vector3 bookcaseSize;
        static void LoadBookcase()
        {
            bookcase=AssetDatabase.LoadAssetAtPath<GameObject>(BookcaseModel);if(bookcase==null){Debug.LogWarning("[Library] No bookcase model; placeholder shelves.");return;}
            var normalImport=(TextureImporter)AssetImporter.GetAtPath(BookcaseTextures+"T_Bookcase_Normal.png");
            if(normalImport!=null&&normalImport.textureType!=TextureImporterType.NormalMap){normalImport.textureType=TextureImporterType.NormalMap;normalImport.SaveAndReimport();}
            bookcaseLit=Lit("M_Library_Bookcase","T_Bookcase_Color","T_Bookcase_Normal");bookcaseCard=Lit("M_Library_BookcaseCard","T_Bookcase_Card",null);
            // Size from the mesh at rest (LOD0), so any re-export keeps fitting.
            var probe=(GameObject)Object.Instantiate(bookcase);var rs=probe.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            bookcaseSize=b.size;Object.DestroyImmediate(probe);
        }
        static Material Lit(string name,string colour,string normal)
        {
            string path="Assets/Art/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(BookcaseTextures+colour+".png"));m.SetColor("_BaseColor",Color.white);
            if(normal!=null){m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(BookcaseTextures+normal+".png"));m.EnableKeyword("_NORMALMAP");}
            m.SetFloat("_Smoothness",.08f);m.SetFloat("_Metallic",0);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        static void Generate(int seed,out bool[,] east,out bool[,] north)
        {
            east=new bool[Cols,Rows];north=new bool[Cols,Rows];
            for(int c=0;c<Cols;c++)for(int r=0;r<Rows;r++){east[c,r]=c<Cols-1;north[c,r]=r<Rows-1;}
            var rng=new System.Random(seed);
            // Recursive backtracker: one route everywhere.
            var seen=new bool[Cols,Rows];var stack=new Stack<Vector2Int>();stack.Push(new Vector2Int(0,4));seen[0,4]=true;
            while(stack.Count>0)
            {
                var at=stack.Peek();var next=new List<Vector2Int>();
                foreach(var d in Steps)
                {var n=at+d;if(n.x>=0&&n.y>=0&&n.x<Cols&&n.y<Rows&&!seen[n.x,n.y])next.Add(n);}
                if(next.Count==0){stack.Pop();continue;}
                var go=next[rng.Next(next.Count)];Open(east,north,at,go);seen[go.x,go.y]=true;stack.Push(go);
            }
            // A few extra gaps so there is more than one way round (and a way out when something is in the way).
            for(int c=0;c<Cols;c++)for(int r=0;r<Rows;r++)
            {
                if(east[c,r]&&rng.NextDouble()<.1)east[c,r]=false;
                if(north[c,r]&&rng.NextDouble()<.1)north[c,r]=false;
            }
            // Centre clearing and the three doorways (each door straddles two cells).
            for(int c=Clearing.xMin;c<Clearing.xMax;c++)for(int r=Clearing.yMin;r<Clearing.yMax;r++)
            {if(c<Clearing.xMax-1)east[c,r]=false;if(r<Clearing.yMax-1)north[c,r]=false;}
            north[0,3]=false;            // west door, rows 3-4
            east[7,Rows-1]=false;        // north door, cols 7-8
            east[11,0]=false;            // south door, cols 11-12
        }
        static readonly Vector2Int[] Steps={Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down};
        static void Open(bool[,] east,bool[,] north,Vector2Int a,Vector2Int b)
        {
            if(a.x!=b.x)east[Mathf.Min(a.x,b.x),a.y]=false;else north[a.x,Mathf.Min(a.y,b.y)]=false;
        }
        static bool Wall(bool[,] east,bool[,] north,int c,int r,Vector2Int d)=>d.x==1?c==Cols-1||east[c,r]:d.x==-1?c==0||east[c-1,r]:d.y==1?r==Rows-1||north[c,r]:r==0||north[c,r-1];
        /// <summary>Walking distance in cells from a door's entry cells to every cell.</summary>
        static int[,] Distance(bool[,] east,bool[,] north,Vector2Int[] from)
        {
            var dist=new int[Cols,Rows];for(int c=0;c<Cols;c++)for(int r=0;r<Rows;r++)dist[c,r]=int.MaxValue;
            var q=new Queue<Vector2Int>();foreach(var f in from){dist[f.x,f.y]=0;q.Enqueue(f);}
            while(q.Count>0){var a=q.Dequeue();foreach(var d in Steps){if(Wall(east,north,a.x,a.y,d))continue;var n=a+d;if(dist[n.x,n.y]==int.MaxValue){dist[n.x,n.y]=dist[a.x,a.y]+1;q.Enqueue(n);}}}
            return dist;
        }
        /// <summary>The glue goes in the dead end furthest (on foot) from every door, outside the clearing.</summary>
        static bool FarDeadEnd(bool[,] east,bool[,] north,out Vector2Int best,out int bestDist)
        {
            var fromDoors=DoorCells.Select(d=>Distance(east,north,d)).ToArray();
            best=default;bestDist=-1;
            for(int c=0;c<Cols;c++)for(int r=0;r<Rows;r++)
            {
                if(Clearing.Contains(new Vector2Int(c,r))||Steps.Count(d=>Wall(east,north,c,r,d))!=3)continue;
                int nearest=fromDoors.Min(dist=>dist[c,r]);
                if(nearest!=int.MaxValue&&nearest>bestDist){bestDist=nearest;best=new Vector2Int(c,r);}
            }
            return bestDist>=0;
        }

        static void Lighting(Transform root,Vector3 desk)
        {
            // The room's own ceiling fixtures go dark; a few dim lamps keep it readable, never bright.
            foreach(var fixture in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t=>t.name.StartsWith("P_CeilingLight")&&Interior.Contains(new Vector3(t.position.x,1.5f,t.position.z))))
            {fixture.gameObject.SetActive(false);EditorUtility.SetDirty(fixture.gameObject);}
            var lamps=new GameObject("Dim lamps").transform;lamps.SetParent(root,false);
            // Dark: hard to see without the torch (and the torch gives you away to the shadow). One weak lamp on the returns
            // desk and a handful of faint pools so the maze is not pitch black.
            // Pitch dark without the torch (-5 EV): no reading lamps, only a faint glow over the returns desk as a landmark.
            Lamp(lamps,"Returns desk lamp",desk+new Vector3(-.5f,1.3f,.1f),new Color(1f,.78f,.5f),6f,4.5f,LightShadows.Soft);
            Darkness(root);
        }
        const string DarknessProfile="Assets/Settings/LibraryDarkness.asset";
        /// <summary>An exposure-darkening Volume faded in by position (LibraryDarkness), not by a trigger collider.</summary>
        static void Darkness(Transform root)
        {
            var profile=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(DarknessProfile);
            if(profile==null){profile=ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();AssetDatabase.CreateAsset(profile,DarknessProfile);}
            if(!profile.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var grade)){grade=profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);AssetDatabase.AddObjectToAsset(grade,profile);}
            grade.postExposure.Override(-5f);grade.saturation.Override(-30f);EditorUtility.SetDirty(grade);EditorUtility.SetDirty(profile);
            var g=new GameObject("Library darkness");g.transform.SetParent(root,false);
            var volume=g.AddComponent<UnityEngine.Rendering.Volume>();volume.isGlobal=true;volume.priority=20;volume.weight=0;volume.sharedProfile=profile;
            var dark=g.AddComponent<LibraryDarkness>();dark.room=Interior;EditorUtility.SetDirty(dark);
        }
        static void Lamp(Transform parent,string name,Vector3 at,Color colour,float intensity,float range,LightShadows shadows)
        {
            var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.position=at;
            var l=g.AddComponent<Light>();l.type=LightType.Point;l.color=colour;l.intensity=intensity;l.range=range;l.shadows=shadows;
        }

        static void MovePickups(SchoolRunController run,Vector3 desk,Vector3 glueSpot)
        {
            var handheld=Object.FindObjectsByType<RunPickup>(FindObjectsInactive.Include).First(p=>p.itemId==2);
            handheld.transform.position=desk+new Vector3(0,.9f,0);EditorUtility.SetDirty(handheld.transform);
            var glue=run.GetComponentInChildren<GluePickup>(true);
            if(glue==null)throw new InvalidOperationException("The glue pickup is missing; run School Run/Install Glue Pickup.");
            // A little reading table in the dead end, glue on top.
            Box(run.transform.Find(RootName),"Glue table (placeholder)",glueSpot+Vector3.up*.45f,new Vector3(.7f,.9f,.5f),Placeholder("M_Library_Desk_Placeholder",new Color(.45f,.32f,.2f)));
            glue.transform.position=glueSpot+new Vector3(0,.92f,0);glue.name="Glue bottle - Library";EditorUtility.SetDirty(glue.gameObject);
        }

        // ---------- Windows (corridor side) ----------
        // South wall looks onto the north cross hall, north wall onto the north hall. Centres along x (world).
        public static readonly (float z,float x)[] WindowSpots={(81.445f,-27f),(81.445f,-20.5f),(81.445f,-13.5f),(99.665f,-27.5f),(99.665f,-21.5f),(99.665f,-9f)};
        public const float WindowWidth=1.6f,Sill=1.0f,Lintel=2.05f;
        static void Windows(Transform root)
        {
            var group=new GameObject("Windows").transform;group.SetParent(root,false);
            var walls=Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Where(r=>r.transform.parent!=null&&r.transform.parent.name=="Walls").ToList();
            var glass=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_Office_ScreenGlass.mat");
            var frame=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_Painted_Trim_Pencil.mat");
            foreach(var line in WindowSpots.GroupBy(w=>w.z))
            {
                foreach(var wall in walls)
                {
                    // The layout builder's wall, even if an earlier run hid it: its box from the mesh, not the (inactive) renderer.
                    var mesh=wall.GetComponent<MeshFilter>().sharedMesh;var local=mesh.bounds;
                    Vector3 lo=wall.transform.TransformPoint(local.min),hi=wall.transform.TransformPoint(local.max);
                    Vector3 min=Vector3.Min(lo,hi),max=Vector3.Max(lo,hi);
                    if(Mathf.Abs((min.z+max.z)/2-line.Key)>.1f||max.y-min.y<2.9f)continue;
                    var cuts=line.Select(w=>w.x).Where(x=>x-WindowWidth/2>min.x+.3f&&x+WindowWidth/2<max.x-.3f).OrderBy(x=>x).ToArray();
                    if(cuts.Length==0)continue;
                    wall.gameObject.SetActive(false);EditorUtility.SetDirty(wall.gameObject);
                    var mat=wall.sharedMaterial;float z=(min.z+max.z)/2,depth=max.z-min.z,h=max.y;float from=min.x;
                    // The wall's own texture mapping (u runs with world x, v = height / 3 m), so the pieces round the windows
                    // line up with the rest of it; a Cube primitive squashes the whole texture onto each face, upside down.
                    var wv=mesh.vertices;var wuv=mesh.uv;var wn=mesh.normals;
                    var faces=Enumerable.Range(0,wv.Length).Where(i=>Mathf.Abs(wn[i].z)>.9f).ToList();
                    int ia=faces.First(),ib=faces.First(i=>Mathf.Abs(wall.transform.TransformPoint(wv[i]).x-wall.transform.TransformPoint(wv[ia]).x)>.5f);
                    float xa=wall.transform.TransformPoint(wv[ia]).x,xb=wall.transform.TransformPoint(wv[ib]).x;
                    float perMetre=(wuv[ib].x-wuv[ia].x)/(xb-xa),u0=wuv[ia].x-perMetre*xa;
                    void Piece(float x0,float x1,float y0,float y1)
                    {
                        var g=new GameObject("Library wall ("+wall.name+")");g.transform.SetParent(group,false);g.transform.position=new Vector3(0,0,z);
                        g.AddComponent<MeshFilter>().sharedMesh=WallPiece(x0,x1,y0,y1,depth,x=>u0+perMetre*x);
                        g.AddComponent<MeshRenderer>().sharedMaterial=mat;g.AddComponent<BoxCollider>();g.layer=wall.gameObject.layer;
                        GameObjectUtility.SetStaticEditorFlags(g,GameObjectUtility.GetStaticEditorFlags(wall.gameObject));
                    }
                    foreach(float x in cuts)
                    {
                        float a=x-WindowWidth/2,b=x+WindowWidth/2;
                        Piece(from,a,0,h);Piece(a,b,0,Sill);Piece(a,b,Lintel,h);from=b;
                        // Inward: toward the room's centre.
                        Vector3 inward=new Vector3(0,0,Mathf.Sign(Interior.center.z-z));
                        var w=new GameObject("Library window").transform;w.SetParent(group,false);w.position=new Vector3(x,0,z);
                        // Glass on Ignore Raycast: blocks the player, not the caretaker's sight (his mask skips that layer).
                        var pane=GameObject.CreatePrimitive(PrimitiveType.Cube);pane.name="Glass";pane.transform.SetParent(w,false);
                        pane.transform.position=new Vector3(x,(Sill+Lintel)/2,z);pane.transform.localScale=new Vector3(WindowWidth,Lintel-Sill,.03f);
                        pane.GetComponent<MeshRenderer>().sharedMaterial=glass;pane.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;pane.layer=2;
                        foreach(float side in new[]{-1f,1f})
                        {
                            Trim(w,new Vector3(x+side*(WindowWidth/2-.03f),(Sill+Lintel)/2,z),new Vector3(.06f,Lintel-Sill,depth+.06f),frame);
                            Trim(w,new Vector3(x,side<0?Sill:Lintel,z),new Vector3(WindowWidth,.06f,depth+.06f),frame);
                        }
                        Trim(w,new Vector3(x,(Sill+Lintel)/2,z),new Vector3(.04f,Lintel-Sill,depth+.02f),frame); // centre mullion
                        Trim(w,new Vector3(x,Sill-.02f,z)+inward*(depth/2+.08f),new Vector3(WindowWidth+.1f,.04f,.16f),frame); // inside sill
                        // Corridor light falls through it onto the library floor.
                        var pool=new Vector3(x,0,z)+inward*1.5f;
                        var lamp=new GameObject("Window light").AddComponent<Light>();lamp.transform.SetParent(w,false);
                        lamp.transform.position=new Vector3(x,2.0f,z)+inward*.3f;lamp.transform.LookAt(pool);
                        lamp.type=LightType.Spot;lamp.spotAngle=62;lamp.innerSpotAngle=35;lamp.range=6;lamp.intensity=5.5f; // LibraryWindow lifts it against the darkness while you are inside
                        lamp.color=new Color(.86f,.9f,1f);lamp.shadows=LightShadows.Soft;
                        var marker=w.gameObject.AddComponent<LibraryWindow>();marker.room=Interior;marker.pool=pool;marker.radius=2.2f;marker.lamp=lamp;marker.plainIntensity=5.5f;EditorUtility.SetDirty(marker);
                    }
                    Piece(from,max.x,0,h);
                }
            }
        }
        /// <summary>A box of wall (world x0..x1, y0..y1, `depth` thick about local z 0) with the corridor walls' mapping.</summary>
        /// <summary>A box of wall (world x0..x1, y0..y1, `depth` thick about local z 0) with the corridor walls' own
        /// world-space mapping (u = world x / 2, v = height / 3): reused by SchoolOfficeSetup to open the hatch through
        /// its wall.</summary>
        internal static Mesh WallPiece(float x0,float x1,float y0,float y1,float depth)=>WallPiece(x0,x1,y0,y1,depth,x=>x/2f);
        static Mesh WallPiece(float x0,float x1,float y0,float y1,float depth,Func<float,float> u)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var n=new List<Vector3>();var t=new List<int>();float d=depth/2;
            // Corners go round anticlockwise seen from outside the face (Unity's front faces are clockwise, so reversed below).
            void Face(Vector3 normal,Vector3[] c,Vector2[] m){int b=v.Count;v.AddRange(c);uv.AddRange(m);for(int i=0;i<4;i++)n.Add(normal);t.AddRange(new[]{b,b+2,b+1,b,b+3,b+2});}
            Vector2 W(float x,float y)=>new Vector2(u(x),y/3f);
            Face(Vector3.forward,new[]{new Vector3(x1,y0,d),new Vector3(x0,y0,d),new Vector3(x0,y1,d),new Vector3(x1,y1,d)},new[]{W(x1,y0),W(x0,y0),W(x0,y1),W(x1,y1)});
            Face(Vector3.back,new[]{new Vector3(x0,y0,-d),new Vector3(x1,y0,-d),new Vector3(x1,y1,-d),new Vector3(x0,y1,-d)},new[]{W(x0,y0),W(x1,y0),W(x1,y1),W(x0,y1)});
            Face(Vector3.up,new[]{new Vector3(x0,y1,-d),new Vector3(x1,y1,-d),new Vector3(x1,y1,d),new Vector3(x0,y1,d)},new[]{W(x0,y1),W(x1,y1),W(x1,y1+depth),W(x0,y1+depth)});
            Face(Vector3.down,new[]{new Vector3(x0,y0,d),new Vector3(x1,y0,d),new Vector3(x1,y0,-d),new Vector3(x0,y0,-d)},new[]{W(x0,y0),W(x1,y0),W(x1,y0+depth),W(x0,y0+depth)});
            Face(Vector3.right,new[]{new Vector3(x1,y0,-d),new Vector3(x1,y0,d),new Vector3(x1,y1,d),new Vector3(x1,y1,-d)},new[]{W(x1,y0),W(x1+depth,y0),W(x1+depth,y1),W(x1,y1)});
            Face(Vector3.left,new[]{new Vector3(x0,y0,d),new Vector3(x0,y0,-d),new Vector3(x0,y1,-d),new Vector3(x0,y1,d)},new[]{W(x0,y0),W(x0+depth,y0),W(x0+depth,y1),W(x0,y1)});
            var mesh=new Mesh{name="Library wall piece"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetNormals(n);mesh.SetTriangles(t,0);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
        static void Trim(Transform parent,Vector3 at,Vector3 size,Material m)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Window frame";g.transform.SetParent(parent,false);g.transform.position=at;g.transform.localScale=size;
            Object.DestroyImmediate(g.GetComponent<Collider>());var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=m;
            // A plain cube has no per-face world-metre UVs for the pencil shader's edge strips; unset, it reads the cube's
            // own lightmap UV2 as if it were metres, drawing a tartan of edge lines across every face.
            if(m.shader.name=="Confiscated/Pencil Surface")IllustratedArtSetup.Tiled(r,m,"library",1);
        }

        // ---------- The shadow (LibraryShadow) ----------
        public const int AlcoveCount=10;
        public const string NoticeText="SILENT STUDY\nPlease keep still\nand quiet.\nNo torches near\nthe books.";
        static void Shadow(Transform root,SchoolRunController run)
        {
            var places=new GameObject("Shadow places").transform;places.SetParent(root,false);
            // Alcoves: dead ends away from the doors, the desk and the glue, spread out (furthest-point picks).
            var fromDoors=DoorCells.Select(d=>Distance(mazeEast,mazeNorth,d)).ToArray();
            var candidates=new List<Vector2Int>();
            for(int c=0;c<Cols;c++)for(int r=0;r<Rows;r++)
            {
                var cell=new Vector2Int(c,r);
                if(Clearing.Contains(cell)||cell==mazeGlue||fromDoors.Min(d=>d[c,r])<3)continue;
                if(Steps.Count(d=>Wall(mazeEast,mazeNorth,c,r,d))>=3)candidates.Add(cell);
            }
            // Too few dead ends: corners of the maze make do.
            if(candidates.Count<AlcoveCount)for(int c=0;c<Cols;c++)for(int r=0;r<Rows;r++)
            {var cell=new Vector2Int(c,r);if(!candidates.Contains(cell)&&!Clearing.Contains(cell)&&cell!=mazeGlue&&fromDoors.Min(d=>d[c,r])>=3&&Steps.Count(d=>Wall(mazeEast,mazeNorth,c,r,d))==2)candidates.Add(cell);}
            var chosen=new List<Vector2Int>{candidates.OrderByDescending(p=>fromDoors.Min(d=>d[p.x,p.y])).First()};
            while(chosen.Count<AlcoveCount&&chosen.Count<candidates.Count)
                chosen.Add(candidates.Where(p=>!chosen.Contains(p)).OrderByDescending(p=>chosen.Min(q=>(p-q).sqrMagnitude)).First());
            var alcoves=new List<Transform>();
            foreach(var cell in chosen)
            {
                if(!NavMesh.SamplePosition(Cell(cell.x,cell.y),out var hit,1.2f,1<<NavArea))continue;
                var a=new GameObject("Alcove "+cell.x+","+cell.y).transform;a.SetParent(places,false);a.position=hit.position;
                a.rotation=Quaternion.Euler(0,(cell.x*37+cell.y*91)%360,0);alcoves.Add(a);
            }
            if(alcoves.Count<5)throw new InvalidOperationException("Only "+alcoves.Count+" shadow alcoves found on the library floor.");
            // Each open door: a point just inside (on the library floor) and just outside.
            var entrances=new List<Transform>();var exits=new List<Transform>();
            var noticeBoard=Placeholder("M_Library_Notice",new Color(.93f,.9f,.8f));
            foreach(var name in Doors)
            {
                var door=Object.FindObjectsByType<OfficeDoor>(FindObjectsInactive.Include).First(d=>d.name==name);
                Vector3 f=door.transform.forward;f.y=0;f.Normalize();Vector3 centre=door.transform.position;centre.y=0;
                Vector3 inward=Interior.Contains(centre+f*1.3f+Vector3.up*1.5f)?f:-f;
                if(!NavMesh.SamplePosition(centre+inward*1.3f,out var inHit,1,1<<NavArea)||!NavMesh.SamplePosition(centre-inward*1.5f,out var outHit,1,NavMesh.AllAreas&~(1<<NavArea)))
                    throw new InvalidOperationException("No floor either side of library door "+name);
                var e=new GameObject("Entrance - "+name).transform;e.SetParent(places,false);e.position=inHit.position;entrances.Add(e);
                var x=new GameObject("Thrown out - "+name).transform;x.SetParent(places,false);x.position=outHit.position;exits.Add(x);
                // A plain library notice on the corridor side, beside the door.
                Vector3 side=Vector3.Cross(Vector3.up,inward);
                var board=Box(places,"Library notice - "+name,centre-inward*.1f+side*1.35f+Vector3.up*1.55f,new Vector3(.62f,.5f,.02f),noticeBoard);
                board.transform.rotation=Quaternion.LookRotation(inward);Object.DestroyImmediate(board.GetComponent<Collider>());
                var g=new GameObject("Library notice lettering");g.transform.SetParent(places,false);
                g.transform.SetPositionAndRotation(board.transform.position-inward*.014f,Quaternion.LookRotation(inward));
                var t=g.AddComponent<TextMesh>();t.font=SchoolTypography.Font;t.text=NoticeText;t.fontSize=80;t.characterSize=.0095f;t.color=Color.black;
                t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;g.AddComponent<WorldLabel>();
            }
            // The figure (Blender: gaunt, long skull, arms past its knees, frayed into tendrils instead of legs), see-through
            // black, wrapped in rolling dark smoke, with two slanted, white-hot slit eyes and a glow round them.
            var shade=new GameObject("Library shadow");shade.transform.SetParent(root,false);
            var skip=shade.AddComponent<NavMeshModifier>();skip.ignoreFromBuild=true;
            var body=Unlit("M_Library_Shadow",new Color(.01f,.01f,.015f,.75f),true,null,false);
            // The smoke emits from the figure's surface, which needs a readable mesh.
            ImportRig();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ShadowModel);
            if(model==null)throw new InvalidOperationException("Missing "+ShadowModel+" (Docs/LibraryMaze.md: made in Blender).");
            var figure=(GameObject)PrefabUtility.InstantiatePrefab(model,shade.transform);figure.name="Figure";
            figure.transform.localPosition=Vector3.zero;figure.transform.localRotation=Quaternion.Euler(0,180,0); // the rigged export faces -Z; the shadow faces +Z
            var parts=figure.GetComponentsInChildren<Renderer>().ToList();
            // Rigged (Blender): Drift while it wanders, Hunt when it comes for you (LibraryShadow drives "Hunting").
            var animator=figure.GetComponent<Animator>();if(animator==null)animator=figure.AddComponent<Animator>();
            animator.runtimeAnimatorController=Controller();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;
            foreach(var skinned in figure.GetComponentsInChildren<SkinnedMeshRenderer>()){skinned.updateWhenOffscreen=true;}
            var headBone=figure.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Head");
            if(headBone==null)throw new InvalidOperationException("The shadow rig has no Head bone.");
            foreach(var r in parts){r.sharedMaterial=body;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;}
            foreach(var c in figure.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
            // HDR colours: the library darkness pulls exposure down, and the eyes must still burn through it (and bloom).
            var eyeMat=Unlit("M_Library_ShadowEye",new Color(32f,30f,27f,1),true,Texture("T_ShadowEye"),false);
            var glowMat=Unlit("M_Library_ShadowEyeGlow",new Color(24f,20f,12f,1),true,Texture("T_ShadowEyeGlow"),true);
            var eyes=new List<Renderer>();
            foreach(int s in new[]{-1,1})
            {
                // Quads face +Z (out of the face); mirrored so both slant down toward the nose.
                eyes.Add(Quad(shade.transform,"Eye",new Vector3(s*.058f,1.99f,.365f),new Vector3(-s*.1f,.05f,1),eyeMat));
                eyes.Add(Quad(shade.transform,"Eye glow",new Vector3(s*.058f,1.99f,.375f),new Vector3(.34f,.2f,1),glowMat));
                // Riding on the head bone, so they follow the lunge.
                eyes[eyes.Count-2].transform.SetParent(headBone,true);eyes[eyes.Count-1].transform.SetParent(headBone,true);
            }
            // Smoke: puffs off the figure's surface, drifting up and trailing in world space as it glides.
            var smokeGo=new GameObject("Smoke");smokeGo.transform.SetParent(shade.transform,false);
            var smoke=smokeGo.AddComponent<ParticleSystem>();smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=smoke.main;main.loop=true;main.playOnAwake=true;main.duration=5;main.maxParticles=450;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startLifetime=new ParticleSystem.MinMaxCurve(1.6f,2.8f);main.startSpeed=new ParticleSystem.MinMaxCurve(.03f,.14f);
            main.startSize=new ParticleSystem.MinMaxCurve(.45f,1.05f);main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(.015f,.015f,.02f,1f),new Color(.05f,.045f,.07f,.85f));
            var emission=smoke.emission;emission.rateOverTime=60;
            var shape=smoke.shape;if(parts[0] is SkinnedMeshRenderer skin){shape.shapeType=ParticleSystemShapeType.SkinnedMeshRenderer;shape.skinnedMeshRenderer=skin;}else{shape.shapeType=ParticleSystemShapeType.MeshRenderer;shape.meshRenderer=(MeshRenderer)parts[0];}shape.normalOffset=.02f;
            var rise=smoke.velocityOverLifetime;rise.enabled=true;rise.space=ParticleSystemSimulationSpace.World;
            rise.x=new ParticleSystem.MinMaxCurve(-.04f,.04f);rise.y=new ParticleSystem.MinMaxCurve(.06f,.16f);rise.z=new ParticleSystem.MinMaxCurve(-.04f,.04f);
            var grow=smoke.sizeOverLifetime;grow.enabled=true;grow.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.6f,1,1.5f));
            var spin=smoke.rotationOverLifetime;spin.enabled=true;spin.z=new ParticleSystem.MinMaxCurve(-.4f,.4f);
            var fade=smoke.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.25f),new GradientAlphaKey(.7f,.6f),new GradientAlphaKey(0,1)});
            fade.color=gradient;
            var puff=smokeGo.GetComponent<ParticleSystemRenderer>();puff.renderMode=ParticleSystemRenderMode.Billboard;puff.sortMode=ParticleSystemSortMode.Distance;
            puff.sharedMaterial=Particles("M_Library_ShadowSmoke",Texture("T_ShadowSmoke"),false);puff.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;puff.receiveShadows=false;
            var ai=shade.AddComponent<LibraryShadow>();ai.animator=animator;
            ai.alcoves=alcoves.ToArray();ai.entrances=entrances.ToArray();ai.exits=exits.ToArray();ai.silhouette=parts.ToArray();ai.eyes=eyes.ToArray();ai.smoke=smoke;
            ai.room=Interior;ai.areaMask=1<<NavArea;
            shade.transform.SetPositionAndRotation(alcoves[0].position,alcoves[0].rotation);EditorUtility.SetDirty(ai);
        }
        const string ShadowController="Assets/Art/Models/Library/LibraryShadow.controller";
        /// <summary>Generic rig, readable mesh (the smoke emits from it), axis conversion baked, both clips looping.</summary>
        static void ImportRig()
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(ShadowModel);if(importer==null)throw new InvalidOperationException("Missing "+ShadowModel);
            bool dirty=!importer.isReadable||importer.animationType!=ModelImporterAnimationType.Generic||!importer.bakeAxisConversion||!importer.importAnimation;
            importer.isReadable=true;importer.animationType=ModelImporterAnimationType.Generic;importer.bakeAxisConversion=true;importer.importAnimation=true;
            if(dirty)importer.SaveAndReimport();
            var clips=importer.clipAnimations.Length>0?importer.clipAnimations:importer.defaultClipAnimations;
            if(clips.Any(c=>!c.loopTime||c.name!=ClipName(c.takeName)))
            {foreach(var c in clips){c.loopTime=true;c.name=ClipName(c.takeName);}importer.clipAnimations=clips;importer.SaveAndReimport();}
        }
        static string ClipName(string take)=>take.Contains("Hunt")?"Hunt":take.Contains("Drift")?"Drift":take;
        static RuntimeAnimatorController Controller()
        {
            var clips=AssetDatabase.LoadAllAssetsAtPath(ShadowModel).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
            var drift=clips.FirstOrDefault(c=>c.name=="Drift");var hunt=clips.FirstOrDefault(c=>c.name=="Hunt");
            if(drift==null||hunt==null)throw new InvalidOperationException("The shadow FBX is missing its Drift/Hunt clips.");
            AssetDatabase.DeleteAsset(ShadowController);
            var controller=UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ShadowController);
            controller.AddParameter("Hunting",AnimatorControllerParameterType.Bool);
            var sm=controller.layers[0].stateMachine;
            var driftState=sm.AddState("Drift");driftState.motion=drift;var huntState=sm.AddState("Hunt");huntState.motion=hunt;sm.defaultState=driftState;
            var go=driftState.AddTransition(huntState);go.hasExitTime=false;go.duration=.2f;go.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If,0,"Hunting");
            var back=huntState.AddTransition(driftState);back.hasExitTime=false;back.duration=.5f;back.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot,0,"Hunting");
            AssetDatabase.SaveAssets();return controller;
        }
        const string ShadowModel="Assets/Art/Models/Library/LibraryShadow.fbx",ShadowTextures="Assets/Art/Textures/Library/";
        static Texture2D Texture(string name)
        {
            string path=ShadowTextures+name+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            if(importer==null)throw new InvalidOperationException("Missing "+path);
            if(!importer.alphaIsTransparency||importer.wrapMode!=TextureWrapMode.Clamp||importer.mipmapEnabled==false)
            {importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.SaveAndReimport();}
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Renderer Quad(Transform parent,string name,Vector3 at,Vector3 size,Material m)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Quad);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=at;
            // A Unity quad shows its face toward -Z; turn it to face out of the head.
            g.transform.localRotation=Quaternion.Euler(0,180,0);g.transform.localScale=size;Object.DestroyImmediate(g.GetComponent<Collider>());
            var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=m;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;return r;
        }
        static Material Particles(string name,Texture2D texture,bool additive)
        {
            string path="Assets/Art/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(m,path);}
            m.SetTexture("_BaseMap",texture);m.SetColor("_BaseColor",Color.white);Transparent(m,additive);EditorUtility.SetDirty(m);return m;
        }
        static void Transparent(Material m,bool additive)
        {
            m.SetFloat("_Surface",1);m.SetFloat("_Blend",additive?2:0);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);
            m.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)(additive?UnityEngine.Rendering.BlendMode.One:UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SrcBlendAlpha",(float)UnityEngine.Rendering.BlendMode.One);m.SetFloat("_DstBlendAlpha",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType","Transparent");m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent+(additive?2:1);
        }
        static Material Unlit(string name,Color colour,bool seeThrough,Texture2D texture,bool additive)
        {
            string path="Assets/Art/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",colour);if(texture!=null)m.SetTexture("_BaseMap",texture);
            if(seeThrough)Transparent(m,additive);
            EditorUtility.SetDirty(m);return m;
        }

        static GameObject Box(Transform parent,string name,Vector3 at,Vector3 size,Material material)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.position=at;g.transform.localScale=size;
            g.GetComponent<MeshRenderer>().sharedMaterial=material;return g;
        }
        static Material Placeholder(string name,Color colour)
        {
            string path="Assets/Art/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",colour);m.SetFloat("_Smoothness",.1f);EditorUtility.SetDirty(m);return m;
        }
    }
}

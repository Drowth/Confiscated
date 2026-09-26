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
        const float ShelfDepth=.4f,ShelfHeight=2.1f;
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
            var walls=BuildMaze(root,out var desk,out var glueSpot);
            Lighting(root,desk);
            MovePickups(run,desk,glueSpot);
            // Door signs said CLASSROOM 9 / 10.
            foreach(var sign in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include).Where(t=>t.text=="CLASSROOM 9"||t.text=="CLASSROOM 10"))
            {sign.text="LIBRARY";EditorUtility.SetDirty(sign);}
            DiningHallSetup.Rebake();
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
                ChosenSeed=seed;break;
            }
            if(ChosenSeed==0)throw new InvalidOperationException("No maze seed met the route-length rules; relax MinDoorToDesk.");

            var maze=new GameObject("Maze").transform;maze.SetParent(root,false);
            var shelf=Placeholder("M_Library_Shelf_Placeholder",new Color(.58f,.44f,.3f));
            var walls=new List<(Vector3,bool)>();
            for(int c=0;c<Cols;c++)for(int r=0;r<Rows;r++)
            {
                if(east[c,r]){var p=new Vector3(X0+(c+1)*CellW,ShelfHeight/2,Z0+(r+.5f)*CellH);Box(maze,"Bookcase",p,new Vector3(ShelfDepth,ShelfHeight,CellH+ShelfDepth),shelf);walls.Add((p,false));}
                if(north[c,r]){var p=new Vector3(X0+(c+.5f)*CellW,ShelfHeight/2,Z0+(r+1)*CellH);Box(maze,"Bookcase",p,new Vector3(CellW+ShelfDepth,ShelfHeight,ShelfDepth),shelf);walls.Add((p,true));}
            }
            // Returns desk in the clearing, open on its south side.
            desk=new Vector3(X0+Clearing.center.x*CellW,0,Z0+(Clearing.center.y+.35f)*CellH);
            Box(root,"Returns desk (placeholder)",desk+Vector3.up*.45f,new Vector3(1.6f,.9f,.7f),Placeholder("M_Library_Desk_Placeholder",new Color(.45f,.32f,.2f)));
            glueSpot=Cell(glueCell.x,glueCell.y);
            return walls;
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
            Lamp(lamps,"Returns desk lamp",desk+new Vector3(-.5f,1.3f,.1f),new Color(1f,.82f,.55f),2.2f,7f,LightShadows.Soft);
            // An even grid of weak, unshadowed pools: every aisle is readable, none of it is bright.
            for(int c=1;c<Cols;c+=3)for(int r=1;r<Rows;r+=3)
                Lamp(lamps,"Reading lamp",Cell(c,r,2.7f),new Color(.95f,.86f,.72f),1.6f,6.5f,LightShadows.None);
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

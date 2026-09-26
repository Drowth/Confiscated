using System;
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
    /// open 32 x 18 m room. Step 1 opens three of its four doors (shut during lessons, open for the run), keeps every
    /// NavMeshAgent (caretaker, Mr Reed, coach) out with a carving block in each doorway, names it LIBRARY, and rebakes.
    /// The maze, the shadow and the moved pickups come later. Chained last in SchoolRunSetup.Build.
    /// </summary>
    public static class LibrarySetup
    {
        public const string RootName="Library";
        public static readonly string[] Doors={"North room A west","North room A north","North classroom A south"};
        public const string ShutDoor="North classroom A north";
        public static readonly Bounds Interior=new Bounds(new Vector3(-15.95f,1.5f,90.55f),new Vector3(32.1f,4f,18.1f));

        [MenuItem("Confiscated/Library/Apply Library Room")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing.");
            ApplyToScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            Debug.Log("[Library] Library doors opened, staff kept out, navigation rebaked.");
        }

        public static void ApplyToScene()
        {
            var run=Object.FindFirstObjectByType<SchoolRunController>();
            if(run==null)throw new InvalidOperationException("Open the SchoolLayout scene first.");
            var old=run.transform.Find(RootName);if(old!=null)Object.DestroyImmediate(old.gameObject);
            var root=new GameObject(RootName).transform;root.SetParent(run.transform,false);
            var keepOut=new GameObject("Staff keep-out").transform;keepOut.SetParent(root,false);

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
                // Staff stop at the threshold; the player (not on the NavMesh) walks straight through.
                var block=new GameObject("Keep-out - "+name).transform;block.SetParent(keepOut,false);
                block.SetPositionAndRotation(door.transform.position,door.transform.rotation);
                var modifier=block.gameObject.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;
                var obstacle=block.gameObject.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;
                obstacle.center=new Vector3(0,1.1f,0);obstacle.size=new Vector3(2.2f,2.2f,.6f);obstacle.carving=true;obstacle.carveOnlyStationary=false;
            }
            // All four door signs said CLASSROOM 9 / 10.
            foreach(var sign in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include).Where(t=>t.text=="CLASSROOM 9"||t.text=="CLASSROOM 10"))
            {sign.text="LIBRARY";EditorUtility.SetDirty(sign);}
            // The doorways were baked shut (the old barriers were solid); rebake so they are walkable again.
            DiningHallSetup.Rebake();
        }
    }
}

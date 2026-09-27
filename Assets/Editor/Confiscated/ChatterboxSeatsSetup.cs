using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// The chatterbox's chokepoint benches. Once he has told the library rumour he moves between these after each chat:
    /// his own bench in the west corridor, a new bench in the north cross hall opposite the library's south door, a new
    /// bench in the east spine by EQUIPMENT, and the hall bench by the main entrance. Each seat copies how he sits on his
    /// own bench. Repeatable; chained into SchoolRunSetup.Build.
    /// </summary>
    public static class ChatterboxSeatsSetup
    {
        public const string GroupName="Chatterbox seats",BenchName="Chatterbox bench";
        const string BenchPrefab="Assets/Prefabs/Hallway/P_Hall_Bench.prefab";
        // New benches: a point in the corridor and the way he faces (away from the wall behind him).
        static readonly (string name,Vector3 probe,Vector3 facing)[] NewSeats=
        {
            ("North cross hall, opposite the library",new Vector3(-10.5f,1f,79.5f),Vector3.forward),
            ("East spine, by EQUIPMENT",new Vector3(13f,1f,52f),Vector3.right),
        };
        static readonly Vector3 EntranceBench=new Vector3(-2.4f,0,3.2f);

        [MenuItem("Confiscated/School Run/Install Chatterbox Seats")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing.");
            ApplyToScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();
        }
        public static void ApplyToScene()
        {
            var run=Object.FindFirstObjectByType<SchoolRunController>();var pupil=Object.FindFirstObjectByType<ChatterboxStudent>();
            if(run==null||pupil==null)throw new InvalidOperationException("Open SchoolLayout with its chatterbox first.");
            // His own seat: the first recorded seat on a rebuild, else where he sits now.
            Vector3 homePos=pupil.seats!=null&&pupil.seats.Length>0&&pupil.seats[0]!=null?pupil.seats[0].position:pupil.transform.position;
            Quaternion homeRot=pupil.seats!=null&&pupil.seats.Length>0&&pupil.seats[0]!=null?pupil.seats[0].rotation:pupil.transform.rotation;
            var old=run.transform.Find(GroupName);if(old!=null)Object.DestroyImmediate(old.gameObject);
            var group=new GameObject(GroupName).transform;group.SetParent(run.transform,false);
            var benches=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.name.StartsWith("P_Hall_Bench")&&PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).ToList();
            var home=benches.OrderBy(b=>Flat(b.position-homePos)).First();
            if(Flat(home.position-homePos)>1)throw new InvalidOperationException("The chatterbox is not sitting on a hall bench.");
            // How he sits on a bench, in the bench's own frame.
            Vector3 localPos=Quaternion.Inverse(home.rotation)*(homePos-home.position);Quaternion localRot=Quaternion.Inverse(home.rotation)*homeRot;
            // Distance from the wall behind him (past his own bench and himself).
            var behind=Physics.RaycastAll(homePos+Vector3.up*.6f,-(homeRot*Vector3.forward),3,~(1<<2),QueryTriggerInteraction.Ignore)
                .Where(h=>!h.transform.IsChildOf(home)&&!h.transform.IsChildOf(pupil.transform)).OrderBy(h=>h.distance).ToArray();
            float fromWall=behind.Length>0?behind[0].distance:.66f;
            var seats=new List<Transform>{Seat(group,"Seat - west corridor (home)",homePos,homeRot)};
            foreach(var (name,probe,facing) in NewSeats)
            {
                if(!Physics.Raycast(probe,-facing,out var hit,3,~(1<<2),QueryTriggerInteraction.Ignore))throw new InvalidOperationException("No wall behind the chatterbox seat: "+name);
                Vector3 seatPos=hit.point+facing*fromWall;seatPos.y=homePos.y;Quaternion seatRot=Quaternion.LookRotation(facing);
                Quaternion benchRot=seatRot*Quaternion.Inverse(localRot);Vector3 benchPos=seatPos-benchRot*localPos;
                var bench=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BenchPrefab),group);
                bench.name=BenchName+" - "+name;bench.transform.SetPositionAndRotation(benchPos,benchRot);
                // The corridor NavMesh is baked without it: carve so staff walk round.
                var bounds=bench.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
                var carve=bench.AddComponent<NavMeshObstacle>();carve.shape=NavMeshObstacleShape.Box;carve.carving=true;
                carve.center=bench.transform.InverseTransformPoint(bounds.center);carve.size=Quaternion.Inverse(benchRot)*bounds.size;carve.size=new Vector3(Mathf.Abs(carve.size.x),Mathf.Abs(carve.size.y),Mathf.Abs(carve.size.z));
                if(!NavMesh.SamplePosition(seatPos+facing*.8f,out _,1,NavMesh.AllAreas))throw new InvalidOperationException("No floor in front of the chatterbox seat: "+name);
                seats.Add(Seat(group,"Seat - "+name,seatPos,seatRot));
            }
            var entrance=benches.OrderBy(b=>Flat(b.position-EntranceBench)).First();
            if(Flat(entrance.position-EntranceBench)>1)throw new InvalidOperationException("The hall bench by the main entrance is missing.");
            seats.Add(Seat(group,"Seat - by the main entrance",entrance.position+entrance.rotation*localPos,entrance.rotation*localRot));
            Undo.RecordObject(pupil,"Chatterbox seats");pupil.seats=seats.ToArray();
            pupil.transform.SetPositionAndRotation(homePos,homeRot);
            EditorUtility.SetDirty(pupil);PrefabUtility.RecordPrefabInstancePropertyModifications(pupil);PrefabUtility.RecordPrefabInstancePropertyModifications(pupil.transform);
        }
        static Transform Seat(Transform parent,string name,Vector3 at,Quaternion facing){var t=new GameObject(name).transform;t.SetParent(parent,false);t.SetPositionAndRotation(at,facing);return t;}
        static float Flat(Vector3 v){v.y=0;return v.magnitude;}
    }
}

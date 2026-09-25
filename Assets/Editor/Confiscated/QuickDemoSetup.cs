using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Confiscated.EditorTools
{
    public static class QuickDemoSetup
    {
        [MenuItem("Confiscated/School Run/Install Quick Demo Pacing")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing.");
            ApplyToScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
        public static void ApplyToScene()
        {
            var run=UnityEngine.Object.FindFirstObjectByType<SchoolRunController>();
            if(run==null)throw new InvalidOperationException("Open the SchoolLayout scene first.");
            var title=UnityEngine.Object.FindFirstObjectByType<SchoolTitleMenu>();
            if(title!=null){title.entranceSeconds=4;EditorUtility.SetDirty(title);}
            foreach(var vhs in UnityEngine.Object.FindObjectsByType<HuntVhsEffect>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {vhs.chase=.28f;vhs.search=.1f;EditorUtility.SetDirty(vhs);}
            var pupil=run.GetComponentInChildren<ChatterboxStudent>(true);
            if(pupil==null)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/P_Student_Seated.prefab");
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,run.transform);
                go.name="Chatterbox on west corridor bench";
                pupil=go.AddComponent<ChatterboxStudent>();
            }
            var bench=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t=>t.name=="P_Hall_Bench"&&t.position.x<-30&&t.position.z>35&&t.position.z<50).FirstOrDefault();
            if(bench==null)throw new InvalidOperationException("West corridor bench is missing.");
            pupil.transform.SetPositionAndRotation(bench.position+Vector3.right*.30f,Quaternion.Euler(0,90,0));
            pupil.reach=.9f;
            pupil.GetComponent<SeatedStudent>().lessonFocus=null;
            ChatterboxSetup.Apply(pupil);
            PrefabUtility.RecordPrefabInstancePropertyModifications(pupil.transform);
            EditorUtility.SetDirty(pupil);
        }
    }
}

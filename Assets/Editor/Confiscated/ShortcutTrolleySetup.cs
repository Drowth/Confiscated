using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Confiscated.EditorTools
{
    public static class ShortcutTrolleySetup
    {
        public static void Configure(RunGate gate)
        {
            if(gate==null||gate.kind!=RunGate.Kind.Shortcut)return;
            foreach(var t in gate.GetComponentsInChildren<TextMesh>())if(t.text.Contains("PUSH TROLLEY"))Object.DestroyImmediate(t.gameObject);
            foreach(var child in gate.transform.Cast<Transform>().ToArray())if(child.name=="Supply trolley"||child.name=="Sketched supply trolley")Object.DestroyImmediate(child.gameObject);
            var old=gate.GetComponent<NavMeshObstacle>();if(old!=null)Object.DestroyImmediate(old);
            var body=new GameObject("Sketched supply trolley").transform;body.SetParent(gate.transform,false);body.localRotation=Quaternion.Euler(0,90,0);
            TrolleyArtSetup.ApplyVisual(body,true);
            var collider=body.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,.56f,0);collider.size=new Vector3(2.84f,1.12f,.73f);
            var nav=body.gameObject.AddComponent<NavMeshObstacle>();nav.center=collider.center;nav.size=collider.size;nav.carving=true;nav.carveOnlyStationary=false;
            gate.obstacle=body;gate.clearedLocalPosition=new Vector3(0,0,1.3f);gate.clearedLocalEuler=Vector3.zero;
            EditorUtility.SetDirty(gate);
        }
        [MenuItem("Confiscated/Chase Feedback/Refresh Shortcut Trolley")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Exit Play before refreshing shortcut.");
            foreach(var gate in Object.FindObjectsByType<RunGate>(FindObjectsSortMode.None))Configure(gate);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
    }
}

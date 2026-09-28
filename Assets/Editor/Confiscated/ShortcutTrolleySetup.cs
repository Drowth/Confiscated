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
            var boxSize=new Vector3(2.84f,1.12f,.73f);
            // The user's own DinnerTrolley model, stretched to this shortcut's longer footprint, in place of the
            // hand-drawn "supplies" dressing -- FurnitureModelSetup's generic swap never reaches this one (RunGate is
            // an Interactable, and its swap filter skips every renderer under any Interactable), so fit it directly.
            if(!ApplyStretchedTrolley(body,boxSize))TrolleyArtSetup.ApplyVisual(body,true);
            var collider=body.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,.56f,0);collider.size=boxSize;
            var nav=body.gameObject.AddComponent<NavMeshObstacle>();nav.center=collider.center;nav.size=collider.size;nav.carving=true;nav.carveOnlyStationary=false;
            gate.obstacle=body;gate.clearedLocalPosition=new Vector3(0,0,1.3f);gate.clearedLocalEuler=Vector3.zero;
            EditorUtility.SetDirty(gate);
        }
        /// <summary>Places DinnerTrolley under body and non-uniformly stretches it to exactly fill boxSize (local
        /// axes, body's own space). Returns false (caller falls back to the hand-drawn dressing) if the model
        /// isn't present.</summary>
        static bool ApplyStretchedTrolley(Transform body,Vector3 boxSize)
        {
            var holderRoot=new GameObject(FurnitureModelSetup.RootName).transform;holderRoot.SetParent(body,false);
            if(!PickupModelSetup.Place(holderRoot,"DinnerTrolley",Mathf.Max(boxSize.x,boxSize.y,boxSize.z),0,0,FurnitureModelSetup.Folder))
            {Object.DestroyImmediate(holderRoot.gameObject);return false;}
            var holder=holderRoot.GetChild(0);
            // body is itself rotated (90 from its parent), so world-space renderer bounds don't line up with boxSize's
            // axes (that's body-local, matching the BoxCollider below) -- measure and fit in body's local space instead.
            Bounds LocalBounds()
            {
                var renderers=holder.GetComponentsInChildren<Renderer>();
                var b=new Bounds(body.InverseTransformPoint(renderers[0].bounds.center),Vector3.zero);
                foreach(var r in renderers)foreach(var sx in new[]{-1,1})foreach(var sy in new[]{-1,1})foreach(var sz in new[]{-1,1})
                    b.Encapsulate(body.InverseTransformPoint(r.bounds.center+Vector3.Scale(r.bounds.extents,new Vector3(sx,sy,sz))));
                return b;
            }
            var have=LocalBounds();
            Vector3 fit=new(boxSize.x/Mathf.Max(have.size.x,.001f),boxSize.y/Mathf.Max(have.size.y,.001f),boxSize.z/Mathf.Max(have.size.z,.001f));
            holder.localScale=Vector3.Scale(holder.localScale,fit);
            var now=LocalBounds();
            holder.localPosition+=new Vector3(-now.center.x,-now.min.y,-now.center.z);
            foreach(var r in holder.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;r.receiveShadows=true;SchoolLightingSetup.ConfigureArtworkMaterial(r.sharedMaterial,false);}
            return true;
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

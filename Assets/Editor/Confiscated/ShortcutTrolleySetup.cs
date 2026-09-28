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
            // The user's own DinnerTrolley model, same undistorted proportions as the dining one (uniform scale, no
            // per-axis stretch -- that looked bad), in place of the hand-drawn "supplies" dressing. FurnitureModelSetup's
            // generic swap never reaches this one (RunGate is an Interactable, and its swap filter skips every renderer
            // under any Interactable), so it's placed directly; the collider/obstacle then match its real bounds.
            var bounds=ApplyTrolleyModel(body);
            if(bounds==null){TrolleyArtSetup.ApplyVisual(body,true);bounds=new Vector3(2.84f,1.12f,.73f);}
            var size=bounds.Value;
            var collider=body.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,size.y/2,0);collider.size=size;
            var nav=body.gameObject.AddComponent<NavMeshObstacle>();nav.center=collider.center;nav.size=collider.size;nav.carving=true;nav.carveOnlyStationary=false;
            gate.obstacle=body;gate.clearedLocalPosition=new Vector3(0,0,1.3f);gate.clearedLocalEuler=Vector3.zero;
            EditorUtility.SetDirty(gate);
        }
        /// <summary>Places two DinnerTrolley instances side by side under body, each at the same undistorted scale as
        /// the real dining trolley (stretching one to span the corridor squashed its boxes into diagonal planks; two
        /// natural-proportioned ones parked side by side reads as a wider maintenance barrier with no distortion).
        /// Returns the combined local-space size (for the collider/obstacle), or null if the model isn't present
        /// (caller falls back to the hand-drawn dressing).</summary>
        static Vector3? ApplyTrolleyModel(Transform body)
        {
            // body is itself rotated (90 from its parent), so world-space renderer bounds don't line up with its own
            // local axes (which the BoxCollider below is defined in) -- measure and place in body's local space.
            Bounds LocalBounds(Transform holder)
            {
                var renderers=holder.GetComponentsInChildren<Renderer>();
                var b=new Bounds(body.InverseTransformPoint(renderers[0].bounds.center),Vector3.zero);
                foreach(var r in renderers)foreach(var sx in new[]{-1,1})foreach(var sy in new[]{-1,1})foreach(var sz in new[]{-1,1})
                    b.Encapsulate(body.InverseTransformPoint(r.bounds.center+Vector3.Scale(r.bounds.extents,new Vector3(sx,sy,sz))));
                return b;
            }
            var holders=new System.Collections.Generic.List<Transform>();
            for(int i=0;i<2;i++)
            {
                var holderRoot=new GameObject(FurnitureModelSetup.RootName+(i==0?"":" "+i)).transform;holderRoot.SetParent(body,false);
                // yaw 90: turns each trolley's long side across the corridor (blocking it) instead of along its length
                // (where the route just skirts round it).
                if(!PickupModelSetup.Place(holderRoot,"DinnerTrolley",1.26f,0,90,FurnitureModelSetup.Folder))
                {Object.DestroyImmediate(holderRoot.gameObject);foreach(var h in holders)Object.DestroyImmediate(h.parent.gameObject);return null;}
                var holder=holderRoot.GetChild(0);
                // Place()'s uniform longestSide scale doesn't reproduce the real dining trolley's proportions (it's
                // fit non-uniformly per axis there, via FurnitureModelSetup) -- came out short and shallow (0.56 tall,
                // 0.46 deep) next to the real one's 1.05/0.67. Re-fit to those same real-world dimensions (blocking
                // width stays as placed; the swap to a 90-off yaw here means depth and height are the ones to correct).
                var renderers=holder.GetComponentsInChildren<Renderer>();
                var wb=renderers[0].bounds;foreach(var r in renderers)wb.Encapsulate(r.bounds);
                holder.localScale=Vector3.Scale(holder.localScale,new Vector3(.67f/Mathf.Max(wb.size.x,.001f),1.05f/Mathf.Max(wb.size.y,.001f),1));
                holders.Add(holder);
            }
            // Centre each on body's local origin first (matches LocalBounds' frame), then slide the pair apart along
            // the corridor width (local X) so they sit edge to edge without overlapping.
            foreach(var holder in holders){var b=LocalBounds(holder);holder.localPosition+=new Vector3(-b.center.x,-b.min.y,-b.center.z);}
            float width=LocalBounds(holders[0]).size.x;
            for(int i=0;i<holders.Count;i++)holders[i].localPosition+=new Vector3((i-.5f)*width,0,0);
            foreach(var holder in holders)foreach(var r in holder.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;r.receiveShadows=true;SchoolLightingSetup.ConfigureArtworkMaterial(r.sharedMaterial,false);}
            var combined=LocalBounds(holders[0]);foreach(var holder in holders)combined.Encapsulate(LocalBounds(holder));
            return combined.size;
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

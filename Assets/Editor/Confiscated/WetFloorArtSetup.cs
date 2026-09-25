using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Confiscated.EditorTools
{
    public static class WetFloorArtSetup
    {
        public static void Apply(WetFloorHazard wet)
        {
            var old=wet.transform.Find("Pencil blue damp patch");
            if(old!=null)Object.DestroyImmediate(old.gameObject);
            old=wet.transform.Find("Sketched wet puddle");
            if(old!=null)Object.DestroyImmediate(old.gameObject);
            const string path="Assets/Art/Materials/M_SketchPuddle.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Confiscated/Sketch Puddle"));AssetDatabase.CreateAsset(material,path);}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/T_Puddle_Pencil.png"));
            var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/SchoolProps/SM_SketchPuddle.fbx");
            var root=new GameObject("Sketched wet puddle");
            root.transform.SetParent(wet.transform,false);
            PrefabUtility.InstantiatePrefab(model,root.transform);
            root.transform.localPosition=new Vector3(0,.009f,0);
            root.transform.localScale=new Vector3(wet.size.x,1,wet.size.y);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial=material;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows=false;
            }
            EditorUtility.SetDirty(material);
            ApplySigns(wet);
        }
        /// <summary>
        /// The illustrated hallway A-frame at each end of the puddle, printed face outwards, the way a caretaker leaves them.
        /// Replaces the box-built stand, whose lettering also drew through walls.
        /// </summary>
        static void ApplySigns(WetFloorHazard wet)
        {
            // The generated sign model is preferred; the hallway library's A-frame is the fallback if it is missing.
            bool modelled=PickupModelSetup.Has("WetFloorSign");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hallway/P_Hall_WetFloorSign.prefab");
            if(prefab==null&&!modelled)return;
            foreach(Transform child in wet.transform.Cast<Transform>().ToArray())
                if(child.name=="Yellow caution stand"||child.name=="Wet floor sign")Object.DestroyImmediate(child.gameObject);
            // The corridor runs along whichever axis has the longer clear run between walls.
            Physics.SyncTransforms();Vector3 centre=wet.transform.position+Vector3.up*1.2f;
            float Clear(Vector3 d)=>(Physics.Raycast(centre,d,out var a,40,~0,QueryTriggerInteraction.Ignore)?a.distance:40)+(Physics.Raycast(centre,-d,out var b,40,~0,QueryTriggerInteraction.Ignore)?b.distance:40);
            bool alongX=Clear(Vector3.right)>Clear(Vector3.forward);
            Vector3 along=alongX?Vector3.right:Vector3.forward,across=alongX?Vector3.forward:Vector3.right;
            float reach=(alongX?wet.size.x:wet.size.y)*.5f+.4f,aside=(alongX?wet.size.y:wet.size.x)*.22f;
            foreach(float end in new[]{-1f,1f})
            {
                var sign=modelled?new GameObject("Wet floor sign"):(GameObject)PrefabUtility.InstantiatePrefab(prefab,wet.transform);sign.name="Wet floor sign";
                sign.transform.SetParent(wet.transform,true);
                // The printed face looks along the prop's -Z, so that is the side turned to whoever is approaching.
                sign.transform.SetPositionAndRotation(wet.transform.position+along*end*reach+across*end*aside,Quaternion.LookRotation(-along*end));
                // 0.62 m: a real A-frame, and clearly readable from a child's eye height.
                if(modelled)PickupModelSetup.Place(sign.transform,"WetFloorSign",.62f,0,270);
                // Set dressing only: the navigation mesh does not know about it, so neither should the player's collider.
                foreach(var collider in sign.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
            }
        }
        [MenuItem("Confiscated/Chase Feedback/Capture Wet Floors")]
        public static void Capture()
        {
            System.IO.Directory.CreateDirectory("D:/Confiscated/Docs/WetFloor");
            var go=new GameObject("Wet floor review camera");var camera=go.AddComponent<Camera>();camera.fieldOfView=68;
            try
            {
                int index=0;
                foreach(var wet in Object.FindObjectsByType<WetFloorHazard>(FindObjectsSortMode.None))
                {
                    var sign=wet.transform.Cast<Transform>().First(t=>t.name=="Wet floor sign");
                    Vector3 along=sign.position-wet.transform.position;along.y=0;along.Normalize();
                    foreach(float end in new[]{1f,-1f})
                    {
                        // A running child approaching along the corridor from either end.
                        camera.transform.position=wet.transform.position+along*end*4.2f+Vector3.up*1.25f;
                        camera.transform.LookAt(wet.transform.position+Vector3.up*.35f);
                        HallwayPropLibrary.Capture(camera,1500,950,"D:/Confiscated/Docs/WetFloor/Puddle"+index+(end>0?"_a":"_b")+".png");
                    }
                    index++;
                }
            }
            finally{Object.DestroyImmediate(go);}
        }
        [MenuItem("Confiscated/Chase Feedback/Replace puddle art")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play before changing puddles.");
            foreach(var wet in Object.FindObjectsByType<WetFloorHazard>(FindObjectsSortMode.None))Apply(wet);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
    }
}

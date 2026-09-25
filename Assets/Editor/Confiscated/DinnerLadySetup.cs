using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Confiscated.EditorTools
{
    public static class DinnerLadySetup
    {
        public static void Configure(DinnerTrolleyPatrol trolley)
        {
            const string path="Assets/Art/Textures/T_DinnerLady.png";
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(tex==null)return;
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);if(!importer.alphaIsTransparency||importer.npotScale!=TextureImporterNPOTScale.None){importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}
            const string mp="Assets/Art/Materials/M_DinnerLady.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(mp);if(mat==null){mat=new Material(Shader.Find("Confiscated/Character Cutout"));AssetDatabase.CreateAsset(mat,mp);}mat.SetTexture("_BaseMap",tex);EditorUtility.SetDirty(mat);
            var visual=trolley.transform.Find("Sketched trolley visual");if(visual==null)return;trolley.visual=visual;
            var old=visual.Find("Dinner lady");if(old!=null)Object.DestroyImmediate(old.gameObject);
            var lady=new GameObject("Dinner lady").transform;lady.SetParent(visual,false);lady.localPosition=new Vector3(.86f,0,0);
            var hit=lady.gameObject.AddComponent<CapsuleCollider>();hit.center=new Vector3(0,.85f,0);hit.height=1.7f;hit.radius=.25f;
            var nav=lady.gameObject.AddComponent<NavMeshObstacle>();nav.shape=NavMeshObstacleShape.Capsule;nav.center=hit.center;nav.height=hit.height;nav.radius=hit.radius;nav.carving=true;nav.carveOnlyStationary=false;
            var motion=new GameObject("Walking pencil pivot").transform;motion.SetParent(lady,false);
            var sway=motion.gameObject.AddComponent<CutoutMotion>();sway.movementSource=trolley.transform;sway.walkSpeed=trolley.speed;sway.walkBounce=.014f;sway.walkSwayDegrees=.6f;sway.strideLength=.8f;
            var q=GameObject.CreatePrimitive(PrimitiveType.Quad);q.name="Dinner lady cutout";q.transform.SetParent(motion,false);q.transform.localPosition=new Vector3(0,.91f,0);q.transform.localScale=new Vector3(1.233f,1.85f,1);Object.DestroyImmediate(q.GetComponent<Collider>());q.GetComponent<Renderer>().sharedMaterial=mat;
            visual.localRotation=Quaternion.Euler(0,180,0);EditorUtility.SetDirty(trolley);
        }
        [MenuItem("Confiscated/Chase Feedback/Add Dinner Lady")]
        public static void Install(){if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first.");foreach(var t in Object.FindObjectsByType<DinnerTrolleyPatrol>(FindObjectsSortMode.None))Configure(t);EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();}
    }
}

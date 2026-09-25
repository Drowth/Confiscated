using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Confiscated.EditorTools
{
    public static class SeatedStudentSetup
    {
        const string TexturePath="Assets/Art/Textures/T_Student_Seated_Views.png";
        const string MaterialPath="Assets/Art/Materials/M_Student_Seated.mat";
        const string PrefabPath="Assets/Prefabs/Characters/P_Student_Seated.prefab";
        [MenuItem("Confiscated/Add Seated Classmate")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Leave Play mode first.");
            ApplyToScene();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
        public static void ApplyToScene()
        {
            CreateStudent(TexturePath,MaterialPath,PrefabPath,"Seated classmate","PupilChair_Extra_1_3");
            ApplyGirlToScene();
        }
        [MenuItem("Confiscated/Add Seated Girl Classmate")]
        public static void ApplyGirl()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Leave Play mode first.");
            ApplyGirlToScene();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
        static void ApplyGirlToScene()
        {
            CreateStudent("Assets/Art/Textures/T_Student_Girl_Seated_Views.png",
                "Assets/Art/Materials/M_Student_Girl_Seated.mat","Assets/Prefabs/Characters/P_Student_Girl_Seated.prefab",
                "Seated girl classmate","PupilChair_Extra_1_2");
        }
        static void CreateStudent(string texturePath,string materialPath,string prefabPath,string studentName,string chairName)
        {
            var room=GameObject.Find("School/Details/Year 6 furnishings");if(room==null)return;
            var chair=room.transform.Find("Additional pupil desks/"+chairName);
            if(chair==null)return;
            var old=room.transform.Find(studentName);if(old!=null)Object.DestroyImmediate(old.gameObject);
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(mat==null){mat=new Material(Shader.Find("Confiscated/Character Cutout"));AssetDatabase.CreateAsset(mat,materialPath);}
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));mat.SetColor("_BaseColor",Color.white);
            mat.SetFloat("_MagentaKey",1);mat.SetFloat("_DirectionalViews",1);
            mat.SetVector("_FrontRect",new Vector4(275f/1536,0,440f/1536,1));
            mat.SetVector("_BackRect",new Vector4(820f/1536,0,440f/1536,1));EditorUtility.SetDirty(mat);
            var root=new GameObject(studentName);root.transform.SetParent(room.transform,false);
            // Seat the cutout slightly forward of the chair back, between the chair and desktop.
            root.transform.position=chair.position+Vector3.left*.12f;root.transform.rotation=Quaternion.Euler(0,270,0);
            var visual=GameObject.CreatePrimitive(PrimitiveType.Quad);visual.name="Front and back pupil artwork";visual.transform.SetParent(root.transform,false);
            visual.transform.localPosition=new Vector3(0,.66f,0);visual.transform.localScale=new Vector3(1.30f*440/1024,1.30f,1);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            var renderer=visual.GetComponent<MeshRenderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;
            var student=root.AddComponent<SeatedStudent>();student.artwork=visual.transform;
            System.IO.Directory.CreateDirectory("Assets/Prefabs/Characters");
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            student.lessonFocus=Object.FindFirstObjectByType<SchoolPeriodController>()?.teacherHome;
            EditorUtility.SetDirty(student);
        }
    }
}

using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Confiscated.EditorTools
{
    public static class ChatterboxSetup
    {
        const string Folder="Assets/Art/Textures/Chatterbox/";
        const string MaterialPath="Assets/Art/Materials/M_Chatterbox_Ginger.mat";
        [MenuItem("Confiscated/School Run/Install Ginger Chatterbox")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing.");
            var pupil=UnityEngine.Object.FindFirstObjectByType<ChatterboxStudent>();
            if(pupil==null)throw new InvalidOperationException("Open SchoolLayout with its hallway chatterbox first.");
            Apply(pupil);
            EditorSceneManager.MarkSceneDirty(pupil.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
        public static void Apply(ChatterboxStudent pupil)
        {
            var closed=Import("T_Chatterbox_Ginger_Closed_v1.png");
            var open=Import("T_Chatterbox_Ginger_Open_v1.png");
            var shader=Shader.Find("Confiscated/Chatterbox Cutout");
            if(shader==null)throw new InvalidOperationException("Chatterbox shader has not imported.");
            var mat=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,MaterialPath);}
            mat.shader=shader;mat.SetTexture("_BaseMap",closed);mat.SetTexture("_TalkMap",open);
            mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_MouthOpen",0);
            mat.SetFloat("_DirectionalViews",0);mat.SetFloat("_MagentaKey",0);mat.SetFloat("_Cutoff",.5f);
            mat.SetVector("_MouthRect",new Vector4(454f/1024,1-486f/1536,162f/1024,96f/1536));
            EditorUtility.SetDirty(mat);
            var seated=pupil.GetComponent<SeatedStudent>();
            var art=seated!=null?seated.artwork:pupil.GetComponentInChildren<Renderer>().transform;
            Undo.RecordObjects(new UnityEngine.Object[]{pupil,art,art.GetComponent<Renderer>()},"Ginger chatterbox artwork");
            pupil.artwork=art.GetComponent<Renderer>();pupil.artwork.sharedMaterial=mat;
            // This source is one portrait, not the old two-view atlas. Keep shoes at floor height.
            art.localPosition=new Vector3(0,.66f,0);art.localScale=new Vector3(.96f,1.44f,1);
            pupil.interruptionSeconds=4.2f;
            if(seated!=null){Undo.RecordObject(seated,"Hallway pupil facing");seated.lessonFocus=null;EditorUtility.SetDirty(seated);PrefabUtility.RecordPrefabInstancePropertyModifications(seated);}
            foreach(var obj in new UnityEngine.Object[]{pupil,art,pupil.artwork})
            {EditorUtility.SetDirty(obj);PrefabUtility.RecordPrefabInstancePropertyModifications(obj);}
        }
        static Texture2D Import(string name)
        {
            string path=Folder+name;AssetDatabase.ImportAsset(path);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("Missing chatterbox art: "+path);
            importer.textureType=TextureImporterType.Default;importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=true;importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;
            importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}

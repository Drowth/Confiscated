using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Confiscated.EditorTools
{
    public static class SchoolLetteringSetup
    {
        const string PaperPath="Assets/Resources/SchoolStyle/T_Quiet_Paper.png";
        [MenuItem("Confiscated/Apply Readable Hand Lettering")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Leave Play mode first.");
            ApplyToScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }
        public static void ApplyToScene()
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(PaperPath);
            importer.textureType=TextureImporterType.Default;
            importer.wrapMode=TextureWrapMode.Repeat;
            importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var paper=AssetDatabase.LoadAssetAtPath<Texture2D>(PaperPath);
            foreach(var name in new[]{"M_Painted_Trim_Pencil","M_Chapter_Paper"})
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+name+".mat");
                if(material==null)continue;
                material.SetTexture("_BaseMap",paper);
                material.SetColor("_BaseColor",Color.white);
                EditorUtility.SetDirty(material);
            }
            const string spritePath="Assets/Art/UI/S_Inventory_Paper.asset";
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            var replacement=Sprite.Create(paper,new Rect(0,0,paper.width,paper.height),new Vector2(.5f,.5f),100);
            replacement.name="S_Inventory_Paper";
            if(sprite==null)AssetDatabase.CreateAsset(replacement,spritePath);
            else {EditorUtility.CopySerialized(replacement,sprite);Object.DestroyImmediate(replacement);EditorUtility.SetDirty(sprite);}
            foreach(var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())ApplyLabels(root);
            // Keep the reusable furnishings consistent with their placed instances.
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab.GetComponentInChildren<TextMesh>(true)==null && prefab.GetComponentInChildren<Text>(true)==null)continue;
                var root=PrefabUtility.LoadPrefabContents(path);
                try {if(ApplyLabels(root))PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally {PrefabUtility.UnloadPrefabContents(root);}
            }
        }
        static bool ApplyLabels(GameObject root)
        {
            bool changed=false;
            foreach(var text in root.GetComponentsInChildren<TextMesh>(true))
            {
                if(text.font==SchoolTypography.Font || (text.font!=null && text.font.name.Contains("Fredericka")))continue;
                var renderer=text.GetComponent<Renderer>();
                var oldSize=renderer.bounds.size;
                text.font=SchoolTypography.Font;
                text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);
                var newSize=renderer.bounds.size;
                float oldExtent=Mathf.Max(oldSize.x,oldSize.z),newExtent=Mathf.Max(newSize.x,newSize.z);
                if(newExtent>0.001f && oldExtent>0.001f)
                    text.characterSize*=Mathf.Clamp(oldExtent/newExtent,.8f,1.4f);
                var label=text.GetComponent<WorldLabel>();
                if(label==null)label=text.gameObject.AddComponent<WorldLabel>();
                else {label.enabled=false;label.enabled=true;}
                EditorUtility.SetDirty(text);
                if(PrefabUtility.IsPartOfPrefabInstance(text))PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                changed=true;
            }
            foreach(var text in root.GetComponentsInChildren<Text>(true))
            {
                if(text.font==SchoolTypography.Font || (text.font!=null && text.font.name.Contains("Fredericka")))continue;
                text.font=SchoolTypography.Font;EditorUtility.SetDirty(text);changed=true;
                if(PrefabUtility.IsPartOfPrefabInstance(text))PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            }
            return changed;
        }
    }
}

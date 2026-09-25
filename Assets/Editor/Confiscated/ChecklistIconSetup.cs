using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Confiscated.EditorTools
{
    /// <summary>Gives the HUD belongings checklist the items' own artwork. Chained into SchoolRunSetup.Build: a rebuilt run controller loses the references.</summary>
    public static class ChecklistIconSetup
    {
        // Run order: phone, yo-yo, handheld, skateboard, robot.
        public static readonly string[] Paths=
        {
            "Assets/Art/Textures/T_Phone_Held.png","Assets/Art/Textures/T_Item_YoYo.png","Assets/Art/Textures/T_Item_HandheldGame.png",
            "Assets/Art/Textures/T_Item_Skateboard.png","Assets/Art/Textures/T_Item_ToyRobot.png"
        };
        [MenuItem("Confiscated/School Run/Assign Checklist Icons")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first.");
            ApplyToScene();EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
        public static void ApplyToScene()
        {
            var run=Object.FindFirstObjectByType<SchoolRunController>();
            if(run==null)throw new System.InvalidOperationException("Open SchoolLayout first.");
            var icons=new Texture2D[Paths.Length];
            for(int i=0;i<Paths.Length;i++)
            {
                icons[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(Paths[i]);
                if(icons[i]==null)Debug.LogWarning("Checklist icon missing, the pencil silhouette will be used: "+Paths[i]);
            }
            run.checklistIcons=icons;EditorUtility.SetDirty(run);
            EditorSceneManager.MarkSceneDirty(run.gameObject.scene);
        }
    }
}

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Confiscated.EditorTools
{
    public static class SchoolTitleSetup
    {
        [MenuItem("Confiscated/Build Cinematic Title Menu")]
        public static void Apply()
        {ApplyToScene();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();}
        public static void ApplyToScene()
        {
            var game=Object.FindFirstObjectByType<GameManager>();if(game==null)return;
            var menu=game.GetComponent<SchoolTitleMenu>();if(menu==null)menu=game.gameObject.AddComponent<SchoolTitleMenu>();
            menu.titleLogo=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/ConfiscatedLogo.png");
            menu.titleMusic=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Off-Kilter Melody.wav");
            menu.classroomDoor=GameObject.Find("School/Doors/Year 6 west")?.GetComponent<OfficeDoor>();
            menu.window=GameObject.Find("Garden view window 0 Wall_14")?.transform;
            menu.backgroundWalkerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/P_Caretaker.prefab");
            EditorUtility.SetDirty(menu);
        }
    }
}

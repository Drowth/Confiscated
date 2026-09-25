using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Confiscated.EditorTools
{
    public static class SchoolMusicSetup
    {
        public const string ClipPath="Assets/Audio/Music/The_Caretakers_Patrol.mp3";
        [MenuItem("Confiscated/Audio/Assign school exploration music")]
        public static void Apply()
        {
            var importer=AssetImporter.GetAtPath(ClipPath) as AudioImporter;
            if(importer==null)throw new System.InvalidOperationException("Missing exploration music: "+ClipPath);
            importer.forceToMono=false;
            var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.Streaming;
            settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.85f;
            importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            ApplyToScene();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
        public static void ApplyToScene()
        {
            var game=Object.FindFirstObjectByType<GameManager>();var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            if(game==null||clip==null)return;
            var music=game.GetComponent<SchoolExplorationMusic>();
            if(music==null)music=game.gameObject.AddComponent<SchoolExplorationMusic>();
            music.music=clip;
            var source=music.GetComponent<AudioSource>();source.clip=clip;source.playOnAwake=false;
            source.loop=true;source.spatialBlend=0;source.volume=0;source.priority=160;
            EditorUtility.SetDirty(music);EditorUtility.SetDirty(source);
        }
    }
}

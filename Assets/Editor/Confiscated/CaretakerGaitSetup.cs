using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Confiscated.EditorTools
{
    public static class CaretakerGaitSetup
    {
        [MenuItem("Confiscated/Audio/Install caretaker walking poses and footsteps")]
        public static void Apply()
        {
            Configure(Object.FindFirstObjectByType<SchoolRunController>().caretaker);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
        public static void Configure(CaretakerAI caretaker)
        {
            var gait=caretaker.GetComponent<CaretakerGait>();
            if(gait==null)gait=caretaker.gameObject.AddComponent<CaretakerGait>();
            gait.cutout=caretaker.GetComponentInChildren<MeshRenderer>();
            gait.walkSheet=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/T_Caretaker_Walk.png");
            gait.leftStep=Clip("CareTakerFootStep1");gait.rightStep=Clip("CareTakerFootStep2");
            var threat=caretaker.GetComponent<CaretakerThreatAudio>();
            if(threat==null)threat=caretaker.gameObject.AddComponent<CaretakerThreatAudio>();
            threat.whistleOne=Clip("CareTakerWhistle1");threat.whistleTwo=Clip("CareTakerWhistle2");threat.heartbeat=Clip("Heartbeat");
            EditorUtility.SetDirty(threat);
            if(PrefabUtility.IsPartOfPrefabInstance(threat))PrefabUtility.RecordPrefabInstancePropertyModifications(threat);
            EditorUtility.SetDirty(gait);
            if(PrefabUtility.IsPartOfPrefabInstance(gait))PrefabUtility.RecordPrefabInstancePropertyModifications(gait);
        }
        static AudioClip Clip(string name)
        {
            string path="Assets/Audio/SFX/"+name+".wav";
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono=true;var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;
            importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}

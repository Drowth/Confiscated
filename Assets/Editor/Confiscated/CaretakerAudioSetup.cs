using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace Confiscated.EditorTools
{
    public static class CaretakerAudioSetup
    {
        [MenuItem("Confiscated/Audio/Assign caretaker keys")]
        public static void Apply()
        {
            const string clipPath="Assets/Audio/SFX/JanglingKeys.mp3";
            var importer=(AudioImporter)AssetImporter.GetAtPath(clipPath);
            importer.forceToMono=true;
            var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;
            importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            const string prefabPath="Assets/Prefabs/Props/P_Caretaker.prefab";
            var prefab=PrefabUtility.LoadPrefabContents(prefabPath);
            try{Configure(prefab,clip);PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            foreach(var caretaker in Object.FindObjectsByType<CaretakerAI>(FindObjectsSortMode.None))
            {
                var audio=Configure(caretaker.gameObject,clip);PrefabUtility.RecordPrefabInstancePropertyModifications(audio);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
        static CaretakerWalkAudio Configure(GameObject go,AudioClip clip)
        {
            var audio=go.GetComponent<CaretakerWalkAudio>();
            if(audio==null)audio=go.AddComponent<CaretakerWalkAudio>();
            audio.keysClip=clip;EditorUtility.SetDirty(audio);return audio;
        }
    }
    [InitializeOnLoad]
    public static class CaretakerAudioSmokeTest
    {
        const string Marker="Temp/test_caretaker_keys",Report="../Docs/CaretakerKeys_Validation.txt";
        static int step,frame;static double at,start;static bool passed,background;static int errors;
        static CaretakerWalkAudio audio;static NavMeshAgent agent;
        static CaretakerAudioSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Begin();}};}
        public static void Arm()=>File.WriteAllText(Marker,"1");
        static void Begin(){step=errors=0;frame=-1;passed=true;start=at=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;File.WriteAllText(Report,"Caretaker walking keys runtime check\n");Application.logMessageReceived+=Log;EditorApplication.update+=Tick;}
        static void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception){errors++;File.AppendAllText(Report,m+"\n");}}
        static void Check(bool ok,string text){passed&=ok;File.AppendAllText(Report,(ok?"ok   ":"FAIL ")+text+"\n");}
        static void Next(){step++;at=EditorApplication.timeSinceStartup;}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish();return;}
            if(frame==Time.frameCount)return;frame=Time.frameCount;
            if(EditorApplication.timeSinceStartup-at<.6)return;
            try
            {
                if(EditorApplication.timeSinceStartup-start>30)throw new Exception("Audio test timed out");
                switch(step)
                {
                    case 0:
                        audio=Object.FindFirstObjectByType<CaretakerWalkAudio>();agent=audio.GetComponent<NavMeshAgent>();
                        Check(!audio.Source.isPlaying,"stationary caretaker is silent");
                        Check(audio.keysClip!=null&&audio.keysClip.name=="JanglingKeys","supplied MP3 is assigned");
                        Check(audio.Source.spatialBlend==1&&audio.Source.transform.IsChildOf(audio.transform),"3D emitter follows caretaker");
                        agent.isStopped=false;agent.speed=1.5f;agent.SetDestination(agent.transform.position+Vector3.left*3);Next();break;
                    case 1:
                        Check(agent.velocity.magnitude>.08f&&audio.Source.isPlaying&&audio.Source.volume>0,"actual walking starts key sound");
                        Check(Vector3.Distance(audio.Source.transform.position,audio.transform.TransformPoint(new Vector3(-.3f,1.05f,0)))<.01f,"emitter stays at waist while moving");
                        agent.isStopped=true;Next();break;
                    case 2:
                        Check(!audio.Source.isPlaying,"standing still fades and stops keys");
                        agent.isStopped=false;Next();break;
                    case 3:
                        Check(audio.Source.isPlaying,"walking again restarts keys");
                        audio.enabled=false;Next();break;
                    case 4:
                        Check(!audio.Source.isPlaying,"disabling character audio stops playback");Finish();break;
                }
            }
            catch(Exception e){Check(false,e.ToString());Finish();}
        }
        static void Finish(){EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Application.runInBackground=background;Check(errors==0,"no runtime errors");File.AppendAllText(Report,passed?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;}
    }
}

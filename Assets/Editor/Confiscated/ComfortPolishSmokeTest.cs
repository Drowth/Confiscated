using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Runs against the current scene in Play only; never rebuilds or saves the school.</summary>
    [InitializeOnLoad]
    public static class ComfortPolishSmokeTest
    {
        const string Marker="Temp/test_comfort_polish",Folder="../Docs/ComfortPolish/",Report=Folder+"Validation.txt";
        static readonly string[] Keys={"Confiscated.Volume","Confiscated.Audio.Music","Confiscated.Audio.Effects","Confiscated.Audio.Voice","Confiscated.BaseFov","Confiscated.MouseSensitivity","Confiscated.AlwaysShowHints"};
        static readonly float[] saved=new float[Keys.Length];static readonly bool[] existed=new bool[Keys.Length];
        static int stage,lastFrame,errors;static double began,at;static bool background;
        static AudioSource warning;static float authoredVolume;
        static Keyboard keyboard;
        static SchoolRunController R=>SchoolRunController.Instance;
        static PlayerInteractor P=>R.period.Player;
        static ChaseCamera Feel=>P.GetComponent<ChaseCamera>();
        static PauseMenu Pause=>GameManager.Instance.GetComponent<PauseMenu>();
        static ContextualControlHints Hints=>Object.FindAnyObjectByType<ContextualControlHints>(FindObjectsInactive.Include);
        static AudioMixer Mixer=>Resources.Load<AudioMixer>("SchoolAudio");
        static ComfortPolishSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Begin();}};}
        [MenuItem("Confiscated/Play Test/Arm Comfort Polish Test")]
        public static void Arm(){File.WriteAllText(Marker,"armed");}
        static void Begin()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Report,DateTime.Now.ToString("O")+"\nCurrent scene: "+UnityEngine.SceneManagement.SceneManager.GetActiveScene().path+"\n");
            for(int i=0;i<Keys.Length;i++){existed[i]=PlayerPrefs.HasKey(Keys[i]);saved[i]=i==Keys.Length-1?PlayerPrefs.GetInt(Keys[i]):PlayerPrefs.GetFloat(Keys[i]);}
            background=Application.runInBackground;Application.runInBackground=true;stage=errors=0;lastFrame=-1;began=at=EditorApplication.timeSinceStartup;
            Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        }
        static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+message+"\n");}}
        static void Need(bool value,string label){File.AppendAllText(Report,(value?"ok   ":"FAIL ")+label+"\n");if(!value)throw new Exception(label);}
        static void Next(int next){stage=next;at=EditorApplication.timeSinceStartup;}
        static float Alpha(string name)=>Hints.transform.Find("Hint "+name).GetComponent<CanvasGroup>().alpha;
        static void Slider(string name,float value)=>Object.FindObjectsByType<Slider>(FindObjectsInactive.Include).First(s=>s.name==name).value=value;
        static void Button(string name)=>Object.FindObjectsByType<Button>(FindObjectsInactive.Include).First(s=>s.name==name).onClick.Invoke();
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish(false);return;}
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            try
            {
                double age=EditorApplication.timeSinceStartup-at;
                if(EditorApplication.timeSinceStartup-began>100)throw new Exception("Timed out at stage "+stage);
                if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();return;}
                if(ComicDialogue.IsActive){ComicDialogue.Instance.Advance();return;}
                switch(stage)
                {
                    case 0:
                        if(age<1)return;
                        R.period.PrepareChaseRetry();R.BeginRound(999);R.caretaker.Freeze();
                        foreach(var ai in Object.FindObjectsByType<CaretakerAI>()){ai.Freeze();ai.enabled=false;}
                        foreach(var shadow in Object.FindObjectsByType<LibraryShadow>())shadow.Paused=true;
                        ContextualControlHints.AlwaysShow=false;Time.timeScale=1;Next(1);break;
                    case 1:
                        if(age<1)return;
                        Need(Hints!=null&&Hints.gameObject.activeInHierarchy,"contextual strip is built in the live run");
                        Need(!P.GetComponent<PlayerTorch>().Toggle()&&!ContextualControlHints.Learned(ContextualControlHints.Action.Torch),"failed torch use does not learn its hint");
                        Need(!P.GetComponent<ClockworkDecoy>().Deploy()&&!ContextualControlHints.Learned(ContextualControlHints.Action.Toy),"failed toy deployment does not learn its hint");
                        Pause.Open();Slider("MUSIC",.5f);Slider("EFFECTS",.6f);Slider("VOICE",.7f);Slider("FIELD OF VIEW",76);
                        Need(PauseMenu.IsOpen&&Mathf.Abs(Feel.BaseFov-76)<.01f,"FOV slider updates the base lens while paused");
                        Need(Mathf.Abs(PlayerPrefs.GetFloat("Confiscated.BaseFov")-76)<.01f,"FOV preference is saved");
                        foreach(var channel in new[]{SchoolAudio.Channel.Music,SchoolAudio.Channel.Effects,SchoolAudio.Channel.Voice})Need(Mathf.Abs(PlayerPrefs.GetFloat("Confiscated.Audio."+channel)-SchoolAudio.GetLevel(channel))<.001f,"saved "+channel+" preference");
                        float db;Need(Mixer.GetFloat("EffectsVolume",out db)&&Mathf.Abs(db-20*Mathf.Log10(.6f))<.01f,"effects mixer gain is independent");
                        Need(Mixer.GetFloat("VoiceVolume",out db)&&Mathf.Abs(db-20*Mathf.Log10(.7f))<.01f,"voice mixer gain is independent");
                        Slider("VOICE",0);Need(Mixer.GetFloat("VoiceVolume",out db)&&db<=-80,"voice can be muted");Slider("VOICE",.7f);
                        Button("Hints");Need(ContextualControlHints.AlwaysShow,"pause button enables always-visible hints");Button("Hints");
                        Next(2);break;
                    case 2:
                        if(age<.5)return;
                        ScreenCapture.CaptureScreenshot(Folder+"pause-settings.png");Next(21);break;
                    case 21:
                        if(age<.5)return;
                        Pause.Close();
                        Feel.SetBaseFov(150);Need(Feel.BaseFov==90,"FOV upper bound");Feel.SetBaseFov(10);Need(Feel.BaseFov==50,"FOV lower bound");Feel.SetBaseFov(76);
                        var lens=new GameObject("Saved FOV check",typeof(Camera));var reloaded=lens.AddComponent<ChaseCamera>();
                        Need(Mathf.Abs(reloaded.BaseFov-76)<.01f,"a fresh camera loads the saved FOV");Object.Destroy(lens);
                        GameManager.Instance.lockerUI.Open(P);Need(ContextualControlHints.Learned(ContextualControlHints.Action.Bag),"successful satchel opening learns its hint");GameManager.Instance.lockerUI.Close();
                        Need(Object.FindObjectsByType<AudioSource>().Where(s=>s.isPlaying).All(s=>s.outputAudioMixerGroup!=null),"all currently playing sources have a category");
                        warning=SchoolAudio.Create(new GameObject("Polish test warning"),SchoolAudio.Channel.Voice,true);warning.clip=Resources.Load<AudioClip>("Audio/SchoolAmbience");warning.loop=true;warning.spatialBlend=1;warning.maxDistance=10;warning.volume=.4f;authoredVolume=warning.volume;
                        warning.transform.position=P.transform.position;warning.Play();Next(3);break;
                    case 3:
                        if(age<1)return;
                        Need(SchoolAudio.MusicDuck<=.31f,"audible nearby warning ducks music quickly");
                        Need(warning.volume==authoredVolume,"ducking leaves the authored source volume intact");
                        warning.transform.position=P.transform.position+Vector3.up*100;
                        Need(!SchoolAudio.AudibleWarning(warning,P.transform.position),"inaudible distant warning does not request ducking");warning.Stop();
                        foreach(var source in Object.FindObjectsByType<AudioSource>())if(source.outputAudioMixerGroup!=null&&source.outputAudioMixerGroup.name!="Music")source.Stop();
                        Next(4);break;
                    case 4:
                        if(age<10)return;
                        Need(SchoolAudio.MusicDuck>.99f,"music recovers after the warning ends");
                        float gain;Need(Mixer.GetFloat("MusicVolume",out gain)&&Mathf.Abs(gain-20*Mathf.Log10(.5f))<.01f,"music recovery respects its user volume");
                        Need(Alpha("Bag")==0,"learned satchel hint fades away");Need(Alpha("Lean")>.99f,"unused lean hint remains visible");
                        Need(Alpha("Torch")==0&&Alpha("Toy")==0,"unavailable tools don't fill the strip");
                        Need(Mathf.Abs(P.ViewCamera.fieldOfView-76)<.01f,"normal gameplay returns to the selected FOV");
                        var movement=P.GetComponent<FirstPersonController>();movement.Controller.enabled=false;P.transform.SetPositionAndRotation(new Vector3(-33,0,36),Quaternion.identity);movement.Controller.enabled=true;Physics.SyncTransforms();
                        keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift));Next(41);break;
                    case 41:
                        if(age<.7)return;
                        Need(ContextualControlHints.Learned(ContextualControlHints.Action.Run),"actual player sprint input learns the run hint");
                        InputSystem.RemoveDevice(keyboard);keyboard=null;
                        ContextualControlHints.AlwaysShow=true;Next(5);break;
                    case 5:
                        if(age<1)return;
                        Need(Alpha("Bag")>.99f&&Alpha("Torch")>.99f,"always-visible option restores learned and tool hints");
                        ScreenCapture.CaptureScreenshot(Folder+"hints-always.png");Next(6);break;
                    case 6:
                        if(age<.5)return;
                        Need(errors==0,"no runtime errors during the test");Finish(true);break;
                }
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL stage "+stage+": "+e.Message+"\n");Finish(false);}
        }
        static void Finish(bool success)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
            if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}
            for(int i=0;i<Keys.Length;i++){if(!existed[i])PlayerPrefs.DeleteKey(Keys[i]);else if(i==Keys.Length-1)PlayerPrefs.SetInt(Keys[i],(int)saved[i]);else PlayerPrefs.SetFloat(Keys[i],saved[i]);}
            PlayerPrefs.Save();Time.timeScale=1;Application.runInBackground=background;
            File.AppendAllText(Report,success?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;
        }
    }
}

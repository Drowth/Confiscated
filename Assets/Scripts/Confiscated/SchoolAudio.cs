using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Confiscated
{
    /// <summary>Category gains live in the mixer, leaving authored source fades and spatial attenuation intact.</summary>
    public sealed class SchoolAudio : MonoBehaviour
    {
        public enum Channel { Music, Effects, Voice }
        static AudioMixer mixer;
        static readonly AudioMixerGroup[] groups=new AudioMixerGroup[3];
        static readonly List<AudioSource> warnings=new();
        static readonly float[] levels={1,1,1};
        static SchoolAudio instance;
        float duck=1,scareUntil=-1;
        public static float MusicDuck=>instance!=null?instance.duck:1;
        public static void DuckForScare(float seconds)
        {if(instance!=null)instance.scareUntil=Mathf.Max(instance.scareUntil,Time.unscaledTime+seconds);}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){mixer=null;instance=null;warnings.Clear();for(int i=0;i<3;i++){groups[i]=null;levels[i]=Mathf.Clamp01(PlayerPrefs.GetFloat("Confiscated.Audio."+(Channel)i,1));}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install(){var go=new GameObject("School audio mix");DontDestroyOnLoad(go);instance=go.AddComponent<SchoolAudio>();}
        void OnEnable(){SceneManager.sceneLoaded+=RouteScene;}
        void OnDisable(){SceneManager.sceneLoaded-=RouteScene;}
        static void RouteScene(Scene scene,LoadSceneMode mode)
        {
            // Authored sources and helpers created by Awake get an effects fallback before their first Update.
            foreach(var source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
                if(source.outputAudioMixerGroup==null)Route(source);
        }
        static void Load()
        {
            if(mixer!=null)return;
            mixer=Resources.Load<AudioMixer>("SchoolAudio");
            if(mixer==null){Debug.LogError("SchoolAudio mixer is missing.");return;}
            for(int i=0;i<3;i++){var found=mixer.FindMatchingGroups(((Channel)i).ToString());if(found.Length>0)groups[i]=found[0];}
        }
        public static AudioSource Create(GameObject owner,Channel channel=Channel.Effects,bool warning=false)
        {var source=owner.AddComponent<AudioSource>();source.playOnAwake=false;Route(source,channel,warning);return source;}
        public static void Route(AudioSource source,Channel channel=Channel.Effects,bool warning=false)
        {
            if(source==null)return;Load();source.outputAudioMixerGroup=groups[(int)channel];
            if(warning&&!warnings.Contains(source))warnings.Add(source);
        }
        public static float GetLevel(Channel channel)=>levels[(int)channel];
        public static void SetLevel(Channel channel,float value)
        {levels[(int)channel]=Mathf.Clamp01(value);PlayerPrefs.SetFloat("Confiscated.Audio."+channel,levels[(int)channel]);Apply(channel);}
        static void Apply(Channel channel)
        {
            Load();if(mixer==null)return;
            float gain=levels[(int)channel]*(channel==Channel.Music?MusicDuck:1);
            mixer.SetFloat(channel+"Volume",gain<=.0001f?-80:20*Mathf.Log10(gain));
        }
        void Start(){for(int i=0;i<3;i++)Apply((Channel)i);}
        public static bool AudibleWarning(AudioSource source,Vector3 listener)
        {
            if(source==null||!source.isActiveAndEnabled||!source.isPlaying||source.mute||source.volume<=.01f)return false;
            if(source.outputAudioMixerGroup==groups[(int)Channel.Voice]&&GetLevel(Channel.Voice)<=.001f)return false;
            if(source.outputAudioMixerGroup==groups[(int)Channel.Effects]&&GetLevel(Channel.Effects)<=.001f)return false;
            return source.spatialBlend<.5f||Vector3.Distance(source.transform.position,listener)<source.maxDistance*.8f;
        }
        void LateUpdate()
        {
            var run=SchoolRunController.Instance;var player=run!=null&&run.period!=null?run.period.Player:null;
            bool playing=GameManager.Instance!=null&&GameManager.Instance.IsPlaying;
            if(PauseMenu.IsOpen)return;
            float target=1;
            if(playing&&player!=null)
            {
                if(run.caretaker!=null&&run.caretaker.Current==CaretakerAI.State.Chase)target=.4f;
                for(int i=warnings.Count-1;i>=0;i--)
                {
                    if(warnings[i]==null){warnings.RemoveAt(i);continue;}
                    if(AudibleWarning(warnings[i],player.transform.position))target=Mathf.Min(target,.3f);
                }
            }
            if(Time.unscaledTime<scareUntil)target=Mathf.Min(target,.08f);
            // Quick enough to expose a warning's onset; recover slowly to avoid pumping between footsteps.
            float next=Mathf.MoveTowards(duck,target,Time.unscaledDeltaTime*(target<duck?6: .65f));
            if(!Mathf.Approximately(next,duck)){duck=next;Apply(Channel.Music);}
        }
    }
}

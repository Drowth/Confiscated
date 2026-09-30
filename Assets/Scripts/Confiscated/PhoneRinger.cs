using UnityEngine;

namespace Confiscated
{
    /// <summary>
    /// The classroom opening's text message: an SMS tone and one vibration from the phone on Smith's desk, with the
    /// prop shaking while it buzzes. Mr Reed hears it and confiscates the phone.
    /// </summary>
    public class PhoneRinger : MonoBehaviour
    {
        AudioSource source,hum;
        Transform buzzing;Vector3 restPosition;Quaternion restRotation;
        public AudioSource Emitter=>source;
        /// <summary>The desk phone is shaking on vibrate (the opening text).</summary>
        public bool Vibrating=>buzzing!=null;
        public bool Buzzing=>hum!=null&&hum.isPlaying;

        void Awake()
        {
            var old=GetComponent<AudioSource>();if(old!=null){old.Stop();old.playOnAwake=false;}
            var emitter=new GameObject("Phone sound emitter");emitter.transform.SetParent(transform,false);
            source=SchoolAudio.Create(emitter,SchoolAudio.Channel.Effects,true);
            source.spatialBlend=1f;source.minDistance=.8f;source.maxDistance=24;source.dopplerLevel=0;source.volume=.65f;source.playOnAwake=false;
            hum=SchoolAudio.Create(emitter,SchoolAudio.Channel.Effects,true);hum.spatialBlend=1f;hum.minDistance=.8f;hum.maxDistance=16;hum.dopplerLevel=0;hum.volume=.5f;
            hum.playOnAwake=false;var vibrate=Resources.Load<AudioClip>("Audio/PhoneVibrate");hum.clip=vibrate!=null?vibrate:TempAudio.Buzz;
        }
        void LateUpdate()
        {
            if(buzzing==null)return;
            source.transform.position=buzzing.position;
            if(!hum.isPlaying){StopBuzzing();return;}
            // Shakes while the one-shot vibration plays, unscaled so it keeps going under Reed's dialogue.
            float t=Time.unscaledTime;
            buzzing.localPosition=restPosition+new Vector3(Mathf.PerlinNoise(t*60,0)-.5f,0,Mathf.PerlinNoise(0,t*60)-.5f)*.012f;
            buzzing.localRotation=restRotation*Quaternion.Euler(0,(Mathf.PerlinNoise(t*55,3)-.5f)*10,0);
        }
        void StopBuzzing(){if(buzzing==null)return;buzzing.localPosition=restPosition;buzzing.localRotation=restRotation;buzzing=null;}
        public void PlayScriptedAt(Transform phone)
        {
            Deactivate();
            buzzing=phone;restPosition=phone.localPosition;restRotation=phone.localRotation;source.transform.position=phone.position;
            var textTone=Resources.Load<AudioClip>("Audio/PhoneTextTone");
            source.clip=textTone!=null?textTone:TempAudio.Ring;source.loop=false;source.Play();
            // PhoneVibrate.mp3 repeats a 1.2 s buzz every 2 s; play only the first buzz.
            hum.loop=false;hum.time=hum.clip==TempAudio.Buzz?0:.35f;hum.Play();hum.SetScheduledEndTime(AudioSettings.dspTime+1.3);LateUpdate();
        }
        public void Deactivate()
        {
            if(source!=null)source.Stop();
            if(hum!=null)hum.Stop();
            StopBuzzing();
            HudController.Instance?.SetPhoneState(HudController.PhoneState.Hidden,0f);
        }
    }
}

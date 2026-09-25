using UnityEngine;

namespace Confiscated
{
    /// <summary>A physical bell: the emitter stays on the wall while the gong and striker vibrate.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SchoolBell : MonoBehaviour
    {
        public Transform gong;
        public Transform striker;
        public GameObject ringingMarks;
        AudioSource source;
        Vector3 gongPosition, strikerPosition;
        Quaternion gongRotation, strikerRotation;
        double scheduledStart;
        public AudioSource Source => source != null ? source : source = GetComponent<AudioSource>();
        public bool IsRinging => Source.isPlaying && AudioSettings.dspTime >= scheduledStart;

        void Awake()
        {
            source=GetComponent<AudioSource>();
            if(gong!=null){gongPosition=gong.localPosition;gongRotation=gong.localRotation;}
            if(striker!=null){strikerPosition=striker.localPosition;strikerRotation=striker.localRotation;}
            Rest();
        }

        public void Ring() => RingAt(AudioSettings.dspTime+.05);
        public void RingAt(double dspTime)
        {
            if(!isActiveAndEnabled || Source.clip==null)return;
            Source.Stop();
            scheduledStart=dspTime;
            Source.PlayScheduled(dspTime);
        }

        public void StopRinging(){Source.Stop();Rest();}
        void LateUpdate()
        {
            if(!IsRinging){Rest();return;}
            float t=Source.time;
            float envelope=Mathf.Clamp01(t*18)*Mathf.Clamp01((Source.clip.length-t)*12);
            float oscillation=Mathf.Sin(t*2*Mathf.PI*23);
            if(gong!=null)
            {
                gong.localPosition=gongPosition+new Vector3(oscillation*.0025f,0,Mathf.Sin(t*157)*.001f)*envelope;
                gong.localRotation=gongRotation*Quaternion.Euler(0,oscillation*1.2f*envelope,Mathf.Sin(t*131)*.65f*envelope);
            }
            if(striker!=null)striker.localRotation=strikerRotation*Quaternion.Euler(0,0,oscillation*5*envelope);
            if(ringingMarks!=null)ringingMarks.SetActive(Mathf.Sin(t*35)>-.25f);
        }
        void Rest()
        {
            if(gong!=null){gong.localPosition=gongPosition;gong.localRotation=gongRotation;}
            if(striker!=null){striker.localPosition=strikerPosition;striker.localRotation=strikerRotation;}
            if(ringingMarks!=null)ringingMarks.SetActive(false);
        }
        void OnDisable(){if(source!=null)source.Stop();Rest();}
    }
}

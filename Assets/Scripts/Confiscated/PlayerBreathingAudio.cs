using UnityEngine;
namespace Confiscated
{
    [DisallowMultipleComponent]
    public sealed class PlayerBreathingAudio : MonoBehaviour
    {
        public AudioSource Voice {get;private set;}
        public string Phase {get;private set;}="Silent";
        AudioClip running,recovery;FirstPersonController movement;
        void Awake()
        {
            movement=GetComponent<FirstPersonController>();
            running=Resources.Load<AudioClip>("Audio/RunHardBreathing");
            recovery=Resources.Load<AudioClip>("Audio/RunStopBreathing");
            Voice=gameObject.AddComponent<AudioSource>();Voice.playOnAwake=false;Voice.loop=true;Voice.spatialBlend=0;Voice.volume=0;
        }
        void LateUpdate()
        {
            bool active=movement.enabled&&!movement.MovementLocked&&!ComicDialogue.IsActive&&Time.timeScale>0&&
                (GameManager.Instance==null||GameManager.Instance.IsPlaying);
            Tick(active,movement.IsSprinting,movement.SprintFraction,Time.deltaTime);
        }
        public void Tick(bool active,bool sprinting,float reserve,float dt)
        {
            string phase=!active?"Silent":sprinting?"Running":reserve<.9999f?"Recovering":"Silent";
            if(phase!=Phase)
            {
                Voice.Stop();Voice.volume=0;Phase=phase;
                Voice.clip=phase=="Running"?running:phase=="Recovering"?recovery:null;
                if(Voice.clip!=null)Voice.Play();
            }
            if(phase!="Silent")Voice.volume=Mathf.MoveTowards(Voice.volume,phase=="Running"?.5f:Mathf.Lerp(.12f,.45f,1-reserve),dt*3);
        }
        void OnDisable(){if(Voice!=null)Voice.Stop();Phase="Silent";}
        void OnDestroy(){if(Voice!=null)Destroy(Voice);}
    }
}

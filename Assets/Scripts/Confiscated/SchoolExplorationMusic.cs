using UnityEngine;

namespace Confiscated
{
    /// <summary>Non-positional school underscore, faded around lessons and important sound cues.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SchoolExplorationMusic : MonoBehaviour
    {
        public AudioClip music;
        [Range(0,1)] public float volume=.22f;
        public float fadeSeconds=1.5f;
        public AudioSource Source {get;private set;}
        GameManager game;
        SchoolBellSystem bells;
        PhoneRinger phone;
        bool started,paused;

        public bool ExplorationActive
        {
            get
            {
                if(game==null||!game.IsPlaying)return false;
                var period=game.schoolPeriod;
                var run=SchoolRunController.Instance;
                // Reed releasing the player begins the free-roaming portion. Keep the same
                // underscore running when the formal lesson phase gives way to the five-item hunt.
                return (period!=null&&period.IsRoaming)||(run!=null&&run.RoundStarted);
            }
        }

        void Awake()
        {
            Source=GetComponent<AudioSource>();Source.Stop();Source.playOnAwake=false;
            Source.clip=music;Source.loop=true;Source.spatialBlend=0;Source.dopplerLevel=0;
            Source.volume=0;Source.priority=160;
            game=FindFirstObjectByType<GameManager>();bells=FindFirstObjectByType<SchoolBellSystem>();phone=FindFirstObjectByType<PhoneRinger>();
        }
        void Update()
        {
            if(game==null||music==null)return;
            bool exploring=ExplorationActive;
            float target=exploring?volume:0;
            if(ComicDialogue.IsActive||(bells!=null&&bells.IsRinging)||(phone!=null&&phone.Emitter!=null&&phone.Emitter.isPlaying))target*=.3f;
            if(exploring&&!Source.isPlaying)
            {
                if(started&&paused)Source.UnPause();else {Source.Play();started=true;}
                paused=false;
            }
            Source.volume=Mathf.MoveTowards(Source.volume,target,Time.unscaledDeltaTime*Mathf.Max(.01f,volume)/Mathf.Max(.05f,fadeSeconds));
            if(!exploring&&Source.volume<=.001f&&Source.isPlaying){Source.Pause();paused=true;}
        }
        void OnDisable(){if(Source!=null){Source.Stop();Source.volume=0;}started=paused=false;}
    }
}

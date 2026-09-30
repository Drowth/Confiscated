using UnityEngine;
namespace Confiscated
{
    /// <summary>
    /// One heartbeat for the player during the chase (not the opening errand), driven by whichever threat is nearest: the caretaker (unless frozen), the dinner
    /// lady, the PE coach, or the library shadow while you're in the library with it. Silent beyond Range; louder and
    /// faster the closer the nearest one gets, easing off again as it moves away. Made lazily by CaretakerThreatAudio,
    /// which holds the clip, so it survives scene rebuilds.
    /// </summary>
    public sealed class ThreatHeartbeat : MonoBehaviour
    {
        public const float Range=22f,Closest=2f;
        public static ThreatHeartbeat Instance {get;private set;}
        public AudioSource Source {get;private set;}
        public float NearestThreat {get;private set;}=float.MaxValue;
        CaretakerAI[] caretakers;DinnerTrolleyPatrol[] dinnerLadies;PeCoach[] coaches;LibraryShadow[] shadows;
        float refreshAt;

        public static void Ensure(AudioClip clip)
        {
            if(Instance==null)new GameObject("Threat heartbeat").AddComponent<ThreatHeartbeat>();
            if(Instance.Source.clip==null)Instance.Source.clip=clip;
        }
        void Awake()
        {
            Instance=this;
            Source=gameObject.AddComponent<AudioSource>();Source.playOnAwake=false;Source.spatialBlend=0;Source.loop=true;Source.volume=0;
        }
        void OnDestroy(){if(Instance==this)Instance=null;}

        void Update()
        {
            var gm=GameManager.Instance;var run=SchoolRunController.Instance;
            var player=run!=null&&run.period!=null?run.period.Player:null;
            // Only once the chase is on (phone recovered, round started): during the opening errand nobody can catch you.
            bool active=gm!=null&&gm.IsPlaying&&!ComicDialogue.IsActive&&Time.timeScale>0&&player!=null&&run.RoundStarted;
            if(!active){NearestThreat=float.MaxValue;Source.Stop();Source.volume=0;Source.pitch=1;return;}
            // Staff come and go (the coach and dinner lady only exist in some phases), so re-find them now and then.
            if(Time.time>=refreshAt){Refresh();refreshAt=Time.time+1;}
            NearestThreat=Nearest(player.transform.position);
            float closeness=NearestThreat<Range?Mathf.InverseLerp(Range,Closest,NearestThreat):0;
            float volume=closeness>0?Mathf.Lerp(.06f,.9f,closeness):0;
            Source.volume=Mathf.MoveTowards(Source.volume,volume,Time.deltaTime*1.5f);
            Source.pitch=Mathf.MoveTowards(Source.pitch,Mathf.Lerp(1f,1.9f,closeness),Time.deltaTime*2);
            if(Source.volume>0){if(!Source.isPlaying&&Source.clip!=null)Source.Play();}
            else if(Source.isPlaying)Source.Stop();
        }

        void Refresh()
        {
            caretakers=FindObjectsByType<CaretakerAI>();dinnerLadies=FindObjectsByType<DinnerTrolleyPatrol>();
            coaches=FindObjectsByType<PeCoach>();shadows=FindObjectsByType<LibraryShadow>();
        }
        float Nearest(Vector3 p)
        {
            float best=float.MaxValue;
            void Consider(Behaviour b){if(b!=null&&b.isActiveAndEnabled)best=Mathf.Min(best,Vector3.Distance(b.transform.position,p));}
            foreach(var c in caretakers)if(c!=null&&c.Current!=CaretakerAI.State.Frozen)Consider(c);
            foreach(var d in dinnerLadies)Consider(d);
            foreach(var k in coaches)if(k!=null&&!k.Paused)Consider(k);
            // It never leaves the library, and can't reach you outside it -- no pounding heart through the library wall.
            foreach(var s in shadows)if(s!=null&&s.Current!=LibraryShadow.Phase.Gone&&s.Inside(p))Consider(s);
            return best;
        }
    }
}

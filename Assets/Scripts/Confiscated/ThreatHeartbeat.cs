using UnityEngine;
namespace Confiscated
{
    /// <summary>
    /// The player's heartbeat during the chase (never the opening errand). It beats while something is actively after
    /// Smith -- the caretaker or Mr Reed chasing, or the library shadow hunting him inside the library -- at any range,
    /// and whenever the caretaker (or Mr Reed) is within Range even if not chasing yet. Louder and faster the closer
    /// that threat is, easing off as it drops away. Made lazily by CaretakerThreatAudio, which holds the clip, so it
    /// survives scene rebuilds.
    /// </summary>
    public sealed class ThreatHeartbeat : MonoBehaviour
    {
        public const float Range=22f,Closest=2f,ChaseFloor=.2f;
        public static ThreatHeartbeat Instance {get;private set;}
        public AudioSource Source {get;private set;}
        public float NearestThreat {get;private set;}=float.MaxValue;
        CaretakerAI[] caretakers;LibraryShadow[] shadows;
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
            if(Time.time>=refreshAt){Refresh();refreshAt=Time.time+1;}
            NearestThreat=Nearest(player.transform.position,out bool chased);
            float closeness=NearestThreat<Range?Mathf.InverseLerp(Range,Closest,NearestThreat):0;
            // Being chased always registers, however far back the chaser is.
            if(chased)closeness=Mathf.Max(closeness,ChaseFloor);
            float volume=closeness>0?Mathf.Lerp(.06f,.9f,closeness):0;
            Source.volume=Mathf.MoveTowards(Source.volume,volume,Time.deltaTime*1.5f);
            Source.pitch=Mathf.MoveTowards(Source.pitch,Mathf.Lerp(1f,1.9f,closeness),Time.deltaTime*2);
            if(Source.volume>0){if(!Source.isPlaying&&Source.clip!=null)Source.Play();}
            else if(Source.isPlaying)Source.Stop();
        }

        void Refresh()
        {
            caretakers=FindObjectsByType<CaretakerAI>();shadows=FindObjectsByType<LibraryShadow>();
        }
        /// <summary>Distance to the nearest threat that counts: a chaser at any range, or a caretaker (not frozen) within Range.</summary>
        float Nearest(Vector3 p,out bool chased)
        {
            float best=float.MaxValue;bool anyChase=false;
            void Consider(Behaviour b,bool chasing)
            {
                if(b==null||!b.isActiveAndEnabled)return;
                float d=Vector3.Distance(b.transform.position,p);
                if(!chasing&&d>=Range)return;
                best=Mathf.Min(best,d);anyChase|=chasing;
            }
            foreach(var c in caretakers)if(c!=null&&c.Current!=CaretakerAI.State.Frozen)Consider(c,c.Current==CaretakerAI.State.Chase);
            // It never leaves the library, and can't reach you outside it -- no pounding heart through the library wall.
            foreach(var s in shadows)if(s!=null&&s.Current==LibraryShadow.Phase.Hunt&&s.Inside(p))Consider(s,true);
            chased=anyChase;return best;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Confiscated
{
    /// <summary>A seated pupil holds a short conversation within arm's reach. A wide berth avoids it; the wind-up toy does not interest him.</summary>
    public sealed class ChatterboxStudent : MonoBehaviour
    {
        public float reach=.9f, warningDistance=3.5f, interruptionSeconds=4.2f, cooldownSeconds=25;
        public Renderer artwork;
        public bool Talking=>ComicDialogue.IsActive&&ComicDialogue.Instance.Actor==transform;
        public bool MouthOpen {get;private set;}
        public int Interruptions {get;private set;}
        public bool Distracted=>Time.time<distractedUntil;
        public bool Available=>Time.time>=availableAt&&!needsSpace&&!Distracted;
        float distractedUntil,availableAt;
        bool needsSpace,warned;
        FirstPersonController movement;
        PlayerInteractor player;
        MaterialPropertyBlock appearance;
        AudioSource voice,speech;
        AudioClip chatterClip;
        int lastLine=-1;
        // Each line has a recorded clip in Resources/Audio. Lines whose clip is missing fall back to the syllable placeholder,
        // and are only picked when no line has a clip at all.
        static readonly string[] lineTexts=
        {
            "Have you seen my yo-yo? It glows in the dark! Anyway... where are you going?",
            "Did you see Mr Reed today? He looks funny, doesn't he? Anyway... where are you going?"
        };
        static readonly string[] lineClips={"ChatterboxYoYo","ChatterboxMrReed"};
        // His first chat (once; a chase retry already knows): the only place the library shadow's rules are given (Docs/LibraryMaze.md).
        public const string RumourLine="My brother says something lives in the library. Stand still with your torch off and it walks right past you. Move, and... GONE! Anyway... where are you going?";
        const string RumourClip="ChatterboxLibraryRumour";
        public bool ToldRumour {get;private set;}
        // After the rumour he is a roaming blocker: he calls you over from across a corridor, and after each chat he moves to
        // another chokepoint bench (while you can't see him). A sweet buys a quiet pass: he munches instead of talking.
        [Tooltip("Chokepoint benches he moves between once he has told the rumour (ChatterboxSeatsSetup).")]
        public Transform[] seats;
        public float calloutDistance=6f,munchSeconds=25f,moveAwayDistance=12f;
        public int Seat {get;private set;}
        public int Moves {get;private set;}
        public int SweetsEaten {get;private set;}
        public float Reach=>ToldRumour?calloutDistance:reach;
        bool movePending,wasTalking;
        void OnDisable()
        {
            if(Talking)ComicDialogue.Cancel();
            SetMouth(false);
            if(voice!=null)voice.Stop();
            if(speech!=null)speech.Stop();
        }
        int PickLine(out AudioClip clip)
        {
            if(DarkModeDialogue.Active)
            {
                lastLine=lastLine==0?1:0;clip=DarkModeDialogue.Voice(DarkModeDialogue.Chatterbox[lastLine]);return lastLine;
            }
            var recorded=new List<int>();var all=new List<int>();
            for(int i=0;i<lineTexts.Length;i++){all.Add(i);if(Resources.Load<AudioClip>("Audio/"+lineClips[i])!=null)recorded.Add(i);}
            var pool=recorded.Count>0?recorded:all;
            if(pool.Count>1)pool.Remove(lastLine);
            lastLine=pool[Random.Range(0,pool.Count)];
            clip=Resources.Load<AudioClip>("Audio/"+lineClips[lastLine]);
            return lastLine;
        }
        void OnDestroy(){if(chatterClip!=null)Destroy(chatterClip);}
        void Awake()
        {
            voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.loop=true;
            voice.spatialBlend=0;voice.volume=.12f;voice.ignoreListenerPause=true;
            // Quiet, original cartoon syllables; the actual words are shown in the dialogue balloon.
            const int rate=22050;float[] samples=new float[rate/2];
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate,syllable=t*6,phase=syllable-Mathf.Floor(syllable);
                float envelope=Mathf.Sin(Mathf.PI*Mathf.Clamp01(phase/.78f));
                float hz=220+35*Mathf.Sin(t*19);
                samples[i]=envelope*(.5f*Mathf.Sin(2*Mathf.PI*hz*t)+.22f*Mathf.Sin(4*Mathf.PI*hz*t)+.12f*Mathf.Sin(6*Mathf.PI*hz*t));
            }
            chatterClip=AudioClip.Create("Chatterbox syllables",samples.Length,1,rate,false);chatterClip.SetData(samples,0);voice.clip=chatterClip;
            speech=gameObject.AddComponent<AudioSource>();speech.playOnAwake=false;speech.loop=false;speech.spatialBlend=0;speech.ignoreListenerPause=true;
        }
        void LateUpdate()
        {
            // A recorded clip drives the mouth and replaces the placeholder syllables for as long as it plays.
            if(speech.isPlaying&&!Talking)speech.Stop();
            bool recorded=speech.isPlaying;
            bool speaking=Talking&&(recorded||ComicDialogue.Instance.IsSpeaking);
            bool syllables=speaking&&!recorded&&speech.clip==null;
            if(syllables){if(!voice.isPlaying)voice.Play();}else if(voice.isPlaying)voice.Stop();
            SetMouth(speaking&&Mathf.FloorToInt(Time.unscaledTime*9)%2==0);
        }
        void SetMouth(bool open)
        {
            MouthOpen=open;
            if(artwork==null)return;
            appearance??=new MaterialPropertyBlock();artwork.GetPropertyBlock(appearance);
            appearance.SetFloat("_MouthOpen",open?1:0);artwork.SetPropertyBlock(appearance);
        }
        bool Live=>SchoolRunController.Instance!=null&&SchoolRunController.Instance.RoundStarted&&
            GameManager.Instance!=null&&GameManager.Instance.IsPlaying&&!ComicDialogue.IsActive;
        bool Visible(Vector3 target)
        {
            // Start above the bench back; walls and shut doors still prevent an encounter.
            return !Physics.Linecast(transform.position+Vector3.up*1.1f,target+Vector3.up,out var hit,~0,QueryTriggerInteraction.Ignore)||hit.transform.IsChildOf(player.transform);
        }
        /// <summary>To a different chokepoint bench, at random.</summary>
        public void MoveSeat()
        {
            if(seats==null||seats.Length<2)return;
            int next=Seat;for(int i=0;i<10&&next==Seat;i++)next=Random.Range(0,seats.Length);
            Seat=next;Moves++;transform.SetPositionAndRotation(seats[Seat].position,seats[Seat].rotation);
            needsSpace=false;warned=false;
        }
        /// <summary>A new run: back on his first bench, rumour untold is kept (it is told once).</summary>
        public void ResetRun(){movePending=false;Seat=0;if(seats!=null&&seats.Length>0)transform.SetPositionAndRotation(seats[0].position,seats[0].rotation);distractedUntil=0;needsSpace=false;warned=false;}
        void Update()
        {
            // A chat just ended: once out of the player's sight, he moves on to another bench.
            if(wasTalking&&!Talking&&ToldRumour)movePending=true;wasTalking=Talking;
            if(!Live)return;
            if(player==null){player=SchoolRunController.Instance.period.Player;movement=player.GetComponent<FirstPersonController>();}
            var delta=player.transform.position-transform.position;delta.y=0;
            float distance=delta.magnitude,warnAt=Mathf.Max(warningDistance,Reach+3);
            if(movePending&&(distance>moveAwayDistance||!Visible(player.transform.position))){movePending=false;MoveSeat();return;}
            if(distance>warnAt+1){needsSpace=false;warned=false;}
            if(!Available||movement.MovementLocked||movement.ForcedCorridorRun||movement.IsFallen||player.InputLocked)return;
            // This pupil faces into the hall. Passing behind the bench never triggers a conversation.
            if(Vector3.Dot(transform.forward,delta)<0||!Visible(player.transform.position))return;
            if(!warned&&distance<warnAt)
            {
                warned=true;
                HudController.Instance?.SetStatus("Chatterbox ahead. Give him space.",4);
            }
            if(distance>Reach)return;
            // A sweet: he takes it and is too busy chewing to talk.
            if(ToldRumour&&player.GetComponent<Sweets>()?.Spend()==true)
            {
                SweetsEaten++;distractedUntil=Time.time+munchSeconds;needsSpace=true;movePending=true;
                HudController.Instance?.SetBark("Chatterbox: Ooh, sweets! Mmf... fanks!",2.5f);
                HudController.Instance?.SetStatus("He's too busy chewing to talk. Go!",3);
                return;
            }
            AudioClip clip;string words;
            if(!ToldRumour){ToldRumour=true;clip=Resources.Load<AudioClip>("Audio/"+RumourClip);words=RumourLine;}
            else{int line=PickLine(out clip);words=DarkModeDialogue.Active?DarkModeDialogue.Chatterbox[line].text:lineTexts[line];}
            // Hold him for the whole recording, plus a beat; lines without a recording get time to read.
            float hold=clip!=null?clip.length+.5f:words==RumourLine?8f:interruptionSeconds;
            if(!ComicDialogue.TrySpeakTimed(transform,"Chatterbox",words,hold)){if(words==RumourLine)ToldRumour=false;return;}
            speech.clip=clip;if(clip!=null)speech.Play();
            Interruptions++;availableAt=Time.time+cooldownSeconds;needsSpace=true;
            HudController.Instance?.SetStatus(null);
        }
    }
}

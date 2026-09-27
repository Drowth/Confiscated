using System.Collections.Generic;
using UnityEngine;

namespace Confiscated
{
    /// <summary>
    /// A seated pupil who talks at you. He speaks in the world, not in a cutscene: a slow subtitle and his voice from where
    /// he sits, and during the run he is loud enough for the caretaker to hear. You're stuck listening (rooted to the spot,
    /// free to look around) until he finishes; only something dragging you away (a chat teleport) cuts him off. His first chat is the library rumour; after that he roams chokepoint benches and calls you over
    /// from across the corridor. A sweet buys a quiet pass. The wind-up toy does not interest him.
    /// </summary>
    public sealed class ChatterboxStudent : MonoBehaviour
    {
        public float reach=.9f, warningDistance=3.5f, interruptionSeconds=4.2f, cooldownSeconds=25;
        [Tooltip("Before the rumour: how close you pass before he calls you over (the first meeting must not be missed).")]
        public float firstCallout=3.5f;
        [Tooltip("Subtitle speed: slower than the comic dialogue, so it can be read while moving.")]
        public float lettersPerSecond=15f;
        [Tooltip("He stops mid-sentence if you get this far away.")]
        public float cutOffDistance=12f;
        [Tooltip("During the run his chatter is a noise the caretaker can hear.")]
        public float noiseEvery=2.5f,noiseRadius=14f;
        public Renderer artwork;
        public bool Talking=>talking;
        /// <summary>What he is saying, and how much of it the subtitle shows so far.</summary>
        public string Words {get;private set;}="";
        public string Shown {get;private set;}="";
        public int CutOffs {get;private set;}
        public bool MouthOpen {get;private set;}
        public int Interruptions {get;private set;}
        public bool Distracted=>Time.time<distractedUntil;
        public bool Available=>Time.time>=availableAt&&!needsSpace&&!Distracted;
        float distractedUntil,availableAt,talkStart,talkEnds,nextNoise;
        bool needsSpace,warned,talking;
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
        public float Reach=>ToldRumour?calloutDistance:firstCallout;
        bool movePending,wasTalking;
        void OnDisable(){Hush();}
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
            // Both voices come from where he sits: quieter as you walk away.
            voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.loop=true;Spatial(voice);voice.volume=.18f;
            // Quiet, original cartoon syllables for lines without a recording.
            const int rate=22050;float[] samples=new float[rate/2];
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate,syllable=t*6,phase=syllable-Mathf.Floor(syllable);
                float envelope=Mathf.Sin(Mathf.PI*Mathf.Clamp01(phase/.78f));
                float hz=220+35*Mathf.Sin(t*19);
                samples[i]=envelope*(.5f*Mathf.Sin(2*Mathf.PI*hz*t)+.22f*Mathf.Sin(4*Mathf.PI*hz*t)+.12f*Mathf.Sin(6*Mathf.PI*hz*t));
            }
            chatterClip=AudioClip.Create("Chatterbox syllables",samples.Length,1,rate,false);chatterClip.SetData(samples,0);voice.clip=chatterClip;
            speech=gameObject.AddComponent<AudioSource>();speech.playOnAwake=false;speech.loop=false;Spatial(speech);
        }
        static void Spatial(AudioSource s){s.spatialBlend=1;s.rolloffMode=AudioRolloffMode.Linear;s.minDistance=2;s.maxDistance=16;s.dopplerLevel=0;}
        void LateUpdate()
        {
            // A recorded clip drives the mouth and replaces the placeholder syllables for as long as it plays.
            bool recorded=speech.isPlaying;
            bool speaking=talking&&(recorded||Shown.Length<Words.Length);
            bool syllables=speaking&&!recorded&&speech.clip==null;
            if(syllables){if(!voice.isPlaying)voice.Play();}else if(voice.isPlaying)voice.Stop();
            SetMouth(speaking&&Mathf.FloorToInt(Time.time*9)%2==0);
        }
        void SetMouth(bool open)
        {
            MouthOpen=open;
            if(artwork==null)return;
            appearance??=new MaterialPropertyBlock();artwork.GetPropertyBlock(appearance);
            appearance.SetFloat("_MouthOpen",open?1:0);artwork.SetPropertyBlock(appearance);
        }
        // From the moment you are free to roam (the newsletter errand), so the first meeting is not missed.
        bool Live
        {
            get
            {
                var run=SchoolRunController.Instance;
                return run!=null&&GameManager.Instance!=null&&GameManager.Instance.IsPlaying&&!ComicDialogue.IsActive&&(run.RoundStarted||run.period.IsRoaming);
            }
        }
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
        /// <summary>A new run: back on his first bench (the rumour, once told, stays told).</summary>
        public void ResetRun(){Hush();movePending=false;Seat=0;if(seats!=null&&seats.Length>0)transform.SetPositionAndRotation(seats[0].position,seats[0].rotation);distractedUntil=0;needsSpace=false;warned=false;}
        void Update()
        {
            // A chat just ended: once out of the player's sight, he moves on to another bench.
            if(wasTalking&&!talking&&ToldRumour)movePending=true;wasTalking=talking;
            if(!Live){if(talking&&(GameManager.Instance==null||!GameManager.Instance.IsPlaying))Hush();return;}
            if(player==null){player=SchoolRunController.Instance.period.Player;movement=player.GetComponent<FirstPersonController>();}
            var delta=player.transform.position-transform.position;delta.y=0;
            float distance=delta.magnitude,warnAt=Mathf.Max(warningDistance,Reach+3);
            if(talking){Talk(distance);return;}
            if(movePending&&(distance>moveAwayDistance||!Visible(player.transform.position))){movePending=false;MoveSeat();return;}
            if(distance>warnAt+1){needsSpace=false;warned=false;}
            if(!Available||movement.MovementLocked||movement.ForcedCorridorRun||movement.IsFallen||player.InputLocked)return;
            // This pupil faces into the hall. Passing behind the bench never triggers a conversation.
            if(Vector3.Dot(transform.forward,delta)<0||!Visible(player.transform.position))return;
            if(ToldRumour&&!warned&&distance<warnAt)
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
                return;
            }
            AudioClip clip;string words;
            if(!ToldRumour){clip=Resources.Load<AudioClip>("Audio/"+RumourClip);words=RumourLine;}
            else{int line=PickLine(out clip);words=DarkModeDialogue.Active?DarkModeDialogue.Chatterbox[line].text:lineTexts[line];}
            Words=words;Shown="";talking=true;talkStart=Time.time;nextNoise=Time.time+.5f;
            // Long enough to read the whole subtitle, or to hear the whole recording.
            talkEnds=Time.time+Mathf.Max(words.Length/lettersPerSecond+2.2f,clip!=null?clip.length+.5f:0);
            speech.clip=clip;if(clip!=null)speech.Play();
            Interruptions++;
        }
        void Talk(float distance)
        {
            // Rooted until he's done. A short rolling hold, not the shared MovementLocked, so it can never outlive the chat.
            movement?.GlueFeet(.2f);
            int letters=Mathf.Min(Words.Length,Mathf.FloorToInt((Time.time-talkStart)*lettersPerSecond));
            Shown=Words.Substring(0,letters);
            HudController.Instance?.SetBark("Chatterbox: "+Shown,.3f);
            if(SchoolRunController.Instance.RoundStarted&&Time.time>=nextNoise){nextNoise=Time.time+noiseEvery;NoiseEvents.Emit(transform.position,noiseRadius,"chatterbox");}
            if(distance>cutOffDistance)
            {
                // Walked off mid-sentence. A rumour cut short is told again next time.
                CutOffs++;Hush();HudController.Instance?.SetBark("Chatterbox: Hey! I wasn't finished!",2f);
                availableAt=Time.time+cooldownSeconds;needsSpace=true;return;
            }
            if(Time.time<talkEnds)return;
            if(Words==RumourLine)ToldRumour=true;
            Hush();availableAt=Time.time+cooldownSeconds;needsSpace=true;
        }
        void Hush()
        {
            talking=false;SetMouth(false);
            if(voice!=null)voice.Stop();
            if(speech!=null)speech.Stop();
        }
    }
}

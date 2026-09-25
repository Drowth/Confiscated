using System.Collections.Generic;
using UnityEngine;

namespace Confiscated
{
    /// <summary>A seated pupil holds a short conversation within arm's reach. A wide berth or toy avoids it.</summary>
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
        void OnEnable()=>NoiseEvents.OnNoise+=Hear;
        void OnDisable()
        {
            NoiseEvents.OnNoise-=Hear;
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
        void Hear(Vector3 position,float radius,string source)
        {
            if(!Live||source!="clockwork toy"||Vector3.Distance(position,transform.position)>Mathf.Min(radius,10))return;
            bool first=!Distracted;
            distractedUntil=Time.time+6;
            if(first&&player!=null&&Vector3.Distance(player.transform.position,transform.position)<8)
                HudController.Instance?.SetStatus("The chatterbox is listening to the toy. Slip past.",3);
        }
        bool Visible(Vector3 target)
        {
            // Start above the bench back; walls and shut doors still prevent an encounter.
            return !Physics.Linecast(transform.position+Vector3.up*1.1f,target+Vector3.up,out var hit,~0,QueryTriggerInteraction.Ignore)||hit.transform.IsChildOf(player.transform);
        }
        void Update()
        {
            if(!Live)return;
            if(player==null){player=SchoolRunController.Instance.period.Player;movement=player.GetComponent<FirstPersonController>();}
            var delta=player.transform.position-transform.position;delta.y=0;
            float distance=delta.magnitude;
            if(distance>warningDistance+1){needsSpace=false;warned=false;}
            if(!Available||movement.MovementLocked||movement.ForcedCorridorRun||movement.IsFallen||player.InputLocked)return;
            // This pupil faces into the hall. Passing behind the bench never triggers a conversation.
            if(Vector3.Dot(transform.forward,delta)<0||!Visible(player.transform.position))return;
            if(!warned&&distance<warningDistance)
            {
                warned=true;
                HudController.Instance?.SetStatus("Chatterbox ahead. Give him space, or distract him with a wind-up toy.",4);
            }
            if(distance>reach)return;
            int line=PickLine(out var clip);
            // Hold him for the whole recording, plus a beat; the placeholder lines use the default hold.
            string words=DarkModeDialogue.Active?DarkModeDialogue.Chatterbox[line].text:lineTexts[line];
            if(!ComicDialogue.TrySpeakTimed(transform,"Chatterbox",words,clip!=null?clip.length+.5f:interruptionSeconds))return;
            speech.clip=clip;if(clip!=null)speech.Play();
            Interruptions++;availableAt=Time.time+cooldownSeconds;needsSpace=true;
            HudController.Instance?.SetStatus(null);
        }
    }
}

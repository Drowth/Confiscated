using System.Collections.Generic;
using UnityEngine;
namespace Confiscated
{
    /// <summary>Contextual words and matching voice resources, selected only once the power has failed.</summary>
    public static class DarkModeDialogue
    {
        public sealed class Line
        {
            public readonly string speaker,text,clip;
            public Line(string who,string words,string resource){speaker=who;text=words;clip="DarkMode/"+resource;}
            public string Caption=>speaker+": "+text;
        }
        public static bool Active=>SchoolGameMode.Dark&&GameManager.Instance!=null&&GameManager.Instance.GetComponent<DarkModeController>()?.BlackedOut==true;
        public static readonly Line PowerOut=new("Mr Reed","The power's gone. Keep calm. Smith, do you still have that torch I confiscated last week?","ReedPowerOut");
        static readonly Dictionary<string,Line> replacements=new()
        {
            ["Caretaker: Smith, why aren't you in class? Show me your hall pass."]=new("Caretaker","Smith? What are you doing out here, the lights are out? go back to class!","CaretakerHallPass"),
            ["Caretaker: Mr Reed's delivery? Straight to the tray and back. No other rooms, Smith."]=new("Caretaker","I don't have time for this, drop the newsletters where you must, I need to find the trip switch. Wait in class.","CaretakerDelivery"),
            ["Caretaker: That phone was confiscated, Smith. Detention!"]=new("Caretaker","I can see that phone glowing, Smith. Back to your class!", "CaretakerPhone"),
            ["Caretaker: Get away from that door, Smith! That's my key!"]=new("Caretaker","Lights out doesn't mean you can sneak into my office, Smith!", "CaretakerDoor"),
            ["Caretaker: This area is out of bounds, Smith. Come with me."]=new("Caretaker","This area's out of bounds, even in a power cut. Come here, Smith.","CaretakerBounds"),
            ["Dinner lady: Mind out, love."]=new("Dinner lady","Mind the trolley, love. I can barely see where I'm going.","DinnerLadyMindOut"),
            ["Dinner lady: Caretaker, there's a student out of class!"]=new("Dinner lady","Caretaker! Smith's sneaking about in the dark!", "DinnerLadyTellTale"),
            ["Miss D Tenison: "+DetentionBlackboard.Introduction]=new("Miss D Tenison","A power cut is no excuse, Smith. Clear all three blackboards, then clap the rubbers clean at the cleaning station.","TenisonBoards"),
            ["Miss D Tenison: Three clean boards and two clean rubbers, Smith."]=new("Miss D Tenison","Three clean boards and two clean rubbers. I'll check them when the lights come back, Smith.","TenisonReminder"),
            ["Miss D Tenison: That will do, Master Smith. You may leave."]=new("Miss D Tenison","That will do, Master Smith. You may leave. Watch your step in the corridor.","TenisonLeave"),
            ["Miss D Tenison: This is detention. I would rather not see you here again."]=new("Miss D Tenison","This is detention, Smith. The lights may be out, but the rules still apply.","TenisonRules")
        };
        static readonly Dictionary<string,Line> barks=new()
        {
            ["CaretakerSpotted"]=new("Caretaker","Oi, Smith! The dark won't hide you from me!", "CaretakerSpotted"),
            ["CaretakerSearch"]=new("Caretaker","Darkness won't hide you, Smith. I can still hear you.","CaretakerSearch"),
            ["CaretakerWhosThere"]=new("Caretaker","Who's there? I heard someone in the dark.","CaretakerWhosThere"),
            ["CaretakerHeardThat"]=new("Caretaker","I heard that, Smith. Power cut or not, I'll find you.","CaretakerHeardThat")
        };
        public static readonly Line[] Chatterbox=
        {
            new("Chatterbox","Did you see his eyes? They're glowing red! Tell me that's just a reflection...", "ChatterboxEyes"),
            new("Chatterbox","My yo-yo glows in the dark. It would be useful now, if he hadn't confiscated it!", "ChatterboxYoYo")
        };
        public static IEnumerable<Line> Lines
        {
            get {yield return PowerOut;foreach(var l in replacements.Values)yield return l;foreach(var l in barks.Values)yield return l;foreach(var l in Chatterbox)yield return l;}
        }
        public static string Resolve(string value)=>Active&&value!=null&&replacements.TryGetValue(value,out var line)?line.Caption:value;
        public static bool TryBark(string clip,out Line line){line=null;return Active&&barks.TryGetValue(clip,out line);}
        public static bool TryCaption(string caption,out Line result)
        {
            foreach(var line in Lines)if(line.Caption==caption){result=line;return true;}
            result=null;return false;
        }
        public static float PlayWorldVoice(Line line)
        {
            var game=GameManager.Instance;Transform actor=line.speaker switch
            {
                "Mr Reed"=>game?.schoolPeriod?.teacher?.transform,
                "Caretaker"=>game?.schoolPeriod?.caretaker?.transform,
                "Miss D Tenison"=>game?.detention?.teacher?.transform,
                "Dinner lady"=>Object.FindFirstObjectByType<DinnerTrolleyPatrol>()?.transform,
                _=>null
            };
            if(actor==null)return 0;
            var voice=actor.GetComponent<DarkModeVoice>();if(voice==null)voice=actor.gameObject.AddComponent<DarkModeVoice>();
            var clip=Voice(line);voice.Play(clip);return clip.length;
        }
        public static AudioClip Recording(string text)
        {
            foreach(var line in Lines)if(line.text==text)return Voice(line);
            return null;
        }
        static readonly Dictionary<string,AudioClip> placeholders=new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetVoices(){foreach(var clip in placeholders.Values)if(clip!=null)Object.Destroy(clip);placeholders.Clear();}
        public static AudioClip Voice(Line line)
        {
            var clip=Resources.Load<AudioClip>("Audio/"+line.clip);if(clip!=null)return clip;
            if(placeholders.TryGetValue(line.clip,out clip)&&clip!=null)return clip;
            // The same nonverbal cartoon-syllable fallback used by the chatterbox, never an unrelated spoken recording.
            const int rate=22050;float seconds=Mathf.Clamp(line.text.Length/22f,1.8f,8);
            float pitch=line.speaker=="Caretaker"?115:line.speaker=="Mr Reed"?165:line.speaker=="Chatterbox"?240:195;
            var samples=new float[Mathf.CeilToInt(rate*seconds)];
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate,phase=Mathf.Repeat(t*6,1),envelope=Mathf.Sin(Mathf.PI*Mathf.Clamp01(phase/.78f));
                float hz=pitch+20*Mathf.Sin(t*19);samples[i]=.12f*envelope*(.5f*Mathf.Sin(2*Mathf.PI*hz*t)+.22f*Mathf.Sin(4*Mathf.PI*hz*t));
            }
            clip=AudioClip.Create("Placeholder_"+line.clip, samples.Length,1,rate,false);clip.SetData(samples,0);placeholders[line.clip]=clip;return clip;
        }
    }

    public sealed class DarkModeVoice : MonoBehaviour
    {
        AudioSource source;
        public void Play(AudioClip clip)
        {
            if(source==null)
            {
                source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;
                source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=5;source.maxDistance=30;source.dopplerLevel=0;TalkingMouth.Register(source);
            }
            source.clip=clip;source.Play();
        }
        void Update(){if(source!=null&&source.isPlaying&&(ComicDialogue.IsActive||GameManager.Instance==null||(!GameManager.Instance.IsPlaying&&GameManager.Instance.Current!=GameManager.State.Detention)))source.Stop();}
        void OnDisable(){if(source!=null)source.Stop();}
    }
}

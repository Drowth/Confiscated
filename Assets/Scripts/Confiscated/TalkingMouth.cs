using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Confiscated
{
    /// <summary>
    /// Two-frame talking for the cutout staff, Mr Men style: while a character speaks (dialogue balloon or one of their registered voice
    /// sources), the Character Cutout shader draws the other mouth state nine times a second, pausing in the gaps of a recording.
    /// Installed at run time on every renderer showing artwork listed in <see cref="Mouths"/>, so scene rebuilds cannot lose it. The grinning
    /// hunt / creepy faces are deliberately not listed: a fixed grin while he talks is the point. The chatterbox has painted frames of his own.
    /// </summary>
    [DefaultExecutionOrder(9000)] // after CaretakerGait and HuntFaces have chosen this frame's artwork
    public sealed class TalkingMouth : MonoBehaviour
    {
        /// <summary>Mouth centre and half size, and a patch of bare skin, in source-art pixels from the top left; style 1 = the artwork's mouth is open.</summary>
        struct Mouth { public float x,y,halfWidth,halfHeight,skinX,skinY;public int style; }
        const float SourceWidth=1024,SourceHeight=1536; // every listed drawing is a 1024 x 1536 portrait
        static readonly Dictionary<string,Mouth> Mouths=new()
        {
            ["T_Mr_Reed"]=new Mouth{x=508,y=263,halfWidth=25,halfHeight=15,skinX=508,skinY=285},
            ["T_Caretaker_Idle"]=new Mouth{x=514,y=276,halfWidth=26,halfHeight=15,skinX=500,skinY=294},
            ["T_Miss_D_Tenison"]=new Mouth{x=506,y=270,halfWidth=24,halfHeight=15,skinX=505,skinY=292},
            ["T_DinnerLady"]=new Mouth{x=514.5f,y=335,halfWidth=40,halfHeight=24,skinX=470,skinY=346,style=1},
        };
        static readonly List<AudioSource> voices=new();
        static readonly int BaseMap=Shader.PropertyToID("_BaseMap"),Open=Shader.PropertyToID("_MouthOpen"),Rect=Shader.PropertyToID("_MouthRect"),
            Skin=Shader.PropertyToID("_MouthSkinUV"),Style=Shader.PropertyToID("_MouthStyle");
        static readonly float[] samples=new float[256];

        /// <summary>Anything that gives a character a speaking AudioSource calls this once; the mouth of whoever owns it follows it.</summary>
        public static void Register(AudioSource voice){if(voice!=null&&!voices.Contains(voice))voices.Add(voice);}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){voices.Clear();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook(){UnityEngine.SceneManagement.SceneManager.sceneLoaded-=OnSceneLoaded;UnityEngine.SceneManagement.SceneManager.sceneLoaded+=OnSceneLoaded;InstallAll();}
        static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,UnityEngine.SceneManagement.LoadSceneMode mode)=>InstallAll();
        public static void InstallAll()
        {
            foreach(var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var material=r.sharedMaterial;
                if(material==null||material.shader.name!="Confiscated/Character Cutout"||r.GetComponent<TalkingMouth>()!=null)continue;
                var art=material.GetTexture(BaseMap);
                if(art!=null&&Mouths.ContainsKey(art.name))r.gameObject.AddComponent<TalkingMouth>();
            }
        }

        Renderer art;Transform owner;MaterialPropertyBlock block;
        bool shown;float loudest,quietSince;AudioSource heard;
        public bool IsTalking {get;private set;}
        public bool MouthChanged=>shown;

        void Awake()
        {
            art=GetComponent<Renderer>();block=new MaterialPropertyBlock();
            // The character, not the cutout quad: voices and dialogue are attached to the body that owns this artwork.
            owner=GetComponentInParent<CaretakerAI>()?.transform??GetComponentInParent<NavMeshAgent>()?.transform??GetComponentInParent<MissTenison>()?.transform??GetComponentInParent<DinnerTrolleyPatrol>()?.transform??transform;
        }
        AudioSource Speaking()
        {
            var talk=ComicDialogue.Instance;
            if(ComicDialogue.IsActive&&talk.SpeakerTransform!=null&&(talk.SpeakerTransform==owner||owner.IsChildOf(talk.SpeakerTransform)||talk.SpeakerTransform.IsChildOf(owner)))
            {
                if(talk.LineVoice!=null&&talk.LineVoice.isPlaying)return talk.LineVoice;
                IsTalking=talk.IsSpeaking;return null;
            }
            for(int i=voices.Count-1;i>=0;i--)
            {
                if(voices[i]==null){voices.RemoveAt(i);continue;}
                if(voices[i].isPlaying&&voices[i].transform.IsChildOf(owner))return voices[i];
            }
            return null;
        }
        void LateUpdate()
        {
            IsTalking=false;
            var voice=Speaking();
            if(voice!=null)
            {
                IsTalking=true;
                if(voice!=heard){heard=voice;loudest=0;quietSince=Time.unscaledTime;}
                // Shut the mouth in the pauses of a recording. Only trusted once the source has proved it reports a level at all.
                voice.GetOutputData(samples,0);float sum=0;foreach(float s in samples)sum+=s*s;float level=Mathf.Sqrt(sum/samples.Length);
                loudest=Mathf.Max(loudest,level);
                if(level>.012f||loudest<.03f)quietSince=Time.unscaledTime;
                if(Time.unscaledTime-quietSince>.12f)IsTalking=false;
            }
            else heard=null;

            art.GetPropertyBlock(block);
            var current=block.GetTexture(BaseMap);if(current==null&&art.sharedMaterial!=null)current=art.sharedMaterial.GetTexture(BaseMap);
            // Walk cycles, expressions and hunt faces have their mouths elsewhere (or a grin that should stay put).
            bool flip=IsTalking&&current!=null&&Mouths.ContainsKey(current.name)&&Mathf.FloorToInt(Time.unscaledTime*9)%2==0;
            if(flip==shown&&!flip)return;
            shown=flip;
            if(flip)
            {
                // Source-art pixels, not current.width/height: some of these import rescaled to a power of two.
                var m=Mouths[current.name];const float w=SourceWidth,h=SourceHeight;
                block.SetVector(Rect,new Vector4(m.x/w,1-m.y/h,m.halfWidth/w,m.halfHeight/h));
                block.SetVector(Skin,new Vector4(m.skinX/w,1-m.skinY/h,0,0));block.SetFloat(Style,m.style);
            }
            block.SetFloat(Open,flip?1:0);art.SetPropertyBlock(block);
        }
        void OnDisable(){if(art==null||!shown)return;shown=false;art.GetPropertyBlock(block);block.SetFloat(Open,0);art.SetPropertyBlock(block);}
    }
}

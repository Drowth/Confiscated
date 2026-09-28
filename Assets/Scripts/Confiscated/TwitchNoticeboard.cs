using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Confiscated
{
    /// <summary>
    /// A cork noticeboard in the Year 6 corridor that Twitch chat writes on: "!note your message" pins a sticky note signed
    /// with the viewer's name. One note per viewer (a new one replaces theirs), twelve on the board, oldest falls off. Purely
    /// cosmetic. Filtered: short, printable text only, no links, a basic word list. Hidden unless chat is live and notes are on.
    /// Notes last the session, through retries.
    /// </summary>
    public sealed class TwitchNoticeboard : MonoBehaviour
    {
        public const string NotesKey="Confiscated.Twitch.ChatNotes";
        public const int MaxNotes=9,MaxLength=48;
        // A big chat can post dozens a second: queue them and pin one at a time, with a cooldown per viewer.
        public const float PinEverySeconds=5,ViewerCooldownSeconds=60;
        const int MaxPending=30;
        static readonly List<(string user,string text)> pending=new();
        static readonly Dictionary<string,float> lastAccepted=new(System.StringComparer.OrdinalIgnoreCase);
        float nextPin;
        public static int Pending=>pending.Count;
        public static bool NotesOn{get=>PlayerPrefs.GetInt(NotesKey,1)==1;set{PlayerPrefs.SetInt(NotesKey,value?1:0);PlayerPrefs.Save();}}
        public static TwitchNoticeboard Instance {get;private set;}
        // (viewer, message), oldest first. Static so the board survives a retry's scene reload.
        static readonly List<(string user,string text)> notes=new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){notes.Clear();pending.Clear();lastAccepted.Clear();Instance=null;}
        public static IReadOnlyList<(string user,string text)> Notes=>notes;

        static readonly string[] Blocked={"fuck","shit","cunt","bitch","nigg","fag","retard","slut","whore","dick","cock","pussy","twat","wank","rape","nazi","kys","kill yourself","porn","sex"};
        static readonly Color[] Paper={new(1f,.93f,.45f),new(1f,.74f,.80f),new(.70f,.87f,1f),new(.76f,.95f,.68f)};

        Transform board;readonly List<GameObject> pinned=new();
        public bool Visible=>board!=null&&board.gameObject.activeSelf;
        public Transform Board=>board;
        public int PinnedCount=>pinned.Count;

        void Awake(){Instance=this;}
        void OnEnable()=>TwitchChat.Received+=OnChat;
        void OnDisable()=>TwitchChat.Received-=OnChat;
        void OnDestroy(){if(Instance==this)Instance=null;if(board!=null)Destroy(board.gameObject);}

        void Update()
        {
            bool show=TwitchChat.Live&&NotesOn;
            if(show&&board==null){Build();Repin();}
            if(board!=null&&board.gameObject.activeSelf!=show)board.gameObject.SetActive(show);
            if(pending.Count>0&&Time.unscaledTime>=nextPin)PinNext();
        }
        /// <summary>Moves the oldest queued note onto the board (tests call it directly).</summary>
        public void PinNext()
        {
            if(pending.Count==0)return;
            var n=pending[0];pending.RemoveAt(0);nextPin=Time.unscaledTime+PinEverySeconds;
            notes.RemoveAll(x=>string.Equals(x.user,n.user,System.StringComparison.OrdinalIgnoreCase));
            notes.Add(n);while(notes.Count>MaxNotes)notes.RemoveAt(0);
            if(board!=null)Repin();
        }

        void OnChat(string user,string text)
        {
            if(!NotesOn)return;
            var t=text.Trim();
            if(!t.StartsWith("!note ",System.StringComparison.OrdinalIgnoreCase))return;
            Post(user,t.Substring(6));
        }

        /// <summary>Queues a note. False when the filter rejects it or the viewer posted one in the last minute.</summary>
        public bool Post(string user,string message)
        {
            string clean=Clean(message);if(clean==null)return false;
            float now=Time.unscaledTime;
            if(lastAccepted.TryGetValue(user,out var at)&&now-at<ViewerCooldownSeconds)return false;
            lastAccepted[user]=now;
            pending.RemoveAll(n=>string.Equals(n.user,user,System.StringComparison.OrdinalIgnoreCase));
            pending.Add((user,clean));
            while(pending.Count>MaxPending)pending.RemoveAt(0); // keep the freshest
            return true;
        }

        /// <summary>Printable text only (the handwriting font has no emoji), trimmed to length; null if empty, a link or blocked.</summary>
        public static string Clean(string message)
        {
            var sb=new StringBuilder();
            foreach(char c in message??"")if(c>=32&&c<127&&c!='<'&&c!='>')sb.Append(c);
            string s=System.Text.RegularExpressions.Regex.Replace(sb.ToString(),@"\s+"," ").Trim();
            if(s.Length==0)return null;
            string lower=s.ToLowerInvariant();
            if(lower.Contains("http")||lower.Contains("www")||System.Text.RegularExpressions.Regex.IsMatch(lower,@"\w\.(com|tv|gg|net|org|io|ly|co|uk|xyz)\b"))return null;
            string squashed=new string(lower.Where(char.IsLetter).ToArray());
            if(Blocked.Any(b=>squashed.Contains(b.Replace(" ",""))))return null;
            return s.Length>MaxLength?s.Substring(0,MaxLength-3).TrimEnd()+"...":s;
        }

        // ------------------------------------------------------------------ the board

        void Build()
        {
            if(!FindWall(out var at,out var intoWall))return;
            board=new GameObject("Chat noticeboard").transform;
            board.SetPositionAndRotation(at,Quaternion.LookRotation(intoWall)); // -Z faces the corridor (TextMesh reads from -Z)
            var wood=Mat(new Color(.42f,.27f,.15f));var cork=Mat(new Color(.72f,.53f,.33f));var header=Mat(new Color(.97f,.95f,.88f));
            Box("Frame",new Vector3(0,0,.01f),new Vector3(1.66f,1.14f,.03f),wood);
            Box("Cork",new Vector3(0,0,-.006f),new Vector3(1.56f,1.04f,.01f),cork);
            Box("Header card",new Vector3(0,.44f,-.013f),new Vector3(1.1f,.13f,.004f),header);
            Words("CHAT NOTICEBOARD",new Vector3(0,.455f,-.017f),.0048f,FontStyle.Bold,board);
            Words("type  !note your message  in chat",new Vector3(0,.405f,-.017f),.0032f,FontStyle.Italic,board);
        }

        /// <summary>A clear 1.7 m stretch of the Year 6 corridor's west wall, between its windows.</summary>
        static bool FindWall(out Vector3 at,out Vector3 intoWall)
        {
            at=default;intoWall=Vector3.left;
            for(float z=19.3f;z<=23.5f;z+=.25f)foreach(float dz in new[]{0f,-.35f,.35f,-.7f,.7f})
            {
                float zz=z+dz;
                bool clear=true;float wallX=0;
                foreach(float o in new[]{-.85f,0,.85f})
                    foreach(float y in new[]{1.0f,1.6f,2.1f})
                    {
                        var from=new Vector3(-33.9f,y,zz+o);
                        if(!Physics.Raycast(from,Vector3.left,out var hit,3f,~0,QueryTriggerInteraction.Ignore)||!hit.collider.name.StartsWith("Wall_")){clear=false;break;}
                        if(wallX==0)wallX=hit.point.x;else if(Mathf.Abs(hit.point.x-wallX)>.03f){clear=false;break;}
                    }
                if(!clear)continue;
                at=new Vector3(wallX+.03f,1.55f,zz);return true;
            }
            return false;
        }

        void Repin()
        {
            foreach(var p in pinned)if(p!=null)Destroy(p);pinned.Clear();
            if(board==null)return;
            for(int i=0;i<notes.Count;i++)
            {
                int col=i%3,row=i/3;
                var rng=new System.Random(notes[i].user.GetHashCode()^notes[i].text.GetHashCode());
                var note=new GameObject("Note from "+notes[i].user).transform;note.SetParent(board,false);
                note.localPosition=new Vector3(-.5f+col*.5f,.21f-row*.305f,-.018f);
                note.localRotation=Quaternion.Euler(0,0,(float)(rng.NextDouble()*10-5));
                var paper=GameObject.CreatePrimitive(PrimitiveType.Cube);Destroy(paper.GetComponent<Collider>());
                paper.transform.SetParent(note,false);paper.transform.localScale=new Vector3(.45f,.28f,.003f);
                paper.GetComponent<Renderer>().sharedMaterial=Mat(Paper[rng.Next(Paper.Length)]);
                var pin=GameObject.CreatePrimitive(PrimitiveType.Sphere);Destroy(pin.GetComponent<Collider>());
                pin.transform.SetParent(note,false);pin.transform.localPosition=new Vector3(0,.12f,-.006f);pin.transform.localScale=Vector3.one*.018f;
                pin.GetComponent<Renderer>().sharedMaterial=Mat(new Color(.8f,.12f,.1f));
                Words(Wrap(notes[i].text,16),new Vector3(0,.025f,-.004f),.0056f,FontStyle.Normal,note);
                Words("- "+notes[i].user,new Vector3(0,-.105f,-.004f),.0042f,FontStyle.Italic,note);
                pinned.Add(note.gameObject);
            }
        }

        static string Wrap(string s,int width)
        {
            var lines=new List<string>();var line="";
            foreach(var word in s.Split(' '))
            {
                var w=word;
                while(w.Length>width){if(line.Length>0){lines.Add(line);line="";}lines.Add(w.Substring(0,width));w=w.Substring(width);}
                if(line.Length+w.Length+(line.Length>0?1:0)>width){lines.Add(line);line=w;}else line=line.Length>0?line+" "+w:w;
            }
            if(line.Length>0)lines.Add(line);
            return string.Join("\n",lines.Take(3));
        }

        readonly Dictionary<Color,Material> materials=new();
        Material Mat(Color c)
        {
            if(materials.TryGetValue(c,out var m))return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=c};m.SetFloat("_Smoothness",.05f);
            return materials[c]=m;
        }
        void Box(string name,Vector3 local,Vector3 size,Material m)
        {
            var b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.name=name;Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(board,false);b.transform.localPosition=local;b.transform.localScale=size;b.GetComponent<Renderer>().sharedMaterial=m;
        }
        static void Words(string text,Vector3 local,float size,FontStyle style,Transform parent)
        {
            var g=new GameObject(text.Length>20?text.Substring(0,20):text);g.transform.SetParent(parent,false);g.transform.localPosition=local;
            var t=g.AddComponent<TextMesh>();t.font=SchoolTypography.Font;t.fontSize=64;t.characterSize=size;t.fontStyle=style;
            t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=Color.black;t.text=text;
            g.AddComponent<WorldLabel>();
        }
    }
}

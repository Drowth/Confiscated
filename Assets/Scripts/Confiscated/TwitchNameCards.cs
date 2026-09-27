using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Confiscated
{
    /// <summary>
    /// Twitch viewers claim the school: each new chatter gets a random free corridor locker or pupil desk, and a paper
    /// name card appears on it (a label on the locker door, a folded card on the desk, readable from both sides).
    /// Claims last for the session, across retries. Hidden unless chat is live and viewer names are on.
    /// </summary>
    public sealed class TwitchNameCards : MonoBehaviour
    {
        public enum Kind { Locker, Desk }
        sealed class Slot { public string key; public Kind kind; public Transform target; public GameObject card; }

        // user -> slot key. Static so a viewer keeps their locker through a retry (the scene reloads).
        static readonly Dictionary<string,string> claims=new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()=>claims.Clear();
        // Chat bots would otherwise take the first lockers of every stream.
        static readonly HashSet<string> Bots=new(System.StringComparer.OrdinalIgnoreCase)
        {"nightbot","streamelements","streamlabs","moobot","fossabot","sery_bot","wizebot","botrixoficial","kofistreambot","soundalerts","commanderroot","streamstickers"};

        public static TwitchNameCards Instance {get;private set;}
        readonly Dictionary<string,Slot> slots=new();
        Transform root;Material paper;
        public int SlotCount=>slots.Count;
        public int FreeSlots=>slots.Values.Count(s=>s.card==null);
        public bool Visible=>root!=null&&root.gameObject.activeSelf;
        public GameObject CardFor(string user)=>claims.TryGetValue(user,out var key)&&slots.TryGetValue(key,out var s)?s.card:null;
        public Kind? KindFor(string user)=>claims.TryGetValue(user,out var key)&&slots.TryGetValue(key,out var s)?s.kind:null;
        public Transform TargetFor(string user)=>claims.TryGetValue(user,out var key)&&slots.TryGetValue(key,out var s)?s.target:null;

        void Awake()
        {
            Instance=this;
            root=new GameObject("Twitch name cards").transform;
            foreach(var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if(t.name=="Locker"&&t.GetComponent<Renderer>()!=null&&t.GetComponentInParent<PlayerLocker>()==null)Add(t,Kind.Locker);
                else if(t.name.StartsWith("PupilDesk")&&t.GetComponentInChildren<Renderer>()!=null)Add(t,Kind.Desk);
            }
            // Plain cream card stock, lit by the school's lights (so it goes dark in a blackout). The scene's paper
            // material carries the pencil-grid texture, which drowns out a name at this size.
            paper=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Twitch name card"};
            paper.SetColor("_BaseColor",new Color(.97f,.94f,.84f));paper.SetFloat("_Smoothness",.05f);
            // Viewers who claimed a spot before this retry get it back.
            foreach(var c in claims)if(slots.TryGetValue(c.Value,out var s))Build(s,c.Key);
            root.gameObject.SetActive(false);
        }
        void Add(Transform t,Kind kind){string key=Path(t);if(!slots.ContainsKey(key))slots.Add(key,new Slot{key=key,kind=kind,target=t});}
        static string Path(Transform t){var s=t.name+"@"+t.position.ToString("F1");return s;}
        void OnEnable()=>TwitchChat.Received+=OnChat;
        void OnDisable()=>TwitchChat.Received-=OnChat;
        void OnDestroy(){if(Instance==this)Instance=null;if(root!=null)Destroy(root.gameObject);if(paper!=null)Destroy(paper);}
        void Update()
        {
            bool show=TwitchChat.Live&&TwitchChat.ShowNames;
            if(root.gameObject.activeSelf!=show)root.gameObject.SetActive(show);
        }

        void OnChat(string user,string text)=>Claim(user);
        /// <summary>Gives a viewer a random free locker or desk. Returns false for bots, repeat viewers, or a full school.</summary>
        public bool Claim(string user)
        {
            if(string.IsNullOrEmpty(user)||Bots.Contains(user)||claims.ContainsKey(user))return false;
            var free=slots.Values.Where(s=>s.card==null).ToList();if(free.Count==0)return false;
            var slot=free[Random.Range(0,free.Count)];
            claims[user]=slot.key;Build(slot,user);return true;
        }

        void Build(Slot slot,string user)
        {
            if(slot.card!=null)return;
            if(slot.kind==Kind.Locker)BuildLocker(slot,user);else BuildDesk(slot,user);
        }

        void BuildLocker(Slot slot,string user)
        {
            var body=slot.target.GetComponent<Renderer>().bounds;
            // Locker doors face away from the wall, along -forward (as P_PlayerLocker's nameplate does).
            Vector3 front=-slot.target.forward;
            float depth=Vector3.Scale(body.extents,Abs(front)).magnitude;
            Vector3 at=new Vector3(body.center.x,body.min.y+body.size.y*.79f,body.center.z)+front*(depth+.004f);
            var card=Group("Locker name - "+user,at,Quaternion.LookRotation(-front));
            Paper(card,new Vector3(.34f,.1f,.006f),Vector3.zero);
            Letters(card,user,new Vector3(0,0,-.005f),Quaternion.identity,.3f);
            slot.card=card.gameObject;
        }

        void BuildDesk(Slot slot,string user)
        {
            var top=slot.target.Find("Top")?.GetComponent<Renderer>();
            var bounds=top!=null?top.bounds:slot.target.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
            // The card stands along the desk's long edge, at the front (the modesty panel side), readable from both sides.
            Vector3 along=bounds.size.x>=bounds.size.z?Vector3.right:Vector3.forward;
            Vector3 across=Vector3.Cross(Vector3.up,along);
            var modesty=slot.target.Find("Modesty");
            float side=modesty!=null?Mathf.Sign(Vector3.Dot(modesty.position-bounds.center,across)):1;
            float half=Vector3.Scale(bounds.extents,Abs(across)).magnitude;
            Vector3 at=new Vector3(bounds.center.x,bounds.max.y+.045f,bounds.center.z)+across*side*Mathf.Max(0,half-.09f);
            var card=Group("Desk name - "+user,at,Quaternion.LookRotation(across));
            Paper(card,new Vector3(.26f,.085f,.004f),Vector3.zero);
            Letters(card,user,new Vector3(0,0,-.0035f),Quaternion.identity,.23f);
            Letters(card,user,new Vector3(0,0,.0035f),Quaternion.Euler(0,180,0),.23f);
            slot.card=card.gameObject;
        }

        static Vector3 Abs(Vector3 v)=>new(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
        Transform Group(string name,Vector3 at,Quaternion rotation)
        {var g=new GameObject(name).transform;g.SetParent(root,false);g.SetPositionAndRotation(at,rotation);return g;}
        void Paper(Transform parent,Vector3 size,Vector3 offset)
        {
            var p=GameObject.CreatePrimitive(PrimitiveType.Cube);p.name="Paper";Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(parent,false);p.transform.localPosition=offset;p.transform.localScale=size;
            var r=p.GetComponent<Renderer>();r.sharedMaterial=paper;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        static void Letters(Transform parent,string user,Vector3 offset,Quaternion rotation,float maxWidth)
        {
            // Built unrotated in world space first, so its bounds are its true width; then fitted and placed.
            var g=new GameObject("Name");
            var t=g.AddComponent<TextMesh>();t.font=SchoolTypography.Font;t.fontSize=64;t.characterSize=.0055f;t.fontStyle=FontStyle.Bold;
            t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=Color.black;t.text=user;
            g.AddComponent<WorldLabel>();
            float width=g.GetComponent<Renderer>().bounds.size.x;
            if(width>maxWidth)t.characterSize*=maxWidth/width; // long names shrink to fit the card
            g.transform.SetParent(parent,false);g.transform.localPosition=offset;g.transform.localRotation=rotation;
        }
    }
}

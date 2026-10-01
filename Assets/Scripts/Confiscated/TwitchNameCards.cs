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
        sealed class Slot { public string key; public Kind kind; public Transform target; public GameObject card; public ViewerLockerHide hide; }

        // user -> slot key. Static so a viewer keeps their locker through a retry (the scene reloads).
        static readonly Dictionary<string,string> claims=new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){claims.Clear();lastSpoke.Clear();}
        // When every spot is taken (a big stream fills 83 in seconds), the viewer quiet for longest gives theirs up.
        static readonly Dictionary<string,float> lastSpoke=new(System.StringComparer.OrdinalIgnoreCase);
        public const float RecycleAfterSeconds=300;
        public int Recycled {get;private set;}
        // Chat bots would otherwise take the first lockers of every stream.
        static readonly HashSet<string> Bots=new(System.StringComparer.OrdinalIgnoreCase)
        {"nightbot","streamelements","streamlabs","moobot","fossabot","sery_bot","wizebot","botrixoficial","kofistreambot","soundalerts","commanderroot","streamstickers"};

        public static TwitchNameCards Instance {get;private set;}
        readonly Dictionary<string,Slot> slots=new();
        Transform root,hides;Material paper;
        public int SlotCount=>slots.Count;
        public int FreeSlots=>slots.Values.Count(s=>s.card==null);
        public bool Visible=>root!=null&&root.gameObject.activeSelf;
        public GameObject CardFor(string user)=>claims.TryGetValue(user,out var key)&&slots.TryGetValue(key,out var s)?s.card:null;
        public Kind? KindFor(string user)=>claims.TryGetValue(user,out var key)&&slots.TryGetValue(key,out var s)?s.kind:null;
        public ViewerLockerHide HideFor(string user)=>claims.TryGetValue(user,out var key)&&slots.TryGetValue(key,out var s)?s.hide:null;
        public Transform TargetFor(string user)=>claims.TryGetValue(user,out var key)&&slots.TryGetValue(key,out var s)?s.target:null;

        void Awake()
        {
            Instance=this;
            root=new GameObject("Twitch name cards").transform;
            hides=new GameObject("Twitch locker hides").transform;
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
            // Without a live Twitch chat there'd otherwise be zero claimed lockers -- and so nowhere to hide -- so
            // one locker in the bank always works the same way a claimed one would, just without a name on it.
            if(!TwitchChat.Live)EnsureOfflineHidingSpot();
            root.gameObject.SetActive(false);
        }
        // "Nobody" reads fine in both the prompt ("hide in Nobody's locker") and the scrawl ("Nobody woz ere").
        public const string OfflineViewer="Nobody";
        void EnsureOfflineHidingSpot()
        {
            if(claims.ContainsKey(OfflineViewer))return;
            var free=slots.Values.Where(s=>s.kind==Kind.Locker&&s.card==null).ToList();
            if(free.Count==0)return;
            var slot=free[Random.Range(0,free.Count)];
            claims[OfflineViewer]=slot.key;Build(slot,OfflineViewer);
        }
        void Add(Transform t,Kind kind){string key=Path(t);if(!slots.ContainsKey(key))slots.Add(key,new Slot{key=key,kind=kind,target=t});}
        static string Path(Transform t){var s=t.name+"@"+t.position.ToString("F1");return s;}
        void OnEnable()=>TwitchChat.Received+=OnChat;
        void OnDisable()=>TwitchChat.Received-=OnChat;
        void OnDestroy(){if(Instance==this)Instance=null;if(root!=null)Destroy(root.gameObject);if(hides!=null)Destroy(hides.gameObject);if(paper!=null)Destroy(paper);}
        void Update()
        {
            bool show=TwitchChat.Live&&TwitchChat.ShowNames;
            if(root.gameObject.activeSelf!=show)root.gameObject.SetActive(show);
        }

        void OnChat(string user,string text){lastSpoke[user]=Time.realtimeSinceStartup;Claim(user);}
        /// <summary>Gives a viewer a random free locker or desk. Returns false for bots, repeat viewers, or a full school.</summary>
        public bool Claim(string user)
        {
            if(string.IsNullOrEmpty(user)||Bots.Contains(user)||claims.ContainsKey(user))return false;
            var free=slots.Values.Where(s=>s.card==null).ToList();
            if(free.Count==0)
            {
                var spare=Quietest();if(spare==null)return false;
                free.Add(spare);Recycled++;
            }
            var slot=free[Random.Range(0,free.Count)];
            claims[user]=slot.key;if(!lastSpoke.ContainsKey(user))lastSpoke[user]=Time.realtimeSinceStartup;Build(slot,user);return true;
        }

        /// <summary>Frees the spot of the longest-silent viewer (5+ minutes quiet, and not hiding in it right now).</summary>
        Slot Quietest()
        {
            float now=Time.realtimeSinceStartup;string victim=null;float oldest=float.MaxValue;
            foreach(var c in claims)
            {
                float spoke=lastSpoke.TryGetValue(c.Key,out var t)?t:0;
                if(now-spoke<RecycleAfterSeconds||spoke>=oldest||!slots.TryGetValue(c.Value,out var s))continue;
                if(ViewerLockerHide.Current!=null&&ViewerLockerHide.Current==s.hide)continue;
                victim=c.Key;oldest=spoke;
            }
            if(victim==null)return null;
            var slot=slots[claims[victim]];claims.Remove(victim);
            if(slot.card!=null)Destroy(slot.card);slot.card=null;
            if(slot.hide!=null)Destroy(slot.hide.gameObject);slot.hide=null;
            return slot;
        }
        /// <summary>Test hook: pretend a viewer last spoke this many seconds ago.</summary>
        public static void AgeForTest(string user,float seconds)=>lastSpoke[user]=Time.realtimeSinceStartup-seconds;

        void Build(Slot slot,string user)
        {
            if(slot.card!=null)return;
            if(slot.kind==Kind.Locker)BuildLocker(slot,user);else BuildDesk(slot,user);
        }

        // Each locker model's own brass nameplate, measured off its mesh + texture (the brass-coloured part of the door
        // face): centre across the door and up from the locker's centre, width, height, and how far its face stands
        // out from the centre. Metres, relative to the Locker placeholder's bounds.
        static readonly Dictionary<string,float[]> Nameplates=new()
        {
            ["SchoolLocker1"]=new[]{-.004f,.535f,.180f,.056f,.140f},
            ["SchoolLocker2"]=new[]{-.006f,.534f,.190f,.059f,.143f},
            ["SchoolLocker3"]=new[]{-.004f,.532f,.185f,.058f,.140f},
            ["SchoolLocker4"]=new[]{.002f,.535f,.191f,.064f,.136f},
            ["SchoolLocker5"]=new[]{-.001f,.537f,.190f,.057f,.137f},
            ["SchoolLocker6"]=new[]{0f,.534f,.182f,.059f,.141f},
        };
        static float[] NameplateOf(Transform locker)
        {
            foreach(var r in locker.GetComponentsInChildren<MeshRenderer>(true))
                if(Nameplates.TryGetValue(r.name,out var plate))return plate;
            return Nameplates["SchoolLocker1"];
        }

        void BuildLocker(Slot slot,string user)
        {
            var body=slot.target.GetComponent<Renderer>().bounds;
            // Locker doors face away from the wall, along -forward (as P_PlayerLocker's nameplate does).
            Vector3 front=-slot.target.forward,right=Vector3.Cross(Vector3.up,front).normalized*-1;
            float depth=Vector3.Scale(body.extents,Abs(front)).magnitude;
            // The card covers exactly the locker's own nameplate: same size, laid flat on its face.
            var plate=NameplateOf(slot.target);
            Vector3 at=body.center+right*plate[0]+Vector3.up*plate[1]+front*(plate[4]+.0015f);
            var card=Group("Locker name - "+user,at,Quaternion.LookRotation(-front));
            Paper(card,new Vector3(plate[2],plate[3],.002f),Vector3.zero);
            Letters(card,user,new Vector3(0,0,-.0015f),Quaternion.identity,plate[2]*.9f,plate[3]*.8f);
            // A viewer's locker is a hiding place. The trigger sits on Ignore Raycast, so only the interact ray sees it.
            // Under its own always-active, unscaled root rather than root/card: root gets switched off whenever Twitch
            // chat isn't live to show names, which used to take the hide trigger down with it (its Awake, which creates
            // the AudioSource DoorSounds plays through, never ran on an inactive hierarchy). Not under the locker
            // either -- that's a scaled cube, which squashed the trigger and the whole built interior out of shape.
            var door=new GameObject("Hide in "+user+"'s locker");door.layer=2;door.transform.SetParent(hides,false);
            door.transform.SetPositionAndRotation(new Vector3(body.center.x,body.center.y,body.center.z)+front*(depth+.02f),Quaternion.LookRotation(-front));
            var trigger=door.AddComponent<BoxCollider>();trigger.isTrigger=true;
            trigger.size=new Vector3(Vector3.Scale(body.size,Abs(Vector3.Cross(Vector3.up,front))).magnitude,body.size.y,.06f);
            var hide=door.AddComponent<ViewerLockerHide>();hide.viewer=user;hide.locker=slot.target;hide.card=card.gameObject;
            slot.card=card.gameObject;slot.hide=hide;
            // The offline stand-in isn't a real viewer: no name card on the outside, just the scrawl once you're in.
            if(user==OfflineViewer)card.gameObject.SetActive(false);
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
        static void Letters(Transform parent,string user,Vector3 offset,Quaternion rotation,float maxWidth,float maxHeight=float.MaxValue)
        {
            // Built unrotated in world space first, so its bounds are its true size; then fitted and placed.
            var g=new GameObject("Name");
            var t=g.AddComponent<TextMesh>();t.font=SchoolTypography.Font;t.fontSize=64;t.characterSize=.0055f;t.fontStyle=FontStyle.Bold;
            t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=Color.black;t.text=user;
            g.AddComponent<WorldLabel>();
            var size=g.GetComponent<Renderer>().bounds.size;
            t.characterSize*=Mathf.Min(1,maxWidth/Mathf.Max(size.x,.0001f),maxHeight/Mathf.Max(size.y,.0001f)); // long names shrink to fit the card
            g.transform.SetParent(parent,false);g.transform.localPosition=offset;g.transform.localRotation=rotation;
        }
    }
}

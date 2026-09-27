using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Confiscated
{
    /// <summary>
    /// Twitch chat votes on what happens next, during the chase round only. Every so often three options appear; viewers
    /// type 1, 2 or 3 (one vote each, the latest counts) and the winner fires, credited to a viewer who voted for it.
    /// Attaches itself to the school run at scene load, so no scene rebuild can lose it. Idle unless chat is live.
    /// </summary>
    public sealed class TwitchChaos : MonoBehaviour
    {
        public enum Effect { Noise, TellTale, Flicker, WetFloor, GlueFeet, Teleport, Detention, TeaBreak, SugarRush }
        public float firstVoteDelay=25,voteInterval=45,voteSeconds=20,resultSeconds=5;

        public static TwitchChaos Instance {get;private set;}
        public bool Voting {get;private set;}
        public Effect[] Options {get;private set;}=new Effect[0];
        public int[] Tallies {get;private set;}=new int[0];
        public float VoteRemaining {get;private set;}
        public float NextVoteIn {get;private set;}
        public Effect? LastWinner {get;private set;}
        public string LastCredit {get;private set;}
        public int VotesHeld {get;private set;}
        public GameObject Panel=>panel;

        readonly Dictionary<string,int> ballots=new();
        float resultUntil;
        GameObject panel;Text heading,body;
        List<(Light light,float intensity)> fixtures;
        static readonly Color Cream=new(.97f,.94f,.83f),Ink=new(.055f,.075f,.105f),Twitch=new(.57f,.27f,1f);

        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){hooked=false;Instance=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            if(!hooked){hooked=true;SceneManager.sceneLoaded+=(_,_)=>Attach();}
            Attach();
        }
        static void Attach()
        {
            var run=SchoolRunController.Instance!=null?SchoolRunController.Instance:Object.FindFirstObjectByType<SchoolRunController>();
            if(run!=null&&run.GetComponent<TwitchChaos>()==null)run.gameObject.AddComponent<TwitchChaos>();
        }

        public static string Label(Effect e)=>e switch
        {
            Effect.Noise=>"Make a noise near them",
            Effect.TellTale=>"Tell on them",
            Effect.Flicker=>"Lights out",
            Effect.WetFloor=>"Spill a drink",
            Effect.GlueFeet=>"Glue their shoes down",
            Effect.Teleport=>"Teleport them somewhere",
            Effect.Detention=>"DETENTION!",
            Effect.TeaBreak=>"Caretaker's tea break",
            _=>"Sugar rush",
        };
        public static bool Helpful(Effect e)=>e==Effect.TeaBreak||e==Effect.SugarRush;
        [Tooltip("Chance a vote (from the third on) offers detention, which costs the player their latest belonging.")]
        public float detentionChance=.3f;

        void Awake(){Instance=this;NextVoteIn=firstVoteDelay;}
        void OnEnable()=>TwitchChat.Received+=OnChat;
        void OnDisable(){TwitchChat.Received-=OnChat;RestoreLights();}
        void OnDestroy(){if(Instance==this)Instance=null;if(panel!=null)Destroy(panel.transform.root.gameObject);}

        bool Running
        {
            get
            {
                var game=GameManager.Instance;var run=SchoolRunController.Instance;
                return TwitchChat.Live&&game!=null&&game.IsPlaying&&run!=null&&run.RoundStarted&&!ComicDialogue.IsActive&&!PauseMenu.IsOpen;
            }
        }

        void Update()
        {
            bool running=Running;
            if(running)
            {
                // Timers only move while the chase is actually live: detention, dialogue and pause all hold them.
                if(Voting){VoteRemaining-=Time.deltaTime;if(VoteRemaining<=0)CloseVote();}
                else{NextVoteIn-=Time.deltaTime;if(NextVoteIn<=0)OpenVote();}
            }
            Draw(running);
        }

        void OnChat(string user,string text)
        {
            if(!Voting||!Running)return;
            string t=text.Trim().TrimStart('!','#');
            if(t.Length!=1||t[0]<'1'||t[0]>'0'+Options.Length)return;
            ballots[user]=t[0]-'1';
            Tallies=new int[Options.Length];foreach(var b in ballots.Values)Tallies[b]++;
        }

        List<Effect> Pool(bool helpful)
        {
            var run=SchoolRunController.Instance;var list=new List<Effect>();
            if(!helpful)
            {
                list.Add(Effect.Noise);list.Add(Effect.TellTale);
                if(!SchoolGameMode.Dark&&Fixtures().Count>0)list.Add(Effect.Flicker);
                if(Object.FindFirstObjectByType<WetFloorHazard>()!=null)list.Add(Effect.WetFloor);
                list.Add(Effect.GlueFeet);list.Add(Effect.Teleport);
            }
            else if(TwitchChat.ChatCanHelp)
            {
                if(run!=null&&run.caretaker!=null&&run.caretaker.Current!=CaretakerAI.State.Frozen&&!run.caretaker.IsGlued)list.Add(Effect.TeaBreak);
                list.Add(Effect.SugarRush);
            }
            return list;
        }

        /// <summary>Two troublemaking options, plus a third that is sometimes a mercy (when chat is allowed to help).</summary>
        public void OpenVote()
        {
            var harm=Pool(false).OrderBy(_=>Random.value).ToList();
            // Detention is the big one: never in the first two votes, never twice running, and only now and then.
            var detention=GameManager.Instance!=null?GameManager.Instance.detention:null;
            if(VotesHeld>=2&&LastWinner!=Effect.Detention&&detention!=null&&detention.Ready&&Random.value<detentionChance)harm.Insert(0,Effect.Detention);
            var pick=harm.Take(2).ToList();
            var third=harm.Skip(2).Concat(Pool(true)).OrderBy(_=>Random.value).FirstOrDefault();
            if(harm.Skip(2).Any()||Pool(true).Any())pick.Add(third);
            Options=pick.ToArray();Tallies=new int[Options.Length];ballots.Clear();
            Voting=true;VoteRemaining=voteSeconds;VotesHeld++;
        }

        public void CloseVote()
        {
            Voting=false;NextVoteIn=voteInterval;resultUntil=Time.time+resultSeconds;
            int best=Tallies.Length>0?Tallies.Max():0;
            if(best==0){LastWinner=null;LastCredit=null;return;}
            var tied=Enumerable.Range(0,Tallies.Length).Where(i=>Tallies[i]==best).ToList();
            int winner=tied[Random.Range(0,tied.Count)];
            var voters=ballots.Where(b=>b.Value==winner).Select(b=>b.Key).ToList();
            LastWinner=Options[winner];LastCredit=voters[Random.Range(0,voters.Count)];
            Apply(Options[winner],TwitchChat.ShowNames?LastCredit:"Chat");
        }

        public void Apply(Effect effect,string who)
        {
            var run=SchoolRunController.Instance;var hud=HudController.Instance;
            var player=run!=null&&run.period!=null&&run.period.Player!=null?run.period.Player.transform:null;
            if(player==null)return;
            // Emit before the bark: the caretaker's own "He heard something..." must not replace the credit line.
            switch(effect)
            {
                case Effect.Noise:
                    Vector3 offset=Quaternion.Euler(0,Random.Range(0,360f),0)*Vector3.forward*Random.Range(3f,6f);
                    Vector3 at=NavMesh.SamplePosition(player.position+offset,out var hit,4,NavMesh.AllAreas)?hit.position:player.position;
                    NoiseEvents.Emit(at,90,"twitch chat");
                    hud?.SetBark(who+" knocked a chair over right next to you!",3.5f);break;
                case Effect.TellTale:
                    NoiseEvents.Emit(player.position,90,"tell-tale");
                    hud?.SetBark(who+" shouted \"SIIIR! SMITH'S OUT OF CLASS!\"",3.5f);break;
                case Effect.Flicker:
                    StartCoroutine(Blackout());
                    hud?.SetBark(who+" is fiddling with the fuse box...",3.5f);break;
                case Effect.WetFloor:
                    SpillAhead(player);
                    hud?.SetBark(who+" spilled a drink. Mind your step!",3.5f);break;
                case Effect.TeaBreak:
                    if(run.caretaker!=null)run.caretaker.TryStickInGlue(10);
                    hud?.SetBark(who+" put the kettle on. The caretaker's on a tea break!",3.5f);break;
                case Effect.SugarRush:
                    player.GetComponent<FirstPersonController>()?.SugarRush(8);
                    hud?.SetBark(who+" slipped you a fizzy drink. Sugar rush! Sprint all you like.",3.5f);break;
                case Effect.GlueFeet:
                    player.GetComponent<FirstPersonController>()?.GlueFeet(3);
                    hud?.SetBark(who+" glued your shoes to the floor!",3);break;
                case Effect.Teleport:
                    if(Teleport(player))hud?.SetBark(who+" teleported you. Where even is this?",3.5f);
                    else hud?.SetBark(who+" tried to teleport you. Nothing happened.",3);break;
                case Effect.Detention:
                    hud?.SetBark(who+" sent you to DETENTION!",3);
                    GameManager.Instance?.Caught();break;
            }
        }

        public Vector3? LastTeleport {get;private set;}
        /// <summary>A random spot the player could walk to, well away from the caretaker, the exit and staff-only rooms.</summary>
        bool Teleport(Transform player)
        {
            var run=SchoolRunController.Instance;var mesh=NavMesh.CalculateTriangulation();if(mesh.vertices.Length==0)return false;
            var exit=Object.FindObjectsByType<RunGate>(FindObjectsSortMode.None).FirstOrDefault(g=>g.kind==RunGate.Kind.Exit);
            var path=new NavMeshPath();
            for(int tries=0;tries<60;tries++)
            {
                int t=Random.Range(0,mesh.indices.Length/3)*3;
                Vector3 p=(mesh.vertices[mesh.indices[t]]+mesh.vertices[mesh.indices[t+1]]+mesh.vertices[mesh.indices[t+2]])/3;
                if(Mathf.Abs(p.y-player.position.y)>1||Vector3.Distance(p,player.position)<12)continue;
                if(run.caretaker!=null&&Vector3.Distance(p,run.caretaker.transform.position)<18)continue;
                if(exit!=null&&Vector3.Distance(p,exit.transform.position)<15)continue;
                if(run.Restricted(p)||!NavMesh.SamplePosition(p,out var hit,1,NavMesh.AllAreas))continue;
                // Walkable from where they stand, so never inside a room that's shut for the run.
                if(!NavMesh.CalculatePath(player.position,hit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                var body=player.GetComponent<CharacterController>();if(body!=null)body.enabled=false;
                player.position=hit.position;if(body!=null)body.enabled=true;Physics.SyncTransforms();
                LastTeleport=hit.position;return true;
            }
            return false;
        }

        public GameObject LastSpill {get;private set;}
        void SpillAhead(Transform player)
        {
            var template=Object.FindFirstObjectByType<WetFloorHazard>();if(template==null)return;
            Vector3 forward=player.forward;forward.y=0;forward=forward.sqrMagnitude>.01f?forward.normalized:Vector3.forward;
            Vector3 spot=player.position+forward*4.5f;
            if(!NavMesh.SamplePosition(spot,out var hit,2,NavMesh.AllAreas))return;
            var spill=Instantiate(template.gameObject,hit.position,Quaternion.LookRotation(forward));spill.name="Chat spill";
            // The hazard reads the player's position itself; the copied sign must not block a corridor.
            foreach(var c in spill.GetComponentsInChildren<Collider>())Destroy(c);
            Destroy(spill,40);LastSpill=spill;
        }

        List<(Light light,float intensity)> Fixtures()
        {
            fixtures??=Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)
                .Where(l=>l.transform.parent!=null&&l.transform.parent.name.StartsWith("P_CeilingLight")).Select(l=>(l,l.intensity)).ToList();
            return fixtures;
        }
        public bool BlackoutActive {get;private set;}
        IEnumerator Blackout()
        {
            if(BlackoutActive)yield break;BlackoutActive=true;
            // Snapshot now, not at load: other systems (library darkness, the lighting rework) may have retuned them since.
            var lights=Fixtures();for(int i=0;i<lights.Count;i++)if(lights[i].light!=null)lights[i]=(lights[i].light,lights[i].light.intensity);
            float end=Time.time+7;
            while(Time.time<end)
            {
                float left=end-Time.time;
                // Stutter, then a few seconds of near dark, then stutter back on.
                bool dark=left<5.5f&&left>1.5f;
                foreach(var f in lights)if(f.light!=null)f.light.intensity=dark?f.intensity*.04f:f.intensity*(Random.value<.5f?.1f:1f);
                yield return new WaitForSeconds(dark?.25f:.07f);
            }
            RestoreLights();
        }
        void RestoreLights()
        {
            if(!BlackoutActive||fixtures==null)return;
            foreach(var f in fixtures)if(f.light!=null)f.light.intensity=f.intensity;
            BlackoutActive=false;
        }

        // ------------------------------------------------------------------ panel

        void Draw(bool running)
        {
            bool show=running;
            if(panel==null){if(!show)return;Build();}
            if(panel.activeSelf!=show)panel.SetActive(show);
            if(!show)return;
            if(Voting)
            {
                heading.text="CHAT VOTE  "+Mathf.CeilToInt(Mathf.Max(0,VoteRemaining))+"s";
                int total=Mathf.Max(1,Tallies.Sum());var sb=new System.Text.StringBuilder();
                for(int i=0;i<Options.Length;i++)
                {
                    int bars=Mathf.RoundToInt(10f*Tallies[i]/total);
                    sb.Append("<b>"+(i+1)+"</b>  "+Label(Options[i])+(Helpful(Options[i])?"  <color=#3E7D4A>(help)</color>":"")+"\n");
                    sb.Append("    <color=#8F4BFF>"+new string('|',bars)+"</color><color=#B9B3A0>"+new string('|',10-bars)+"</color>  "+Tallies[i]+"\n");
                }
                sb.Append("<size=17>Type 1, 2 or 3 in chat</size>");
                body.text=sb.ToString();
            }
            else if(Time.time<resultUntil)
            {
                heading.text="CHAT HAS SPOKEN";
                body.text=LastWinner.HasValue?Label(LastWinner.Value)+"\n<size=17>"+(TwitchChat.ShowNames?"blame "+LastCredit:"blame chat")+"</size>":"Nobody voted.\n<size=17>Chat is asleep.</size>";
            }
            else
            {
                heading.text="TWITCH CHAT";
                body.text="<size=17>Next vote in "+Mathf.CeilToInt(Mathf.Max(0,NextVoteIn))+"s</size>";
            }
        }

        void Build()
        {
            var root=new GameObject("Twitch chaos canvas",typeof(Canvas),typeof(CanvasScaler));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=900;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            panel=new GameObject("Chat vote",typeof(RectTransform));panel.transform.SetParent(root.transform,false);
            var rect=(RectTransform)panel.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,.62f);rect.anchoredPosition=new Vector2(-24,0);rect.sizeDelta=new Vector2(390,250);
            var paper=panel.AddComponent<Image>();paper.color=new Color(Cream.r,Cream.g,Cream.b,.93f);paper.raycastTarget=false;
            var fit=panel.AddComponent<VerticalLayoutGroup>();fit.padding=new RectOffset(18,18,12,14);fit.spacing=4;fit.childControlHeight=fit.childControlWidth=true;fit.childForceExpandHeight=false;
            panel.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var edge=new GameObject("Pencil edge",typeof(RectTransform));edge.transform.SetParent(panel.transform,false);
            edge.AddComponent<LayoutElement>().ignoreLayout=true;var er=(RectTransform)edge.transform;er.anchorMin=Vector2.zero;er.anchorMax=Vector2.one;er.offsetMin=er.offsetMax=Vector2.zero;
            var border=edge.AddComponent<SketchBorder>();border.color=Ink;border.raycastTarget=false;
            var stripe=new GameObject("Twitch stripe",typeof(RectTransform));stripe.transform.SetParent(panel.transform,false);
            stripe.AddComponent<LayoutElement>().ignoreLayout=true;var sr=(RectTransform)stripe.transform;sr.anchorMin=Vector2.zero;sr.anchorMax=new Vector2(0,1);sr.sizeDelta=new Vector2(7,0);sr.anchoredPosition=new Vector2(3.5f,0);
            var si=stripe.AddComponent<Image>();si.color=Twitch;si.raycastTarget=false;
            heading=Line("Heading",26);body=Line("Options",21);
        }
        Text Line(string name,int size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(panel.transform,false);
            var t=go.AddComponent<Text>();t.font=SchoolTypography.Font;t.fontSize=size;t.color=Ink;t.supportRichText=true;t.raycastTarget=false;
            t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;return t;
        }
    }
}

#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR) || !STEAMWORKS_NET
#define DISABLESTEAMWORKS
#endif
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace Confiscated
{
    /// <summary>
    /// Casual Steam leaderboard: a finished escape uploads the signed-in player's time (Steam keeps their best), then the top ten and
    /// their own rank are read back. Lives across scene reloads so a restart never loses an upload. Steam being absent or failing only
    /// changes the status line; it never blocks a run. A modified client can cheat this, so never describe the times as verified.
    /// </summary>
    public sealed class SteamLeaderboard : MonoBehaviour
    {
        public struct Entry { public int rank; public string name; public long ms; public bool you; }
        /// <summary>Automated tests warp the player through a run; they set this so those times never reach Steam.</summary>
        public static bool Suppress;
        public static event Action Changed;
        public static string Status {get;private set;}="";
        public static string Category {get;private set;}="";
        public static readonly List<Entry> Top=new();
        public static Entry? You {get;private set;}
        static SteamLeaderboard instance;
        // Every Play starts unsuppressed, even with domain reload off; a test sets the flag again after this runs.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){Suppress=false;Status="";Category="";Top.Clear();You=null;}

        static string BoardId(string category)=>category=="Phone start"?"phone":category=="Lesson start"?"lesson":category=="Classroom start"?"classroom":null;
        static void Set(string status){Status=status;Changed?.Invoke();}

        /// <summary>Call once, after a valid escape has frozen its time.</summary>
        public static void Submit(string category,long ms)
        {
            Category=category??"";Top.Clear();You=null;
            string board=BoardId(category);
            if(Suppress){Set("");return;}
            if(Category.StartsWith("Dark / ")){Set("Dark mode personal best saved locally.");return;}
            if(board==null||ms<=0||ms>int.MaxValue){Set("This run can't be ranked.");return;}
#if DISABLESTEAMWORKS
            Set("");
#else
            if(instance==null){var g=new GameObject("Steam leaderboard");DontDestroyOnLoad(g);instance=g.AddComponent<SteamLeaderboard>();}
            instance.Begin(board,(int)ms);
#endif
        }

#if !DISABLESTEAMWORKS
        bool ready,busy;
        string queuedBoard;int queuedMs;
        SteamLeaderboard_t handle;
        CallResult<LeaderboardFindResult_t> found;
        CallResult<LeaderboardScoreUploaded_t> uploaded;
        CallResult<LeaderboardScoresDownloaded_t> topLoaded,youLoaded;
        Callback<PersonaStateChange_t> personaChanged;
        readonly List<(CSteamID id,int rank,int score)> rows=new();
        (CSteamID id,int rank,int score)? mine;

        bool Init()
        {
            if(ready)return true;
            try
            {
                if(!Packsize.Test()||!DllCheck.Test()||!SteamAPI.Init())return false;
            }
            catch(Exception e){Debug.LogWarning("Steam unavailable: "+e.Message);return false;}
            found=CallResult<LeaderboardFindResult_t>.Create(OnFound);
            uploaded=CallResult<LeaderboardScoreUploaded_t>.Create(OnUploaded);
            topLoaded=CallResult<LeaderboardScoresDownloaded_t>.Create(OnTop);
            youLoaded=CallResult<LeaderboardScoresDownloaded_t>.Create(OnYou);
            // Names of strangers arrive late; redraw when Steam has them.
            personaChanged=Callback<PersonaStateChange_t>.Create(_=>{if(!busy&&rows.Count>0)Publish();});
            return ready=true;
        }
        void Begin(string board,int ms)
        {
            if(!Init()){Set("Steam isn't running. Time saved on this PC only.");return;}
            // One upload at a time; a newer finish waits, and only the latest waiting one is kept.
            if(busy){queuedBoard=board;queuedMs=ms;return;}
            busy=true;pendingMs=ms;rows.Clear();mine=null;
            Set("Sending your time to Steam...");
            // The editor, development builds and Valve's public test app (480) use separate boards, so testing never reaches the released game's tables.
            bool testing=Application.isEditor||Debug.isDebugBuild||SteamUtils.GetAppID().m_AppId==480;
            found.Set(SteamUserStats.FindOrCreateLeaderboard((testing?"test_escape_":"escape_")+board+"_v1",ELeaderboardSortMethod.k_ELeaderboardSortMethodAscending,ELeaderboardDisplayType.k_ELeaderboardDisplayTypeTimeMilliSeconds));
        }
        int pendingMs;
        void Fail(string why){busy=false;Set(why+" Time saved on this PC only.");Drain();}
        void Drain(){if(queuedBoard==null)return;string b=queuedBoard;queuedBoard=null;Begin(b,queuedMs);}
        void OnFound(LeaderboardFindResult_t r,bool ioFailure)
        {
            if(ioFailure||r.m_bLeaderboardFound==0){Fail("Couldn't reach the Steam leaderboard.");return;}
            handle=r.m_hSteamLeaderboard;
            uploaded.Set(SteamUserStats.UploadLeaderboardScore(handle,ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,pendingMs,null,0));
        }
        void OnUploaded(LeaderboardScoreUploaded_t r,bool ioFailure)
        {
            if(ioFailure||r.m_bSuccess==0){Fail("Steam didn't accept the time.");return;}
            Set(r.m_bScoreChanged!=0?"New Steam best! Loading the table...":"Loading the table...");
            topLoaded.Set(SteamUserStats.DownloadLeaderboardEntries(handle,ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal,1,10));
        }
        void Read(LeaderboardScoresDownloaded_t r,List<(CSteamID,int,int)> into)
        {
            for(int i=0;i<r.m_cEntryCount;i++)
                if(SteamUserStats.GetDownloadedLeaderboardEntry(r.m_hSteamLeaderboardEntries,i,out var e,null,0))
                {into.Add((e.m_steamIDUser,e.m_nGlobalRank,e.m_nScore));SteamFriends.RequestUserInformation(e.m_steamIDUser,true);}
        }
        void OnTop(LeaderboardScoresDownloaded_t r,bool ioFailure)
        {
            if(ioFailure){Fail("Your time was sent, but the table didn't load.");return;}
            rows.Clear();Read(r,rows);
            youLoaded.Set(SteamUserStats.DownloadLeaderboardEntries(handle,ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobalAroundUser,0,0));
        }
        void OnYou(LeaderboardScoresDownloaded_t r,bool ioFailure)
        {
            var own=new List<(CSteamID,int,int)>();if(!ioFailure)Read(r,own);
            if(own.Count>0)mine=own[0];else mine=null;
            busy=false;Publish();Drain();
        }
        static string Name(CSteamID id)
        {
            string n=SteamFriends.GetFriendPersonaName(id);
            if(string.IsNullOrWhiteSpace(n)||n=="[unknown]")n="...";
            n=n.Replace('\n',' ').Replace('\r',' ');
            return n.Length>16?n.Substring(0,15)+"…":n;
        }
        void Publish()
        {
            var self=SteamUser.GetSteamID();
            Top.Clear();
            foreach(var row in rows)Top.Add(new Entry{rank=row.rank,name=Name(row.id),ms=row.score,you=row.id==self});
            You=mine.HasValue?new Entry{rank=mine.Value.rank,name=Name(mine.Value.id),ms=mine.Value.score,you=true}:(Entry?)null;
            Set(You.HasValue?"YOUR BEST  "+RunTiming.Format(You.Value.ms)+"   RANK #"+You.Value.rank:"");
        }
        // Unscaled: Steam answers must arrive while the results screen has the game paused.
        void Update(){if(ready)SteamAPI.RunCallbacks();}
        void OnDestroy(){if(instance!=this)return;instance=null;if(ready){ready=false;SteamAPI.Shutdown();}}
#endif
    }

    /// <summary>The table drawn under the escape results. It only reads <see cref="SteamLeaderboard"/>, so a restart can destroy it mid-upload.</summary>
    public sealed class SteamLeaderboardView : MonoBehaviour
    {
        Text header,left,right;
        public string HeaderText=>header!=null?header.text:"";
        public static SteamLeaderboardView Show(Transform overlay,Text style)
        {
            if(overlay==null||style==null)return null;
            var old=overlay.Find("Steam leaderboard");if(old!=null)Destroy(old.gameObject);
            var g=new GameObject("Steam leaderboard",typeof(RectTransform));g.transform.SetParent(overlay,false);
            var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,0);r.anchoredPosition=new Vector2(0,18);r.sizeDelta=new Vector2(1100,250);
            var view=g.AddComponent<SteamLeaderboardView>();
            view.header=view.Make(style,"Header",26,TextAnchor.UpperCenter,new Vector2(0,0),new Vector2(1100,36),new Color(.93f,.8f,.45f));
            view.left=view.Make(style,"Ranks 1-5",24,TextAnchor.UpperLeft,new Vector2(-270,-42),new Vector2(500,200),Color.white);
            view.right=view.Make(style,"Ranks 6-10",24,TextAnchor.UpperLeft,new Vector2(290,-42),new Vector2(500,200),Color.white);
            view.Redraw();
            return view;
        }
        Text Make(Text style,string name,int size,TextAnchor anchor,Vector2 position,Vector2 box,Color colour)
        {
            var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(transform,false);
            var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,1);r.anchoredPosition=position;r.sizeDelta=box;
            var t=g.AddComponent<Text>();t.font=style.font;t.fontSize=size;t.alignment=anchor;t.color=colour;t.raycastTarget=false;
            // Steam names are other people's text: never let them inject rich-text tags.
            t.supportRichText=false;t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Overflow;
            return t;
        }
        void OnEnable(){SteamLeaderboard.Changed+=Redraw;}
        void OnDisable(){SteamLeaderboard.Changed-=Redraw;}
        void Redraw()
        {
            if(header==null)return;
            var top=SteamLeaderboard.Top;
            string status=SteamLeaderboard.Status;
            header.text=top.Count>0?"STEAM TOP TEN  |  "+SteamLeaderboard.Category+(status.Length>0?"  |  "+status:""):status;
            var a=new System.Text.StringBuilder();var b=new System.Text.StringBuilder();
            for(int i=0;i<top.Count&&i<10;i++)
                (i<5?a:b).Append(top[i].you?"> ":"   ").Append(top[i].rank).Append(".  ").Append(top[i].name).Append("   ").Append(RunTiming.Format(top[i].ms)).Append('\n');
            left.text=a.ToString();right.text=b.ToString();
        }
    }
}

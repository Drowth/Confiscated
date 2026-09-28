using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace Confiscated
{
    /// <summary>
    /// Read-only Twitch chat for a streamer's channel. Joins as an anonymous "justinfan" guest over IRC/TLS, so there is no
    /// login, no OAuth and no developer account, and the game can never post in the chat. Lives across scene reloads.
    /// Messages are read on a worker thread and handed to the main thread through <see cref="Received"/>.
    /// </summary>
    public sealed class TwitchChat : MonoBehaviour
    {
        public const string ChannelKey="Confiscated.Twitch.Channel",EnabledKey="Confiscated.Twitch.Enabled",
            HelpKey="Confiscated.Twitch.ChatCanHelp",NamesKey="Confiscated.Twitch.ShowNames";
        const string Host="irc.chat.twitch.tv";const int Port=6697;
        public enum Status { Off, Connecting, Connected, Failed }

        public static TwitchChat Instance { get; private set; }
        /// <summary>(display name, message text), always raised on the main thread.</summary>
        public static event Action<string,string> Received;
        public static string SavedChannel=>PlayerPrefs.GetString(ChannelKey,"");
        public static bool ChatCanHelp{get=>PlayerPrefs.GetInt(HelpKey,1)==1;set{PlayerPrefs.SetInt(HelpKey,value?1:0);PlayerPrefs.Save();}}
        public static bool ShowNames{get=>PlayerPrefs.GetInt(NamesKey,1)==1;set{PlayerPrefs.SetInt(NamesKey,value?1:0);PlayerPrefs.Save();}}
        /// <summary>Connected to a real channel, or fed by a test.</summary>
        public static bool Live=>Instance!=null&&(Instance.State==Status.Connected||Instance.testFeed);

        public Status State=>(Status)status;
        public string Channel {get;private set;}
        public string LastError=>lastError;
        public int MessagesReceived {get;private set;}
        /// <summary>Most recent distinct chatters, newest last.</summary>
        public IReadOnlyList<string> RecentChatters=>recent;
        /// <summary>Distinct people who've typed since the game started (the menu shows it).</summary>
        public int ChatterCount=>everyone.Count;

        readonly ConcurrentQueue<(string user,string text)> inbox=new();
        readonly List<string> recent=new();
        readonly HashSet<string> everyone=new(System.StringComparer.OrdinalIgnoreCase);
        volatile int status;volatile string lastError="";
        bool testFeed;Link link;
        // One per connection, so a worker left over from a quick disconnect/reconnect can only ever stop itself.
        sealed class Link{public volatile bool stop;public volatile TcpClient client;}

        public static TwitchChat Ensure()
        {
            if(Instance!=null)return Instance;
            var go=new GameObject("Twitch chat");DontDestroyOnLoad(go);
            return Instance=go.AddComponent<TwitchChat>();
        }

        // The streamer connects once from the title screen; later launches reconnect by themselves.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoConnect(){if(PlayerPrefs.GetInt(EnabledKey,0)==1&&SavedChannel.Length>0)Ensure().Connect(SavedChannel);}

        /// <summary>Accepts "name", "#name" or a twitch.tv link. Returns null when it isn't a possible channel name.</summary>
        public static string Normalise(string input)
        {
            if(string.IsNullOrWhiteSpace(input))return null;
            string s=input.Trim().ToLowerInvariant();
            int slash=s.LastIndexOf('/');if(slash>=0)s=s.Substring(slash+1);
            s=s.TrimStart('#','@');
            return Regex.IsMatch(s,"^[a-z0-9_]{3,25}$")?s:null;
        }

        public bool Connect(string input)
        {
            string channel=Normalise(input);
            if(channel==null){lastError="That isn't a Twitch channel name.";status=(int)Status.Failed;return false;}
            StopWorker();
            Channel=channel;PlayerPrefs.SetString(ChannelKey,channel);PlayerPrefs.SetInt(EnabledKey,1);PlayerPrefs.Save();
            lastError="";status=(int)Status.Connecting;
            var current=link=new Link();
            new Thread(()=>Run(channel,current)){IsBackground=true,Name="Twitch chat"}.Start();
            return true;
        }

        public void Disconnect()
        {
            StopWorker();status=(int)Status.Off;
            PlayerPrefs.SetInt(EnabledKey,0);PlayerPrefs.Save();
        }

        /// <summary>Test hook: pretend a viewer typed this. Marks chat as live without touching the network.</summary>
        public static void InjectForTest(string user,string text){var chat=Ensure();chat.testFeed=true;chat.inbox.Enqueue((user,text));}
        public static void EndTestFeed(){if(Instance!=null){Instance.testFeed=false;while(Instance.inbox.TryDequeue(out _)){}}}

        const int MaxPerFrame=250;
        void Update()
        {
            // A big chat can burst hundreds of lines at once; spread them over frames rather than hitching one.
            for(int handled=0;handled<MaxPerFrame&&inbox.TryDequeue(out var m);handled++)
            {
                MessagesReceived++;
                everyone.Add(m.user);recent.Remove(m.user);recent.Add(m.user);if(recent.Count>50)recent.RemoveAt(0);
                try{Received?.Invoke(m.user,m.text);}catch(Exception e){Debug.LogException(e);}
            }
        }

        void OnDestroy(){StopWorker();if(Instance==this)Instance=null;}
        void OnApplicationQuit()=>StopWorker();

        void StopWorker()
        {
            var old=link;link=null;if(old==null)return;
            old.stop=true;
            try{old.client?.Close();}catch{}
        }

        void Run(string channel,Link me)
        {
            float backoff=2;
            while(!me.stop)
            {
                try{Session(channel,me);backoff=2;}
                catch(Exception e)when(!me.stop){lastError=e is IOException||e is SocketException?"Lost connection to Twitch. Retrying...":e.Message;}
                catch{}
                if(me.stop)break;
                status=(int)Status.Failed;
                for(float waited=0;waited<backoff&&!me.stop;waited+=.25f)Thread.Sleep(250);
                if(!me.stop)status=(int)Status.Connecting;
                backoff=Mathf.Min(backoff*2,30);
            }
        }

        void Session(string channel,Link me)
        {
            var tcp=new TcpClient();me.client=tcp;
            if(me.stop){tcp.Close();return;}
            tcp.Connect(Host,Port);
            // Twitch pings about every five minutes; a read that waits longer than this means the link is dead.
            tcp.ReceiveTimeout=7*60*1000;
            using var ssl=new SslStream(tcp.GetStream(),false);
            ssl.AuthenticateAsClient(Host);
            using var reader=new StreamReader(ssl,new UTF8Encoding(false));
            using var writer=new StreamWriter(ssl,new UTF8Encoding(false)){NewLine="\r\n",AutoFlush=true};
            // Anonymous guest login: any PASS is accepted for a justinfan nick, which can read but never send.
            writer.WriteLine("CAP REQ :twitch.tv/tags");
            writer.WriteLine("PASS SCHMOOPIIE");
            writer.WriteLine("NICK justinfan"+new System.Random().Next(10000,99999));
            writer.WriteLine("JOIN #"+channel);
            string line;
            while(!me.stop&&(line=reader.ReadLine())!=null)
            {
                if(line.StartsWith("PING")){writer.WriteLine("PONG"+line.Substring(4));continue;}
                if(line.Contains(" RECONNECT"))return;
                if(line.Contains(" 366 ")||line.Contains(" ROOMSTATE #"+channel)){status=(int)Status.Connected;lastError="";continue;}
                if(line.Contains(" NOTICE ")&&line.Contains("unsuccessful")){lastError="Twitch refused the guest login.";return;}
                if(!me.stop&&TryParse(line,out var user,out var text))inbox.Enqueue((user,text));
            }
            if(!me.stop)throw new IOException("closed");
        }

        /// <summary>Parses "@tags :nick!nick@host PRIVMSG #chan :text". Prefers the display-name tag for the name shown.</summary>
        public static bool TryParse(string line,out string user,out string text)
        {
            user=text=null;
            string tags="";
            if(line.StartsWith("@")){int sp=line.IndexOf(' ');if(sp<0)return false;tags=line.Substring(1,sp-1);line=line.Substring(sp+1);}
            int cmd=line.IndexOf(" PRIVMSG #");if(!line.StartsWith(":")||cmd<0)return false;
            int colon=line.IndexOf(" :",cmd+10);if(colon<0)return false;
            int bang=line.IndexOf('!');
            string nick=line.Substring(1,(bang>0&&bang<cmd?bang:cmd)-1);
            string display=null;
            foreach(var tag in tags.Split(';'))if(tag.StartsWith("display-name=")){display=tag.Substring(13);break;}
            user=CleanName(string.IsNullOrEmpty(display)?nick:display);
            text=line.Substring(colon+2);
            if(text.StartsWith("\u0001ACTION ")){text=text.Substring(8).TrimEnd('\u0001');}
            return user.Length>0;
        }

        /// <summary>Names end up in UI text with rich text on, so strip anything that could be read as markup.</summary>
        public static string CleanName(string name)
        {
            var sb=new StringBuilder();
            foreach(char c in name??"")if(c!='<'&&c!='>'&&!char.IsControl(c))sb.Append(c);
            string s=sb.ToString().Trim();
            return s.Length>25?s.Substring(0,25):s;
        }
    }
}

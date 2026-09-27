using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Twitch chat votes: title setup panel, parser, vote counting, every effect, settings, restart, and a real guest JOIN.</summary>
    [InitializeOnLoad]
    public static class TwitchChaosSmokeTest
    {
        const string Marker="Temp/twitch_chaos_test",Dir="../Docs/Twitch",Report=Dir+"/Validation.txt";
        static int step,errors;static double at,started;static bool pass,background;
        static readonly List<(Vector3 pos,float radius,string source)> heard=new();
        static string savedChannel;static int savedEnabled,savedHelp,savedNames;
        static List<float> before;static List<(Light l,float i)> lit;static Color ambientBefore;static List<(MeshRenderer r,Material m)> panels;static int wetBefore,yawTries;
        static SchoolRunController R=>SchoolRunController.Instance;
        static TwitchChaos T=>TwitchChaos.Instance;
        static Transform Player=>R.period.Player.transform;
        static TwitchChaosSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Start();}};}
        [MenuItem("Confiscated/Play Test/Arm Twitch Chat Test (runs on next Play)")]
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Start()
        {
            Directory.CreateDirectory(Dir);
            step=errors=0;pass=true;heard.Clear();at=started=EditorApplication.timeSinceStartup;
            savedChannel=PlayerPrefs.GetString(TwitchChat.ChannelKey,"");savedEnabled=PlayerPrefs.GetInt(TwitchChat.EnabledKey,0);
            savedHelp=PlayerPrefs.GetInt(TwitchChat.HelpKey,1);savedNames=PlayerPrefs.GetInt(TwitchChat.NamesKey,1);
            TwitchChat.ChatCanHelp=true;TwitchChat.ShowNames=true;
            background=Application.runInBackground;Application.runInBackground=true;
            File.WriteAllText(Report,"Twitch chat votes: injected chat for the game side, one real anonymous JOIN for the network side ("+DateTime.Now.ToString("s")+")\n");
            Application.logMessageReceived+=Log;NoiseEvents.OnNoise+=Hear;EditorApplication.update+=Tick;
        }
        static void Hear(Vector3 p,float r,string s)=>heard.Add((p,r,s));
        static void Log(string m,string trace,LogType t){if(t==LogType.Error||t==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+m+"\n");}}
        static void Check(bool ok,string message){pass&=ok;File.AppendAllText(Report,(ok?"ok   ":"FAIL ")+message+"\n");}
        static void Next(int n){step=n;at=EditorApplication.timeSinceStartup;}
        static string Bark=>HudController.Instance.statusText!=null?HudController.Instance.statusText.text:"";
        static void Vote(params (string user,string text)[] lines){foreach(var l in lines)TwitchChat.InjectForTest(l.user,l.text);}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish();return;}
            double elapsed=EditorApplication.timeSinceStartup-at;
            try
            {
                if(EditorApplication.timeSinceStartup-started>120)throw new Exception("Twitch test timeout at step "+step);
                switch(step)
                {
                    case 0:
                        if(elapsed<1)return;
                        Check(SchoolTitleMenu.IsActive,"title screen is up");
                        SchoolTitleMenu.Instance.ShowTwitch(true);Next(1);break;
                    case 1:
                        if(elapsed<.4)return;
                        ScreenCapture.CaptureScreenshot(Dir+"/TitlePanel.png");Next(2);break;
                    case 2:
                        if(elapsed<.4)return;
                        Check(GameObject.Find("Twitch setup")!=null&&GameObject.Find("Twitch setup").activeInHierarchy,"title has a Twitch setup panel");
                        // Parser and channel-name handling.
                        Check(TwitchChat.TryParse("@badge-info=;display-name=Dave_42;color=#FF0000 :dave_42!dave_42@dave_42.tmi.twitch.tv PRIVMSG #somechan :2",out var u,out var m)&&u=="Dave_42"&&m=="2","tagged PRIVMSG parses display name and text");
                        Check(TwitchChat.TryParse(":bob!bob@bob.tmi.twitch.tv PRIVMSG #somechan :hello :) there",out u,out m)&&u=="bob"&&m=="hello :) there","untagged PRIVMSG keeps colons in the text");
                        Check(!TwitchChat.TryParse(":tmi.twitch.tv 366 justinfan1 #somechan :End of /NAMES list",out _,out _)&&!TwitchChat.TryParse("PING :tmi.twitch.tv",out _,out _),"server lines are not chat");
                        Check(TwitchChat.CleanName("<b>evil</b>")=="bevil/b","names are stripped of markup");
                        Check(TwitchChat.Normalise(" #SomeStreamer ")=="somestreamer"&&TwitchChat.Normalise("https://www.twitch.tv/Some_Streamer")=="some_streamer"&&TwitchChat.Normalise("no spaces allowed")==null,"channel box accepts names, #names and links");
                        SchoolTitleMenu.Instance.ShowTwitch(false);SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();
                        Next(3);break;
                    case 3:
                        if(elapsed<1)return;
                        ComicDialogue.Cancel();R.period.PrepareChaseRetry();R.PrepareChaseRetry();R.secondStaff?.Freeze();
                        var shade=Object.FindFirstObjectByType<LibraryShadow>();if(shade!=null)shade.Paused=true;
                        Check(T!=null&&T.gameObject==R.gameObject,"vote system attached itself to the school run");
                        Next(4);break;
                    case 4:
                        if(elapsed<.5)return;
                        ComicDialogue.Cancel();
                        Check(R.RoundStarted&&GameManager.Instance.IsPlaying,"chase round is live");
                        Check(T.Panel==null||!T.Panel.activeSelf,"no chat panel while no channel is connected");
                        float wait=T.NextVoteIn;
                        TwitchChat.InjectForTest("viewer","hi");Next(5);
                        before=new List<float>{wait};break;
                    case 5:
                        if(elapsed<1)return;
                        Check(TwitchChat.Live&&T.Panel!=null&&T.Panel.activeSelf,"panel shows once chat is live");
                        Check(T.NextVoteIn<before[0]-.5f,"countdown to the next vote runs during the chase");
                        T.OpenVote();
                        Check(T.Voting&&T.Options.Length==3&&T.Options.Distinct().Count()==3,"a vote offers three different options");
                        Check(T.Options.Count(TwitchChaos.Helpful)<=1,"at most one helpful option per vote");
                        Check(!T.Options.Contains(TwitchChaos.Effect.Detention),"detention is never offered in the first vote");
                        Vote(("alice","1"),("bob","2"),("carol","2"),("bob","1"),("dave","hello 2"),("eve","!3"),("frank","9"));
                        Next(6);break;
                    case 6:
                        if(elapsed<.4)return;
                        Check(T.Tallies.SequenceEqual(new[]{2,1,1}),"one vote each, latest counts, !3 accepted, chatter and 9 ignored ("+string.Join(",",T.Tallies)+")");
                        ScreenCapture.CaptureScreenshot(Dir+"/Vote.png");Next(7);break;
                    case 7:
                        if(elapsed<.4)return;
                        var top=T.Options[0];T.CloseVote();
                        Check(T.LastWinner==top&&(T.LastCredit=="alice"||T.LastCredit=="bob"),"winner fires and is credited to someone who voted for it ("+T.LastCredit+")");
                        Next(8);break;
                    case 8:
                        if(elapsed<.3)return;
                        ScreenCapture.CaptureScreenshot(Dir+"/Result.png");
                        T.OpenVote();T.CloseVote();
                        Check(T.LastWinner==null,"no votes, no effect");
                        // Each effect on its own.
                        heard.Clear();T.Apply(TwitchChaos.Effect.Noise,"tester");
                        Check(heard.Any(h=>h.source=="twitch chat"&&Vector3.Distance(h.pos,Player.position)<7),"noise lands a few metres from the player");
                        Check(Bark.Contains("tester"),"effects credit the viewer in the HUD bark");
                        heard.Clear();T.Apply(TwitchChaos.Effect.TellTale,"tester");
                        Check(heard.Any(h=>h.source=="tell-tale"&&Vector3.Distance(h.pos,Player.position)<1),"tell-tale is heard at the player");
                        Check(!ComicDialogue.IsActive,"chat barks never open the modal dialogue");
                        R.caretaker.ResumeAfterDetention(0);
                        T.Apply(TwitchChaos.Effect.TeaBreak,"tester");
                        Check(R.caretaker.IsGlued,"tea break stops the caretaker");
                        var fp=Player.GetComponent<FirstPersonController>();
                        T.Apply(TwitchChaos.Effect.SugarRush,"tester");
                        Check(Mathf.Approximately(fp.SprintFraction,1)&&fp.SugarRushing,"sugar rush fills stamina and keeps it full");
                        T.Apply(TwitchChaos.Effect.GlueFeet,"tester");
                        Check(fp.FeetGlued&&!fp.MovementLocked,"glued shoes root the player without touching the shared movement lock");
                        Vector3 from=Player.position;T.Apply(TwitchChaos.Effect.Teleport,"tester");
                        Check(T.LastTeleport.HasValue&&Vector3.Distance(from,Player.position)>=12&&!R.Restricted(Player.position),"teleport moves the player at least 12 m, not into a staff room ("+Vector3.Distance(from,Player.position).ToString("0")+" m)");
                        Check(Vector3.Distance(Player.position,R.caretaker.transform.position)>=18,"teleport lands well away from the caretaker");
                        wetBefore=Object.FindObjectsByType<WetFloorHazard>(FindObjectsSortMode.None).Length;yawTries=0;
                        Next(9);break;
                    case 9:
                        if(T.LastSpill==null&&yawTries<8)
                        {
                            Player.rotation=Quaternion.Euler(0,45*yawTries++,0);T.Apply(TwitchChaos.Effect.WetFloor,"tester");return;
                        }
                        if(elapsed<.2)return;
                        Check(T.LastSpill!=null&&Object.FindObjectsByType<WetFloorHazard>(FindObjectsSortMode.None).Length==wetBefore+1,"spill puts a new wet floor near the player");
                        Check(T.LastSpill!=null&&T.LastSpill.GetComponentsInChildren<Collider>().Length==0,"spilled sign cannot block a corridor");
                        lit=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.transform.parent!=null&&l.transform.parent.name.StartsWith("P_CeilingLight")).Select(l=>(l,l.intensity)).ToList();
                        Check(lit.Count>10,"found "+lit.Count+" ceiling fixtures for lights out");
                        ambientBefore=RenderSettings.ambientLight;
                        panels=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="Universal Render Pipeline/Unlit"&&!r.transform.IsChildOf(Player)&&r.GetComponentInParent<Canvas>()==null&&r.GetComponentInParent<LibraryShadow>()==null).Take(20).Select(r=>(r,r.sharedMaterial)).ToList();
                        // The outage is long; keep staff from catching the test player while it waits.
                        R.caretaker.Freeze();R.PauseStaff();
                        T.Apply(TwitchChaos.Effect.Flicker,"tester");Next(10);break;
                    case 10:
                        if(elapsed<3)return;
                        Check(T.BlackoutActive&&lit.All(f=>!f.l.enabled||f.l.intensity<=.001f),"lights out switches every ceiling fixture off");
                        Check(T.Outage.Lights.All(e=>e.light==null||!e.light.enabled),"every non-player light in the school is off, as in Dark Mode");
                        Check(RenderSettings.ambientLight.maxColorComponent<.03f&&Mathf.Approximately(Shader.GetGlobalFloat("_SchoolDarkness"),1),"ambient is Dark Mode black and characters go dark (glowing eyes)");
                        var glowing=panels.Where(p=>p.r.sharedMaterial.shader.name=="Universal Render Pipeline/Unlit").Select(p=>p.r.name).ToList();
                        Check(panels.Count>0&&glowing.Count==0,"flat unlit art stops glowing in the dark ("+panels.Count+" sampled"+(glowing.Count>0?", still unlit: "+string.Join(", ",glowing):"")+")");
                        ScreenCapture.CaptureScreenshot(Dir+"/LightsOut.png");Next(11);break;
                    case 11:
                        if(T.BlackoutActive&&elapsed<25)return;
                        Check(!T.BlackoutActive&&lit.All(f=>Mathf.Approximately(f.l.intensity,f.i)),"lights come back to exactly their old brightness");
                        Check(RenderSettings.ambientLight==ambientBefore&&Shader.GetGlobalFloat("_SchoolDarkness")==0,"ambient and character shading restored");
                        Check(panels.All(p=>p.r.sharedMaterial==p.m),"original art materials put back");
                        // Settings.
                        TwitchChat.ChatCanHelp=false;bool anyHelp=false;
                        for(int i=0;i<30;i++){T.OpenVote();anyHelp|=T.Options.Any(TwitchChaos.Helpful);T.CloseVote();}
                        Check(!anyHelp,"'chat can help' off keeps help out of every vote");
                        TwitchChat.ChatCanHelp=true;TwitchChat.ShowNames=false;
                        T.OpenVote();Vote(("secretname","1"));Next(12);break;
                    case 12:
                        if(elapsed<.3)return;
                        T.CloseVote();
                        Check(!Bark.Contains("secretname")&&Bark.StartsWith("Chat"),"viewer names off hides the name: \""+Bark+"\"");
                        TwitchChat.ShowNames=true;
                        T.Apply(TwitchChaos.Effect.Detention,"tester");Next(20);break;
                    case 20:
                        if(elapsed<1.5)return;
                        Check(GameManager.Instance.Current==GameManager.State.Detention,"detention vote sends the player to detention");
                        ScreenCapture.CaptureScreenshot(Dir+"/Detention.png");Next(21);break;
                    case 21:
                        if(elapsed<.4)return;
                        Check(!T.Panel.activeSelf,"votes pause during detention");
                        GameManager.Instance.RestartRun();Next(13);break;
                    case 13:
                        if(elapsed<2)return;
                        Check(TwitchChat.Instance!=null&&T!=null&&SchoolRunController.Instance!=null&&T.gameObject==SchoolRunController.Instance.gameObject,"chat and votes survive a restart");
                        Check(!T.Voting,"a restart starts with no vote open");
                        TwitchChat.EndTestFeed();
                        // Network: a real anonymous guest JOIN. Needs no live stream, only that Twitch answers.
                        TwitchChat.Instance.Connect("twitch");Next(14);break;
                    case 14:
                        var state=TwitchChat.Instance.State;
                        if(state!=TwitchChat.Status.Connected&&elapsed<20)return;
                        Check(state==TwitchChat.Status.Connected,"real guest login joined #twitch over TLS ("+state+" "+TwitchChat.Instance.LastError+")");
                        TwitchChat.Instance.Disconnect();
                        Check(TwitchChat.Instance.State==TwitchChat.Status.Off,"disconnect goes quiet");
                        Finish();break;
                }
            }
            catch(Exception e){Check(false,e.ToString());Finish();}
        }
        static void Finish()
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;NoiseEvents.OnNoise-=Hear;
            TwitchChat.EndTestFeed();if(TwitchChat.Instance!=null&&TwitchChat.Instance.State!=TwitchChat.Status.Off)TwitchChat.Instance.Disconnect();
            PlayerPrefs.SetString(TwitchChat.ChannelKey,savedChannel);PlayerPrefs.SetInt(TwitchChat.EnabledKey,savedEnabled);
            PlayerPrefs.SetInt(TwitchChat.HelpKey,savedHelp);PlayerPrefs.SetInt(TwitchChat.NamesKey,savedNames);PlayerPrefs.Save();
            Application.runInBackground=background;
            Check(errors==0,"no runtime errors");
            File.AppendAllText(Report,pass?"PASS\n":"FAIL\n");
            EditorApplication.isPlaying=false;
        }
    }
}

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Confiscated.EditorTools
{
    /// <summary>Title screen Twitch button + caption, photographed connected (the saved channel) and offline. Restores the streamer's settings.</summary>
    [InitializeOnLoad]
    public static class TitleTwitchBadgeCapture
    {
        const string Marker="Temp/title_twitch_badge",Dir="../Docs/Twitch",Report=Dir+"/TitleBadge.txt";
        static int step;static double at,started;static string channel;static int enabled;
        static TitleTwitchBadgeCapture(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Start();}};}
        [MenuItem("Confiscated/Play Test/Arm Title Twitch Badge Capture (runs on next Play)")]
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Start()
        {
            step=0;at=started=EditorApplication.timeSinceStartup;Directory.CreateDirectory(Dir);
            channel=PlayerPrefs.GetString(TwitchChat.ChannelKey,"");enabled=PlayerPrefs.GetInt(TwitchChat.EnabledKey,0);
            File.WriteAllText(Report,"Title Twitch badge ("+DateTime.Now.ToString("s")+") saved channel '"+channel+"' enabled "+enabled+"\n");
            EditorApplication.update+=Tick;
        }
        static void Log(string s)=>File.AppendAllText(Report,s+"\n");
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Done();return;}
            double elapsed=EditorApplication.timeSinceStartup-at;
            if(EditorApplication.timeSinceStartup-started>40){Log("timeout");Done();return;}
            var chat=TwitchChat.Instance;
            switch(step)
            {
                case 0:
                    if(elapsed<1)return;
                    if(chat==null||chat.State==TwitchChat.Status.Off)TwitchChat.Ensure().Connect(string.IsNullOrEmpty(channel)?"twitch":channel);
                    step=1;at=EditorApplication.timeSinceStartup;break;
                case 1:
                    if(TwitchChat.Instance.State!=TwitchChat.Status.Connected&&elapsed<15)return;
                    if(elapsed<3)return; // let a few messages / the caption settle
                    Log("state "+TwitchChat.Instance.State+" chatters "+TwitchChat.Instance.ChatterCount+" messages "+TwitchChat.Instance.MessagesReceived);
                    ScreenCapture.CaptureScreenshot(Dir+"/Title_Connected.png");step=2;at=EditorApplication.timeSinceStartup;break;
                case 2:
                    if(elapsed<.5)return;
                    TwitchChat.Instance.Disconnect();step=3;at=EditorApplication.timeSinceStartup;break;
                case 3:
                    if(elapsed<.5)return;
                    ScreenCapture.CaptureScreenshot(Dir+"/Title_Offline.png");step=4;at=EditorApplication.timeSinceStartup;break;
                case 4:
                    if(elapsed<.5)return;
                    Done();break;
            }
        }
        static void Done()
        {
            EditorApplication.update-=Tick;
            PlayerPrefs.SetString(TwitchChat.ChannelKey,channel);PlayerPrefs.SetInt(TwitchChat.EnabledKey,enabled);PlayerPrefs.Save();
            Log("done");EditorApplication.isPlaying=false;
        }
    }
}

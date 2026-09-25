using System;
using System.Globalization;
using UnityEngine;
namespace Confiscated
{
    /// <summary>Real elapsed time, sampled at run start/end; detention and menus count.</summary>
    public sealed class RunTiming
    {
        double started;
        bool running, finished;
        long elapsed;
        string category;
        public string Category => category;
        public long Milliseconds => running ? Math.Max(0,(long)((Time.realtimeSinceStartupAsDouble-started)*1000)) : elapsed;
        public void Start(string mode)
        {
            if(running||finished)return;
            category=(SchoolGameMode.Dark?"Dark / ":"")+mode;started=Time.realtimeSinceStartupAsDouble;running=true;
        }
        public long Stop()
        {
            if(running){elapsed=Milliseconds;running=false;finished=true;}
            return elapsed;
        }
        public static string Format(long ms) => (ms/60000).ToString("00")+":"+((ms/1000)%60).ToString("00")+"."+(ms%1000).ToString("000");
        // Smooth inverse-time score. Full milliseconds are used, not rounded display seconds.
        public static double Score(long ms) => 1000000000d/(Math.Max(0,ms)+1000d);
        public string Result(bool escaped)
        {
            long ms=Stop();
            string mode=category??"Run";
            if(!escaped)return "TIME  "+Format(ms)+"\nSCORE  0 - escape to score";
            string key="Confiscated.RunBest.v1."+mode;
            long best;
            bool hasBest=long.TryParse(PlayerPrefs.GetString(key,""),NumberStyles.Integer,CultureInfo.InvariantCulture,out best)&&best>=0;
            bool record=!hasBest||ms<best;
            string comparison=!hasBest?"FIRST FINISH":ms<best?"NEW BEST!  -"+Format(best-ms):ms==best?"PERSONAL BEST MATCHED":"+"+Format(ms-best)+" from best";
            if(record){best=ms;PlayerPrefs.SetString(key,ms.ToString(CultureInfo.InvariantCulture));PlayerPrefs.Save();}
            return "TIME  "+Format(ms)+"\nSCORE  "+Score(ms).ToString("N3",CultureInfo.InvariantCulture)+"\n"+comparison+"\nBEST  "+Format(best)+"  |  "+mode;
        }
    }
}

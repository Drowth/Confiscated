using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Title credits classroom, HUD chat counter, a 500-chatter stress vote, name-card recycling, the chat noticeboard and chat's report on the results screen.</summary>
    [InitializeOnLoad]
    public static class StreamerExtrasSmokeTest
    {
        const string Marker="Temp/streamer_extras_test",Dir="../Docs/Twitch",Report=Dir+"/Extras_Validation.txt";
        const int Crowd=500;
        static int step,errors;static double at,started;static bool pass,background;
        static string savedChannel;static int savedEnabled,savedNotes;static string boardText;
        static SchoolRunController R=>SchoolRunController.Instance;
        static TwitchChaos T=>TwitchChaos.Instance;
        static StreamerExtrasSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Start();}};}
        [MenuItem("Confiscated/Play Test/Arm Streamer Extras Test (runs on next Play)")]
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Start()
        {
            Directory.CreateDirectory(Dir);step=errors=0;pass=true;at=started=EditorApplication.timeSinceStartup;
            savedChannel=PlayerPrefs.GetString(TwitchChat.ChannelKey,"");savedEnabled=PlayerPrefs.GetInt(TwitchChat.EnabledKey,0);savedNotes=PlayerPrefs.GetInt(TwitchNoticeboard.NotesKey,1);
            background=Application.runInBackground;Application.runInBackground=true;
            File.WriteAllText(Report,"Streamer extras ("+DateTime.Now.ToString("s")+")\n");
            Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        }
        static void Log(string m,string t,LogType type){if(type==LogType.Error||type==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+m+"\n");}}
        static void Check(bool ok,string message){pass&=ok;File.AppendAllText(Report,(ok?"ok   ":"FAIL ")+message+"\n");}
        static void Next(int n){step=n;at=EditorApplication.timeSinceStartup;}
        static TextMesh Board=>GameObject.Find("Year 6 furnishings").GetComponentsInChildren<TextMesh>(true).First(t=>t.name=="BoardWriting");
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish();return;}
            double elapsed=EditorApplication.timeSinceStartup-at;
            try
            {
                if(EditorApplication.timeSinceStartup-started>120)throw new Exception("timeout at step "+step);
                switch(step)
                {
                    case 0:
                        if(elapsed<1)return;
                        if(TwitchChat.Instance!=null&&TwitchChat.Instance.State!=TwitchChat.Status.Off)TwitchChat.Instance.Disconnect();
                        boardText=Board.text;
                        SchoolTitleMenu.Instance.ShowCredits(true);Next(1);break;
                    case 1:
                        if(elapsed<.8)return;
                        var testers=Object.FindObjectsByType<SeatedStudent>(FindObjectsSortMode.None).Where(s=>s.name.EndsWith("(tester)")).ToList();
                        Check(SchoolTitleMenu.Instance.CreditsOpen&&testers.Count==SchoolCredits.Testers.Length,"credits: "+testers.Count+" testers seated in Year 6");
                        Check(SchoolCredits.Testers.All(n=>GameObject.Find("Name card - "+n)!=null),"each tester has a name card on their desk: "+SchoolCredits.TesterList);
                        Check(Board.text.Contains(SchoolCredits.Creator),"the board credits "+SchoolCredits.Creator);
                        Check(SchoolCredits.Testers.Contains("Joseph Taylor")&&SchoolCredits.Testers.Contains("Harry"),"Joseph Taylor and Harry are credited");
                        ScreenCapture.CaptureScreenshot(Dir+"/Credits.png");Next(2);break;
                    case 2:
                        if(elapsed<.6)return;
                        SchoolTitleMenu.Instance.ShowCredits(false);Next(3);break;
                    case 3:
                        if(elapsed<.4)return;
                        Check(!SchoolTitleMenu.Instance.CreditsOpen&&GameObject.Find("Title credits (temporary)")==null&&Board.text==boardText,"closing credits puts the classroom and the lesson board back");
                        Check(Object.FindObjectsByType<SeatedStudent>(FindObjectsSortMode.None).Count(s=>s.name.StartsWith("Seated")&&s.GetComponent<ChatterboxStudent>()==null)==2,"the regular classmates are back in their seats");
                        SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();Next(4);break;
                    case 4:
                        if(elapsed<1)return;
                        ComicDialogue.Cancel();R.period.PrepareChaseRetry();R.PrepareChaseRetry();R.PauseStaff();R.caretaker.Freeze();
                        var shade=Object.FindFirstObjectByType<LibraryShadow>();if(shade!=null)shade.Paused=true;
                        typeof(TwitchChaos).GetField("<NextVoteIn>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(T,9999f);
                        TwitchChat.InjectForTest("viewer","hello");Next(5);break;
                    case 5:
                        if(elapsed<.5)return;
                        Check(T.CounterText.Contains("1 chatter"),"HUD shows the chat count: \""+T.CounterText+"\"");
                        ScreenCapture.CaptureScreenshot(Dir+"/HudCounter.png");
                        // A big stream: 500 viewers vote within a moment, a third change their mind.
                        T.OpenVote();
                        for(int i=0;i<Crowd;i++)TwitchChat.InjectForTest("user"+i,((i%3)+1).ToString());
                        for(int i=0;i<Crowd;i+=3)TwitchChat.InjectForTest("user"+i,"2");
                        Next(6);break;
                    case 6:
                        if(elapsed<2)return;
                        Check(T.Tallies.Sum()==Crowd,"500 voters, one vote each after changes ("+string.Join(",",T.Tallies)+")");
                        Check(TwitchChat.Instance.ChatterCount==Crowd+1&&T.CounterText.Contains("501"),"counter reads "+T.CounterText);
                        var names=TwitchNameCards.Instance;
                        Check(names.FreeSlots==0,"a busy chat fills all "+names.SlotCount+" lockers and desks");
                        string holder=new[]{"viewer"}.Concat(Enumerable.Range(0,Crowd).Select(i=>"user"+i)).First(u=>names.CardFor(u)!=null);
                        TwitchNameCards.AgeForTest(holder,TwitchNameCards.RecycleAfterSeconds+60);
                        TwitchChat.InjectForTest("latecomer","hi all");Next(7);break;
                    case 7:
                        if(elapsed<.4)return;
                        var nc=TwitchNameCards.Instance;
                        Check(nc.CardFor("latecomer")!=null&&nc.CardFor("user0")==null||nc.CardFor("latecomer")!=null,"a new chatter takes the spot of someone quiet for 5+ minutes (recycled "+nc.Recycled+")");
                        T.CloseVote();
                        var report=T.Report();File.AppendAllText(Report,"---\n"+report+"\n---\n");
                        Check(report.Contains("CHAT'S REPORT")&&report.Contains("Quickest voter: user0")&&report.Contains("Most votes"),"chat's report credits voters by name");
                        // Noticeboard.
                        Check(TwitchNoticeboard.Clean("visit www.example.com")==null&&TwitchNoticeboard.Clean("go to example.tv now")==null,"links are refused");
                        Check(TwitchNoticeboard.Clean("f u c k this")==null,"spaced-out swearing is refused");
                        Check(TwitchNoticeboard.Clean("hi   caretaker <b>")=="hi caretaker b","extra spaces and markup are stripped");
                        var board=TwitchNoticeboard.Instance;
                        Check(board.Post("amy","Run Smith run!")&&!board.Post("amy","again already"),"one note per viewer per minute");
                        string[] samples={"RUN SMITH RUN!!","the caretaker is behind you","hide in my locker","dinner lady saw nothing","I left the lights on","go left at the hall","free the phone","sweets for the chatterbox","caretaker smells of mop"};
                        for(int i=0;i<14;i++)board.Post("noter"+i,samples[i%samples.Length]);
                        for(int i=0;i<16;i++)board.PinNext();
                        Check(TwitchNoticeboard.Notes.Count==TwitchNoticeboard.MaxNotes&&board.PinnedCount==TwitchNoticeboard.MaxNotes&&board.Visible,"board holds the latest "+TwitchNoticeboard.MaxNotes+" notes");
                        Check(TwitchNoticeboard.Notes.Last().user=="noter13"&&TwitchNoticeboard.Notes.All(n=>n.user!="amy"),"oldest notes fall off first");
                        Next(80);break;
                    case 80:
                        if(elapsed<.3)return; // replaced notes are destroyed at the end of the frame
                        var nb=TwitchNoticeboard.Instance;
                        var cam=new GameObject("Noticeboard camera").AddComponent<Camera>();cam.fieldOfView=50;
                        cam.transform.position=nb.Board.position-nb.Board.forward*1.9f;cam.transform.LookAt(nb.Board.position);
                        HallwayPropLibrary.Capture(cam,1400,900,Path.GetFullPath(Dir+"/Noticeboard.png"));Object.Destroy(cam.gameObject);
                        TwitchNoticeboard.NotesOn=false;Next(8);break;
                    case 8:
                        if(elapsed<.3)return;
                        Check(!TwitchNoticeboard.Instance.Visible,"'chat notes' off takes the board down");
                        TwitchNoticeboard.NotesOn=true;
                        GameManager.Instance.Caught(R.caretaker);Next(9);break;
                    case 9:
                        if(elapsed<1.5)return;
                        var body=HudController.Instance.overlayBody!=null?HudController.Instance.overlayBody.text:"";
                        Check(body.Contains("CHAT'S REPORT"),"the caught screen includes chat's report");
                        ScreenCapture.CaptureScreenshot(Dir+"/CaughtReport.png");Next(10);break;
                    case 10:
                        if(elapsed<.5)return;
                        Finish();break;
                }
            }
            catch(Exception e){Check(false,e.ToString());Finish();}
        }
        static void Finish()
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Application.runInBackground=background;TwitchChat.EndTestFeed();
            PlayerPrefs.SetString(TwitchChat.ChannelKey,savedChannel);PlayerPrefs.SetInt(TwitchChat.EnabledKey,savedEnabled);PlayerPrefs.SetInt(TwitchNoticeboard.NotesKey,savedNotes);PlayerPrefs.Save();
            Check(errors==0,"no runtime errors");File.AppendAllText(Report,pass?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;
        }
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Confiscated.EditorTools
{
    [InitializeOnLoad]
    public static class DarkDialogueSmokeTest
    {
        const string Marker="Temp/dark_dialogue_test",Report="../Docs/DarkMode/DialogueValidation.txt";
        const string Pass="Caretaker: Smith, why aren't you in class? Show me your hall pass.";
        static int stage,frame,completed;
        static double at,started;
        static bool background;
        static GameManager G=>GameManager.Instance;
        static DarkDialogueSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);Begin();}};}
        [MenuItem("Confiscated/Dark Mode/Arm Dialogue Test")]
        public static void Arm(){Directory.CreateDirectory("../Docs/DarkMode");File.WriteAllText(Marker,"armed");}
        static void Begin()
        {
            stage=0;frame=-1;started=at=EditorApplication.timeSinceStartup;
            background=Application.runInBackground;Application.runInBackground=true;SteamLeaderboard.Suppress=true;
            completed=PlayerPrefs.GetInt("Confiscated.Ending.v1.Completed",-1);PlayerPrefs.SetInt("Confiscated.Ending.v1.Completed",1);
            File.WriteAllText(Report,"Dark dialogue checks in the actual school; checkpoint warp and direct dialogue calls; no synthetic score or persistent unlock.\n");EditorApplication.update+=Tick;
        }
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);File.AppendAllText(Report,"ok "+label+"\n");}
        static void Next(){stage++;at=EditorApplication.timeSinceStartup;}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish(false);return;}if(frame==Time.frameCount)return;frame=Time.frameCount;
            double dt=EditorApplication.timeSinceStartup-at;
            try
            {
                if(EditorApplication.timeSinceStartup-started>60)throw new Exception("Timeout at stage "+stage);
                switch(stage)
                {
                    case 0:
                        if(dt<1||G==null||!SchoolTitleMenu.IsActive)return;
                        Check(!DarkModeDialogue.Active&&DarkModeDialogue.Resolve(Pass)==Pass,"normal-mode words unchanged");
                        SchoolTitleMenu.Instance.StartDarkGame();SchoolTitleMenu.Instance.SkipIntro();ComicDialogue.Cancel();
                        Check(!DarkModeDialogue.Active&&DarkModeDialogue.Resolve(Pass)==Pass,"dark selection alone does not mention a power cut before it happens");
                        G.schoolPeriod.PrepareChaseRetry();SchoolRunController.Instance.PrepareChaseRetry();SchoolRunController.Instance.PauseStaff();G.GetComponent<DarkModeController>().PrepareRetry();
                        var p=G.schoolPeriod.Player;var f=p.GetComponent<FirstPersonController>();f.Controller.enabled=false;p.transform.position=new Vector3(-33.6f,0,29);f.Controller.enabled=true;Next();break;
                    case 1:
                        if(!DarkModeDialogue.Active)return;
                        Check(!ComicDialogue.IsActive&&Time.timeScale==1,"blackout reaction does not pause or turn the camera towards a hidden teacher");
                        Check(G.schoolPeriod.teacher.GetComponent<DarkModeVoice>()!=null,"Mr Reed reacts from his classroom");
                        Check(DarkModeDialogue.Lines.All(l=>!string.IsNullOrEmpty(l.text)&&DarkModeDialogue.Voice(l)!=null),"every contextual line has a matching recording or nonverbal fallback");
                        foreach(var line in DarkModeDialogue.Lines.Where(l=>l.speaker!="Miss D Tenison"))
                        {
                            var recorded=Resources.Load<AudioClip>("Audio/"+line.clip);
                            Check(recorded!=null&&recorded.channels==1&&recorded.length>0&&DarkModeDialogue.Voice(line)==recorded,"supplied mono recording selected: "+line.clip);
                        }
                        Check(DarkModeDialogue.Resolve("Mr Reed: Master Smith. That had better not be a phone.")=="Mr Reed: Master Smith. That had better not be a phone.","unrelated recorded introduction preserved");
                        ComicDialogue.TrySpeak(Pass);Next();break;
                    case 2:
                        if(dt<.5)return;
                        Check(ComicDialogue.IsActive&&ComicDialogue.Instance.FullText.Contains("lights are out"),"pass inspection uses power-cut dialogue");
                        Check(ComicDialogue.Instance.GetComponents<AudioSource>().Any(a=>a.isPlaying&&a.clip==Resources.Load<AudioClip>("Audio/DarkMode/CaretakerHallPass")),"pass dialogue plays the supplied Dark Mode recording");
                        ComicDialogue.Instance.Advance();
                        ScreenCapture.CaptureScreenshot("../Docs/DarkMode/13-dark-dialogue.png");Next();break;
                    case 3:
                        if(dt<.5)return;ComicDialogue.Cancel();
                        var caretaker=SchoolRunController.Instance.caretaker;caretaker.ResumeAfterDetention(0);
                        Check(caretaker.Say("CaretakerSearch",true),"caretaker contextual search voice starts");
                        Check(HudController.Instance.statusText.text.Contains("I can still hear you"),"search words match the new sound cue");
                        caretaker.Freeze();
                        HudController.Instance.SetStatus("Dinner lady: Mind out, love.",3);
                        Check(HudController.Instance.statusText.text.Contains("barely see")&&Object.FindFirstObjectByType<DinnerTrolleyPatrol>().GetComponent<DarkModeVoice>()!=null,"dinner-lady warning changes words and voice together");
                        Check(DarkModeDialogue.Resolve("Miss D Tenison: "+DetentionBlackboard.Introduction).Contains("power cut"),"detention acknowledges the outage while retaining the cleaning task");
                        var chatter=Object.FindFirstObjectByType<ChatterboxStudent>();
                        Check(ComicDialogue.TrySpeakTimed(chatter.transform,"Chatterbox",DarkModeDialogue.Chatterbox[0].text,2),"chatterbox can deliver the red-eye remark");
                        Check(ComicDialogue.Instance.FullText.Contains("glowing red"),"chatterbox text refers to the glowing eyes");ComicDialogue.Cancel();Next();break;
                    case 4:
                        SchoolGameMode.Select(false);
                        Check(!DarkModeDialogue.Active&&DarkModeDialogue.Resolve(Pass)==Pass&&!DarkModeDialogue.TryBark("CaretakerSearch",out _),"normal mode returns to original dialogue and recordings");Finish(true);break;
                }
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL stage "+stage+": "+e+"\n");Finish(false);}
        }
        static void Finish(bool success)
        {
            EditorApplication.update-=Tick;ComicDialogue.Cancel();Application.runInBackground=background;
            if(completed<0)PlayerPrefs.DeleteKey("Confiscated.Ending.v1.Completed");else PlayerPrefs.SetInt("Confiscated.Ending.v1.Completed",completed);PlayerPrefs.Save();
            File.AppendAllText(Report,success?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;
        }
    }
}

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Confiscated.EditorTools
{
    [InitializeOnLoad] public static class ReedVoiceSmokeTest
    {
        const string Marker="Temp/reed_voice_test",Report="../Docs/ReedVoice_Validation.txt";
        static int stage;static double at;
        static ReedVoiceSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker)){File.Delete(Marker);stage=0;at=EditorApplication.timeSinceStartup;File.WriteAllText(Report,"Mr Reed typewriter and supplied voice check\n");EditorApplication.update+=Tick;}};}
        public static void Arm()=>File.WriteAllText(Marker,"1");
        static void Check(bool condition,string label){if(!condition)throw new Exception(label);File.AppendAllText(Report,"ok "+label+"\n");}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){EditorApplication.update-=Tick;return;}
            if(EditorApplication.timeSinceStartup-at<.9)return;
            try
            {
                var d=ComicDialogue.Instance;
                switch(stage)
                {
                    case 0:
                        SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();ComicDialogue.Cancel();
                        ComicDialogue.TrySpeak("Mr Reed: These newsletters need delivering to the office. Please take your time to read these instructions before leaving the classroom, Smith.\nCaretaker: I'll take those keys.");break;
                    case 1:
                        Check(d.IsTyping&&d.VisibleText.Length>0&&d.VisibleText.Length<d.FullText.Length,"letters reveal progressively");
                        Check(d.IsReedVoicePlaying,"provided audio plays during Reed typing");
                        Check(d.GetComponent<AudioSource>().clip.name=="MrReedTalk1","correct recording loaded");
                        d.Advance();Check(!d.IsTyping&&!d.IsReedVoicePlaying,"click completes text and stops voice immediately");
                        d.Advance();break;
                    case 2:
                        Check(d.Speaker=="Caretaker"&&!d.IsReedVoicePlaying,"other speaker does not play Reed voice");
                        ComicDialogue.Cancel();Check(!ComicDialogue.IsActive&&!d.IsReedVoicePlaying,"dismissal clears dialogue and sound");
                        ComicDialogue.TrySpeak("Mr Reed: Hello.");break;
                    case 3:
                        if(d.IsTyping)return;
                        Check(!d.IsReedVoicePlaying,"natural typing completion stops voice");
                        d.Advance();Check(!ComicDialogue.IsActive&&!d.IsReedVoicePlaying,"closing completed line stays silent");
                        File.AppendAllText(Report,"PASS\n");EditorApplication.update-=Tick;EditorApplication.isPlaying=false;return;
                }
                stage++;at=EditorApplication.timeSinceStartup;
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");EditorApplication.update-=Tick;EditorApplication.isPlaying=false;}
        }
    }
}

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Exercises actual scare coroutines in SchoolLayout, without rebuilding or saving the scene.</summary>
    [InitializeOnLoad]
    public static class JumpscareRefinementSmokeTest
    {
        const string Marker="Temp/jumpscare_refinement_test",Report="../Docs/JumpscareRefinement_Validation.txt";
        static int stage,variant,lastFrame,errors,savedHead;static bool savedHeadExists,background,sampled;
        static double started,at;static float savedMotion,near,fov;static Vector3 eyePosition;static Quaternion eyeRotation;
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static SchoolRunController Run=>SchoolRunController.Instance;
        static PlayerInteractor Player=>Run.period.Player;
        static LibraryShadow Shadow=>Object.FindFirstObjectByType<LibraryShadow>();
        static ChaseCamera Feel=>Player.GetComponent<ChaseCamera>();
        static FirstPersonController Legs=>Player.GetComponent<FirstPersonController>();
        static AudioSource Source(string field)=>(AudioSource)typeof(LibraryShadow).GetField(field,Private).GetValue(Shadow);
        static JumpscareRefinementSmokeTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&File.Exists(Marker))Begin();};}
        public static void Arm()=>File.WriteAllText(Marker,"armed");
        static void Begin()
        {
            File.Delete(Marker);stage=variant=errors=0;lastFrame=-1;started=at=EditorApplication.timeSinceStartup;
            background=Application.runInBackground;Application.runInBackground=true;
            savedHeadExists=PlayerPrefs.HasKey(CaretakerCatchScare.ModelledHeadKey);savedHead=PlayerPrefs.GetInt(CaretakerCatchScare.ModelledHeadKey);
            File.WriteAllText(Report,"Jumpscare refinement: "+DateTime.Now.ToString("O")+"\n");
            Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        }
        static void Log(string msg,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error){errors++;File.AppendAllText(Report,"ERROR "+msg+"\n");}}
        static void Need(bool condition,string label){File.AppendAllText(Report,(condition?"ok ":"FAIL ")+label+"\n");if(!condition)throw new Exception(label);}
        static void Next(int value){stage=value;at=EditorApplication.timeSinceStartup;sampled=false;}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish(false);return;}
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            double age=EditorApplication.timeSinceStartup-at;
            try
            {
                if(EditorApplication.timeSinceStartup-started>50)throw new Exception("Timed out at "+stage);
                if(stage==0)
                {
                    if(age<1)return;
                    if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();}
                    ComicDialogue.Cancel();Run.period.PrepareChaseRetry();Run.PrepareChaseRetry();Run.PauseStaff();Run.caretaker.Freeze();
                    savedMotion=Feel.intensity;Need(Shadow!=null,"live library shadow exists");
                    var roaming=Shadow.GetComponent<LibraryShadowWorldHead>();Need(roaming!=null&&roaming.Ready,"generated head attached during ordinary play");
                    Need(Shadow.eyes.Length==2&&Shadow.eyes[0].name=="White slit eye","roaming eyes use the generated head");
                    roaming.SetVisible(0,false);Need(!Shadow.eyes[0].gameObject.activeInHierarchy,"roaming head conceals with shadow");
                    roaming.SetVisible(.7f,false);Need(Shadow.eyes[0].gameObject.activeInHierarchy,"roaming head returns with shadow");
                    Next(1);return;
                }
                if(stage==1)
                {
                    if(age<.3)return;
                    Feel.intensity=variant==0?1:0;Shadow.Paused=false;Run.Recover(LibraryShadow.LibraryItem);
                    var body=Player.GetComponent<CharacterController>();body.enabled=false;Player.transform.position=Shadow.entrances[0].position;body.enabled=true;Physics.SyncTransforms();
                    near=Player.ViewCamera.nearClipPlane;fov=Feel.BaseFov;eyePosition=Player.ViewCamera.transform.localPosition;eyeRotation=Player.ViewCamera.transform.localRotation;
                    Source("breath").volume=1;Source("breath").Play();
                    Shadow.StartCoroutine((IEnumerator)typeof(LibraryShadow).GetMethod("Catch",Private).Invoke(Shadow,new object[]{Player}));
                    Need(!Source("breath").isPlaying&&Source("breath").volume==0,"shadow breathing stops immediately (effects "+Feel.intensity+")");
                    Next(2);return;
                }
                if(stage==2)
                {
                    if(age>.25&&age<.65&&!sampled){Need(SchoolAudio.MusicDuck<=.09f,"whisper music duck");sampled=true;}
                    if(age<1.23)return;
                    var overlay=GameObject.Find("Library shadow jumpscare");Need(overlay!=null,"shadow overlay visible");
                    var art=overlay.transform.Find("Face").GetComponent<RawImage>();Need(art.texture is RenderTexture&&overlay.GetComponent<LibraryShadowScareHead>().Ready,"generated 3D shadow head used");
                    Need(Source("sting").isPlaying&&Source("sting").time>.7f&&Source("sting").time<1.2f,"scream passes measured peak at arrival");
                    if(variant==1){Need(Quaternion.Angle(art.transform.localRotation,Quaternion.identity)<.01f,"effects off: no face roll");Need(!overlay.transform.Find("Flash").GetComponent<Image>().enabled,"effects off: no flash");}
                    ScreenCapture.CaptureScreenshot("../Docs/Jumpscare_Shadow_"+variant+".png");Next(3);return;
                }
                if(stage==3)
                {
                    if(age<1.1)return;
                    Need(!Shadow.Catching&&GameObject.Find("Library shadow jumpscare")==null&&GameObject.Find("Shadow scare 3D stage")==null,"shadow overlay and 3D stage cleaned up");
                    Need(!Run.Has(LibraryShadow.LibraryItem)&&!Shadow.Inside(Player.transform.position),"handheld returned and player ejected");
                    Need(!Legs.MovementLocked&&!Legs.LookLocked,"player controls restored");
                    Need(Mathf.Abs(Player.ViewCamera.nearClipPlane-near)<.001f&&Mathf.Abs(Player.ViewCamera.fieldOfView-fov)<.1f,"camera near plane and lens restored");
                    Need(!Source("sting").isPlaying,"scream stopped after cut");
                    Shadow.Paused=true;variant++;Next(variant<2?1:4);return;
                }
                if(stage==4)
                {
                    if(age<.3)return;
                    Feel.intensity=variant==2?1:0;PlayerPrefs.SetInt(CaretakerCatchScare.ModelledHeadKey,1);
                    if(variant==3)GameManager.Instance.Caught(Run.caretaker);else Need(CaretakerCatchScare.Play(Run.caretaker),"caretaker mesh starts");
                    Next(5);return;
                }
                if(stage==5)
                {
                    if(age<.48)return;
                    var scare=GameObject.Find("Caretaker caught close-up");Need(scare!=null,"caretaker hit visible");
                    var audio=scare.GetComponent<AudioSource>();Need(audio.isPlaying&&audio.time>.23f&&audio.time<.6f,"caretaker sound peak aligned to arrival");
                    Need(SchoolAudio.MusicDuck<=.09f,"caretaker music duck");
                    var art=scare.GetComponentInChildren<RawImage>();Need(art.enabled,"face is visible during hit");
                    if(variant==3){Need(art.texture is RenderTexture,"3D caretaker mode runs");var head=GameObject.Find("Caretaker lunge stage").GetComponentInChildren<CaretakerHeadEyes>();Need(head!=null&&Mathf.Abs(Mathf.DeltaAngle(head.transform.localEulerAngles.z,0))<.01f,"effects off: 3D head has no roll");}
                    ScreenCapture.CaptureScreenshot("../Docs/Jumpscare_Caretaker_"+variant+".png");Next(6);return;
                }
                if(stage==6)
                {
                    if(age<.6)return;
                    Need(GameObject.Find("Caretaker caught close-up")==null&&GameObject.Find("Caretaker lunge stage")==null,"caretaker stage and overlay cleaned up");
                    variant++;if(variant<4){Next(4);return;}
                    Need(GameManager.Instance.Current==GameManager.State.Caught,"real caretaker catch still ends run");
                    Need(errors==0,"no runtime errors");Finish(true);
                }
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");Finish(false);}
        }
        static void Finish(bool pass)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
            if(Run!=null&&Run.period!=null&&Run.period.Player!=null&&Feel!=null)Feel.intensity=savedMotion;
            if(savedHeadExists)PlayerPrefs.SetInt(CaretakerCatchScare.ModelledHeadKey,savedHead);else PlayerPrefs.DeleteKey(CaretakerCatchScare.ModelledHeadKey);
            Application.runInBackground=background;File.AppendAllText(Report,pass?"PASS\n":"FAIL\n");EditorApplication.isPlaying=false;
        }
    }
}

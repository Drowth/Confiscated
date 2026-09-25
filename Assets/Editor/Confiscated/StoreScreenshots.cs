using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace Confiscated.EditorTools
{
    // Steam store screenshots: forces the Game view to a fixed 1920x1080 (Steam's size) for capture, then restores
    // whatever size the user had selected. Uses UnityEditor internals (GameView/GameViewSizes) via reflection.
    public static class StoreScreenshots
    {
        public const string Dir="../Docs/SteamStore/Screenshots/";
        const BindingFlags Any=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        static readonly Assembly Ed=typeof(Editor).Assembly;

        static EditorWindow GameView()=>EditorWindow.GetWindow(Ed.GetType("UnityEditor.GameView"));
        static object Group()
        {
            var sizes=Ed.GetType("UnityEditor.GameViewSizes");
            var instance=typeof(ScriptableSingleton<>).MakeGenericType(sizes).GetProperty("instance").GetValue(null);
            return sizes.GetMethod("GetGroup").Invoke(instance,new object[]{(int)GameViewSizeGroupType.Standalone});
        }
        static int FindOrAdd(int w,int h)
        {
            var group=Group();var gt=group.GetType();
            int count=(int)gt.GetMethod("GetTotalCount").Invoke(group,null);
            for(int i=0;i<count;i++)
            {
                var s=gt.GetMethod("GetGameViewSize").Invoke(group,new object[]{i});var st=s.GetType();
                if((int)st.GetProperty("width").GetValue(s)==w&&(int)st.GetProperty("height").GetValue(s)==h&&st.GetProperty("sizeType").GetValue(s).ToString()=="FixedResolution")return i;
            }
            var sizeType=Ed.GetType("UnityEditor.GameViewSizeType");
            var size=Activator.CreateInstance(Ed.GetType("UnityEditor.GameViewSize"),Enum.Parse(sizeType,"FixedResolution"),w,h,"Steam store");
            gt.GetMethod("AddCustomSize").Invoke(group,new[]{size});
            return count;
        }
        static PropertyInfo Selected=>Ed.GetType("UnityEditor.GameView").GetProperty("selectedSizeIndex",Any);

        // SessionState survives the domain reload that entering Play mode causes; 0 is Free Aspect.
        const string PrevKey="Confiscated.StoreShots.PrevGameViewSize";
        [MenuItem("Confiscated/Steam/Game View 1920x1080")]
        public static void UseStoreSize()
        {
            var gv=GameView();int target=FindOrAdd(1920,1080);int current=(int)Selected.GetValue(gv);
            if(current!=target)SessionState.SetInt(PrevKey,current);
            Selected.SetValue(gv,target);gv.Repaint();
        }
        [MenuItem("Confiscated/Steam/Restore Game View Size")]
        public static void RestoreSize()
        {
            var gv=GameView();Selected.SetValue(gv,SessionState.GetInt(PrevKey,0));SessionState.EraseInt(PrevKey);gv.Repaint();
        }
        public static void Capture(string name){Directory.CreateDirectory(Dir);ScreenCapture.CaptureScreenshot(Dir+name+".png");}

        // Posing helpers for play-mode shots.
        public static void Warp(Vector3 feet,Vector3 look)
        {
            var p=GameManager.Instance.schoolPeriod.Player;var f=p.GetComponent<FirstPersonController>();
            f.Controller.enabled=false;p.transform.position=feet;f.Controller.enabled=true;
            var flat=look-feet;flat.y=0;if(flat.sqrMagnitude>.001f)p.transform.rotation=Quaternion.LookRotation(flat);
            f.LookLocked=true;p.ViewCamera.transform.LookAt(look);Physics.SyncTransforms();
        }
        public static void Release(){GameManager.Instance.schoolPeriod.Player.GetComponent<FirstPersonController>().LookLocked=false;}
        public static void SetState(CaretakerAI ai,CaretakerAI.State s)=>typeof(CaretakerAI).GetField("state",Any).SetValue(ai,s);
        public static void Place(CaretakerAI ai,Vector3 p,Vector3 face)
        {
            var agent=ai.GetComponent<UnityEngine.AI.NavMeshAgent>();agent.Warp(p);agent.ResetPath();
            var d=face-p;d.y=0;if(d.sqrMagnitude>.001f)ai.transform.rotation=Quaternion.LookRotation(d);
        }
    }
}

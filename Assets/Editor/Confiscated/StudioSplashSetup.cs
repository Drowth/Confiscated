using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// Rusty Spoon Studio splash: logo fades in, holds, fades out, then loads SchoolLayout. Any key/click skips it.
    /// Its own scene, inserted first in Build Settings -- only shows in an actual build, not when pressing Play
    /// straight into SchoolLayout in the editor.
    /// </summary>
    public static class StudioSplashSetup
    {
        const string ScenePath="Assets/Scenes/StudioSplash.unity";
        const string LogoPath="Assets/Art/UI/T_RustySpoonStudioLogo.png";

        [MenuItem("Confiscated/Studio/Build Splash Screen")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Leave Play first.");
            var logo=AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            if(logo==null)throw new System.InvalidOperationException("Missing sprite "+LogoPath+" -- import the studio logo first.");

            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);

            var camGo=new GameObject("Splash Camera");
            var cam=camGo.AddComponent<Camera>();
            cam.clearFlags=CameraClearFlags.SolidColor;
            cam.backgroundColor=new Color(.07f,.06f,.05f); // near-black: the logo itself is transparent-backed
            cam.orthographic=true;cam.cullingMask=0;
            camGo.AddComponent<AudioListener>();
            camGo.tag="MainCamera";

            var canvasGo=new GameObject("Splash Canvas");
            var canvas=canvasGo.AddComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);
            scaler.matchWidthOrHeight=.5f;
            var group=canvasGo.AddComponent<CanvasGroup>();group.alpha=0;

            var logoGo=new GameObject("Logo");
            logoGo.transform.SetParent(canvasGo.transform,false);
            var img=logoGo.AddComponent<Image>();img.sprite=logo;img.preserveAspect=true;img.raycastTarget=false;
            var rt=logoGo.GetComponent<RectTransform>();
            rt.anchorMin=new Vector2(.5f,.5f);rt.anchorMax=new Vector2(.5f,.5f);rt.pivot=new Vector2(.5f,.5f);
            rt.sizeDelta=new Vector2(1100,550);rt.anchoredPosition=Vector2.zero;

            var controller=canvasGo.AddComponent<StudioSplashController>();
            controller.logo=group;controller.nextScene="SchoolLayout";

            EditorSceneManager.SaveScene(scene,ScenePath);

            var builds=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).ToList();
            builds.Insert(0,new EditorBuildSettingsScene(ScenePath,true));
            EditorBuildSettings.scenes=builds.ToArray();

            AssetDatabase.SaveAssets();
            Debug.Log("[StudioSplash] Built "+ScenePath+" and put it first in Build Settings.");
        }
    }
}

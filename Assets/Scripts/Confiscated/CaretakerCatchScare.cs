using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace Confiscated
{
    public sealed class CaretakerCatchScare : MonoBehaviour
    {
        public const float Duration=1.15f;
        /// <summary>Player preference: 1 lunges the modelled head at the camera, 0 (default) scales the flat drawing.</summary>
        public const string ModelledHeadKey="Caught3DHead";
        const string ModelledHeadResource="Art/CaretakerLungeHead3D";
        // The modelled head is one unit tall; this is its height in metres in front of the lunge camera.
        // A longish lens: a wide one inflates the nose until it hides the grin.
        const float HeadHeight=.5f,LungeFieldOfView=28f;
        static readonly Vector3 RigOrigin=new Vector3(0,-500,0);
        public static float LungeScale(float elapsed)=>Mathf.Lerp(.16f,2.2f,Mathf.Pow(Mathf.Clamp01((elapsed-.08f)/.8f),2.35f));
        public static bool UseModelledHead=>PlayerPrefs.GetInt(ModelledHeadKey,0)==1&&Resources.Load<GameObject>(ModelledHeadResource)!=null;
        public static void Play(CaretakerAI caretaker)
        {
            if(caretaker==null)return;
            Texture face=Resources.Load<Texture2D>("Art/CaretakerLungeHead");
            if(face==null)face=Resources.Load<Texture2D>("Art/CaretakerCaughtFace");
            if(face==null)foreach(var r in caretaker.GetComponentsInChildren<Renderer>())
                if(r.sharedMaterial!=null&&r.sharedMaterial.HasProperty("_BaseMap")){face=r.sharedMaterial.GetTexture("_BaseMap");if(face!=null)break;}
            if(face==null&&!UseModelledHead)return;
            var go=new GameObject("Caretaker caught close-up");go.AddComponent<CaretakerCatchScare>().StartCoroutine(go.GetComponent<CaretakerCatchScare>().Show(face));
        }

        /// <summary>A private stage far below the school: black void, one camera, the head looking back along +Z.</summary>
        public static Camera BuildLungeRig(out Transform head)
        {
            // Deliberately not under the overlay canvas, whose transform is driven in screen pixels.
            var stage=new GameObject("Caretaker lunge stage").transform;stage.position=RigOrigin;
            var camera=new GameObject("Lunge camera").AddComponent<Camera>();camera.transform.SetParent(stage,false);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.fieldOfView=LungeFieldOfView;
            camera.nearClipPlane=.02f;camera.farClipPlane=20;camera.allowHDR=false;camera.depth=-50;
            head=Instantiate(Resources.Load<GameObject>(ModelledHeadResource),stage).transform;head.localScale=Vector3.one*HeadHeight;
            return camera;
        }
        /// <summary>Same growth curve as the flat drawing, expressed as real distance so the nose leads and the face balloons.</summary>
        public static void PoseLungeRig(Camera camera,Transform head,float elapsed)
        {
            float scale=LungeScale(elapsed),approach=Mathf.InverseLerp(.16f,2.2f,scale);
            // Perspective enlarges the near features, so the modelled head stops a little short of the flat drawing's final size
            // to keep both the eyes and the grin in view.
            float distance=HeadHeight/(2*Mathf.Tan(LungeFieldOfView*.5f*Mathf.Deg2Rad)*Mathf.Lerp(.16f,1.75f,approach));
            // Never let the tip of the nose cross the lens.
            distance=Mathf.Max(distance,HeadHeight*.45f);
            float settle=Mathf.Clamp01(elapsed/.88f);
            // He comes in turned and tilted, and is square-on by the time the eyes and grin fill the view.
            // The lens ends just below the nose. Any lower, or any wider a lens, and the generator's blank underside of the nose shows.
            float yaw=Mathf.Lerp(24,0,Mathf.SmoothStep(0,1,settle)),pitch=Mathf.Lerp(-5,-8,approach),roll=Mathf.Lerp(-6,0,settle);
            if(elapsed>.88f){float tremble=(elapsed-.88f)*90;yaw+=Mathf.Sin(tremble*1.7f)*.7f;roll+=Mathf.Sin(tremble)*.6f;}
            head.localPosition=new Vector3(Mathf.Lerp(.35f,0,Mathf.SmoothStep(0,1,settle))*distance*.4f,Mathf.Lerp(-.02f,.085f*HeadHeight,approach),distance);
            head.localRotation=Quaternion.Euler(pitch,180+yaw,roll);
        }

        IEnumerator Show(Texture texture)
        {
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,1000);scaler.matchWidthOrHeight=.5f;
            var bg=new GameObject("Dark backdrop",typeof(RectTransform),typeof(Image));bg.transform.SetParent(transform,false);
            var br=bg.GetComponent<RectTransform>();br.anchorMin=Vector2.zero;br.anchorMax=Vector2.one;br.offsetMin=br.offsetMax=Vector2.zero;bg.GetComponent<Image>().color=Color.black;bg.GetComponent<Image>().raycastTarget=false;
            var art=new GameObject("Caretaker lunge",typeof(RectTransform),typeof(RawImage));art.transform.SetParent(transform,false);
            var rect=art.GetComponent<RectTransform>();var image=art.GetComponent<RawImage>();image.raycastTarget=false;
            if(UseModelledHead)
            {
                // The stage camera draws into a texture that fills the screen above the black backdrop.
                var target=new RenderTexture(Mathf.Max(16,Screen.width),Mathf.Max(16,Screen.height),24);
                var camera=BuildLungeRig(out var head);camera.targetTexture=target;
                rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;image.texture=target;
                for(float elapsed=0;elapsed<Duration;elapsed+=Time.unscaledDeltaTime){PoseLungeRig(camera,head,elapsed);yield return null;}
                camera.targetTexture=null;Destroy(camera.transform.parent.gameObject);target.Release();Destroy(target);
                Destroy(gameObject);yield break;
            }
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(0,-40);
            image.texture=texture;image.uvRect=new Rect(0,0,1,1);
            rect.sizeDelta=new Vector2(1000f*texture.width/texture.height,1000);
            for(float elapsed=0;elapsed<Duration;elapsed+=Time.unscaledDeltaTime)
            {
                float scale=LungeScale(elapsed);
                rect.localScale=Vector3.one*scale;
                rect.anchoredPosition=new Vector2(0,Mathf.InverseLerp(.16f,2.2f,scale)*160);
                rect.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(-6,0,Mathf.Clamp01(elapsed/.88f)));
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}

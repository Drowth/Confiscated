using UnityEngine;
using UnityEngine.UI;

namespace Confiscated
{
    /// <summary>A real mesh on a private black stage, rendered above the school during the catch.</summary>
    public sealed class LibraryShadowScareHead : MonoBehaviour
    {
        public const string Resource="Art/LibraryShadowScareHead3D";
        Transform stage,head;Camera view;RenderTexture target;RawImage image;
        public static bool Available=>Resources.Load<GameObject>(Resource)!=null;
        public bool Ready=>head!=null;
        public void Build(RawImage output)
        {
            image=output;
            stage=new GameObject("Shadow scare 3D stage").transform;stage.position=new Vector3(0,-700,0);
            view=new GameObject("Shadow scare camera").AddComponent<Camera>();view.transform.SetParent(stage,false);
            view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=Color.black;view.fieldOfView=32;
            view.nearClipPlane=.015f;view.farClipPlane=10;view.allowHDR=false;view.depth=-50;
            target=new RenderTexture(Mathf.Max(16,Screen.width),Mathf.Max(16,Screen.height),24);view.targetTexture=target;
            head=Instantiate(Resources.Load<GameObject>(Resource),stage).transform;head.localScale=Vector3.one*.5f;
            output.texture=target;output.color=Color.white;
            var r=output.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;r.localScale=Vector3.one;
            Pose(0,0,0);
        }
        public void Pose(float approach,float hold,float motion)
        {
            if(head==null)return;
            float height=Mathf.Lerp(.12f,1.28f,approach)+hold*.12f;
            float distance=.5f/(2*Mathf.Tan(view.fieldOfView*.5f*Mathf.Deg2Rad)*height);
            float shake=motion*Mathf.Exp(-hold*8)*Mathf.Clamp01(hold*30);
            head.localPosition=new Vector3(Mathf.Lerp(-.08f,0,approach)+Mathf.Sin(hold*103)*.004f*shake,-.015f,distance);
            head.localRotation=Quaternion.Euler(-3,180+Mathf.Lerp(-18,0,approach)*motion+Mathf.Sin(hold*71)*shake,Mathf.Lerp(5,0,approach)*motion+Mathf.Sin(hold*91)*shake);
        }
        void OnDestroy()
        {
            if(view!=null)view.targetTexture=null;
            if(stage!=null)Destroy(stage.gameObject);
            if(target!=null){target.Release();Destroy(target);}
        }
    }
}

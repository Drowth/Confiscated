using UnityEngine;
using UnityEngine.UI;
namespace Confiscated
{
    /// <summary>The caught screen's portrait: the modelled head on its own black stage, looking side to side with rolling pupils.</summary>
    public sealed class CaughtPortraitHead : MonoBehaviour
    {
        const float HeadHeight=.5f,FieldOfView=28f,Distance=1.32f;
        // Well clear of the lunge stage, which can be alive at the same moment.
        static readonly Vector3 StageOrigin=new Vector3(100,-500,0);
        RenderTexture target;Transform stage,head;CaretakerHeadEyes eyes;float started;
        public static bool Available=>Resources.Load<GameObject>("Art/CaretakerLungeHead3D")!=null;
        public static Camera BuildStage(out Transform head)
        {
            var stage=new GameObject("Caught portrait stage").transform;stage.position=StageOrigin;
            var camera=new GameObject("Portrait camera").AddComponent<Camera>();camera.transform.SetParent(stage,false);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.fieldOfView=FieldOfView;
            camera.nearClipPlane=.05f;camera.farClipPlane=10;camera.allowHDR=false;camera.depth=-51;
            head=Instantiate(Resources.Load<GameObject>("Art/CaretakerLungeHead3D"),stage).transform;head.localScale=Vector3.one*HeadHeight;
            head.localPosition=new Vector3(0,0,Distance);
            return camera;
        }
        /// <summary>A slow, suspicious sweep from side to side. Kept under 30 degrees: the model's sides are invented.</summary>
        public static void Pose(Transform head,float seconds)
        {
            float yaw=Mathf.Sin(seconds*1.15f)*27,pitch=Mathf.Sin(seconds*.7f+1)*4-2,roll=Mathf.Sin(seconds*.9f+.5f)*3;
            head.localRotation=Quaternion.Euler(pitch,180+yaw,roll);
            head.GetComponent<CaretakerHeadEyes>()?.Roll(seconds);
        }
        void Awake()
        {
            target=new RenderTexture(1024,1024,24);var camera=BuildStage(out head);camera.targetTexture=target;stage=camera.transform.parent;
            GetComponent<RawImage>().texture=target;started=Time.unscaledTime;Pose(head,0);
        }
        void Update(){if(head!=null)Pose(head,Time.unscaledTime-started);}
        void OnDestroy()
        {
            if(stage!=null)Destroy(stage.gameObject);
            if(target!=null){target.Release();Destroy(target);}
        }
    }
}

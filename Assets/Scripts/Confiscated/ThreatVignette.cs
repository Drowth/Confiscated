using UnityEngine;
using UnityEngine.UI;
namespace Confiscated
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ThreatVignette : MaskableGraphic
    {
        public float strength=.18f;
        public float alert;
        public void SetAlert(float value){alert=value;SetVerticesDirty();}
        public void SetStrength(float value){if(Mathf.Abs(value-strength)<.005f)return;strength=value;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var rect=rectTransform.rect;const int nx=24,ny=16;
            for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++)
            {
                float u=x/(float)nx,v=y/(float)ny;float distance=new Vector2((u-.5f)*2,(v-.5f)*2).magnitude;
                float alpha=Mathf.SmoothStep(0,strength,Mathf.InverseLerp(.45f,1.3f,distance));
                float rim=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.82f,1f,Mathf.Max(Mathf.Abs(u-.5f)*2,Mathf.Abs(v-.5f)*2)))*alert;
                vh.AddVert(new Vector3(rect.xMin+rect.width*u,rect.yMin+rect.height*v),new Color(Mathf.Lerp(.015f,.8f,rim),.025f,.03f,Mathf.Max(alpha,rim*.6f)),Vector2.zero);
            }
            for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){int a=y*(nx+1)+x;vh.AddTriangle(a,a+nx+1,a+1);vh.AddTriangle(a+1,a+nx+1,a+nx+2);}
        }
    }
}

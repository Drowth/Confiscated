using UnityEngine;
using UnityEngine.UI;
namespace Confiscated
{
    /// <summary>Two broken, uneven ink strokes follow each UI square instead of a smooth vector border.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class SketchBorder : MaskableGraphic
    {
        public float thickness=2.1f;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float inset=3;
            Vector2[] corners={new(r.xMin+inset,r.yMin+inset),new(r.xMax-inset,r.yMin+inset),new(r.xMax-inset,r.yMax-inset),new(r.xMin+inset,r.yMax-inset)};
            for(int pass=0;pass<2;pass++)for(int side=0;side<4;side++)for(int segment=0;segment<11;segment++)
            {
                if(pass==1&&(segment+side)%4==0)continue;
                var a=Vector2.Lerp(corners[side],corners[(side+1)%4],segment/11f);
                var b=Vector2.Lerp(corners[side],corners[(side+1)%4],(segment+1)/11f);
                var normal=new Vector2(-(b-a).y,(b-a).x).normalized;
                a+=normal*(Mathf.Sin(segment*2.3f+side)*.8f+pass*2.4f);
                b+=normal*(Mathf.Sin((segment+1)*2.3f+side)*.8f+pass*2.4f);
                var width=normal*(pass==0?thickness:.65f);int start=vh.currentVertCount;
                var tint=color;if(pass==1)tint.a*=.65f;
                vh.AddVert(a-width,tint,Vector2.zero);vh.AddVert(a+width,tint,Vector2.zero);
                vh.AddVert(b+width,tint,Vector2.zero);vh.AddVert(b-width,tint,Vector2.zero);
                vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
            }
        }
    }
}

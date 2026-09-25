using UnityEngine;
using UnityEngine.UI;

namespace Confiscated
{
    /// <summary>Stable pencil strokes and crosshatching; geometry stays crisp at any HUD scale.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SketchStaminaBar : MaskableGraphic
    {
        public float Fraction { get; private set; } = 1f;
        public void SetFraction(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Abs(value - Fraction) < .001f) return;
            Fraction = value;
            SetVerticesDirty();
        }

        void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero);
            vh.AddVert(c, tint, Vector2.zero); vh.AddVert(d, tint, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
        void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 n = new Vector2(-(b-a).y, (b-a).x).normalized * width * .5f;
            Quad(vh, a-n, a+n, b+n, b-n, tint);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            Color ink = new Color(.18f,.22f,.21f), paper = new Color(.9f,.87f,.73f,.97f);
            Quad(vh,new Vector2(r.xMin,r.yMin+2),new Vector2(r.xMin+1,r.yMax-1),
                new Vector2(r.xMax-2,r.yMax),new Vector2(r.xMax,r.yMin),paper);
            float left=r.xMin+8, bottom=r.yMin+8, top=r.yMax-8;
            float end=left+(r.width-16)*Fraction;
            Color fill=Fraction<=.2f?new Color(.85f,.14f,.11f):new Color(.39f,.53f,.39f);
            Quad(vh,new Vector2(left,bottom),new Vector2(left,top),new Vector2(end,top),new Vector2(end,bottom),fill);
            // Fixed irregularity avoids flicker while stamina changes.
            for(float x=left+3;x<end;x+=6)
            {
                float y=bottom+Mathf.Sin(x*1.7f)*.8f;
                float dx=Mathf.Min(9,end-x);
                Stroke(vh,new Vector2(x,y),new Vector2(x+dx,Mathf.Lerp(bottom,top,dx/9)),1,new Color(.2f,.29f,.24f,.6f));
            }
            for(int pass=0;pass<2;pass++)
            {
                float inset=3+pass*2;
                Vector2 a=new Vector2(r.xMin+inset,r.yMin+inset),b=new Vector2(r.xMin+inset+1,r.yMax-inset);
                Vector2 c=new Vector2(r.xMax-inset,r.yMax-inset-1),d=new Vector2(r.xMax-inset-1,r.yMin+inset+1);
                Stroke(vh,a,b,pass==0?1.8f:.8f,ink);Stroke(vh,b,c,1.2f,ink);
                Stroke(vh,c,d,1.7f,ink);Stroke(vh,d,a,1,ink);
            }
        }
    }
}

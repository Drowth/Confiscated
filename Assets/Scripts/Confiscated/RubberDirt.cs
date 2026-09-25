using UnityEngine;
using UnityEngine.UI;
namespace Confiscated
{
    /// <summary>Stable chalk smears fade from the felt as dust is knocked free.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RubberDirt : MaskableGraphic
    {
        public float dirt=1;
        public void SetDirt(float value){dirt=value;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var random=new System.Random(412);
            for(int i=0;i<95;i++)
            {
                float threshold=(float)random.NextDouble();float x=((float)random.NextDouble()-.5f)*140,y=((float)random.NextDouble()-.5f)*85;
                float sx=2+(float)random.NextDouble()*14,sy=1+(float)random.NextDouble()*4;
                float alpha=Mathf.Clamp01((dirt-threshold)*6)*.8f;if(alpha<=0)continue;
                var c=new Color(.91f,.9f,.78f,alpha);int n=vh.currentVertCount;
                vh.AddVert(new Vector2(x-sx,y-sy),c,Vector2.zero);vh.AddVert(new Vector2(x-sx,y+sy),c,Vector2.zero);vh.AddVert(new Vector2(x+sx,y+sy),c,Vector2.zero);vh.AddVert(new Vector2(x+sx,y-sy),c,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
        }
    }
}


using UnityEngine;
using UnityEngine.UI;
namespace Confiscated
{
    /// <summary>
    /// Five shadowed boxes holding the belongings' own artwork: greyed out until recovered, then full colour with a two-stroke chalk tick.
    /// Without artwork (icons unassigned) it falls back to the pencil silhouettes.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PickupChecklist : MaskableGraphic
    {
        readonly float[] ticks=new float[5];readonly bool[] held=new bool[5];
        readonly RawImage[] pictures=new RawImage[5];readonly Material[] greys=new Material[5];
        Ticks chalk;
        public float Tick(int i)=>ticks[i];
        public bool HasPicture(int i)=>pictures[i]!=null;
        Vector2 Cell(int i){var r=rectTransform.rect;return new Vector2(r.xMin+18+i*86,r.yMin+31);}

        /// <summary>Artwork in run order: phone, yo-yo, handheld, skateboard, robot. Missing entries keep their silhouette.</summary>
        public void SetIcons(Texture[] icons)
        {
            if(icons==null)return;
            var shader=Resources.Load<Shader>("Shaders/UIGreyable");
            for(int i=0;i<5&&i<icons.Length;i++)
            {
                if(icons[i]==null||pictures[i]!=null)continue;
                var go=new GameObject("Belonging "+i,typeof(RectTransform),typeof(RawImage));go.transform.SetParent(transform,false);
                var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=Vector2.zero;rt.pivot=new Vector2(.5f,.5f);
                rt.anchoredPosition=new Vector2(10+i*86+35,9+38);
                // Fit the 58 x 64 space inside the box without stretching the drawing.
                float aspect=(float)icons[i].width/icons[i].height;
                rt.sizeDelta=aspect>58f/64f?new Vector2(58,58/aspect):new Vector2(64*aspect,64);
                var image=go.GetComponent<RawImage>();image.texture=icons[i];image.raycastTarget=false;
                if(shader!=null){greys[i]=new Material(shader);image.material=greys[i];}
                pictures[i]=image;
            }
            // The tick is drawn by a last child so it lands on top of the artwork.
            var top=new GameObject("Chalk ticks",typeof(RectTransform),typeof(CanvasRenderer),typeof(Ticks));top.transform.SetParent(transform,false);
            var tr=(RectTransform)top.transform;tr.pivot=rectTransform.pivot;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;
            chalk=top.GetComponent<Ticks>();chalk.owner=this;chalk.raycastTarget=false;
            Paint();SetVerticesDirty();
        }
        void Paint()
        {
            for(int i=0;i<5;i++)
            {
                if(pictures[i]==null)continue;
                if(greys[i]!=null){greys[i].SetFloat("_Grey",held[i]?0:1);pictures[i].color=new Color(1,1,1,held[i]?1:.5f);}
                // No shader: a dark shadow of the item stands in for the greyed-out drawing.
                else pictures[i].color=held[i]?Color.white:new Color(0,0,0,.5f);
            }
        }
        void Update()
        {
            var run=SchoolRunController.Instance;if(run==null)return;
            bool changed=false;
            for(int i=0;i<5;i++){bool has=run.Has(i);if(has&&!held[i])ticks[i]=0;changed|=has!=held[i];held[i]=has;ticks[i]=has?Mathf.Min(1,ticks[i]+Time.deltaTime*2.5f):0;}
            if(changed)Paint();
            SetVerticesDirty();if(chalk!=null)chalk.SetVerticesDirty();
        }
        protected override void OnDestroy(){foreach(var m in greys)if(m!=null)Destroy(m);base.OnDestroy();}

        static void Line(VertexHelper vh,Vector2 a,Vector2 b,Color c,float width=2)
        {var n=new Vector2(a.y-b.y,b.x-a.x).normalized*width*.5f;int v=vh.currentVertCount;vh.AddVert(a-n,c,Vector2.zero);vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);vh.AddVert(b-n,c,Vector2.zero);vh.AddTriangle(v,v+1,v+2);vh.AddTriangle(v,v+2,v+3);}
        static void Fill(VertexHelper vh,Vector2 p,Vector2 size,Color c)
        {int v=vh.currentVertCount;vh.AddVert(p,c,Vector2.zero);vh.AddVert(p+Vector2.up*size.y,c,Vector2.zero);vh.AddVert(p+size,c,Vector2.zero);vh.AddVert(p+Vector2.right*size.x,c,Vector2.zero);vh.AddTriangle(v,v+1,v+2);vh.AddTriangle(v,v+2,v+3);}
        static void Box(VertexHelper vh,Vector2 p,Vector2 size,Color c){Line(vh,p,p+Vector2.right*size.x,c);Line(vh,p+Vector2.right*size.x,p+size,c);Line(vh,p+size,p+Vector2.up*size.y,c);Line(vh,p+Vector2.up*size.y,p,c);}
        static void Circle(VertexHelper vh,Vector2 p,float radius,Color c){for(int j=0;j<20;j++){float a=j*Mathf.PI/10,b=(j+1)*Mathf.PI/10;Line(vh,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,c);}}
        static void DrawTick(VertexHelper vh,Vector2 p,float tick)
        {
            Color chalk=new Color(.62f,1,.63f);
            Vector2 a=p+new Vector2(13,-9),b=p+new Vector2(22,-16),end=p+new Vector2(44,6);
            if(tick>0)Line(vh,a,Vector2.Lerp(a,b,Mathf.Clamp01(tick*3)),chalk,3);
            if(tick>1f/3)Line(vh,b,Vector2.Lerp(b,end,(tick-1f/3)*1.5f),chalk,3);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for(int i=0;i<5;i++)
            {
                Vector2 p=Cell(i);Color c=held[i]?new Color(.92f,.92f,.77f):new Color(.67f,.68f,.6f,.8f);
                Fill(vh,p-new Vector2(8,22),new Vector2(70,76),new Color(.07f,.08f,.1f,held[i]?.5f:.62f));
                Box(vh,p-new Vector2(8,22),new Vector2(70,76),new Color(.88f,.86f,.72f,held[i]?.8f:.45f));
                if(pictures[i]!=null)continue;
                if(i==0){Box(vh,p+new Vector2(12,0),new Vector2(23,43),c);Box(vh,p+new Vector2(16,9),new Vector2(15,26),c);}
                if(i==1){Circle(vh,p+new Vector2(24,23),16,c);Circle(vh,p+new Vector2(24,23),4,c);Line(vh,p+new Vector2(24,7),p+new Vector2(44,-1),c);}
                if(i==2){Box(vh,p+new Vector2(7,2),new Vector2(36,40),c);Box(vh,p+new Vector2(13,20),new Vector2(24,17),c);Line(vh,p+new Vector2(12,10),p+new Vector2(23,10),c);Line(vh,p+new Vector2(17,5),p+new Vector2(17,15),c);Circle(vh,p+new Vector2(34,10),2,c);}
                if(i==3){Box(vh,p+new Vector2(0,19),new Vector2(52,10),c);Circle(vh,p+new Vector2(9,13),5,c);Circle(vh,p+new Vector2(43,13),5,c);}
                if(i==4){Box(vh,p+new Vector2(12,8),new Vector2(28,23),c);Box(vh,p+new Vector2(15,32),new Vector2(22,13),c);Circle(vh,p+new Vector2(21,39),2,c);Circle(vh,p+new Vector2(31,39),2,c);Line(vh,p+new Vector2(18,8),p+new Vector2(15,0),c);Line(vh,p+new Vector2(34,8),p+new Vector2(37,0),c);}
            }
            // With no artwork there is no top layer, so the ticks are drawn here as before.
            if(chalk==null)for(int i=0;i<5;i++)DrawTick(vh,Cell(i),ticks[i]);
        }

        /// <summary>Top layer: only the chalk ticks, above the artwork.</summary>
        public sealed class Ticks : MaskableGraphic
        {
            public PickupChecklist owner;
            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();if(owner==null)return;
                for(int i=0;i<5;i++)DrawTick(vh,owner.Cell(i),owner.ticks[i]);
            }
        }
    }
}

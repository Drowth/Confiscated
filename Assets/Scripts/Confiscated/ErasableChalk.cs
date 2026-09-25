using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Confiscated
{
    /// <summary>Chalk glyphs split into small textured fragments. Rubbing removes only touched fragments.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class ErasableChalk : MaskableGraphic
    {
        public Font font;
        public string[] lines = { "Make good choices.", "Look after our school.", "Walk in the corridors.", "Return borrowed things.", "Keep the classroom tidy.", "Listen before you act." };
        struct Fragment { public Vector2 min,max,uvBL,uvBR,uvTL,uvTR;public float opacity; }
        readonly List<Fragment> pieces=new();
        public float Cleared {get;private set;}
        public override Texture mainTexture=>font!=null?font.material.mainTexture:Texture2D.whiteTexture;
        protected override void OnEnable(){base.OnEnable();Font.textureRebuilt+=FontChanged;}
        protected override void OnDisable(){Font.textureRebuilt-=FontChanged;base.OnDisable();}
        void FontChanged(Font changed){if(changed==font)Build(true);}
        bool building;
        public void ResetInk(){Cleared=0;Build(false);}
        void Build(bool preserve)
        {
            if(font==null||building)return;building=true;
            var old=preserve?pieces.ToArray():null;pieces.Clear();
            foreach(string line in lines)font.RequestCharactersInTexture(line,43,FontStyle.Normal);
            for(int i=0;i<lines.Length;i++)AddLine(lines[i],43,200-i*77);
            if(old!=null&&old.Length==pieces.Count)for(int i=0;i<pieces.Count;i++){var p=pieces[i];p.opacity=old[i].opacity;pieces[i]=p;}
            building=false;SetAllDirty();
        }
        void AddLine(string text,int size,float baseline)
        {
            font.RequestCharactersInTexture(text,size,FontStyle.Normal);float width=0;
            foreach(char c in text){font.GetCharacterInfo(c,out var ch,size);width+=ch.advance;}
            float x=-width*.5f;
            foreach(char c in text)
            {
                font.GetCharacterInfo(c,out var ch,size);
                if(!char.IsWhiteSpace(c))for(int row=0;row<6;row++)for(int col=0;col<6;col++)
                {
                    float u=col/6f,v=row/6f,u1=(col+1)/6f,v1=(row+1)/6f;
                    Vector2 UV(float a,float b)=>Vector2.Lerp(Vector2.Lerp(ch.uvBottomLeft,ch.uvBottomRight,a),Vector2.Lerp(ch.uvTopLeft,ch.uvTopRight,a),b);
                    pieces.Add(new Fragment{min=new Vector2(x+Mathf.Lerp(ch.minX,ch.maxX,u),baseline+Mathf.Lerp(ch.minY,ch.maxY,v)),max=new Vector2(x+Mathf.Lerp(ch.minX,ch.maxX,u1),baseline+Mathf.Lerp(ch.minY,ch.maxY,v1)),uvBL=UV(u,v),uvBR=UV(u1,v),uvTL=UV(u,v1),uvTR=UV(u1,v1),opacity=1});
                }
                x+=ch.advance;
            }
        }
        public float Rub(Vector2 from,Vector2 to,float radius)
        {
            float removed=0,total=0;var segment=to-from;float length=segment.sqrMagnitude;
            for(int i=0;i<pieces.Count;i++)
            {
                var p=pieces[i];var center=(p.min+p.max)*.5f;
                var nearest=from+segment*(length<.01f?0:Mathf.Clamp01(Vector2.Dot(center-from,segment)/length));
                float d=Vector2.Distance(center,nearest),before=p.opacity;
                if(d<radius)p.opacity=Mathf.Max(0,p.opacity-Mathf.Clamp01((radius-d)/8f));
                removed+=before-p.opacity;total+=p.opacity;pieces[i]=p;
            }
            Cleared=pieces.Count==0?0:1-total/pieces.Count;
            if(removed>0)SetVerticesDirty();return removed;
        }
        public void ClearAll(){for(int i=0;i<pieces.Count;i++){var p=pieces[i];p.opacity=0;pieces[i]=p;}Cleared=1;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();foreach(var p in pieces)
            {
                if(p.opacity<=0)continue;int n=vh.currentVertCount;var tint=color;tint.a*=p.opacity;
                vh.AddVert(p.min,tint,p.uvBL);vh.AddVert(new Vector2(p.min.x,p.max.y),tint,p.uvTL);
                vh.AddVert(p.max,tint,p.uvTR);vh.AddVert(new Vector2(p.max.x,p.min.y),tint,p.uvBR);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
        }
    }
}

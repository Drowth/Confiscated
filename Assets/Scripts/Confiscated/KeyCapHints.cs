using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Confiscated
{
    /// <summary>Drawn key caps and mouse buttons for control hints (Resources/Art/UI). Falls back to plain text if the art is missing.</summary>
    public static class KeyCapHints
    {
        static readonly string[] Caps={"F","Q","E","1","2","SHIFT","SPACE","TAB"};
        // Opaque bounds of each drawn cap in the 256 px cells (x, y from bottom, w, h), so sprites crop tight.
        static readonly RectInt[] CapRects={new RectInt(34,34,188,188),new RectInt(290,34,188,188),new RectInt(546,34,188,188),new RectInt(802,34,188,188),new RectInt(1058,34,188,188),new RectInt(1292,40,232,176),new RectInt(1548,40,232,176),new RectInt(1826,34,188,188)};
        static readonly RectInt[] MouseRects={new RectInt(63,23,129,210),new RectInt(319,23,129,210)};
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        static Sprite Get(string key)
        {
            if(sprites.Count==0)
            {
                var caps=Resources.Load<Texture2D>("Art/UI/T_UI_KeyCaps");var mouse=Resources.Load<Texture2D>("Art/UI/T_UI_MouseButtons");
                if(caps!=null)for(int i=0;i<Caps.Length;i++)sprites[Caps[i]]=Cell(caps,CapRects[i]);
                if(mouse!=null){sprites["LMB"]=Cell(mouse,MouseRects[0]);sprites["RMB"]=Cell(mouse,MouseRects[1]);}
            }
            return sprites.TryGetValue(key,out var s)?s:null;
        }
        static Sprite Cell(Texture2D t,RectInt r)=>Sprite.Create(t,new Rect(r.x,r.y,r.width,r.height),new Vector2(.5f,.5f),100);

        /// <summary>One left-aligned row: each hint is its keys (joined by "/") then a short label, e.g. ("F", "interact").</summary>
        public static RectTransform Row(Transform parent,float height,int fontSize,Color color,params (string keys,string label)[] hints)
        {
            var row=new GameObject("Key hints",typeof(RectTransform),typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();row.SetParent(parent,false);
            var layout=row.GetComponent<HorizontalLayoutGroup>();layout.childAlignment=TextAnchor.MiddleLeft;layout.spacing=height*.12f;
            layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=layout.childForceExpandHeight=false;
            for(int h=0;h<hints.Length;h++)
            {
                var keys=hints[h].keys.Split(' ');
                for(int k=0;k<keys.Length;k++)
                {
                    if(k>0)Label(row,"/",height,fontSize,color);
                    var sprite=Get(keys[k]);
                    if(sprite==null){Label(row,keys[k],height,fontSize,color);continue;}
                    var go=new GameObject(keys[k],typeof(RectTransform),typeof(Image),typeof(LayoutElement));go.transform.SetParent(row,false);
                    var image=go.GetComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;
                    var e=go.GetComponent<LayoutElement>();e.preferredHeight=height;e.preferredWidth=height*sprite.rect.width/sprite.rect.height;
                }
                Label(row,hints[h].label+(h<hints.Length-1?"   ":""),height,fontSize,color);
            }
            return row;
        }
        static void Label(Transform row,string text,float height,int fontSize,Color color)
        {
            var go=new GameObject("Text",typeof(RectTransform),typeof(Text),typeof(LayoutElement));go.transform.SetParent(row,false);
            var t=go.GetComponent<Text>();t.font=SchoolTypography.Font;t.fontSize=fontSize;t.color=color;t.text=text;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;
            t.horizontalOverflow=HorizontalWrapMode.Overflow;go.GetComponent<LayoutElement>().preferredHeight=height;
        }
    }
}

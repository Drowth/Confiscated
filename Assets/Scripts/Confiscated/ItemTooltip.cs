using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Confiscated
{
    /// <summary>The small paper card that follows the mouse over an inventory or codex square: name, what it is, how to use it.</summary>
    public sealed class ItemTooltip : MonoBehaviour
    {
        static readonly Color Paper=new(.97f,.94f,.83f),Ink=new(.12f,.17f,.26f),Pencil=new(.45f,.32f,.12f);
        RectTransform rect,canvasRect;
        Text title,body,use;
        const float Width=330;

        public static ItemTooltip Create(RectTransform canvas)
        {
            var go=new GameObject("Item tooltip",typeof(RectTransform),typeof(Image));go.transform.SetParent(canvas,false);
            var tip=go.AddComponent<ItemTooltip>();tip.canvasRect=canvas;tip.rect=(RectTransform)go.transform;
            tip.rect.anchorMin=tip.rect.anchorMax=new Vector2(.5f,.5f);tip.rect.pivot=new Vector2(0,1);
            var image=go.GetComponent<Image>();image.color=Paper;image.raycastTarget=false;
            var edge=new GameObject("Pencil edge",typeof(RectTransform),typeof(SketchBorder)).GetComponent<RectTransform>();edge.SetParent(go.transform,false);
            edge.anchorMin=Vector2.zero;edge.anchorMax=Vector2.one;edge.offsetMin=edge.offsetMax=Vector2.zero;
            var border=edge.GetComponent<SketchBorder>();border.color=Ink;border.raycastTarget=false;
            tip.title=tip.Line(24,Ink,FontStyle.Bold);tip.body=tip.Line(19,Ink,FontStyle.Normal);tip.use=tip.Line(17,Pencil,FontStyle.Italic);
            go.SetActive(false);return tip;
        }
        Text Line(int size,Color colour,FontStyle style)
        {
            var t=new GameObject("Line",typeof(RectTransform),typeof(Text)).GetComponent<Text>();t.transform.SetParent(transform,false);
            var r=t.rectTransform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.sizeDelta=new Vector2(Width-28,40);
            t.font=SchoolTypography.Font;t.fontSize=size;t.fontStyle=style;t.color=colour;t.alignment=TextAnchor.UpperLeft;
            t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;t.raycastTarget=false;return t;
        }

        public void Show(string name,string what,string how)
        {
            title.text=name;body.text=what??"";use.text=how??"";
            float y=-12;
            foreach(var t in new[]{title,body,use})
            {
                bool on=!string.IsNullOrEmpty(t.text);t.gameObject.SetActive(on);if(!on)continue;
                t.rectTransform.anchoredPosition=new Vector2(14,y);float h=t.preferredHeight;t.rectTransform.sizeDelta=new Vector2(Width-28,h);y-=h+6;
            }
            rect.sizeDelta=new Vector2(Width,-y+8);
            gameObject.SetActive(true);transform.SetAsLastSibling();Follow();
        }
        public void Show(ItemCatalogue.Item item){if(item!=null)Show(item.name,item.blurb,item.use);}
        public void Hide()=>gameObject.SetActive(false);
        void LateUpdate()=>Follow();
        void Follow()
        {
            if(Mouse.current==null||canvasRect==null)return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,Mouse.current.position.ReadValue(),null,out var p);
            var half=canvasRect.rect.size*.5f;var size=rect.sizeDelta;
            p+=new Vector2(22,-22);
            if(p.x+size.x>half.x-8)p.x-=size.x+44;
            if(p.y-size.y<-half.y+8)p.y+=size.y+44;
            rect.anchoredPosition=p;
        }
    }

    /// <summary>A square showing one item: its picture, a count, greyed or "?" when not had. Hover shows the tooltip.</summary>
    public sealed class ItemCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public ItemTooltip tooltip;
        public Image picture;
        public Text count,caption,unknown;
        public SketchBorder border;
        /// <summary>What to show on hover; null to show nothing.</summary>
        public Func<(string name,string what,string how)?> tip;
        bool hovered;
        static readonly Color Ink=new(.13f,.18f,.29f,.86f),Hot=new(.62f,.38f,.10f);

        public void Set(ItemCatalogue.Item item,bool have,int amount,bool known=true)
        {
            picture.sprite=item?.picture;picture.enabled=known&&item?.picture!=null;
            picture.color=have?Color.white:new Color(.2f,.22f,.28f,.28f);
            count.text=have&&amount>1?"x"+amount:"";
            if(caption!=null)caption.text=known&&item!=null?item.name:"";
            if(unknown!=null)unknown.gameObject.SetActive(!known);
        }
        public void OnPointerEnter(PointerEventData e)
        {
            hovered=true;if(border!=null)border.color=Hot;
            var t=tip?.Invoke();if(t.HasValue&&tooltip!=null)tooltip.Show(t.Value.name,t.Value.what,t.Value.how);
        }
        public void OnPointerExit(PointerEventData e){hovered=false;if(border!=null)border.color=Ink;if(tooltip!=null)tooltip.Hide();}
        void OnDisable(){if(hovered&&tooltip!=null)tooltip.Hide();hovered=false;if(border!=null)border.color=Ink;}
    }
}

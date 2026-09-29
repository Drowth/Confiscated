using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Confiscated
{
    /// <summary>
    /// Picking something up. The first time ever: its picture pops in, then what it is, and it goes in the codex.
    /// After that: a small picture-and-name toast. Never blocks play; waits out dialogue and the pause menu.
    /// </summary>
    public sealed class ItemPopup : MonoBehaviour
    {
        static ItemPopup instance;
        static readonly Color Paper=new(.95f,.92f,.80f),Ink=new(.12f,.17f,.26f),Pencil=new(.45f,.32f,.12f);
        readonly Queue<(ItemCatalogue.Item item,bool first)> queue=new();
        RectTransform card,toast;
        CanvasGroup cardGroup,textGroup,toastGroup;
        Image picture,toastPicture;
        Text heading,title,blurb,use,toastName;
        (ItemCatalogue.Item item,bool first) current;
        float age=-1;

        public static void Show(ItemCatalogue.Item item,bool first)
        {
            if(item==null)return;
            if(instance==null)instance=new GameObject("Item pop-up").AddComponent<ItemPopup>();
            instance.queue.Enqueue((item,first));
        }
        public static bool Showing=>instance!=null&&instance.age>=0;

        void Awake(){Build();}
        void OnDestroy(){if(instance==this)instance=null;}

        void Update()
        {
            if(ComicDialogue.IsActive||PauseMenu.IsOpen)return;
            if(age<0)
            {
                if(queue.Count==0)return;
                current=queue.Dequeue();age=0;Begin();
            }
            age+=Time.unscaledDeltaTime;
            if(current.first)
            {
                // Picture first, with a little overshoot; the words follow; then it fades.
                float pop=Mathf.Clamp01(age/.28f),scale=pop<1?Mathf.LerpUnclamped(.55f,1f,1-(1-pop)*(1-pop))+Mathf.Sin(pop*Mathf.PI)*.08f:1;
                picture.rectTransform.localScale=Vector3.one*scale;
                cardGroup.alpha=age<4.3f?Mathf.Clamp01(age/.15f):Mathf.Clamp01(1-(age-4.3f)/.4f);
                textGroup.alpha=Mathf.Clamp01((age-.5f)/.35f);
                if(age>4.7f)End();
            }
            else
            {
                toastGroup.alpha=age<1.6f?Mathf.Clamp01(age/.12f):Mathf.Clamp01(1-(age-1.6f)/.3f);
                toast.anchoredPosition=new Vector2(0,188+Mathf.Clamp01(age/.2f)*12);
                if(age>1.9f)End();
            }
        }

        void Begin()
        {
            var item=current.item;
            if(current.first)
            {
                card.gameObject.SetActive(true);picture.sprite=item.picture;picture.enabled=item.picture!=null;
                title.text=item.name;blurb.text=item.blurb;use.text=item.use;use.gameObject.SetActive(!string.IsNullOrEmpty(item.use));
                textGroup.alpha=0;cardGroup.alpha=0;
            }
            else
            {
                toast.gameObject.SetActive(true);toastPicture.sprite=item.picture;toastPicture.enabled=item.picture!=null;toastName.text=item.name;toastGroup.alpha=0;
            }
        }
        void End(){age=-1;card.gameObject.SetActive(false);toast.gameObject.SetActive(false);}

        void Build()
        {
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=250;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,1000);scaler.matchWidthOrHeight=1;

            card=Panel("New item",transform,new Vector2(.5f,1),new Vector2(0,-350),new Vector2(460,400));
            cardGroup=card.gameObject.AddComponent<CanvasGroup>();cardGroup.blocksRaycasts=false;
            heading=Words(card,"NEW  ·  added to your codex",new Vector2(0,172),new Vector2(420,30),17,Pencil,FontStyle.Bold);
            picture=new GameObject("Picture",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            picture.transform.SetParent(card,false);picture.rectTransform.anchoredPosition=new Vector2(0,62);picture.rectTransform.sizeDelta=new Vector2(190,190);
            picture.preserveAspect=true;picture.raycastTarget=false;
            var words=new GameObject("Explanation",typeof(RectTransform),typeof(CanvasGroup)).GetComponent<RectTransform>();words.SetParent(card,false);
            textGroup=words.GetComponent<CanvasGroup>();
            title=Words(words,"",new Vector2(0,-60),new Vector2(420,40),32,Ink,FontStyle.Bold);
            blurb=Words(words,"",new Vector2(0,-112),new Vector2(410,64),21,Ink,FontStyle.Normal);
            use=Words(words,"",new Vector2(0,-165),new Vector2(410,30),19,Pencil,FontStyle.Italic);
            card.gameObject.SetActive(false);

            toast=Panel("Picked up",transform,new Vector2(.5f,0),new Vector2(0,188),new Vector2(300,76));
            toastGroup=toast.gameObject.AddComponent<CanvasGroup>();toastGroup.blocksRaycasts=false;
            toastPicture=new GameObject("Picture",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            toastPicture.transform.SetParent(toast,false);toastPicture.rectTransform.anchoredPosition=new Vector2(-108,0);toastPicture.rectTransform.sizeDelta=new Vector2(62,62);
            toastPicture.preserveAspect=true;toastPicture.raycastTarget=false;
            toastName=Words(toast,"",new Vector2(34,0),new Vector2(210,60),22,Ink,FontStyle.Bold);toastName.alignment=TextAnchor.MiddleLeft;
            toast.gameObject.SetActive(false);
        }
        static RectTransform Panel(string label,Transform parent,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var rect=new GameObject(label,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;
            var image=rect.GetComponent<Image>();image.color=Paper;image.raycastTarget=false;
            var edge=new GameObject("Pencil edge",typeof(RectTransform),typeof(SketchBorder)).GetComponent<RectTransform>();edge.SetParent(rect,false);
            edge.anchorMin=Vector2.zero;edge.anchorMax=Vector2.one;edge.offsetMin=edge.offsetMax=Vector2.zero;
            var border=edge.GetComponent<SketchBorder>();border.color=Ink;border.raycastTarget=false;
            return rect;
        }
        static Text Words(Transform parent,string value,Vector2 position,Vector2 size,int fontSize,Color colour,FontStyle style)
        {
            var t=new GameObject(value.Length>0?value:"Words",typeof(RectTransform),typeof(Text)).GetComponent<Text>();t.transform.SetParent(parent,false);
            t.rectTransform.anchoredPosition=position;t.rectTransform.sizeDelta=size;t.font=SchoolTypography.Font;t.fontSize=fontSize;t.fontStyle=style;
            t.color=colour;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;t.text=value;return t;
        }
    }
}

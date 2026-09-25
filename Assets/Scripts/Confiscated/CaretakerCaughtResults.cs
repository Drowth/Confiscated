using UnityEngine;
using UnityEngine.UI;
namespace Confiscated
{
    public static class CaretakerCaughtResults
    {
        public static void Show(HudController hud)
        {
            var texture=Resources.Load<Texture2D>("Art/CaretakerCaughtFace");
            // The modelled head replaces the drawn portrait; the drawing remains the fallback if the model is missing.
            bool modelled=CaughtPortraitHead.Available;
            if(hud==null||hud.overlay==null||texture==null&&!modelled)return;
            var background=hud.overlay.GetComponent<Image>();
            if(background!=null)background.color=Color.black;
            var panel=new GameObject("Caught portrait area",typeof(RectTransform));
            panel.transform.SetParent(hud.overlay.transform,false);panel.transform.SetAsFirstSibling();
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=new Vector2(.01f,.03f);rect.anchorMax=new Vector2(.51f,.97f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            var art=new GameObject("Caught face portrait",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));
            art.transform.SetParent(panel.transform,false);
            var raw=art.GetComponent<RawImage>();raw.texture=texture;raw.raycastTarget=false;
            var aspect=art.GetComponent<AspectRatioFitter>();aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;aspect.aspectRatio=modelled?1:(float)texture.width/texture.height;
            if(modelled)art.AddComponent<CaughtPortraitHead>();
            Place(hud.overlayTitle,new Vector2(.52f,.7f),new Vector2(.98f,.88f));
            Place(hud.overlayBody,new Vector2(.53f,.12f),new Vector2(.97f,.69f));
        }
        static void Place(Text text,Vector2 min,Vector2 max)
        {
            if(text==null)return;
            var rect=text.rectTransform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=18;
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
namespace Confiscated
{
    /// <summary>Pale writing paper with subtle paper grain, shared by worksheets and held documents.</summary>
    public static class SchoolPaperStyle
    {
        public static Image Apply(GameObject target,Sprite texture,bool button=false)
        {
            var background=target.AddComponent<Image>();
            background.color=button?new Color(.945f,.94f,.915f):new Color(.99f,.985f,.965f);
            var grain=new GameObject("Faint paper grain",typeof(RectTransform),typeof(Image));grain.transform.SetParent(target.transform,false);
            var rect=grain.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=grain.GetComponent<Image>();image.sprite=texture;image.color=new Color(1,1,1,.65f);image.raycastTarget=false;
            return background;
        }
    }
}

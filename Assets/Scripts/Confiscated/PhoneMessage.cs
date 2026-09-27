using UnityEngine;
using UnityEngine.UI;

namespace Confiscated
{
    /// <summary>
    /// The moment Smith gets the phone back it lights up: "4 missed messages", and a friend's text that tells the player
    /// what the run is about (the rest of the confiscated stuff). A small hand-drawn phone slides up bottom right, buzzes,
    /// types the text, and slides away. It never pauses the game. Shown once per session (a retry already knows).
    /// </summary>
    public sealed class PhoneMessage : MonoBehaviour
    {
        public const string Header="4 missed messages",Sender="Maddie",Text="did u get ur stuff back?? he's got ur yoyo AND ur skateboard lol";
        public const float ShowSeconds=9f,LettersPerSecond=22f;
        public static bool Shown {get;private set;}
        public static PhoneMessage Current {get;private set;}
        public bool Visible=>root!=null&&root.gameObject.activeSelf;
        public string Typed {get;private set;}="";
        RectTransform root;Text header,body;float started;static AudioClip buzz;
        public static void Show()
        {
            if(Shown||HudController.Instance==null)return;
            Shown=true;var host=new GameObject("Phone message",typeof(PhoneMessage));Current=host.GetComponent<PhoneMessage>();
        }
        public static void ResetSession(){Shown=false;if(Current!=null)Destroy(Current.gameObject);}
        void Start()
        {
            var canvas=HudController.Instance.GetComponentInParent<Canvas>().transform;
            var go=new GameObject("Phone",typeof(RectTransform),typeof(Image));go.transform.SetParent(canvas,false);root=go.GetComponent<RectTransform>();
            root.anchorMin=root.anchorMax=root.pivot=new Vector2(1,0);root.sizeDelta=new Vector2(380,260);root.anchoredPosition=new Vector2(-24,-280);
            go.GetComponent<Image>().color=new Color(.12f,.13f,.15f,.96f);go.GetComponent<Image>().raycastTarget=false;
            var edge=new GameObject("Pencil edge",typeof(RectTransform),typeof(SketchBorder));edge.transform.SetParent(go.transform,false);Stretch(edge.GetComponent<RectTransform>(),0);
            edge.GetComponent<SketchBorder>().raycastTarget=false;edge.GetComponent<SketchBorder>().color=new Color(.9f,.87f,.73f);
            // Screen: a lit panel with the header, then the message bubble.
            var screen=new GameObject("Screen",typeof(RectTransform),typeof(Image));screen.transform.SetParent(go.transform,false);Stretch(screen.GetComponent<RectTransform>(),10);
            screen.GetComponent<Image>().color=new Color(.78f,.86f,.9f);screen.GetComponent<Image>().raycastTarget=false;
            header=Label(screen.transform,new Vector2(0,1),new Vector2(0,-8),new Vector2(-16,40),26,FontStyle.Bold,new Color(.1f,.12f,.16f));
            var bubble=new GameObject("Bubble",typeof(RectTransform),typeof(Image));bubble.transform.SetParent(screen.transform,false);
            var br=bubble.GetComponent<RectTransform>();br.anchorMin=new Vector2(0,0);br.anchorMax=new Vector2(1,1);br.offsetMin=new Vector2(10,10);br.offsetMax=new Vector2(-10,-56);
            bubble.GetComponent<Image>().color=new Color(1f,.98f,.9f);bubble.GetComponent<Image>().raycastTarget=false;
            body=Label(bubble.transform,new Vector2(0,1),new Vector2(0,-6),new Vector2(-16,-12),25,FontStyle.Normal,new Color(.08f,.1f,.12f));
            body.rectTransform.anchorMin=Vector2.zero;body.rectTransform.anchorMax=Vector2.one;body.rectTransform.offsetMin=new Vector2(8,6);body.rectTransform.offsetMax=new Vector2(-8,-6);body.alignment=TextAnchor.UpperLeft;
            header.text=Header;body.text="";started=Time.time;
            if(buzz==null)buzz=TempAudio.Buzz;TempAudio.PlayAt(buzz,Camera.main!=null?Camera.main.transform.position:Vector3.zero,.7f);
        }
        void Update()
        {
            if(root==null)return;
            float t=Time.time-started;
            // Slides up, holds, slides away.
            float rise=Mathf.Clamp01(t/.35f)*Mathf.Clamp01((ShowSeconds-t)/.35f);
            root.anchoredPosition=new Vector2(-24+(t<1.2f?Mathf.Sin(t*70)*3:0),Mathf.Lerp(-280,120,Mathf.SmoothStep(0,1,rise)));
            int letters=Mathf.Clamp(Mathf.FloorToInt((t-.9f)*LettersPerSecond),0,Text.Length);
            Typed=Text.Substring(0,letters);body.text=letters>0?"<b>"+Sender+":</b> "+Typed:"";
            if(t>ShowSeconds)Destroy(gameObject);
        }
        void OnDestroy(){if(root!=null)Destroy(root.gameObject);if(Current==this)Current=null;}
        static Text Label(Transform parent,Vector2 anchor,Vector2 position,Vector2 size,int fontSize,FontStyle style,Color colour)
        {
            var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>();r.anchorMin=new Vector2(0,anchor.y);r.anchorMax=new Vector2(1,anchor.y);r.pivot=new Vector2(.5f,1);r.anchoredPosition=position;r.sizeDelta=size;
            var t=go.GetComponent<Text>();t.font=SchoolTypography.Font;t.fontSize=fontSize;t.fontStyle=style;t.color=colour;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.supportRichText=true;
            return t;
        }
        static void Stretch(RectTransform r,float inset){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(inset,inset);r.offsetMax=new Vector2(-inset,-inset);}
    }
}

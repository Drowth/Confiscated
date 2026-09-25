using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
namespace Confiscated
{
    public sealed class PeriodWorksheetUI : MonoBehaviour
    {
        public SchoolPeriodController period;
        public Sprite paper;
        public bool IsOpen {get;private set;}
        Canvas canvas;RectTransform panel;Text title,body,feedback;GameObject picture;Button[] choices;Button action;
        bool oldLook,oldInput;CursorLockMode oldCursor;bool oldVisible;
        FirstPersonController movement;PlayerInteractor player;
        static readonly Color Ink=new(.12f,.17f,.25f);
        void Build()
        {
            var go=new GameObject("English worksheet canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1100;
            var scale=go.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1600,1000);scale.matchWidthOrHeight=.5f;
            var shade=Rect("Shade",canvas.transform,Vector2.zero,new Vector2(4000,4000));shade.gameObject.AddComponent<Image>().color=new Color(.02f,.03f,.05f,.66f);
            panel=Rect("Pencil worksheet",canvas.transform,Vector2.zero,new Vector2(1140,760));SchoolPaperStyle.Apply(panel.gameObject,paper);
            Border(panel);
            title=Label("Title",new Vector2(0,300),new Vector2(1020,65),37);
            body=Label("Instructions",new Vector2(0,218),new Vector2(1020,100),24);
            picture=Rect("Book illustration",panel,new Vector2(-295,5),new Vector2(350,265)).gameObject;
            for(int i=0;i<3;i++)
            {
                var book=Rect("Library book "+i,picture.transform,new Vector2(i%2*13,55-i*55),new Vector2(265,43));book.localRotation=Quaternion.Euler(0,0,i==1?-2:1);
                var image=book.gameObject.AddComponent<Image>();image.sprite=paper;image.color=i==0?new Color(.45f,.23f,.23f):i==1?new Color(.29f,.43f,.36f):new Color(.34f,.39f,.52f);
                Border(book);
                var pages=Rect("Paper edges",book,new Vector2(7,-7),new Vector2(228,16));var fill=pages.gameObject.AddComponent<Image>();fill.sprite=paper;fill.color=new Color(.93f,.89f,.76f);
            }
            choices=new Button[3];for(int i=0;i<3;i++){int n=i;choices[i]=Button("Sentence "+i,new Vector2(224,85-i*94),new Vector2(550,76),()=>period.ChooseSentence(n));}
            action=Button("Action",new Vector2(0,-50),new Vector2(800,90),()=>period.Volunteer());
            feedback=Label("Feedback",new Vector2(0,-244),new Vector2(1020,75),23);
            Button("Close",new Vector2(420,-323),new Vector2(210,48),Close).GetComponentInChildren<Text>().text="Close [Esc]";
            go.SetActive(false);
        }
        public void Open()
        {
            if(IsOpen||!period.CanUseSeat)return;GameManager.Instance.lockerUI?.Close();
            if(canvas==null)Build();if(EventSystem.current==null&&FindFirstObjectByType<EventSystem>()==null){var e=new GameObject("School UI events",typeof(EventSystem));e.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();}
            player=period.Player;movement=player.GetComponent<FirstPersonController>();oldLook=movement.LookLocked;oldInput=player.InputLocked;oldCursor=Cursor.lockState;oldVisible=Cursor.visible;
            movement.LookLocked=true;player.InputLocked=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;IsOpen=true;canvas.gameObject.SetActive(true);Refresh();
        }
        public void Close()
        {
            if(!IsOpen)return;IsOpen=false;canvas.gameObject.SetActive(false);movement.LookLocked=oldLook;player.InputLocked=oldInput;player.SuppressActionsThisFrame();Cursor.lockState=oldCursor;Cursor.visible=oldVisible;
        }
        void Update(){if(ComicDialogue.IsActive)return;if(IsOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)Close();}
        public void Feedback(string value){if(ComicDialogue.TrySpeak(value)){feedback.text="";return;}feedback.text=value;}
        public void Refresh()
        {
            var phase=period.Current;bool worksheet=phase==SchoolPeriodController.Phase.Worksheet||phase==SchoolPeriodController.Phase.Review;
            picture.SetActive(worksheet);foreach(var b in choices)b.gameObject.SetActive(worksheet);action.gameObject.SetActive(!worksheet);feedback.text="";
            if(worksheet)
            {
                bool review=phase==SchoolPeriodController.Phase.Review;
                title.text=review?"THE SCHOOL NEWSLETTER - YOUR CAPTION":"WHAT'S IN THE PICTURE?";
                body.text=review?"The school newsletter":"";
                string[] text=review?new[]{"Three library books, in three different colours.","The shelf contains only red books.","There is one book on the shelf."}:new[]{"A football","A pencil","Some books"};
                for(int i=0;i<3;i++)choices[i].GetComponentInChildren<Text>().text=text[i];
            }
            else if(phase==SchoolPeriodController.Phase.Volunteer)
            {
                title.text="A REASON TO LEAVE CLASS";body.text="Newsletters for the school office";
                action.GetComponentInChildren<Text>().text="Raise your hand - I'll take them, sir.";
            }
            if(phase==SchoolPeriodController.Phase.Volunteer)action.interactable=true;
        }
        RectTransform Rect(string name,Transform parent,Vector2 p,Vector2 size){var g=new GameObject(name,typeof(RectTransform));var r=g.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=p;r.sizeDelta=size;return r;}
        Text Label(string name,Vector2 p,Vector2 size,int fontSize)
        {
            var r=Rect(name,panel,p,size);var t=r.gameObject.AddComponent<Text>();t.font=SchoolTypography.Font;t.fontSize=fontSize;t.color=new Color(.035f,.035f,.035f);t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;
        }
        Button Button(string name,Vector2 p,Vector2 size,UnityEngine.Events.UnityAction click)
        {
            var r=Rect(name,panel,p,size);var image=SchoolPaperStyle.Apply(r.gameObject,paper,true);
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;b.onClick.AddListener(click);
            Border(r);
            var text=Label(name+" text",p,size-new Vector2(28,8),24);text.transform.SetParent(r,true);text.rectTransform.anchoredPosition=Vector2.zero;return b;
        }
        void Border(RectTransform parent)
        {
            var r=Rect("Drawn edge",parent,Vector2.zero,parent.sizeDelta);
            var edge=r.gameObject.AddComponent<SketchBorder>();edge.color=Ink;edge.raycastTarget=false;
        }
        void OnDisable(){Close();}
    }
}

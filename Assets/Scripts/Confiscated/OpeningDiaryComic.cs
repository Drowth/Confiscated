using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Confiscated
{
    /// <summary>A short illustrated diary entry before the live classroom phone incident.</summary>
    public sealed class OpeningDiaryComic : MonoBehaviour
    {
        static readonly string[] Lines =
        {
            "First, my yo-yo. Apparently it was 'a distraction'.",
            "Then my game. I wasn't even playing it.",
            "The skateboard went next. The wheels hadn't touched the floor.",
            "Even my robot. The caretaker says I can have them back at the end of term.",
            "That's four things. I'm getting them back before I go home."
        };
        static readonly float[] Seconds = { 4.8f, 4.8f, 4.8f, 6f, 5f };
        static readonly Color Paper = new Color(.95f,.91f,.77f), Ink = new Color(.07f,.09f,.13f);

        public static bool Show(FirstPersonController movement, PlayerInteractor interactor, Action finished)
        {
            var art = Resources.Load<Texture2D>("Art/OpeningDiaryComic");
            if (art == null) { Debug.LogError("Missing opening diary comic artwork."); return false; }
            var go = new GameObject("Opening diary cutscene", typeof(OpeningDiaryComic));
            go.GetComponent<OpeningDiaryComic>().Begin(art, movement, interactor, finished);
            return true;
        }

        FirstPersonController movement;
        PlayerInteractor interactor;
        Action finished;
        CanvasGroup group;
        RectTransform page, frame;
        Image[] covers;
        Text caption, progress;
        float oldTime, startedAt, beatAt, skipHeld;
        bool oldMove, oldLook, oldInput, oldCursorVisible, ending;
        CursorLockMode oldCursorLock;
        int beat;

        void Begin(Texture2D art, FirstPersonController mover, PlayerInteractor actor, Action callback)
        {
            movement=mover;interactor=actor;finished=callback;
            oldTime=Time.timeScale;Time.timeScale=0;
            if(movement!=null){oldMove=movement.MovementLocked;oldLook=movement.LookLocked;movement.MovementLocked=movement.LookLocked=true;}
            if(interactor!=null){oldInput=interactor.InputLocked;interactor.InputLocked=true;interactor.SuppressActionsThisFrame();}
            oldCursorLock=Cursor.lockState;oldCursorVisible=Cursor.visible;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=false;
            Build(art);beat=0;startedAt=beatAt=Time.unscaledTime;SetBeat();
        }

        void Build(Texture2D art)
        {
            var root=new GameObject("Diary canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(CanvasGroup));
            root.transform.SetParent(transform,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30001;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=1f;
            group=root.GetComponent<CanvasGroup>();group.blocksRaycasts=true;group.alpha=0;
            var background=ImageRect("Ink surround",root.transform,Vector2.zero,new Vector2(1920,1080),new Color(.025f,.035f,.045f,1));
            background.rectTransform.anchorMin=Vector2.zero;background.rectTransform.anchorMax=Vector2.one;
            background.rectTransform.offsetMin=background.rectTransform.offsetMax=Vector2.zero;
            background.raycastTarget=true;
            Label("DEAR DIARY...",root.transform,new Vector2(0,497),new Vector2(1300,65),50,Paper,TextAnchor.MiddleLeft);
            Label("MASTER SMITH  /  YEAR 6",root.transform,new Vector2(0,458),new Vector2(1300,35),23,new Color(.74f,.7f,.59f),TextAnchor.MiddleRight);
            page=Rect("Four confiscations",root.transform,new Vector2(0,22),new Vector2(1320,812));
            var sheet=page.gameObject.AddComponent<RawImage>();sheet.texture=art;sheet.raycastTarget=false;
            covers=new Image[4];
            for(int i=0;i<4;i++)
            {
                float x=(i%2==0?-1:1)*330f,y=(i<2?1:-1)*203f;
                covers[i]=ImageRect("Unrevealed panel "+(i+1),page,new Vector2(x,y),new Vector2(650,396),new Color(.035f,.045f,.06f,.94f));
                covers[i].raycastTarget=false;
            }
            frame=Rect("Current panel ink frame",page,Vector2.zero,new Vector2(650,396));
            var border=frame.gameObject.AddComponent<SketchBorder>();border.color=new Color(.84f,.47f,.19f);border.raycastTarget=false;
            var note=ImageRect("Diary sentence",root.transform,new Vector2(0,-445),new Vector2(1370,122),Paper);
            note.raycastTarget=false;
            var noteBorder=Rect("Pencil edge",note.transform,Vector2.zero,new Vector2(1370,122)).gameObject.AddComponent<SketchBorder>();
            noteBorder.color=Ink;noteBorder.raycastTarget=false;
            caption=Label("",note.transform,Vector2.zero,new Vector2(1260,90),34,Ink,TextAnchor.MiddleLeft);
            progress=Label("",root.transform,new Vector2(-545,-518),new Vector2(300,28),20,Paper,TextAnchor.MiddleLeft);
            Label("SPACE / CLICK: NEXT     HOLD ESC: SKIP",root.transform,new Vector2(355,-518),new Vector2(670,28),20,Paper,TextAnchor.MiddleRight);
        }

        void Update()
        {
            if(ending)return;
            float now=Time.unscaledTime;
            var kb=Keyboard.current;var mouse=Mouse.current;var pad=Gamepad.current;
            bool skip=(kb!=null&&kb.escapeKey.isPressed)||(pad!=null&&pad.buttonEast.isPressed);
            skipHeld=skip?skipHeld+Time.unscaledDeltaTime:0;
            if(skipHeld>.65f){Finish();return;}
            bool next=(kb!=null&&(kb.spaceKey.wasPressedThisFrame||kb.enterKey.wasPressedThisFrame))||
                (mouse!=null&&mouse.leftButton.wasPressedThisFrame)||(pad!=null&&pad.buttonSouth.wasPressedThisFrame);
            if(now-beatAt>Seconds[beat]||(next&&now-beatAt>.35f))
            {
                beat++;
                if(beat==Lines.Length){Finish();return;}
                beatAt=now;SetBeat();
            }
            group.alpha=Mathf.Clamp01((now-startedAt)/.35f);
            for(int i=0;i<4;i++)
            {
                var color=covers[i].color;
                color.a=Mathf.MoveTowards(color.a,i<=beat?.0f:.94f,Time.unscaledDeltaTime*3f);
                covers[i].color=color;
            }
            if(beat<4)
            {
                float x=(beat%2==0?-1:1)*330f,y=(beat<2?1:-1)*203f;
                frame.anchoredPosition=new Vector2(x,y);
                float pulse=1+.035f*Mathf.Exp(-(now-beatAt)*5);
                frame.localScale=new Vector3(pulse,pulse,1);
            }
        }

        void SetBeat()
        {
            caption.text=Lines[beat];progress.text=beat<4?"PAGE 1  /  "+(beat+1)+" OF 4":"PAGE 1  /  THE PLAN";
            frame.gameObject.SetActive(beat<4);
        }

        void Finish()
        {
            if(ending)return;ending=true;
            Time.timeScale=oldTime;
            if(movement!=null){movement.MovementLocked=oldMove;movement.LookLocked=oldLook;}
            if(interactor!=null){interactor.InputLocked=oldInput;interactor.SuppressActionsThisFrame();}
            Cursor.lockState=oldCursorLock;Cursor.visible=oldCursorVisible;
            var callback=finished;finished=null;Destroy(gameObject);callback?.Invoke();
        }

        static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
        }
        static Image ImageRect(string name,Transform parent,Vector2 position,Vector2 size,Color color)
        {
            var image=Rect(name,parent,position,size).gameObject.AddComponent<Image>();image.color=color;return image;
        }
        static Text Label(string value,Transform parent,Vector2 position,Vector2 size,int fontSize,Color color,TextAnchor alignment)
        {
            var label=Rect("Ink text",parent,position,size).gameObject.AddComponent<Text>();
            label.font=SchoolTypography.Font;label.fontSize=fontSize;label.color=color;label.alignment=alignment;
            label.text=value;label.raycastTarget=false;label.resizeTextForBestFit=true;label.resizeTextMinSize=fontSize-5;
            label.resizeTextMaxSize=fontSize;return label;
        }
    }
}

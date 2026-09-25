using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
namespace Confiscated
{
    /// <summary>One of three prewritten boards. The same chalk surface persists in world and close-up.</summary>
    public class DetentionBlackboard : Interactable
    {
        public DetentionController detention;
        public Font chalkFont;
        public Sprite woodPaper,boardPaper;
        public int boardNumber=1;
        public bool IsOpen {get;private set;}
        public bool Erased {get;private set;}
        public ErasableChalk Ink {get;private set;}
        public ChalkDust Dust {get;private set;}
        public RectTransform Surface=>surface;
        public const string Introduction="Clear all three blackboards, Smith. Then clap the rubbers clean at the cleaning station. After that, you may leave.";
        public string TeacherInstruction=>detention!=null&&detention.BoardsCleared==3?"All three boards are clear. Clap the rubbers clean at the cleaning station.":Introduction;
        Canvas modal,world;
        Canvas hudCanvas;
        bool hudWasEnabled;
        RectTransform surface,eraser;
        Text instruction,progress;
        PlayerInteractor player;
        FirstPersonController movement;
        bool oldMove,oldLook,oldInput,oldCursor,rubbing;
        CursorLockMode oldLock;
        Vector2 lastRub;
        AudioSource friction;
        AudioClip frictionClip;
        static readonly Color Chalk=new(.89f,.88f,.76f),Navy=new(.10f,.15f,.22f);
        void Awake(){if(surface==null)Build();}
        void OnDisable(){Close();}
        void OnDestroy(){Close();}
        public override bool CanInteract(PlayerInteractor who)=>detention!=null&&detention.Active&&!Erased;
        public override string GetPrompt(PlayerInteractor who)=>Erased?"Blackboard "+boardNumber+" is clean.":CanInteract(who)?"F: erase blackboard "+boardNumber+" / 3":"Detention blackboard "+boardNumber;
        public override void Interact(PlayerInteractor who){if(CanInteract(who))Open(who);}
        public void BeginSentence()
        {
            if(surface==null)Build();Close();Erased=false;
            Ink.gameObject.SetActive(true);Ink.ResetInk();Dust.Clear();Refresh();
        }
        public void Open(PlayerInteractor who)
        {
            if(IsOpen||!CanInteract(who))return;
            if(EventSystem.current==null&&FindFirstObjectByType<EventSystem>()==null){var e=new GameObject("Blackboard EventSystem",typeof(EventSystem));e.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();}
            player=who;movement=who.GetComponent<FirstPersonController>();oldMove=movement.MovementLocked;oldLook=movement.LookLocked;oldInput=who.InputLocked;
            oldCursor=Cursor.visible;oldLock=Cursor.lockState;movement.MovementLocked=true;movement.LookLocked=true;who.InputLocked=true;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;IsOpen=true;modal.gameObject.SetActive(true);
            hudCanvas=HudController.Instance!=null?HudController.Instance.GetComponentInParent<Canvas>():null;
            if(hudCanvas!=null){hudWasEnabled=hudCanvas.enabled;hudCanvas.enabled=false;}
            surface.SetParent(modal.transform.Find("Sheet"),false);surface.anchoredPosition=new Vector2(0,30);surface.localScale=Vector3.one;
            Refresh();
        }
        public void Close()
        {
            if(!IsOpen)return;IsOpen=false;rubbing=false;friction.Stop();eraser.gameObject.SetActive(false);
            surface.SetParent(world.transform,false);surface.anchoredPosition=Vector2.zero;surface.localScale=Vector3.one;
            modal.gameObject.SetActive(false);
            if(hudCanvas!=null)hudCanvas.enabled=hudWasEnabled;
            if(movement!=null){movement.MovementLocked=oldMove;movement.LookLocked=oldLook;}
            if(player!=null){player.InputLocked=oldInput;player.SuppressActionsThisFrame();}
            Cursor.lockState=oldLock;Cursor.visible=oldCursor;
        }
        void Update()
        {
            if(ComicDialogue.IsActive){if(friction!=null)friction.Stop();rubbing=false;return;}
            if(!IsOpen)return;
            if(!detention.Active){Close();return;}
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){Close();return;}
            if(Erased)return;
            var mouse=Mouse.current;if(mouse==null){friction.Stop();rubbing=false;return;}
            RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,mouse.position.ReadValue(),null,out var point);
            bool inside=surface.rect.Contains(point);eraser.gameObject.SetActive(inside);
            if(inside)
            {
                eraser.anchoredPosition=point;eraser.localRotation=Quaternion.Euler(0,0,Mathf.Clamp((point.x-lastRub.x)*-.25f,-12,12));
                if(mouse.leftButton.isPressed)
                {
                    var start=rubbing?lastRub:point;Rub(start,point);
                    if(!IsOpen||Erased)return;
                    if(Vector2.Distance(start,point)>.5f){if(!friction.isPlaying)friction.Play();friction.volume=.5f;}
                    else friction.Stop();
                    rubbing=true;lastRub=point;return;
                }
            }
            rubbing=false;lastRub=point;friction.Stop();
        }
        public void Rub(Vector2 from,Vector2 to)
        {
            if(!IsOpen||Erased||!detention.Active)return;
            if(!surface.rect.Contains(to))return;
            float removed=Ink.Rub(from,to,46);
            if(removed>0){Dust.Puff(Vector2.Lerp(from,to,.35f),6);Dust.Puff(to,Mathf.Clamp(Mathf.CeilToInt(removed*.3f),6,20));}
            if(Ink.Cleared>=.995f)
            {
                Erased=true;Ink.ClearAll();eraser.gameObject.SetActive(false);friction.Stop();
                Close();detention.BoardCleared();
            }
            Refresh();
        }
        void Refresh()
        {
            if(instruction==null)return;
            instruction.text="BLACKBOARD "+boardNumber+" / 3";
            progress.text="Hold the left mouse button and sweep the rubber across the chalk\n"+Mathf.RoundToInt(Ink.Cleared*100)+"% clear  •  "+(detention!=null?detention.BoardsCleared:0)+" / 3 boards finished";
        }
        void Build()
        {
            var worldObject=new GameObject("World chalk surface",typeof(RectTransform),typeof(Canvas));worldObject.transform.SetParent(transform,false);
            world=worldObject.GetComponent<Canvas>();world.renderMode=RenderMode.WorldSpace;
            worldObject.transform.localPosition=new Vector3(0,0,-.022f);worldObject.transform.localScale=new Vector3(2.83f/1200,.86f/560,.002f);
            ((RectTransform)world.transform).sizeDelta=new Vector2(1200,560);
            surface=Rect("Blackboard surface",world.transform,Vector2.zero,new Vector2(1200,560));
            var bg=surface.gameObject.AddComponent<Image>();bg.sprite=boardPaper;bg.color=new Color(.075f,.14f,.12f);bg.raycastTarget=false;
            var ink=Rect("Original chalk writing",surface,Vector2.zero,surface.sizeDelta);Ink=ink.gameObject.AddComponent<ErasableChalk>();Ink.font=chalkFont;Ink.color=Chalk;Ink.raycastTarget=false;Ink.ResetInk();
            var dust=Rect("Chalk dust",surface,Vector2.zero,surface.sizeDelta);Dust=dust.gameObject.AddComponent<ChalkDust>();Dust.raycastTarget=false;
            var m=new GameObject("Blackboard close-up",typeof(RectTransform),typeof(Canvas));m.transform.SetParent(transform,false);modal=m.GetComponent<Canvas>();modal.renderMode=RenderMode.ScreenSpaceOverlay;modal.sortingOrder=400;m.AddComponent<GraphicRaycaster>();
            var scaler=m.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1536,1024);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var dim=Rect("Shade",m.transform,Vector2.zero,Vector2.zero);dim.anchorMin=Vector2.zero;dim.anchorMax=Vector2.one;dim.offsetMin=dim.offsetMax=Vector2.zero;dim.gameObject.AddComponent<Image>().color=new Color(.02f,.025f,.04f,.92f);
            var sheet=Rect("Sheet",m.transform,Vector2.zero,new Vector2(1536,1024));
            Label("DETENTION — MASTER SMITH",sheet,new Vector2(0,443),new Vector2(1300,60),34,Chalk);
            instruction=Label("",sheet,new Vector2(0,371),new Vector2(1320,84),25,Chalk);instruction.fontStyle=FontStyle.Normal;
            var frame=Rect("Pencil wood frame",sheet,new Vector2(0,30),new Vector2(1250,610));frame.gameObject.AddComponent<Image>().sprite=woodPaper;
            var edge=Rect("Drawn frame edges",frame,Vector2.zero,frame.sizeDelta);var border=edge.gameObject.AddComponent<SketchBorder>();border.color=Navy;border.raycastTarget=false;
            progress=Label("",sheet,new Vector2(0,-335),new Vector2(1280,82),24,Chalk);progress.fontStyle=FontStyle.Normal;
            var close=Rect("Step away",sheet,new Vector2(0,-443),new Vector2(300,54));var buttonImage=close.gameObject.AddComponent<Image>();buttonImage.sprite=boardPaper;
            var button=close.gameObject.AddComponent<Button>();button.targetGraphic=buttonImage;button.onClick.AddListener(Close);Label("Step away  [Esc]",close,Vector2.zero,new Vector2(280,50),24,Navy);
            eraser=Rect("Board rubber",surface,Vector2.zero,new Vector2(98,54));var felt=eraser.gameObject.AddComponent<Image>();felt.sprite=boardPaper;felt.color=new Color(.20f,.23f,.24f);felt.raycastTarget=false;
            var wood=Rect("Wooden grip",eraser,new Vector2(0,9),new Vector2(98,39));var grip=wood.gameObject.AddComponent<Image>();grip.sprite=woodPaper;grip.raycastTarget=false;
            var rim=Rect("Rubber outline",eraser,Vector2.zero,eraser.sizeDelta);var outline=rim.gameObject.AddComponent<SketchBorder>();outline.color=Navy;outline.raycastTarget=false;eraser.gameObject.SetActive(false);
            friction=gameObject.AddComponent<AudioSource>();friction.playOnAwake=false;friction.loop=true;friction.spatialBlend=0;
            frictionClip=Resources.Load<AudioClip>("Audio/ChalkboardRubber");friction.clip=frictionClip;
            m.SetActive(false);Refresh();
        }
        static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size){var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;return r;}
        static Text Label(string value,Transform parent,Vector2 pos,Vector2 size,int fontSize,Color tint,Font font=null){var r=Rect(value==""?"Text":value,parent,pos,size);var t=r.gameObject.AddComponent<Text>();t.text=value;t.font=font!=null?font:SchoolTypography.Font;t.fontSize=fontSize;t.color=tint;t.alignment=TextAnchor.MiddleCenter;t.fontStyle=FontStyle.Bold;t.raycastTarget=false;return t;}
    }
}


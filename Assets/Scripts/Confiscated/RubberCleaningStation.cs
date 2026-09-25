using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
namespace Confiscated
{
    public sealed class RubberCleaningStation : Interactable
    {
        public DetentionController detention;
        public Sprite woodPaper,boardPaper;
        public bool IsOpen {get;private set;}
        public float Dirt {get;private set;}=1;
        public bool Clean=>Dirt<=0;
        public int Claps {get;private set;}
        public ChalkDust Dust {get;private set;}
        public RectTransform Surface=>sheet;
        public RectTransform LeftRubber=>left;
        public RectTransform RightRubber=>right;
        Canvas modal,hud;
        RectTransform sheet,left,right,meter;
        RubberDirt leftDirt,rightDirt;
        Text progress,feedback;
        PlayerInteractor player;
        FirstPersonController movement;
        bool oldMove,oldLook,oldInput,oldCursor,hudEnabled,dragging,dragLeft,armed;
        CursorLockMode oldLock;
        Vector2 offset,lastPoint;
        float kick,flash,finishAt;
        AudioSource sound;
        AudioClip clapSound,cleanSound;
        static readonly Color Cream=new(.91f,.9f,.79f),Ink=new(.08f,.12f,.16f);
        void Awake(){if(sheet==null)Build();}
        void OnDisable(){Close();}
        void OnDestroy(){Close();if(clapSound!=null)Destroy(clapSound);if(cleanSound!=null)Destroy(cleanSound);}
        public override bool CanInteract(PlayerInteractor p)=>detention!=null&&detention.Active&&detention.BoardsCleared==3&&!Clean;
        public override string GetPrompt(PlayerInteractor p)=>Clean?"The rubbers are clean.":detention!=null&&detention.Active?detention.BoardsCleared==3?"F: clap the dusty board rubbers clean":"Clear all three blackboards first ("+detention.BoardsCleared+" / 3).":"Board-rubber cleaning station";
        public override void Interact(PlayerInteractor p){if(CanInteract(p))Open(p);}
        public void ResetCleaning()
        {
            if(sheet==null)Build();Close();Dirt=1;Claps=0;finishAt=0;kick=flash=0;dragging=armed=false;
            left.anchoredPosition=new Vector2(-230,45);right.anchoredPosition=new Vector2(230,45);
            Dust.Clear();Refresh();feedback.text="Pull apart. Bring the felt faces together.";
        }
        public void Open(PlayerInteractor p)
        {
            if(IsOpen||!CanInteract(p))return;
            foreach(var board in detention.blackboards)board.Close();
            if(EventSystem.current==null&&FindFirstObjectByType<EventSystem>()==null)new GameObject("Cleaning EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            player=p;movement=p.GetComponent<FirstPersonController>();oldMove=movement.MovementLocked;oldLook=movement.LookLocked;oldInput=p.InputLocked;oldCursor=Cursor.visible;oldLock=Cursor.lockState;
            movement.MovementLocked=movement.LookLocked=true;p.InputLocked=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            hud=HudController.Instance!=null?HudController.Instance.GetComponentInParent<Canvas>():null;if(hud!=null){hudEnabled=hud.enabled;hud.enabled=false;}
            IsOpen=true;modal.gameObject.SetActive(true);Refresh();
        }
        public void Close()
        {
            if(!IsOpen)return;IsOpen=false;dragging=armed=false;modal.gameObject.SetActive(false);
            if(hud!=null)hud.enabled=hudEnabled;
            if(movement!=null){movement.MovementLocked=oldMove;movement.LookLocked=oldLook;}
            if(player!=null){player.InputLocked=oldInput;player.SuppressActionsThisFrame();}
            Cursor.lockState=oldLock;Cursor.visible=oldCursor;
        }
        public void BeginDrag(bool useLeft,Vector2 pointer)
        {
            if(!IsOpen||Clean||detention.BoardsCleared!=3)return;
            dragLeft=useLeft;var rubber=useLeft?left:right;
            if(!new Rect(rubber.anchoredPosition-new Vector2(90,65),new Vector2(180,130)).Contains(pointer))return;
            dragging=true;offset=rubber.anchoredPosition-pointer;lastPoint=rubber.anchoredPosition;
            armed=Vector2.Distance(left.anchoredPosition,right.anchoredPosition)>300;
        }
        public void DragTo(Vector2 pointer,float seconds)
        {
            if(!IsOpen||!dragging||Clean||!detention.Active)return;
            var rubber=dragLeft?left:right;var other=dragLeft?right:left;
            Vector2 p=pointer+offset;p.x=Mathf.Clamp(p.x,-490,490);p.y=Mathf.Clamp(p.y,-110,200);
            Vector2 delta=p-lastPoint;float speed=delta.magnitude/Mathf.Max(.001f,seconds);
            // Swept contact avoids skipping a clap during a fast mouse movement.
            float t=delta.sqrMagnitude>.001f?Mathf.Clamp01(Vector2.Dot(other.anchoredPosition-lastPoint,delta)/delta.sqrMagnitude):0;
            float contact=Vector2.Distance(lastPoint+delta*t,other.anchoredPosition);
            rubber.anchoredPosition=p;rubber.localRotation=Quaternion.Euler(0,0,Mathf.Clamp(-delta.x*.12f,-14,14));
            if(Vector2.Distance(p,other.anchoredPosition)>300)armed=true;
            if(armed&&contact<155&&speed>250){Impact(Mathf.InverseLerp(250,1700,speed));dragging=armed=false;}
            else if(contact<155){feedback.text="A little more swing - pull apart, then clap.";}
            lastPoint=p;
        }
        public void EndDrag(){dragging=false;}
        void Impact(float strength)
        {
            Claps++;Dirt=Mathf.Max(0,Dirt-Mathf.Lerp(.065f,.12f,strength));
            if(Dirt<.025f)Dirt=0;
            var impact=(left.anchoredPosition+right.anchoredPosition)*.5f;
            Dust.Burst(impact,strength);kick=7+strength*10;flash=1;
            left.anchoredPosition=new Vector2(-180,45);right.anchoredPosition=new Vector2(180,45);
            left.localRotation=Quaternion.Euler(0,0,-12);right.localRotation=Quaternion.Euler(0,0,12);
            sound.pitch=Random.Range(.91f,1.1f);sound.PlayOneShot(clapSound,.5f+strength*.35f);
            feedback.text=Clean?"SPOTLESS! You're free to leave.":strength>.65f?"CLAP! A good cloud of chalk.":"Thump! Pull apart for another clap.";
            if(Clean){finishAt=Time.unscaledTime+.85f;sound.pitch=1;sound.PlayOneShot(cleanSound,.55f);Dust.Burst(Vector2.zero,1);}
            Refresh();
        }
        void Update()
        {
            if(Clean&&detention!=null&&detention.Active&&finishAt>0&&Time.unscaledTime>=finishAt){detention.CompleteCleaning();return;}
            if(!IsOpen)return;if(!detention.Active){Close();return;}if(ComicDialogue.IsActive)return;
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){Close();return;}
            float dt=Time.unscaledDeltaTime;kick=Mathf.MoveTowards(kick,0,dt*45);flash=Mathf.MoveTowards(flash,0,dt*3);
            sheet.anchoredPosition=new Vector2(Mathf.Sin(Time.unscaledTime*83)*kick,Mathf.Cos(Time.unscaledTime*69)*kick*.5f);
            if(!dragging){left.localRotation=Quaternion.Slerp(left.localRotation,Quaternion.identity,dt*12);right.localRotation=Quaternion.Slerp(right.localRotation,Quaternion.identity,dt*12);}
            var mouse=Mouse.current;if(mouse==null||Clean)return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(sheet,mouse.position.ReadValue(),null,out var point);
            if(mouse.leftButton.wasPressedThisFrame)
            {
                bool nearLeft=Vector2.Distance(point,left.anchoredPosition)<Vector2.Distance(point,right.anchoredPosition);BeginDrag(nearLeft,point);
            }
            if(mouse.leftButton.isPressed)DragTo(point,dt);
            if(mouse.leftButton.wasReleasedThisFrame)EndDrag();
        }
        void Refresh()
        {
            if(progress==null)return;leftDirt.SetDirt(Dirt);rightDirt.SetDirt(Dirt);
            progress.text="RUBBERS CLEAN: "+Mathf.RoundToInt((1-Dirt)*100)+"%";
            meter.localScale=new Vector3(Mathf.Max(.001f,1-Dirt),1,1);
        }
        void Build()
        {
            var m=new GameObject("Rubber cleaning close-up",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));m.transform.SetParent(transform,false);modal=m.GetComponent<Canvas>();modal.renderMode=RenderMode.ScreenSpaceOverlay;modal.sortingOrder=410;
            var scaler=m.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1536,1024);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var shade=Rect("Shade",m.transform,Vector2.zero,Vector2.zero);shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.offsetMin=shade.offsetMax=Vector2.zero;shade.gameObject.AddComponent<Image>().color=new Color(.025f,.045f,.045f,.97f);
            sheet=Rect("Cleaning tray",m.transform,Vector2.zero,new Vector2(1536,1024));
            Label("THE LAST BIT OF DETENTION",sheet,new Vector2(0,410),30);
            Label("CLAP THE RUBBERS CLEAN",sheet,new Vector2(0,345),43);
            Label("Three boards cleared. Now knock the chalk out of the felt.",sheet,new Vector2(0,280),24);
            var tray=Panel("Dust tray",sheet,new Vector2(0,10),new Vector2(1150,490),new Color(.12f,.22f,.20f),boardPaper);var trayRim=Rect("Tray outline",tray,Vector2.zero,tray.sizeDelta);var border=trayRim.gameObject.AddComponent<SketchBorder>();border.color=Cream;border.raycastTarget=false;
            left=Rubber("Left rubber",new Vector2(-230,45),out leftDirt);right=Rubber("Right rubber",new Vector2(230,45),out rightDirt);
            var dust=Rect("Clap dust particles",sheet,Vector2.zero,new Vector2(1400,780));Dust=dust.gameObject.AddComponent<ChalkDust>();Dust.raycastTarget=false;
            feedback=Label("Pull apart. Bring the felt faces together.",sheet,new Vector2(0,-185),27);
            Label("Drag either rubber into the other with the left mouse button.\nRelease, pull apart and clap again. A quicker swing shakes out more chalk.",sheet,new Vector2(0,-285),23);
            var track=Panel("Clean meter track",sheet,new Vector2(0,-365),new Vector2(530,18),new Color(.2f,.28f,.24f),boardPaper);
            meter=Panel("Clean meter",track,Vector2.zero,new Vector2(530,18),new Color(.78f,.71f,.38f),boardPaper);meter.pivot=new Vector2(0,.5f);meter.anchoredPosition=new Vector2(-265,0);
            progress=Label("",sheet,new Vector2(0,-403),22);
            var close=Panel("Step away",sheet,new Vector2(0,-467),new Vector2(260,46),Cream,boardPaper);close.GetComponent<Image>().raycastTarget=true;var button=close.gameObject.AddComponent<Button>();button.targetGraphic=close.GetComponent<Image>();button.onClick.AddListener(Close);var caption=Label("Step away  [Esc]",close,Vector2.zero,22);caption.color=Ink;
            sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=0;
            clapSound=MakeSound(false);cleanSound=MakeSound(true);Refresh();m.SetActive(false);
        }
        RectTransform Rubber(string name,Vector2 pos,out RubberDirt dirt)
        {
            var r=Rect(name,sheet,pos,new Vector2(180,130));
            Panel("Wooden grip",r,new Vector2(0,14),new Vector2(184,124),Color.white,woodPaper);
            Panel("Felt face",r,new Vector2(0,-8),new Vector2(164,108),new Color(.18f,.22f,.23f),boardPaper);
            var grime=Rect("Chalk-caked felt",r,new Vector2(0,-8),new Vector2(164,108));dirt=grime.gameObject.AddComponent<RubberDirt>();dirt.raycastTarget=false;
            var rim=r.gameObject.AddComponent<SketchBorder>();rim.color=Ink;rim.raycastTarget=false;return r;
        }
        static AudioClip MakeSound(bool chime)
        {
            const int rate=22050;float[] samples=new float[chime?11025:5512];var random=new System.Random(804);float low=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;low=Mathf.Lerp(low,(float)random.NextDouble()*2-1,.55f);
                samples[i]=chime?(Mathf.Sin(t*880*2*Mathf.PI)+Mathf.Sin(t*1320*2*Mathf.PI)*.3f)*Mathf.Exp(-t*9)*.22f:(low*.6f*Mathf.Exp(-t*30)+Mathf.Sin(t*165*2*Mathf.PI)*Mathf.Exp(-t*45)*.4f);
            }
            var clip=AudioClip.Create(chime?"Rubbers spotless":"Felt clap and chalk puff",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size){var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;return r;}
        static RectTransform Panel(string name,Transform parent,Vector2 pos,Vector2 size,Color tint,Sprite sprite){var r=Rect(name,parent,pos,size);var image=r.gameObject.AddComponent<Image>();image.color=tint;image.sprite=sprite;image.raycastTarget=false;return r;}
        static Text Label(string text,Transform parent,Vector2 pos,int size){var r=Rect("Label",parent,pos,new Vector2(1300,90));var t=r.gameObject.AddComponent<Text>();t.font=SchoolTypography.Font;t.text=text;t.fontSize=size;t.color=Cream;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;}
    }
}




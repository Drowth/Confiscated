using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Confiscated
{
    /// <summary>Live school establishing shots followed by a window-to-classroom camera entrance.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolTitleMenu : MonoBehaviour
    {
        public Sprite titleLogo;
        public AudioClip titleMusic;
        public OfficeDoor classroomDoor;
        public Transform window;
        public float shotSeconds=12f;
        public float entranceSeconds=4f;
        [Tooltip("A staff character prefab (e.g. P_Caretaker), spawned as a cosmetic double crossing the entrance lobby, visible through the glazing during the opening exterior shot.")]
        public GameObject backgroundWalkerPrefab;
        GameObject backgroundWalker;
        public static SchoolTitleMenu Instance {get;private set;}
        public static bool IsActive=>Instance!=null&&Instance.active;
        public bool IsStarting=>starting;
        public int ShotIndex {get;private set;}
        public float SequenceTime=>Time.unscaledTime-startTime;
        public Camera View=>view;
        readonly List<Canvas> hidden=new();
        GameManager game;
        PlayerInteractor player;
        FirstPersonController movement;
        Camera view;
        Canvas canvas;
        CanvasGroup menuGroup;
        Image fade;
        Text caption,skip;
        Button startButton;
        AudioSource musicSource;
        bool active,starting,oldMove,oldLook,oldInput,oldHold,oldDoorEnabled;
        float startTime,oldFov,oldTime;
        Vector3 oldPosition;
        Quaternion oldRotation,doorClosed;
        Vector3 windowPoint,doorPoint,seatPoint;
        static readonly Color Cream=new(.97f,.94f,.83f),Ink=new(.055f,.075f,.105f);

        public void Show(GameManager owner)
        {
            if(active)return;Instance=this;game=owner;player=FindFirstObjectByType<PlayerInteractor>();
            movement=player.GetComponent<FirstPersonController>();view=player.ViewCamera;
            oldPosition=view.transform.localPosition;oldRotation=view.transform.localRotation;oldFov=view.fieldOfView;oldTime=Time.timeScale;
            oldMove=movement.MovementLocked;oldLook=movement.LookLocked;oldInput=player.InputLocked;
            movement.MovementLocked=movement.LookLocked=true;player.InputLocked=true;
            oldHold=player.HoldAnchor!=null&&player.HoldAnchor.gameObject.activeSelf;
            if(player.HoldAnchor!=null)player.HoldAnchor.gameObject.SetActive(false);
            windowPoint=window!=null?window.position:new Vector3(-35.67f,1.68f,25.89f);
            doorPoint=classroomDoor!=null?classroomDoor.transform.position+Vector3.up*1.62f:new Vector3(-32.11f,1.62f,25.89f);
            seatPoint=game.schoolPeriod!=null?game.schoolPeriod.seat.seatedView.position:player.ViewCamera.transform.position;
            if(classroomDoor!=null){oldDoorEnabled=classroomDoor.enabled;doorClosed=classroomDoor.hinge.localRotation;classroomDoor.enabled=false;}
            Build();SpawnBackgroundWalker();active=true;startTime=Time.unscaledTime;Time.timeScale=0;
            if(titleMusic!=null){musicSource.clip=titleMusic;musicSource.Play();}
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            EventSystem.current?.SetSelectedGameObject(startButton.gameObject);
            RenderShot(0,0);
        }
        void SpawnBackgroundWalker()
        {
            if(backgroundWalkerPrefab==null)return;
            backgroundWalker=Instantiate(backgroundWalkerPrefab);
            backgroundWalker.name="Title background walker (cosmetic)";
            // A pure set-dressing double for the opening shot: strip anything that would make it act like a
            // real gameplay agent (AI, navigation, footstep audio, collision), keep only the walk-cycle visual.
            var ai=backgroundWalker.GetComponent<CaretakerAI>();if(ai!=null)Destroy(ai);
            var walkAudio=backgroundWalker.GetComponent<CaretakerWalkAudio>();if(walkAudio!=null)Destroy(walkAudio);
            var agent=backgroundWalker.GetComponent<NavMeshAgent>();if(agent!=null)Destroy(agent);
            var col=backgroundWalker.GetComponent<Collider>();if(col!=null)Destroy(col);
            var walker=backgroundWalker.AddComponent<TitleBackgroundWalker>();
            walker.pointA=new Vector3(-3f,0f,.6f);walker.pointB=new Vector3(2f,0f,.6f);walker.speed=.9f;walker.pauseSeconds=1.8f;
        }
        void Update()
        {
            if(!active)return;
            var kb=Keyboard.current;var pad=Gamepad.current;
            if(starting)
            {
                if((kb!=null&&kb.escapeKey.wasPressedThisFrame)||(pad!=null&&pad.buttonEast.wasPressedThisFrame))SkipIntro();
            }
            else if(kb!=null&&kb.enterKey.wasPressedThisFrame)
            {
                var selected=EventSystem.current?.currentSelectedGameObject?.GetComponent<Button>();
                if(selected!=null){if(selected.interactable)selected.onClick.Invoke();}else StartGame();
            }
        }
        void LateUpdate()
        {
            if(!active)return;
            foreach(var other in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if(other!=canvas&&other.enabled&&other.renderMode!=RenderMode.WorldSpace){hidden.Add(other);other.enabled=false;}
            Cursor.lockState=CursorLockMode.None;Cursor.visible=!starting;
            float elapsed=SequenceTime;
            if(starting){RenderEntrance(elapsed);return;}
            ShotIndex=Mathf.FloorToInt(elapsed/shotSeconds)%4;
            float t=elapsed%shotSeconds;
            RenderShot(ShotIndex,t/shotSeconds);
            fade.color=new Color(0,0,0,Mathf.Max(1-t/.9f,(t-(shotSeconds-.9f))/.9f));
        }
        void Pose(Vector3 position,Vector3 target,float fov)
        {view.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));view.fieldOfView=fov;}
        void RenderShot(int index,float t)
        {
            float u=Mathf.SmoothStep(0,1,t);
            switch(index)
            {
                case 0:
                    Pose(Vector3.Lerp(new Vector3(3.2f,2.05f,-10.8f),new Vector3(-1.8f,1.85f,-9.1f),u),new Vector3(-1.7f,1.5f,-3.5f),57-u*5);
                    caption.text="08:45  /  ANOTHER SCHOOL DAY";break;
                case 1:
                    Pose(Vector3.Lerp(new Vector3(-33.7f,1.65f,22.5f),new Vector3(-33.7f,1.5f,27.5f),u),new Vector3(-33.3f,1.5f,44),58-u*4);
                    caption.text="KEEP YOUR HEAD DOWN.";break;
                case 2:
                    Pose(Vector3.Lerp(new Vector3(-33.7f,1.6f,75.4f),new Vector3(-33.7f,1.6f,78.0f),u),new Vector3(-35.6f,1.3f,82.1f),57-u*5);
                    caption.text="SOME THINGS AREN'T YOURS TO KEEP.";break;
                default:
                    Pose(Vector3.Lerp(windowPoint+new Vector3(-3.9f,.12f,-.4f),windowPoint+new Vector3(-2.5f,0,.2f),u),doorPoint+Vector3.right*5,51-u*5);
                    caption.text="YEAR 6  /  MR REED";break;
            }
        }
        public void StartGame()
        { StartMode(false); }
        public void StartDarkGame()
        { StartMode(true); }
        void StartMode(bool dark)
        {
            if(!active||starting||!SchoolGameMode.Select(dark))return;starting=true;startTime=Time.unscaledTime;
            menuGroup.interactable=false;menuGroup.blocksRaycasts=false;EventSystem.current?.SetSelectedGameObject(null);
            skip.text="Esc  /  Skip introduction";
        }
        void RenderEntrance(float elapsed)
        {
            // Fade away the current shot, then enter through the clear side of the window.
            if(elapsed<.65f){menuGroup.alpha=1-elapsed/.65f;fade.color=new Color(0,0,0,elapsed/.65f);return;}
            menuGroup.alpha=0;
            float t=Mathf.Clamp01((elapsed-.65f)/entranceSeconds);
            Vector3 a=windowPoint+new Vector3(-4.4f,.02f,.52f);
            Vector3 b=windowPoint+new Vector3(.8f,-.04f,.52f);
            Vector3 c=doorPoint+new Vector3(1.7f,0,0);
            Vector3 position;
            if(t<.43f)position=Vector3.Lerp(a,b,Mathf.SmoothStep(0,1,t/.43f));
            else if(t<.70f)position=Vector3.Lerp(b,c,Mathf.SmoothStep(0,1,(t-.43f)/.27f));
            else position=Vector3.Lerp(c,seatPoint,Mathf.SmoothStep(0,1,(t-.70f)/.30f));
            Vector3 teacher=game.schoolPeriod!=null?game.schoolPeriod.teacher.transform.position+Vector3.up*1.7f:seatPoint+Vector3.left*5;
            Vector3 aim=Vector3.Lerp(doorPoint+Vector3.right*9,teacher,Mathf.SmoothStep(0,1,(t-.66f)/.30f));
            Pose(position,aim,Mathf.Lerp(52,60,t));
            if(classroomDoor!=null)classroomDoor.hinge.localRotation=doorClosed*Quaternion.Euler(0,classroomDoor.openAngle*Mathf.SmoothStep(0,1,(t-.18f)/.22f),0);
            float black=Mathf.Max(1-(elapsed-.65f)/.8f,Mathf.Clamp01((t-.93f)/.07f));
            fade.color=new Color(0,0,0,black);
            if(t>=1)Finish();
        }
        public void SkipIntro(){if(active&&starting)Finish();}
        void Restore()
        {
            if(view!=null){view.transform.localPosition=oldPosition;view.transform.localRotation=oldRotation;view.fieldOfView=oldFov;}
            if(movement!=null){movement.MovementLocked=oldMove;movement.LookLocked=oldLook;}
            if(player!=null){player.InputLocked=oldInput;player.SuppressActionsThisFrame();if(player.HoldAnchor!=null)player.HoldAnchor.gameObject.SetActive(oldHold);}
            if(classroomDoor!=null){classroomDoor.hinge.localRotation=doorClosed;classroomDoor.enabled=oldDoorEnabled;}
            foreach(var other in hidden)if(other!=null)other.enabled=true;hidden.Clear();
            if(backgroundWalker!=null){Destroy(backgroundWalker);backgroundWalker=null;}
            Time.timeScale=oldTime;Cursor.visible=false;Cursor.lockState=CursorLockMode.Locked;
        }
        void Finish()
        {
            if(!active)return;active=false;Restore();canvas.gameObject.SetActive(false);
            if(musicSource!=null)musicSource.Stop();
            game.BeginSchoolDay();
        }
        void OnDisable(){if(active){active=false;Restore();if(canvas!=null)canvas.gameObject.SetActive(false);}if(musicSource!=null)musicSource.Stop();}
        void OnDestroy(){if(Instance==this)Instance=null;}
        void Quit()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
            #else
            Application.Quit();
            #endif
        }
        void Build()
        {
            if(EventSystem.current==null&&FindFirstObjectByType<EventSystem>()==null)
            {var e=new GameObject("School menu events",typeof(EventSystem));e.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();}
            var root=new GameObject("School title canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);
            canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=29000;
            musicSource=gameObject.AddComponent<AudioSource>();musicSource.loop=true;musicSource.playOnAwake=false;musicSource.spatialBlend=0;musicSource.volume=.35f;musicSource.priority=160;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            ImageRect("Top cinema bar",root.transform,new Vector2(0,.925f),Vector2.one,Color.black);
            ImageRect("Bottom cinema bar",root.transform,Vector2.zero,new Vector2(1,.075f),Color.black);
            fade=ImageRect("Scene dissolve",root.transform,Vector2.zero,Vector2.one,Color.black);fade.raycastTarget=false;
            var content=Rect("Title artwork",root.transform,Vector2.zero,Vector2.one);menuGroup=content.gameObject.AddComponent<CanvasGroup>();
            var shade=Rect("Ink vignette",content,Vector2.zero,Vector2.one);shade.gameObject.AddComponent<TitleInkShade>().raycastTarget=false;
            Label("A SCHOOL DAY GONE WRONG",content,new Vector2(.07f,.75f),new Vector2(.54f,.81f),22,Cream);
            var title=ImageRect("CONFISCATED! logo",content,new Vector2(.065f,.46f),new Vector2(.60f,.76f),Color.white);
            title.sprite=titleLogo!=null?titleLogo:Resources.Load<Sprite>("Art/ConfiscatedLogo");
            title.rectTransform.pivot=new Vector2(0,.5f);
            title.preserveAspect=true;title.raycastTarget=false;
            // The rules ("recover five, escape him") are taught by play within the first minute; a title screen
            // doesn't also need to spell them out. Kicker + title + the rotating establishing-shot caption carry the mood.
            startButton=MakeButton("Start game",content,new Vector2(.07f,.32f),new Vector2(.28f,.405f),StartGame);
            var dark=MakeButton("Dark mode",content,new Vector2(.30f,.32f),new Vector2(.51f,.405f),StartDarkGame);
            dark.interactable=SchoolGameMode.DarkUnlocked;
            Label(SchoolGameMode.DarkUnlocked?(SchoolGameMode.DevUnlock?"DEV UNLOCK ON. LIGHTS OUT.":"LIGHTS OUT. FIND YOUR TORCH."):"ESCAPE ONCE TO UNLOCK DARK MODE",content,new Vector2(.30f,.27f),new Vector2(.66f,.315f),17,Cream);
            MakeButton("Quit",content,new Vector2(.07f,.225f),new Vector2(.20f,.292f),Quit);
            caption=Label("",content,new Vector2(.07f,.09f),new Vector2(.78f,.15f),17,new Color(.87f,.84f,.73f));
            skip=Label("",root.transform,new Vector2(.7f,.005f),new Vector2(.95f,.065f),17,Cream);skip.alignment=TextAnchor.MiddleRight;
            var endingsBg=ImageRect("Endings backing",content,new Vector2(.67f,.64f),new Vector2(.98f,.91f),new Color(.055f,.075f,.105f,.72f));
            endingsBg.raycastTarget=false;
            var endings=Label(EndingsText(),content,new Vector2(.69f,.655f),new Vector2(.96f,.895f),20,Color.white);
            endings.alignment=TextAnchor.UpperLeft;endings.supportRichText=true;
        }
        static string EndingsText()
        {
            string Row(EndingUnlocks.Ending e,string label)=>EndingUnlocks.IsUnlocked(e)?"<color=#F7F0D4>"+label+"</color>":"<color=#6E695A>?</color>";
            return "<color=#F7F0D4>ENDINGS</color>\n1. "+Row(EndingUnlocks.Ending.Caught,"CAUGHT")
                +"\n2. "+Row(EndingUnlocks.Ending.Completed,"COMPLETED")
                +"\n3. "+Row(EndingUnlocks.Ending.FastRun,"FAST RUN")
                +"\n4. "+Row(EndingUnlocks.Ending.QuackEscape,"QUACK ESCAPE");
        }
        Button MakeButton(string text,Transform parent,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
        {
            var image=ImageRect(text,parent,min,max,Cream);var button=image.gameObject.AddComponent<Button>();
            button.targetGraphic=image;var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.87f,.51f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.8f,.66f,.35f);button.colors=colors;
            var border=Rect("Pencil edge",image.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<SketchBorder>();border.color=Ink;border.raycastTarget=false;
            var label=Label(text.ToUpperInvariant(),image.transform,Vector2.zero,Vector2.one,32,Ink);label.alignment=TextAnchor.MiddleCenter;
            button.onClick.AddListener(action);return button;
        }
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var r=g.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
        static Image ImageRect(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {var i=Rect(name,parent,min,max).gameObject.AddComponent<Image>();i.color=color;return i;}
        static Text Label(string text,Transform parent,Vector2 min,Vector2 max,int size,Color color)
        {var t=Rect(text,parent,min,max).gameObject.AddComponent<Text>();t.font=SchoolTypography.Font;t.text=text;t.fontSize=size;t.color=color;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;}
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TitleInkShade:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            float[] x={0,.30f,.65f,1};float[] a={.82f,.70f,.20f,0};
            for(int i=0;i<4;i++){var tint=new Color(.018f,.025f,.035f,a[i]);vh.AddVert(new Vector2(Mathf.Lerp(r.xMin,r.xMax,x[i]),r.yMin),tint,Vector2.zero);vh.AddVert(new Vector2(Mathf.Lerp(r.xMin,r.xMax,x[i]),r.yMax),tint,Vector2.zero);}
            for(int i=0;i<3;i++){int n=i*2;vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n+1,n+3,n+2);}
        }
    }
}

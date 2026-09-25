using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Confiscated
{
    /// <summary>
    /// Escape (or gamepad Start) pauses: resume, restart the run, settings, back to the title, quit. Added by GameManager and built on
    /// first use, so scene rebuilds cannot lose it. The world stops (timeScale 0, audio paused) but the run clock is real time and keeps
    /// going, as RunTiming documents: pausing must not be a way to plan a leaderboard route for free.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        public static bool IsOpen {get;private set;}
        const string VolumeKey="Confiscated.Volume";
        static readonly Color Cream=new(.97f,.94f,.83f),Ink=new(.055f,.075f,.105f),Amber=new(1,.87f,.51f);
        Canvas canvas;
        Button resume;
        Text effectsLabel,screenLabel,volumeLabel,lookLabel;
        PlayerInteractor player;FirstPersonController movement;
        bool oldMove,oldLook,oldInput,oldCursor,oldAudio,modalLastFrame,fullScreen;
        CursorLockMode oldLock;
        float oldTime,openedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplySaved(){IsOpen=false;AudioListener.pause=false;AudioListener.volume=PlayerPrefs.GetFloat(VolumeKey,1);}

        static bool ModalOpen()
        {
            var gm=GameManager.Instance;
            return gm!=null&&((gm.lockerUI!=null&&gm.lockerUI.IsOpen)||(gm.schoolPeriod!=null&&gm.schoolPeriod.worksheetUI!=null&&gm.schoolPeriod.worksheetUI.IsOpen)||
                (gm.detention!=null&&gm.detention.MinigameOpen));
        }
        static bool CanOpen()
        {
            var gm=GameManager.Instance;
            return gm!=null&&gm.Current!=GameManager.State.Menu&&!SchoolTitleMenu.IsActive&&!ComicDialogue.IsActive;
        }
        void Update()
        {
            var kb=Keyboard.current;var pad=Gamepad.current;
            bool pressed=(kb!=null&&kb.escapeKey.wasPressedThisFrame)||(pad!=null&&pad.startButton.wasPressedThisFrame);
            if(IsOpen){if(pressed&&Time.unscaledTime>openedAt+.15f)Close();return;}
            // Escape also closes the bag, the worksheet and the detention games. Whichever Update runs first, that press must not pause too.
            bool modal=ModalOpen();
            if(pressed&&!modal&&!modalLastFrame&&CanOpen())Open();
            modalLastFrame=modal;
        }
        public void Open()
        {
            if(IsOpen)return;
            player=FindFirstObjectByType<PlayerInteractor>();if(player==null)return;
            movement=player.GetComponent<FirstPersonController>();
            if(canvas==null)Build();
            oldTime=Time.timeScale;oldAudio=AudioListener.pause;oldCursor=Cursor.visible;oldLock=Cursor.lockState;
            // The player controller frees the cursor on the same Escape press, sometimes before this runs, so the saved state can't be trusted during play.
            var gm=GameManager.Instance;
            if(gm!=null&&(gm.IsPlaying||gm.Current==GameManager.State.Detention)){oldLock=CursorLockMode.Locked;oldCursor=false;}
            oldMove=movement.MovementLocked;oldLook=movement.LookLocked;oldInput=player.InputLocked;
            movement.MovementLocked=movement.LookLocked=true;player.InputLocked=true;
            Time.timeScale=0;AudioListener.pause=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            IsOpen=true;openedAt=Time.unscaledTime;canvas.gameObject.SetActive(true);Refresh();
            EventSystem.current?.SetSelectedGameObject(resume.gameObject);
        }
        public void Close()
        {
            if(!IsOpen)return;IsOpen=false;
            if(canvas!=null)canvas.gameObject.SetActive(false);
            Time.timeScale=oldTime;AudioListener.pause=oldAudio;Cursor.lockState=oldLock;Cursor.visible=oldCursor;
            if(movement!=null){movement.MovementLocked=oldMove;movement.LookLocked=oldLook;}
            if(player!=null){player.InputLocked=oldInput;player.SuppressActionsThisFrame();}
            PlayerPrefs.Save();
        }
        // Leaving the scene: never carry a frozen clock or muted audio into the next one.
        void Leave(System.Action go){IsOpen=false;Time.timeScale=1;AudioListener.pause=false;PlayerPrefs.Save();go();}
        void OnDestroy(){if(IsOpen){IsOpen=false;AudioListener.pause=false;}}
        void Quit()
        {
            Leave(()=>{
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
            #else
            Application.Quit();
            #endif
            });
        }
        void CycleEffects()
        {
            var feel=movement!=null?movement.GetComponent<ChaseCamera>():null;if(feel==null)return;
            feel.SetIntensity(feel.intensity>.75f?.5f:feel.intensity>.01f?0:1);Refresh();
        }
        void ToggleScreen(){fullScreen=!fullScreen;Screen.fullScreen=fullScreen;Refresh();}
        void Refresh()
        {
            var feel=movement!=null?movement.GetComponent<ChaseCamera>():null;
            float f=feel!=null?feel.intensity:1;
            effectsLabel.text="CAMERA + VHS: "+(f==0?"OFF":f<1?"REDUCED":"NORMAL");
            screenLabel.text=fullScreen?"FULL SCREEN":"WINDOWED";
        }

        void Build()
        {
            if(EventSystem.current==null&&FindFirstObjectByType<EventSystem>()==null)
            {var e=new GameObject("Pause menu events",typeof(EventSystem));e.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();}
            fullScreen=Screen.fullScreen;
            var root=new GameObject("Pause menu canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);
            canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=31000;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            ImageRect("Dim",root.transform,Vector2.zero,Vector2.one,new Color(.02f,.03f,.05f,.72f));
            var paper=ImageRect("Paper",root.transform,new Vector2(.25f,.13f),new Vector2(.75f,.89f),Cream);
            var edge=Rect("Pencil edge",paper.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<SketchBorder>();edge.color=Ink;edge.raycastTarget=false;
            var p=paper.transform;
            var title=Label("PAUSED",p,new Vector2(.06f,.86f),new Vector2(.94f,.97f),58,Ink);title.alignment=TextAnchor.MiddleCenter;
            var note=Label("The run clock keeps ticking while you're paused.",p,new Vector2(.06f,.80f),new Vector2(.94f,.86f),20,new Color(.35f,.3f,.22f));note.alignment=TextAnchor.MiddleCenter;

            resume=MakeButton("Resume",p,new Vector2(.06f,.665f),new Vector2(.47f,.775f),Close);
            MakeButton("Restart run",p,new Vector2(.06f,.535f),new Vector2(.47f,.645f),()=>Leave(()=>GameManager.Instance.RestartRun()));
            MakeButton("Title screen",p,new Vector2(.06f,.405f),new Vector2(.47f,.515f),()=>Leave(()=>GameManager.Instance.ReturnToTitle()));
            MakeButton("Quit game",p,new Vector2(.06f,.275f),new Vector2(.47f,.385f),Quit);

            volumeLabel=Label("",p,new Vector2(.53f,.735f),new Vector2(.94f,.785f),21,Ink);
            MakeSlider("Volume",p,new Vector2(.53f,.675f),new Vector2(.94f,.73f),0,1,AudioListener.volume,v=>{AudioListener.volume=v;PlayerPrefs.SetFloat(VolumeKey,v);volumeLabel.text="VOLUME  "+Mathf.RoundToInt(v*100)+"%";});
            lookLabel=Label("",p,new Vector2(.53f,.605f),new Vector2(.94f,.655f),21,Ink);
            MakeSlider("Mouse sensitivity",p,new Vector2(.53f,.545f),new Vector2(.94f,.60f),.02f,.2f,movement.MouseSensitivity,v=>{movement.MouseSensitivity=v;lookLabel.text="MOUSE SENSITIVITY  "+Mathf.RoundToInt(v/.08f*100)+"%";});
            effectsLabel=MakeButton("Effects",p,new Vector2(.53f,.405f),new Vector2(.94f,.515f),CycleEffects).GetComponentInChildren<Text>();effectsLabel.fontSize=24;
            screenLabel=MakeButton("Screen",p,new Vector2(.53f,.275f),new Vector2(.94f,.385f),ToggleScreen).GetComponentInChildren<Text>();screenLabel.fontSize=24;

            var keys=Label("WASD move    Shift run    Q / E lean    Space look back\nF or left click interact    1 wind-up toy    2 / G glue    Tab bag",p,new Vector2(.06f,.05f),new Vector2(.94f,.23f),21,Ink);
            keys.alignment=TextAnchor.MiddleCenter;
            root.SetActive(false);
        }
        Button MakeButton(string text,Transform parent,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
        {
            var image=ImageRect(text,parent,min,max,Color.white);var button=image.gameObject.AddComponent<Button>();
            button.targetGraphic=image;var colors=button.colors;colors.normalColor=new Color(.9f,.86f,.72f);colors.highlightedColor=Amber;colors.selectedColor=Amber;colors.pressedColor=new Color(.8f,.66f,.35f);colors.fadeDuration=0;button.colors=colors;
            var border=Rect("Pencil edge",image.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<SketchBorder>();border.color=Ink;border.raycastTarget=false;
            var label=Label(text.ToUpperInvariant(),image.transform,Vector2.zero,Vector2.one,30,Ink);label.alignment=TextAnchor.MiddleCenter;
            button.onClick.AddListener(action);return button;
        }
        void MakeSlider(string name,Transform parent,Vector2 min,Vector2 max,float low,float high,float value,UnityEngine.Events.UnityAction<float> changed)
        {
            var go=DefaultControls.CreateSlider(new DefaultControls.Resources());go.name=name;go.transform.SetParent(parent,false);
            var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;
            var slider=go.GetComponent<Slider>();slider.minValue=low;slider.maxValue=high;
            Tint(go.transform,"Background",new Color(.3f,.27f,.2f,.55f));Tint(go.transform,"Fill Area/Fill",new Color(.78f,.5f,.2f));Tint(go.transform,"Handle Slide Area/Handle",Color.white);
            var colors=slider.colors;colors.normalColor=Ink;colors.highlightedColor=colors.selectedColor=colors.pressedColor=new Color(.78f,.3f,.15f);colors.fadeDuration=0;slider.colors=colors;
            slider.onValueChanged.AddListener(changed);slider.SetValueWithoutNotify(Mathf.Clamp(value,low,high));changed(slider.value);
        }
        static void Tint(Transform root,string path,Color color){var image=root.Find(path)?.GetComponent<Image>();if(image!=null)image.color=color;}
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var r=g.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
        static Image ImageRect(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {var i=Rect(name,parent,min,max).gameObject.AddComponent<Image>();i.color=color;return i;}
        static Text Label(string text,Transform parent,Vector2 min,Vector2 max,int size,Color color)
        {var t=Rect(text.Length==0||text.Length>24?"Text":text,parent,min,max).gameObject.AddComponent<Text>();t.font=SchoolTypography.Font;t.text=text;t.fontSize=size;t.color=color;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;}
    }
}

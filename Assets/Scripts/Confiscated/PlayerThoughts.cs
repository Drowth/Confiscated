using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Confiscated
{
    /// <summary>
    /// Smith's own thoughts, shown as a small thought bubble above the HUD line. Walking into the library without the torch
    /// prompts a reminder to fetch it; the first time in, it also recalls the chatterbox's rumour (stand still, torch off).
    /// Attaches itself to the school run at scene load.
    /// </summary>
    public sealed class PlayerThoughts : MonoBehaviour
    {
        public const string NoTorch="I really should get my torch from the locker.";
        public const string Legend="The chatterbox said if I see anything in here... stand still and turn my torch off.";
        public static PlayerThoughts Instance {get;private set;}
        /// <summary>The rumour reminder plays once per session (a retry already remembers it).</summary>
        static bool legendRecalled;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){legendRecalled=false;Instance=null;hooked=false;}
        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook(){if(!hooked){hooked=true;SceneManager.sceneLoaded+=(_,_)=>Attach();}Attach();}
        static void Attach(){var run=Object.FindFirstObjectByType<SchoolRunController>();if(run!=null&&run.GetComponent<PlayerThoughts>()==null)run.gameObject.AddComponent<PlayerThoughts>();}

        public string Showing=>panel!=null&&panel.activeSelf?text.text:"";
        public int Count {get;private set;}
        readonly Queue<string> pending=new();
        float until;bool wasInLibrary;
        GameObject panel;Text text;
        PlayerInteractor player;ChatterboxStudent chatterbox;
        bool HasTorch=>player!=null&&player.GetComponent<PlayerInventory>()!=null&&player.GetComponent<PlayerInventory>().HasCarried(InventoryItemKind.Torch);

        void Awake()=>Instance=this;
        void OnDestroy(){if(Instance==this)Instance=null;if(panel!=null)Destroy(panel.transform.root.gameObject);}

        public void Think(string thought){if(!pending.Contains(thought)&&Showing!=thought)pending.Enqueue(thought);}

        void Update()
        {
            var game=GameManager.Instance;var run=SchoolRunController.Instance;
            bool live=game!=null&&game.IsPlaying&&run!=null&&!ComicDialogue.IsActive&&!PauseMenu.IsOpen&&!SchoolTitleMenu.IsActive;
            if(live)
            {
                if(player==null){player=run.period.Player;chatterbox=Object.FindFirstObjectByType<ChatterboxStudent>();}
                bool inLibrary=LibraryWindow.InLibrary(player.transform.position);
                if(inLibrary&&!wasInLibrary)
                {
                    if(!HasTorch)Think(NoTorch);
                    if(!legendRecalled&&chatterbox!=null&&chatterbox.ToldRumour){legendRecalled=true;Think(Legend);}
                }
                wasInLibrary=inLibrary;
            }
            bool showing=panel!=null&&panel.activeSelf;
            if(showing&&(Time.time>=until||!live||Showing==NoTorch&&HasTorch)){panel.SetActive(false);showing=false;}
            while(!showing&&live&&pending.Count>0)
            {
                var thought=pending.Dequeue();
                if(thought==NoTorch&&HasTorch)continue;
                Show(thought);showing=true;
            }
        }

        void Show(string thought)
        {
            if(panel==null)Build();
            text.text=thought;panel.SetActive(true);Count++;
            until=Time.time+Mathf.Max(3.5f,thought.Length/13f);
        }

        void Build()
        {
            var root=new GameObject("Player thoughts canvas",typeof(Canvas),typeof(CanvasScaler));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=880;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            panel=new GameObject("Thought",typeof(RectTransform));panel.transform.SetParent(root.transform,false);
            var rect=(RectTransform)panel.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.2f);rect.sizeDelta=new Vector2(760,0);
            var paper=panel.AddComponent<Image>();paper.color=new Color(.97f,.95f,.88f,.95f);paper.raycastTarget=false;
            var fit=panel.AddComponent<VerticalLayoutGroup>();fit.padding=new RectOffset(26,26,14,16);fit.childControlHeight=fit.childControlWidth=true;fit.childForceExpandHeight=false;
            panel.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            Edge(panel.transform,Vector2.zero,Vector2.one,Vector2.zero);
            var go=new GameObject("Words",typeof(RectTransform));go.transform.SetParent(panel.transform,false);
            text=go.AddComponent<Text>();text.font=SchoolTypography.Font;text.fontSize=27;text.fontStyle=FontStyle.Italic;text.color=Color.black;
            text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;
            panel.SetActive(false);
        }
        static void Edge(Transform parent,Vector2 min,Vector2 max,Vector2 offset)
        {
            var e=new GameObject("Pencil edge",typeof(RectTransform));e.transform.SetParent(parent,false);e.AddComponent<LayoutElement>().ignoreLayout=true;
            var r=(RectTransform)e.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=offset;
            var border=e.AddComponent<SketchBorder>();border.color=Color.black;border.raycastTarget=false;
        }
    }
}

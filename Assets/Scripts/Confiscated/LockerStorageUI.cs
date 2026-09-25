using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
namespace Confiscated
{
    /// <summary>Pointer-driven illustrated inventory modal. World time continues, so catches safely cancel the modal.</summary>
    public class LockerStorageUI : MonoBehaviour
    {
        public Sprite backgroundArt, slotPaper;
        public PlayerInventory Inventory { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsDragging { get; private set; }
        public bool HasLockerAccess=>locker!=null;
        public InventorySlotView Source { get; private set; }
        public IReadOnlyList<InventorySlotView> Slots=>slots;
        readonly List<InventorySlotView> slots=new();
        Canvas canvas;
        RectTransform sheet,ghostRect;
        Image ghost;
        Text satchelCount,lockerCount,message;
        Text title,safeTitle,useTitle;
        Text itemDetails;
        Image inspectedIcon;
        RectTransform remoteCover,usePanel;
        Canvas hudCanvas;
        bool hudWasEnabled;
        PlayerInteractor player;
        PlayerLocker locker;
        FirstPersonController movement;
        InventoryEntry sourceEntry;
        bool previousMove,previousLook,previousInput,previousCursorVisible;
        CursorLockMode previousCursor;
        int openedFrame,endedDragFrame=-1;
        static readonly Color Ink=new(.12f,.17f,.26f);
        void Awake(){Build();}
        void OnDisable(){Close();}
        public void Open(PlayerInteractor who,PlayerLocker target=null)
        {
            if(IsOpen)return;
            Inventory=who.GetComponent<PlayerInventory>();if(Inventory==null)return;
            if(canvas==null)Build();
            if(EventSystem.current==null&&FindFirstObjectByType<EventSystem>()==null)
            {
                var system=new GameObject("Inventory EventSystem");system.AddComponent<EventSystem>();
                system.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            player=who;locker=target;movement=who.GetComponent<FirstPersonController>();
            if(who.InputLocked)return;
            previousMove=movement.MovementLocked;previousLook=movement.LookLocked;previousInput=player.InputLocked;
            previousCursor=Cursor.lockState;previousCursorVisible=Cursor.visible;
            movement.MovementLocked=true;movement.LookLocked=true;player.InputLocked=true;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(locker!=null)DoorSounds.For(locker.gameObject,DoorSounds.Kind.Locker).Play(true);
            IsOpen=true;openedFrame=Time.frameCount;canvas.gameObject.SetActive(true);
            hudCanvas=HudController.Instance!=null?HudController.Instance.GetComponentInParent<Canvas>():null;
            if(hudCanvas!=null){hudWasEnabled=hudCanvas.enabled;hudCanvas.enabled=false;}
            ConfigureMode();
            Inspect(null);
            Inventory.Changed+=Refresh;Refresh();
            Feedback(locker!=null&&SchoolGameMode.Dark&&Inventory.Contains(InventoryContainer.Locker,InventoryItemKind.Torch)?"TORCH: move it from your locker to your satchel. Press T to switch it on / off.":"");
            HudController.Instance?.SetPrompt(null);HudController.Instance?.SetHoldProgress(-1);
        }
        public void Close()
        {
            if(!IsOpen)return;
            if(locker!=null)DoorSounds.For(locker.gameObject,DoorSounds.Kind.Locker).Play(false);
            IsOpen=false;CancelSource();
            if(Inventory!=null)Inventory.Changed-=Refresh;
            if(canvas!=null)canvas.gameObject.SetActive(false);
            if(hudCanvas!=null)hudCanvas.enabled=hudWasEnabled;
            if(movement!=null){movement.MovementLocked=previousMove;movement.LookLocked=previousLook;}
            if(player!=null){player.InputLocked=previousInput;player.SuppressActionsThisFrame();}
            Cursor.lockState=previousCursor;Cursor.visible=previousCursorVisible;
        }
        void Update()
        {
            if(ComicDialogue.IsActive)return;
            var kb=Keyboard.current;var gm=GameManager.Instance;
            if(!IsOpen)
            {
                if(kb!=null&&kb.tabKey.wasPressedThisFrame&&gm!=null&&(gm.IsPlaying||gm.Current==GameManager.State.Detention))
                {var who=Object.FindFirstObjectByType<PlayerInteractor>();if(who!=null&&!who.InputLocked)Open(who);}
                return;
            }
            if((gm!=null&&!gm.IsPlaying&&gm.Current!=GameManager.State.Detention)||(locker!=null&&Vector3.Distance(player.transform.position,locker.transform.position)>4.5f)){Close();return;}
            if(Source!=null&&Mouse.current!=null)DragTo(Mouse.current.position.ReadValue());
            if(Time.frameCount==openedFrame)return;
            if(kb!=null&&(kb.escapeKey.wasPressedThisFrame||kb.tabKey.wasPressedThisFrame||kb.fKey.wasPressedThisFrame))Close();
        }
        void ConfigureMode()
        {
            bool local=HasLockerAccess;title.text=local?"YOUR LOCKER":"YOUR SATCHEL";
            remoteCover.gameObject.SetActive(!local);safeTitle.gameObject.SetActive(local);
            foreach(var slot in slots)if(slot.container==InventoryContainer.Locker)slot.gameObject.SetActive(local);
            usePanel.anchoredPosition=local?new Vector2(392-768,512-790):new Vector2(1144-768,512-480);
            useTitle.rectTransform.anchoredPosition=local?new Vector2(213-768,512-790):new Vector2(1144-768,512-345);
            useTitle.color=local?new Color(.97f,.91f,.74f):Ink;
            lockerCount.gameObject.SetActive(local);
            usePanel.gameObject.SetActive(local);useTitle.gameObject.SetActive(local);
            itemDetails.gameObject.SetActive(!local);inspectedIcon.transform.parent.gameObject.SetActive(!local);
        }
        void Refresh()
        {
            foreach(var slot in slots)slot.Refresh();
            int a=0,b=0;
            for(int i=0;i<6;i++)if(Inventory.Get(InventoryContainer.Satchel,i)!=null)a++;
            for(int i=0;i<9;i++)if(Inventory.Get(InventoryContainer.Locker,i)!=null)b++;
            satchelCount.text="CARRIED  "+a+" / 6";lockerCount.text="STORED  "+b+" / 9";
        }
        public void BeginDrag(InventorySlotView view,Vector2 screenPosition)
        {
            if(!IsOpen||!HasLockerAccess||view.Entry==null)return;
            if(Source!=null){IsDragging=true;return;}
            Source=view;sourceEntry=view.Entry;IsDragging=true;
            ghost.sprite=sourceEntry.definition.icon;ghost.gameObject.SetActive(true);ghostRect.SetAsLastSibling();DragTo(screenPosition);Refresh();
            Feedback(sourceEntry.definition.displayName);
        }
        public void DragTo(Vector2 screenPosition)
        {
            if(Source==null)return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(sheet,screenPosition,null,out var local);ghostRect.anchoredPosition=local;
        }
        public void DropOn(InventorySlotView target)
        {
            if(!IsOpen||Source==null)return;
            if(!HasLockerAccess&&(Source.container==InventoryContainer.Locker||target.container==InventoryContainer.Locker)){Feedback("Locker unavailable.");return;}
            if(Source.Entry!=sourceEntry){CancelSource();Refresh();Feedback("Item unavailable.");return;}
            Inventory.Move(Source.container,Source.index,target.container,target.index,out string text);
            CancelSource();endedDragFrame=Time.frameCount;Refresh();Feedback(text);
        }
        public void EndDrag()
        {
            if(!IsDragging)return;
            CancelSource();endedDragFrame=Time.frameCount;Refresh();Feedback("");
        }
        public void Click(InventorySlotView view)
        {
            if(!IsOpen||IsDragging||Time.frameCount==endedDragFrame)return;
            if(!HasLockerAccess){Inspect(view.Entry);return;}
            if(Source!=null){DropOn(view);return;}
            if(view.Entry==null){Feedback("");return;}
            Source=view;sourceEntry=view.Entry;ghost.sprite=sourceEntry.definition.icon;ghost.gameObject.SetActive(true);ghostRect.SetAsLastSibling();
            if(Mouse.current!=null)DragTo(Mouse.current.position.ReadValue());Refresh();
            Feedback(sourceEntry.definition.displayName);
        }
        void CancelSource(){Source=null;sourceEntry=null;IsDragging=false;if(ghost!=null)ghost.gameObject.SetActive(false);}
        void Feedback(string text){if(message!=null)message.text=text;}
        void Inspect(InventoryEntry entry)
        {
            inspectedIcon.sprite=entry?.definition.icon;inspectedIcon.enabled=inspectedIcon.sprite!=null;
            itemDetails.text=entry==null?"YOUR BELONGINGS\n\nClick an item to inspect it.\n\nKeys, your hall pass and newsletters work with E at their destination.\n\n1: hold football   2 / Q: place toy\n3 / G: drop glue (D-pad down)":entry.definition.displayName.ToUpperInvariant()+"\n\n"+entry.definition.description;
        }

        void Build()
        {
            var go=new GameObject("Illustrated locker screen",typeof(RectTransform));go.transform.SetParent(transform,false);
            canvas=go.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=300;
            var scaler=go.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1536,1024);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            var dim=Rect("Dim corridor",go.transform,Vector2.zero,Vector2.zero);dim.anchorMin=Vector2.zero;dim.anchorMax=Vector2.one;dim.offsetMin=dim.offsetMax=Vector2.zero;
            dim.gameObject.AddComponent<Image>().color=new Color(.035f,.04f,.07f,.82f);
            sheet=Rect("Satchel and locker",go.transform,Vector2.zero,new Vector2(1536,1024));
            var art=sheet.gameObject.AddComponent<Image>();art.sprite=backgroundArt;
            title=Label("YOUR LOCKER",new Vector2(768,56),new Vector2(760,60),40,Ink);
            Label("SATCHEL",new Vector2(392,287),new Vector2(380,44),32,new Color(.97f,.91f,.74f));
            safeTitle=Label("LOCKER",new Vector2(1074,290),new Vector2(390,45),30,Ink);
            for(int i=0;i<6;i++)Slot(InventoryContainer.Satchel,i,new Vector2(218+(i%3)*174,453+(i/3)*166),142);
            for(int i=0;i<9;i++)Slot(InventoryContainer.Locker,i,new Vector2(943+(i%3)*132,413+(i/3)*134),114);
            remoteCover=Rect("Held item paper",sheet,new Vector2(1144-768,512-518),new Vector2(760,780));
            var cover=remoteCover.gameObject.AddComponent<Image>();cover.sprite=slotPaper;cover.color=Color.white;
            var coverEdge=Rect("Drawn paper edges",remoteCover,Vector2.zero,remoteCover.sizeDelta);var ce=coverEdge.gameObject.AddComponent<SketchBorder>();ce.color=Ink;ce.raycastTarget=false;
            Slot(InventoryContainer.Use,0,new Vector2(392,790),142);usePanel=(RectTransform)slots[slots.Count-1].transform;
            useTitle=Label("IN HAND",new Vector2(213,790),new Vector2(160,50),30,Ink);
            var inspectRect=Rect("Inspected item",remoteCover,new Vector2(0,215),new Vector2(155,155));
            var iconRect=Rect("Item illustration",inspectRect,Vector2.zero,new Vector2(155,155));inspectedIcon=iconRect.gameObject.AddComponent<Image>();inspectedIcon.preserveAspect=true;inspectedIcon.raycastTarget=false;
            itemDetails=AddText(Rect("Item details",remoteCover,new Vector2(0,-55),new Vector2(560,350)),"",27,Ink);
            satchelCount=Label("CARRIED  0 / 6",new Vector2(392,910),new Vector2(550,35),24,Ink);
            lockerCount=Label("STORED  0 / 9",new Vector2(1074,910),new Vector2(550,35),24,Ink);
            message=Label("",new Vector2(768,964),new Vector2(1400,54),21,Ink);message.fontStyle=FontStyle.Normal;
            var close=Rect("Close locker",sheet,new Vector2(1404-768,512-56),new Vector2(210,55));
            var closeImage=close.gameObject.AddComponent<Image>();closeImage.sprite=slotPaper;
            var closeBorder=Rect("Drawn button edge",close,Vector2.zero,new Vector2(210,55));
            var border=closeBorder.gameObject.AddComponent<SketchBorder>();border.color=Ink;border.raycastTarget=false;
            var button=close.gameObject.AddComponent<Button>();button.targetGraphic=closeImage;button.onClick.AddListener(Close);
            var caption=Rect("Close caption",close,Vector2.zero,new Vector2(202,50));AddText(caption,"Close  [Tab]",22,Ink);
            ghostRect=Rect("Dragged item",sheet,Vector2.zero,new Vector2(118,118));ghost=ghostRect.gameObject.AddComponent<Image>();ghost.preserveAspect=true;ghost.raycastTarget=false;
            ghost.gameObject.SetActive(false);canvas.gameObject.SetActive(false);
        }
        void Slot(InventoryContainer container,int index,Vector2 pixel,float size)
        {
            var rect=Rect(container+" slot "+(index+1),sheet,new Vector2(pixel.x-768,512-pixel.y),Vector2.one*size);
            var background=rect.gameObject.AddComponent<Image>();background.sprite=slotPaper;background.color=new Color(1,1,1,.96f);
            var view=rect.gameObject.AddComponent<InventorySlotView>();view.owner=this;view.container=container;view.index=index;
            var borderRect=Rect("Drawn edges",rect,Vector2.zero,Vector2.one*size);view.border=borderRect.gameObject.AddComponent<SketchBorder>();view.border.raycastTarget=false;
            var iconRect=Rect("Item icon",rect,new Vector2(0,8),new Vector2(size-30,size-38));view.icon=iconRect.gameObject.AddComponent<Image>();view.icon.preserveAspect=true;view.icon.raycastTarget=false;
            var nameRect=Rect("Item name",rect,new Vector2(0,-size*.5f+17),new Vector2(size-10,25));view.caption=AddText(nameRect,"",size>130?19:16,Ink);
            slots.Add(view);
        }
        Text Label(string value,Vector2 pixel,Vector2 size,int fontSize,Color color)=>AddText(Rect(value,sheet,new Vector2(pixel.x-768,512-pixel.y),size),value,fontSize,color);
        static Text AddText(RectTransform rect,string value,int size,Color color)
        {
            var t=rect.gameObject.AddComponent<Text>();t.font=SchoolTypography.Font;t.fontSize=size;t.fontStyle=FontStyle.Bold;
            t.text=value;t.color=color;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;return t;
        }
        static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));var rt=(RectTransform)go.transform;rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.anchoredPosition=position;rt.sizeDelta=size;return rt;
        }
    }
}

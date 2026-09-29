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
        // Satchel mode's right-hand page: belongings and pockets, or the codex. Every square has a hover tooltip.
        ItemTooltip tooltip;
        RectTransform pocketsPage,codexPage;
        readonly List<(ItemCell cell,string id)> belongingCells=new(),pocketCells=new(),codexCells=new();
        Text pocketsEmpty,codexButton,codexCount;
        static readonly string[] PocketItems={Items.Duck,Items.Glue,Items.Sweets,Items.BoltCutters,Items.StoreKey};
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
            IsOpen=true;ContextualControlHints.Used(ContextualControlHints.Action.Bag);openedFrame=Time.frameCount;canvas.gameObject.SetActive(true);
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
            IsOpen=false;CancelSource();if(tooltip!=null)tooltip.Hide();
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
            ShowCodex(false);pocketsPage.gameObject.SetActive(!local);
        }
        void Refresh()
        {
            foreach(var slot in slots)slot.Refresh();
            int a=0,b=0;
            for(int i=0;i<6;i++)if(Inventory.Get(InventoryContainer.Satchel,i)!=null)a++;
            for(int i=0;i<9;i++)if(Inventory.Get(InventoryContainer.Locker,i)!=null)b++;
            satchelCount.text="CARRIED  "+a+" / 6";lockerCount.text="STORED  "+b+" / 9";
            RefreshPockets();
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
        void Inspect(InventoryEntry entry){}
        /// <summary>Satchel, locker and hand squares: hover shows what the item is.</summary>
        public void Hover(InventorySlotView view,bool on)
        {
            if(tooltip==null)return;
            var entry=on?view.Entry:null;
            if(entry==null){tooltip.Hide();return;}
            var item=ItemCatalogue.Get(Items.For(entry.definition.kind));
            if(item!=null)tooltip.Show(item);else tooltip.Show(entry.definition.displayName,entry.definition.description,null);
        }
        void ShowCodex(bool on)
        {
            if(codexPage==null)return;
            codexPage.gameObject.SetActive(on);pocketsPage.gameObject.SetActive(!on&&!HasLockerAccess);
            if(tooltip!=null)tooltip.Hide();RefreshPockets();
        }
        void RefreshPockets()
        {
            if(pocketsPage==null||player==null)return;
            var run=SchoolRunController.Instance;
            for(int i=0;i<belongingCells.Count;i++)
            {
                var (cell,id)=belongingCells[i];
                cell.Set(ItemCatalogue.Get(id),run!=null&&run.Has(i),1);
            }
            // Pockets: only what you're carrying, packed from the left.
            int shown=0;
            foreach(var (cell,id) in pocketCells)
            {
                int amount=Amount(id);bool have=amount>0;cell.gameObject.SetActive(have);
                if(!have)continue;
                cell.Set(ItemCatalogue.Get(id),true,amount);
                ((RectTransform)cell.transform).anchoredPosition=new Vector2(-280+shown*140,-10);shown++;
            }
            pocketsEmpty.gameObject.SetActive(shown==0);
            int known=ItemCodex.KnownCount,total=ItemCatalogue.All.Count;
            codexButton.text="CODEX   "+known+" / "+total;codexCount.text="Found "+known+" of "+total;
            foreach(var (cell,id) in codexCells){bool k=ItemCodex.Known(id);cell.Set(ItemCatalogue.Get(id),k,1,k);}
        }
        int Amount(string id)
        {
            var run=SchoolRunController.Instance;
            return id switch
            {
                Items.Duck=>player.GetComponent<ClockworkDecoy>()?.Charges??0,
                Items.Glue=>player.GetComponent<GlueDeployer>()?.Charges??0,
                Items.Sweets=>player.GetComponent<Sweets>()?.Count??0,
                Items.BoltCutters=>run!=null&&run.HasBoltCutters?1:0,
                Items.StoreKey=>run!=null&&run.HasStoreKey?1:0,
                _=>0
            };
        }
        void BuildPages(RectTransform page)
        {
            pocketsPage=Rect("Belongings and pockets",page,Vector2.zero,page.sizeDelta);
            AddText(Rect("Belongings title",pocketsPage,new Vector2(0,320),new Vector2(700,44)),"BELONGINGS",30,Ink);
            for(int i=0;i<Items.Belongings.Length;i++)
            {
                string id=Items.Belongings[i];int index=i;
                var cell=Cell(pocketsPage,new Vector2(-280+i*140,215),118,false);
                cell.tip=()=>{var item=ItemCatalogue.Get(id);bool have=SchoolRunController.Instance!=null&&SchoolRunController.Instance.Has(index);
                    if(item==null)return null;
                    return have?(item.name,item.blurb,item.use):(item.name,"Still confiscated. It's in a box somewhere in school.",null);};
                belongingCells.Add((cell,id));
            }
            AddText(Rect("Pockets title",pocketsPage,new Vector2(0,95),new Vector2(700,44)),"POCKETS",30,Ink);
            foreach(string id in PocketItems)
            {
                var cell=Cell(pocketsPage,Vector2.zero,118,false);string key=id;
                cell.tip=()=>{var i=ItemCatalogue.Get(key);if(i==null)return null;return (i.name,i.blurb,i.use);};
                pocketCells.Add((cell,id));
            }
            pocketsEmpty=AddText(Rect("Pockets empty",pocketsPage,new Vector2(0,-10),new Vector2(600,60)),"Nothing yet.",24,new Color(.12f,.17f,.26f,.55f));
            pocketsEmpty.fontStyle=FontStyle.Italic;
            codexButton=Button(pocketsPage,new Vector2(0,-300),new Vector2(320,62),()=>ShowCodex(true));

            codexPage=Rect("Codex",page,Vector2.zero,page.sizeDelta);
            AddText(Rect("Codex title",codexPage,new Vector2(0,330),new Vector2(700,44)),"CODEX",32,Ink);
            codexCount=AddText(Rect("Codex count",codexPage,new Vector2(0,290),new Vector2(700,32)),"",21,Ink);codexCount.fontStyle=FontStyle.Normal;
            var all=ItemCatalogue.All;
            for(int i=0;i<all.Count;i++)
            {
                string id=all[i].id;
                var cell=Cell(codexPage,new Vector2(-280+(i%5)*140,195-(i/5)*155),104,true);
                cell.tip=()=>{var item=ItemCatalogue.Get(id);if(item==null)return null;
                    return ItemCodex.Known(id)?(item.name,item.blurb,item.use):("???","Not found yet.",null);};
                codexCells.Add((cell,id));
            }
            var back=Button(codexPage,new Vector2(0,-300),new Vector2(220,62),()=>ShowCodex(false));back.text="BACK";
            codexPage.gameObject.SetActive(false);
        }
        ItemCell Cell(RectTransform parent,Vector2 position,float size,bool named)
        {
            var rect=Rect("Item square",parent,position,Vector2.one*size);
            var background=rect.gameObject.AddComponent<Image>();background.sprite=slotPaper;background.color=new Color(1,1,1,.96f);
            var cell=rect.gameObject.AddComponent<ItemCell>();cell.tooltip=tooltip;
            cell.border=Rect("Drawn edges",rect,Vector2.zero,Vector2.one*size).gameObject.AddComponent<SketchBorder>();cell.border.raycastTarget=false;cell.border.color=new Color(.13f,.18f,.29f,.86f);
            var art=Rect("Picture",rect,Vector2.zero,Vector2.one*(size-22));cell.picture=art.gameObject.AddComponent<Image>();cell.picture.preserveAspect=true;cell.picture.raycastTarget=false;
            cell.count=AddText(Rect("Count",rect,new Vector2(size*.5f-24,-size*.5f+16),new Vector2(48,28)),"",20,Ink);cell.count.alignment=TextAnchor.MiddleRight;
            if(named){cell.caption=AddText(Rect("Name",rect,new Vector2(0,-size*.5f-16),new Vector2(size+30,26)),"",15,Ink);cell.caption.fontStyle=FontStyle.Normal;}
            cell.unknown=AddText(Rect("Unknown",rect,Vector2.zero,Vector2.one*size),"?",54,new Color(.12f,.17f,.26f,.35f));cell.unknown.gameObject.SetActive(false);
            return cell;
        }
        Text Button(RectTransform parent,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction onClick)
        {
            var rect=Rect("Button",parent,position,size);
            var image=rect.gameObject.AddComponent<Image>();image.sprite=slotPaper;
            var edge=Rect("Drawn button edge",rect,Vector2.zero,size).gameObject.AddComponent<SketchBorder>();edge.color=Ink;edge.raycastTarget=false;
            var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.onClick.AddListener(onClick);
            return AddText(Rect("Caption",rect,Vector2.zero,size-new Vector2(8,6)),"",24,Ink);
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
            tooltip=ItemTooltip.Create((RectTransform)go.transform);
            BuildPages(remoteCover);
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
            ghost.gameObject.SetActive(false);tooltip.transform.SetAsLastSibling();canvas.gameObject.SetActive(false);
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

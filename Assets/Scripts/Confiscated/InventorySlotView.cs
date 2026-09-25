using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace Confiscated
{
    public class InventorySlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public LockerStorageUI owner;
        public InventoryContainer container;
        public int index;
        public Image icon;
        public Text caption;
        public SketchBorder border;
        bool hovered;
        public InventoryEntry Entry=>owner.Inventory?.Get(container,index);
        public void Refresh()
        {
            var entry=Entry;icon.gameObject.SetActive(entry!=null);icon.sprite=entry?.definition.icon;
            icon.color=new Color(1,1,1,owner.Source==this ? 0 : 1f);
            caption.text=entry==null?"":entry.definition.displayName;
            border.color=hovered||owner.Source==this?new Color(.62f,.38f,.10f):new Color(.13f,.18f,.29f,.86f);
        }
        public void OnBeginDrag(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)owner.BeginDrag(this,e.position);}
        public void OnDrag(PointerEventData e)=>owner.DragTo(e.position);
        public void OnEndDrag(PointerEventData e)=>owner.EndDrag();
        public void OnDrop(PointerEventData e)=>owner.DropOn(this);
        public void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)owner.Click(this);}
        public void OnPointerEnter(PointerEventData e){hovered=true;Refresh();}
        public void OnPointerExit(PointerEventData e){hovered=false;Refresh();}
    }
}

using System;
using System.Linq;
using UnityEngine;
namespace Confiscated
{
    public enum InventoryContainer { Satchel, Locker, Use }
    [Serializable] public class InventoryEntry
    {
        public InventoryItemDefinition definition;
        public ThrowableBall ball;
    }
    /// <summary>One owner for carried and safe items. Slot moves commit atomically and never clone entries.</summary>
    [DisallowMultipleComponent]
    public class PlayerInventory : MonoBehaviour
    {
        public InventoryItemDefinition phoneItem, footballItem, keyItem;
        readonly InventoryEntry[] satchel=new InventoryEntry[6];
        readonly InventoryEntry[] locker=new InventoryEntry[9];
        readonly InventoryEntry[] use=new InventoryEntry[1];
        public InventoryEntry Equipped=>use[0];
        public bool IsEquipped(InventoryItemKind kind)=>Equipped?.definition.kind==kind;
        public bool HasCarried(InventoryItemKind kind)=>Contains(InventoryContainer.Satchel,kind)||Contains(InventoryContainer.Use,kind);
        void Awake(){if(GetComponent<EquippedItemView>()==null)gameObject.AddComponent<EquippedItemView>();if(GetComponent<QuickAccessBar>()==null)gameObject.AddComponent<QuickAccessBar>();}
        public event Action Changed;
        PlayerInteractor Player=>GetComponent<PlayerInteractor>();
        public bool PhoneStored=>Contains(InventoryContainer.Locker,InventoryItemKind.Phone);
        public int Capacity(InventoryContainer container)=>Slots(container).Length;
        public InventoryEntry Get(InventoryContainer container,int index)=>index>=0&&index<Capacity(container)?Slots(container)[index]:null;
        InventoryEntry[] Slots(InventoryContainer c)=>c==InventoryContainer.Satchel?satchel:c==InventoryContainer.Locker?locker:use;
        public bool Contains(InventoryContainer c,InventoryItemKind kind)=>Slots(c).Any(e=>e!=null&&e.definition.kind==kind);
        public int Find(InventoryContainer c,InventoryItemKind kind)=>Array.FindIndex(Slots(c),e=>e!=null&&e.definition.kind==kind);
        public bool HasRoom=>Array.Exists(satchel,e=>e==null);
        public bool StockLocker(InventoryItemDefinition definition)
        {
            if(definition==null||Contains(InventoryContainer.Locker,definition.kind))return false;
            int slot=Array.FindIndex(locker,e=>e==null);if(slot<0)return false;
            locker[slot]=new InventoryEntry{definition=definition};Changed?.Invoke();return true;
        }
        public bool ToggleFootball()
        {
            if(Equipped?.ball!=null)
            {
                int empty=Array.FindIndex(satchel,e=>e==null);
                if(empty<0){HudController.Instance?.SetStatus("Satchel full. Throw the football or store something in your locker.",3);return false;}
                return Move(InventoryContainer.Use,0,InventoryContainer.Satchel,empty,out _);
            }
            int ball=Array.FindIndex(satchel,e=>e?.ball!=null);
            return ball>=0&&Move(InventoryContainer.Satchel,ball,InventoryContainer.Use,0,out _);
        }
        public bool CanCollect(InventoryItemDefinition definition,ThrowableBall ball=null)
        {
            if(definition==null)return false;
            bool same=satchel.Concat(locker).Concat(use).Any(e=>e!=null&&e.definition==definition&&(ball==null||e.ball==ball));
            return same||HasRoom;
        }
        public bool Collect(InventoryItemDefinition definition,ThrowableBall ball=null)
        {
            if(definition==null)return false;
            if(satchel.Concat(locker).Concat(use).Any(e=>e!=null&&e.definition==definition&&(ball==null||e.ball==ball)))return true;
            int index=Array.FindIndex(satchel,e=>e==null);
            if(index<0){HudController.Instance?.SetStatus("Your satchel is full.",3f);return false;}
            satchel[index]=new InventoryEntry{definition=definition,ball=ball};Changed?.Invoke();return true;
        }
        public bool Move(InventoryContainer from,int source,InventoryContainer to,int target,out string message)
        {
            message="";
            if(source<0||source>=Capacity(from)||target<0||target>=Capacity(to)){message="Choose an inventory square.";return false;}
            var a=Slots(from);var b=Slots(to);var item=a[source];
            if(item==null){message="That square is empty.";return false;}
            if(from==to&&source==target)return true;
            var other=b[target];a[source]=other;b[target]=item;
            foreach(var e in satchel.Concat(locker))if(e?.ball!=null&&e.ball.gameObject.activeSelf)e.ball.StoreInLocker();
            if(Equipped?.ball!=null&&Player.HeldBall!=Equipped.ball)Equipped.ball.Equip(Player);
            bool phone=HasCarried(InventoryItemKind.Phone);
            if(item.definition.kind==InventoryItemKind.Phone||other?.definition.kind==InventoryItemKind.Phone)
                GameManager.Instance?.SetPhoneLocation(phone,PhoneStored);
            if(item.definition.kind==InventoryItemKind.OfficeKey||other?.definition.kind==InventoryItemKind.OfficeKey)
                GameManager.Instance?.officeMission?.SetKeyLocation(HasCarried(InventoryItemKind.OfficeKey),Contains(InventoryContainer.Locker,InventoryItemKind.OfficeKey));
            message=to==InventoryContainer.Use?"Holding "+item.definition.displayName+".":from==to?"Moved "+item.definition.displayName+".":to==InventoryContainer.Locker?
                item.definition.displayName+" stored.":item.definition.displayName+" moved to your satchel.";
            Changed?.Invoke();return true;
        }
        public void RemoveCarriedPhone()
        {
            foreach(var slots in new[]{satchel,use})for(int i=0;i<slots.Length;i++)if(slots[i]?.definition.kind==InventoryItemKind.Phone)slots[i]=null;
            Changed?.Invoke();
        }
        public void RemovePhoneEverywhere()
        {
            foreach(var slots in new[]{satchel,locker,use})for(int i=0;i<slots.Length;i++)if(slots[i]?.definition.kind==InventoryItemKind.Phone)slots[i]=null;
            Changed?.Invoke();
        }
        public void RemoveCarried(InventoryItemKind kind)
        {
            foreach(var slots in new[]{satchel,use})for(int i=0;i<slots.Length;i++)if(slots[i]?.definition.kind==kind)slots[i]=null;
            Changed?.Invoke();
        }
        public void RemoveCarriedBall(ThrowableBall ball)
        {
            foreach(var slots in new[]{satchel,use})for(int i=0;i<slots.Length;i++)if(slots[i]!=null&&slots[i].ball==ball)slots[i]=null;
            Changed?.Invoke();
        }
    }
}

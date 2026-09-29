using System;
using System.Collections.Generic;
using UnityEngine;

namespace Confiscated
{
    /// <summary>
    /// Every item the player can pick up: its picture and a short, plain line saying what it is. The inventory tooltips,
    /// the codex and the new-item pop-up all read from here. Built by Confiscated/Items/Build Item Catalogue
    /// (ItemCatalogueSetup) into Resources/ItemCatalogue.
    /// </summary>
    [CreateAssetMenu(menuName="Confiscated/Item catalogue")]
    public sealed class ItemCatalogue : ScriptableObject
    {
        [Serializable] public sealed class Item
        {
            public string id,name;
            public Sprite picture;
            [TextArea] public string blurb;
            [Tooltip("How to use it, as a key hint. Empty if it's used with F where it matters.")]
            public string use;
        }
        public Item[] items;

        static ItemCatalogue instance;
        public static ItemCatalogue Instance=>instance!=null?instance:instance=Resources.Load<ItemCatalogue>("ItemCatalogue");
        public static IReadOnlyList<Item> All=>Instance!=null?Instance.items:Array.Empty<Item>();
        public static Item Get(string id)
        {
            if(Instance==null||string.IsNullOrEmpty(id))return null;
            foreach(var item in Instance.items)if(item.id==id)return item;
            return null;
        }
    }

    /// <summary>Item ids shared by the pickups, the catalogue and the codex.</summary>
    public static class Items
    {
        public const string Phone="phone",YoYo="yoyo",Handheld="handheld",Skateboard="skateboard",Robot="robot",
            Duck="duck",Glue="glue",Sweets="sweets",Football="football",BoltCutters="boltcutters",StoreKey="storekey",
            OfficeKey="officekey",HallPass="hallpass",Newsletters="newsletters",Torch="torch";
        /// <summary>The five confiscated belongings, in SchoolRunController recovery-id order.</summary>
        public static readonly string[] Belongings={Phone,YoYo,Handheld,Skateboard,Robot};
        public static string For(InventoryItemKind kind)=>kind switch
        {
            InventoryItemKind.Phone=>Phone,InventoryItemKind.Football=>Football,InventoryItemKind.OfficeKey=>OfficeKey,
            InventoryItemKind.HallPass=>HallPass,InventoryItemKind.Newsletters=>Newsletters,InventoryItemKind.Torch=>Torch,_=>null
        };
    }

    /// <summary>Which items the player has ever found (kept between sessions), and the pop-up when they pick one up.</summary>
    public static class ItemCodex
    {
        const string Key="Confiscated.Codex.v1.";
        public static bool Known(string id)=>!string.IsNullOrEmpty(id)&&PlayerPrefs.GetInt(Key+id,0)==1;
        public static int KnownCount{get{int n=0;foreach(var item in ItemCatalogue.All)if(Known(item.id))n++;return n;}}
        /// <summary>A pickup from the world: the full picture-and-explanation card the first time ever, a small toast after.</summary>
        public static void PickUp(string id)
        {
            var item=ItemCatalogue.Get(id);if(item==null)return;
            bool first=!Known(id);
            if(first){PlayerPrefs.SetInt(Key+id,1);PlayerPrefs.Save();}
            ItemPopup.Show(item,first);
        }
        /// <summary>Items that arrive without being picked up (moved out of the locker, handed over): only new ones pop up.</summary>
        public static void Notice(string id){if(!Known(id))PickUp(id);}
#if UNITY_EDITOR
        public static void ForgetAll(){foreach(var item in ItemCatalogue.All)PlayerPrefs.DeleteKey(Key+item.id);}
#endif
    }
}

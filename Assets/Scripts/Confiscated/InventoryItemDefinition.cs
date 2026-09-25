using UnityEngine;
namespace Confiscated
{
    public enum InventoryItemKind { Phone, Football, OfficeKey, Other, HallPass, Newsletters, Torch }
    [CreateAssetMenu(menuName="Confiscated/Inventory item")]
    public class InventoryItemDefinition : ScriptableObject
    {
        public string displayName;
        public InventoryItemKind kind;
        public Sprite icon;
        [TextArea] public string description;
    }
}

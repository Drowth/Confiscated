using UnityEngine;
namespace Confiscated
{
    /// <summary>The player's bag of sweets: each one buys a quiet pass by the chatterbox (he munches instead of talking).</summary>
    public sealed class Sweets : MonoBehaviour
    {
        public const int BagSize=3;
        public int Count {get;private set;}
        public static Sweets On(Component player){var s=player.GetComponent<Sweets>();return s!=null?s:player.gameObject.AddComponent<Sweets>();}
        public void Collect(int amount){Count+=amount;ItemCodex.PickUp(Items.Sweets);}
        public bool Spend(){if(Count<=0)return false;Count--;return true;}
        public void ResetRun()=>Count=0;
    }
}

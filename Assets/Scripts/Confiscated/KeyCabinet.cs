using UnityEngine;
namespace Confiscated
{
    /// <summary>
    /// The school office key cabinet: ten keys on hooks, each with a small handwritten tag. Which hook holds the STORE key is
    /// shuffled every run, so the player has to lean in and read the tags while the caretaker hunts. Lifting the wrong key
    /// jangles the whole bunch: a noise he comes to investigate.
    /// </summary>
    public sealed class KeyCabinet : MonoBehaviour
    {
        public const string StoreLabel="STORE";
        public static readonly string[] Labels={StoreLabel,"PE SHED","BOILER","KITCHEN","HALL","MINIBUS","LIBRARY","ROOF","GATES","STAFF"};
        public KeyHook[] hooks;
        /// <summary>A Tripo key per label, same order as Labels; null where there is none (that hook keeps its drawn key and tag).</summary>
        public Transform[] labelModels;
        public float jingleRadius=18;
        public KeyHook StoreHook {get;private set;}
        public int WrongPicks {get;private set;}
        void Start(){Shuffle();}
        public void Shuffle()
        {
            var order=(string[])Labels.Clone();
            for(int i=order.Length-1;i>0;i--){int j=Random.Range(0,i+1);(order[i],order[j])=(order[j],order[i]);}
            for(int i=0;i<hooks.Length;i++){hooks[i].SetLabel(order[i%order.Length]);hooks[i].ShowModel(Model(hooks[i].Label));if(hooks[i].Label==StoreLabel)StoreHook=hooks[i];}
        }
        // The model carries the label painted on its tag, so it moves to whichever hook the shuffle gave that label.
        Transform Model(string label){int i=System.Array.IndexOf(Labels,label);return labelModels!=null&&i>=0&&i<labelModels.Length?labelModels[i]:null;}
        internal void WrongKey(KeyHook hook)
        {
            WrongPicks++;
            TempAudio.PlayAt(TempAudio.Jingle,hook.transform.position,.9f);
            // Emit first: the caretaker's own "He heard something..." status must not replace this line.
            NoiseEvents.Emit(hook.transform.position,jingleRadius,"key jingle");
            HudController.Instance?.SetStatus("That's the "+hook.Label+" key. The whole bunch jangles!",2.5f);
        }
    }
}

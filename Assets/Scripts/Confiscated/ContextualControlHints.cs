using UnityEngine;
using UnityEngine.UI;

namespace Confiscated
{
    /// <summary>Each successfully used control gets a short grace period, then fades. Unused controls remain readable.</summary>
    public sealed class ContextualControlHints : MonoBehaviour
    {
        public enum Action { Interact, Lean, Toy, Run, LookBack, Torch, Bag, Glue }
        static readonly bool[] learned=new bool[8];
        const string AlwaysKey="Confiscated.AlwaysShowHints";
        public static bool AlwaysShow {get=>PlayerPrefs.GetInt(AlwaysKey,0)!=0;set=>PlayerPrefs.SetInt(AlwaysKey,value?1:0);}
        readonly CanvasGroup[] hints=new CanvasGroup[8];
        readonly float[] shown=new float[8];
        readonly float[] sinceLearned=new float[8];
        public static void Used(Action action)=>learned[(int)action]=true;
        public static bool Learned(Action action)=>learned[(int)action];
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){System.Array.Clear(learned,0,learned.Length);}
        public void Build()
        {
            var layout=gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=14;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=layout.childForceExpandHeight=false;
            string[] keys={"F LMB","Q E","1 RMB","SHIFT","SPACE","T","TAB","2 G"};
            string[] labels={"interact","lean","toy","run","look back","torch","bag","glue"};
            for(int i=0;i<8;i++)
            {
                var row=KeyCapHints.Row(transform,30,18,new Color(.93f,.91f,.78f),(keys[i],labels[i]));
                row.name="Hint "+(Action)i;hints[i]=row.gameObject.AddComponent<CanvasGroup>();hints[i].blocksRaycasts=false;
            }
        }
        void Update()
        {
            if(Time.timeScale<=0||PauseMenu.IsOpen||ComicDialogue.IsActive)return;
            var player=SchoolRunController.Instance?.period?.Player;
            for(int i=0;i<hints.Length;i++)
            {
                if(hints[i]==null)continue;
                // Only count time while the strip is actually visible; lessons and the pause screen don't consume it.
                bool available=true;
                if(player!=null&&!AlwaysShow)
                {
                    if(i==(int)Action.Toy)available=(player.GetComponent<ClockworkDecoy>()?.Charges??0)>0;
                    if(i==(int)Action.Glue)available=(player.GetComponent<GlueDeployer>()?.Charges??0)>0;
                    if(i==(int)Action.Torch)available=player.GetComponent<PlayerTorch>()?.HasTorch??false;
                }
                if(available){shown[i]+=Time.deltaTime;if(learned[i])sinceLearned[i]+=Time.deltaTime;}
                float target=AlwaysShow||(available&&(!learned[i]||shown[i]<8||sinceLearned[i]<3))?1:0;
                hints[i].alpha=Mathf.MoveTowards(hints[i].alpha,target,Time.deltaTime*1.5f);
                var layout=hints[i].GetComponent<LayoutElement>();
                if(layout==null)layout=hints[i].gameObject.AddComponent<LayoutElement>();
                layout.ignoreLayout=hints[i].alpha<=0;
            }
        }
    }
}

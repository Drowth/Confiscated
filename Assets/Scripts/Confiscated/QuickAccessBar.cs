using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Confiscated
{
    /// <summary>Direct access to optional distractions. Keys, passes and deliveries use E in the world.</summary>
    public sealed class QuickAccessBar : MonoBehaviour
    {
        PlayerInventory inventory;
        PlayerInteractor player;
        ClockworkDecoy decoy;
        GlueDeployer glue;
        GameObject root;
        Text toyText,glueText,sweetsText;
        Image toyPanel,gluePanel,glueIcon,sweetsPanel;
        bool hadToy,hadGlue,hadSweets;
        void Awake(){inventory=GetComponent<PlayerInventory>();player=GetComponent<PlayerInteractor>();}
        void Update()
        {
            var game=GameManager.Instance;
            bool visible=game!=null&&game.IsPlaying&&!player.InputLocked&&!ComicDialogue.IsActive&&Cursor.lockState==CursorLockMode.Locked;
            if(root==null&&visible&&HudController.Instance!=null)Build();
            if(root!=null)root.SetActive(visible);
            if(!visible)return;
            if(root==null)return;
            if(decoy==null)decoy=GetComponent<ClockworkDecoy>();
            if(glue==null)glue=GetComponent<GlueDeployer>();
            var sweets=GetComponent<Sweets>();
            int toys=decoy!=null?decoy.Charges:0,glues=glue!=null?glue.Charges:0,bag=sweets!=null?sweets.Count:0;
            // A card appears the first time its item is picked up, then stays (greyed once used up) for the rest of the run.
            hadToy|=toys>0;hadGlue|=glues>0;hadSweets|=bag>0;
            toyText.text="[1] WIND-UP TOY\n"+(toys>0?"Place distraction  x"+toys:"None left");
            toyPanel.color=new Color(.94f,.91f,.79f,toys>0?.9f:.55f);
            glueText.text="[2 / G] GLUE\n"+(glues>0?"Drop trap  x"+glues:"Used up");
            gluePanel.color=new Color(.94f,.91f,.79f,glues>0?.9f:.55f);
            glueIcon.sprite=glue!=null?glue.icon:null;glueIcon.enabled=glueIcon.sprite!=null;
            sweetsText.text="SWEETS\n"+(bag>0?"For the chatterbox  x"+bag:"All given out");
            sweetsPanel.color=new Color(.94f,.91f,.79f,bag>0?.9f:.55f);
            // Shown cards pack against the right edge, in their usual order.
            float x=694-226;
            foreach(var (panel,shown) in new[]{(sweetsPanel,hadSweets),(gluePanel,hadGlue),(toyPanel,hadToy)})
            {
                if(panel.gameObject.activeSelf!=shown)panel.gameObject.SetActive(shown);
                if(shown){panel.rectTransform.anchoredPosition=new Vector2(x,0);x-=234;}
            }
        }
        void Build()
        {
            var parent=HudController.Instance.GetComponentInParent<Canvas>().transform;
            root=new GameObject("Quick access tools",typeof(RectTransform));root.transform.SetParent(parent,false);
            var rect=root.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.right;rect.anchoredPosition=new Vector2(-24,24);rect.sizeDelta=new Vector2(694,80);
            toyPanel=Card("Wind-up toy",new Vector2(0,0),out toyText);
            gluePanel=Card("Glue",new Vector2(234,0),out glueText);
            sweetsPanel=Card("Sweets",new Vector2(468,0),out sweetsText);
            var glueImage=new GameObject("Glue icon",typeof(RectTransform),typeof(Image));glueImage.transform.SetParent(gluePanel.transform,false);
            var gr=glueImage.GetComponent<RectTransform>();gr.anchorMin=gr.anchorMax=gr.pivot=new Vector2(0,.5f);gr.anchoredPosition=new Vector2(8,0);gr.sizeDelta=new Vector2(34,46);
            glueIcon=glueImage.GetComponent<Image>();glueIcon.preserveAspect=true;glueIcon.raycastTarget=false;glueText.rectTransform.offsetMin=new Vector2(44,4);
        }
        Image Card(string name,Vector2 position,out Text text)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(root.transform,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=position;rect.sizeDelta=new Vector2(226,80);
            var image=go.GetComponent<Image>();image.raycastTarget=false;
            var edge=new GameObject("Pencil edge",typeof(RectTransform),typeof(SketchBorder));edge.transform.SetParent(go.transform,false);
            Stretch(edge.GetComponent<RectTransform>());edge.GetComponent<SketchBorder>().raycastTarget=false;edge.GetComponent<SketchBorder>().color=new Color(.1f,.14f,.12f);
            var label=new GameObject("Tool hint",typeof(RectTransform),typeof(Text));label.transform.SetParent(go.transform,false);Stretch(label.GetComponent<RectTransform>());
            text=label.GetComponent<Text>();text.font=SchoolTypography.Font;text.fontSize=19;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(.08f,.12f,.1f);text.raycastTarget=false;
            return image;
        }
        static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(6,4);rect.offsetMax=new Vector2(-6,-4);}
        void OnDestroy(){if(root!=null)Destroy(root);}
    }
}

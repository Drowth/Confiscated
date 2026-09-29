using UnityEngine;
using UnityEngine.UI;

namespace Confiscated
{
    /// <summary>
    /// Small hotbar, bottom right: a picture square for each usable thing you're carrying (wind-up duck, glue, sweets),
    /// with its count and key. A square appears the first time the item is picked up and stays for the run, greyed
    /// when used up. What each one is lives in the TAB inventory's tooltips and the codex, not here.
    /// </summary>
    public sealed class QuickAccessBar : MonoBehaviour
    {
        PlayerInteractor player;
        ClockworkDecoy decoy;
        GlueDeployer glue;
        GameObject root;
        readonly Slot[] slots=new Slot[3];
        static readonly Color Paper=new(.94f,.91f,.79f),Ink=new(.08f,.12f,.1f);
        const float Size=78,Gap=10;
        sealed class Slot{public string id;public Image panel,picture;public Text count;public bool had;}

        void Awake(){player=GetComponent<PlayerInteractor>();}
        void Update()
        {
            var game=GameManager.Instance;
            bool visible=game!=null&&game.IsPlaying&&!player.InputLocked&&!ComicDialogue.IsActive&&Cursor.lockState==CursorLockMode.Locked;
            if(root==null&&visible&&HudController.Instance!=null)Build();
            if(root!=null)root.SetActive(visible);
            if(!visible||root==null)return;
            if(decoy==null)decoy=GetComponent<ClockworkDecoy>();
            if(glue==null)glue=GetComponent<GlueDeployer>();
            var sweets=GetComponent<Sweets>();
            int[] amounts={decoy!=null?decoy.Charges:0,glue!=null?glue.Charges:0,sweets!=null?sweets.Count:0};
            // Shown squares pack against the right edge, in their usual order.
            float x=0;
            for(int i=slots.Length-1;i>=0;i--)
            {
                var s=slots[i];int n=amounts[i];s.had|=n>0;
                if(s.panel.gameObject.activeSelf!=s.had)s.panel.gameObject.SetActive(s.had);
                if(!s.had)continue;
                s.panel.rectTransform.anchoredPosition=new Vector2(x,0);x-=Size+Gap;
                if(s.picture.sprite==null){s.picture.sprite=ItemCatalogue.Get(s.id)?.picture;s.picture.enabled=s.picture.sprite!=null;}
                s.picture.color=n>0?Color.white:new Color(1,1,1,.3f);
                s.panel.color=new Color(Paper.r,Paper.g,Paper.b,n>0?.92f:.5f);
                s.count.text=n>1?"x"+n:n==0?"0":"";
            }
        }
        void Build()
        {
            var parent=HudController.Instance.GetComponentInParent<Canvas>().transform;
            root=new GameObject("Quick access tools",typeof(RectTransform));root.transform.SetParent(parent,false);
            var rect=root.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.right;rect.anchoredPosition=new Vector2(-24,24);rect.sizeDelta=new Vector2(Size,Size);
            slots[0]=Square(Items.Duck,"1");slots[1]=Square(Items.Glue,"2");slots[2]=Square(Items.Sweets,null);
        }
        Slot Square(string id,string key)
        {
            var s=new Slot{id=id};
            var go=new GameObject(id,typeof(RectTransform),typeof(Image));go.transform.SetParent(root.transform,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.right;rect.sizeDelta=new Vector2(Size,Size);
            s.panel=go.GetComponent<Image>();s.panel.raycastTarget=false;
            var edge=new GameObject("Pencil edge",typeof(RectTransform),typeof(SketchBorder));edge.transform.SetParent(go.transform,false);
            Stretch(edge.GetComponent<RectTransform>(),0);edge.GetComponent<SketchBorder>().raycastTarget=false;edge.GetComponent<SketchBorder>().color=new Color(.1f,.14f,.12f);
            var art=new GameObject("Picture",typeof(RectTransform),typeof(Image));art.transform.SetParent(go.transform,false);
            Stretch(art.GetComponent<RectTransform>(),9);s.picture=art.GetComponent<Image>();s.picture.preserveAspect=true;s.picture.raycastTarget=false;
            s.count=Label(go.transform,new Vector2(1,0),new Vector2(-6,4),20,TextAnchor.LowerRight);
            if(key!=null)Label(go.transform,new Vector2(0,1),new Vector2(6,-4),18,TextAnchor.UpperLeft).text=key;
            go.SetActive(false);
            return s;
        }
        static Text Label(Transform parent,Vector2 corner,Vector2 offset,int size,TextAnchor align)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=corner;r.anchoredPosition=offset;r.sizeDelta=new Vector2(50,26);
            var t=go.GetComponent<Text>();t.font=SchoolTypography.Font;t.fontSize=size;t.fontStyle=FontStyle.Bold;t.alignment=align;t.color=Ink;t.raycastTarget=false;return t;
        }
        static void Stretch(RectTransform rect,float inset){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(inset,inset);rect.offsetMax=new Vector2(-inset,-inset);}
        void OnDestroy(){if(root!=null)Destroy(root);}
    }
}

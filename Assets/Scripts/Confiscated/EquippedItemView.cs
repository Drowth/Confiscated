using UnityEngine;
namespace Confiscated
{
    /// <summary>Shows the selected key at the existing hand anchor using its original illustrated meshes.</summary>
    public class EquippedItemView : MonoBehaviour
    {
        PlayerInventory inventory;
        PlayerInteractor player;
        GameObject keyView;
        GameObject paperView;
        UnityEngine.UI.Text paperText;
        void Awake(){inventory=GetComponent<PlayerInventory>();player=GetComponent<PlayerInteractor>();}
        void LateUpdate()
        {
            bool visible=inventory.IsEquipped(InventoryItemKind.OfficeKey);
            var gm=GameManager.Instance;
            visible&=gm!=null&&(gm.IsPlaying||gm.Current==GameManager.State.Detention)&&!(gm.lockerUI!=null&&gm.lockerUI.IsOpen)&&!(gm.detention!=null&&gm.detention.MinigameOpen);
            if(visible&&keyView==null&&player.HoldAnchor!=null)BuildKey();
            if(keyView!=null)keyView.SetActive(visible);
            bool paper=inventory.IsEquipped(InventoryItemKind.HallPass)||inventory.IsEquipped(InventoryItemKind.Newsletters);
            paper&=gm!=null&&(gm.IsPlaying||gm.Current==GameManager.State.Detention)&&!(gm.lockerUI!=null&&gm.lockerUI.IsOpen)&&!player.InputLocked;
            if(paper&&paperView==null)BuildPaper();
            if(paperView!=null){paperView.SetActive(paper);if(paper)paperText.text=inventory.IsEquipped(InventoryItemKind.HallPass)?"HALL PASS\n\nM. SMITH\nYEAR 6\n\nMr Reed\nOffice delivery":"YEAR 6\nNEWSLETTERS\n\nOffice copy\n\nMr Reed";}
        }
        void BuildPaper()
        {
            paperView=new GameObject("Held school paperwork",typeof(RectTransform),typeof(Canvas));paperView.transform.SetParent(player.HoldAnchor,false);
            paperView.transform.localScale=Vector3.one*.001f;paperView.transform.localRotation=Quaternion.Euler(8,-12,-10);
            var rect=paperView.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(210,285);
            var background=SchoolPaperStyle.Apply(paperView,GameManager.Instance.lockerUI.slotPaper);background.raycastTarget=false;
            var go=new GameObject("Handwritten pass",typeof(RectTransform));go.transform.SetParent(paperView.transform,false);
            var r=go.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(12,12);r.offsetMax=new Vector2(-12,-12);
            paperText=go.AddComponent<UnityEngine.UI.Text>();paperText.font=SchoolTypography.Font;paperText.fontSize=24;paperText.color=Color.black;paperText.alignment=TextAnchor.MiddleCenter;paperText.raycastTarget=false;
        }
        void BuildKey()
        {
            var source=GameManager.Instance.officeMission?.key;if(source==null)return;
            keyView=new GameObject("Equipped office key");keyView.transform.SetParent(player.HoldAnchor,false);
            keyView.transform.localPosition=new Vector3(0,.10f,0);
            keyView.transform.localRotation=Quaternion.Euler(-10,15,-25);keyView.transform.localScale=Vector3.one*1.4f;
            foreach(var original in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=original.GetComponent<MeshRenderer>();if(renderer==null)continue;
                var part=new GameObject(original.name,typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(keyView.transform,false);
                part.transform.localPosition=source.transform.InverseTransformPoint(original.transform.position);
                part.transform.localRotation=Quaternion.Inverse(source.transform.rotation)*original.transform.rotation;
                part.transform.localScale=original.transform.localScale;
                part.GetComponent<MeshFilter>().sharedMesh=original.sharedMesh;part.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
                part.layer=gameObject.layer;
            }
        }
        void OnDestroy(){if(keyView!=null)Destroy(keyView);if(paperView!=null)Destroy(paperView);}
    }
}

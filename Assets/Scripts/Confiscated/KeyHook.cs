using UnityEngine;
namespace Confiscated
{
    /// <summary>One hook in the school office key cabinet. The prompt never names the key: only its tag does.</summary>
    public sealed class KeyHook : Interactable
    {
        public KeyCabinet cabinet;
        public Transform key;
        public TextMesh tagText;
        public string Label {get;private set;}
        float swingUntil;Quaternion rest;
        void Awake(){if(key!=null)rest=key.localRotation;}
        public void SetLabel(string label){Label=label;if(tagText!=null)tagText.text=label;}
        public bool IsStore=>Label==KeyCabinet.StoreLabel;
        bool Taken=>IsStore&&SchoolRunController.Instance!=null&&SchoolRunController.Instance.HasStoreKey;
        public override string GetPrompt(PlayerInteractor p)=>CanInteract(p)?"Hold F: take this key (check its tag)":null;
        public override bool CanInteract(PlayerInteractor p){var run=SchoolRunController.Instance;return run!=null&&run.RoundStarted&&!run.HasStoreKey;}
        public override void Interact(PlayerInteractor p)
        {
            if(!CanInteract(p))return;
            if(IsStore){SchoolRunController.Instance.TakeTool(AccessToolPickup.Tool.StoreKey);TempAudio.PlayAt(TempAudio.Pickup,transform.position,.5f);return;}
            swingUntil=Time.time+1.2f;cabinet.WrongKey(this);
        }
        void Update()
        {
            if(key==null)return;
            // A retry hands the key back to its hook.
            if(key.gameObject.activeSelf==Taken)key.gameObject.SetActive(!Taken);
            float left=swingUntil-Time.time;
            key.localRotation=left>0?rest*Quaternion.Euler(0,0,Mathf.Sin(Time.time*26)*20*left/1.2f):rest;
        }
    }
}

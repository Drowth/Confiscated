using UnityEngine;
namespace Confiscated
{
    public sealed class GluePickup : Interactable
    {
        public GameObject visual;
        public bool Taken {get;private set;}
        void Start()=>CollectibleMotion.Attach(visual);
        public override string GetPrompt(PlayerInteractor p)=>Taken?null:
            p.GetComponent<GlueDeployer>()?.Charges>=GlueDeployer.Capacity?"You already have a glue bottle.":"F: take glue (2 / G to drop)";
        public override bool CanInteract(PlayerInteractor p)=>!Taken&&p.GetComponent<GlueDeployer>()!=null&&p.GetComponent<GlueDeployer>().Charges<GlueDeployer.Capacity;
        public override void Interact(PlayerInteractor p)
        {
            if(!CanInteract(p)||!p.GetComponent<GlueDeployer>().Collect())return;
            Taken=true;if(visual!=null)visual.SetActive(false);
            foreach(var collider in GetComponents<Collider>())collider.enabled=false;
        }
    }
}

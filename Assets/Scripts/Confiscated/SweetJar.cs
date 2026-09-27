using UnityEngine;
namespace Confiscated
{
    /// <summary>The sweet jar on the school office counter: one bag per run.</summary>
    public sealed class SweetJar : Interactable
    {
        public GameObject contents;
        public bool Taken {get;private set;}
        public override string GetPrompt(PlayerInteractor p)=>Taken?"The sweet jar is empty.":"F: take a bag of sweets";
        public override bool CanInteract(PlayerInteractor p)=>!Taken&&SchoolRunController.Instance!=null&&SchoolRunController.Instance.RoundStarted;
        public override void Interact(PlayerInteractor p)
        {
            if(!CanInteract(p))return;
            Taken=true;Sweets.On(p).Collect(Sweets.BagSize);TempAudio.PlayAt(TempAudio.Pickup,transform.position,.5f);
            if(contents!=null)contents.SetActive(false);
        }
        public void Refill(){Taken=false;if(contents!=null)contents.SetActive(true);}
    }
}

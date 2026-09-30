using UnityEngine;

namespace Confiscated
{
    /// <summary>Authored office control: position and appearance are editable in the school scene.</summary>
    public sealed class OfficeDoorRelease : Interactable
    {
        [SerializeField] GameObject unpressed,pressed;
        [SerializeField] TextMesh label;
        [SerializeField] AudioSource click;
        void Awake()
        {
            if(click!=null)SchoolAudio.Route(click);
            if(unpressed!=null)unpressed.SetActive(true);
            if(pressed!=null)pressed.SetActive(false);
            if(label!=null)label.text="BLUE DOORS\nPRESS TO OPEN";
        }
        public override string GetPrompt(PlayerInteractor player)=>
            SchoolRunController.Instance!=null&&SchoolRunController.Instance.CorridorDoorsReleased?"Blue corridor doors released.":"F: open all blue corridor doors";
        public override bool CanInteract(PlayerInteractor player)=>SchoolRunController.Instance!=null&&!SchoolRunController.Instance.CorridorDoorsReleased;
        public override void Interact(PlayerInteractor player)
        {
            if(!CanInteract(player))return;
            SchoolRunController.Instance.ReleaseCorridorDoors();
            if(click!=null&&click.clip!=null)click.Play();
            if(label!=null)label.text="BLUE DOORS\nOPEN";
            if(unpressed!=null)unpressed.SetActive(false);
            if(pressed!=null)pressed.SetActive(true);
            HudController.Instance?.SetStatus("Blue corridor doors released. Bolt cutters are in Equipment.",5);
        }
    }
}

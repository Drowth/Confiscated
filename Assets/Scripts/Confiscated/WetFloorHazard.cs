using UnityEngine;
namespace Confiscated
{
    public sealed class WetFloorHazard : MonoBehaviour
    {
        public Vector2 size=new Vector2(2,2.5f);float availableAt;
        public int SlipCount {get;private set;}
        FirstPersonController player;
        void Awake()=>player=Object.FindFirstObjectByType<FirstPersonController>();
        void Update()
        {
            var game=GameManager.Instance;
            if(game==null||!game.IsPlaying||ComicDialogue.IsActive)return;
            if(player==null)player=Object.FindFirstObjectByType<FirstPersonController>();
            if(player==null)return;
            var p=transform.InverseTransformPoint(player.transform.position);
            if(Time.time>=availableAt&&player.IsSprinting&&!player.MovementLocked&&Mathf.Abs(p.x)<size.x*.5f&&Mathf.Abs(p.z)<size.y*.5f&&Mathf.Abs(p.y)<1.5f)
            {player.Slip();if(!player.IsFallen)return;availableAt=Time.time+5;SlipCount++;NoiseEvents.Emit(transform.position,14,"wet floor slip");HudController.Instance?.SetStatus("Slipped! Getting back up...",2.25f);}
        }
    }
}

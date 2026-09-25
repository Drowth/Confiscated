using UnityEngine;
namespace Confiscated
{
    /// <summary>Release requires three fully erased boards followed by clean board rubbers.</summary>
    public class DetentionController : MonoBehaviour
    {
        public Transform arrivalPoint;
        public OfficeDoor door;
        public PhonePickup phonePickup;
        public MissTenison teacher;
        public DetentionBlackboard blackboard;
        public DetentionBlackboard[] blackboards;
        public RubberCleaningStation cleaningStation;
        public int BoardsCleared { get { int count=0;if(blackboards!=null)foreach(var board in blackboards)if(board!=null&&board.Erased)count++;return count; } }
        public bool MinigameOpen { get { if(cleaningStation!=null&&cleaningStation.IsOpen)return true;if(blackboards!=null)foreach(var board in blackboards)if(board!=null&&board.IsOpen)return true;return false; } }
        public Bounds roomBounds;
        public bool Active {get;private set;}
        public bool Seated => false;
        public bool Ready => arrivalPoint!=null&&door!=null&&phonePickup!=null&&teacher!=null&&blackboards!=null&&blackboards.Length==3&&System.Array.TrueForAll(blackboards,b=>b!=null)&&cleaningStation!=null;
        PlayerInteractor player;
        FirstPersonController controller;
        public void Begin(PlayerInteractor who,FirstPersonController movement)
        {
            if(Active)return;player=who;controller=movement;Active=true;
            controller.enabled=true;controller.MovementLocked=false;controller.LookLocked=false;controller.Controller.detectCollisions=true;player.InputLocked=false;
            Warp(arrivalPoint.position,arrivalPoint.rotation);door.SetDetentionDoor(true);
            foreach(var board in blackboards)board.BeginSentence();cleaningStation.ResetCleaning();RefreshTask();
            HudController.Instance?.SetStatus("Miss D Tenison: "+DetentionBlackboard.Introduction,12f);
            HudController.Instance?.SetHoldProgress(-1);
        }
        public void RefreshTask()
        {
            HudController.Instance?.SetObjective(BoardsCleared==3?
                "DETENTION - CLEAN THE RUBBERS\nAll three boards are clear. Use the cleaning station.\nPull the rubbers apart, then drag them together to clap out the dust.":
                "DETENTION - CLEAR THREE BLACKBOARDS\n"+BoardsCleared+" / 3 clean. Look at a board and press F.\nHold left mouse and rub away the prewritten lines.");
        }
        public void BoardCleared(){RefreshTask();HudController.Instance?.SetStatus(BoardsCleared==3?"Three boards spotless. Now clap the rubbers clean at the cleaning station.":"Blackboard cleared! "+BoardsCleared+" / 3 done.",5);}
        public void Sit(DetentionSeat seat)
        {
            if(Active)HudController.Instance?.SetStatus("Miss D Tenison: Three clean boards and two clean rubbers, Smith.",6f);
        }
        void Update()
        {
            if(Active&&!roomBounds.Contains(player.transform.position+Vector3.up))
            {CloseMinigames();Warp(arrivalPoint.position,arrivalPoint.rotation);}
        }
        public void CloseMinigames(){if(blackboards!=null)foreach(var board in blackboards)if(board!=null)board.Close();cleaningStation?.Close();}
        public void CompleteCleaning()
        {
            if(!Active||BoardsCleared!=3||cleaningStation==null||!cleaningStation.Clean)return;
            CloseMinigames();Active=false;controller.MovementLocked=false;controller.LookLocked=false;controller.Controller.detectCollisions=true;player.InputLocked=false;
            player.SuppressActionsThisFrame();door.SetDetentionDoor(false);GameManager.Instance?.FinishDetention();
            HudController.Instance?.SetStatus("Miss D Tenison: That will do, Master Smith. You may leave.",8f);
        }
        void Warp(Vector3 position,Quaternion rotation)
        {
            var cc=controller.Controller;cc.enabled=false;player.transform.SetPositionAndRotation(position,rotation);controller.ResetLook();cc.enabled=true;
        }
    }
}

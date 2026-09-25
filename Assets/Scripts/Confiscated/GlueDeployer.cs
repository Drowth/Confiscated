using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Confiscated
{
    [DisallowMultipleComponent]
    public sealed class GlueDeployer : MonoBehaviour
    {
        public const int Capacity=1;
        public GameObject puddlePrefab;
        public Sprite icon;
        public AudioClip dropSound;
        public int Charges {get;private set;}
        public AudioSource DropAudio {get;private set;}
        public GluePuddle LastDeployed {get;private set;}
        PlayerInteractor player;
        void Awake()
        {
            player=GetComponent<PlayerInteractor>();
            DropAudio=gameObject.AddComponent<AudioSource>();DropAudio.playOnAwake=false;DropAudio.loop=false;
            DropAudio.spatialBlend=1;DropAudio.minDistance=1;DropAudio.maxDistance=14;DropAudio.dopplerLevel=0;
            DropAudio.volume=.8f;
        }
        public bool Collect()
        {
            if(Charges>=Capacity)return false;
            Charges++;
            HudController.Instance?.SetStatus("2 / G: drop glue behind you. He sticks for 4 seconds when he steps in it.",7);
            return true;
        }
        void Update()
        {
            if((Keyboard.current!=null&&(Keyboard.current.digit2Key.wasPressedThisFrame||Keyboard.current.gKey.wasPressedThisFrame))||
                (Gamepad.current!=null&&Gamepad.current.dpad.down.wasPressedThisFrame))Deploy();
        }
        public bool Deploy()
        {
            var game=GameManager.Instance;
            if(player==null||player.InputLocked||ComicDialogue.IsActive||Time.timeScale<=0||Cursor.lockState!=CursorLockMode.Locked||
                game==null||!game.IsPlaying||SchoolRunController.Instance==null||!SchoolRunController.Instance.RoundStarted)return false;
            if(Charges==0){HudController.Instance?.SetStatus("No glue left. Find a bottle in the Art Room.",3);return false;}
            if(puddlePrefab==null||dropSound==null)return false;
            var movement=GetComponent<FirstPersonController>();
            Vector3 heading=movement!=null?movement.Controller.velocity:Vector3.zero;heading.y=0;
            if(heading.sqrMagnitude<.04f){heading=transform.forward;heading.y=0;}
            heading.Normalize();
            // Behind the direction of travel, so deployment never makes the player stop or turn.
            Vector3 desired=transform.position-heading*.55f;
            if(!NavMesh.SamplePosition(transform.position,out var from,.35f,NavMesh.AllAreas)||
                !NavMesh.SamplePosition(desired,out var floor,.35f,NavMesh.AllAreas)||
                NavMesh.Raycast(from.position,floor.position,out _,NavMesh.AllAreas)||
                Physics.Linecast(transform.position+Vector3.up*.15f,floor.position+Vector3.up*.15f,~0,QueryTriggerInteraction.Ignore))return false;
            if(!Physics.Raycast(floor.position+Vector3.up*.25f,Vector3.down,out var ground,.55f,~0,QueryTriggerInteraction.Ignore)||ground.normal.y<.9f)return false;
            var placed=Instantiate(puddlePrefab,ground.point+Vector3.up*.012f,Quaternion.LookRotation(heading));
            LastDeployed=placed.GetComponent<GluePuddle>();
            if(LastDeployed==null){Destroy(placed);return false;}
            Charges--;DropAudio.clip=dropSound;DropAudio.Play();
            HudController.Instance?.SetStatus("Glue down. Lead the caretaker over it!",3);
            return true;
        }
    }
}

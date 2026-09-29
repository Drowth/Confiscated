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
            DropAudio=SchoolAudio.Create(gameObject);DropAudio.playOnAwake=false;DropAudio.loop=false;
            DropAudio.spatialBlend=1;DropAudio.minDistance=1;DropAudio.maxDistance=14;DropAudio.dopplerLevel=0;
            DropAudio.volume=.8f;
        }
        public bool Collect()
        {
            if(Charges>=Capacity)return false;
            Charges++;
            ItemCodex.PickUp(Items.Glue);
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
            if(Charges==0){HudController.Instance?.SetStatus("No glue left. There's a bottle in the library.",3);return false;}
            if(puddlePrefab==null||dropSound==null)return false;
            if(!Place(out var centre,out var along,out float width))return false;
            var placed=Instantiate(puddlePrefab,centre,Quaternion.LookRotation(along));
            LastDeployed=placed.GetComponent<GluePuddle>();
            if(LastDeployed==null){Destroy(placed);return false;}
            // Wall to wall across the corridor, so nobody can step round it.
            LastDeployed.halfWidth=width/2;LastDeployed.halfDepth=Depth/2;
            var art=placed.transform.Find("Sketched glue on floor");if(art!=null)art.localScale=new Vector3(width+.1f,Depth+.15f,1);
            Charges--;ContextualControlHints.Used(ContextualControlHints.Action.Glue);DropAudio.clip=dropSound;DropAudio.Play();
            HudController.Instance?.SetStatus("Glue across the corridor. Whoever follows you sticks!",3);
            return true;
        }

        const float Depth=1f,OpenFloorWidth=2.4f,MaxCorridor=5f;
        /// <summary>
        /// The middle of the hallway you're in, just behind you: the corridor axis nearest your heading (the school is built
        /// on a grid), centred between the walls either side, spanning its full width.
        /// </summary>
        bool Place(out Vector3 centre,out Vector3 along,out float width)
        {
            centre=default;width=OpenFloorWidth;
            var movement=GetComponent<FirstPersonController>();
            Vector3 heading=movement!=null?movement.Controller.velocity:Vector3.zero;heading.y=0;
            if(heading.sqrMagnitude<.04f){heading=transform.forward;heading.y=0;}
            along=Mathf.Abs(heading.x)>Mathf.Abs(heading.z)?new Vector3(Mathf.Sign(heading.x),0,0):new Vector3(0,0,Mathf.Sign(heading.z));
            Vector3 across=new Vector3(along.z,0,-along.x);
            // Behind the direction of travel, so deployment never makes the player stop or turn.
            Vector3 spot=transform.position-along*(Depth/2+.15f);
            Vector3 eye=new Vector3(spot.x,transform.position.y+.5f,spot.z);
            const int solid=~(1<<2);
            bool left=Physics.Raycast(eye,-across,out var l,MaxCorridor,solid,QueryTriggerInteraction.Ignore);
            bool right=Physics.Raycast(eye,across,out var r,MaxCorridor,solid,QueryTriggerInteraction.Ignore);
            if(left&&right&&l.distance+r.distance<=MaxCorridor)
            {width=l.distance+r.distance;spot+=across*((r.distance-l.distance)/2);}
            if(!NavMesh.SamplePosition(transform.position,out var from,.35f,NavMesh.AllAreas)||
                !NavMesh.SamplePosition(spot,out var floor,.6f,NavMesh.AllAreas)||
                NavMesh.Raycast(from.position,floor.position,out _,NavMesh.AllAreas))return false;
            if(!Physics.Raycast(new Vector3(spot.x,floor.position.y+.25f,spot.z),Vector3.down,out var ground,.55f,solid,QueryTriggerInteraction.Ignore)||ground.normal.y<.9f)return false;
            centre=new Vector3(spot.x,ground.point.y+.012f,spot.z);
            return true;
        }
    }
}

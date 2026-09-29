using System.Collections;
using UnityEngine;

namespace Confiscated
{
    /// <summary>
    /// The isolation room's only way in: an old doorway boarded over with plywood and nailed planks, light showing
    /// underneath. Holding F prises it open, but only once <see cref="IsolationRoom.Open"/> (Dark Mode, and unlocked).
    /// </summary>
    public sealed class BoardedDoorway : Interactable
    {
        [Tooltip("Planks and the plywood sheet, pulled off in this order.")]
        public Transform[] boards;
        public bool Opened {get;private set;}

        public override string GetPrompt(PlayerInteractor player)=>Opened?null:IsolationRoom.Open?"Hold F: prise the boards off":IsolationRoom.LockedPrompt;
        public override bool CanInteract(PlayerInteractor player)=>!Opened&&IsolationRoom.Open;
        public override void Interact(PlayerInteractor player)
        {
            if(!CanInteract(player))return;
            Opened=true;StartCoroutine(Tear(player.transform.position));
        }

        IEnumerator Tear(Vector3 from)
        {
            Vector3 toward=from-transform.position;toward.y=0;toward.Normalize();
            foreach(var board in boards)
            {
                if(board==null)continue;
                foreach(var c in board.GetComponentsInChildren<Collider>())c.enabled=false;
                TempAudio.PlayAt(TempAudio.Thud,board.position,.9f);
                StartCoroutine(Fall(board,toward));
                yield return new WaitForSeconds(.22f);
            }
            NoiseEvents.Emit(transform.position,18,"boards torn off");
        }

        static IEnumerator Fall(Transform board,Vector3 toward)
        {
            Vector3 start=board.position;Quaternion turn=board.rotation;
            Vector3 spin=new(Random.Range(-40f,40f),Random.Range(-25f,25f),Random.Range(-70f,70f));
            float floor=.02f+Random.value*.03f;
            for(float t=0;t<.55f;t+=Time.deltaTime)
            {
                float k=t/.55f;
                board.position=Vector3.Lerp(start,new Vector3(start.x,floor,start.z)+toward*(.35f+Random.value*.02f),k*k)+Vector3.up*Mathf.Sin(k*Mathf.PI)*.08f;
                board.rotation=turn*Quaternion.Euler(spin*k);
                yield return null;
            }
        }
    }
}

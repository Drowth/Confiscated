using UnityEngine;
using UnityEngine.InputSystem;

namespace Confiscated
{
    /// <summary>First-person lens feedback only: no duplicated footsteps or automatic camera turns.</summary>
    [DisallowMultipleComponent]
    public sealed class ChaseCamera : MonoBehaviour
    {
        [Range(0,1)] public float intensity=1;
        public float chaseWidening=5, sprintWidening=3;
        Camera view;
        FirstPersonController player;
        float baseFov, offset;
        public float AppliedOffset => offset;
        public bool Active => player!=null&&player.enabled&&!player.IsFallen&&!player.MovementLocked&&!player.LookLocked&&!ComicDialogue.IsActive&&GameManager.Instance!=null&&GameManager.Instance.IsPlaying&&Time.timeScale>0;
        void Awake(){player=GetComponent<FirstPersonController>();view=GetComponentInChildren<Camera>();if(view!=null)baseFov=view.fieldOfView;intensity=PlayerPrefs.GetFloat("Confiscated.CameraIntensity",intensity);}
        public void SetIntensity(float value){intensity=Mathf.Clamp01(value);PlayerPrefs.SetFloat("Confiscated.CameraIntensity",intensity);if(intensity==0)Restore();}
        void Update()
        {
            if(view==null)return;
            if(Active&&Keyboard.current!=null&&Keyboard.current.f8Key.wasPressedThisFrame)
            {
                SetIntensity(intensity>.75f?.5f:intensity>.01f?0:1);
                HudController.Instance?.SetStatus("Camera and VHS effects: "+(intensity==0?"off":intensity<1?"reduced":"normal"),2);
            }
            if(!Active){Restore();return;}
            var run=SchoolRunController.Instance;
            bool chased=run!=null&&run.RoundStarted&&run.caretaker.Current==CaretakerAI.State.Chase;
            float target=intensity*((chased?chaseWidening:0)+(player.IsSprinting?sprintWidening:0));
            offset=Mathf.Lerp(offset,target,1-Mathf.Exp(-Time.deltaTime*3));
            view.fieldOfView=baseFov+offset;
        }
        public Vector3 ExhaustionSway => Active&&player.SprintFraction<=.2f ? new Vector3(Mathf.Sin(Time.time*1.8f)*.3f,0,Mathf.Sin(Time.time*1.4f)*.45f)*intensity : Vector3.zero;
        void Restore(){if(view!=null&&offset!=0)view.fieldOfView=baseFov;offset=0;}
        void OnDisable(){Restore();}
    }
}

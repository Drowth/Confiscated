using UnityEngine;
using UnityEngine.InputSystem;

namespace Confiscated
{
    /// <summary>
    /// Minimal first-person controller for the CONFISCATED! visual test.
    /// Uses the project's InputSystem_Actions asset (Player/Move, Player/Look, Player/Sprint)
    /// and a CharacterController for collision. No jumping, no crouch, no enemy logic.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public const float ChildEyeHeight=1.28f,ChildBodyHeight=1.4f;
        [Header("Input")]
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] string moveActionPath = "Player/Move";
        [SerializeField] string lookActionPath = "Player/Look";
        [SerializeField] string sprintActionPath = "Player/Sprint";

        [Header("Movement")]
        [SerializeField] float walkSpeed = 2.4f;
        [SerializeField] float sprintSpeed = 4.0f;
        [SerializeField] float gravity = -18f;

        [Header("Look")]
        [SerializeField] Transform cameraPivot;
        [Tooltip("Degrees of rotation per pixel of mouse movement.")]
        [SerializeField] float mouseSensitivity = 0.08f;
        [Tooltip("Degrees per second at full gamepad stick deflection.")]
        [SerializeField] float stickSensitivity = 140f;
        [SerializeField] float minPitch = -75f;
        [SerializeField] float maxPitch = 75f;
        [SerializeField] bool lockCursor = true;

        CharacterController controller;
        InputAction moveAction;
        InputAction lookAction;
        InputAction sprintAction;
        float pitch;
        float verticalVelocity;
        float sprintReserve = 5f;
        float sprintRecoveryAt;
        bool exhausted;
        [Header("Lean (Q / E)")]
        [SerializeField] float leanDistance=.48f;
        [SerializeField] float leanRollDegrees=9f;
        [SerializeField] float leanSeconds=.16f;
        [SerializeField] float leanHeadRadius=.16f;
        float lean;
        static readonly RaycastHit[] leanHits=new RaycastHit[8];
        const string SensitivityKey="Confiscated.MouseSensitivity";
        /// <summary>Pause-menu setting; saved between sessions.</summary>
        public float MouseSensitivity{get=>mouseSensitivity;set{mouseSensitivity=Mathf.Clamp(value,.02f,.2f);PlayerPrefs.SetFloat(SensitivityKey,mouseSensitivity);}}
        float distractedUntil;
        public bool IsDistracted=>Time.time<distractedUntil;
        // A short social interruption leaves looking, interacting and walking available.
        public void BriefDistraction(float seconds){distractedUntil=Time.time+Mathf.Clamp(seconds,0,2);}
        float fallTime;
        Vector3 fallCameraPosition,fallDirection;
        Quaternion fallCameraRotation;
        public bool IsFallen {get;private set;}
        public AudioSource FallVoice {get;private set;}
        public float FallTime => fallTime;
        ChaseCamera chaseCamera;
        public void Slip()
        {
            if(IsFallen||!enabled||MovementLocked||LookLocked||cameraPivot==null||ComicDialogue.IsActive)return;
            sprintReserve=Mathf.Max(0,sprintReserve-2.5f);sprintRecoveryAt=Time.time+3.2f;if(sprintReserve==0)exhausted=true;
            fallCameraPosition=cameraPivot.localPosition;fallCameraRotation=cameraPivot.localRotation;
            fallDirection=controller.velocity;fallDirection.y=0;fallDirection=fallDirection.sqrMagnitude>.01f?fallDirection.normalized:transform.forward;
            fallTime=0;IsFallen=true;IsSprinting=false;
            // The recording carries its own run-up: started here, its thud lands on the floor impact at .48 s.
            if(FallVoice!=null&&FallVoice.clip!=null)FallVoice.Play();
        }
        public void KnockDown(Vector3 direction)
        {
            Slip();
            direction.y=0;
            if(IsFallen&&direction.sqrMagnitude>.01f)fallDirection=direction.normalized;
        }
        void CancelFall()
        {
            if(!IsFallen)return;
            // Cut short only when the fall itself is: caught, dialogue, or a scripted reposition.
            if(FallVoice!=null&&fallTime<2.2f)FallVoice.Stop();
            if(cameraPivot!=null){cameraPivot.localPosition=fallCameraPosition;cameraPivot.localRotation=fallCameraRotation;}
            IsFallen=false;verticalVelocity=0;
        }
        void AdvanceFall()
        {
            fallTime+=Time.deltaTime;
            // A short forward skid is collision-tested; the camera itself never moves through a wall.
            if(controller.enabled)controller.Move((fallDirection*(fallTime<.3f?1.7f*(1-fallTime/.3f):0)+Vector3.down*2)*Time.deltaTime);
            if(fallTime>=2.25f)CancelFall();
        }
        void PoseFall()
        {
            float t=fallTime;
            float down=Mathf.Clamp01(t/.48f);down*=down;
            float rising=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.05f,2.25f,t));
            float ground=Mathf.Lerp(down,0,rising);
            float motion=chaseCamera!=null?chaseCamera.intensity:1;
            float impact=t>=.48f&&t<.75f?Mathf.Sin((t-.48f)*55)*Mathf.Exp(-(t-.48f)*15)*motion:0;
            var position=fallCameraPosition;position.y=Mathf.Lerp(fallCameraPosition.y,.22f,ground)+impact*.035f;
            cameraPivot.localPosition=position;
            var lying=Quaternion.Euler(-84,fallCameraRotation.eulerAngles.y,11*motion);
            cameraPivot.localRotation=Quaternion.Slerp(fallCameraRotation,lying,Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.48f))*(1-rising))*Quaternion.Euler(impact*2,0,impact*2);
        }
        public bool IsSprinting { get; private set; }
        public bool ForcedCorridorRun { get; private set; }
        public float ForcedRunRemaining { get; private set; }
        Vector3 forcedRunDirection;
        float forcedRunSpeed, forcedRunStalled;
        Transform forcedRunPusher;

        /// <summary>Coach's corridor drill: fixed world direction, independent of movement or sprint input.</summary>
        public bool StartForcedCorridorRun(Vector3 direction, float metres, float speed, Transform pusher = null)
        {
            direction.y = 0;
            if (ForcedCorridorRun || MovementLocked || IsFallen || !enabled || direction.sqrMagnitude < .9f || metres < 2f)
                return false;
            forcedRunDirection = direction.normalized;
            ForcedRunRemaining = metres;
            forcedRunSpeed = speed;
            forcedRunStalled = 0;
            forcedRunPusher = pusher;
            ForcedCorridorRun = true;
            return true;
        }

        public void StopForcedCorridorRun()
        {
            ForcedCorridorRun = false;
            ForcedRunRemaining = 0;
            forcedRunStalled = 0;
            forcedRunPusher = null;
        }
        public float SprintFraction => sprintReserve / 5f;

        public CharacterController Controller => controller;
        public bool MovementLocked { get; set; }
        public bool LookLocked { get; set; }
        public void ResetLook()
        {
            CancelFall();
            distractedUntil=0;lean=0;
            pitch = 0f; verticalVelocity = 0f;
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.identity;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            mouseSensitivity=PlayerPrefs.GetFloat(SensitivityKey,mouseSensitivity);
            chaseCamera=GetComponent<ChaseCamera>();if(chaseCamera==null)chaseCamera=gameObject.AddComponent<ChaseCamera>();
            if(GetComponent<PlayerBreathingAudio>()==null)gameObject.AddComponent<PlayerBreathingAudio>();
            if(GetComponent<PlayerFootstepAudio>()==null)gameObject.AddComponent<PlayerFootstepAudio>();
            // Loaded now, not at the first slip: a late start would put the thud after the floor impact.
            FallVoice=gameObject.AddComponent<AudioSource>();FallVoice.playOnAwake=false;FallVoice.spatialBlend=0;FallVoice.volume=.85f;
            FallVoice.clip=Resources.Load<AudioClip>("Audio/PlayerFall");if(FallVoice.clip!=null)FallVoice.clip.LoadAudioData();
            if (cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraPivot = cam.transform;
            }
            controller.height=ChildBodyHeight;controller.center=new Vector3(0,ChildBodyHeight*.5f,0);controller.stepOffset=.22f;
            if(cameraPivot!=null){var p=cameraPivot.localPosition;p.y=ChildEyeHeight;cameraPivot.localPosition=p;}
        }

        void OnEnable()
        {
            if (inputActions != null)
            {
                moveAction = inputActions.FindAction(moveActionPath, false);
                lookAction = inputActions.FindAction(lookActionPath, false);
                sprintAction = inputActions.FindAction(sprintActionPath, false);
                inputActions.Enable();
                if (moveAction == null || lookAction == null)
                    Debug.LogWarning("[FirstPersonController] Move/Look actions not found in " + inputActions.name, this);
            }
            else
            {
                Debug.LogWarning("[FirstPersonController] No InputActionAsset assigned; player will not move.", this);
            }

            if (cameraPivot != null)
            {
                pitch = cameraPivot.localEulerAngles.x;
                if (pitch > 180f) pitch -= 360f;
            }

            if (lockCursor) SetCursorLocked(true);
        }

        void OnDisable()
        {
            CancelFall();
            StopForcedCorridorRun();
            IsSprinting=false;
            if (inputActions != null) inputActions.Disable();
            SetCursorLocked(false);
        }

        void Update()
        {
            IsSprinting=false;
            if(IsFallen)
            {
                if(ComicDialogue.IsActive||MovementLocked||LookLocked||GameManager.Instance!=null&&!GameManager.Instance.IsPlaying)CancelFall();
                else AdvanceFall();
                return;
            }
            if(ComicDialogue.IsActive)return;
            HandleCursor();
            HandleLook();
            HandleMove();
            HandleLean();
        }

        void LateUpdate()
        {
            if(IsFallen){PoseFall();return;}
            if(LookLocked||ComicDialogue.IsActive||cameraPivot==null)return;
            bool lookingBack=Keyboard.current!=null&&Keyboard.current.spaceKey.isPressed;
            cameraPivot.localRotation=Quaternion.Euler(new Vector3(pitch,lookingBack?180:0,-lean*leanRollDegrees)+(chaseCamera!=null?chaseCamera.ExhaustionSway:Vector3.zero));
            var p=cameraPivot.localPosition;p.x=lean*leanDistance;cameraPivot.localPosition=p;
        }

        /// <summary>
        /// Hold Q / E to lean the head out sideways. Only the camera moves: the body (which staff sight, the dinner lady and the
        /// chatterbox all test against) stays where it is, so peeking round a corner keeps the player behind cover.
        /// </summary>
        void HandleLean()
        {
            var kb=Keyboard.current;float want=0;
            bool allowed=kb!=null&&!MovementLocked&&!LookLocked&&!IsSprinting&&!kb.spaceKey.isPressed&&Cursor.lockState==CursorLockMode.Locked&&
                (GameManager.Instance==null||GameManager.Instance.IsPlaying);
            if(allowed)want=(kb.eKey.isPressed?1:0)-(kb.qKey.isPressed?1:0);
            if(want!=0)
            {
                // Stop the head short of a wall instead of looking through it.
                Vector3 origin=transform.position+Vector3.up*(cameraPivot!=null?cameraPivot.localPosition.y:ChildEyeHeight);
                float room=leanDistance;
                int count=Physics.SphereCastNonAlloc(origin,leanHeadRadius,transform.right*want,leanHits,leanDistance,~0,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++)
                {
                    var hit=leanHits[i];
                    if(hit.transform==transform||hit.transform.IsChildOf(transform))continue;
                    room=Mathf.Min(room,hit.distance);
                }
                want*=Mathf.Clamp01(room/leanDistance);
            }
            lean=Mathf.MoveTowards(lean,want,Time.deltaTime/leanSeconds);
        }
        public float Lean=>lean;

        void HandleCursor()
        {
            if (LookLocked) return;
            if (!lockCursor) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursorLocked(true);
        }

        void HandleLook()
        {
            if (LookLocked) return;
            if (lookAction == null || cameraPivot == null) return;
            Vector2 look = lookAction.ReadValue<Vector2>();
            if (look.sqrMagnitude < 1e-8f) return;

            bool isPointer = lookAction.activeControl != null && lookAction.activeControl.device is Pointer;
            float scale = isPointer ? mouseSensitivity : stickSensitivity * Time.deltaTime;

            transform.Rotate(0f, look.x * scale, 0f, Space.Self);
            pitch = Mathf.Clamp(pitch - look.y * scale, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void HandleMove()
        {
            if (MovementLocked || GameManager.Instance != null && !GameManager.Instance.IsPlaying && GameManager.Instance.Current != GameManager.State.Detention)
            { StopForcedCorridorRun(); verticalVelocity = 0f; return; }
            if (ForcedCorridorRun)
            {
                // A coach drill only advances while he is physically close behind the pupil.
                if (forcedRunPusher != null)
                {
                    var gap = transform.position - forcedRunPusher.position; gap.y = 0;
                    if (gap.sqrMagnitude > 1.65f * 1.65f || Vector3.Dot(gap, forcedRunDirection) < -.15f)
                    { forcedRunStalled = 0; return; }
                }
                if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
                verticalVelocity += gravity * Time.deltaTime;
                var forcedStart = transform.position;
                float advance = Mathf.Min(ForcedRunRemaining, forcedRunSpeed * Time.deltaTime);
                controller.Move(forcedRunDirection * advance + Vector3.up * verticalVelocity * Time.deltaTime);
                var travelled = Vector3.Dot(transform.position - forcedStart, forcedRunDirection);
                ForcedRunRemaining -= Mathf.Max(0, travelled);
                IsSprinting = travelled > Time.deltaTime * .1f;
                forcedRunStalled = IsSprinting ? 0 : forcedRunStalled + Time.deltaTime;
                if (ForcedRunRemaining <= .001f || forcedRunStalled > .75f) StopForcedCorridorRun();
                return;
            }
            Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            if (input.sqrMagnitude > 1f) input.Normalize();

            bool sprinting = !IsDistracted && sprintAction != null && sprintAction.IsPressed();
            if (SchoolRunController.Instance != null)
            {
                if (exhausted && sprintReserve >= 1.5f) exhausted = false;
                sprinting = sprinting && input.sqrMagnitude > .01f && !exhausted && sprintReserve > 0;
                if (sprinting)
                {
                    sprintReserve = Mathf.Max(0, sprintReserve - Time.deltaTime);
                    sprintRecoveryAt = Time.time + 1.2f;
                    if (sprintReserve == 0) exhausted = true;
                }
                else if (Time.time >= sprintRecoveryAt) sprintReserve = Mathf.Min(5, sprintReserve + Time.deltaTime * .85f);
            }
            float speed = sprinting ? sprintSpeed : walkSpeed;
            if(IsDistracted)speed*=.45f;

            Vector3 planar = (transform.right * input.x + transform.forward * input.y) * speed;

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = planar + Vector3.up * verticalVelocity;
            Vector3 before=transform.position;
            controller.Move(velocity * Time.deltaTime);
            Vector3 moved=transform.position-before;moved.y=0;
            IsSprinting=sprinting&&input.sqrMagnitude>.01f&&moved.magnitude>Time.deltaTime*.1f;
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}

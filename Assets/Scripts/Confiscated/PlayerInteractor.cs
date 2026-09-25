using UnityEngine;
using UnityEngine.InputSystem;

namespace Confiscated
{
    /// <summary>
    /// Looks for an Interactable in front of the camera, shows its prompt on the HUD and triggers it
    /// with the Player/Interact action (instant or hold). Also owns the "held item" anchor for the ball.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] string interactActionPath = "Player/Interact";
        [SerializeField] string attackActionPath = "Player/Attack";
        [SerializeField] Camera viewCamera;
        [SerializeField] float reach = 2.6f;
        [SerializeField] LayerMask hitMask = ~0;
        [SerializeField] Transform holdAnchor;

        InputAction interactAction;
        InputAction attackAction;
        Interactable current;
        float holdProgress;
        int suppressedFrame=-1;

        public Camera ViewCamera => viewCamera;
        public Transform HoldAnchor => holdAnchor;
        public ThrowableBall HeldBall { get; set; }
        public bool HasPhone { get; set; }
        bool inputLocked;
        public bool InputLocked { get=>inputLocked||(GetComponent<FirstPersonController>()?.IsFallen??false); set=>inputLocked=value; }
        public void SuppressActionsThisFrame() => suppressedFrame=Time.frameCount;

        void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (holdAnchor == null && viewCamera != null)
            {
                var a = new GameObject("HoldAnchor").transform;
                a.SetParent(viewCamera.transform, false);
                a.localPosition = new Vector3(0.35f, -0.28f, 0.75f);
                holdAnchor = a;
            }
        }

        void OnEnable()
        {
            if (inputActions != null)
            {
                interactAction = inputActions.FindAction(interactActionPath, false);
                attackAction = inputActions.FindAction(attackActionPath, false);
                inputActions.Enable();
            }
        }

        void Update()
        {
            if (ComicDialogue.IsActive || InputLocked || suppressedFrame == Time.frameCount) { SetCurrent(null); return; }

            Interactable found = null;
            if (viewCamera != null)
                found = Resolve(new Ray(viewCamera.transform.position, viewCamera.transform.forward), reach, hitMask);
            if (found != null && !found.CanInteract(this)) { /* keep it so the prompt can explain */ }
            SetCurrent(found);

            // Throw the held ball with Attack (left mouse / gamepad west).
            if (HeldBall != null && attackAction != null && attackAction.WasPressedThisFrame())
            {
                HeldBall.Throw(viewCamera.transform.forward * 9f + Vector3.up * 2.2f);
                return;
            }

            if (current == null || interactAction == null) { holdProgress = 0f; return; }
            if (!current.CanInteract(this)) { holdProgress = 0f; return; }

            if (current.holdSeconds <= 0f)
            {
                if (interactAction.WasPressedThisFrame() || (Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame)) current.Interact(this);
            }
            else
            {
                if (interactAction.IsPressed() || (Mouse.current!=null&&Mouse.current.leftButton.isPressed))
                {
                    holdProgress += Time.deltaTime / current.holdSeconds;
                    if (holdProgress >= 1f)
                    {
                        holdProgress = 0f;
                        current.Interact(this);
                    }
                }
                else holdProgress = 0f;
            }
            HudController.Instance?.SetHoldProgress(current != null && current.holdSeconds > 0f ? holdProgress : -1f);
        }

        static readonly RaycastHit[] rayHits = new RaycastHit[16];
        const int IgnoreRaycastLayer = 2;

        /// <summary>
        /// What the player is pointing at. Trigger volumes and invisible blockers (Ignore Raycast layer, e.g. the
        /// locked-doorway barriers) never hide the interactable behind them; the first solid surface still does.
        /// </summary>
        public static Interactable Resolve(Ray ray, float reach, LayerMask mask)
        {
            int count = Physics.RaycastNonAlloc(ray, rayHits, reach, mask, QueryTriggerInteraction.Collide);
            System.Array.Sort(rayHits, 0, count, HitDistance.Instance);
            for (int i = 0; i < count; i++)
            {
                var collider = rayHits[i].collider;
                var target = collider.GetComponentInParent<Interactable>();
                if (target != null) return target;
                if (collider.isTrigger || collider.gameObject.layer == IgnoreRaycastLayer) continue;
                return null;
            }
            return null;
        }

        sealed class HitDistance : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistance Instance = new HitDistance();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        void SetCurrent(Interactable next)
        {
            if (next != current) holdProgress = 0f;
            current = next;
            var hud = HudController.Instance;
            if (hud == null) return;
            if (current == null)
            {
                hud.SetPrompt(HeldBall != null ? "Left click: throw the football" : null);
                hud.SetHoldProgress(-1f);
            }
            else hud.SetPrompt(current.GetPrompt(this));
        }
    }
}


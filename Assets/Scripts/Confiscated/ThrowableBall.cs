using UnityEngine;

namespace Confiscated
{
    /// <summary>
    /// The football: pick up with Interact, throw with Attack. Bounces make noise the caretaker investigates.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class ThrowableBall : Interactable
    {
        [SerializeField] float noiseRadius = 22f;
        [SerializeField] float minImpactSpeed = 1.2f;
        [SerializeField] float noiseCooldown = 0.35f;
        [SerializeField] int maxNoisyBounces = 3;
        [SerializeField] AudioClip impactClip;

        Rigidbody body;
        Collider col;
        bool held;
        RigidbodyInterpolation freeInterpolation;
        CollisionDetectionMode freeCollisionDetection;
        float lastNoiseTime = -10f;
        int noisyBounces;

        void Start(){CollectibleMotion.AttachMeshes(transform,body);}

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            freeInterpolation=body.interpolation;freeCollisionDetection=body.collisionDetectionMode;
        }

        public override string GetPrompt(PlayerInteractor player) => held ? null : "F: pick up the football";

        public override bool CanInteract(PlayerInteractor player) => !held && player.HeldBall == null &&
            (player.GetComponent<PlayerInventory>() == null || player.GetComponent<PlayerInventory>().CanCollect(player.GetComponent<PlayerInventory>().footballItem,this));

        public override void Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return;
            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                if(!inventory.Collect(inventory.footballItem,this))return;
                StoreInLocker();HudController.Instance?.SetStatus("Football collected. Press 1 to hold it; left click to throw.",4f);return;
            }
            Equip(player);
        }
        public void Equip(PlayerInteractor player)
        {
            if(player.HeldBall!=null&&player.HeldBall!=this)return;
            gameObject.SetActive(true);body.isKinematic=false;
            held = true;
            player.HeldBall = this;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            // Carrying is driven by the camera parent, not interpolated physics poses.
            // Leaving interpolation enabled writes the previous world pose over that attachment.
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            col.enabled = false;
            transform.SetParent(player.HoldAnchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            body.position = transform.position;
            body.rotation = transform.rotation;
            TempAudio.PlayAt(TempAudio.Pickup, transform.position, 0.5f);
            HudController.Instance?.SetPrompt("Left click: throw the football");
        }

        public void Throw(Vector3 velocity)
        {
            if (!held) return;
            var player = GetComponentInParent<PlayerInteractor>();
            player?.GetComponent<PlayerInventory>()?.RemoveCarriedBall(this);
            if (player != null && player.HeldBall == this) player.HeldBall = null;
            transform.SetParent(null, true);
            // Start simulation at the current hand position, never the last free-body pose.
            body.position = transform.position;
            body.rotation = transform.rotation;
            held = false;
            col.enabled = true;
            body.isKinematic = false;
            body.collisionDetectionMode = freeCollisionDetection;
            body.interpolation = freeInterpolation;
            body.linearVelocity = velocity;
            body.angularVelocity = Random.insideUnitSphere * 6f;
            noisyBounces = 0;
            HudController.Instance?.SetPrompt(null);
        }

        void OnCollisionEnter(Collision c)
        {
            if (held) return;
            if (c.relativeVelocity.magnitude < minImpactSpeed) return;
            if (Time.time - lastNoiseTime < noiseCooldown) return;
            if (noisyBounces >= maxNoisyBounces) return;
            lastNoiseTime = Time.time;
            noisyBounces++;
            float loudness = Mathf.Clamp01(c.relativeVelocity.magnitude / 8f);
            TempAudio.PlayAt(impactClip != null ? impactClip : TempAudio.Thud, transform.position, 0.3f + 0.7f * loudness);
            NoiseEvents.Emit(transform.position, noiseRadius * (0.5f + 0.5f * loudness), "football");
        }

        public void StoreInLocker()
        {
            var owner=GetComponentInParent<PlayerInteractor>();
            if(owner!=null && owner.HeldBall==this)owner.HeldBall=null;
            transform.SetParent(null,true);held=false;
            body.collisionDetectionMode=CollisionDetectionMode.Discrete;body.isKinematic=true;col.enabled=false;gameObject.SetActive(false);
        }
        public void TakeFromLocker(PlayerInteractor player)
        {
            gameObject.SetActive(true);held=false;body.isKinematic=false;
            body.interpolation=freeInterpolation;body.collisionDetectionMode=freeCollisionDetection;col.enabled=true;
            Equip(player);
        }
    }
}


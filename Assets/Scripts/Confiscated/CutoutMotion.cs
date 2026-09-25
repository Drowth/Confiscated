using UnityEngine;

namespace Confiscated
{
    /// <summary>Single-image character motion. Put this on a feet-level pivot below the billboard.</summary>
    [DisallowMultipleComponent]
    public class CutoutMotion : MonoBehaviour
    {
        [Tooltip("The gameplay root whose movement drives the steps. Never assign the animated pivot itself.")]
        public Transform movementSource;

        [Header("Movement")]
        [Min(.1f)] public float walkSpeed = 1.5f;
        [Min(.1f)] public float runSpeed = 3.3f;
        [Tooltip("Metres travelled during one full left/right step cycle.")]
        [Min(.1f)] public float strideLength = 1.4f;
        [Min(0f)] public float stoppedThreshold = .06f;
        [Min(.01f)] public float blendSeconds = .14f;
        [Tooltip("Ignore movement faster than this, such as a warp or respawn.")]
        [Min(.1f)] public float teleportSpeed = 12f;

        [Header("Idle")]
        [Min(0f)] public float breathsPerSecond = .35f;
        [Range(0f, .1f)] public float breathingStretch = .009f;

        [Header("Steps")]
        [Min(0f)] public float walkBounce = .035f;
        [Min(0f)] public float runBounce = .055f;
        [Range(0f, 10f)] public float walkSwayDegrees = 1.8f;
        [Range(0f, 10f)] public float runSwayDegrees = 3f;
        [Range(0f, .1f)] public float stepSquash = .012f;

        Vector3 restPosition, restScale, previousPosition;
        Quaternion restRotation;
        float smoothedSpeed, stepPhase, breathPhase;
        bool frozen;

        [HideInInspector] public bool externalGait;
        [HideInInspector] public float gaitPhase,gaitSpeed;
        public float CurrentSpeed => smoothedSpeed;

        void OnEnable()
        {
            ResetMotion();
        }

        /// <summary>Capture the resting pose and restart the cycle, including for editor previews.</summary>
        public void ResetMotion()
        {
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            restScale = transform.localScale;
            previousPosition = movementSource != null ? movementSource.position : Vector3.zero;
            smoothedSpeed = stepPhase = breathPhase = 0f;
            frozen = false;
        }

        void OnDisable() { RestorePose(); }

        void LateUpdate() { Advance(Time.deltaTime); }

        /// <summary>Also usable for an editor preview or deterministic pose capture.</summary>
        public void Advance(float deltaTime)
        {
            if (movementSource == null || movementSource == transform || movementSource.IsChildOf(transform))
            {
                RestorePose();
                return;
            }
            var displacement = movementSource.position - previousPosition;
            previousPosition = movementSource.position;
            // Dialogue pauses time; don't leave a walking cutout suspended mid-step.
            if (frozen || deltaTime <= 0f) { RestorePose(); return; }
            displacement.y = 0f;
            float measuredSpeed = displacement.magnitude / deltaTime;
            if (measuredSpeed > teleportSpeed)
            {
                // Warps must not produce a burst of running animation.
                smoothedSpeed = 0f;
                measuredSpeed = 0f;
            }
            float blend = 1f - Mathf.Exp(-deltaTime / Mathf.Max(.01f, blendSeconds));
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, measuredSpeed, blend);
            if (smoothedSpeed < stoppedThreshold) smoothedSpeed = 0f;
            if(externalGait)smoothedSpeed=gaitSpeed;
            float moving = Mathf.Clamp01(smoothedSpeed / Mathf.Max(.1f, walkSpeed));
            float running = Mathf.InverseLerp(walkSpeed, Mathf.Max(walkSpeed + .01f, runSpeed), smoothedSpeed);
            stepPhase = Mathf.Repeat(stepPhase + smoothedSpeed / Mathf.Max(.1f, strideLength) * Mathf.PI * 2f * deltaTime, Mathf.PI * 2f);
            if(externalGait)stepPhase=gaitPhase;
            breathPhase = Mathf.Repeat(breathPhase + breathsPerSecond * Mathf.PI * 2f * deltaTime, Mathf.PI * 2f);

            float step = Mathf.Sin(stepPhase);
            float lift = Mathf.Abs(step) * Mathf.Lerp(walkBounce, runBounce, running) * moving;
            float sway = step * Mathf.Lerp(walkSwayDegrees, runSwayDegrees, running) * moving;
            float stretch = Mathf.Sin(breathPhase) * breathingStretch * (1f - moving)
                - Mathf.Cos(stepPhase * 2f) * stepSquash * moving;
            // The pivot stays at the feet; breathing changes height without lifting the whole image.
            transform.localPosition = restPosition + Vector3.up * lift;
            transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, sway);
            transform.localScale = Vector3.Scale(restScale, new Vector3(1f - stretch * .35f, 1f + stretch, 1f));
        }

        public void SetFrozen(bool value)
        {
            frozen = value;
            if (movementSource != null) previousPosition = movementSource.position;
            if (value)
            {
                smoothedSpeed = 0f;
                RestorePose();
            }
        }

        void RestorePose()
        {
            transform.localPosition = restPosition;
            transform.localRotation = restRotation;
            transform.localScale = restScale;
        }
    }
}

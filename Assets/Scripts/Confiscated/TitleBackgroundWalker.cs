using UnityEngine;

namespace Confiscated
{
    /// <summary>
    /// Cosmetic-only pacing for a background character during the title sequence: walks back and forth between
    /// two points, pausing briefly at each end. The title runs with Time.timeScale=0, so this drives its own
    /// CutoutMotion directly with unscaled time (CutoutMotion's own LateUpdate still fires with a zero scaled
    /// delta and would otherwise snap the pose back to rest every frame; running after it lets this call win).
    /// Never a real gameplay actor - purely a double, spawned and destroyed by SchoolTitleMenu.
    /// </summary>
    [DefaultExecutionOrder(5000)]
    public sealed class TitleBackgroundWalker : MonoBehaviour
    {
        public Vector3 pointA = Vector3.zero, pointB = Vector3.forward;
        public float speed = 1.1f, pauseSeconds = 1.4f;

        CutoutMotion motion;
        bool towardB = true;
        float pauseUntil;

        void Awake()
        {
            motion = GetComponentInChildren<CutoutMotion>();
            transform.position = pointA;
            transform.rotation = Quaternion.LookRotation((pointB - pointA).normalized);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (Time.unscaledTime >= pauseUntil)
            {
                Vector3 target = towardB ? pointB : pointA;
                Vector3 delta = target - transform.position; delta.y = 0f;
                float step = speed * dt;
                if (delta.magnitude <= step)
                {
                    transform.position = new Vector3(target.x, transform.position.y, target.z);
                    towardB = !towardB;
                    pauseUntil = Time.unscaledTime + pauseSeconds;
                }
                else
                {
                    transform.position += delta.normalized * step;
                    transform.rotation = Quaternion.LookRotation(delta.normalized);
                }
            }
            if (motion != null) motion.Advance(dt);
        }
    }
}

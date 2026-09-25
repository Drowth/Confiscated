using UnityEngine;
using UnityEngine.Rendering;

namespace Confiscated
{
    /// <summary>
    /// Rotates a flat cutout (Quad, front face = local -Z) around the Y axis so it always faces the camera.
    /// Used on a character's visual-facing pivot. Animation belongs on a child so it cannot override this rotation.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(10000)]
    public class BillboardY : MonoBehaviour
    {
        [Tooltip("Optional explicit target. If empty, Camera.main is used.")]
        public Transform targetOverride;

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            Camera.onPreCull += OnCameraPreCull;
            LateUpdate();
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            Camera.onPreCull -= OnCameraPreCull;
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            FaceRenderingCamera(camera);
        }

        void OnCameraPreCull(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline == null) FaceRenderingCamera(camera);
        }

        void FaceRenderingCamera(Camera camera)
        {
            // Captures and Scene View can render between gameplay frames. Resolve facing
            // for each draw, after movement, instead of inheriting the previous camera's yaw.
            if (camera.cameraType == CameraType.Reflection || camera.cameraType == CameraType.Preview) return;
            FaceTarget(targetOverride != null ? targetOverride : camera.transform);
        }

        void LateUpdate()
        {
            Transform target = targetOverride;
            if (target == null && Camera.main != null) target = Camera.main.transform;
            if (target == null) return;
            FaceTarget(target);
        }

        public void FaceTarget(Transform target)
        {
            if (target == null) return;
            Vector3 toSelf = transform.position - target.position;
            toSelf.y = 0f;
            if (toSelf.sqrMagnitude < 1e-6f) return;
            // Quad front face points along local -Z, so forward must point away from the viewer.
            transform.rotation = Quaternion.LookRotation(toSelf.normalized, Vector3.up);
        }
    }
}

using UnityEngine;

namespace Confiscated
{
    /// <summary>Mr Reed's rear walking views follow the same forward vector as his sight cone.</summary>
    [DisallowMultipleComponent]
    public sealed class ReedDirectionalArt : MonoBehaviour
    {
        const float MetresPerStep = .7f;
        Renderer cutout;
        Texture front, huntFront, frontWalk, huntWalk, rearIdle, rearUp, rearDown;
        MaterialPropertyBlock block;
        Vector3 previousPosition;
        float travelled;

        public bool IsRearTexture(Texture texture) => texture != null &&
            (texture == rearIdle || texture == rearUp || texture == rearDown);
        public bool IsFrontWalkTexture(Texture texture, bool hunt) => texture != null && texture == (hunt ? huntWalk : frontWalk);

        void Awake()
        {
            cutout = GetComponentInChildren<MeshRenderer>();
            front = cutout != null ? cutout.sharedMaterial.GetTexture("_BaseMap") : null;
            huntFront = Resources.Load<Texture2D>("Art/Hunt/T_Mr_Reed_Hunt");
            frontWalk = Resources.Load<Texture2D>("Art/ReedFrontWalk");
            huntWalk = Resources.Load<Texture2D>("Art/ReedHuntWalk");
            rearIdle = Resources.Load<Texture2D>("Art/ReedRearIdle");
            rearUp = Resources.Load<Texture2D>("Art/ReedRearWalkUp");
            rearDown = Resources.Load<Texture2D>("Art/ReedRearWalkDown");
            block = new MaterialPropertyBlock();
            previousPosition = transform.position;
        }

        void OnEnable() { previousPosition = transform.position; travelled = 0f; }

        void LateUpdate()
        {
            if (cutout == null) return;
            Vector3 delta = transform.position - previousPosition;
            previousPosition = transform.position;
            delta.y = 0f;
            float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            bool walking = speed > .06f && speed < 12f;
            if (walking) travelled += delta.magnitude;
            else travelled = 0f;

            var viewer = Camera.main;
            bool rear = !ComicDialogue.IsAddressingPlayer(transform) && viewer != null &&
                Vector3.Dot(transform.forward, viewer.transform.position - transform.position) < 0f;
            bool hunt = SchoolRunController.Instance != null &&
                SchoolRunController.Instance.GetComponent<HuntFaces>()?.TeacherTurned == true;
            Texture art = hunt && huntFront != null ? huntFront : front;
            Vector4 crop = new Vector4(1, 1, 0, 0);
            float poseWidth = 1f;
            int step = Mathf.FloorToInt(travelled / MetresPerStep);
            if (!rear && walking && (hunt ? huntWalk : frontWalk) != null)
            {
                art = hunt ? huntWalk : frontWalk;
                // Measured centres in the generated sheets; equal-half crops made him
                // jump sideways on each step. The 550-pixel windows exclude the other pose.
                crop = new Vector4(550f / 1536f, 1, (step % 2 == 0 ? 253f : 737f) / 1536f, 0);
                poseWidth = .72f;
            }
            if (rear)
            {
                if (walking && rearUp != null)
                {
                    bool down = travelled / MetresPerStep % 1f > .5f;
                    art = down && rearDown != null ? rearDown : rearUp;
                    crop = new Vector4(.5f, 1f, step % 2 == 0 ? 0f : .5f, 0f);
                }
                else if (rearIdle != null) art = rearIdle;
            }
            cutout.GetPropertyBlock(block);
            block.SetTexture("_BaseMap", art);
            block.SetVector("_BaseMap_ST", crop);
            block.SetFloat("_PoseWidth", poseWidth);
            cutout.SetPropertyBlock(block);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Confiscated
{
    /// <summary>
    /// Sprint into a shut door you could open anyway and you barge it open: it bangs off the wall, the head jolts and the
    /// game hitches for a moment. The bang is a loud noise (the caretaker and Mr Reed come to the doorway) and the drawing
    /// says so: a sound ring spreads through the walls, a "!" pops over every adult it reaches, and a scribble marks the spot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorSlam : MonoBehaviour
    {
        public const string Source = "door slam";
        public const float Radius = 40, WaveSpeed = 34;
        public static int Slams;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetCount() => Slams = 0;
        public static readonly Color Ink = new Color(.78f, .2f, .14f, 1);
        FirstPersonController player;
        PlayerInteractor interactor;
        OfficeDoor[] doors;
        float nextAllowed;
        static Material material, depthTested;

        void Awake() { player = GetComponent<FirstPersonController>(); interactor = GetComponent<PlayerInteractor>(); }
        void OnDisable() { if (Time.timeScale == HitStopScale) Time.timeScale = 1; }

        void Update()
        {
            if (player == null || interactor == null || player.SprintIntent == Vector3.zero || Time.time < nextAllowed) return;
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
            if (doors == null) doors = FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None);
            Vector3 at = transform.position;
            foreach (var door in doors)
            {
                if (door == null || !door.isActiveAndEnabled) continue;
                Vector3 local = door.transform.InverseTransformPoint(at);
                float halfWidth = Mathf.Max(.5f, Mathf.Abs(door.transform.InverseTransformPoint(door.hinge.position).x)) + .1f;
                if (Mathf.Abs(local.x) > halfWidth || Mathf.Abs(local.z) > .75f || local.y < -.6f || local.y > 1.2f) continue;
                Vector3 into = door.transform.forward * (local.z > 0 ? -1 : 1);
                if (Vector3.Dot(player.SprintIntent, into) < .5f || !door.CanSlam(interactor)) continue;
                Slam(door);
                return;
            }
        }

        void Slam(OfficeDoor door)
        {
            nextAllowed = Time.time + .6f; Slams++;
            door.Slam(transform.position);
            GetComponent<ChaseCamera>()?.Kick(1);
            player.BriefDistraction(.2f); // the shoulder takes the hit: a stagger, no sprint for a moment
            StartCoroutine(HitStop());
            Vector3 spot = door.transform.position;
            Burst(door);
            Wave(spot, 0, 1); Wave(spot, .14f, .6f);
            Scribble(spot);
            // Adults the bang reaches get a "!" as the ring passes them, drawn through the walls.
            foreach (var adult in FindObjectsByType<CaretakerAI>(FindObjectsSortMode.None))
            {
                float distance = Vector3.Distance(adult.transform.position, spot);
                if (adult.WouldHear(spot, Radius)) Alert(adult.transform, distance / WaveSpeed);
            }
            HudController.Instance?.SetStatus("SLAM! The whole school heard that.", 2.5f);
            // After the line above, so the caretaker's own "He heard the door slam" wins when he is in range.
            NoiseEvents.Emit(spot, Radius, Source);
        }

        const float HitStopScale = .05f;
        static IEnumerator HitStop()
        {
            if (Time.timeScale != 1) yield break;
            Time.timeScale = HitStopScale;
            yield return new WaitForSecondsRealtime(.06f);
            // Something else (a comic balloon, the pause menu) may have taken time over meanwhile.
            if (Time.timeScale == HitStopScale) Time.timeScale = 1;
        }

        static Material Mat(bool throughWalls)
        {
            if (material == null)
            {
                // The Resources asset keeps the shader in player builds.
                var asset = Resources.Load<Material>("Art/M_SoundWave");
                material = asset != null ? asset : new Material(Shader.Find("Confiscated/Sound Wave"));
                depthTested = new Material(material) { name = "Sound wave (depth tested)" };
                depthTested.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            }
            return throughWalls ? material : depthTested;
        }

        static LineRenderer Stroke(Transform parent, int points, bool loop, bool throughWalls)
        {
            var g = new GameObject("Slam stroke"); g.transform.SetParent(parent, false);
            var line = g.AddComponent<LineRenderer>();
            line.sharedMaterial = Mat(throughWalls); line.positionCount = points; line.loop = loop; line.useWorldSpace = true;
            line.numCapVertices = 2; line.textureMode = LineTextureMode.Stretch; line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }

        static GameObject Effect(string name, Vector3 at) { var g = new GameObject(name); g.transform.position = at; return g; }

        /// <summary>Comic impact lines bursting out round the door, depth tested: it's right in front of the player.</summary>
        static void Burst(OfficeDoor door)
        {
            var root = Effect("Door slam burst", door.transform.position + Vector3.up * 1.1f);
            var lines = new List<LineRenderer>();
            for (int i = 0; i < 9; i++) lines.Add(Stroke(root.transform, 2, false, false));
            var run = root.AddComponent<SlamEffect>(); run.life = .4f;
            Vector3 right = door.transform.right, up = Vector3.up, centre = root.transform.position;
            run.tick = t =>
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    float a = i * Mathf.PI * 2 / lines.Count + .3f, r = .75f + t * 1.1f;
                    Vector3 dir = right * Mathf.Cos(a) * 1.1f + up * Mathf.Sin(a) * .95f;
                    lines[i].SetPosition(0, centre + dir * r); lines[i].SetPosition(1, centre + dir * (r + .45f * (1 - t) + .05f));
                    lines[i].widthMultiplier = .07f * (1 - t * .7f); var c = Ink; c.a = 1 - t * t;
                    lines[i].startColor = lines[i].endColor = c;
                }
            };
        }

        /// <summary>A pencil ring spreading out across the floor at the speed the noise travels, visible through walls.</summary>
        static void Wave(Vector3 centre, float delay, float strength)
        {
            var root = Effect("Door slam sound ring", centre);
            var ring = Stroke(root.transform, 90, true, true); ring.enabled = false;
            float seed = Random.value * 10, life = Radius / WaveSpeed;
            var run = root.AddComponent<SlamEffect>(); run.delay = delay; run.life = life;
            run.tick = t =>
            {
                ring.enabled = true;
                float r = Radius * t + .5f; // linear: the "!" over each adult pops as the ring reaches them
                for (int i = 0; i < 90; i++)
                {
                    float a = i * Mathf.PI * 2 / 90, wobble = 1 + .025f * Mathf.Sin(a * 7 + seed) + .015f * Mathf.Sin(a * 19 - seed * 2);
                    ring.SetPosition(i, centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r * wobble + Vector3.up * .08f);
                }
                ring.widthMultiplier = Mathf.Lerp(.35f, .12f, t) * strength;
                var c = Ink; c.a = strength * (1 - t) * (1 - t * .5f); ring.startColor = ring.endColor = c;
            };
        }

        /// <summary>Where they will come looking: a scribbled circle left on the floor, fading slowly.</summary>
        static void Scribble(Vector3 centre)
        {
            var root = Effect("Door slam heard-here scribble", centre);
            var line = Stroke(root.transform, 70, false, true);
            float seed = Random.value * 10;
            for (int i = 0; i < 70; i++)
            {
                float a = i * Mathf.PI * 2 * 2.3f / 70 + seed, r = .75f + .12f * Mathf.Sin(i * .61f + seed) + .04f * (i % 3);
                line.SetPosition(i, centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r + Vector3.up * .06f);
            }
            var run = root.AddComponent<SlamEffect>(); run.life = 8;
            run.tick = t => { line.widthMultiplier = .07f; var c = Ink; c.a = .85f * (1 - t) * Mathf.Min(1, t * 30); line.startColor = line.endColor = c; };
        }

        /// <summary>A hand-drawn "!" popping up over an adult who heard it: they know where you were.</summary>
        static void Alert(Transform adult, float delay)
        {
            var root = Effect("Heard the slam", adult.position);
            var bar = Stroke(root.transform, 2, false, true); var dot = Stroke(root.transform, 2, false, true);
            bar.enabled = dot.enabled = false;
            var run = root.AddComponent<SlamEffect>(); run.delay = delay; run.life = 1.8f;
            run.tick = t =>
            {
                if (adult == null) return;
                bar.enabled = dot.enabled = true;
                float pop = t < .08f ? Mathf.Lerp(.2f, 1.35f, t / .08f) : 1 + .35f * Mathf.Exp(-(t - .08f) * 14) * Mathf.Cos((t - .08f) * 40);
                var cam = Camera.main; Vector3 side = cam != null ? cam.transform.right : Vector3.right;
                Vector3 top = adult.position + Vector3.up * 2.55f;
                // Holds a readable size on screen however far across the school he is.
                float far = cam != null ? Vector3.Distance(cam.transform.position, top) : 10;
                float size = Mathf.Max(.7f, far * .11f) * pop, lean = Mathf.Sin(t * 22) * .03f * (1 - t);
                bar.SetPosition(0, top + (Vector3.up * .95f + side * lean) * size); bar.SetPosition(1, top + Vector3.up * .3f * size);
                dot.SetPosition(0, top + Vector3.up * .08f * size); dot.SetPosition(1, top);
                bar.startWidth = .42f * size; bar.endWidth = .2f * size; dot.widthMultiplier = .36f * size;
                var c = Ink; c.a = t > .75f ? (1 - t) / .25f : 1; bar.startColor = bar.endColor = dot.startColor = dot.endColor = c;
            };
        }
    }

    /// <summary>Runs one of DoorSlam's drawn effects over its life (t 0..1), then removes it.</summary>
    public sealed class SlamEffect : MonoBehaviour
    {
        public float delay, life = 1;
        public System.Action<float> tick;
        float age;
        void Update()
        {
            age += Time.deltaTime;
            if (age < delay) return;
            float t = (age - delay) / life;
            if (t >= 1) { Destroy(gameObject); return; }
            tick?.Invoke(t);
        }
    }
}

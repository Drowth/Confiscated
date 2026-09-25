using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
namespace Confiscated
{
    /// <summary>A confiscated wind-up toy: delayed noise buys time after breaking sight.</summary>
    public sealed class ClockworkDecoy : MonoBehaviour
    {
        public int Charges { get; private set; }
        public GameObject toyPrefab;
        PlayerInteractor player;
        static AudioClip quackClip;
        public static int TotalDeployedThisRun { get; private set; }
        public static void ResetRunTally() => TotalDeployedThisRun = 0;
        void Awake() { player = GetComponent<PlayerInteractor>(); if (quackClip == null) quackClip = Resources.Load<AudioClip>("Audio/ToyDuck"); }
        public void Collect() { Charges = Mathf.Min(3, Charges + 1); HudController.Instance?.SetStatus("Q / right click: leave a toy, then turn a corner. It rattles in 2 seconds. He must lose sight of you first.", 8); }
        void Update() { if ((Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)||(Mouse.current!=null&&Mouse.current.rightButton.wasPressedThisFrame)) Deploy(); }
        public bool Deploy()
        {
            if (player.InputLocked || ComicDialogue.IsActive || Cursor.lockState != CursorLockMode.Locked || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return false;
            if(Charges==0){HudController.Instance?.SetStatus("No wind-up toys left. Find one on a desk.",2);return false;}
            Vector3 forward = player.ViewCamera.transform.forward; forward.y = 0; forward.Normalize();
            Vector3 desired = transform.position + forward * .85f;
            if (!NavMesh.SamplePosition(desired, out var floor, .7f, NavMesh.AllAreas) || NavMesh.Raycast(transform.position, floor.position, out _, NavMesh.AllAreas)) return false;
            if (Physics.Linecast(transform.position + Vector3.up * .4f, floor.position + Vector3.up * .4f, ~0, QueryTriggerInteraction.Ignore)) return false;
            Charges--;
            TotalDeployedThisRun++;
            GameObject toy = Instantiate(toyPrefab, floor.position, Quaternion.LookRotation(forward));
            toy.name = "Ticking wind-up decoy";
            // A flat cutout must stay square-on to whoever is looking; a modelled toy reads from any side.
            if (toy.transform.Find("Cutout card") != null) toy.AddComponent<BillboardY>();
            StartCoroutine(Rattle(toy));
            HudController.Instance?.SetStatus("Toy wound. Turn a corner before it rattles!", 2); return true;
        }
        IEnumerator Rattle(GameObject toy)
        {
            yield return new WaitForSeconds(2);
            Vector3 origin = toy.transform.position;
            Quaternion originalRotation = toy.transform.rotation;
            bool diverted = false, explained = false;
            bool canTilt = toy.GetComponent<BillboardY>() == null;
            AudioSource quack = null;
            if (quackClip != null)
            {
                quack = toy.AddComponent<AudioSource>();
                quack.clip = quackClip; quack.playOnAwake = false; quack.loop = false;
                quack.spatialBlend = 1f; quack.minDistance = 2f; quack.maxDistance = 16f; quack.volume = .85f;
                quack.Play();
            }
            float duration = quackClip != null ? quackClip.length : 8f;
            float t = 0f, nextNoiseAt = 0f;
            while (t < duration)
            {
                if (toy == null) yield break;
                if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
                {
                    float bounce = Mathf.Abs(Mathf.Sin(t * 13f)) * .1f;
                    float wobbleX = (Mathf.PerlinNoise(t * 29f, 1.7f) - .5f) * .03f;
                    float wobbleZ = (Mathf.PerlinNoise(3.1f, t * 31f) - .5f) * .03f;
                    toy.transform.position = origin + new Vector3(wobbleX, bounce, wobbleZ);
                    if (canTilt) toy.transform.rotation = originalRotation * Quaternion.Euler(0, Mathf.Sin(t * 18f) * 9f, 0);
                    if (t >= nextNoiseAt)
                    {
                        nextNoiseAt += .5f;
                        NoiseEvents.Emit(origin, 38, "clockwork toy");
                        var caretaker=SchoolRunController.Instance?.caretaker;
                        if(!diverted&&caretaker!=null&&caretaker.Investigating(origin))
                        {diverted=true;HudController.Instance?.SetStatus("He is checking the toy. Keep moving!",3);}
                        else if(!explained&&!diverted&&caretaker!=null&&caretaker.Current==CaretakerAI.State.Chase)
                        {explained=true;HudController.Instance?.SetStatus("He is still following you. Get behind a wall or closed door!",3);}
                        if (quackClip == null) TempAudio.PlayAt(TempAudio.Warn, origin, .35f);
                    }
                }
                t += Time.deltaTime;
                yield return null;
            }
            if (toy != null) { toy.transform.SetPositionAndRotation(origin, originalRotation); Destroy(toy); }
        }
    }
}

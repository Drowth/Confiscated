using UnityEngine;
using UnityEngine.AI;
namespace Confiscated
{
    public sealed class RunGate : Interactable
    {
        public enum Kind { Chain, Shortcut, Exit }
        public Kind kind;
        public Transform obstacle;
        public Vector3 clearedLocalPosition;
        public Vector3 clearedLocalEuler;
        public bool Cleared { get; private set; }
        public override string GetPrompt(PlayerInteractor p)
        {
            var run = SchoolRunController.Instance;
            if (kind == Kind.Exit) return run.ReadyToEscape ? "Hold F: escape with all five items" : "MAIN EXIT - Recover all five items and bring your phone.";
            if (Cleared) return null;
            if (kind == Kind.Shortcut) return "Hold F: push trolley aside (noisy)";
            return run.HasBoltCutters && run.RoundStarted ? "Hold F: cut the property cage chain (noisy)" : "CHAINED - Bolt cutters needed. Check EQUIPMENT.";
        }
        public override bool CanInteract(PlayerInteractor p)
        {
            var run = SchoolRunController.Instance;
            return run != null && (kind == Kind.Exit ? run.ReadyToEscape : !Cleared && (kind == Kind.Shortcut || run.RoundStarted && run.HasBoltCutters));
        }
        public override void Interact(PlayerInteractor p)
        {
            if (!CanInteract(p)) return;
            if (kind == Kind.Exit) { DoorSounds.For(gameObject,DoorSounds.Kind.Main).Play(true); SchoolRunController.Instance.Escape(); return; }
            Cleared = true;if(kind==Kind.Chain)SchoolRunController.Instance.CageOpen=true;
            if (obstacle != null) { obstacle.localPosition = clearedLocalPosition; obstacle.localRotation = Quaternion.Euler(clearedLocalEuler); }
            if(kind==Kind.Shortcut)
            {
                var sound=SchoolAudio.Create(obstacle!=null?obstacle.gameObject:gameObject);
                sound.clip=Resources.Load<AudioClip>("Audio/DinnerTrolleyMove");sound.spatialBlend=1;
                sound.rolloffMode=AudioRolloffMode.Linear;sound.minDistance=2;sound.maxDistance=25;sound.dopplerLevel=0;sound.volume=.85f;
                if(sound.clip!=null){sound.Play();Destroy(sound,sound.clip.length+.1f);}else Destroy(sound);
            }
            var blocker = GetComponent<NavMeshObstacle>(); if (blocker != null) blocker.enabled = false;
            NoiseEvents.Emit(transform.position, 45, "moving furniture");
            HudController.Instance?.SetStatus(kind == Kind.Chain ? "Chain cut. The cage stays open." : "Shortcut cleared. He may have heard that.", 4);
        }
    }
}


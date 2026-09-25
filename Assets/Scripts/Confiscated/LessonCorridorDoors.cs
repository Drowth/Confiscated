using UnityEngine;

namespace Confiscated
{
    /// <summary>Corridor fire doors kept shut during lessons. They swing open and stay open once the escape run begins.</summary>
    public sealed class LessonCorridorDoors : Interactable
    {
        public Transform hinge, secondHinge;
        public float openAngle = -102f, secondOpenAngle = 102f;
        [Tooltip("Collision and navigation block across the opening while the doors are shut.")]
        public GameObject barrier;
        public bool IsOpen => openAmount > .9f;
        DoorSounds doorSounds;
        float openAmount;
        Quaternion closedRotation, secondClosedRotation;

        void Awake()
        {
            closedRotation = hinge.localRotation;
            secondClosedRotation = secondHinge.localRotation;
            doorSounds = DoorSounds.For(gameObject, DoorSounds.Kind.Heavy);
        }

        public override string GetPrompt(PlayerInteractor player) =>
            SchoolRunController.LessonsInProgress ? "Corridor doors are kept shut during lessons." : null;
        public override bool CanInteract(PlayerInteractor player) => false;
        public override void Interact(PlayerInteractor player) { }

        void Update()
        {
            bool shut = SchoolRunController.LessonsInProgress;
            if (barrier != null && barrier.activeSelf != shut) barrier.SetActive(shut);
            float previous = openAmount;
            // A chase retry begins with the run already under way: no swing, no sound.
            openAmount = !shut && Time.timeSinceLevelLoad < 1f ? 1f : Mathf.MoveTowards(openAmount, shut ? 0f : 1f, Time.deltaTime * 1.6f);
            if (Time.timeSinceLevelLoad >= 1f) doorSounds.Movement(previous, openAmount);
            float t = Mathf.SmoothStep(0, 1, openAmount);
            hinge.localRotation = closedRotation * Quaternion.Euler(0, openAngle * t, 0);
            secondHinge.localRotation = secondClosedRotation * Quaternion.Euler(0, secondOpenAngle * t, 0);
        }
    }
}

using UnityEngine;

namespace Confiscated
{
    /// <summary>Blue corridor doors swing open permanently when the office release is pressed.</summary>
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
            doorSounds.openingOverride=Resources.Load<AudioClip>("Audio/Doors/BlueCorridorOpen");
        }

        public override string GetPrompt(PlayerInteractor player) =>
            SchoolRunController.Instance!=null&&!SchoolRunController.Instance.CorridorDoorsReleased ? "BLUE DOORS - Release button in the caretaker's office." : null;
        public override bool CanInteract(PlayerInteractor player) => false;
        public override void Interact(PlayerInteractor player) { }

        void Update()
        {
            bool shut = SchoolRunController.Instance==null||!SchoolRunController.Instance.CorridorDoorsReleased;
            if (barrier != null && barrier.activeSelf != shut) barrier.SetActive(shut);
            float previous = openAmount;
            float speed=doorSounds.openingOverride!=null?1f/Mathf.Max(.1f,doorSounds.openingOverride.length):1.6f;
            openAmount = Mathf.MoveTowards(openAmount, shut ? 0f : 1f, Time.deltaTime * speed);
            doorSounds.Movement(previous, openAmount);
            float t = Mathf.SmoothStep(0, 1, openAmount);
            hinge.localRotation = closedRotation * Quaternion.Euler(0, openAngle * t, 0);
            secondHinge.localRotation = secondClosedRotation * Quaternion.Euler(0, secondOpenAngle * t, 0);
        }
    }
}

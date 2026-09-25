using UnityEngine;
using UnityEngine.AI;
namespace Confiscated
{
    /// <summary>Locked objective doors block both navigation and physical tailgating.</summary>
    public sealed class RunDoorBlocker : MonoBehaviour
    {
        public OfficeDoor door;
        public GameObject barrier;
        void Update()
        {
            if (door == null || barrier == null) return;
            bool locked = door.closedForRun || !door.IsUnlocked || door.LessonLocked;
            if (barrier.activeSelf != locked) barrier.SetActive(locked);
        }
    }
}

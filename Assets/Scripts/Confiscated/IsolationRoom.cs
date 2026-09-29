using UnityEngine;

namespace Confiscated
{
    /// <summary>
    /// The secret "reflection room": a small isolation cell cut from the corner of the closed South room A. Its only
    /// way in is a boarded-up doorway on the south-west corridor, with light showing underneath. It stays boarded in
    /// every mode for now; it can only be prised open in Dark Mode, once something sets <see cref="Unlocked"/> (how the
    /// player earns that is still to be decided). It will hold a secret ending. Its bulb is the one light the blackout
    /// leaves burning.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IsolationRoom : MonoBehaviour
    {
        public BoardedDoorway boards;
        public Light bulb;
        [Tooltip("Warm strip on the corridor floor at the foot of the boards.")]
        public GameObject lightUnderDoor;
        /// <summary>Set by whatever ends up earning the way in. Only counts in Dark Mode.</summary>
        public static bool Unlocked {get;set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>Unlocked=false;
        public static bool Open=>SchoolGameMode.Dark&&Unlocked;
        public static string LockedPrompt=>SchoolGameMode.Dark?"Boarded up. There's a light on behind it...":"Boarded up.";
    }
}

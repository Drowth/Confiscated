using UnityEngine;

namespace Confiscated
{
    /// <summary>
    /// The secret "reflection room": a small isolation cell cut from the corner of the closed South room A, its one door
    /// on the south-west corridor. Locked in every mode for now. It only ever opens in Dark Mode, once something sets
    /// <see cref="Unlocked"/> (how the player earns that is still to be decided); it will hold a secret ending.
    /// Its bulb is the one light the blackout leaves burning, and in Dark Mode it shows as a line of light under the door.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IsolationRoom : MonoBehaviour
    {
        public OfficeDoor door;
        public Light bulb;
        [Tooltip("Warm strip on the corridor floor at the foot of the door, shown only during the Dark Mode blackout.")]
        public GameObject lightUnderDoor;
        /// <summary>Set by whatever ends up earning the key. Only counts in Dark Mode.</summary>
        public static bool Unlocked {get;set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>Unlocked=false;
        public static bool Open=>SchoolGameMode.Dark&&Unlocked;
        public static string LockedPrompt=>SchoolGameMode.Dark?"Locked. There's a light on in there...":"Locked.";
        DarkModeController dark;

        void Start(){dark=FindFirstObjectByType<DarkModeController>();}
        void Update()
        {
            bool leak=SchoolGameMode.Dark&&dark!=null&&dark.BlackedOut;
            if(lightUnderDoor!=null&&lightUnderDoor.activeSelf!=leak)lightUnderDoor.SetActive(leak);
        }
    }
}

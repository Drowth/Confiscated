using System;
using UnityEngine;

namespace Confiscated
{
    /// <summary>Broadcast channel for things the caretaker can hear. Subscribers must unsubscribe in OnDisable.</summary>
    public static class NoiseEvents
    {
        /// <summary>(world position, audible radius in metres, source label)</summary>
        public static event Action<Vector3, float, string> OnNoise;

        public static void Emit(Vector3 position, float radius, string source)
        {
            OnNoise?.Invoke(position, radius, source);
        }
    }
}

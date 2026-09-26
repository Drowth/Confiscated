using UnityEngine;
namespace Confiscated
{
    /// <summary>Unsteady practical light: a bare bulb's odd flicker, or a slow firebox pulse.</summary>
    [RequireComponent(typeof(Light))]
    public sealed class LightFlicker : MonoBehaviour
    {
        public enum Mode { Bulb, Glow }
        public Mode mode;
        public float baseIntensity = 1f;
        [Range(0, 1)] public float depth = .25f;
        Light lamp; float seed, dipUntil;
        void Awake() { lamp = GetComponent<Light>(); seed = Random.value * 100f; }
        void Update()
        {
            float t = Time.time;
            float wave = mode == Mode.Glow
                ? .5f + .5f * Mathf.Sin(t * 1.3f + seed) * Mathf.PerlinNoise(t * .4f, seed)
                : Mathf.PerlinNoise(t * 7f, seed);
            // The bulb now and then drops out for a moment, like a loose filament.
            if (mode == Mode.Bulb && Time.time > dipUntil && Random.value < Time.deltaTime * .25f) dipUntil = Time.time + Random.Range(.05f, .18f);
            float dip = Time.time < dipUntil ? .35f : 1f;
            lamp.intensity = baseIntensity * (1f - depth + depth * wave) * dip;
        }
    }
}

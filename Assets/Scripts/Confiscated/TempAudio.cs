using UnityEngine;

namespace Confiscated
{
    /// <summary>
    /// TEMPORARY procedural sound effects so the loop is playable without audio assets.
    /// Replace with real clips by assigning AudioClips on the components that call these.
    /// </summary>
    public static class TempAudio
    {
        const int Rate = 22050;

        static AudioClip ringClip, warnClip, thudClip, caughtClip, winClip, pickupClip, jingleClip, whisperClip, buzzClip;

        public static AudioClip Ring => ringClip ??= Build("Temp_Ring", 1.6f, t =>
        {
            // Old mobile "brr-brr": two bursts of a 1.3 kHz tone modulated at 25 Hz.
            float env = (t % 0.8f) < 0.3f ? 1f : 0f;
            float am = 0.6f + 0.4f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 25f * t));
            return env * am * 0.5f * Mathf.Sin(2f * Mathf.PI * 1300f * t);
        });

        public static AudioClip Warn => warnClip ??= Build("Temp_Warn", 0.35f, t =>
            0.4f * Mathf.Sin(2f * Mathf.PI * 90f * t) * (1f - t / 0.35f));

        public static AudioClip Thud => thudClip ??= Build("Temp_Thud", 0.18f, t =>
            0.8f * Mathf.Sin(2f * Mathf.PI * (140f - 300f * t) * t) * Mathf.Exp(-t * 22f));

        public static AudioClip Caught => caughtClip ??= Build("Temp_Caught", 0.9f, t =>
            0.5f * Mathf.Sin(2f * Mathf.PI * (220f - 120f * t) * t) * (1f - t / 0.9f));

        public static AudioClip Win => winClip ??= Build("Temp_Win", 0.8f, t =>
        {
            float f = t < 0.25f ? 523f : t < 0.5f ? 659f : 784f;
            return 0.4f * Mathf.Sin(2f * Mathf.PI * f * t) * (1f - (t % 0.25f) / 0.3f);
        });

        public static AudioClip Pickup => pickupClip ??= Build("Temp_Pickup", 0.15f, t =>
            0.35f * Mathf.Sin(2f * Mathf.PI * (600f + 1200f * t) * t) * (1f - t / 0.15f));

        // A phone on vibrate: two short buzzes.
        public static AudioClip Buzz => buzzClip ??= Build("Temp_Buzz", 0.75f, t =>
        {
            float on = (t % 0.4f) < 0.26f ? 1f : 0f;
            return on * 0.35f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 165f * t)) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 31f * t));
        });

        // A bunch of keys shaken: metal clinks (inharmonic partials, fast decay) scattered over 0.7 s.
        static readonly float[] Clinks = { 0f, .07f, .16f, .21f, .33f, .41f, .52f, .6f };
        public static AudioClip Jingle => jingleClip ??= Build("Temp_Jingle", 0.8f, t =>
        {
            float v = 0f;
            for (int i = 0; i < Clinks.Length; i++)
            {
                float d = t - Clinks[i]; if (d < 0f) continue;
                float f = 2600f + 370f * (i % 4);
                v += (Mathf.Sin(2f * Mathf.PI * f * d) + .6f * Mathf.Sin(2f * Mathf.PI * f * 1.73f * d) + .35f * Mathf.Sin(2f * Mathf.PI * f * 2.61f * d)) * Mathf.Exp(-d * 38f);
            }
            return .22f * v;
        });

        // A breathy whisper: hiss shaped into five syllables, fading in and out over 1.5 s.
        public static AudioClip Whisper => whisperClip ??= BuildNoise("Temp_Whisper", 1.5f, (t, n) =>
            n * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 3.3f * t)), 2f) * Mathf.Sin(Mathf.PI * t / 1.5f) * .5f);

        static AudioClip BuildNoise(string name, float seconds, System.Func<float, float, float> f)
        {
            var rng = new System.Random(7); float last = 0f;
            return Build(name, seconds, t => { float r = (float)rng.NextDouble() * 2f - 1f, hiss = r - last; last = r; return f(t, hiss); });
        }

        static AudioClip Build(string name, float seconds, System.Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume);
        }
    }
}

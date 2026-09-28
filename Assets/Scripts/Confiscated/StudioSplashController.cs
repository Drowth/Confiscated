using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Confiscated
{
    /// <summary>Studio logo splash: fades in, holds, fades out, then loads the next scene. Any key/click/button skips ahead.</summary>
    public class StudioSplashController : MonoBehaviour
    {
        public CanvasGroup logo;
        public string nextScene = "SchoolLayout";
        public float fadeIn = .6f, hold = 1.8f, fadeOut = .6f;

        enum Phase { In, Hold, Out }
        Phase phase = Phase.In;
        float t;
        bool loading;

        void Update()
        {
            var kb = Keyboard.current; var mouse = Mouse.current; var pad = Gamepad.current;
            bool skip = (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                        (kb != null && kb.anyKey.wasPressedThisFrame) ||
                        (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (skip) { Load(); return; }

            t += Time.deltaTime;
            switch (phase)
            {
                case Phase.In:
                    logo.alpha = fadeIn > 0 ? Mathf.Clamp01(t / fadeIn) : 1;
                    if (t >= fadeIn) { t = 0; phase = Phase.Hold; }
                    break;
                case Phase.Hold:
                    logo.alpha = 1;
                    if (t >= hold) { t = 0; phase = Phase.Out; }
                    break;
                case Phase.Out:
                    logo.alpha = fadeOut > 0 ? 1 - Mathf.Clamp01(t / fadeOut) : 0;
                    if (t >= fadeOut) Load();
                    break;
            }
        }

        void Load()
        {
            if (loading) return;
            loading = true;
            SceneManager.LoadScene(nextScene);
        }
    }
}

using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Player footsteps: clips load, pacing follows ground covered, and a real walk down the west corridor is audible.</summary>
    [InitializeOnLoad]
    public static class PlayerFootstepSmokeTest
    {
        const string Marker = "Temp/run_player_footsteps", Report = "../Docs/PlayerFootsteps_Validation.txt";
        static int stage, errors, lastFrame, startSteps; static bool heard, background; static double started, at; static float walked;
        static SchoolRunController R => Object.FindFirstObjectByType<SchoolRunController>();
        static SchoolPeriodController S => R.period;
        static FirstPersonController F => S.Player.GetComponent<FirstPersonController>();
        static PlayerFootstepAudio Steps => F.GetComponent<PlayerFootstepAudio>();

        static PlayerFootstepSmokeTest() { EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode && File.Exists(Marker)) { File.Delete(Marker); Begin(); } }; }
        [MenuItem("Confiscated/Play Test/Arm Player Footstep Test")]
        public static void Arm() { File.WriteAllText(Marker, "armed"); }
        static void Begin()
        {
            stage = errors = 0; lastFrame = -1; heard = false; walked = 0; started = at = EditorApplication.timeSinceStartup;
            background = Application.runInBackground; Application.runInBackground = true;
            File.WriteAllText(Report, "Player footsteps: pacing checks on the live component, then a real collision-tested walk.\n");
            Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        static void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception) { errors++; File.AppendAllText(Report, "ERROR " + message + "\n"); } }
        static void Need(bool value, string label) { File.AppendAllText(Report, (value ? "ok   " : "FAIL ") + label + "\n"); if (!value) throw new Exception(label); }
        static void Next(int value) { stage = value; at = EditorApplication.timeSinceStartup; }
        static int Simulate(float speed, float seconds, bool sprinting)
        {
            int before = Steps.StepCount; for (float t = 0; t < seconds; t += 1 / 60f) Steps.Advance(speed / 60f, 1 / 60f, sprinting); return Steps.StepCount - before;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { Finish(false); return; }
            if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
            double now = EditorApplication.timeSinceStartup;
            try
            {
                if (now - started > 150) throw new Exception("Timeout at stage " + stage);
                if (SchoolTitleMenu.IsActive) { SchoolTitleMenu.Instance.StartGame(); SchoolTitleMenu.Instance.SkipIntro(); return; }
                if (ComicDialogue.IsActive) { ComicDialogue.Instance.Advance(); return; }
                switch (stage)
                {
                    case 0:
                        if (now - at < 1) return;
                        Need(Steps != null && Steps.Ready, "footstep component attached with both recordings loaded");
                        Need(F.FallVoice != null && F.FallVoice.clip != null && F.FallVoice.clip.loadState == AudioDataLoadState.Loaded, "fall recording is loaded before any slip can happen");
                        Need(Steps.StepCount == 0, "silent while seated at the desk");
                        int walk = Simulate(2.4f, 3, false), sprint = Simulate(4f, 3, true);
                        Need(walk >= 6 && walk <= 7, "walking pace: " + walk + " steps in 3 s (about two a second)");
                        Need(sprint >= 9 && sprint <= 10, "sprinting pace: " + sprint + " steps in 3 s, quicker than walking");
                        int foot = Steps.LastFoot; Simulate(2.4f, .5f, false); Need(Steps.LastFoot != foot, "feet alternate");
                        Need(Simulate(0, 2, false) == 0, "pushing against a wall covers no ground and makes no sound");
                        int beforeWarp = Steps.StepCount; Steps.Advance(30, 1 / 60f, false); Need(Steps.StepCount == beforeWarp, "a warp is not a stride");
                        Time.timeScale = 4; S.ChooseSentence(2); Next(1); break;
                    case 1:
                        if (S.Current != SchoolPeriodController.Phase.Volunteer) return;
                        S.Volunteer(); R.caretaker.ResumeAfterDetention(999); Time.timeScale = 1;
                        F.Controller.enabled = false; F.transform.SetPositionAndRotation(new Vector3(-33.0f, 0, 36), Quaternion.identity); F.Controller.enabled = true; Physics.SyncTransforms();
                        Next(2); break;
                    case 2:
                        if (now - at < .5) return;
                        // Closing dialogue restores whatever time scale was live when it opened; audio is always real time.
                        Time.timeScale = 1; startSteps = Steps.StepCount; Next(3); break;
                    case 3:
                        // A real collision-tested walk north along the west corridor at walking speed.
                        float step = 2.4f * Time.deltaTime; F.Controller.Move(Vector3.forward * step + Vector3.down * .02f); walked += step;
                        heard |= Steps.Feet.isPlaying;
                        if (walked < 6.5f) return;
                        int taken = Steps.StepCount - startSteps;
                        Need(taken >= 5 && taken <= 7, "real 6.5 m walk produced " + taken + " footsteps");
                        Need(heard, "footstep audio was actually playing during the walk");
                        Time.timeScale = 1; F.Slip(); startSteps = Steps.StepCount; Next(4); break;
                    case 4:
                        if (now - at < .3) return;
                        Need(F.IsFallen && F.FallVoice != null && F.FallVoice.clip != null && F.FallVoice.isPlaying, "slipping plays the fall recording from the moment of the slip");
                        Need(Mathf.Abs(F.FallVoice.time - F.FallTime) < .1f, "recording runs in step with the fall (audio " + F.FallVoice.time.ToString("F2") + " s, fall " + F.FallTime.ToString("F2") + " s), so its thud at .52 s meets the floor impact at .48 s");
                        Next(5); break;
                    case 5:
                        if (F.IsFallen) return;
                        Need(Steps.StepCount == startSteps, "no footsteps while sliding and lying on the floor");
                        Need(!F.FallVoice.isPlaying, "fall recording has finished by the time the player is back up");
                        Need(errors == 0, "no runtime errors"); Finish(true); break;
                }
            }
            catch (Exception e) { File.AppendAllText(Report, "FAIL stage " + stage + ": " + e.Message + "\n"); Finish(false); }
        }
        static void Finish(bool success)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log; Time.timeScale = 1; Application.runInBackground = background;
            File.AppendAllText(Report, success ? "PASS\n" : "FAIL\n"); EditorApplication.isPlaying = false;
        }
    }
}

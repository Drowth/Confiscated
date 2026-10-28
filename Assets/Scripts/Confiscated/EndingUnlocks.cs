using UnityEngine;
namespace Confiscated
{
    /// <summary>Persistent record of which School Run endings the player has reached, shown on the title screen.</summary>
    public static class EndingUnlocks
    {
        public enum Ending { Caught, Completed, FastRun, QuackEscape, DarkEscape }
        static string Key(Ending e) => "Confiscated.Ending.v1." + e;
        /// <summary>Steamworks achievement API name, e.g. ENDING_FASTRUN.</summary>
        public static string AchievementId(Ending e) => "ENDING_" + e.ToString().ToUpperInvariant();
        public static bool IsUnlocked(Ending e) => PlayerPrefs.GetInt(Key(e), 0) == 1;
        public static void Unlock(Ending e)
        {
            // Steam is told every time, so an ending first reached offline still unlocks the next time it is reached on Steam.
            SteamLeaderboard.Achieve(AchievementId(e));
            if (IsUnlocked(e)) return;
            PlayerPrefs.SetInt(Key(e), 1);
            PlayerPrefs.Save();
        }
    }
}

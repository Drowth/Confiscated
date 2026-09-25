using UnityEngine;
namespace Confiscated
{
    /// <summary>Persistent record of which School Run endings the player has reached, shown on the title screen.</summary>
    public static class EndingUnlocks
    {
        public enum Ending { Caught, Completed, FastRun, QuackEscape }
        static string Key(Ending e) => "Confiscated.Ending.v1." + e;
        public static bool IsUnlocked(Ending e) => PlayerPrefs.GetInt(Key(e), 0) == 1;
        public static void Unlock(Ending e)
        {
            if (IsUnlocked(e)) return;
            PlayerPrefs.SetInt(Key(e), 1);
            PlayerPrefs.Save();
        }
    }
}

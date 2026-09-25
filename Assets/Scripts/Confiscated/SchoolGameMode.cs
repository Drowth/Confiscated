using UnityEngine;
namespace Confiscated
{
    public static class SchoolGameMode
    {
        public static bool Dark {get;private set;}
        #if UNITY_EDITOR
        // Editor-only dev switch (menu Confiscated/Dark Mode/Dev Unlock): opens Dark Mode without touching the saved endings.
        public const string DevUnlockKey="Confiscated.DevUnlockDark";
        public static bool DevUnlock{get=>UnityEditor.EditorPrefs.GetBool(DevUnlockKey,false);set=>UnityEditor.EditorPrefs.SetBool(DevUnlockKey,value);}
        #else
        public static bool DevUnlock=>false;
        #endif
        public static bool DarkUnlocked=>DevUnlock||EndingUnlocks.IsUnlocked(EndingUnlocks.Ending.Completed)
            ||EndingUnlocks.IsUnlocked(EndingUnlocks.Ending.FastRun)||EndingUnlocks.IsUnlocked(EndingUnlocks.Ending.QuackEscape);
        public static bool Select(bool dark)
        {
            if(dark&&!DarkUnlocked)return false;
            Dark=dark;return true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>Dark=false;
    }
}

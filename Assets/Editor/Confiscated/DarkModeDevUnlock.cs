using UnityEditor;
namespace Confiscated.EditorTools
{
    // Lets the title screen's Dark mode button work in the editor without replaying the base game. Stored in EditorPrefs, so it never reaches a build or the saved endings.
    public static class DarkModeDevUnlock
    {
        const string Path="Confiscated/Dark Mode/Dev Unlock";
        [MenuItem(Path)] static void Toggle()=>SchoolGameMode.DevUnlock=!SchoolGameMode.DevUnlock;
        [MenuItem(Path,true)] static bool Validate(){Menu.SetChecked(Path,SchoolGameMode.DevUnlock);return true;}
    }
}

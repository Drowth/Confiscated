using System.Linq;
using UnityEngine.InputSystem;
using UnityEditor;
namespace Confiscated.EditorTools
{
    /// <summary>
    /// Input-driving tests (EscapeFeelSmokeTest, AutoRunBot) disable the real mouse and keyboard and add virtual ones.
    /// If a test dies before its own cleanup (a recompile, Play stopped mid-run), the real devices stayed off and the
    /// title menu ignored clicks. Put them back whenever scripts reload or Play ends.
    /// </summary>
    [InitializeOnLoad] public static class TestInputRestore
    {
        static TestInputRestore()
        {
            Restore();
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)Restore();};
        }
        public static void Restore()
        {
            foreach(var d in InputSystem.devices.Where(d=>!d.native&&(d is Mouse||d is Keyboard)).ToArray())InputSystem.RemoveDevice(d);
            foreach(var d in InputSystem.devices.Where(d=>d.native&&!d.enabled&&(d is Mouse||d is Keyboard)).ToArray())InputSystem.EnableDevice(d);
        }
    }
}

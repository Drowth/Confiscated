using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Confiscated.EditorTools
{
    public static class EscapeLoopSetup
    {
        [MenuItem("Confiscated/School Run/Install Simple Escape Loop")]
        public static void Build(){SchoolRunSetup.RefreshEscapeLoop();}
    }
}

using UnityEngine;
namespace Confiscated
{
    // Retain the original wall renderers so the editor pass can be reapplied without losing the school shell.
    public sealed class OutdoorEnvironment : MonoBehaviour
    {
        public Renderer[] hiddenWallRenderers;
    }
}

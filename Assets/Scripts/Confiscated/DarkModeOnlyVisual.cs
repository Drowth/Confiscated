using UnityEngine;
namespace Confiscated
{
    /// <summary>Optional environmental clues that only appear in a Dark Mode run.</summary>
    [DisallowMultipleComponent]
    public sealed class DarkModeOnlyVisual : MonoBehaviour
    {
        public GameObject visual;
        void OnEnable()=>RefreshVisibility();
        void Update()=>RefreshVisibility();
        public void RefreshVisibility()
        {
            if(visual!=null&&visual!=gameObject&&visual.activeSelf!=SchoolGameMode.Dark)
                visual.SetActive(SchoolGameMode.Dark);
        }
    }
}

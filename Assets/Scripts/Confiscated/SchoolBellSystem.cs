using UnityEngine;

namespace Confiscated
{
    /// <summary>One event rings all scene bells on the same audio clock. No hidden global speaker.</summary>
    public sealed class SchoolBellSystem : MonoBehaviour
    {
        public SchoolBell[] bells;
        public bool IsRinging
        {
            get { if(bells!=null)foreach(var bell in bells)if(bell!=null&&bell.Source.isPlaying)return true;return false; }
        }
        [ContextMenu("Ring school bells")]
        public void Ring()
        {
            if(!Application.isPlaying || !isActiveAndEnabled || IsRinging)return;
            double start=AudioSettings.dspTime+.1;
            if(bells!=null)foreach(var bell in bells)if(bell!=null)bell.RingAt(start);
        }
        [ContextMenu("Stop school bells")]
        public void Stop()
        {
            if(bells!=null)foreach(var bell in bells)if(bell!=null)bell.StopRinging();
        }
        void OnDisable(){Stop();}
    }
}

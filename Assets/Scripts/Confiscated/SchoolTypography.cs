using UnityEngine;

namespace Confiscated
{
    /// <summary>Shared, build-safe lettering and quiet paper for readable school artwork.</summary>
    public static class SchoolTypography
    {
        static Font handwriting;
        public static Font Font => handwriting != null ? handwriting :
            handwriting = Resources.Load<Font>("SchoolStyle/PatrickHand-Regular");
    }
}

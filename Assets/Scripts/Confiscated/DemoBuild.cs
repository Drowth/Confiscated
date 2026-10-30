using UnityEngine;

namespace Confiscated
{
    /// <summary>
    /// The Steam demo is this same game built with the CONFISCATED_DEMO define (menu Confiscated/Build/Windows Demo Candidate).
    /// The run stops after <see cref="ItemCap"/> belongings on a wishlist card; Dark Mode, achievements and leaderboards stay in the full game.
    /// </summary>
    public static class DemoBuild
    {
#if CONFISCATED_DEMO
        public static bool On=>true;
#else
        public static bool On=>false;
#endif
        public const int ItemCap=2;
        /// <summary>The demo's own Steamworks app id. 0 until Valve assigns it, which skips the relaunch-through-Steam check.</summary>
        public const uint AppId=0;
        /// <summary>Opens the full game's page in the Steam client, where the player is already signed in to wishlist.</summary>
        public static void OpenStore()=>Application.OpenURL("steam://store/"+SteamLeaderboard.AppId);
    }
}

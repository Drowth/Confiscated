using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// A shortcut for playtesting the library maze by hand: enters Play, skips the opening, starts the run with the
    /// caretaker and Mr Reed paused (only the shadow is after you), puts the torch in your satchel and stands you
    /// outside the library's west door, facing in. Everything else runs as normal.
    /// </summary>
    [InitializeOnLoad]
    public static class LibraryPlaytest
    {
        const string Marker="Temp/library_playtest";
        static double at;static bool done;
        static LibraryPlaytest()
        {
            EditorApplication.playModeStateChanged+=s=>
            {
                if(s!=PlayModeStateChange.EnteredPlayMode||!File.Exists(Marker))return;
                File.Delete(Marker);at=EditorApplication.timeSinceStartup;done=false;EditorApplication.update+=Tick;
            };
        }
        [MenuItem("Confiscated/Library/Play Maze Test (start at the library)")]
        public static void Play()
        {
            if(EditorApplication.isPlaying){Debug.LogWarning("[Library] Stop Play first.");return;}
            File.WriteAllText(Marker,"armed");EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||done){EditorApplication.update-=Tick;return;}
            if(EditorApplication.timeSinceStartup-at<.8)return;
            done=true;EditorApplication.update-=Tick;
            var run=SchoolRunController.Instance;if(run==null){Debug.LogWarning("[Library] No school run in this scene.");return;}
            if(SchoolTitleMenu.IsActive){SchoolTitleMenu.Instance.StartGame();SchoolTitleMenu.Instance.SkipIntro();}
            ComicDialogue.Cancel();run.period.PrepareChaseRetry();run.PrepareChaseRetry();
            run.PauseStaff();run.caretaker.Freeze();
            var player=run.period.Player;var bag=player.GetComponent<PlayerInventory>();
            for(int slot=0;slot<8&&bag.Contains(InventoryContainer.Locker,InventoryItemKind.Torch);slot++)
                bag.Move(InventoryContainer.Locker,bag.Find(InventoryContainer.Locker,InventoryItemKind.Torch),InventoryContainer.Satchel,slot,out _);
            var shade=Object.FindFirstObjectByType<LibraryShadow>();
            if(shade!=null&&shade.exits.Length>0)
            {
                Vector3 outside=shade.exits[0].position,inside=shade.entrances[0].position;
                var body=player.GetComponent<CharacterController>();body.enabled=false;
                Vector3 face=inside-outside;face.y=0;player.transform.SetPositionAndRotation(outside,Quaternion.LookRotation(face));
                body.enabled=true;Physics.SyncTransforms();player.GetComponent<FirstPersonController>().ResetLook();
            }
            HudController.Instance?.SetStatus("Maze test: caretaker and Mr Reed paused. T for the torch. Find the handheld game on the returns desk.",7);
            Debug.Log("[Library] Maze playtest ready at the west door.");
        }
    }
}

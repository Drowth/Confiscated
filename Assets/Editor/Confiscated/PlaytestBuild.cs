using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Confiscated.EditorTools
{
    /// <summary>Windows playtest build of the enabled build scenes into ../Builds/Playtest_yyyy-MM-dd_HHmm, with a result file.</summary>
    public static class PlaytestBuild
    {
        public const string ResultFile="../Builds/last_playtest_build.txt";

        [MenuItem("Confiscated/Build/Windows Playtest Build")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before building.");
            var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
            if(scenes.Length==0)throw new InvalidOperationException("No scenes are enabled in Build Settings.");
            string folder=Path.GetFullPath("../Builds/Playtest_"+DateTime.Now.ToString("yyyy-MM-dd_HHmm"));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=scenes,locationPathName=Path.Combine(folder,"Confiscated.exe"),
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            var s=report.summary;
            File.WriteAllText(ResultFile,$"{s.result}\n{folder}\nscenes: {string.Join(", ",scenes)}\nsize: {s.totalSize/1048576f:F0} MB\ntime: {s.totalTime}\nerrors: {s.totalErrors} warnings: {s.totalWarnings}\n");
            Debug.Log("[PlaytestBuild] "+s.result+" -> "+folder);
        }
    }
}

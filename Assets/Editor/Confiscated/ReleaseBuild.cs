using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Confiscated.EditorTools
{
    /// <summary>The performance-test package generates these even for ordinary player builds.</summary>
    public sealed class ReleaseHousekeeping : IPreprocessBuildWithReport
    {
        public int callbackOrder=>int.MaxValue;
        public void OnPreprocessBuild(BuildReport report)
        {
            if((report.summary.options&BuildOptions.IncludeTestAssemblies)!=0)return;
            foreach(string name in new[]{"PerformanceTestRunInfo.json","PerformanceTestRunSettings.json"})
            {
                string path="Assets/Resources/"+name;
                if(File.Exists(path))AssetDatabase.DeleteAsset(path);
            }
        }
    }

    /// <summary>Portable store candidate. Reports and debug symbols stay outside the upload folder.</summary>
    public static class ReleaseBuild
    {
        public const string ResultFile="../Builds/last_release_build.txt";
        [MenuItem("Confiscated/Build/Windows Store Candidate")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before building.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save the edited scene before building.");
            var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
            var expected=new[]{"Assets/Scenes/StudioSplash.unity","Assets/Scenes/SchoolLayout.unity"};
            if(!scenes.SequenceEqual(expected))throw new InvalidOperationException("Store build must launch StudioSplash then SchoolLayout. Check enabled scenes.");
            string root=Path.GetFullPath("../Builds/Release_"+DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
            string player=Path.Combine(root,"Windows");
            Directory.CreateDirectory(player);
            Directory.CreateDirectory(Path.GetDirectoryName(ResultFile));
            File.WriteAllText(ResultFile,"Building\n"+root+"\n");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=scenes,locationPathName=Path.Combine(player,"Confiscated.exe"),
                target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.CompressWithLz4HC|BuildOptions.DetailedBuildReport});
            var s=report.summary;
            string result=$"{s.result}\n{root}\nUTC: {DateTime.UtcNow:O}\nUnity: {Application.unityVersion}\nVersion: {PlayerSettings.bundleVersion}\nScenes: {string.Join(", ",scenes)}\nCompression: LZ4HC (lossless)\nBuild data: {s.totalSize/1048576f:F1} MiB\nTime: {s.totalTime}\nErrors: {s.totalErrors}; warnings: {s.totalWarnings}\n";
            File.WriteAllText(ResultFile,result);
            File.WriteAllText(Path.Combine(root,"BuildReport.txt"),result);
            File.WriteAllLines(Path.Combine(root,"BuildWarnings.txt"),report.steps.SelectMany(step=>step.messages)
                .Where(m=>m.type==LogType.Warning||m.type==LogType.Error||m.type==LogType.Exception)
                .Select(m=>m.type+": "+m.content).Distinct());
            if(s.result!=BuildResult.Succeeded)throw new InvalidOperationException("Store build failed; see "+ResultFile);
            foreach(string dir in Directory.GetDirectories(player,"*DoNotShip*"))
                Directory.Move(dir,Path.Combine(root,Path.GetFileName(dir)));
            if(File.Exists(Path.Combine(player,"steam_appid.txt")))throw new InvalidOperationException("Do not ship steam_appid.txt in a store depot.");
            File.WriteAllText(Path.Combine(player,"README.txt"),
                "CONFISCATED!\r\nWindows 64-bit\r\n\r\nExtract the entire ZIP, then run Confiscated.exe. Keep the executable,\r\nConfiscated_Data and all accompanying files together.\r\n\r\nKeyboard and mouse. Controls are shown in game. Escape opens the pause\r\nmenu for settings and Quit. Twitch audience features are optional.\r\nAn internet connection and Steam are not required for local play.\r\n\r\nFeedback and updates: https://drowth.itch.io/confiscated\r\n");
            var assets=report.packedAssets.SelectMany(p=>p.contents).GroupBy(a=>a.sourceAssetPath)
                .Select(g=>new{path=g.Key,size=g.Sum(a=>(long)a.packedSize)}).OrderByDescending(a=>a.size);
            File.WriteAllLines(Path.Combine(root,"PackedAssets.tsv"),new[]{"Bytes\tSource asset"}.Concat(assets.Select(a=>a.size+"\t"+a.path)));
            long bytes=Directory.GetFiles(player,"*",SearchOption.AllDirectories).Sum(p=>new FileInfo(p).Length);
            File.AppendAllText(ResultFile,$"Upload folder: {player}\nInstalled bytes: {bytes}\n");
            Debug.Log("[ReleaseBuild] Ready for testing: "+player);
        }
    }
}

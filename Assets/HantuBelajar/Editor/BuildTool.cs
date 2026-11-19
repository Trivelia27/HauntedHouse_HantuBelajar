using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HantuBelajar.EditorTools
{
    /// <summary>
    /// Tools > Hantu Belajar > Build Windows: membangun Builds/Windows/HauntedHouse.exe.
    /// Batch: Unity.exe -batchmode -projectPath P -executeMethod HantuBelajar.EditorTools.BuildTool.BuildWindowsBatch -quit
    /// </summary>
    public static class BuildTool
    {
        public const string OutDir = "Builds/Windows";
        public const string ExeName = "HauntedHouse.exe";

        [MenuItem("Tools/Hantu Belajar/Build Windows (.exe)")]
        public static void BuildWindows() { Build(); }

        public static void BuildWindowsBatch()
        {
            bool ok = false;
            try { ok = Build(); }
            catch (System.Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Build()
        {
            ProjectBuilder.BuildAll();
            Directory.CreateDirectory(OutDir);
            BuildPlayerOptions o = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/HauntedHouse.unity" },
                locationPathName = Path.Combine(OutDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            BuildReport r = BuildPipeline.BuildPlayer(o);
            BuildSummary s = r.summary;
            string msg = "[BuildTool] hasil " + s.result + ", ukuran " + (s.totalSize / (1024 * 1024)) + " MB, waktu " + s.totalTime + ", error " + s.totalErrors + ", peringatan " + s.totalWarnings;
            Debug.Log(msg);
            Directory.CreateDirectory("TestOutput");
            File.WriteAllText("TestOutput/build_report.txt", msg + "\n");
            return s.result == BuildResult.Succeeded;
        }
    }
}

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JumpCraft.EditorTools
{
    /// <summary>
    /// WebGL settings and build commands for JumpCraftGame.
    /// Menu: JumpCraft > WebGL > ...
    /// Command line (Unity must be closed for this project):
    ///   Unity.exe -batchmode -quit -projectPath "path/to/JumpCraftGame"
    ///     -executeMethod JumpCraft.EditorTools.WebGLBuild.BuildFromCommandLine
    /// </summary>
    public static class WebGLBuild
    {
        // Relative to the Unity project folder; ignored by the repo's .gitignore (**/[Bb]uilds/).
        public const string OutputPath = "Builds/WebGL";

        [MenuItem("JumpCraft/WebGL/Apply Recommended Settings", priority = 0)]
        public static void ApplySettings()
        {
            // Gzip + decompression fallback runs on any static host (GitHub Pages, itch.io, a class
            // server) without needing the server to send Content-Encoding headers.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.runInBackground = true;

            AssetDatabase.SaveAssets();
            Debug.Log("[WebGLBuild] Applied WebGL player settings (Gzip, decompression fallback, data caching).");
        }

        [MenuItem("JumpCraft/WebGL/Switch Platform to WebGL", priority = 1)]
        public static void SwitchPlatform()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
            {
                Debug.Log("[WebGLBuild] Active build target is already WebGL.");
                return;
            }
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        }

        [MenuItem("JumpCraft/WebGL/Build", priority = 20)]
        public static void BuildMenu() => Build(BuildOptions.None);

        [MenuItem("JumpCraft/WebGL/Build and Run (local test)", priority = 21)]
        public static void BuildAndRunMenu() => Build(BuildOptions.AutoRunPlayer);

        [MenuItem("JumpCraft/WebGL/Development Build and Run", priority = 22)]
        public static void DevBuildAndRunMenu() => Build(BuildOptions.Development | BuildOptions.AutoRunPlayer);

        public static void BuildFromCommandLine()
        {
            var succeeded = Build(BuildOptions.None);
            EditorApplication.Exit(succeeded ? 0 : 1);
        }

        static bool Build(BuildOptions options)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[WebGLBuild] No enabled scenes in Build Profiles / Build Settings.");
                return false;
            }

            ApplySettings();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = options,
            });

            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[WebGLBuild] Build {summary.result} with {summary.totalErrors} error(s).");
                return false;
            }

            var fullPath = Path.GetFullPath(OutputPath);
            Debug.Log($"[WebGLBuild] Build succeeded: {fullPath} " +
                      $"({summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime:mm\\:ss}).");
            return true;
        }
    }
}

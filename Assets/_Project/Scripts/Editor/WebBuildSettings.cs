using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Musketeers.EditorTools
{
    // Applies the Web player settings from TECH_DESIGN §7 and builds to Builds/Web.
    // Kept as code so the settings are reproducible instead of hand-clicked.
    public static class WebBuildSettings
    {
        public const string OutputPath = "Builds/Web";

        [MenuItem("Tools/Musketeers/Apply Web Player Settings")]
        public static void Apply()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            PlayerSettings.companyName = "Los Cuatro Mosqueteros";
            PlayerSettings.productName = "The Last Musketeer Standing";

            // Gzip + JS decompression fallback: no server Content-Encoding headers needed.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.runInBackground = true;
            // NGO and Services use reflection; heavier stripping breaks them (use link.xml instead).
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);
            PlayerSettings.defaultWebScreenWidth = 1600;
            PlayerSettings.defaultWebScreenHeight = 900;
            // Custom template: full-window canvas, dark background, loading bar, Fullscreen button.
            PlayerSettings.WebGL.template = "PROJECT:Musketeers";

            AssetDatabase.SaveAssets();
            Debug.Log("[WebBuildSettings] Applied TECH_DESIGN §7 Web player settings. Version " + PlayerSettings.bundleVersion);
        }

        [MenuItem("Tools/Musketeers/Build Web")]
        public static void Build()
        {
            Apply();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[WebBuildSettings] No enabled scenes in Build Settings.");
                return;
            }

            if (Directory.Exists(OutputPath))
                Directory.Delete(OutputPath, true);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"[WebBuildSettings] Web build succeeded: {OutputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0}s)");
            else
                Debug.LogError($"[WebBuildSettings] Web build {summary.result} with {summary.totalErrors} errors.");
        }
    }
}

using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RajaSawit.EditorTools
{
    /// <summary>
    /// Entry point for the GitHub Actions build (game-ci/unity-builder, buildMethod).
    /// Runs the same steps as the "Raja Sawit" menu, then builds an Android APK.
    /// </summary>
    public static class CiBuild
    {
        public static void BuildAndroid()
        {
            int exitCode = 1;
            try
            {
                RajaSawitSetup.SetupProject();
                RajaSawitSetup.CreateHomeScene();
                AssetDatabase.SaveAssets();

                string path = Arg("-customBuildPath") ?? "build/Android/RajaSawit.apk";
                if (!path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) path += ".apk";

                string versionCode = Arg("-androidVersionCode");
                if (int.TryParse(versionCode, out int code) && code > 0) PlayerSettings.Android.bundleVersionCode = code;
                string version = Arg("-buildVersion");
                if (!string.IsNullOrEmpty(version) && version != "none") PlayerSettings.bundleVersion = version;

                EditorUserBuildSettings.buildAppBundle = false;
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Project/Scenes/Home.unity" },
                    locationPathName = path,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.Development,
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                Debug.Log($"[RajaSawit] Build {summary.result}: {summary.outputPath}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} error, {summary.totalTime}");
                if (summary.result == BuildResult.Succeeded) exitCode = 0;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            EditorApplication.Exit(exitCode);
        }

        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name && !args[i + 1].StartsWith("-")) return args[i + 1];
            return null;
        }
    }
}

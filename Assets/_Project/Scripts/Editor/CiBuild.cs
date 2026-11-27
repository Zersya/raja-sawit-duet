using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace RajaSawit.EditorTools
{
    /// <summary>
    /// Entry point for the GitHub Actions build (game-ci/unity-builder, buildMethod).
    /// Runs the EditMode tests, then the same steps as the "Raja Sawit" menu, then builds an Android APK.
    /// GameCI only counts a build as passed when the log contains "Build succeeded!".
    /// </summary>
    public static class CiBuild
    {
        public static void BuildAndroid()
        {
            int exitCode = 1;
            try
            {
                if (!RunEditModeTests())
                {
                    EditorApplication.Exit(2);
                    return;
                }

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
                if (summary.result == BuildResult.Succeeded)
                {
                    exitCode = 0;
                    Debug.Log("Build succeeded!");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>Runs every EditMode test synchronously and logs each failure.</summary>
        public static bool RunEditModeTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callbacks = new TestCallbacks();
            api.RegisterCallbacks(callbacks);
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }) { runSynchronously = true });
            api.UnregisterCallbacks(callbacks);

            Debug.Log($"[RajaSawit] Tes EditMode: {callbacks.Passed} lulus, {callbacks.Failed} gagal, {callbacks.Skipped} dilewati.");
            if (!callbacks.Finished) Debug.LogError("[RajaSawit] Tes EditMode tidak selesai.");
            if (callbacks.Passed == 0) Debug.LogError("[RajaSawit] Tidak ada tes EditMode yang jalan.");
            return callbacks.Finished && callbacks.Failed == 0 && callbacks.Passed > 0;
        }

        private class TestCallbacks : ICallbacks
        {
            public int Passed, Failed, Skipped;
            public bool Finished;

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result) => Finished = true;

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren) return;
                switch (result.TestStatus)
                {
                    case TestStatus.Passed:
                        Passed++;
                        Debug.Log($"[RajaSawit] LULUS {result.FullName}");
                        break;
                    case TestStatus.Failed:
                        Failed++;
                        Debug.LogError($"[RajaSawit] GAGAL {result.FullName}: {result.Message}\n{result.StackTrace}");
                        break;
                    default:
                        Skipped++;
                        break;
                }
                if (!string.IsNullOrEmpty(result.Output)) Debug.Log(result.Output);
            }
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

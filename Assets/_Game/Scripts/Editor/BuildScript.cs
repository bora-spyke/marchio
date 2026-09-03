using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Marchio.Editor
{
    public static class BuildScript
    {
        [MenuItem("Marchio/Build Android APK")]
        public static void BuildAndroid()
        {
            var args = Environment.GetCommandLineArgs();
            int idx = Array.IndexOf(args, "-buildOutput");
            string output = idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : "Builds/neonloop.apk";
            EditorUserBuildSettings.buildAppBundle = false;
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Marchio] Android build {report.summary.result}: {report.summary.totalSize} bytes, {report.summary.totalErrors} errors -> {output}");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}

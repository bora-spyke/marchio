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
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTarget.Android, BuildTargetGroup.Android, "Builds/neonloop.apk");
        }

        [MenuItem("Marchio/Build Web")]
        public static void BuildWeb()
        {
            Build(BuildTarget.WebGL, BuildTargetGroup.WebGL, "Builds/web");
        }

        static void Build(BuildTarget target, BuildTargetGroup group, string defaultOutput)
        {
            var args = Environment.GetCommandLineArgs();
            int idx = Array.IndexOf(args, "-buildOutput");
            string output = idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : defaultOutput;
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = target,
                targetGroup = group,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Marchio] {target} build {report.summary.result}: {report.summary.totalSize} bytes, {report.summary.totalErrors} errors -> {output}");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}

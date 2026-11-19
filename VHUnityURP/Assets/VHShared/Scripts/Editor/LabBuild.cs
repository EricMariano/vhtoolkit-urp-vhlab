using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VHLab.Editor
{
    public static class LabBuild
    {
        [MenuItem("VHLab/Build Windows x64")]
        public static void BuildWindows()
        {
            const BuildTarget target = BuildTarget.StandaloneWindows64;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new BuildFailedException("Install Windows Build Support for this Unity Editor.");

            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).ToArray();
            if (scenes.Length == 0)
                throw new BuildFailedException("No enabled scenes in EditorBuildSettings.");

            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, "-labBuildOutput");
            if (index >= 0 && (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-")))
                throw new BuildFailedException("Missing value for -labBuildOutput.");

            var output = index >= 0 ? arguments[index + 1] : "../artifacts/windows/VHLab.exe";
            output = Path.GetFullPath(output);
            if (!string.Equals(Path.GetExtension(output), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Windows build output must end in .exe.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = arguments.Contains("-labDevelopment") ? BuildOptions.Development : BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Windows build failed: {report.summary.result}.");

            Debug.Log($"VHLab Windows build completed: {output}");
        }
    }
}

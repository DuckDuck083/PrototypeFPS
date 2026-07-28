using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildPrototypeFPS
{
    private const string OutputDirectory = "FPS game build 0.5v";
    private const string ExecutablePath = OutputDirectory + "/PrototypeFPS.exe";

    [MenuItem("Build/Build Clean Windows Player")]
    public static void BuildCleanWindowsPlayer()
    {
        Directory.CreateDirectory(OutputDirectory);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/MainScene.unity" },
            locationPathName = ExecutablePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.CleanBuildCache
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"Windows build failed: {report.summary.result}");

        Debug.Log($"Verified Windows build created at {Path.GetFullPath(ExecutablePath)}");
    }
}

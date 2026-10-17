using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Milestro.Tests
{
    public static class RenderAtlasBenchmarkBuilder
    {
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Atlas Benchmark", typeof(RenderAtlasBenchmarkRunner));
            const string scenePath = "Assets/MilestroAtlasBenchmark.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            var previousBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            var previousTiming = PlayerSettings.enableFrameTimingStats;
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.enableFrameTimingStats = true;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scenePath },
                    locationPathName = "Build/AtlasBenchmark/AtlasBenchmark.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException("Atlas benchmark player build failed: " + report.summary.result);
                }
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previousBackend);
                PlayerSettings.enableFrameTimingStats = previousTiming;
            }
        }

        public static void BuildWorldText()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("World Text Benchmark", typeof(WorldTextBenchmarkRunner));
            const string scenePath = "Assets/MilestroWorldTextBenchmark.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            var previousBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            var previousTiming = PlayerSettings.enableFrameTimingStats;
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.enableFrameTimingStats = true;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scenePath },
                    locationPathName = "Build/WorldTextBenchmark/WorldTextBenchmark.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException("World text benchmark player build failed: " + report.summary.result);
                }
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previousBackend);
                PlayerSettings.enableFrameTimingStats = previousTiming;
            }
        }
    }
}

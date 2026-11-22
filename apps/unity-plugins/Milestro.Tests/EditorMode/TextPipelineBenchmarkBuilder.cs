using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Milestro.Tests
{
    public static class TextPipelineBenchmarkBuilder
    {
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Text Pipeline Benchmark", typeof(TextPipelineBenchmarkRunner));
            const string scenePath = "Assets/MilestroTextPipelineBenchmark.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            var previousBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            var previousTiming = PlayerSettings.enableFrameTimingStats;
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.enableFrameTimingStats = true;
                var output = Environment.GetEnvironmentVariable("MILESTRO_TEXT_BENCHMARK_PLAYER");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scenePath },
                    locationPathName = string.IsNullOrEmpty(output)
                        ? "Build/TextPipelineBenchmark/TextPipelineBenchmark.exe" : output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException("Text pipeline benchmark build failed: " + report.summary.result);
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

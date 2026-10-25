using System;
using System.IO;
using Milestro.Tests.TextBoxVertical;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Milestro.Tests.Editor
{
    public static class TextBoxVerticalDegradePlayerBuilder
    {
        private const string OutputArgument = "-milestroBuildOutput";
        private const string GeneratedRoot = "Assets/Milestro.Tests.Generated";
        private const string ScenePath = GeneratedRoot + "/VerticalDegradeProbe.unity";

        public static void BuildPlayer()
        {
            Build(development: false);
        }

        public static void BuildDevelopmentPlayer()
        {
            Build(development: true);
        }

        private static void Build(bool development)
        {
            var output = ArgumentValue(OutputArgument);
            if (string.IsNullOrWhiteSpace(output))
            {
                output = Path.Combine("Build", "TextBoxVerticalDegrade",
                    development ? "development" : "player", "MilestroVerticalDegrade.exe");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
            Directory.CreateDirectory(GeneratedRoot);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            try
            {
                var options = development ? BuildOptions.Development : BuildOptions.None;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = options,
                });
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "TextBox vertical degrade player build failed: " +
                        report.summary.result + ".");
                }

                Debug.Log("TEXTBOX_VERTICAL_PLAYER_BUILD output=" + output +
                          " development=" + development + " result=" +
                          report.summary.result + ".");
            }
            finally
            {
                AssetDatabase.DeleteAsset(GeneratedRoot);
                AssetDatabase.Refresh();
            }
        }

        private static string ArgumentValue(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; ++index)
            {
                if (string.Equals(arguments[index], name, StringComparison.Ordinal))
                {
                    return arguments[index + 1];
                }
            }

            return "";
        }
    }
}

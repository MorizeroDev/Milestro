using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

namespace Milestro.Tests.TextBoxHyperlinkIntegration.Editor
{
    public static class TextBoxHyperlinkIntegrationPlayerBuilder
    {
        public const string OutputPath = "Build/Task201Hyperlink/TextBoxHyperlinkIntegration.app";

        [MenuItem("Milestro/Task 201/Build macOS IL2CPP Medium Hyperlink Player")]
        public static void BuildMacOsIl2Cpp()
        {
            TextBoxHyperlinkIntegrationFixtureBuilder.GenerateFromEnvironment();
            ValidatePlayerAssemblyBoundary();

            var namedTarget = NamedBuildTarget.Standalone;
            var previousBackend = PlayerSettings.GetScriptingBackend(namedTarget);
            var previousStripping = PlayerSettings.GetManagedStrippingLevel(namedTarget);
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
            BuildReport report;
            try
            {
                PlayerSettings.SetScriptingBackend(namedTarget, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetManagedStrippingLevel(namedTarget, ManagedStrippingLevel.Medium);
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { TextBoxHyperlinkIntegrationFixtureBuilder.ScenePath },
                    locationPathName = OutputPath,
                    target = BuildTarget.StandaloneOSX,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.Development
                });
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(namedTarget, previousBackend);
                PlayerSettings.SetManagedStrippingLevel(namedTarget, previousStripping);
            }

            Debug.Log($"Task 201 hyperlink player build result: {report.summary.result}");
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Task 201 hyperlink player build failed: {report.summary.result}.");
            }
        }

        private static void ValidatePlayerAssemblyBoundary()
        {
            var runtimeFound = false;
            foreach (var assembly in CompilationPipeline.GetAssemblies(
                         AssembliesType.PlayerWithoutTestAssemblies))
            {
                if (assembly.name == "Milestro.Tests.Support")
                {
                    runtimeFound = true;
                }
                if (assembly.name == "Milestro.Tests.EditorMode" ||
                    assembly.name == "Milestro.Tests.PlayMode")
                {
                    throw new InvalidOperationException(
                        $"Milestro test assembly leaked into the player: {assembly.name}");
                }
            }

            if (!runtimeFound ||
                typeof(TextBoxHyperlinkIntegrationReceiver).Assembly.GetName().Name !=
                "Milestro.Tests.Support" ||
                typeof(TextBoxHyperlinkIntegrationScenarioRunner).Assembly.GetName().Name !=
                "Milestro.Tests.Support")
            {
                throw new InvalidOperationException("Task 201 runtime assembly boundary is invalid.");
            }
        }
    }
}

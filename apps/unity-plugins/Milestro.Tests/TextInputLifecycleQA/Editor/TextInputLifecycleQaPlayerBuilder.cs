using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

namespace Milestro.TextInputLifecycleQA.Editor
{
    public static class TextInputLifecycleQaPlayerBuilder
    {
        public const string OutputPath = "Build/Task159QA/TextInputLifecycleQA.app";

        private static readonly Type[] RequiredRuntimeTypes =
        {
            typeof(TextInputLifecycleQaBootstrap),
            typeof(TextInputLifecycleQaReceiver),
            typeof(TextInputLifecycleQaRuntimeListener),
            typeof(TextInputLifecycleQaScenarioRunner),
            typeof(TextInputLifecycleQaStrictProvider),
            typeof(TextInputLifecycleQaInputModule)
        };

        [MenuItem("Milestro/Task 159/Build macOS IL2CPP Lifecycle QA Player")]
        public static void BuildMacOsIl2Cpp()
        {
            TextInputLifecycleQaFixtureBuilder.GenerateFromEnvironment();

            var namedTarget = NamedBuildTarget.Standalone;
            var previousBackend = PlayerSettings.GetScriptingBackend(namedTarget);
            var previousStripping = PlayerSettings.GetManagedStrippingLevel(namedTarget);
            var buildOptions = BuildOptions.Development;
            if ((buildOptions & BuildOptions.IncludeTestAssemblies) != 0)
            {
                throw new InvalidOperationException("Task 159 QA player must exclude test assemblies.");
            }
            ValidatePlayerAssemblyBoundary();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
            BuildReport report;
            try
            {
                PlayerSettings.SetScriptingBackend(namedTarget, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetManagedStrippingLevel(namedTarget, ManagedStrippingLevel.Medium);
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[]
                    {
                        TextInputLifecycleQaFixtureBuilder.BootstrapScenePath,
                        TextInputLifecycleQaFixtureBuilder.ScenePath
                    },
                    locationPathName = OutputPath,
                    target = BuildTarget.StandaloneOSX,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = buildOptions
                });
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(namedTarget, previousBackend);
                PlayerSettings.SetManagedStrippingLevel(namedTarget, previousStripping);
            }

            Debug.Log($"Task 159 QA player build result: {report.summary.result}");
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Task 159 QA player build failed: {report.summary.result}.");
            }
        }

        private static void ValidatePlayerAssemblyBoundary()
        {
            var runtimeFound = false;
            foreach (var assembly in CompilationPipeline.GetAssemblies(
                         AssembliesType.PlayerWithoutTestAssemblies))
            {
                if (assembly.name == "Milestro.TextInputLifecycleQA.Runtime")
                {
                    runtimeFound = true;
                }
                if (assembly.name == "Milestro.TextInputLifecycleQA.Editor" ||
                    assembly.name == "Milestro.TextInputLifecycleQA.EditModeTests" ||
                    assembly.name == "Milestro.TextInputLifecycleQA.PlayModeTests")
                {
                    throw new InvalidOperationException(
                        $"Test assembly leaked into PlayerWithoutTestAssemblies: {assembly.name}");
                }
            }
            if (!runtimeFound)
            {
                throw new InvalidOperationException(
                    "Task 159 QA runtime assembly is missing from PlayerWithoutTestAssemblies.");
            }
            foreach (var runtimeType in RequiredRuntimeTypes)
            {
                if (runtimeType.Assembly.GetName().Name != "Milestro.TextInputLifecycleQA.Runtime")
                {
                    throw new InvalidOperationException(
                        $"Task 159 QA runtime type has the wrong assembly: {runtimeType.FullName}");
                }
            }
        }
    }
}

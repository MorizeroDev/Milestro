using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Milestro.Tests.ScreenSpaceHiDpiIntegration.Editor
{
    public static class ScreenSpaceHiDpiPlayerBuilder
    {
        [MenuItem("Milestro/Task 214/Build And Run macOS Metal HiDPI Gate")]
        public static void BuildAndRunMetal()
        {
            if (Application.platform != RuntimePlatform.OSXEditor)
            {
                throw new InvalidOperationException("The Metal gate must be built and run by a macOS Unity Editor.");
            }
            BuildAndRun(BuildTarget.StandaloneOSX, GraphicsDeviceType.Metal, "metal", "ScreenSpaceHiDpi.app");
        }

        [MenuItem("Milestro/Task 214/Build And Run Windows D3D12 HiDPI Gate")]
        public static void BuildAndRunD3D12()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                throw new InvalidOperationException("The D3D12 gate must be built and run by a Windows Unity Editor.");
            }
            BuildAndRun(BuildTarget.StandaloneWindows64,
                GraphicsDeviceType.Direct3D12,
                "d3d12",
                "ScreenSpaceHiDpi.exe");
        }

        private static void BuildAndRun(BuildTarget target,
            GraphicsDeviceType graphicsApi,
            string backend,
            string executableName)
        {
            ValidatePlayerAssemblyBoundary();
            using var fixture = ScreenSpaceHiDpiFixtureBuilder.Generate(backend);
            var useDefaultGraphicsApis = PlayerSettings.GetUseDefaultGraphicsAPIs(target);
            var graphicsApis = PlayerSettings.GetGraphicsAPIs(target);
            var defaultWidth = PlayerSettings.defaultScreenWidth;
            var defaultHeight = PlayerSettings.defaultScreenHeight;
            var fullscreenMode = PlayerSettings.fullScreenMode;
            var resizableWindow = PlayerSettings.resizableWindow;
            try
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
                PlayerSettings.SetGraphicsAPIs(target, new[] { graphicsApi });
                PlayerSettings.defaultScreenWidth = 1920;
                PlayerSettings.defaultScreenHeight = 1080;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.resizableWindow = true;

                var output = Path.Combine("Build",
                    "ScreenSpaceHiDpi",
                    fixture.ExactHead,
                    backend,
                    DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff"),
                    executableName);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScreenSpaceHiDpiFixtureBuilder.ScenePath },
                    locationPathName = output,
                    target = target,
                    targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                    options = BuildOptions.Development | BuildOptions.AutoRunPlayer
                });
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException("Screen Space HiDPI player build failed: " +
                                                        report.summary.result + ".");
                }
                Debug.Log("Screen Space HiDPI player built and launched: backend=" + backend +
                          " head=" + fixture.ExactHead + " tree=" + fixture.ExactTree +
                          " manifest=" + fixture.PackageManifestSha256 +
                          " files=" + fixture.PackageFileCount + " GUIDs=" + fixture.PackageGuidCount +
                          " output=" + output + ".");
            }
            finally
            {
                try
                {
                    PlayerSettings.SetUseDefaultGraphicsAPIs(target, useDefaultGraphicsApis);
                }
                finally
                {
                    try
                    {
                        PlayerSettings.SetGraphicsAPIs(target, graphicsApis);
                    }
                    finally
                    {
                        PlayerSettings.defaultScreenWidth = defaultWidth;
                        PlayerSettings.defaultScreenHeight = defaultHeight;
                        PlayerSettings.fullScreenMode = fullscreenMode;
                        PlayerSettings.resizableWindow = resizableWindow;
                    }
                }
            }
        }

        private static void ValidatePlayerAssemblyBoundary()
        {
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            if (!assemblies.Any(assembly =>
                    assembly.name == "Milestro.Tests.Support"))
            {
                throw new InvalidOperationException("Milestro test support is absent from player assemblies.");
            }
            if (assemblies.Any(assembly =>
                    assembly.name == "Milestro.Tests.EditorMode" ||
                    assembly.name == "Milestro.Tests.PlayMode"))
            {
                throw new InvalidOperationException("Milestro test assembly leaked into the player.");
            }
            if (typeof(ScreenSpaceHiDpiScenarioRunner).Assembly.GetName().Name !=
                "Milestro.Tests.Support")
            {
                throw new InvalidOperationException("Screen Space HiDPI runner has the wrong assembly identity.");
            }
        }
    }
}

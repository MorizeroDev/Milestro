using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Milestro.AndroidValidation.Editor
{
    // Copy this fixture into an isolated Demo's Assets/Editor, not the shipped package.
    public static class BuildAndroidValidation
    {
        private const string ModeAsset = "Assets/Resources/MilestroAndroidValidationMode.txt";

        public static void PerformBuild()
        {
            string mode = Argument("-milestroBackend");
            if (mode != "VulkanDirect" && mode != "VulkanStagingCopy" && mode != "GLES3")
                throw new ArgumentException("-milestroBackend must be VulkanDirect, VulkanStagingCopy or GLES3.");
            if (File.Exists(ModeAsset) || Directory.Exists(ModeAsset) || File.Exists(ModeAsset + ".meta"))
                throw new InvalidOperationException("Refusing to overwrite existing validation mode asset.");

            // Use this editor installation, not another SDK/NDK selected in global preferences.
            string android = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer");
            string sdk = AndroidExternalToolsSettings.sdkRootPath;
            string ndk = AndroidExternalToolsSettings.ndkRootPath;
            string jdk = AndroidExternalToolsSettings.jdkRootPath;
            try
            {
                AndroidExternalToolsSettings.sdkRootPath = Path.Combine(android, "SDK");
                AndroidExternalToolsSettings.ndkRootPath = Path.Combine(android, "NDK");
                AndroidExternalToolsSettings.jdkRootPath = Path.Combine(android, "OpenJDK");
                Debug.Log($"[AndroidValidation] SDK={AndroidExternalToolsSettings.sdkRootPath} " +
                          $"NDK={AndroidExternalToolsSettings.ndkRootPath} JDK={AndroidExternalToolsSettings.jdkRootPath}");
                Build(mode);
            }
            finally
            {
                AndroidExternalToolsSettings.sdkRootPath = sdk;
                AndroidExternalToolsSettings.ndkRootPath = ndk;
                AndroidExternalToolsSettings.jdkRootPath = jdk;
                AssetDatabase.DeleteAsset(ModeAsset);
            }
        }

        private static void Build(string mode)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Launch Unity with -buildTarget Android before running this fixture.");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] {
                mode == "GLES3" ? GraphicsDeviceType.OpenGLES3 : GraphicsDeviceType.Vulkan
            });
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            Directory.CreateDirectory("Assets/Resources");
            File.WriteAllText(ModeAsset, mode);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            const string plugin = "Assets/Milestro/Plugins/Android/arm64-v8a/libMilestro.so";
            if (!(AssetImporter.GetAtPath(plugin) is PluginImporter importer))
                throw new FileNotFoundException("Matching native library must be installed before validation.", plugin);
            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(false);
            importer.SetCompatibleWithPlatform(BuildTarget.Android, true);
            importer.SetPlatformData(BuildTarget.Android, "CPU", "ARM64");
            importer.isPreloaded = true;
            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory("Builds");
            string output = $"Builds/MilestroDemo-{mode}.apk";
            Debug.Log($"[AndroidValidation] Building {mode}: {output}");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"{mode}: {report.summary.result}, errors={report.summary.totalErrors}");
            Debug.Log($"[AndroidValidation] SUCCESS {mode}: {Path.GetFullPath(output)} ({new FileInfo(output).Length} APK bytes)");
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException($"Missing {name}");
            return args[index + 1];
        }
    }
}

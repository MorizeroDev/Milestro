using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Milestro.Components;
using Milestro.Components.Internal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace Milestro.Tests.ScreenSpaceHiDpiIntegration.Editor
{
    public static class ScreenSpaceHiDpiFixtureBuilder
    {
        public const string RootPath = "Assets/__MilestroScreenSpaceHiDpiIntegration";
        public const string ScenePath = RootPath + "/ScreenSpaceHiDpi.unity";
        public const string ManifestPath = RootPath + "/ScreenSpaceHiDpiPackageManifest.json";
        public const int ExpectedPackageFileCount = 424;
        public const int ExpectedPackageGuidCount = 232;
        public const string ExpectedPackageContentSha256 =
            "3baf38f785f03d8377853d0deeeef92bce71883c07ff0f09f37a65497557985d";

        internal const string IcuPayloadPath = "Resources/Milestro/icudtl.dat.bytes";

        private static readonly string[] PackageRoots =
        {
            "Milestro",
            "Milestro.Editor",
            "Milestro.Experimental",
            "Milestro.InputSystem",
            "Resources"
        };

        public static GeneratedFixture Generate(string backend)
        {
            ValidateSceneSetup();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var generated = false;
            try
            {
                var exactHead = RequiredObjectId("MILESTRO_SCREEN_SPACE_HIDPI_HEAD");
                var exactTree = RequiredObjectId("MILESTRO_SCREEN_SPACE_HIDPI_TREE");
                var expectedManifestSha256 = RequiredSha256("MILESTRO_SCREEN_SPACE_HIDPI_PACKAGE_MANIFEST_SHA256");
                var sourceRepository = RequiredDirectory("MILESTRO_SCREEN_SPACE_HIDPI_SOURCE_REPO");
                var packageRoot = RequiredDirectory("MILESTRO_SCREEN_SPACE_HIDPI_PACKAGE_ROOT");
                ValidateCleanGitSource(sourceRepository, exactHead, exactTree);
                ValidateLoadedPackageRoot(packageRoot);
                if (!string.Equals(expectedManifestSha256,
                        ExpectedPackageContentSha256,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "MILESTRO_SCREEN_SPACE_HIDPI_PACKAGE_MANIFEST_SHA256 is not the reviewed Screen Space HiDPI package digest.");
                }
                var manifest = BuildPackageManifest(packageRoot,
                    sourceRepository,
                    exactHead,
                    exactTree);
                if (!string.Equals(manifest.contentSha256, expectedManifestSha256,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Loaded package manifest does not match MILESTRO_SCREEN_SPACE_HIDPI_PACKAGE_MANIFEST_SHA256: " +
                        manifest.contentSha256 + ".");
                }

                if (AssetDatabase.IsValidFolder(RootPath))
                {
                    throw new InvalidOperationException(
                        "Screen Space HiDPI fixture root already exists; refusing to delete or mix pre-existing assets: " +
                        RootPath + ".");
                }
                AssetDatabase.CreateFolder("Assets", "__MilestroScreenSpaceHiDpiIntegration");
                generated = true;
                File.WriteAllText(Path.GetFullPath(ManifestPath),
                    JsonUtility.ToJson(manifest, prettyPrint: true),
                    new UTF8Encoding(false));
                AssetDatabase.ImportAsset(ManifestPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var manifestAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath) ??
                                    throw new InvalidOperationException("Generated package manifest was not imported.");
                CreateScene(backend,
                    exactHead,
                    exactTree,
                    expectedManifestSha256,
                    manifestAsset);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(ScenePath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                return new GeneratedFixture(setup,
                    exactHead,
                    exactTree,
                    expectedManifestSha256,
                    manifest.fileCount,
                    manifest.guidCount);
            }
            catch
            {
                if (generated)
                {
                    try
                    {
                        RestoreSceneSetup(setup);
                    }
                    finally
                    {
                        DeleteGeneratedAssets();
                    }
                }
                throw;
            }
        }

        private static ScreenSpaceHiDpiPackageManifest BuildPackageManifest(string packageRoot,
            string sourceRepository,
            string exactHead,
            string exactTree)
        {
            var sourcePackageRoot = Path.Combine(sourceRepository, "apps", "unity-plugins");
            var sourceIcuPayload = Path.Combine(sourceRepository,
                "ext",
                "icu-cmake",
                "common",
                "icudtl.dat");
            if (!Directory.Exists(sourcePackageRoot) || !File.Exists(sourceIcuPayload))
            {
                throw new InvalidOperationException(
                    "Screen Space HiDPI clean source does not contain the canonical Unity package and ICU payload.");
            }

            var sourceMissingFolderMetas = FindMissingFolderMetas(sourcePackageRoot);
            ScreenSpaceHiDpiPackageIdentityPolicy.RequireEquivalentDirectoryLayout(
                FindDirectoryPaths(sourcePackageRoot),
                FindDirectoryPaths(packageRoot));
            var loadedMetaPaths = FindMetaPaths(packageRoot);
            ScreenSpaceHiDpiPackageIdentityPolicy.RequireExactLoadedMetaClosure(
                FindMetaPaths(sourcePackageRoot),
                loadedMetaPaths);
            ScreenSpaceHiDpiPackageIdentityPolicy.RequireUniqueMetaGuids(loadedMetaPaths
                .Select(relativePath => ReadGuid(Path.Combine(packageRoot,
                    relativePath.Replace('/', Path.DirectorySeparatorChar))))
                .ToArray());
            var loadedContainerMetadata = ScreenSpaceHiDpiPackageManifest.HostContainerMetaPaths
                .Select(relativePath => ReadHostContainerMetadata(packageRoot, relativePath))
                .ToArray();
            var hostContainerMetadata = ScreenSpaceHiDpiPackageIdentityPolicy.RequireExactHostContainerMetadata(
                sourceMissingFolderMetas,
                loadedContainerMetadata);
            ValidateUniquePhysicalIcuResource(packageRoot);

            var sourceEntries = BuildPayloadEntries(sourcePackageRoot, sourceIcuPayload);
            var loadedEntries = BuildPayloadEntries(packageRoot, null);
            ScreenSpaceHiDpiPackageIdentityPolicy.RequireEquivalentPayload(sourceEntries, loadedEntries);
            ValidatePayloadShape(sourceEntries);

            var manifest = new ScreenSpaceHiDpiPackageManifest
            {
                exactHead = exactHead,
                exactTree = exactTree,
                fileCount = loadedEntries.Length,
                guidCount = CountUniqueGuids(loadedEntries),
                entries = loadedEntries,
                hostContainerMetadata = hostContainerMetadata
            };
            manifest.contentSha256 = manifest.ComputeContentSha256();
            if (!string.Equals(manifest.contentSha256,
                    ExpectedPackageContentSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Screen Space HiDPI source payload digest is not the reviewed 424-file package: " +
                    manifest.contentSha256 + ".");
            }
            manifest.Validate(exactHead, exactTree, ExpectedPackageContentSha256);
            return manifest;
        }

        private static ScreenSpaceHiDpiPackageManifestEntry[] BuildPayloadEntries(string packageRoot,
            string? injectedIcuPayload)
        {
            var entries = new List<ScreenSpaceHiDpiPackageManifestEntry>();
            var guids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rootName in PackageRoots)
            {
                var root = Path.Combine(packageRoot, rootName);
                if (!Directory.Exists(root))
                {
                    throw new InvalidOperationException("Required package root is missing: " + root + ".");
                }
                var paths = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToList();
                var rootMeta = root + ".meta";
                if (File.Exists(rootMeta))
                {
                    paths.Add(rootMeta);
                }
                foreach (var path in paths
                             .OrderBy(value => value, StringComparer.Ordinal))
                {
                    if (string.Equals(Path.GetFileName(path), ".DS_Store", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    var relative = RelativePath(packageRoot, path);
                    if (ScreenSpaceHiDpiPackageManifest.IsHostContainerMetaPath(relative))
                    {
                        continue;
                    }
                    var guid = path.EndsWith(".meta", StringComparison.Ordinal)
                        ? ReadGuid(path)
                        : string.Empty;
                    if (!string.IsNullOrEmpty(guid) && !guids.Add(guid))
                    {
                        throw new InvalidOperationException("Duplicate package GUID: " + guid + ".");
                    }
                    entries.Add(new ScreenSpaceHiDpiPackageManifestEntry
                    {
                        path = relative,
                        sha256 = Sha256File(path),
                        guid = guid
                    });
                }
            }

            if (!string.IsNullOrEmpty(injectedIcuPayload))
            {
                var existingIcu = entries.FirstOrDefault(entry =>
                    string.Equals(entry.path, IcuPayloadPath, StringComparison.Ordinal));
                if (existingIcu != null)
                {
                    throw new InvalidOperationException(
                        "Screen Space HiDPI source unexpectedly contains the generated ICU payload: " +
                        IcuPayloadPath + ".");
                }
                entries.Add(new ScreenSpaceHiDpiPackageManifestEntry
                {
                    path = IcuPayloadPath,
                    sha256 = Sha256File(injectedIcuPayload),
                    guid = string.Empty
                });
            }

            return entries.OrderBy(entry => entry.path, StringComparer.Ordinal).ToArray();
        }

        private static void ValidatePayloadShape(ScreenSpaceHiDpiPackageManifestEntry[] entries)
        {
            var guidCount = CountUniqueGuids(entries);
            if (entries.Length != ExpectedPackageFileCount || guidCount != ExpectedPackageGuidCount)
            {
                throw new InvalidOperationException(
                    "Screen Space HiDPI exact package shape mismatch: files=" + entries.Length +
                    " GUIDs=" + guidCount + ".");
            }
        }

        private static int CountUniqueGuids(IEnumerable<ScreenSpaceHiDpiPackageManifestEntry> entries)
        {
            return entries.Where(entry => !string.IsNullOrEmpty(entry.guid))
                .Select(entry => entry.guid)
                .Distinct(StringComparer.Ordinal)
                .Count();
        }

        private static string[] FindMissingFolderMetas(string packageRoot)
        {
            return Directory.GetDirectories(packageRoot, "*", SearchOption.AllDirectories)
                .Where(directory => !File.Exists(directory + ".meta"))
                .Select(directory => RelativePath(packageRoot, directory) + ".meta")
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] FindDirectoryPaths(string packageRoot)
        {
            return Directory.GetDirectories(packageRoot, "*", SearchOption.AllDirectories)
                .Select(directory => RelativePath(packageRoot, directory))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] FindMetaPaths(string packageRoot)
        {
            return Directory.GetFiles(packageRoot, "*.meta", SearchOption.AllDirectories)
                .Select(path => RelativePath(packageRoot, path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static ScreenSpaceHiDpiHostContainerMetadata ReadHostContainerMetadata(string packageRoot,
            string relativePath)
        {
            var fullPath = Path.Combine(packageRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                return new ScreenSpaceHiDpiHostContainerMetadata { path = relativePath };
            }
            return new ScreenSpaceHiDpiHostContainerMetadata
            {
                path = relativePath,
                guid = ReadGuid(fullPath),
                sha256 = Sha256File(fullPath),
                sourcePathAbsent = true,
                folderAsset = File.ReadLines(fullPath)
                    .Any(line => string.Equals(line.Trim(), "folderAsset: yes", StringComparison.Ordinal))
            };
        }

        private static void ValidateUniquePhysicalIcuResource(string packageRoot)
        {
            var assetsRoot = Path.GetFullPath(Application.dataPath);
            var expectedPhysicalPath = Path.GetFullPath(Path.Combine(packageRoot,
                IcuPayloadPath.Replace('/', Path.DirectorySeparatorChar)));
            var logicalMatches = Directory.GetFiles(assetsRoot,
                    "icudtl.dat.*",
                    SearchOption.AllDirectories)
                .Where(path => string.Equals(Path.GetFileNameWithoutExtension(path),
                                   "icudtl.dat",
                                   StringComparison.Ordinal) &&
                               string.Equals(Path.GetFileName(Path.GetDirectoryName(path)),
                                   "Milestro",
                                   StringComparison.Ordinal) &&
                               string.Equals(Path.GetFileName(Path.GetDirectoryName(
                                       Path.GetDirectoryName(path))),
                                   "Resources",
                                   StringComparison.Ordinal))
                .Select(Path.GetFullPath)
                .ToArray();
            ScreenSpaceHiDpiPackageIdentityPolicy.RequireUniquePhysicalIcuResource(expectedPhysicalPath,
                logicalMatches);
            if (!string.Equals(Sha256File(expectedPhysicalPath),
                    ScreenSpaceHiDpiPackageManifest.ExpectedIcuSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The unique physical Milestro/icudtl.dat resource has the wrong bytes.");
            }
        }

        private static void CreateScene(string backend,
            string exactHead,
            string exactTree,
            string manifestSha256,
            TextAsset manifestAsset)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Screen Space HiDPI Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.025f, 0.035f, 1f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var canvasObject = new GameObject("Screen Space HiDPI Canvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);

            var textBoxObject = new GameObject("Screen Space HiDPI TextBox", typeof(RectTransform));
            textBoxObject.transform.SetParent(canvasObject.transform, false);
            ConfigureRect(textBoxObject.GetComponent<RectTransform>(), new Vector2(720f, 150f), new Vector2(0f, 350f));
            var textBoxProducer = textBoxObject.AddComponent<TextBoxRenderTextureProducer>();
            textBoxProducer.content =
                "<a href=\"milestro://integration/screen-space-hidpi\" id=\"screen-space-hidpi-link\">Screen Space HiDPI physical link — 清晰文本</a>\nLogical layout must not move.";
            textBoxProducer.size = 34f;
            var textBox = textBoxObject.AddComponent<TextBox>();
            textBox.raycastTarget = true;
            var pointerAudit = textBoxObject.AddComponent<ScreenSpaceHiDpiPhysicalPointerAudit>();

            var inputObject = new GameObject("Screen Space HiDPI TextInput", typeof(RectTransform));
            inputObject.transform.SetParent(canvasObject.transform, false);
            ConfigureRect(inputObject.GetComponent<RectTransform>(), new Vector2(720f, 150f), new Vector2(0f, 140f));
            var textInput = inputObject.AddComponent<TextInput>();
            textInput.Text = "Screen Space HiDPI input baseline";
            textInput.size = 34f;

            var slimObject = new GameObject("Screen Space HiDPI SlimText", typeof(RectTransform));
            slimObject.transform.SetParent(canvasObject.transform, false);
            ConfigureRect(slimObject.GetComponent<RectTransform>(), new Vector2(720f, 90f), new Vector2(0f, -60f));
            var slimProducer = slimObject.AddComponent<SlimTextRenderTextureProducer>();
            slimProducer.text = "SlimText HiDPI — 细线 AaBb 1234";
            slimProducer.fontSize = 34f;
            slimObject.AddComponent<ScreenSpaceHiDpiSlimTextGraphic>().Configure(slimProducer);

            var worldTextObject = new GameObject("Screen Space HiDPI WorldSpaceTextBox", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(worldTextObject, scene);
            worldTextObject.transform.position = new Vector3(-3f, -3.3f, 0f);
            var worldText = worldTextObject.AddComponent<WorldSpaceTextBox>();
            worldText.textureSizePixels = new Vector2Int(512, 128);
            worldText.pixelsPerUnit = 100f;
            worldTextObject.GetComponent<TextBoxRenderTextureProducer>().content = "WorldSpaceTextBox 512x128";

            var worldSlimObject = new GameObject("Screen Space HiDPI WorldSpaceSlimText", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(worldSlimObject, scene);
            worldSlimObject.transform.position = new Vector3(2.5f, -3.3f, 0f);
            var worldSlim = worldSlimObject.AddComponent<WorldSpaceSlimText>();
            worldSlim.textureSizePixels = new Vector2Int(384, 96);
            worldSlim.pixelsPerUnit = 100f;
            worldSlim.text = "WorldSpaceSlimText 384x96";

            var runnerObject = new GameObject("Screen Space HiDPI Scenario Runner");
            SceneManager.MoveGameObjectToScene(runnerObject, scene);
            runnerObject.AddComponent<ScreenSpaceHiDpiScenarioRunner>().Configure(canvas,
                camera,
                textBox,
                textInput,
                slimProducer,
                worldText,
                worldSlim,
                pointerAudit,
                manifestAsset,
                exactHead,
                exactTree,
                manifestSha256,
                backend);

            EditorUtility.SetDirty(textBoxObject);
            EditorUtility.SetDirty(inputObject);
            EditorUtility.SetDirty(slimObject);
            EditorUtility.SetDirty(worldTextObject);
            EditorUtility.SetDirty(worldSlimObject);
            EditorUtility.SetDirty(runnerObject);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Could not save Screen Space HiDPI fixture scene.");
            }
        }

        private static void ValidateLoadedPackageRoot(string packageRoot)
        {
            var packageFullPath = Path.GetFullPath(packageRoot).TrimEnd(Path.DirectorySeparatorChar) +
                                  Path.DirectorySeparatorChar;
            foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                if (script.GetClass() != typeof(TextBox))
                {
                    continue;
                }
                var assetPath = AssetDatabase.GetAssetPath(script);
                var fullPath = Path.GetFullPath(assetPath);
                if (!fullPath.StartsWith(packageFullPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The loaded Milestro TextBox script is not inside MILESTRO_SCREEN_SPACE_HIDPI_PACKAGE_ROOT: " +
                        fullPath + ".");
                }
                return;
            }
            throw new InvalidOperationException("Could not resolve the loaded Milestro TextBox script.");
        }

        private static void ValidateCleanGitSource(string sourceRepository, string exactHead, string exactTree)
        {
            var actualHead = RunGit(sourceRepository, "rev-parse HEAD").Trim();
            var actualTree = RunGit(sourceRepository, "rev-parse HEAD^{tree}").Trim();
            var status = RunGit(sourceRepository, "status --porcelain=v1 --untracked-files=all");
            if (!string.Equals(actualHead, exactHead, StringComparison.Ordinal) ||
                !string.Equals(actualTree, exactTree, StringComparison.Ordinal) ||
                !string.IsNullOrEmpty(status))
            {
                throw new InvalidOperationException(
                    "Screen Space HiDPI source is not the exact clean build identity: HEAD=" + actualHead +
                    " tree=" + actualTree + " dirty=" + !string.IsNullOrEmpty(status) + ".");
            }
        }

        private static string RunGit(string repository, string arguments)
        {
            var start = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "-C \"" + repository.Replace("\"", "\\\"") + "\" " + arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(start) ??
                                throw new InvalidOperationException("Could not start git for Screen Space HiDPI identity validation.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(30000) || process.ExitCode != 0)
            {
                throw new InvalidOperationException("Screen Space HiDPI git validation failed: " + error.Trim());
            }
            return output;
        }

        private static void ConfigureRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static string RequiredObjectId(string name)
        {
            var value = Environment.GetEnvironmentVariable(name) ?? string.Empty;
            ScreenSpaceHiDpiPackageManifest.RequireObjectId(value, name);
            return value;
        }

        private static string RequiredSha256(string name)
        {
            var value = Environment.GetEnvironmentVariable(name) ?? string.Empty;
            if (!ScreenSpaceHiDpiHash.IsSha256(value))
            {
                throw new InvalidOperationException(name + " must be an exact SHA-256 digest.");
            }
            return value;
        }

        private static string RequiredDirectory(string name)
        {
            var value = Environment.GetEnvironmentVariable(name) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value))
            {
                throw new InvalidOperationException(name + " must name an existing directory.");
            }
            return Path.GetFullPath(value);
        }

        internal static string ReadGuid(string path)
        {
            var values = File.ReadLines(path)
                .Where(line => line.StartsWith("guid: ", StringComparison.Ordinal))
                .Select(line => line.Substring("guid: ".Length).Trim())
                .ToArray();
            if (values.Length == 1)
            {
                var value = values[0];
                if (value.Length == 32 && value.All(Uri.IsHexDigit))
                {
                    return value;
                }
            }
            throw new InvalidOperationException("Package meta file has no valid GUID: " + path + ".");
        }

        internal static string Sha256File(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var builder = new StringBuilder(64);
            foreach (var value in sha.ComputeHash(stream))
            {
                builder.Append(value.ToString("x2"));
            }
            return builder.ToString();
        }

        private static string RelativePath(string root, string path)
        {
            var rootUri = new Uri(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
                                  Path.DirectorySeparatorChar);
            var pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString())
                .Replace('\\', '/');
        }

        private static void ValidateSceneSetup()
        {
            for (var index = 0; index < SceneManager.sceneCount; ++index)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                {
                    throw new InvalidOperationException(
                        "Save or discard dirty scene changes before building the Screen Space HiDPI harness: " + scene.path + ".");
                }
            }
        }

        private static void RestoreSceneSetup(SceneSetup[] setup)
        {
            if (setup.Length > 0 && setup.All(entry => !string.IsNullOrEmpty(entry.path)))
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            else
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void DeleteGeneratedAssets()
        {
            if (AssetDatabase.IsValidFolder(RootPath) && !AssetDatabase.DeleteAsset(RootPath))
            {
                throw new InvalidOperationException("Could not delete temporary Screen Space HiDPI fixture root.");
            }
            AssetDatabase.Refresh();
        }

        public sealed class GeneratedFixture : IDisposable
        {
            private readonly SceneSetup[] setup;
            private bool disposed;

            internal GeneratedFixture(SceneSetup[] setup,
                string exactHead,
                string exactTree,
                string packageManifestSha256,
                int packageFileCount,
                int packageGuidCount)
            {
                this.setup = setup;
                ExactHead = exactHead;
                ExactTree = exactTree;
                PackageManifestSha256 = packageManifestSha256;
                PackageFileCount = packageFileCount;
                PackageGuidCount = packageGuidCount;
            }

            public string ExactHead { get; }
            public string ExactTree { get; }
            public string PackageManifestSha256 { get; }
            public int PackageFileCount { get; }
            public int PackageGuidCount { get; }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }
                disposed = true;
                try
                {
                    RestoreSceneSetup(setup);
                }
                finally
                {
                    DeleteGeneratedAssets();
                }
            }
        }
    }

    internal static class ScreenSpaceHiDpiPackageIdentityPolicy
    {
        internal static ScreenSpaceHiDpiHostContainerMetadata[] RequireExactHostContainerMetadata(
            IReadOnlyCollection<string> sourceMissingPaths,
            IReadOnlyCollection<ScreenSpaceHiDpiHostContainerMetadata> loadedMetadata)
        {
            if (sourceMissingPaths == null || loadedMetadata == null)
            {
                throw new ArgumentNullException(sourceMissingPaths == null
                    ? nameof(sourceMissingPaths)
                    : nameof(loadedMetadata));
            }

            var expectedPaths = ScreenSpaceHiDpiPackageManifest.HostContainerMetaPaths;
            var sourcePaths = sourceMissingPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (!sourcePaths.SequenceEqual(expectedPaths, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Screen Space HiDPI source folder/meta closure is not the exact two-path allowlist.");
            }

            var loadedByPath = loadedMetadata
                .GroupBy(metadata => metadata?.path ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            if (loadedByPath.Count != expectedPaths.Length ||
                expectedPaths.Any(path => !loadedByPath.TryGetValue(path, out var values) || values.Length != 1))
            {
                throw new InvalidOperationException(
                    "Loaded Screen Space HiDPI package does not contain exactly the two host-owned folder metas.");
            }

            var result = new List<ScreenSpaceHiDpiHostContainerMetadata>(expectedPaths.Length);
            foreach (var path in expectedPaths)
            {
                var metadata = loadedByPath[path][0];
                if (metadata == null || metadata.guid == null || metadata.guid.Length != 32 ||
                    metadata.guid.Any(character => !Uri.IsHexDigit(character)) ||
                    !ScreenSpaceHiDpiHash.IsSha256(metadata.sha256) ||
                    !metadata.sourcePathAbsent || !metadata.folderAsset)
                {
                    throw new InvalidOperationException(
                        "Loaded Screen Space HiDPI host-owned folder meta identity is invalid: " + path + ".");
                }
                result.Add(metadata);
            }
            if (result.Select(metadata => metadata.guid)
                    .Distinct(StringComparer.Ordinal)
                    .Count() != result.Count)
            {
                throw new InvalidOperationException(
                    "Loaded Screen Space HiDPI host-owned folder metas have duplicate GUIDs.");
            }
            return result.ToArray();
        }

        internal static void RequireEquivalentPayload(
            IReadOnlyCollection<ScreenSpaceHiDpiPackageManifestEntry> sourceEntries,
            IReadOnlyCollection<ScreenSpaceHiDpiPackageManifestEntry> loadedEntries)
        {
            if (sourceEntries == null || loadedEntries == null)
            {
                throw new ArgumentNullException(sourceEntries == null
                    ? nameof(sourceEntries)
                    : nameof(loadedEntries));
            }

            var sourceByPath = ToPathMap(sourceEntries, "source");
            var loadedByPath = ToPathMap(loadedEntries, "loaded");
            if (sourceByPath.Count != loadedByPath.Count)
            {
                throw new InvalidOperationException(
                    "Loaded Screen Space HiDPI package has an extra or missing payload entry.");
            }

            foreach (var pair in sourceByPath)
            {
                if (!loadedByPath.TryGetValue(pair.Key, out var loaded) ||
                    !string.Equals(pair.Value.sha256, loaded.sha256, StringComparison.Ordinal) ||
                    !string.Equals(pair.Value.guid, loaded.guid, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Loaded Screen Space HiDPI package payload differs from the exact source at " +
                        pair.Key + ".");
                }
            }
        }

        internal static void RequireEquivalentDirectoryLayout(
            IReadOnlyCollection<string> sourceDirectoryPaths,
            IReadOnlyCollection<string> loadedDirectoryPaths)
        {
            if (sourceDirectoryPaths == null || loadedDirectoryPaths == null ||
                !sourceDirectoryPaths.OrderBy(path => path, StringComparer.Ordinal)
                    .SequenceEqual(loadedDirectoryPaths.OrderBy(path => path, StringComparer.Ordinal),
                        StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Loaded Screen Space HiDPI package directory layout differs from the exact source.");
            }
        }

        internal static void RequireExactLoadedMetaClosure(
            IReadOnlyCollection<string> sourceMetaPaths,
            IReadOnlyCollection<string> loadedMetaPaths)
        {
            if (sourceMetaPaths == null || loadedMetaPaths == null)
            {
                throw new ArgumentNullException(sourceMetaPaths == null
                    ? nameof(sourceMetaPaths)
                    : nameof(loadedMetaPaths));
            }
            var expectedLoadedPaths = sourceMetaPaths
                .Concat(ScreenSpaceHiDpiPackageManifest.HostContainerMetaPaths)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var actualLoadedPaths = loadedMetaPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (!expectedLoadedPaths.SequenceEqual(actualLoadedPaths, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Loaded Screen Space HiDPI package meta closure is not exact source plus the two host-owned paths.");
            }
        }

        internal static void RequireUniqueMetaGuids(IReadOnlyCollection<string> guids)
        {
            if (guids == null || guids.Any(guid => guid == null || guid.Length != 32 ||
                                             guid.Any(character => !Uri.IsHexDigit(character))) ||
                guids.Distinct(StringComparer.Ordinal).Count() != guids.Count)
            {
                throw new InvalidOperationException(
                    "Loaded Screen Space HiDPI package contains an invalid or duplicate Unity GUID.");
            }
        }

        internal static void RequireUniquePhysicalIcuResource(string expectedPhysicalPath,
            IReadOnlyCollection<string> logicalResourcePaths)
        {
            if (string.IsNullOrEmpty(expectedPhysicalPath) || logicalResourcePaths == null)
            {
                throw new ArgumentException("Screen Space HiDPI physical ICU resource inputs are incomplete.");
            }
            if (logicalResourcePaths.Count != 1 ||
                !string.Equals(logicalResourcePaths.Single(),
                    expectedPhysicalPath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Milestro/icudtl.dat must resolve to one physical .bytes file inside the loaded package root.");
            }
        }

        private static Dictionary<string, ScreenSpaceHiDpiPackageManifestEntry> ToPathMap(
            IEnumerable<ScreenSpaceHiDpiPackageManifestEntry> entries,
            string label)
        {
            var result = new Dictionary<string, ScreenSpaceHiDpiPackageManifestEntry>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.path) ||
                    !ScreenSpaceHiDpiHash.IsSha256(entry.sha256) ||
                    !result.TryAdd(entry.path, entry))
                {
                    throw new InvalidOperationException(
                        "Screen Space HiDPI " + label + " payload contains an invalid or duplicate entry.");
                }
            }
            return result;
        }
    }
}

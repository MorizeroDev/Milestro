using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Milestro.Components;
using Milestro.Tests.TextInputLifecycle.Integration.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Milestro.Tests.TextInputLifecycle.Integration.EditMode
{
    public class TextInputLifecyclePersistentAssetTests
    {
        private const string TestHead = "1590000000000000000000000000000000000001";
        private const string TestTree = "1590000000000000000000000000000000000002";

        [OneTimeSetUp]
        public void CreateEmptyTestRunnerBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(scene.handle));
            Assert.That(scene.path, Is.Empty);
            Assert.That(scene.GetRootGameObjects(), Is.Empty);
            Assert.That(scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)), Is.Empty);
        }

        [Test]
        public void PersistentBindingsSurvivePrefabImportAndSceneReloadWithoutNotification()
        {
            using var testSceneGuard = TestRunnerSceneGuard.Enter();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            Scene opened = default;
            try
            {
                TextInputLifecycleIntegrationStableRecorder.Arm();
                try
                {
                    TextInputLifecycleIntegrationFixtureBuilder.GenerateAssetsWithoutValidation(TestHead,
                        TestTree);
                    AssertStableRecorderIsSilent("initial asset generation/import");
                }
                finally
                {
                    TextInputLifecycleIntegrationStableRecorder.Disarm();
                    TextInputLifecycleIntegrationStableRecorder.Reset();
                }

                TextInputLifecycleIntegrationStableRecorder.Arm();
                try
                {
                    TextInputLifecycleIntegrationFixtureBuilder.ValidateGeneratedAssets();
                    AssertStableRecorderIsSilent("first builder validation/scene open");
                }
                finally
                {
                    TextInputLifecycleIntegrationStableRecorder.Disarm();
                    TextInputLifecycleIntegrationStableRecorder.Reset();
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    TextInputLifecycleIntegrationFixtureBuilder.PrefabPath);
                Assert.That(prefab, Is.Not.Null);
                AssertReceiverIsSilent(prefab!.GetComponent<TextInputLifecycleIntegrationReceiver>());

                TextInputLifecycleIntegrationStableRecorder.Arm();
                try
                {
                    AssetDatabase.ImportAsset(TextInputLifecycleIntegrationFixtureBuilder.PrefabPath,
                        ImportAssetOptions.ForceUpdate);
                    AssetDatabase.ImportAsset(TextInputLifecycleIntegrationFixtureBuilder.ScenePath,
                        ImportAssetOptions.ForceUpdate);
                    AssertStableRecorderIsSilent("asset import/deserialization");
                }
                finally
                {
                    TextInputLifecycleIntegrationStableRecorder.Disarm();
                    TextInputLifecycleIntegrationStableRecorder.Reset();
                }
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    TextInputLifecycleIntegrationFixtureBuilder.PrefabPath);
                Assert.That(prefab, Is.Not.Null);
                AssertReceiverIsSilent(prefab!.GetComponent<TextInputLifecycleIntegrationReceiver>());

                TextInputLifecycleIntegrationStableRecorder.Arm();
                try
                {
                    opened = EditorSceneManager.OpenScene(
                        TextInputLifecycleIntegrationFixtureBuilder.ScenePath,
                        OpenSceneMode.Additive);
                    AssertStableRecorderIsSilent("post-import Editor scene load");
                }
                finally
                {
                    TextInputLifecycleIntegrationStableRecorder.Disarm();
                    TextInputLifecycleIntegrationStableRecorder.Reset();
                }
                var persistentObject = FindRoot(opened, "InspectorPersistent");
                Assert.That(persistentObject, Is.Not.Null);
                var receiver = persistentObject!.GetComponent<TextInputLifecycleIntegrationReceiver>();
                var input = persistentObject.GetComponent<TextInput>();
                AssertReceiverIsSilent(receiver);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(persistentObject),
                    Is.EqualTo(prefab));
                Assert.That(PrefabUtility.HasPrefabInstanceAnyOverrides(persistentObject, false), Is.True);

                Assert.That(input.onValueChanged.GetPersistentListenerState(0),
                    Is.EqualTo(UnityEventCallState.RuntimeOnly));
                input.Text = "explicit-after-scene-load";
                Assert.That(receiver.ValueChangedCount, Is.Zero);
                Assert.That(receiver.ValueChangedPayload, Is.Empty);
                Assert.That(receiver.EndEditCount, Is.Zero);
                Assert.That(receiver.FocusGainedCount, Is.Zero);
                Assert.That(receiver.FocusLostCount, Is.Zero);

                Assert.That(EditorSceneManager.CloseScene(opened, removeScene: true), Is.True);
                opened = default;
                TextInputLifecycleIntegrationStableRecorder.Arm();
                try
                {
                    opened = EditorSceneManager.OpenScene(
                        TextInputLifecycleIntegrationFixtureBuilder.ScenePath,
                        OpenSceneMode.Additive);
                    AssertStableRecorderIsSilent("Editor scene reopen");
                }
                finally
                {
                    TextInputLifecycleIntegrationStableRecorder.Disarm();
                    TextInputLifecycleIntegrationStableRecorder.Reset();
                }
                AssertReceiverIsSilent(FindRoot(opened, "InspectorPersistent")!
                    .GetComponent<TextInputLifecycleIntegrationReceiver>());
            }
            finally
            {
                TextInputLifecycleIntegrationStableRecorder.Disarm();
                TextInputLifecycleIntegrationStableRecorder.Reset();
                if (opened.IsValid() && opened.isLoaded)
                {
                    EditorSceneManager.CloseScene(opened, removeScene: true);
                }
                var allSaved = true;
                for (var index = 0; index < setup.Length; ++index)
                {
                    allSaved &= !string.IsNullOrEmpty(setup[index].path);
                }
                if (setup.Length > 0 && allSaved)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
                else if (SceneManager.sceneCount == 0 ||
                         SceneManager.GetActiveScene().path.Length != 0)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                TextInputLifecycleIntegrationFixtureBuilder.DeleteGeneratedAssets();
            }
        }

        [Test]
        public void ProfilerLauncherRestoresBuildSettingsAndPlayModeStartSceneExactly()
        {
            using var testSceneGuard = TestRunnerSceneGuard.Enter();
            if (TextInputLifecycleIntegrationProfilerLauncher.HasPendingRestore)
            {
                TextInputLifecycleIntegrationProfilerLauncher.RestoreEditorState();
            }
            var originalScenes = EditorBuildSettings.scenes;
            var originalStartScene = EditorSceneManager.playModeStartScene;
            try
            {
                TextInputLifecycleIntegrationFixtureBuilder.Generate(TestHead, TestTree);

                TextInputLifecycleIntegrationProfilerLauncher.PrepareTemporaryEditorState();

                Assert.That(TextInputLifecycleIntegrationProfilerLauncher.HasPendingRestore, Is.True);
                var bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    TextInputLifecycleIntegrationFixtureBuilder.BootstrapScenePath);
                var target = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    TextInputLifecycleIntegrationFixtureBuilder.ScenePath);
                Assert.That(bootstrap, Is.Not.Null);
                Assert.That(target, Is.Not.Null);
                Assert.That(EditorSceneManager.playModeStartScene, Is.EqualTo(bootstrap));
                AssertPreparedBuildScene(TextInputLifecycleIntegrationFixtureBuilder.BootstrapScenePath);
                AssertPreparedBuildScene(TextInputLifecycleIntegrationFixtureBuilder.ScenePath);

                TextInputLifecycleIntegrationProfilerLauncher.RestoreEditorState();

                Assert.That(TextInputLifecycleIntegrationProfilerLauncher.HasPendingRestore, Is.False);
                AssertBuildSettingsEqual(EditorBuildSettings.scenes, originalScenes);
                Assert.That(EditorSceneManager.playModeStartScene, Is.EqualTo(originalStartScene));
            }
            finally
            {
                if (TextInputLifecycleIntegrationProfilerLauncher.HasPendingRestore)
                {
                    TextInputLifecycleIntegrationProfilerLauncher.RestoreEditorState();
                }
                EditorBuildSettings.scenes = originalScenes;
                EditorSceneManager.playModeStartScene = originalStartScene;
                TextInputLifecycleIntegrationFixtureBuilder.DeleteGeneratedAssets();
            }
        }

        private static GameObject? FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var match = Find(root.transform, name);
                if (match != null)
                {
                    return match.gameObject;
                }
            }
            return null;
        }

        private static Transform? Find(Transform current, string name)
        {
            if (current.name == name)
            {
                return current;
            }
            for (var index = 0; index < current.childCount; ++index)
            {
                var match = Find(current.GetChild(index), name);
                if (match != null)
                {
                    return match;
                }
            }
            return null;
        }

        private static void AssertReceiverIsSilent(TextInputLifecycleIntegrationReceiver receiver)
        {
            Assert.That(receiver, Is.Not.Null);
            Assert.That(receiver.ValueChangedCount, Is.Zero);
            Assert.That(receiver.EndEditCount, Is.Zero);
            Assert.That(receiver.FocusGainedCount, Is.Zero);
            Assert.That(receiver.FocusLostCount, Is.Zero);
            Assert.That(receiver.Sequence, Is.Empty);
        }

        private static void AssertStableRecorderIsSilent(string phase)
        {
            Assert.That(TextInputLifecycleIntegrationStableRecorder.IsArmed, Is.True,
                $"Stable recorder was not armed for {phase}.");
            Assert.That(TextInputLifecycleIntegrationStableRecorder.ValueChangedCount, Is.Zero,
                $"{phase} invoked onValueChanged on an imported or transient receiver.");
            Assert.That(TextInputLifecycleIntegrationStableRecorder.EndEditCount, Is.Zero,
                $"{phase} invoked onEndEdit on an imported or transient receiver.");
            Assert.That(TextInputLifecycleIntegrationStableRecorder.FocusGainedCount, Is.Zero,
                $"{phase} invoked onFocusGained on an imported or transient receiver.");
            Assert.That(TextInputLifecycleIntegrationStableRecorder.FocusLostCount, Is.Zero,
                $"{phase} invoked onFocusLost on an imported or transient receiver.");
        }

        private static void AssertPreparedBuildScene(string path)
        {
            var count = 0;
            var enabled = false;
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.path != path)
                {
                    continue;
                }
                ++count;
                enabled = scene.enabled;
            }
            Assert.That(count, Is.EqualTo(1), $"Prepared scene count mismatch for {path}.");
            Assert.That(enabled, Is.True, $"Prepared scene is disabled: {path}.");
            Assert.That(SceneUtility.GetBuildIndexByScenePath(path), Is.GreaterThanOrEqualTo(0),
                $"Prepared scene is not resolvable through Build Settings: {path}.");
        }

        private static void AssertBuildSettingsEqual(EditorBuildSettingsScene[] actual,
            EditorBuildSettingsScene[] expected)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (var index = 0; index < actual.Length; ++index)
            {
                Assert.That(actual[index].path, Is.EqualTo(expected[index].path),
                    $"Build Settings path mismatch at index {index}.");
                Assert.That(actual[index].enabled, Is.EqualTo(expected[index].enabled),
                    $"Build Settings enabled mismatch at index {index}.");
            }
        }

        private sealed class TestRunnerSceneGuard : IDisposable
        {
            private readonly string tempRoot;
            private readonly string guardPath;
            private readonly int initialGuardHandle;
            private readonly bool originalDirty;
            private bool disposed;

            private TestRunnerSceneGuard(string tempRoot,
                string guardPath,
                int initialGuardHandle,
                bool originalDirty)
            {
                this.tempRoot = tempRoot;
                this.guardPath = guardPath;
                this.initialGuardHandle = initialGuardHandle;
                this.originalDirty = originalDirty;
            }

            public static TestRunnerSceneGuard Enter()
            {
                if (SceneManager.sceneCount != 1)
                {
                    throw new InvalidOperationException(
                        "Task 159 EditMode Integration only takes ownership of one untitled neutral scene.");
                }

                var scene = SceneManager.GetSceneAt(0);
                if (!IsUntitledNeutral(scene) ||
                    SceneManager.GetActiveScene().handle != scene.handle)
                {
                    throw new InvalidOperationException(
                        "Task 159 EditMode Integration refuses saved scenes, roots, EventSystems, or " +
                        "a non-active Test Runner scene.");
                }

                var originalDirty = scene.isDirty;
                var tempRoot = "Assets/__MilestroTask159TestGuard_" +
                               Guid.NewGuid().ToString("N");
                var guardPath = tempRoot + "/Guard.unity";
                var folderGuid = AssetDatabase.CreateFolder("Assets",
                    tempRoot.Substring("Assets/".Length));
                if (string.IsNullOrEmpty(folderGuid) || !AssetDatabase.IsValidFolder(tempRoot))
                {
                    throw new InvalidOperationException(
                        $"Could not create the test-owned scene guard root: {tempRoot}");
                }

                var guardHandle = scene.handle;
                try
                {
                    var saveSucceeded = EditorSceneManager.SaveScene(scene, guardPath);
                    if (!saveSucceeded)
                    {
                        throw new InvalidOperationException(
                            $"Could not save the test-owned scene guard: {guardPath}");
                    }
                    if (!IsExactSavedGuard(scene, guardPath, guardHandle) ||
                        !HasExactGuardRootContent(tempRoot, guardPath))
                    {
                        throw new InvalidOperationException(
                            "Saved test guard failed its exact postcondition. " +
                            SafeDescribeRootEntries(tempRoot) + ". " +
                            DescribeLoadedScenes());
                    }
                    return new TestRunnerSceneGuard(tempRoot,
                        guardPath,
                        guardHandle,
                        originalDirty);
                }
                catch (Exception originalFailure)
                {
                    var cleanupFailure = CleanupFailedEnter(tempRoot,
                        guardPath,
                        guardHandle,
                        originalDirty);
                    if (cleanupFailure != null)
                    {
                        throw new InvalidOperationException(
                            "Task 159 test guard entry failed and fail-safe cleanup could not " +
                            "complete. Original failure: " + originalFailure.Message +
                            "\nCleanup failure: " + cleanupFailure,
                            originalFailure);
                    }
                    throw;
                }
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }
                disposed = true;

                var failures = new List<string>();
                var guardHandles = new List<int> { initialGuardHandle };
                for (var index = SceneManager.sceneCount - 1; index >= 0; --index)
                {
                    var scene = SceneManager.GetSceneAt(index);
                    if (scene.path == guardPath)
                    {
                        guardHandles.Add(scene.handle);
                        continue;
                    }
                    if (!scene.path.StartsWith(
                            TextInputLifecycleIntegrationFixtureBuilder.RootPath + "/",
                            StringComparison.Ordinal))
                    {
                        failures.Add($"Refusing to close a non-test scene: {Describe(scene)}");
                        continue;
                    }
                    if (!EditorSceneManager.CloseScene(scene, removeScene: true))
                    {
                        failures.Add($"Could not close test-owned scene: {Describe(scene)}");
                    }
                }

                Scene savedGuard = default;
                for (var index = 0; index < SceneManager.sceneCount; ++index)
                {
                    var scene = SceneManager.GetSceneAt(index);
                    if (scene.path == guardPath)
                    {
                        savedGuard = scene;
                        break;
                    }
                }
                if (failures.Count == 0 &&
                    (!savedGuard.IsValid() ||
                     !IsExactSavedGuard(savedGuard, guardPath, savedGuard.handle)))
                {
                    failures.Add("The exact saved test guard was not restored before cleanup.");
                }

                if (failures.Count == 0)
                {
                    var neutral = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                    if (originalDirty)
                    {
                        EditorSceneManager.MarkSceneDirty(neutral);
                    }
                    foreach (var handle in guardHandles)
                    {
                        if (IsLoaded(handle))
                        {
                            failures.Add($"Test guard handle {handle} survived cleanup.");
                        }
                    }
                    if (SceneManager.sceneCount != 1 ||
                        SceneManager.GetActiveScene().handle != neutral.handle ||
                        !IsUntitledNeutral(neutral) || neutral.isDirty != originalDirty)
                    {
                        failures.Add("The neutral Test Runner scene was not restored exactly.");
                    }
                }

                if (failures.Count == 0 && AssetDatabase.IsValidFolder(tempRoot))
                {
                    if (!AssetDatabase.DeleteAsset(tempRoot))
                    {
                        failures.Add($"Could not delete test-owned guard root: {tempRoot}");
                    }
                    AssetDatabase.Refresh();
                }
                if (AssetDatabase.IsValidFolder(tempRoot))
                {
                    failures.Add($"Test-owned guard root survived cleanup: {tempRoot}");
                }
                if (failures.Count != 0)
                {
                    throw new InvalidOperationException(string.Join("\n", failures));
                }
            }

            private static bool IsUntitledNeutral(Scene scene)
            {
                return scene.IsValid() && scene.isLoaded &&
                       string.IsNullOrEmpty(scene.path) &&
                       scene.GetRootGameObjects().Length == 0 &&
                       CountEventSystems(scene) == 0;
            }

            private static bool IsExactSavedGuard(Scene scene,
                string expectedPath,
                int expectedHandle)
            {
                return scene.IsValid() && scene.isLoaded &&
                       scene.handle == expectedHandle &&
                       scene.path == expectedPath && !scene.isDirty &&
                       scene.GetRootGameObjects().Length == 0 &&
                       CountEventSystems(scene) == 0 &&
                       SceneManager.sceneCount == 1 &&
                       SceneManager.GetActiveScene().handle == expectedHandle;
            }

            private static string? CleanupFailedEnter(string tempRoot,
                string guardPath,
                int originalHandle,
                bool originalDirty)
            {
                try
                {
                    if (IsSoleOwnedGuard(guardPath, originalHandle) &&
                        HasExactGuardRootContent(tempRoot, guardPath))
                    {
                        var neutral = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                            NewSceneMode.Single);
                        if (originalDirty)
                        {
                            EditorSceneManager.MarkSceneDirty(neutral);
                        }
                        if (IsLoaded(originalHandle))
                        {
                            return $"Owned guard handle {originalHandle} survived the Single " +
                                   "transition. " + DescribeLoadedScenes();
                        }
                        if (SceneManager.sceneCount != 1 ||
                            SceneManager.GetActiveScene().handle != neutral.handle ||
                            !IsUntitledNeutral(neutral) || neutral.isDirty != originalDirty)
                        {
                            return "Neutral Test Runner setup was not restored after leaving the " +
                                   "owned guard. " + DescribeLoadedScenes();
                        }
                        return DeleteExactTempRoot(tempRoot,
                            guardPath,
                            allowGuardContent: true);
                    }

                    if (IsOriginalUntitledScene(originalHandle) &&
                        HasExactEmptyTempRoot(tempRoot))
                    {
                        return DeleteExactTempRoot(tempRoot,
                            guardPath,
                            allowGuardContent: false);
                    }

                    return "Refusing cleanup because the original handle is no longer the sole " +
                           "exact owned Guard, or because the unsaved scene/temp root content " +
                           $"changed. Evidence was preserved at {tempRoot}. " +
                           SafeDescribeRootEntries(tempRoot) + ". " + DescribeLoadedScenes();
                }
                catch (Exception cleanupException)
                {
                    return cleanupException.GetType().Name + ": " + cleanupException.Message +
                           ". Evidence: " + SafeDescribeRootEntries(tempRoot) + ". " +
                           DescribeLoadedScenes();
                }
            }

            private static bool IsSoleOwnedGuard(string guardPath, int originalHandle)
            {
                if (SceneManager.sceneCount != 1)
                {
                    return false;
                }
                var scene = SceneManager.GetSceneAt(0);
                return scene.IsValid() && scene.isLoaded &&
                       scene.handle == originalHandle &&
                       scene.path == guardPath &&
                       SceneManager.GetActiveScene().handle == originalHandle &&
                       scene.GetRootGameObjects().Length == 0 &&
                       CountEventSystems(scene) == 0;
            }

            private static bool IsOriginalUntitledScene(int originalHandle)
            {
                if (SceneManager.sceneCount != 1)
                {
                    return false;
                }
                var scene = SceneManager.GetSceneAt(0);
                return scene.IsValid() && scene.isLoaded &&
                       scene.handle == originalHandle &&
                       SceneManager.GetActiveScene().handle == originalHandle &&
                       IsUntitledNeutral(scene);
            }

            private static bool HasExactEmptyTempRoot(string tempRoot)
            {
                return HasExpectedRootAndFolderMeta(tempRoot) &&
                       Directory.GetFileSystemEntries(tempRoot).Length == 0;
            }

            private static bool HasExactGuardRootContent(string tempRoot, string guardPath)
            {
                if (!HasExpectedRootAndFolderMeta(tempRoot))
                {
                    return false;
                }

                var guardFound = false;
                foreach (var entry in Directory.GetFileSystemEntries(tempRoot))
                {
                    if (SamePath(entry, guardPath))
                    {
                        if (!IsRegularFile(entry))
                        {
                            return false;
                        }
                        guardFound = true;
                        continue;
                    }
                    if (SamePath(entry, guardPath + ".meta") && IsRegularFile(entry))
                    {
                        continue;
                    }
                    return false;
                }
                return guardFound;
            }

            private static bool HasExpectedRootAndFolderMeta(string tempRoot)
            {
                return AssetDatabase.IsValidFolder(tempRoot) &&
                       Directory.Exists(tempRoot) && !IsReparsePoint(tempRoot) &&
                       IsRegularFile(tempRoot + ".meta");
            }

            private static string? DeleteExactTempRoot(string tempRoot,
                string guardPath,
                bool allowGuardContent)
            {
                if (!AssetDatabase.IsValidFolder(tempRoot))
                {
                    return null;
                }
                var contentIsExact = allowGuardContent
                    ? HasExactGuardRootContent(tempRoot, guardPath)
                    : HasExactEmptyTempRoot(tempRoot);
                if (!contentIsExact)
                {
                    return "Refusing to delete changed test-owned guard content. " +
                           SafeDescribeRootEntries(tempRoot);
                }
                if (!AssetDatabase.DeleteAsset(tempRoot))
                {
                    return $"Could not delete test-owned guard root: {tempRoot}";
                }
                AssetDatabase.Refresh();
                return AssetDatabase.IsValidFolder(tempRoot)
                    ? $"Test-owned guard root survived deletion: {tempRoot}"
                    : null;
            }

            private static bool IsRegularFile(string path)
            {
                return File.Exists(path) && !Directory.Exists(path) &&
                       !IsReparsePoint(path);
            }

            private static bool IsReparsePoint(string path)
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }

            private static bool SamePath(string left, string right)
            {
                return string.Equals(Path.GetFullPath(left),
                    Path.GetFullPath(right),
                    StringComparison.Ordinal);
            }

            private static int CountEventSystems(Scene scene)
            {
                var count = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    count += root.GetComponentsInChildren<EventSystem>(true).Length;
                }
                return count;
            }

            private static bool IsLoaded(int handle)
            {
                for (var index = 0; index < SceneManager.sceneCount; ++index)
                {
                    if (SceneManager.GetSceneAt(index).handle == handle)
                    {
                        return true;
                    }
                }
                return false;
            }

            private static string Describe(Scene scene)
            {
                return $"handle={scene.handle},path='{scene.path}',dirty={scene.isDirty}," +
                       $"roots={scene.GetRootGameObjects().Length}," +
                       $"eventSystems={CountEventSystems(scene)}";
            }

            private static string SafeDescribeRootEntries(string tempRoot)
            {
                try
                {
                    var entries = new List<string>();
                    if (Directory.Exists(tempRoot) && !IsReparsePoint(tempRoot))
                    {
                        foreach (var entry in Directory.GetFileSystemEntries(tempRoot))
                        {
                            entries.Add(DescribeEntryRelativeToRoot(tempRoot, entry));
                        }
                        entries.Sort(StringComparer.Ordinal);
                    }
                    return "root=" + DescribeEntry(tempRoot, ".") +
                           ",folderMeta=" +
                           DescribeEntry(tempRoot + ".meta",
                               "../" + Path.GetFileName(tempRoot) + ".meta") +
                           ",entries=[" + string.Join(", ", entries) + "]";
                }
                catch (Exception exception)
                {
                    return $"rootEntries=<unreadable:{exception.GetType().Name}:" +
                           exception.Message + ">";
                }
            }

            private static string DescribeEntryRelativeToRoot(string tempRoot, string path)
            {
                var root = Path.GetFullPath(tempRoot).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                var fullPath = Path.GetFullPath(path);
                var prefix = root + Path.DirectorySeparatorChar;
                var relativePath = fullPath.StartsWith(prefix, StringComparison.Ordinal)
                    ? fullPath.Substring(prefix.Length)
                    : fullPath;
                return DescribeEntry(path, relativePath);
            }

            private static string DescribeEntry(string path,
                string relativePath)
            {
                string type;
                try
                {
                    var attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        type = "symlink";
                    }
                    else if ((attributes & FileAttributes.Directory) != 0)
                    {
                        type = "directory";
                    }
                    else
                    {
                        type = "file";
                    }
                }
                catch (FileNotFoundException)
                {
                    type = "missing";
                }
                catch (DirectoryNotFoundException)
                {
                    type = "missing";
                }
                return type + ":" + relativePath.Replace('\\', '/');
            }

            private static string DescribeLoadedScenes()
            {
                var scenes = new List<string>();
                for (var index = 0; index < SceneManager.sceneCount; ++index)
                {
                    scenes.Add(Describe(SceneManager.GetSceneAt(index)));
                }
                return scenes.Count == 0 ? "loadedScenes=<none>" :
                    "loadedScenes=[" + string.Join("; ", scenes) + "]";
            }
        }
    }
}

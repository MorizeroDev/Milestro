using System;
using System.Collections.Generic;
using Milestro.Components;
using Milestro.TextInputLifecycleQA.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Milestro.TextInputLifecycleQA.Tests
{
    public class TextInputLifecyclePersistentAssetTests
    {
        private const string TestHead = "1590000000000000000000000000000000000001";
        private const string TestTree = "1590000000000000000000000000000000000002";

        [Test]
        public void PersistentBindingsSurvivePrefabImportAndSceneReloadWithoutNotification()
        {
            using var testSceneGuard = TestRunnerSceneGuard.Enter();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            Scene opened = default;
            try
            {
                TextInputLifecycleQaStableRecorder.Arm();
                try
                {
                    TextInputLifecycleQaFixtureBuilder.GenerateAssetsWithoutValidation(TestHead,
                        TestTree);
                    AssertStableRecorderIsSilent("initial asset generation/import");
                }
                finally
                {
                    TextInputLifecycleQaStableRecorder.Disarm();
                    TextInputLifecycleQaStableRecorder.Reset();
                }

                TextInputLifecycleQaStableRecorder.Arm();
                try
                {
                    TextInputLifecycleQaFixtureBuilder.ValidateGeneratedAssets();
                    AssertStableRecorderIsSilent("first builder validation/scene open");
                }
                finally
                {
                    TextInputLifecycleQaStableRecorder.Disarm();
                    TextInputLifecycleQaStableRecorder.Reset();
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    TextInputLifecycleQaFixtureBuilder.PrefabPath);
                Assert.That(prefab, Is.Not.Null);
                AssertReceiverIsSilent(prefab!.GetComponent<TextInputLifecycleQaReceiver>());

                TextInputLifecycleQaStableRecorder.Arm();
                try
                {
                    AssetDatabase.ImportAsset(TextInputLifecycleQaFixtureBuilder.PrefabPath,
                        ImportAssetOptions.ForceUpdate);
                    AssetDatabase.ImportAsset(TextInputLifecycleQaFixtureBuilder.ScenePath,
                        ImportAssetOptions.ForceUpdate);
                    AssertStableRecorderIsSilent("asset import/deserialization");
                }
                finally
                {
                    TextInputLifecycleQaStableRecorder.Disarm();
                    TextInputLifecycleQaStableRecorder.Reset();
                }
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    TextInputLifecycleQaFixtureBuilder.PrefabPath);
                Assert.That(prefab, Is.Not.Null);
                AssertReceiverIsSilent(prefab!.GetComponent<TextInputLifecycleQaReceiver>());

                TextInputLifecycleQaStableRecorder.Arm();
                try
                {
                    opened = EditorSceneManager.OpenScene(
                        TextInputLifecycleQaFixtureBuilder.ScenePath,
                        OpenSceneMode.Additive);
                    AssertStableRecorderIsSilent("post-import Editor scene load");
                }
                finally
                {
                    TextInputLifecycleQaStableRecorder.Disarm();
                    TextInputLifecycleQaStableRecorder.Reset();
                }
                var persistentObject = FindRoot(opened, "InspectorPersistent");
                Assert.That(persistentObject, Is.Not.Null);
                var receiver = persistentObject!.GetComponent<TextInputLifecycleQaReceiver>();
                var input = persistentObject.GetComponent<TextInput>();
                AssertReceiverIsSilent(receiver);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(persistentObject),
                    Is.EqualTo(prefab));
                Assert.That(PrefabUtility.HasPrefabInstanceAnyOverrides(persistentObject, false), Is.True);

                input.Text = "explicit-after-scene-load";
                Assert.That(receiver.ValueChangedCount, Is.EqualTo(1));
                Assert.That(receiver.ValueChangedPayload, Is.EqualTo("explicit-after-scene-load"));
                Assert.That(receiver.EndEditCount, Is.Zero);
                Assert.That(receiver.FocusGainedCount, Is.Zero);
                Assert.That(receiver.FocusLostCount, Is.Zero);

                Assert.That(EditorSceneManager.CloseScene(opened, removeScene: true), Is.True);
                opened = default;
                TextInputLifecycleQaStableRecorder.Arm();
                try
                {
                    opened = EditorSceneManager.OpenScene(
                        TextInputLifecycleQaFixtureBuilder.ScenePath,
                        OpenSceneMode.Additive);
                    AssertStableRecorderIsSilent("Editor scene reopen");
                }
                finally
                {
                    TextInputLifecycleQaStableRecorder.Disarm();
                    TextInputLifecycleQaStableRecorder.Reset();
                }
                AssertReceiverIsSilent(FindRoot(opened, "InspectorPersistent")!
                    .GetComponent<TextInputLifecycleQaReceiver>());
            }
            finally
            {
                TextInputLifecycleQaStableRecorder.Disarm();
                TextInputLifecycleQaStableRecorder.Reset();
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
                TextInputLifecycleQaFixtureBuilder.DeleteGeneratedAssets();
            }
        }

        [Test]
        public void ProfilerLauncherRestoresBuildSettingsAndPlayModeStartSceneExactly()
        {
            using var testSceneGuard = TestRunnerSceneGuard.Enter();
            if (TextInputLifecycleQaProfilerLauncher.HasPendingRestore)
            {
                TextInputLifecycleQaProfilerLauncher.RestoreEditorState();
            }
            var originalScenes = EditorBuildSettings.scenes;
            var originalStartScene = EditorSceneManager.playModeStartScene;
            try
            {
                TextInputLifecycleQaFixtureBuilder.Generate(TestHead, TestTree);

                TextInputLifecycleQaProfilerLauncher.PrepareTemporaryEditorState();

                Assert.That(TextInputLifecycleQaProfilerLauncher.HasPendingRestore, Is.True);
                var bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    TextInputLifecycleQaFixtureBuilder.BootstrapScenePath);
                var target = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    TextInputLifecycleQaFixtureBuilder.ScenePath);
                Assert.That(bootstrap, Is.Not.Null);
                Assert.That(target, Is.Not.Null);
                Assert.That(EditorSceneManager.playModeStartScene, Is.EqualTo(bootstrap));
                AssertPreparedBuildScene(TextInputLifecycleQaFixtureBuilder.BootstrapScenePath);
                AssertPreparedBuildScene(TextInputLifecycleQaFixtureBuilder.ScenePath);

                TextInputLifecycleQaProfilerLauncher.RestoreEditorState();

                Assert.That(TextInputLifecycleQaProfilerLauncher.HasPendingRestore, Is.False);
                AssertBuildSettingsEqual(EditorBuildSettings.scenes, originalScenes);
                Assert.That(EditorSceneManager.playModeStartScene, Is.EqualTo(originalStartScene));
            }
            finally
            {
                if (TextInputLifecycleQaProfilerLauncher.HasPendingRestore)
                {
                    TextInputLifecycleQaProfilerLauncher.RestoreEditorState();
                }
                EditorBuildSettings.scenes = originalScenes;
                EditorSceneManager.playModeStartScene = originalStartScene;
                TextInputLifecycleQaFixtureBuilder.DeleteGeneratedAssets();
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

        private static void AssertReceiverIsSilent(TextInputLifecycleQaReceiver receiver)
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
            Assert.That(TextInputLifecycleQaStableRecorder.IsArmed, Is.True,
                $"Stable recorder was not armed for {phase}.");
            Assert.That(TextInputLifecycleQaStableRecorder.ValueChangedCount, Is.Zero,
                $"{phase} invoked onValueChanged on an imported or transient receiver.");
            Assert.That(TextInputLifecycleQaStableRecorder.EndEditCount, Is.Zero,
                $"{phase} invoked onEndEdit on an imported or transient receiver.");
            Assert.That(TextInputLifecycleQaStableRecorder.FocusGainedCount, Is.Zero,
                $"{phase} invoked onFocusGained on an imported or transient receiver.");
            Assert.That(TextInputLifecycleQaStableRecorder.FocusLostCount, Is.Zero,
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
                        "Task 159 EditMode QA only takes ownership of one untitled neutral scene.");
                }

                var scene = SceneManager.GetSceneAt(0);
                if (!IsUntitledNeutral(scene) ||
                    SceneManager.GetActiveScene().handle != scene.handle)
                {
                    throw new InvalidOperationException(
                        "Task 159 EditMode QA refuses saved scenes, roots, EventSystems, or " +
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

                try
                {
                    var guardHandle = scene.handle;
                    if (!EditorSceneManager.SaveScene(scene, guardPath) ||
                        !IsExactSavedGuard(scene, guardPath, guardHandle))
                    {
                        throw new InvalidOperationException(
                            $"Could not establish the exact test-owned scene guard: {guardPath}");
                    }
                    return new TestRunnerSceneGuard(tempRoot,
                        guardPath,
                        guardHandle,
                        originalDirty);
                }
                catch
                {
                    if (AssetDatabase.IsValidFolder(tempRoot))
                    {
                        AssetDatabase.DeleteAsset(tempRoot);
                        AssetDatabase.Refresh();
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
                            TextInputLifecycleQaFixtureBuilder.RootPath + "/",
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
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Milestro.Tests.TextInputLifecycle.Integration.Editor
{
    [InitializeOnLoad]
    public static class TextInputLifecycleIntegrationProfilerLauncher
    {
        private const int StateVersion = 1;
        private const double InterruptedTransitionGraceSeconds = 1d;
        private const string PendingStateKey =
            "Milestro.Tests.TextInputLifecycle.Integration.ProfilerLauncher.PendingState.v1";
        private static bool restoreFailureLogged;
        private static double recoverNotBefore;

        static TextInputLifecycleIntegrationProfilerLauncher()
        {
            DelayInterruptedTransitionRecovery();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= RecoverAfterInterruptedTransition;
            EditorApplication.update += RecoverAfterInterruptedTransition;
        }

        public static bool HasPendingRestore =>
            !string.IsNullOrEmpty(SessionState.GetString(PendingStateKey, string.Empty));

        [MenuItem("Milestro/Task 159/Profiler/Prepare Bootstrap And Enter Play Mode")]
        public static void PrepareBootstrapAndEnterPlayMode()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Task 159 Profiler Integration must be launched from a stable Edit Mode state.");
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                throw new InvalidOperationException(
                    "Wait for compilation and asset import to finish before launching Task 159 Profiler Integration.");
            }

            PrepareTemporaryEditorState();
            try
            {
                UpdatePhase("entering-play-mode");
                EditorApplication.isPlaying = true;
            }
            catch
            {
                RestoreEditorState();
                throw;
            }
        }

        public static void PrepareTemporaryEditorState()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Task 159 Profiler Integration settings cannot be prepared during a Play Mode transition.");
            }
            if (HasPendingRestore)
            {
                RestoreEditorState();
            }

            // Validation must precede the snapshot and all temporary Editor settings changes.
            TextInputLifecycleIntegrationFixtureBuilder.ValidateGeneratedAssets();
            var bootstrap = RequireSceneAsset(TextInputLifecycleIntegrationFixtureBuilder.BootstrapScenePath);
            RequireSceneAsset(TextInputLifecycleIntegrationFixtureBuilder.ScenePath);

            var state = CaptureState();
            SessionState.SetString(PendingStateKey, JsonUtility.ToJson(state));
            DelayInterruptedTransitionRecovery();
            try
            {
                EditorBuildSettings.scenes = BuildTemporaryScenes(state.scenes);
                EditorSceneManager.playModeStartScene = bootstrap;
                VerifyPreparedState();
            }
            catch
            {
                RestoreEditorState();
                throw;
            }
        }

        [MenuItem("Milestro/Task 159/Profiler/Restore Temporary Editor Settings")]
        public static void RestoreEditorState()
        {
            if (!HasPendingRestore)
            {
                return;
            }

            var state = ReadState();
            var restoredScenes = new EditorBuildSettingsScene[state.scenes.Length];
            for (var index = 0; index < state.scenes.Length; ++index)
            {
                restoredScenes[index] = new EditorBuildSettingsScene(state.scenes[index].path,
                    state.scenes[index].enabled);
            }
            EditorBuildSettings.scenes = restoredScenes;
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(state.playModeStartScenePath)
                ? null
                : RequireSceneAsset(state.playModeStartScenePath);
            VerifyRestoredState(state);
            SessionState.EraseString(PendingStateKey);
        }

        private static ProfilerLaunchState CaptureState()
        {
            var scenes = EditorBuildSettings.scenes;
            var snapshots = new BuildSceneSnapshot[scenes.Length];
            for (var index = 0; index < scenes.Length; ++index)
            {
                snapshots[index] = new BuildSceneSnapshot
                {
                    path = scenes[index].path ?? string.Empty,
                    enabled = scenes[index].enabled
                };
            }
            return new ProfilerLaunchState
            {
                version = StateVersion,
                phase = "prepared",
                scenes = snapshots,
                playModeStartScenePath = CurrentPlayModeStartScenePath()
            };
        }

        private static EditorBuildSettingsScene[] BuildTemporaryScenes(BuildSceneSnapshot[] original)
        {
            var scenes = new List<EditorBuildSettingsScene>(original.Length + 2);
            for (var index = 0; index < original.Length; ++index)
            {
                var path = original[index].path;
                if (path == TextInputLifecycleIntegrationFixtureBuilder.BootstrapScenePath ||
                    path == TextInputLifecycleIntegrationFixtureBuilder.ScenePath)
                {
                    continue;
                }
                scenes.Add(new EditorBuildSettingsScene(path, original[index].enabled));
            }
            scenes.Add(new EditorBuildSettingsScene(
                TextInputLifecycleIntegrationFixtureBuilder.BootstrapScenePath,
                enabled: true));
            scenes.Add(new EditorBuildSettingsScene(TextInputLifecycleIntegrationFixtureBuilder.ScenePath,
                enabled: true));
            return scenes.ToArray();
        }

        private static void VerifyPreparedState()
        {
            RequirePreparedScene(TextInputLifecycleIntegrationFixtureBuilder.BootstrapScenePath);
            RequirePreparedScene(TextInputLifecycleIntegrationFixtureBuilder.ScenePath);
            if (CurrentPlayModeStartScenePath() !=
                TextInputLifecycleIntegrationFixtureBuilder.BootstrapScenePath)
            {
                throw new InvalidOperationException(
                    "Task 159 Profiler Integration start scene is not the generated bootstrap scene.");
            }
        }

        private static void RequirePreparedScene(string expectedPath)
        {
            RequireSceneAsset(expectedPath);
            var count = 0;
            var enabled = false;
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.path != expectedPath)
                {
                    continue;
                }
                ++count;
                enabled = scene.enabled;
            }
            if (count != 1 || !enabled || SceneUtility.GetBuildIndexByScenePath(expectedPath) < 0)
            {
                throw new InvalidOperationException(
                    $"Task 159 Profiler Integration scene is not uniquely enabled and resolvable: {expectedPath}");
            }
        }

        private static void VerifyRestoredState(ProfilerLaunchState expected)
        {
            var actual = EditorBuildSettings.scenes;
            if (actual.Length != expected.scenes.Length)
            {
                throw new InvalidOperationException(
                    "Task 159 Profiler Integration did not restore the original Build Settings scene count.");
            }
            for (var index = 0; index < actual.Length; ++index)
            {
                if (actual[index].path != expected.scenes[index].path ||
                    actual[index].enabled != expected.scenes[index].enabled)
                {
                    throw new InvalidOperationException(
                        $"Task 159 Profiler Integration Build Settings restoration mismatch at index {index}.");
                }
            }
            if (CurrentPlayModeStartScenePath() != expected.playModeStartScenePath)
            {
                throw new InvalidOperationException(
                    "Task 159 Profiler Integration did not restore the original Play Mode start scene.");
            }
        }

        private static SceneAsset RequireSceneAsset(string path)
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (scene == null)
            {
                throw new InvalidOperationException($"Task 159 Integration scene is missing: {path}");
            }
            return scene;
        }

        private static string CurrentPlayModeStartScenePath()
        {
            var startScene = EditorSceneManager.playModeStartScene;
            return startScene == null ? string.Empty : AssetDatabase.GetAssetPath(startScene);
        }

        private static ProfilerLaunchState ReadState()
        {
            var json = SessionState.GetString(PendingStateKey, string.Empty);
            var state = JsonUtility.FromJson<ProfilerLaunchState>(json);
            if (state == null || state.version != StateVersion || state.scenes == null ||
                state.playModeStartScenePath == null)
            {
                throw new InvalidOperationException(
                    "Task 159 Profiler Integration pending Editor settings snapshot is invalid.");
            }
            return state;
        }

        private static void UpdatePhase(string phase)
        {
            if (!HasPendingRestore)
            {
                return;
            }
            var state = ReadState();
            state.phase = phase;
            SessionState.SetString(PendingStateKey, JsonUtility.ToJson(state));
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (!HasPendingRestore)
            {
                return;
            }
            try
            {
                switch (change)
                {
                    case PlayModeStateChange.ExitingEditMode:
                        UpdatePhase("exiting-edit-mode");
                        break;
                    case PlayModeStateChange.EnteredPlayMode:
                        UpdatePhase("playing");
                        break;
                    case PlayModeStateChange.ExitingPlayMode:
                        UpdatePhase("exiting-play-mode");
                        break;
                    case PlayModeStateChange.EnteredEditMode:
                        RestoreEditorState();
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void RecoverAfterInterruptedTransition()
        {
            if (!HasPendingRestore || EditorApplication.isPlaying ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.timeSinceStartup < recoverNotBefore)
            {
                return;
            }
            try
            {
                RestoreEditorState();
                restoreFailureLogged = false;
            }
            catch (Exception exception)
            {
                if (!restoreFailureLogged)
                {
                    Debug.LogException(exception);
                    restoreFailureLogged = true;
                }
            }
        }

        private static void DelayInterruptedTransitionRecovery()
        {
            recoverNotBefore = EditorApplication.timeSinceStartup +
                               InterruptedTransitionGraceSeconds;
        }

        [Serializable]
        private sealed class ProfilerLaunchState
        {
            public int version;
            public string phase = string.Empty;
            public BuildSceneSnapshot[] scenes = Array.Empty<BuildSceneSnapshot>();
            public string playModeStartScenePath = string.Empty;
        }

        [Serializable]
        private sealed class BuildSceneSnapshot
        {
            public string path = string.Empty;
            public bool enabled;
        }
    }
}

using System;
using Milestro.Components;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Milestro.Tests.TextInputLifecycle.Integration.Editor
{
    public static class TextInputLifecycleIntegrationFixtureBuilder
    {
        public const string RootPath = "Assets/__MilestroTask159Integration";
        public const string PrefabPath = RootPath + "/TextInputLifecycle.prefab";
        public const string ScenePath = RootPath + "/TextInputLifecycle.unity";
        public const string SceneLoadPath = RootPath + "/TextInputLifecycle";
        public const string BootstrapScenePath = RootPath + "/TextInputLifecycleBootstrap.unity";

        [MenuItem("Milestro/Task 159/Create Production Lifecycle Integration Fixtures")]
        public static void GenerateFromEnvironment()
        {
            Generate(RequiredObjectId("MILESTRO_TASK159_HEAD"),
                RequiredObjectId("MILESTRO_TASK159_TREE"));
            Debug.Log($"Task 159 Integration fixtures generated at {RootPath}.");
        }

        [MenuItem("Milestro/Task 159/Profiler/Run No-Listener Burst")]
        private static void RunNoListenerProfilerBurst()
        {
            RunProfilerBurst(TextInputLifecycleIntegrationProfilerCase.NoListener);
        }

        [MenuItem("Milestro/Task 159/Profiler/Run Runtime AddListener Burst")]
        private static void RunRuntimeProfilerBurst()
        {
            RunProfilerBurst(TextInputLifecycleIntegrationProfilerCase.RuntimeAddListener);
        }

        [MenuItem("Milestro/Task 159/Profiler/Run Inspector Persistent Burst")]
        private static void RunPersistentProfilerBurst()
        {
            RunProfilerBurst(TextInputLifecycleIntegrationProfilerCase.InspectorPersistent);
        }

        public static string Generate(string sourceHead, string sourceTree)
        {
            TextInputLifecycleIntegrationStableRecorder.Arm();
            string scenePath;
            try
            {
                scenePath = GenerateAssetsWithoutValidation(sourceHead, sourceTree);
                RequireStableRecorderSilence("initial asset generation/import");
            }
            finally
            {
                TextInputLifecycleIntegrationStableRecorder.Disarm();
                TextInputLifecycleIntegrationStableRecorder.Reset();
            }

            TextInputLifecycleIntegrationStableRecorder.Arm();
            try
            {
                ValidateGeneratedAssets();
                RequireStableRecorderSilence("first builder validation/scene open");
            }
            finally
            {
                TextInputLifecycleIntegrationStableRecorder.Disarm();
                TextInputLifecycleIntegrationStableRecorder.Reset();
            }
            return scenePath;
        }

        public static string GenerateAssetsWithoutValidation(string sourceHead, string sourceTree)
        {
            RequireObjectId(sourceHead, nameof(sourceHead));
            RequireObjectId(sourceTree, nameof(sourceTree));
            EnsureIntegrationSceneIsNotLoaded();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var restoreSavedSetup = ValidateInitialSceneSetup(setup);
            var generatedSuccessfully = false;
            try
            {
                if (AssetDatabase.IsValidFolder(RootPath) && !AssetDatabase.DeleteAsset(RootPath))
                {
                    throw new InvalidOperationException($"Could not remove existing Integration root {RootPath}.");
                }
                if (!AssetDatabase.IsValidFolder("Assets"))
                {
                    throw new InvalidOperationException("Unity project has no Assets root.");
                }
                AssetDatabase.CreateFolder("Assets", "__MilestroTask159Integration");

                var prefabRoot = CreateTextInputObject("Persistent TextInput");
                try
                {
                    var prefabInput = prefabRoot.GetComponent<TextInput>();
                    var prefabReceiver = prefabRoot.AddComponent<TextInputLifecycleIntegrationReceiver>();
                    BindPersistentListeners(prefabInput, prefabReceiver);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(prefabRoot);
                }

                var generated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);

                var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

                var eventSystemObject = new GameObject("EventSystem",
                    typeof(EventSystem),
                    typeof(TextInputLifecycleIntegrationInputModule));
                SceneManager.MoveGameObjectToScene(eventSystemObject, generated);

                var noListener = CreateTextInputObject("NoListener", canvasObject.transform);
                Position(noListener, 260f);
                var runtimeListener = CreateTextInputObject("RuntimeAddListener", canvasObject.transform);
                Position(runtimeListener, 0f);
                var runtimeInput = runtimeListener.GetComponent<TextInput>();
                var runtimeObserver = runtimeListener.AddComponent<TextInputLifecycleIntegrationRuntimeListener>();
                runtimeObserver.Configure(runtimeInput);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException("Generated Integration prefab could not be loaded.");
                }
                var persistentObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, generated);
                persistentObject.name = "InspectorPersistent";
                persistentObject.transform.SetParent(canvasObject.transform, false);
                Position(persistentObject, -260f);
                var persistentInput = persistentObject.GetComponent<TextInput>();
                var persistentReceiver = persistentObject.GetComponent<TextInputLifecycleIntegrationReceiver>();
                persistentInput.SetTextWithoutNotify("scene-prefab-override");
                PrefabUtility.RecordPrefabInstancePropertyModifications(persistentInput);

                var runnerObject = new GameObject("Task159 Integration Runner");
                SceneManager.MoveGameObjectToScene(runnerObject, generated);
                var runner = runnerObject.AddComponent<TextInputLifecycleIntegrationScenarioRunner>();
                runner.Configure(noListener.GetComponent<TextInput>(),
                    runtimeInput,
                    runtimeObserver,
                    persistentInput,
                    persistentReceiver,
                    sourceHead,
                    sourceTree);

                if (!EditorSceneManager.SaveScene(generated, ScenePath))
                {
                    throw new InvalidOperationException($"Could not save Integration scene {ScenePath}.");
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);

                var bootstrapScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                var bootstrapObject = new GameObject("Task159 Integration Bootstrap");
                SceneManager.MoveGameObjectToScene(bootstrapObject, bootstrapScene);
                var bootstrap = bootstrapObject.AddComponent<TextInputLifecycleIntegrationBootstrap>();
                bootstrap.Configure(SceneLoadPath, sourceHead, sourceTree);
                EditorUtility.SetDirty(bootstrap);
                if (!EditorSceneManager.SaveScene(bootstrapScene, BootstrapScenePath))
                {
                    throw new InvalidOperationException(
                        $"Could not save Integration bootstrap scene {BootstrapScenePath}.");
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(BootstrapScenePath, ImportAssetOptions.ForceUpdate);
                generatedSuccessfully = true;
            }
            finally
            {
                try
                {
                    RestoreSceneSetup(setup, restoreSavedSetup);
                }
                finally
                {
                    if (!generatedSuccessfully)
                    {
                        AssetDatabase.DeleteAsset(RootPath);
                    }
                }
            }

            return ScenePath;
        }

        public static void ValidateGeneratedAssets()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException("Integration prefab is missing.");
            }
            var input = prefab.GetComponent<TextInput>();
            var receiver = prefab.GetComponent<TextInputLifecycleIntegrationReceiver>();
            if (input == null || receiver == null)
            {
                throw new InvalidOperationException("Integration prefab is missing TextInput or receiver.");
            }
            ValidatePersistentEvent(input, "m_OnValueChanged", receiver, "OnValueChanged", 0);
            ValidatePersistentEvent(input, "m_OnEndEdit", receiver, "OnEndEdit", 0);
            ValidatePersistentEvent(input, "m_OnFocusGained", receiver, "OnFocusGained", 0);
            ValidatePersistentEvent(input, "m_OnFocusLost", receiver, "OnFocusLost", 0);

            var dependencies = AssetDatabase.GetDependencies(ScenePath, recursive: true);
            if (Array.IndexOf(dependencies, PrefabPath) < 0)
            {
                throw new InvalidOperationException("Integration scene does not depend on the generated prefab.");
            }

            Scene opened = default;
            try
            {
                opened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                var persistentObject = FindInScene(opened, "InspectorPersistent");
                if (persistentObject == null)
                {
                    throw new InvalidOperationException("Integration scene has no persistent prefab instance.");
                }
                var sceneInput = persistentObject.GetComponent<TextInput>();
                var sceneReceiver = persistentObject.GetComponent<TextInputLifecycleIntegrationReceiver>();
                if (sceneInput == null || sceneReceiver == null)
                {
                    throw new InvalidOperationException(
                        "Integration scene persistent instance is missing TextInput or receiver.");
                }
                ValidatePersistentEvent(sceneInput,
                    "m_OnValueChanged",
                    sceneReceiver,
                    "OnValueChanged",
                    0);
                ValidatePersistentEvent(sceneInput, "m_OnEndEdit", sceneReceiver, "OnEndEdit", 0);
                ValidatePersistentEvent(sceneInput,
                    "m_OnFocusGained",
                    sceneReceiver,
                    "OnFocusGained",
                    0);
                ValidatePersistentEvent(sceneInput, "m_OnFocusLost", sceneReceiver, "OnFocusLost", 0);
                if (PrefabUtility.GetCorrespondingObjectFromSource(persistentObject) != prefab ||
                    !PrefabUtility.HasPrefabInstanceAnyOverrides(persistentObject, false))
                {
                    throw new InvalidOperationException("Integration scene prefab source or override is missing.");
                }
                if (FindInScene(opened, "Task159 Integration Runner")?
                        .GetComponent<TextInputLifecycleIntegrationScenarioRunner>() == null)
                {
                    throw new InvalidOperationException("Integration scene runner is missing.");
                }
            }
            finally
            {
                if (opened.IsValid() && opened.isLoaded &&
                    !EditorSceneManager.CloseScene(opened, removeScene: true))
                {
                    throw new InvalidOperationException("Could not close validated Integration scene.");
                }
            }

            ValidateBootstrapScene();
        }

        private static void ValidateBootstrapScene()
        {
            Scene opened = default;
            try
            {
                opened = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Additive);
                var bootstrapObject = FindInScene(opened, "Task159 Integration Bootstrap");
                var bootstrap = bootstrapObject?.GetComponent<TextInputLifecycleIntegrationBootstrap>();
                if (bootstrap == null || bootstrap.TargetScenePath != SceneLoadPath ||
                    bootstrap.SourceHead.Length != 40 || bootstrap.SourceTree.Length != 40)
                {
                    throw new InvalidOperationException(
                        "Integration bootstrap scene target loader is missing or misconfigured.");
                }
                foreach (var root in opened.GetRootGameObjects())
                {
                    if (root.GetComponentInChildren<TextInput>(true) != null)
                    {
                        throw new InvalidOperationException(
                            "Integration bootstrap scene must not contain a TextInput.");
                    }
                }
            }
            finally
            {
                if (opened.IsValid() && opened.isLoaded &&
                    !EditorSceneManager.CloseScene(opened, removeScene: true))
                {
                    throw new InvalidOperationException("Could not close validated Integration bootstrap scene.");
                }
            }
        }

        public static void DeleteGeneratedAssets()
        {
            EnsureIntegrationSceneIsNotLoaded();
            if (AssetDatabase.IsValidFolder(RootPath) && !AssetDatabase.DeleteAsset(RootPath))
            {
                throw new InvalidOperationException($"Could not remove Integration root {RootPath}.");
            }
            AssetDatabase.Refresh();
        }

        private static void RunProfilerBurst(TextInputLifecycleIntegrationProfilerCase profilerCase)
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Enter Play Mode and wait for TASK159_INTEGRATION_RESULT PASS first.");
            }
#if UNITY_2023_1_OR_NEWER
            var runner = UnityEngine.Object.FindFirstObjectByType<TextInputLifecycleIntegrationScenarioRunner>();
#else
            var runner = UnityEngine.Object.FindObjectOfType<TextInputLifecycleIntegrationScenarioRunner>();
#endif
            if (runner == null || !runner.StartProfilerBurst(profilerCase))
            {
                throw new InvalidOperationException(
                    $"Could not start {profilerCase} profiler burst; wait for the Integration scenario to finish.");
            }
        }

        private static GameObject CreateTextInputObject(string name, Transform? parent = null)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }
            var rect = (RectTransform)gameObject.transform;
            rect.sizeDelta = new Vector2(900f, 180f);
            gameObject.AddComponent<TextInput>();
            return gameObject;
        }

        private static void Position(GameObject gameObject, float y)
        {
            ((RectTransform)gameObject.transform).anchoredPosition = new Vector2(0f, y);
        }

        private static GameObject? FindInScene(Scene scene, string name)
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

        private static void BindPersistentListeners(TextInput input, TextInputLifecycleIntegrationReceiver receiver)
        {
            UnityEventTools.AddPersistentListener(input.onValueChanged, receiver.OnValueChanged);
            UnityEventTools.AddPersistentListener(input.onEndEdit, receiver.OnEndEdit);
            UnityEventTools.AddPersistentListener(input.onFocusGained, receiver.OnFocusGained);
            UnityEventTools.AddPersistentListener(input.onFocusLost, receiver.OnFocusLost);
            EditorUtility.SetDirty(input);
        }

        private static void RequireStableRecorderSilence(string phase)
        {
            if (!TextInputLifecycleIntegrationStableRecorder.IsArmed ||
                TextInputLifecycleIntegrationStableRecorder.ValueChangedCount != 0 ||
                TextInputLifecycleIntegrationStableRecorder.EndEditCount != 0 ||
                TextInputLifecycleIntegrationStableRecorder.FocusGainedCount != 0 ||
                TextInputLifecycleIntegrationStableRecorder.FocusLostCount != 0)
            {
                throw new InvalidOperationException(
                    $"Task 159 Integration lifecycle notification occurred during {phase}.");
            }
        }

        private static void ValidatePersistentEvent(TextInput input,
            string fieldName,
            TextInputLifecycleIntegrationReceiver receiver,
            string methodName,
            int expectedMode)
        {
            var serialized = new SerializedObject(input);
            var calls = serialized.FindProperty(fieldName + ".m_PersistentCalls.m_Calls");
            if (calls == null || !calls.isArray || calls.arraySize != 1)
            {
                throw new InvalidOperationException($"{fieldName} does not have exactly one persistent call.");
            }
            var call = calls.GetArrayElementAtIndex(0);
            var target = call.FindPropertyRelative("m_Target").objectReferenceValue;
            var method = call.FindPropertyRelative("m_MethodName").stringValue;
            var mode = call.FindPropertyRelative("m_Mode").enumValueIndex;
            if (target != receiver || method != methodName || mode != expectedMode)
            {
                throw new InvalidOperationException(
                    $"{fieldName} persistent binding mismatch: target={target}, method={method}, mode={mode}.");
            }
            if (fieldName == "m_OnFocusGained" || fieldName == "m_OnFocusLost")
            {
                var receiverMethod = receiver.GetType().GetMethod(methodName);
                if (receiverMethod == null || receiverMethod.ReturnType != typeof(void) ||
                    receiverMethod.GetParameters().Length != 0)
                {
                    throw new InvalidOperationException(
                        $"{fieldName} persistent receiver must be a void zero-parameter method: {methodName}.");
                }
            }
        }

        private static bool ValidateInitialSceneSetup(SceneSetup[] setup)
        {
            var hasUnsaved = false;
            for (var index = 0; index < SceneManager.sceneCount; ++index)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                {
                    throw new InvalidOperationException(
                        $"Save or discard dirty scene changes before generating Integration fixtures: {scene.path}");
                }
                if (string.IsNullOrEmpty(scene.path))
                {
                    hasUnsaved = true;
                    if (scene.GetRootGameObjects().Length != 0)
                    {
                        throw new InvalidOperationException(
                            "The only supported unsaved setup is a clean empty scene.");
                    }
                }
            }
            if (hasUnsaved && SceneManager.sceneCount != 1)
            {
                throw new InvalidOperationException(
                    "An unsaved Integration entry scene cannot be mixed with other loaded scenes.");
            }
            return setup.Length > 0 && !hasUnsaved;
        }

        private static void RestoreSceneSetup(SceneSetup[] setup, bool restoreSavedSetup)
        {
            if (restoreSavedSetup)
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            else
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void EnsureIntegrationSceneIsNotLoaded()
        {
            for (var index = 0; index < SceneManager.sceneCount; ++index)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.path.StartsWith(RootPath + "/", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Close the loaded Integration scene before regenerating or deleting assets: {scene.path}");
                }
            }
        }

        private static string RequiredObjectId(string environmentName)
        {
            var value = Environment.GetEnvironmentVariable(environmentName) ?? string.Empty;
            RequireObjectId(value, environmentName);
            return value;
        }

        private static void RequireObjectId(string value, string label)
        {
            if (value.Length != 40)
            {
                throw new ArgumentException($"{label} must be a 40-character Git object ID.");
            }
            for (var index = 0; index < value.Length; ++index)
            {
                var character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    throw new ArgumentException($"{label} must be a lowercase hexadecimal Git object ID.");
                }
            }
        }
    }
}

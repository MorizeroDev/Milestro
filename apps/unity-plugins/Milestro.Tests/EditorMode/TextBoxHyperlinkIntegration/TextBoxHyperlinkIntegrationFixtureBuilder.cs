using System;
using System.Linq;
using Milestro.Components;
using Milestro.Components.Internal;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Milestro.Tests.TextBoxHyperlinkIntegration.Editor
{
    public static class TextBoxHyperlinkIntegrationFixtureBuilder
    {
        public const string RootPath = "Assets/__MilestroTask201HyperlinkIntegration";
        public const string PrefabPath = RootPath + "/TextBoxHyperlink.prefab";
        public const string ScenePath = RootPath + "/TextBoxHyperlink.unity";

        [MenuItem("Milestro/Task 201/Create Hyperlink Integration Fixtures")]
        public static void GenerateFromEnvironment()
        {
            Generate(RequiredObjectId("MILESTRO_TASK201_HEAD"), RequiredObjectId("MILESTRO_TASK201_TREE"));
            Debug.Log($"Task 201 hyperlink fixtures generated and reopened at {RootPath}.");
        }

        [MenuItem("Milestro/Task 201/Open Hyperlink Fixture in Play Mode")]
        public static void OpenInPlayMode()
        {
            GenerateFromEnvironment();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        public static void Generate(string sourceHead, string sourceTree)
        {
            RequireObjectId(sourceHead, nameof(sourceHead));
            RequireObjectId(sourceTree, nameof(sourceTree));
            EnsureFixtureSceneIsNotLoaded();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            ValidateSceneSetup();
            try
            {
                if (AssetDatabase.IsValidFolder(RootPath) && !AssetDatabase.DeleteAsset(RootPath))
                {
                    throw new InvalidOperationException($"Could not remove existing fixture root {RootPath}.");
                }

                AssetDatabase.CreateFolder("Assets", "__MilestroTask201HyperlinkIntegration");
                CreatePrefab();
                CreateScene(sourceHead, sourceTree);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport |
                                                       ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport |
                                                      ImportAssetOptions.ForceUpdate);
                ValidateGeneratedAssets();
            }
            finally
            {
                RestoreSceneSetup(setup);
            }
        }

        public static void ValidateGeneratedAssets()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException("Generated hyperlink prefab is missing.");
            }

            ValidatePersistentBinding(prefab.GetComponent<TextBox>(),
                prefab.GetComponent<TextBoxHyperlinkIntegrationReceiver>());
            var dependencies = AssetDatabase.GetDependencies(ScenePath, recursive: true);
            if (!dependencies.Contains(PrefabPath))
            {
                throw new InvalidOperationException("Generated scene does not depend on the hyperlink prefab.");
            }

            Scene opened = default;
            var closeAfterValidation = false;
            try
            {
                for (var index = 0; index < SceneManager.sceneCount; ++index)
                {
                    var loaded = SceneManager.GetSceneAt(index);
                    if (loaded.path == ScenePath)
                    {
                        opened = loaded;
                        break;
                    }
                }
                if (!opened.IsValid())
                {
                    opened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                    closeAfterValidation = true;
                }
                var instance = FindInScene(opened, "Persistent Hyperlink TextBox");
                if (instance == null)
                {
                    throw new InvalidOperationException("Reopened scene has no hyperlink prefab instance.");
                }

                ValidatePersistentBinding(instance.GetComponent<TextBox>(),
                    instance.GetComponent<TextBoxHyperlinkIntegrationReceiver>());
                if (PrefabUtility.GetCorrespondingObjectFromSource(instance) != prefab)
                {
                    throw new InvalidOperationException("Reopened scene lost its hyperlink prefab source.");
                }

                if (FindInScene(opened, "Task201 Hyperlink Integration Runner")?
                        .GetComponent<TextBoxHyperlinkIntegrationScenarioRunner>() == null)
                {
                    throw new InvalidOperationException("Reopened scene has no hyperlink scenario runner.");
                }
                if (FindInScene(opened, "EventSystem")?.GetComponent<StandaloneInputModule>() == null)
                {
                    throw new InvalidOperationException("Reopened scene has no physical mouse input module.");
                }
            }
            finally
            {
                if (closeAfterValidation && opened.IsValid() && opened.isLoaded &&
                    !EditorSceneManager.CloseScene(opened, removeScene: true))
                {
                    throw new InvalidOperationException(
                        "Could not close the validated hyperlink scene: " +
                        $"active={SceneManager.GetActiveScene().handle == opened.handle}, " +
                        $"dirty={opened.isDirty}, handle={opened.handle}, " +
                        $"sceneCount={SceneManager.sceneCount}.");
                }
            }
        }

        public static void DeleteGeneratedAssets()
        {
            EnsureFixtureSceneIsNotLoaded();
            if (AssetDatabase.IsValidFolder(RootPath) && !AssetDatabase.DeleteAsset(RootPath))
            {
                throw new InvalidOperationException($"Could not remove fixture root {RootPath}.");
            }
            AssetDatabase.Refresh();
        }

        private static void CreatePrefab()
        {
            var root = CreateTextBoxObject();
            try
            {
                var textBox = root.GetComponent<TextBox>();
                var receiver = root.GetComponent<TextBoxHyperlinkIntegrationReceiver>();
                UnityEventTools.AddPersistentListener(textBox.onLinkClicked, receiver.OnLinkClicked);
                textBox.onLinkClicked.SetPersistentListenerState(0,
                    UnityEventCallState.EditorAndRuntime);
                EditorUtility.SetDirty(textBox);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                {
                    throw new InvalidOperationException("Could not save the hyperlink prefab.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateScene(string sourceHead, string sourceTree)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            SceneManager.MoveGameObjectToScene(canvasObject, scene);

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystemObject, scene);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException("Could not reload the hyperlink prefab before scene creation.");
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "Persistent Hyperlink TextBox";
            instance.transform.SetParent(canvasObject.transform, false);
            ConfigureRect(instance.GetComponent<RectTransform>());

            var runnerObject = new GameObject("Task201 Hyperlink Integration Runner");
            SceneManager.MoveGameObjectToScene(runnerObject, scene);
            runnerObject.AddComponent<TextBoxHyperlinkIntegrationScenarioRunner>().Configure(
                instance.GetComponent<TextBox>(),
                instance.GetComponent<TextBoxHyperlinkIntegrationReceiver>(),
                sourceHead,
                sourceTree);
            EditorUtility.SetDirty(runnerObject);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Could not save hyperlink scene {ScenePath}.");
            }
        }

        private static GameObject CreateTextBoxObject()
        {
            var root = new GameObject("Persistent Hyperlink TextBox", typeof(RectTransform));
            root.SetActive(false);
            var producer = root.AddComponent<TextBoxRenderTextureProducer>();
            producer.content =
                "<a href=\"milestro://integration/task201\" id=\"task201-link\">Open integration link</a>";
            producer.size = 36f;
            producer.textAlign = Milestro.Model.TextAlign.Left;
            producer.textDirection = Milestro.Model.TextDirection.Ltr;
            var textBox = root.AddComponent<TextBox>();
            textBox.raycastTarget = true;
            root.AddComponent<TextBoxHyperlinkIntegrationReceiver>();
            ConfigureRect(root.GetComponent<RectTransform>());
            root.SetActive(true);
            return root;
        }

        private static void ConfigureRect(RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(520f, 120f);
            rectTransform.anchoredPosition = Vector2.zero;
        }

        private static void ValidatePersistentBinding(TextBox? textBox,
            TextBoxHyperlinkIntegrationReceiver? receiver)
        {
            if (textBox == null || receiver == null)
            {
                throw new InvalidOperationException("Hyperlink fixture is missing TextBox or receiver.");
            }

            var serialized = new SerializedObject(textBox);
            var calls = serialized.FindProperty("m_OnLinkClicked.m_PersistentCalls.m_Calls");
            if (calls == null || !calls.isArray || calls.arraySize != 1)
            {
                throw new InvalidOperationException("Hyperlink event does not have exactly one persistent call.");
            }

            var call = calls.GetArrayElementAtIndex(0);
            var target = call.FindPropertyRelative("m_Target").objectReferenceValue;
            var methodName = call.FindPropertyRelative("m_MethodName").stringValue;
            var mode = call.FindPropertyRelative("m_Mode").enumValueIndex;
            var callState = call.FindPropertyRelative("m_CallState").enumValueIndex;
            var method = receiver.GetType().GetMethod(methodName);
            if (target != receiver ||
                methodName != nameof(TextBoxHyperlinkIntegrationReceiver.OnLinkClicked) ||
                mode != 0 ||
                callState != (int)UnityEventCallState.EditorAndRuntime ||
                method == null ||
                method.ReturnType != typeof(void) ||
                method.GetParameters().Length != 1 ||
                method.GetParameters()[0].ParameterType != typeof(LinkClickedEventArgs))
            {
                throw new InvalidOperationException(
                    "Hyperlink persistent binding mismatch: " +
                    $"target={target}, method={methodName}, mode={mode}, callState={callState}.");
            }
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

        private static void ValidateSceneSetup()
        {
            for (var index = 0; index < SceneManager.sceneCount; ++index)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                {
                    throw new InvalidOperationException(
                        $"Save or discard dirty scene changes before generating fixtures: {scene.path}");
                }
            }
        }

        private static void RestoreSceneSetup(SceneSetup[] setup)
        {
            if (setup.Length > 0 && setup.All(entry => !string.IsNullOrEmpty(entry.path)))
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
                return;
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void EnsureFixtureSceneIsNotLoaded()
        {
            for (var index = 0; index < SceneManager.sceneCount; ++index)
            {
                if (SceneManager.GetSceneAt(index).path.StartsWith(RootPath + "/", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Close the generated hyperlink fixture scene first.");
                }
            }
        }

        private static string RequiredObjectId(string variableName)
        {
            var value = Environment.GetEnvironmentVariable(variableName) ?? string.Empty;
            RequireObjectId(value, variableName);
            return value;
        }

        private static void RequireObjectId(string value, string name)
        {
            if (value.Length != 40 || value.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new ArgumentException($"{name} must be an exact 40-character Git object ID.", name);
            }
        }
    }
}

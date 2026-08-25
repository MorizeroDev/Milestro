using System;
using Milestro.Components;
using Milestro.Tests.TextBoxHyperlinkIntegration.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Milestro.Tests.TextBoxHyperlinkIntegration.Tests.EditMode
{
    public class TextBoxHyperlinkPersistentAssetTests
    {
        private const string SourceHead = "1111111111111111111111111111111111111111";
        private const string SourceTree = "2222222222222222222222222222222222222222";
        private string testRunnerGuardRoot = string.Empty;
        private string testRunnerGuardScene = string.Empty;
        private bool originalSceneDirty;

        [OneTimeSetUp]
        public void CreateDirtyTestRunnerBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.MarkSceneDirty(scene);

            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(scene.handle));
            Assert.That(scene.path, Is.Empty);
            Assert.That(scene.isDirty, Is.True);
            Assert.That(scene.GetRootGameObjects(), Is.Empty);

            originalSceneDirty = scene.isDirty;
            testRunnerGuardRoot = "Assets/__MilestroTask201TestRunnerSceneGuard_" +
                                  Guid.NewGuid().ToString("N");
            testRunnerGuardScene = testRunnerGuardRoot + "/Guard.unity";
            var folderGuid = AssetDatabase.CreateFolder("Assets",
                testRunnerGuardRoot.Substring("Assets/".Length));
            Assert.That(folderGuid, Is.Not.Empty);
            Assert.That(AssetDatabase.IsValidFolder(testRunnerGuardRoot), Is.True);
            Assert.That(EditorSceneManager.SaveScene(scene, testRunnerGuardScene), Is.True);
            Assert.That(scene.path, Is.EqualTo(testRunnerGuardScene));
            Assert.That(scene.isDirty, Is.False);
        }

        [OneTimeTearDown]
        public void RestoreDirtyTestRunnerBootstrapScene()
        {
            if (string.IsNullOrEmpty(testRunnerGuardRoot))
            {
                return;
            }

            Assert.That(SceneManager.sceneCount, Is.EqualTo(1),
                "Refusing to discard an unexpected scene during Test Runner guard cleanup.");
            var guard = SceneManager.GetSceneAt(0);
            Assert.That(guard.IsValid(), Is.True);
            Assert.That(guard.isLoaded, Is.True);
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(guard.handle));
            Assert.That(guard.path, Is.EqualTo(testRunnerGuardScene));
            Assert.That(guard.isDirty, Is.False);
            Assert.That(guard.GetRootGameObjects(), Is.Empty);

            var restored = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (originalSceneDirty)
            {
                EditorSceneManager.MarkSceneDirty(restored);
            }
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(restored.handle));
            Assert.That(restored.path, Is.Empty);
            Assert.That(restored.isDirty, Is.EqualTo(originalSceneDirty));
            Assert.That(restored.GetRootGameObjects(), Is.Empty);

            Assert.That(AssetDatabase.DeleteAsset(testRunnerGuardRoot), Is.True);
            AssetDatabase.Refresh();
            Assert.That(AssetDatabase.IsValidFolder(testRunnerGuardRoot), Is.False);
        }

        [Test]
        public void DynamicClassListenerSurvivesPrefabAndSceneReimport()
        {
            TextBoxHyperlinkIntegrationFixtureBuilder.Generate(SourceHead, SourceTree);
            try
            {
                AssertPersistentInvocation(AssetDatabase.LoadAssetAtPath<GameObject>(
                    TextBoxHyperlinkIntegrationFixtureBuilder.PrefabPath));

                var scene = EditorSceneManager.OpenScene(TextBoxHyperlinkIntegrationFixtureBuilder.ScenePath,
                    OpenSceneMode.Additive);
                try
                {
                    var instance = FindInScene(scene, "Persistent Hyperlink TextBox");
                    AssertPersistentInvocation(instance);
                }
                finally
                {
                    Assert.That(EditorSceneManager.CloseScene(scene, removeScene: true), Is.True);
                }
            }
            finally
            {
                TextBoxHyperlinkIntegrationFixtureBuilder.DeleteGeneratedAssets();
            }
        }

        private static void AssertPersistentInvocation(GameObject? owner)
        {
            Assert.That(owner, Is.Not.Null);
            var textBox = owner!.GetComponent<TextBox>();
            var receiver = owner.GetComponent<TextBoxHyperlinkIntegrationReceiver>();
            Assert.That(textBox, Is.Not.Null);
            Assert.That(receiver, Is.Not.Null);

            receiver.ResetRecords();
            textBox.onLinkClicked.Invoke(new LinkClickedEventArgs("persisted-href", "persisted-id"));
            Assert.That(receiver.Count, Is.EqualTo(1));
            Assert.That(receiver.LastHref, Is.EqualTo("persisted-href"));
            Assert.That(receiver.LastId, Is.EqualTo("persisted-id"));
        }

        private static GameObject? FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
                foreach (var transform in transforms)
                {
                    if (transform.name == name)
                    {
                        return transform.gameObject;
                    }
                }
            }
            return null;
        }
    }
}

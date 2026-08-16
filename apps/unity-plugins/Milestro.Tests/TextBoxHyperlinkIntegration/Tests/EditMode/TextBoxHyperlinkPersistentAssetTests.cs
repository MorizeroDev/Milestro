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

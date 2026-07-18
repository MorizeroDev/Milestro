using Milestro.Components;
using Milestro.TextInputLifecycleQA.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
    }
}

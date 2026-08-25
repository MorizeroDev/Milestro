using System.Collections;
using Milestro.Components;
using Milestro.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Milestro.Tests.TextInputLifecycle.Integration.PlayMode
{
    public class TextInputLifecycleFacadePlayModeTests
    {
        [UnityTest]
        public IEnumerator RealDispatcherCoversNoListenerRuntimePersistentExceptionAndRecovery()
        {
            TextInputLifecycleIntegrationStableRecorder.ArmForTargetScene(
                "1590000000000000000000000000000000000001",
                "1590000000000000000000000000000000000002");
            var eventSystemObject = new GameObject("Task159 EventSystem",
                typeof(EventSystem),
                typeof(TextInputLifecycleIntegrationInputModule));
            var root = new GameObject("Task159 Scenario Root");
            root.SetActive(false);
            try
            {
                var noListener = CreateInput("NoListener", root.transform);
                var runtimeListener = CreateInput("RuntimeAddListener", root.transform);
                var runtimeObserver = runtimeListener.gameObject
                    .AddComponent<TextInputLifecycleIntegrationRuntimeListener>();
                runtimeObserver.Configure(runtimeListener);
                var persistentListener = CreateInput("PersistentListener", root.transform);
                var receiver = persistentListener.gameObject.AddComponent<TextInputLifecycleIntegrationReceiver>();
                persistentListener.onValueChanged.AddListener(receiver.OnValueChanged);
                persistentListener.onEndEdit.AddListener(receiver.OnEndEdit);
                persistentListener.onFocusGained.AddListener(receiver.OnFocusGained);
                persistentListener.onFocusLost.AddListener(receiver.OnFocusLost);

                var runner = root.AddComponent<TextInputLifecycleIntegrationScenarioRunner>();
                runner.Configure(noListener,
                    runtimeListener,
                    runtimeObserver,
                    persistentListener,
                    receiver,
                    "1590000000000000000000000000000000000001",
                    "1590000000000000000000000000000000000002");
                root.SetActive(true);

                for (var frame = 0; frame < 900 && !runner.Completed; ++frame)
                {
                    yield return null;
                }

                Assert.That(runner.Completed, Is.True);
                Assert.That(runner.Result, Is.Not.Null);
                Assert.That(runner.Result!.status, Is.EqualTo("PASS"), runner.Result.message);
                Assert.That(runner.Result.initialSceneLoadPassed, Is.True);
                Assert.That(runner.Result.initialSceneLoadRuntimeListenerPassed, Is.True);
                Assert.That(runner.Result.initialSceneLoadPersistentReceiverPassed, Is.True);
                Assert.That(runner.Result.initialSceneLoadStableRecorderPassed, Is.True);
                Assert.That(runner.Result.targetSceneInitialLoadPassed, Is.True);
                Assert.That(runner.Result.exceptionCasesPassed, Is.EqualTo(4));
                Assert.That(runner.Result.exceptionRecoveriesPassed, Is.EqualTo(4));
            }
            finally
            {
                TextInputLifecycleIntegrationStableRecorder.Disarm();
                TextInputLifecycleIntegrationStableRecorder.Reset();
                HybridInputRuntime.SetProviderOverride(null);
                Object.Destroy(root);
                Object.Destroy(eventSystemObject);
            }
            yield return null;
        }

        private static TextInput CreateInput(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            gameObject.transform.SetParent(parent, false);
            return gameObject.AddComponent<TextInput>();
        }
    }
}

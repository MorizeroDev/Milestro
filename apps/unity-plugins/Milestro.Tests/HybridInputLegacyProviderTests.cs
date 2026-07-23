#if ENABLE_LEGACY_INPUT_MANAGER
using System.Collections.Generic;
using Milestro.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Milestro.Tests
{
    public class HybridInputLegacyProviderTests
    {
        [Test]
        public void MatchesOnlyOneActiveStandaloneInputModule()
        {
            var gameObject = new GameObject();
            try
            {
                var module = gameObject.AddComponent<StandaloneInputModule>();
                var derivedModule = gameObject.AddComponent<DerivedStandaloneInputModule>();
                var provider = new HybridInputLegacyProvider(new FakeLegacyInputSource());

                Assert.That(provider.Match(new HybridInputEnvironment(module, 1, true)),
                    Is.EqualTo(HybridInputProviderMatch.Exact));
                Assert.That(provider.Match(new HybridInputEnvironment(module, 2, true)),
                    Is.EqualTo(HybridInputProviderMatch.None));
                Assert.That(provider.Match(new HybridInputEnvironment(null, 1, true)),
                    Is.EqualTo(HybridInputProviderMatch.None));
                Assert.That(provider.Match(new HybridInputEnvironment(derivedModule, 1, true)),
                    Is.EqualTo(HybridInputProviderMatch.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CollectPublishesEdgesTextCompositionAndHeldSnapshot()
        {
            var source = new FakeLegacyInputSource
            {
                InputString = "text",
                CompositionString = "composition"
            };
            source.PressedKeys.Add(KeyCode.LeftShift);
            source.DownKeys.Add(KeyCode.A);
            source.UpKeys.Add(KeyCode.C);
            var eventSink = new CapturingEventSink();
            var provider = new HybridInputLegacyProvider(source);
            provider.Start(eventSink);

            provider.Collect(new HybridInputCollectContext(3, 4d));

            Assert.That(eventSink.PressedKeys, Is.EqualTo(new[] { KeyCode.LeftShift }));
            Assert.That(eventSink.Events, Has.Count.EqualTo(4));
            Assert.That(eventSink.Events[0].Kind, Is.EqualTo(HybridInputEventKind.KeyState));
            Assert.That(eventSink.Events[0].Key, Is.EqualTo(KeyCode.A));
            Assert.That(eventSink.Events[0].KeyPressed, Is.True);
            Assert.That(eventSink.Events[1].Key, Is.EqualTo(KeyCode.C));
            Assert.That(eventSink.Events[1].KeyPressed, Is.False);
            Assert.That(eventSink.Events[2].Kind, Is.EqualTo(HybridInputEventKind.CommittedText));
            Assert.That(eventSink.Events[2].Text, Is.EqualTo("text"));
            Assert.That(eventSink.Events[3].Kind, Is.EqualTo(HybridInputEventKind.Composition));
            Assert.That(eventSink.Events[3].Text, Is.EqualTo("composition"));
            Assert.That(eventSink.Events[3].Timestamp, Is.EqualTo(4d));
        }

        [Test]
        public void CompositionOnlyPublishesWhenItChanges()
        {
            var source = new FakeLegacyInputSource { CompositionString = "same" };
            var eventSink = new CapturingEventSink();
            var provider = new HybridInputLegacyProvider(source);
            provider.Start(eventSink);

            provider.Collect(new HybridInputCollectContext(1, 1d));
            provider.Collect(new HybridInputCollectContext(2, 2d));
            source.CompositionString = string.Empty;
            provider.Collect(new HybridInputCollectContext(3, 3d));

            Assert.That(eventSink.Events, Has.Count.EqualTo(2));
            Assert.That(eventSink.Events[0].Text, Is.EqualTo("same"));
            Assert.That(eventSink.Events[1].Text, Is.Empty);
        }

        [Test]
        public void ImeCommandsAreForwardedAndStopDoesNotWriteGlobalIme()
        {
            var source = new FakeLegacyInputSource();
            var provider = new HybridInputLegacyProvider(source);
            provider.Start(new CapturingEventSink());

            provider.SetImeEnabled(true);
            provider.SetImeCursorPosition(new Vector2(4f, 8f));
            provider.Stop();

            Assert.That(source.ImeEnabled, Is.True);
            Assert.That(source.ImeEnabledCalls, Is.EqualTo(1));
            Assert.That(source.ImeCursorPosition, Is.EqualTo(new Vector2(4f, 8f)));
        }

        [Test]
        public void CapabilitiesDoNotClaimScrollLifecycle()
        {
            var provider = new HybridInputLegacyProvider(new FakeLegacyInputSource());

            Assert.That(provider.Kind, Is.EqualTo(HybridInputProviderKind.Legacy));
            Assert.That(provider.ScrollCapability, Is.EqualTo(HybridScrollCapability.Unsupported));
            Assert.That((provider.Capabilities & HybridInputCapabilities.ScrollPhase) == 0, Is.True);
        }

#if ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER && !MILESTRO_INPUT_SYSTEM_SUPPORTED
        [Test]
        public void UnsupportedInputSystemInBothUsesOneLegacyDeltaOnlyRoute()
        {
            var gameObject = new GameObject();
            try
            {
                var module = gameObject.AddComponent<
                    UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                var provider = new HybridInputLegacyProvider(new FakeLegacyInputSource());
                var dispatcher = new HybridInputDispatcher();
                dispatcher.RegisterProvider(provider);
                dispatcher.RefreshEnvironment(new HybridInputEnvironment(module, 1, true));
                dispatcher.Drain(1, 1d);

                var diagnostics = dispatcher.Diagnostics;
                Assert.That(diagnostics.SelectionStatus,
                    Is.EqualTo(HybridInputSelectionStatus.Selected));
                Assert.That(diagnostics.ProviderId, Is.EqualTo(HybridInputLegacyProvider.ProviderId));
                Assert.That(diagnostics.ProviderKind, Is.EqualTo(HybridInputProviderKind.Legacy));
                Assert.That(diagnostics.ScrollCapability,
                    Is.EqualTo(HybridScrollCapability.DeltaOnly));
                Assert.That((diagnostics.Capabilities & HybridInputCapabilities.ScrollDelta) != 0,
                    Is.True);
                Assert.That(provider, Is.Not.InstanceOf<IHybridInputFocusSessionProvider>());

                var eventData = new PointerEventData(null!)
                {
                    scrollDelta = new Vector2(1.25f, -2.5f)
                };
                var resolved = dispatcher.ResolveScrollInput(eventData);

                Assert.That(resolved.Delta, Is.EqualTo(eventData.scrollDelta));
                Assert.That(resolved.Metadata.Capability,
                    Is.EqualTo(HybridScrollCapability.DeltaOnly));
                Assert.That(resolved.Metadata.DeviceKind,
                    Is.EqualTo(HybridInputDeviceKind.Unknown));
                Assert.That(resolved.Metadata.GesturePhase,
                    Is.EqualTo(HybridInputPhase.Unknown));
                Assert.That(resolved.Metadata.MomentumPhase,
                    Is.EqualTo(HybridInputPhase.Unknown));
                Assert.That(resolved.Metadata.GestureId, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void UnsupportedInputSystemFallbackKeepsExistingFailClosedBoundaries()
        {
            var gameObject = new GameObject();
            try
            {
                var module = gameObject.AddComponent<
                    UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                var derivedModule = gameObject.AddComponent<DerivedInputSystemUIInputModule>();
                var standaloneModule = gameObject.AddComponent<StandaloneInputModule>();
                var provider = new HybridInputLegacyProvider(new FakeLegacyInputSource());

                Assert.That(provider.Match(new HybridInputEnvironment(module, 2, true)),
                    Is.EqualTo(HybridInputProviderMatch.None));
                Assert.That(provider.ScrollCapability,
                    Is.EqualTo(HybridScrollCapability.Unsupported));
                Assert.That(provider.Match(new HybridInputEnvironment(null, 1, true)),
                    Is.EqualTo(HybridInputProviderMatch.None));
                Assert.That(provider.Match(new HybridInputEnvironment(derivedModule, 1, true)),
                    Is.EqualTo(HybridInputProviderMatch.None));
                Assert.That(provider.Match(new HybridInputEnvironment(module, 1, true)),
                    Is.EqualTo(HybridInputProviderMatch.Exact));
                Assert.That(provider.ScrollCapability,
                    Is.EqualTo(HybridScrollCapability.DeltaOnly));
                Assert.That(provider.Match(new HybridInputEnvironment(standaloneModule, 1, true)),
                    Is.EqualTo(HybridInputProviderMatch.Exact));
                Assert.That(provider.ScrollCapability,
                    Is.EqualTo(HybridScrollCapability.Unsupported));
                Assert.That((provider.Capabilities & HybridInputCapabilities.ScrollDelta) == 0,
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private sealed class DerivedInputSystemUIInputModule :
            UnityEngine.InputSystem.UI.InputSystemUIInputModule
        {
        }
#endif

        [Test]
        public void ActiveCompositionFocusHandoffQuarantinesOnePollBoundary()
        {
            var source = new FakeLegacyInputSource { CompositionString = "old" };
            var eventSink = new CapturingEventSink();
            var provider = new HybridInputLegacyProvider(source);
            provider.Start(eventSink);
            provider.SetImeEnabled(true);
            provider.Collect(new HybridInputCollectContext(1, 1d));
            eventSink.Events.Clear();
            source.DownKeys.Add(KeyCode.A);

            provider.SetImeEnabled(false);
            provider.SetImeEnabled(true);
            source.InputString = "late";
            source.CompositionString = "late-composition";
            provider.Collect(new HybridInputCollectContext(2, 2d));

            Assert.That(eventSink.Events, Has.Count.EqualTo(1));
            Assert.That(eventSink.Events[0].Kind, Is.EqualTo(HybridInputEventKind.KeyState));
            Assert.That(eventSink.Events[0].Key, Is.EqualTo(KeyCode.A));
            Assert.That(source.ImeEnabled, Is.True);

            eventSink.Events.Clear();
            source.DownKeys.Clear();
            source.InputString = "new";
            source.CompositionString = "new-composition";
            provider.Collect(new HybridInputCollectContext(3, 3d));

            Assert.That(eventSink.Events, Has.Count.EqualTo(2));
            Assert.That(eventSink.Events[0].Kind, Is.EqualTo(HybridInputEventKind.CommittedText));
            Assert.That(eventSink.Events[0].Text, Is.EqualTo("new"));
            Assert.That(eventSink.Events[1].Kind, Is.EqualTo(HybridInputEventKind.Composition));
            Assert.That(eventSink.Events[1].Text, Is.EqualTo("new-composition"));
        }

        [Test]
        public void NativeCancelPrecedesImeOff()
        {
            var operations = new List<string>();
            var source = new FakeLegacyInputSource(operations) { CompositionString = "old" };
            var canceller = new FakeImeSessionCanceller(operations, HybridInputImeCancellationResult.Succeeded);
            var provider = new HybridInputLegacyProvider(source, canceller);
            provider.Start(new CapturingEventSink());
            provider.SetImeEnabled(true);
            provider.Collect(new HybridInputCollectContext(1, 1d));
            operations.Clear();

            provider.SetImeEnabled(false);

            Assert.That(operations, Is.EqualTo(new[] { "cancel", "ime-off" }));
            Assert.That(canceller.CancelCount, Is.EqualTo(1));
        }

        [Test]
        public void NativeCancelFailureWaitsForEmptyCompositionPoll()
        {
            var source = new FakeLegacyInputSource { CompositionString = "old" };
            var canceller = new FakeImeSessionCanceller(null, HybridInputImeCancellationResult.Failed);
            var eventSink = new CapturingEventSink();
            var provider = new HybridInputLegacyProvider(source, canceller);
            provider.Start(eventSink);
            provider.SetImeEnabled(true);
            provider.Collect(new HybridInputCollectContext(1, 1d));
            eventSink.Events.Clear();

            provider.SetImeEnabled(false);
            provider.SetImeEnabled(true);
            source.InputString = "late";
            provider.Collect(new HybridInputCollectContext(2, 2d));

            Assert.That(eventSink.Events, Is.Empty);
            Assert.That(source.ImeEnabled, Is.False);
            Assert.That(HybridInputImeCancellationDiagnostics.LastResult,
                Is.EqualTo(HybridInputImeCancellationResult.Failed));

            source.CompositionString = string.Empty;
            provider.Collect(new HybridInputCollectContext(3, 3d));
            Assert.That(source.ImeEnabled, Is.True);

            source.InputString = "new";
            provider.Collect(new HybridInputCollectContext(4, 4d));
            Assert.That(eventSink.Events, Has.Count.EqualTo(1));
            Assert.That(eventSink.Events[0].Text, Is.EqualTo("new"));
        }

        [Test]
        public void FocusHandoffWithoutCompositionAcceptsNewInputImmediately()
        {
            var source = new FakeLegacyInputSource();
            var eventSink = new CapturingEventSink();
            var provider = new HybridInputLegacyProvider(source);
            provider.Start(eventSink);
            provider.SetImeEnabled(true);

            provider.SetImeEnabled(false);
            provider.SetImeEnabled(true);
            source.InputString = "new";
            provider.Collect(new HybridInputCollectContext(1, 1d));

            Assert.That(eventSink.Events, Has.Count.EqualTo(1));
            Assert.That(eventSink.Events[0].Kind, Is.EqualTo(HybridInputEventKind.CommittedText));
            Assert.That(eventSink.Events[0].Text, Is.EqualTo("new"));
            Assert.That(source.ImeEnabled, Is.True);
        }

        [Test]
        public void CommitWithoutEmptyCompositionUsesFastHandoff()
        {
            var source = new FakeLegacyInputSource { CompositionString = "old" };
            var eventSink = new CapturingEventSink();
            var provider = new HybridInputLegacyProvider(source);
            provider.Start(eventSink);
            provider.SetImeEnabled(true);
            provider.Collect(new HybridInputCollectContext(1, 1d));
            eventSink.Events.Clear();
            source.InputString = "commit";
            provider.Collect(new HybridInputCollectContext(2, 2d));
            eventSink.Events.Clear();

            provider.SetImeEnabled(false);
            provider.SetImeEnabled(true);
            source.InputString = "new";
            provider.Collect(new HybridInputCollectContext(3, 3d));

            Assert.That(eventSink.Events, Has.Count.EqualTo(1));
            Assert.That(eventSink.Events[0].Kind, Is.EqualTo(HybridInputEventKind.CommittedText));
            Assert.That(eventSink.Events[0].Text, Is.EqualTo("new"));
            Assert.That(source.ImeEnabled, Is.True);
        }

        [Test]
        public void CompositionChangedAfterCommitStillQuarantinesHandoff()
        {
            var source = new FakeLegacyInputSource { CompositionString = "old" };
            var eventSink = new CapturingEventSink();
            var provider = new HybridInputLegacyProvider(source);
            provider.Start(eventSink);
            provider.SetImeEnabled(true);
            provider.Collect(new HybridInputCollectContext(1, 1d));
            eventSink.Events.Clear();
            source.InputString = "commit";
            source.CompositionString = "remaining";
            provider.Collect(new HybridInputCollectContext(2, 2d));
            eventSink.Events.Clear();

            provider.SetImeEnabled(false);
            provider.SetImeEnabled(true);
            source.InputString = "late";
            provider.Collect(new HybridInputCollectContext(3, 3d));

            Assert.That(eventSink.Events, Is.Empty);
            Assert.That(source.ImeEnabled, Is.True);
        }

        [Test]
        public void FocusHandoffSamplesCompositionThatStartedAfterLastPoll()
        {
            var source = new FakeLegacyInputSource();
            var eventSink = new CapturingEventSink();
            var provider = new HybridInputLegacyProvider(source);
            provider.Start(eventSink);
            provider.SetImeEnabled(true);
            source.CompositionString = "old-not-yet-polled";

            provider.SetImeEnabled(false);
            provider.SetImeEnabled(true);
            source.InputString = "late";
            provider.Collect(new HybridInputCollectContext(1, 1d));

            Assert.That(eventSink.Events, Is.Empty);
            Assert.That(source.ImeEnabled, Is.True);
        }

        [Test]
        public void RapidFocusHandoffsReplayOnlyFinalImeIntentAfterPollBoundary()
        {
            var source = new FakeLegacyInputSource { CompositionString = "old" };
            var eventSink = new CapturingEventSink();
            var provider = new HybridInputLegacyProvider(source);
            provider.Start(eventSink);
            provider.SetImeEnabled(true);
            provider.Collect(new HybridInputCollectContext(1, 1d));
            eventSink.Events.Clear();

            provider.SetImeEnabled(false);
            provider.SetImeEnabled(true);
            provider.SetImeEnabled(false);
            provider.SetImeCursorPosition(new Vector2(12f, 16f));
            provider.SetImeEnabled(true);
            source.InputString = "late";
            source.CompositionString = "late-composition";
            provider.Collect(new HybridInputCollectContext(2, 2d));

            Assert.That(eventSink.Events, Is.Empty);
            Assert.That(source.ImeEnabled, Is.True);
            Assert.That(source.ImeCursorPosition, Is.EqualTo(new Vector2(12f, 16f)));
        }

        private sealed class CapturingEventSink : IHybridInputEventSink
        {
            internal List<HybridInputEvent> Events { get; } = new List<HybridInputEvent>();
            internal List<KeyCode> PressedKeys { get; } = new List<KeyCode>();

            public void Enqueue(HybridInputEvent inputEvent)
            {
                Events.Add(inputEvent);
            }

            public void ReplacePressedKeys(IReadOnlyList<KeyCode> pressedKeys)
            {
                PressedKeys.Clear();
                PressedKeys.AddRange(pressedKeys);
            }

            public void ResetDeviceState()
            {
            }
        }

        private sealed class DerivedStandaloneInputModule : StandaloneInputModule
        {
        }

        private sealed class FakeLegacyInputSource : ILegacyInputSource
        {
            private readonly List<string>? operations;

            internal FakeLegacyInputSource(List<string>? operations = null)
            {
                this.operations = operations;
            }

            internal HashSet<KeyCode> PressedKeys { get; } = new HashSet<KeyCode>();
            internal HashSet<KeyCode> DownKeys { get; } = new HashSet<KeyCode>();
            internal HashSet<KeyCode> UpKeys { get; } = new HashSet<KeyCode>();

            public string InputString { get; set; } = string.Empty;
            public string CompositionString { get; set; } = string.Empty;
            internal bool ImeEnabled { get; private set; }
            internal int ImeEnabledCalls { get; private set; }
            internal Vector2 ImeCursorPosition { get; private set; }

            public bool GetKey(KeyCode key) => PressedKeys.Contains(key);
            public bool GetKeyDown(KeyCode key) => DownKeys.Contains(key);
            public bool GetKeyUp(KeyCode key) => UpKeys.Contains(key);

            public void SetImeEnabled(bool enabled)
            {
                ImeEnabled = enabled;
                ++ImeEnabledCalls;
                operations?.Add(enabled ? "ime-on" : "ime-off");
            }

            public void SetImeCursorPosition(Vector2 position)
            {
                ImeCursorPosition = position;
            }
        }

        private sealed class FakeImeSessionCanceller : IHybridInputImeSessionCanceller
        {
            private readonly List<string>? operations;
            private readonly HybridInputImeCancellationResult result;

            internal FakeImeSessionCanceller(List<string>? operations, HybridInputImeCancellationResult result)
            {
                this.operations = operations;
                this.result = result;
            }

            public bool IsRequired => true;
            internal int CancelCount { get; private set; }

            public HybridInputImeCancellationResult CancelComposition()
            {
                ++CancelCount;
                operations?.Add("cancel");
                return result;
            }
        }
    }

}

#if ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER && !MILESTRO_INPUT_SYSTEM_SUPPORTED
namespace UnityEngine.InputSystem.UI
{
    internal class InputSystemUIInputModule : BaseInputModule
    {
        public override void Process()
        {
        }
    }
}
#endif
#endif

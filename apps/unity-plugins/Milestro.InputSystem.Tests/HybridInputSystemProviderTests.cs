using System;
using System.Collections.Generic;
using Milestro.Input;
using Milestro.InputSystem.Model;
using Milestro.InputSystem.Service;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Milestro.InputSystemTests
{
    public class HybridInputSystemProviderTests
    {
        [Test]
        public void MatchesOnlyOneActiveInputSystemUiModule()
        {
            var gameObject = new GameObject();
            try
            {
                var module = gameObject.AddComponent<InputSystemUIInputModule>();
                var provider = new HybridInputSystemProvider(new FakeInputSystemSource());

                Assert.That(provider.Match(new HybridInputEnvironment(module, 1, true)),
                    Is.EqualTo(HybridInputProviderMatch.Exact));
                Assert.That(provider.Match(new HybridInputEnvironment(module, 2, true)),
                    Is.EqualTo(HybridInputProviderMatch.None));
                Assert.That(provider.Match(new HybridInputEnvironment(null, 1, true)),
                    Is.EqualTo(HybridInputProviderMatch.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CollectPublishesEdgesAndHeldSnapshot()
        {
            var source = new FakeInputSystemSource();
            source.PressedKeys.Add(KeyCode.LeftShift);
            source.DownKeys.Add(KeyCode.A);
            source.UpKeys.Add(KeyCode.C);
            var eventSink = new CapturingEventSink();
            var provider = new HybridInputSystemProvider(source);
            provider.Start(eventSink);

            provider.Collect(new HybridInputCollectContext(3, 4d));

            Assert.That(source.RefreshCount, Is.EqualTo(1));
            Assert.That(eventSink.PressedKeys, Is.EqualTo(new[] { KeyCode.LeftShift }));
            Assert.That(eventSink.Events, Has.Count.EqualTo(2));
            Assert.That(eventSink.Events[0].Key, Is.EqualTo(KeyCode.A));
            Assert.That(eventSink.Events[0].KeyPressed, Is.True);
            Assert.That(eventSink.Events[1].Key, Is.EqualTo(KeyCode.C));
            Assert.That(eventSink.Events[1].KeyPressed, Is.False);
            Assert.That(eventSink.Events[1].Timestamp, Is.EqualTo(4d));
        }

        [Test]
        public void TextAndCompositionCallbacksStopWithProvider()
        {
            var source = new FakeInputSystemSource();
            var eventSink = new CapturingEventSink();
            var provider = new HybridInputSystemProvider(source);
            provider.Start(eventSink);

            source.RaiseText('x', 1d);
            source.RaiseComposition("composition", 2d);
            provider.Stop();
            source.RaiseText('y', 3d);

            Assert.That(source.StartCount, Is.EqualTo(1));
            Assert.That(source.StopCount, Is.EqualTo(1));
            Assert.That(eventSink.Events, Has.Count.EqualTo(2));
            Assert.That(eventSink.Events[0].Kind, Is.EqualTo(HybridInputEventKind.CommittedText));
            Assert.That(eventSink.Events[0].Text, Is.EqualTo("x"));
            Assert.That(eventSink.Events[1].Kind, Is.EqualTo(HybridInputEventKind.Composition));
            Assert.That(eventSink.Events[1].Text, Is.EqualTo("composition"));
        }

        [Test]
        public void ImeCommandsRefreshAndForwardWithoutStopSideEffects()
        {
            var source = new FakeInputSystemSource();
            var provider = new HybridInputSystemProvider(source);
            provider.Start(new CapturingEventSink());

            provider.SetImeEnabled(true);
            provider.SetImeCursorPosition(new Vector2(4f, 8f));
            provider.Stop();

            Assert.That(source.RefreshCount, Is.EqualTo(2));
            Assert.That(source.ImeEnabled, Is.True);
            Assert.That(source.ImeEnabledCalls, Is.EqualTo(1));
            Assert.That(source.ImeCursorPosition, Is.EqualTo(new Vector2(4f, 8f)));
        }

        [Test]
        public void CapabilitiesReportDeltaOnlyWithoutDeviceOrPhaseClaims()
        {
            var provider = new HybridInputSystemProvider(new FakeInputSystemSource());

            Assert.That(provider.Kind, Is.EqualTo(HybridInputProviderKind.InputSystem));
            Assert.That(provider.ScrollCapability, Is.EqualTo(HybridScrollCapability.DeltaOnly));
            Assert.That((provider.Capabilities & HybridInputCapabilities.ScrollDelta) != 0, Is.True);
            Assert.That((provider.Capabilities & HybridInputCapabilities.ScrollDevice) == 0, Is.True);
            Assert.That((provider.Capabilities & HybridInputCapabilities.ScrollPhase) == 0, Is.True);
        }

        [Test]
        public void ScrollResolverEnrichesTheExistingUguiDeltaWithoutReadingAnotherSource()
        {
            var provider = new HybridInputSystemProvider(new FakeInputSystemSource());
            var eventData = new PointerEventData(null!) { scrollDelta = new Vector2(1.25f, -2.5f) };

            Assert.That(provider.TryResolveScrollInput(eventData, out var resolved), Is.True);
            Assert.That(resolved.Delta, Is.EqualTo(eventData.scrollDelta));
            Assert.That(resolved.Metadata.Capability, Is.EqualTo(HybridScrollCapability.DeltaOnly));
            Assert.That(resolved.Metadata.DeviceKind, Is.EqualTo(HybridInputDeviceKind.Unknown));
            Assert.That(resolved.Metadata.GesturePhase, Is.EqualTo(HybridInputPhase.Unknown));
            Assert.That(resolved.Metadata.MomentumPhase, Is.EqualTo(HybridInputPhase.Unknown));
            Assert.That(resolved.Metadata.GestureId, Is.Zero);
        }

        [Test]
        public void ActiveModuleSwitchSelectsOneProviderAndAdvancesGeneration()
        {
            var inputSystemObject = new GameObject();
            var standaloneObject = new GameObject();
            try
            {
                var inputSystemModule = inputSystemObject.AddComponent<InputSystemUIInputModule>();
                var standaloneModule = standaloneObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                var source = new FakeInputSystemSource();
                var inputSystemProvider = new HybridInputSystemProvider(source);
                var fallbackProvider = new FakeFallbackProvider();
                var dispatcher = new HybridInputDispatcher();
                dispatcher.RegisterProvider(inputSystemProvider);
                dispatcher.RegisterProvider(fallbackProvider);
                var frameSink = new CapturingFrameSink();
                using var registration = dispatcher.RegisterSink(frameSink);
                registration.AcquireFocus();

                dispatcher.RefreshEnvironment(new HybridInputEnvironment(inputSystemModule, 1, true));
                dispatcher.Drain(1, 1d);
                dispatcher.RefreshEnvironment(new HybridInputEnvironment(standaloneModule, 1, true));
                dispatcher.Drain(2, 2d);

                Assert.That(source.StartCount, Is.EqualTo(1));
                Assert.That(source.StopCount, Is.EqualTo(1));
                Assert.That(fallbackProvider.StartCount, Is.EqualTo(1));
                Assert.That(frameSink.Frames, Has.Count.EqualTo(2));
                Assert.That(frameSink.Frames[0].ProviderId, Is.EqualTo(HybridInputSystemProvider.ProviderId));
                Assert.That(frameSink.Frames[1].ProviderId, Is.EqualTo(FakeFallbackProvider.ProviderId));
                Assert.That(frameSink.Frames[1].ProviderGeneration > frameSink.Frames[0].ProviderGeneration,
                    Is.True);
                Assert.That(frameSink.Resets, Does.Contain(HybridInputResetReason.ProviderChanged));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inputSystemObject);
                UnityEngine.Object.DestroyImmediate(standaloneObject);
            }
        }

        [Test]
        public void DeviceResetDropsOldInputAndReplaysImeToSameFocusOwner()
        {
            var inputSystemObject = new GameObject();
            try
            {
                var module = inputSystemObject.AddComponent<InputSystemUIInputModule>();
                var source = new FakeInputSystemSource();
                var provider = new HybridInputSystemProvider(source);
                var dispatcher = new HybridInputDispatcher();
                dispatcher.RegisterProvider(provider);
                var frameSink = new CapturingFrameSink();
                using var registration = dispatcher.RegisterSink(frameSink);
                registration.AcquireFocus();
                dispatcher.RefreshEnvironment(new HybridInputEnvironment(module, 1, true));
                registration.SetImeEnabled(true);
                registration.SetImeCursorPosition(new Vector2(4f, 8f));
                source.PressedKeys.Add(KeyCode.LeftShift);
                dispatcher.Drain(1, 1d);
                var firstFrame = frameSink.Frames[0];
                frameSink.Resets.Clear();
                var oldTextCallback = source.CaptureTextCallback();
                source.RaiseText('o', 1.1d);
                source.RaiseComposition("old", 1.2d);
                source.PressedKeys.Clear();

                source.ChangeDevice("B");
                oldTextCallback('x', 1.3d);
                source.RaiseText('n', 1.4d);
                dispatcher.Drain(2, 2d);

                Assert.That(frameSink.Resets, Has.Count.EqualTo(1));
                Assert.That(frameSink.Resets[0], Is.EqualTo(HybridInputResetReason.DeviceChanged));
                Assert.That(frameSink.Frames, Has.Count.EqualTo(2));
                var secondFrame = frameSink.Frames[1];
                Assert.That(secondFrame.ProviderGeneration, Is.EqualTo(firstFrame.ProviderGeneration));
                Assert.That(secondFrame.FocusEpoch > firstFrame.FocusEpoch, Is.True);
                Assert.That(secondFrame.Events, Has.Count.EqualTo(1));
                Assert.That(secondFrame.Events[0].Text, Is.EqualTo("n"));
                Assert.That(secondFrame.IsKeyPressed(KeyCode.LeftShift), Is.False);
                Assert.That(source.ImeEnabled, Is.True);
                Assert.That(source.ImeEnabledCalls, Is.EqualTo(3));
                Assert.That(source.ImeCursorPositionCalls, Is.EqualTo(3));
                Assert.That(source.ImeCursorPosition, Is.EqualTo(new Vector2(4f, 8f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inputSystemObject);
            }
        }

        [Test]
        public void DeviceChangesCoverReplacementRemovalAdditionAndRepeatedRefresh()
        {
            var source = new FakeInputSystemSource();
            var eventSink = new CapturingEventSink();
            var provider = new HybridInputSystemProvider(source);
            provider.Start(eventSink);

            source.ChangeDevice("B");
            source.ChangeDevice(null);
            source.ChangeDevice(null);
            source.Refresh();
            source.ChangeDevice("C");

            Assert.That(eventSink.DeviceResetCount, Is.EqualTo(3));
        }

        [Test]
        public void RealSourceCoalescesRemovedFallbackAndDisconnectedIntoOneCoreReset()
        {
            var inputSystemObject = new GameObject();
            try
            {
                var module = inputSystemObject.AddComponent<InputSystemUIInputModule>();
                var keyboardA = new FakeUnityInputSystemKeyboard();
                var keyboardB = new FakeUnityInputSystemKeyboard();
                var backend = new FakeUnityInputSystemBackend(keyboardA);
                var source = new UnityInputSystemSource(backend);
                var provider = new HybridInputSystemProvider(source);
                var dispatcher = new HybridInputDispatcher();
                dispatcher.RegisterProvider(provider);
                var frameSink = new CapturingFrameSink();
                using var registration = dispatcher.RegisterSink(frameSink);
                registration.AcquireFocus();
                dispatcher.RefreshEnvironment(new HybridInputEnvironment(module, 1, true));
                registration.SetImeEnabled(true);
                registration.SetImeCursorPosition(new Vector2(4f, 8f));
                dispatcher.Drain(1, 1d);
                var firstFrame = frameSink.Frames[0];
                frameSink.Resets.Clear();
                var lateTextInput = keyboardA.CaptureTextInput();

                backend.CurrentKeyboard = null;
                backend.RaiseDeviceChange();
                backend.CurrentKeyboard = keyboardB;
                backend.RaiseDeviceChange();
                source.Refresh();
                lateTextInput('o');
                keyboardB.RaiseText('n');
                dispatcher.Drain(2, 2d);
                source.Refresh();

                Assert.That(keyboardA.UnbindCount, Is.EqualTo(1));
                Assert.That(keyboardB.BindCount, Is.EqualTo(1));
                Assert.That(frameSink.Resets, Is.EqualTo(new[] { HybridInputResetReason.DeviceChanged }));
                Assert.That(frameSink.Frames, Has.Count.EqualTo(2));
                var secondFrame = frameSink.Frames[1];
                Assert.That(secondFrame.ProviderGeneration, Is.EqualTo(firstFrame.ProviderGeneration));
                Assert.That(secondFrame.FocusEpoch > firstFrame.FocusEpoch, Is.True);
                Assert.That(secondFrame.Events, Has.Count.EqualTo(1));
                Assert.That(secondFrame.Events[0].Text, Is.EqualTo("n"));
                Assert.That(keyboardB.ImeEnabledCalls, Is.EqualTo(1));
                Assert.That(keyboardB.ImeEnabled, Is.True);
                Assert.That(keyboardB.ImeCursorPositionCalls, Is.EqualTo(1));
                Assert.That(keyboardB.ImeCursorPosition, Is.EqualTo(new Vector2(4f, 8f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inputSystemObject);
            }
        }

        [Test]
        public void RealSourceRemovalWithoutFallbackResetsOnceOnRefresh()
        {
            var keyboard = new FakeUnityInputSystemKeyboard();
            var backend = new FakeUnityInputSystemBackend(keyboard);
            var source = new UnityInputSystemSource(backend);
            var deviceResetCount = 0;
            source.DeviceChanged += () => ++deviceResetCount;
            source.Start();

            backend.CurrentKeyboard = null;
            backend.RaiseDeviceChange();
            source.Refresh();
            source.Refresh();

            Assert.That(deviceResetCount, Is.EqualTo(1));
            Assert.That(keyboard.UnbindCount, Is.EqualTo(1));
        }

        [Test]
        public void RealSourceStopClearsPendingRemovalBeforeRestart()
        {
            var keyboardA = new FakeUnityInputSystemKeyboard();
            var keyboardB = new FakeUnityInputSystemKeyboard();
            var backend = new FakeUnityInputSystemBackend(keyboardA);
            var source = new UnityInputSystemSource(backend);
            var deviceResetCount = 0;
            source.DeviceChanged += () => ++deviceResetCount;
            source.Start();

            backend.CurrentKeyboard = null;
            backend.RaiseDeviceChange();
            source.Stop();
            backend.CurrentKeyboard = keyboardB;
            backend.RaiseDeviceChange();
            source.Start();
            source.Refresh();

            Assert.That(deviceResetCount, Is.Zero);
            Assert.That(backend.StartCount, Is.EqualTo(2));
            Assert.That(backend.StopCount, Is.EqualTo(1));
            Assert.That(keyboardB.BindCount, Is.EqualTo(1));
        }

        [Test]
        public void RealSourceEmptyCompositionAckQuarantinesOldImeSession()
        {
            var keyboard = new FakeUnityInputSystemKeyboard();
            var backend = new FakeUnityInputSystemBackend(keyboard);
            var source = new UnityInputSystemSource(backend);
            var text = new List<char>();
            var compositions = new List<string>();
            source.TextInput += (character, _) => text.Add(character);
            source.CompositionChanged += (composition, _) => compositions.Add(composition);
            source.Start();
            source.SetImeCursorPosition(new Vector2(4f, 8f));
            source.SetImeEnabled(true);
            keyboard.RaiseComposition("old");
            var lateText = keyboard.CaptureTextInput();
            var lateComposition = keyboard.CaptureComposition();

            source.SetImeEnabled(false);
            source.SetImeEnabled(true);
            lateComposition("late");
            lateText('x');
            lateComposition(string.Empty);
            lateComposition("later");
            lateText('y');
            keyboard.RaiseComposition("new");
            keyboard.RaiseText('n');

            Assert.That(compositions, Has.Count.EqualTo(2));
            Assert.That(compositions[0], Is.EqualTo("old"));
            Assert.That(compositions[1], Is.EqualTo("new"));
            Assert.That(text, Has.Count.EqualTo(1));
            Assert.That(text[0], Is.EqualTo('n'));
            Assert.That(keyboard.UnbindCount, Is.EqualTo(1));
            Assert.That(keyboard.BindCount, Is.EqualTo(2));
            Assert.That(keyboard.ImeEnabled, Is.True);
            Assert.That(keyboard.ImeCursorPosition, Is.EqualTo(new Vector2(4f, 8f)));
            Assert.That(keyboard.ImeCursorPositionCalls, Is.EqualTo(2));
        }

        [Test]
        public void RealSourceSynchronousEmptyAckIsInternalToQuiesce()
        {
            var keyboard = new FakeUnityInputSystemKeyboard
            {
                RaiseEmptyCompositionOnImeDisable = true
            };
            var backend = new FakeUnityInputSystemBackend(keyboard);
            var source = new UnityInputSystemSource(backend);
            var compositions = new List<string>();
            source.CompositionChanged += (composition, _) => compositions.Add(composition);
            source.Start();
            source.SetImeEnabled(true);
            keyboard.RaiseComposition("old");

            source.SetImeEnabled(false);
            source.SetImeEnabled(true);
            keyboard.RaiseComposition("new");

            Assert.That(compositions, Has.Count.EqualTo(2));
            Assert.That(compositions[0], Is.EqualTo("old"));
            Assert.That(compositions[1], Is.EqualTo("new"));
            Assert.That(keyboard.UnbindCount, Is.EqualTo(1));
            Assert.That(keyboard.BindCount, Is.EqualTo(2));
            Assert.That(keyboard.ImeEnabled, Is.True);
        }

        [Test]
        public void RealSourceAfterUpdateQuarantinesOldImeSessionWithoutAck()
        {
            var keyboard = new FakeUnityInputSystemKeyboard();
            var backend = new FakeUnityInputSystemBackend(keyboard);
            var source = new UnityInputSystemSource(backend);
            var text = new List<char>();
            var compositions = new List<string>();
            source.TextInput += (character, _) => text.Add(character);
            source.CompositionChanged += (composition, _) => compositions.Add(composition);
            source.Start();
            source.SetImeEnabled(true);
            keyboard.RaiseComposition("old");
            var lateText = keyboard.CaptureTextInput();
            var lateComposition = keyboard.CaptureComposition();

            source.SetImeEnabled(false);
            source.SetImeEnabled(true);
            lateComposition("late-1");
            lateText('x');
            lateText('y');
            lateComposition("late-2");
            backend.RaiseAfterUpdate();
            lateComposition("later");
            lateText('z');
            // Once rebound, the platform must only invoke the current delegate for the new session.
            keyboard.RaiseComposition("new");
            keyboard.RaiseText('n');

            Assert.That(compositions, Has.Count.EqualTo(2));
            Assert.That(compositions[0], Is.EqualTo("old"));
            Assert.That(compositions[1], Is.EqualTo("new"));
            Assert.That(text, Has.Count.EqualTo(1));
            Assert.That(text[0], Is.EqualTo('n'));
            Assert.That(keyboard.UnbindCount, Is.EqualTo(1));
            Assert.That(keyboard.BindCount, Is.EqualTo(2));
            Assert.That(keyboard.ImeEnabled, Is.True);
        }

        [Test]
        public void RealSourceNativeCancelPrecedesImeOff()
        {
            var operations = new List<string>();
            var keyboard = new FakeUnityInputSystemKeyboard(operations);
            var backend = new FakeUnityInputSystemBackend(keyboard);
            var canceller = new FakeUnityImeSessionCanceller(operations, HybridInputImeCancellationResult.Succeeded);
            var source = new UnityInputSystemSource(backend, canceller);
            var text = new List<char>();
            source.TextInput += (character, _) => text.Add(character);
            source.Start();
            source.SetImeEnabled(true);
            keyboard.RaiseComposition("old");
            var oldText = keyboard.CaptureTextInput();
            var oldComposition = keyboard.CaptureComposition();
            canceller.OnCancel = () =>
            {
                oldText('x');
                oldComposition(string.Empty);
            };
            operations.Clear();

            source.SetImeEnabled(false);

            Assert.That(operations, Is.EqualTo(new[] { "cancel", "ime-off" }));
            Assert.That(canceller.CancelCount, Is.EqualTo(1));
            Assert.That(text, Is.Empty);
            Assert.That(keyboard.BindCount, Is.EqualTo(2));
        }

        [Test]
        public void RealSourceNativeCancelFailureWaitsForEmptyAck()
        {
            var keyboard = new FakeUnityInputSystemKeyboard();
            var backend = new FakeUnityInputSystemBackend(keyboard);
            var canceller = new FakeUnityImeSessionCanceller(null, HybridInputImeCancellationResult.Failed);
            var source = new UnityInputSystemSource(backend, canceller);
            var text = new List<char>();
            source.TextInput += (character, _) => text.Add(character);
            source.Start();
            source.SetImeEnabled(true);
            keyboard.RaiseComposition("old");
            var oldComposition = keyboard.CaptureComposition();

            source.SetImeEnabled(false);
            source.SetImeEnabled(true);
            backend.RaiseAfterUpdate();
            keyboard.RaiseText('x');

            Assert.That(keyboard.BindCount, Is.EqualTo(1));
            Assert.That(keyboard.ImeEnabled, Is.False);
            Assert.That(text, Is.Empty);
            Assert.That(HybridInputImeCancellationDiagnostics.LastResult,
                Is.EqualTo(HybridInputImeCancellationResult.Failed));

            oldComposition(string.Empty);
            keyboard.RaiseText('n');

            Assert.That(keyboard.BindCount, Is.EqualTo(2));
            Assert.That(keyboard.ImeEnabled, Is.True);
            Assert.That(text, Is.EqualTo(new[] { 'n' }));
        }

        [Test]
        public void RealSourceFocusHandoffWithoutCompositionAcceptsNewInputImmediately()
        {
            var keyboard = new FakeUnityInputSystemKeyboard();
            var backend = new FakeUnityInputSystemBackend(keyboard);
            var source = new UnityInputSystemSource(backend);
            var text = new List<char>();
            source.TextInput += (character, _) => text.Add(character);
            source.Start();
            source.SetImeEnabled(true);
            var oldText = keyboard.CaptureTextInput();

            source.SetImeEnabled(false);
            source.SetImeEnabled(true);
            oldText('x');
            keyboard.RaiseText('n');

            Assert.That(text, Has.Count.EqualTo(1));
            Assert.That(text[0], Is.EqualTo('n'));
            Assert.That(keyboard.UnbindCount, Is.EqualTo(1));
            Assert.That(keyboard.BindCount, Is.EqualTo(2));
            Assert.That(keyboard.ImeEnabled, Is.True);
        }

        [Test]
        public void RealSourceCommitWithoutEmptyCompositionUsesFastHandoff()
        {
            var keyboard = new FakeUnityInputSystemKeyboard();
            var backend = new FakeUnityInputSystemBackend(keyboard);
            var source = new UnityInputSystemSource(backend);
            var text = new List<char>();
            source.TextInput += (character, _) => text.Add(character);
            source.Start();
            source.SetImeEnabled(true);
            keyboard.RaiseComposition("old");
            keyboard.RaiseText('a');
            var oldText = keyboard.CaptureTextInput();

            source.SetImeEnabled(false);
            source.SetImeEnabled(true);
            oldText('x');
            keyboard.RaiseText('n');

            Assert.That(keyboard.BindCount, Is.EqualTo(2));
            Assert.That(keyboard.ImeEnabled, Is.True);
            Assert.That(text, Has.Count.EqualTo(2));
            Assert.That(text[0], Is.EqualTo('a'));
            Assert.That(text[1], Is.EqualTo('n'));
        }

        [Test]
        public void RealSourceCompositionAfterCommitStillQuarantinesHandoff()
        {
            var keyboard = new FakeUnityInputSystemKeyboard();
            var backend = new FakeUnityInputSystemBackend(keyboard);
            var source = new UnityInputSystemSource(backend);
            var text = new List<char>();
            source.TextInput += (character, _) => text.Add(character);
            source.Start();
            source.SetImeEnabled(true);
            keyboard.RaiseComposition("old");
            keyboard.RaiseText('a');
            keyboard.RaiseComposition("remaining");
            var oldText = keyboard.CaptureTextInput();

            source.SetImeEnabled(false);
            source.SetImeEnabled(true);
            oldText('x');

            Assert.That(keyboard.BindCount, Is.EqualTo(1));
            Assert.That(keyboard.ImeEnabled, Is.False);
            Assert.That(text, Is.EqualTo(new[] { 'a' }));

            backend.RaiseAfterUpdate();
            keyboard.RaiseText('n');

            Assert.That(keyboard.BindCount, Is.EqualTo(2));
            Assert.That(keyboard.ImeEnabled, Is.True);
            Assert.That(text, Has.Count.EqualTo(2));
            Assert.That(text[1], Is.EqualTo('n'));
        }

        [Test]
        public void RealSourceRapidFocusHandoffsReplayOnlyFinalImeIntent()
        {
            var keyboard = new FakeUnityInputSystemKeyboard();
            var backend = new FakeUnityInputSystemBackend(keyboard);
            var source = new UnityInputSystemSource(backend);
            var text = new List<char>();
            source.TextInput += (character, _) => text.Add(character);
            source.Start();
            source.SetImeEnabled(true);
            keyboard.RaiseComposition("old");
            var oldText = keyboard.CaptureTextInput();

            source.SetImeEnabled(false);
            source.SetImeEnabled(true);
            source.SetImeEnabled(false);
            source.SetImeCursorPosition(new Vector2(12f, 16f));
            source.SetImeEnabled(true);
            oldText('x');
            backend.RaiseAfterUpdate();
            oldText('y');
            keyboard.RaiseText('n');

            Assert.That(text, Has.Count.EqualTo(1));
            Assert.That(text[0], Is.EqualTo('n'));
            Assert.That(keyboard.UnbindCount, Is.EqualTo(1));
            Assert.That(keyboard.BindCount, Is.EqualTo(2));
            Assert.That(keyboard.ImeEnabled, Is.True);
            Assert.That(keyboard.ImeCursorPosition, Is.EqualTo(new Vector2(12f, 16f)));
        }

        [Test]
        public void RealSourceStopInvalidatesQuiescingSessionAndAfterUpdateSubscription()
        {
            var keyboard = new FakeUnityInputSystemKeyboard();
            var backend = new FakeUnityInputSystemBackend(keyboard);
            var source = new UnityInputSystemSource(backend);
            var text = new List<char>();
            source.TextInput += (character, _) => text.Add(character);
            source.Start();
            source.SetImeEnabled(true);
            keyboard.RaiseComposition("old");
            var oldText = keyboard.CaptureTextInput();

            source.SetImeEnabled(false);
            source.SetImeEnabled(true);
            source.Stop();
            oldText('x');
            backend.RaiseAfterUpdate();

            Assert.That(text, Is.Empty);
            Assert.That(backend.StopCount, Is.EqualTo(1));
            Assert.That(keyboard.UnbindCount, Is.EqualTo(1));
            Assert.That(keyboard.ImeEnabled, Is.False);
        }

        [Test]
        public void RealSourceDisableCompletesQuiesceWithoutReenablingIme()
        {
            var keyboard = new FakeUnityInputSystemKeyboard();
            var backend = new FakeUnityInputSystemBackend(keyboard);
            var source = new UnityInputSystemSource(backend);
            var text = new List<char>();
            source.TextInput += (character, _) => text.Add(character);
            source.Start();
            source.SetImeEnabled(true);
            keyboard.RaiseComposition("old");
            var oldText = keyboard.CaptureTextInput();

            source.SetImeEnabled(false);
            oldText('x');
            backend.RaiseAfterUpdate();
            oldText('y');

            Assert.That(text, Is.Empty);
            Assert.That(keyboard.UnbindCount, Is.EqualTo(1));
            Assert.That(keyboard.BindCount, Is.EqualTo(2));
            Assert.That(keyboard.ImeEnabled, Is.False);
        }

        [Test]
        public void RealSourceDeviceReplacementInvalidatesQuiescingSession()
        {
            var keyboardA = new FakeUnityInputSystemKeyboard();
            var keyboardB = new FakeUnityInputSystemKeyboard();
            var backend = new FakeUnityInputSystemBackend(keyboardA);
            var source = new UnityInputSystemSource(backend);
            var text = new List<char>();
            var deviceResetCount = 0;
            source.TextInput += (character, _) => text.Add(character);
            source.DeviceChanged += () => ++deviceResetCount;
            source.Start();
            source.SetImeEnabled(true);
            keyboardA.RaiseComposition("old");
            var oldText = keyboardA.CaptureTextInput();

            source.SetImeEnabled(false);
            source.SetImeEnabled(true);
            backend.CurrentKeyboard = keyboardB;
            backend.RaiseDeviceChange();
            oldText('x');
            source.SetImeEnabled(true);
            keyboardB.RaiseText('n');

            Assert.That(text, Has.Count.EqualTo(1));
            Assert.That(text[0], Is.EqualTo('n'));
            Assert.That(deviceResetCount, Is.EqualTo(1));
            Assert.That(keyboardA.UnbindCount, Is.EqualTo(1));
            Assert.That(keyboardB.BindCount, Is.EqualTo(1));
            Assert.That(keyboardB.ImeEnabled, Is.True);
        }

        [Test]
        public void RealSourceDispatcherFocusHandoffDoesNotTransferOldImeEvents()
        {
            var inputSystemObject = new GameObject();
            try
            {
                var module = inputSystemObject.AddComponent<InputSystemUIInputModule>();
                var keyboard = new FakeUnityInputSystemKeyboard();
                var backend = new FakeUnityInputSystemBackend(keyboard);
                var source = new UnityInputSystemSource(backend);
                var provider = new HybridInputSystemProvider(source);
                var dispatcher = new HybridInputDispatcher();
                dispatcher.RegisterProvider(provider);
                var firstSink = new CapturingFrameSink();
                var secondSink = new CapturingFrameSink();
                using var first = dispatcher.RegisterSink(firstSink);
                using var second = dispatcher.RegisterSink(secondSink);
                first.AcquireFocus();
                dispatcher.RefreshEnvironment(new HybridInputEnvironment(module, 1, true));
                first.SetImeEnabled(true);
                keyboard.RaiseComposition("old");
                dispatcher.Drain(1, 1d);
                var oldText = keyboard.CaptureTextInput();
                var oldComposition = keyboard.CaptureComposition();

                first.ReleaseFocus();
                second.AcquireFocus();
                second.SetImeEnabled(true);
                second.SetImeCursorPosition(new Vector2(12f, 16f));
                oldComposition("late");
                oldText('x');
                dispatcher.Drain(2, 2d);
                backend.RaiseAfterUpdate();
                oldComposition("later");
                oldText('y');
                keyboard.RaiseComposition("new");
                keyboard.RaiseText('n');
                dispatcher.Drain(3, 3d);

                Assert.That(firstSink.Resets, Does.Contain(HybridInputResetReason.FocusChanged));
                Assert.That(secondSink.Frames, Has.Count.EqualTo(2));
                Assert.That(secondSink.Frames[0].Events, Is.Empty);
                Assert.That(secondSink.Frames[1].Events, Has.Count.EqualTo(2));
                Assert.That(secondSink.Frames[1].Events[0].Text, Is.EqualTo("new"));
                Assert.That(secondSink.Frames[1].Events[1].Text, Is.EqualTo("n"));
                Assert.That(keyboard.ImeCursorPosition, Is.EqualTo(new Vector2(12f, 16f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inputSystemObject);
            }
        }

        [Test]
        public void RealSourceApplicationFocusLossQuarantinesCompositionUntilAfterUpdate()
        {
            var inputSystemObject = new GameObject();
            try
            {
                var module = inputSystemObject.AddComponent<InputSystemUIInputModule>();
                var keyboard = new FakeUnityInputSystemKeyboard();
                var backend = new FakeUnityInputSystemBackend(keyboard);
                var source = new UnityInputSystemSource(backend);
                var provider = new HybridInputSystemProvider(source);
                var dispatcher = new HybridInputDispatcher();
                dispatcher.RegisterProvider(provider);
                var frameSink = new CapturingFrameSink();
                using var registration = dispatcher.RegisterSink(frameSink);
                registration.AcquireFocus();
                dispatcher.RefreshEnvironment(new HybridInputEnvironment(module, 1, true));
                registration.SetImeEnabled(true);
                keyboard.RaiseComposition("old");
                dispatcher.Drain(1, 1d);
                var oldText = keyboard.CaptureTextInput();

                dispatcher.RefreshEnvironment(new HybridInputEnvironment(module, 1, false));
                oldText('x');
                backend.RaiseAfterUpdate();
                dispatcher.RefreshEnvironment(new HybridInputEnvironment(module, 1, true));
                keyboard.RaiseText('n');
                dispatcher.Drain(2, 2d);

                Assert.That(frameSink.Resets, Does.Contain(HybridInputResetReason.ApplicationFocusLost));
                Assert.That(frameSink.Frames, Has.Count.EqualTo(2));
                Assert.That(frameSink.Frames[1].Events, Has.Count.EqualTo(1));
                Assert.That(frameSink.Frames[1].Events[0].Text, Is.EqualTo("n"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inputSystemObject);
            }
        }

        private sealed class CapturingEventSink : IHybridInputEventSink
        {
            internal List<HybridInputEvent> Events { get; } = new List<HybridInputEvent>();
            internal List<KeyCode> PressedKeys { get; } = new List<KeyCode>();
            internal int DeviceResetCount { get; private set; }

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
                ++DeviceResetCount;
            }
        }

        private sealed class CapturingFrameSink : IHybridInputFrameSink
        {
            internal List<HybridInputFrame> Frames { get; } = new List<HybridInputFrame>();
            internal List<HybridInputResetReason> Resets { get; } = new List<HybridInputResetReason>();

            public void OnInputFrame(HybridInputFrame frame)
            {
                Frames.Add(frame);
            }

            public void OnInputReset(HybridInputResetReason reason)
            {
                Resets.Add(reason);
            }
        }

        private sealed class FakeFallbackProvider : IHybridInputProvider
        {
            internal const string ProviderId = "fallback";

            public string Id => ProviderId;
            public int Priority => 0;
            public HybridInputProviderKind Kind => HybridInputProviderKind.Custom;
            public HybridInputCapabilities Capabilities => HybridInputCapabilities.None;
            public HybridScrollCapability ScrollCapability => HybridScrollCapability.Unsupported;
            internal int StartCount { get; private set; }

            public HybridInputProviderMatch Match(HybridInputEnvironment environment)
            {
                return environment.ActiveModule != null &&
                       environment.ActiveModule.GetType() ==
                       typeof(UnityEngine.EventSystems.StandaloneInputModule)
                    ? HybridInputProviderMatch.Exact
                    : HybridInputProviderMatch.None;
            }

            public void Start(IHybridInputEventSink sink)
            {
                ++StartCount;
            }

            public void Stop()
            {
            }

            public void Collect(HybridInputCollectContext context)
            {
            }

            public void SetImeEnabled(bool enabled)
            {
            }

            public void SetImeCursorPosition(Vector2 position)
            {
            }
        }

        private sealed class FakeUnityInputSystemBackend : IUnityInputSystemBackend
        {
            private event Action? DeviceChanged;
            private event Action? AfterUpdate;

            internal FakeUnityInputSystemBackend(IUnityInputSystemKeyboard? currentKeyboard)
            {
                CurrentKeyboard = currentKeyboard;
            }

            public IUnityInputSystemKeyboard? CurrentKeyboard { get; internal set; }
            internal int StartCount { get; private set; }
            internal int StopCount { get; private set; }

            public void Start(Action deviceChanged, Action afterUpdate)
            {
                ++StartCount;
                DeviceChanged += deviceChanged;
                AfterUpdate += afterUpdate;
            }

            public void Stop(Action deviceChanged, Action afterUpdate)
            {
                ++StopCount;
                DeviceChanged -= deviceChanged;
                AfterUpdate -= afterUpdate;
            }

            internal void RaiseDeviceChange()
            {
                DeviceChanged?.Invoke();
            }

            internal void RaiseAfterUpdate()
            {
                AfterUpdate?.Invoke();
            }
        }

        private sealed class FakeUnityInputSystemKeyboard : IUnityInputSystemKeyboard
        {
            private readonly List<string>? operations;
            private Action<char>? textInput;
            private Action<string>? compositionChanged;

            internal FakeUnityInputSystemKeyboard(List<string>? operations = null)
            {
                this.operations = operations;
            }

            internal int BindCount { get; private set; }
            internal int UnbindCount { get; private set; }
            internal bool ImeEnabled { get; private set; }
            internal int ImeEnabledCalls { get; private set; }
            internal Vector2 ImeCursorPosition { get; private set; }
            internal int ImeCursorPositionCalls { get; private set; }
            internal bool RaiseEmptyCompositionOnImeDisable { get; set; }

            public void Bind(Action<char> nextTextInput, Action<string> nextCompositionChanged)
            {
                ++BindCount;
                textInput = nextTextInput;
                compositionChanged = nextCompositionChanged;
            }

            public void Unbind()
            {
                ++UnbindCount;
                textInput = null;
                compositionChanged = null;
            }

            public bool GetKey(KeyCode key) => false;
            public bool GetKeyDown(KeyCode key) => false;
            public bool GetKeyUp(KeyCode key) => false;

            public void SetImeEnabled(bool enabled)
            {
                ImeEnabled = enabled;
                ++ImeEnabledCalls;
                operations?.Add(enabled ? "ime-on" : "ime-off");
                if (!enabled && RaiseEmptyCompositionOnImeDisable)
                {
                    compositionChanged?.Invoke(string.Empty);
                }
            }

            public void SetImeCursorPosition(Vector2 position)
            {
                ImeCursorPosition = position;
                ++ImeCursorPositionCalls;
            }

            internal Action<char> CaptureTextInput()
            {
                return textInput ?? throw new InvalidOperationException("Keyboard is not bound.");
            }

            internal Action<string> CaptureComposition()
            {
                return compositionChanged ?? throw new InvalidOperationException("Keyboard is not bound.");
            }

            internal void RaiseText(char character)
            {
                textInput?.Invoke(character);
            }

            internal void RaiseComposition(string composition)
            {
                compositionChanged?.Invoke(composition);
            }
        }

        private sealed class FakeUnityImeSessionCanceller : IHybridInputImeSessionCanceller
        {
            private readonly List<string>? operations;
            private readonly HybridInputImeCancellationResult result;

            internal FakeUnityImeSessionCanceller(List<string>? operations, HybridInputImeCancellationResult result)
            {
                this.operations = operations;
                this.result = result;
            }

            public bool IsRequired => true;
            internal int CancelCount { get; private set; }
            internal Action? OnCancel { get; set; }

            public HybridInputImeCancellationResult CancelComposition()
            {
                ++CancelCount;
                operations?.Add("cancel");
                OnCancel?.Invoke();
                return result;
            }
        }

        private sealed class FakeInputSystemSource : IHybridInputSystemSource
        {
            internal HashSet<KeyCode> PressedKeys { get; } = new HashSet<KeyCode>();
            internal HashSet<KeyCode> DownKeys { get; } = new HashSet<KeyCode>();
            internal HashSet<KeyCode> UpKeys { get; } = new HashSet<KeyCode>();
            internal int StartCount { get; private set; }
            internal int StopCount { get; private set; }
            internal int RefreshCount { get; private set; }
            internal bool ImeEnabled { get; private set; }
            internal int ImeEnabledCalls { get; private set; }
            internal Vector2 ImeCursorPosition { get; private set; }
            internal int ImeCursorPositionCalls { get; private set; }
            private long deviceGeneration;
            private string? deviceId = "A";

            public event Action<char, double>? TextInput;
            public event Action<string, double>? CompositionChanged;
            public event Action? DeviceChanged;

            public void Start() => ++StartCount;
            public void Stop() => ++StopCount;
            public void Refresh() => ++RefreshCount;
            public bool GetKey(KeyCode key) => PressedKeys.Contains(key);
            public bool GetKeyDown(KeyCode key) => DownKeys.Contains(key);
            public bool GetKeyUp(KeyCode key) => UpKeys.Contains(key);

            public void SetImeEnabled(bool enabled)
            {
                ImeEnabled = enabled;
                ++ImeEnabledCalls;
            }

            public void SetImeCursorPosition(Vector2 position)
            {
                ImeCursorPosition = position;
                ++ImeCursorPositionCalls;
            }

            internal void RaiseText(char character, double timestamp)
            {
                TextInput?.Invoke(character, timestamp);
            }

            internal void RaiseComposition(string composition, double timestamp)
            {
                CompositionChanged?.Invoke(composition, timestamp);
            }

            internal Action<char, double> CaptureTextCallback()
            {
                var capturedGeneration = deviceGeneration;
                return (character, timestamp) =>
                {
                    if (capturedGeneration == deviceGeneration)
                    {
                        TextInput?.Invoke(character, timestamp);
                    }
                };
            }

            internal void ChangeDevice(string? nextDeviceId)
            {
                if (deviceId == nextDeviceId)
                {
                    return;
                }

                deviceId = nextDeviceId;
                ++deviceGeneration;
                DeviceChanged?.Invoke();
            }
        }
    }
}

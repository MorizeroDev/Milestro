using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Input;
using Milestro.Model;
using Milestro.Skia.TextLayout;
using Milestro.Util;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.LowLevel;

namespace Milestro.Tests
{
    public class HybridInputDispatcherTests
    {
        [Test]
        public void HighestMatchAndPrioritySelectExactlyOneProvider()
        {
            var dispatcher = new HybridInputDispatcher();
            var compatible = new FakeProvider("compatible", HybridInputProviderMatch.Compatible, priority: 100);
            var exactLow = new FakeProvider("exact-low", HybridInputProviderMatch.Exact, priority: 1);
            var exactHigh = new FakeProvider("exact-high", HybridInputProviderMatch.Exact, priority: 2);
            dispatcher.RegisterProvider(compatible);
            dispatcher.RegisterProvider(exactLow);
            dispatcher.RegisterProvider(exactHigh);

            dispatcher.RefreshEnvironment(FocusedEnvironment());

            Assert.That(dispatcher.Diagnostics.SelectionStatus, Is.EqualTo(HybridInputSelectionStatus.Selected));
            Assert.That(dispatcher.Diagnostics.ProviderId, Is.EqualTo("exact-high"));
            Assert.That(compatible.StartCount, Is.Zero);
            Assert.That(exactLow.StartCount, Is.Zero);
            Assert.That(exactHigh.StartCount, Is.EqualTo(1));
        }

        [Test]
        public void EqualBestProvidersFailClosedAsConflict()
        {
            var dispatcher = new HybridInputDispatcher();
            dispatcher.RegisterProvider(new FakeProvider("first", HybridInputProviderMatch.Exact));
            dispatcher.RegisterProvider(new FakeProvider("second", HybridInputProviderMatch.Exact));

            dispatcher.RefreshEnvironment(FocusedEnvironment());

            Assert.That(dispatcher.Diagnostics.SelectionStatus, Is.EqualTo(HybridInputSelectionStatus.Conflict));
            Assert.That(dispatcher.Diagnostics.ProviderId, Is.Empty);
        }

        [Test]
        public void ExplicitOverrideStillRequiresProviderToMatchEnvironment()
        {
            var dispatcher = new HybridInputDispatcher();
            dispatcher.RegisterProvider(new FakeProvider("new", HybridInputProviderMatch.None));
            dispatcher.SetProviderOverride("new");

            dispatcher.RefreshEnvironment(FocusedEnvironment());

            Assert.That(dispatcher.Diagnostics.SelectionStatus,
                Is.EqualTo(HybridInputSelectionStatus.OverrideRejected));
            Assert.That(dispatcher.Diagnostics.ProviderId, Is.Empty);
        }

        [Test]
        public void LateProviderRegistrationAndRemovalReselectImmediately()
        {
            var dispatcher = new HybridInputDispatcher();
            dispatcher.RefreshEnvironment(FocusedEnvironment());
            var fallback = new FakeProvider("fallback", HybridInputProviderMatch.Compatible);
            var preferred = new FakeProvider("preferred", HybridInputProviderMatch.Exact);

            dispatcher.RegisterProvider(fallback);
            Assert.That(dispatcher.Diagnostics.ProviderId, Is.EqualTo("fallback"));

            var preferredHandle = dispatcher.RegisterProvider(preferred);
            Assert.That(dispatcher.Diagnostics.ProviderId, Is.EqualTo("preferred"));

            dispatcher.UnregisterProvider(preferredHandle);
            Assert.That(dispatcher.Diagnostics.ProviderId, Is.EqualTo("fallback"));
        }

        [Test]
        public void StaleRegistrationHandleCannotRemoveSameProviderAfterReset()
        {
            var dispatcher = new HybridInputDispatcher();
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var oldHandle = dispatcher.RegisterProvider(provider);
            dispatcher.RefreshEnvironment(FocusedEnvironment());

            dispatcher.Reset();
            var currentHandle = dispatcher.RegisterProvider(provider);
            dispatcher.RefreshEnvironment(FocusedEnvironment());
            dispatcher.UnregisterProvider(oldHandle);

            Assert.That(dispatcher.Diagnostics.ProviderId, Is.EqualTo("provider"));

            dispatcher.UnregisterProvider(currentHandle);
            Assert.That(dispatcher.Diagnostics.ProviderId, Is.Empty);
        }

        [Test]
        public void DuplicateProviderInstanceOrIdIsRejectedWithinOneEpoch()
        {
            var dispatcher = new HybridInputDispatcher();
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            dispatcher.RegisterProvider(provider);

            Assert.That(RegisterThrows(dispatcher, provider), Is.True);
            Assert.That(RegisterThrows(dispatcher,
                new FakeProvider("provider", HybridInputProviderMatch.Compatible)), Is.True);
        }

        [Test]
        public void ProviderSwitchStopsOldBeforeStartingNewAndDropsStaleCallbacks()
        {
            var calls = new List<string>();
            var useFirst = true;
            var first = new FakeProvider("first",
                _ => useFirst ? HybridInputProviderMatch.Exact : HybridInputProviderMatch.None,
                calls);
            var second = new FakeProvider("second",
                _ => useFirst ? HybridInputProviderMatch.None : HybridInputProviderMatch.Exact,
                calls);
            var dispatcher = new HybridInputDispatcher();
            dispatcher.RegisterProvider(first);
            dispatcher.RegisterProvider(second);
            var sink = new FakeFrameSink();
            using var registration = dispatcher.RegisterSink(sink);
            registration.AcquireFocus();
            dispatcher.RefreshEnvironment(FocusedEnvironment());
            calls.Clear();

            useFirst = false;
            dispatcher.RefreshEnvironment(FocusedEnvironment());
            first.Emit(HybridInputEvent.CommittedText("stale", 1d));
            dispatcher.Drain(1, 1d);

            Assert.That(calls,
                Is.EqualTo(new[]
                {
                    "first:session:end",
                    "first:ime:False",
                    "first:stop",
                    "second:start",
                    "second:session:start",
                    "second:ime:False"
                }));
            Assert.That(sink.Frames, Has.Count.EqualTo(1));
            Assert.That(sink.Frames[0].Events, Is.Empty);
            Assert.That(sink.Resets, Does.Contain(HybridInputResetReason.ProviderChanged));
        }

        [Test]
        public void ProviderSwitchWithoutFocusedSinkDoesNotWriteIme()
        {
            var calls = new List<string>();
            var useFirst = true;
            var first = new FakeProvider("first",
                _ => useFirst ? HybridInputProviderMatch.Exact : HybridInputProviderMatch.None,
                calls);
            var second = new FakeProvider("second",
                _ => useFirst ? HybridInputProviderMatch.None : HybridInputProviderMatch.Exact,
                calls);
            var dispatcher = new HybridInputDispatcher();
            dispatcher.RegisterProvider(first);
            dispatcher.RegisterProvider(second);
            dispatcher.RefreshEnvironment(FocusedEnvironment());
            calls.Clear();

            useFirst = false;
            dispatcher.RefreshEnvironment(FocusedEnvironment());

            Assert.That(calls, Is.EqualTo(new[] { "first:stop", "second:start" }));
        }

        [Test]
        public void ReplacingModuleRestartsSameProviderAndClearsOldOwnerState()
        {
            var firstObject = new GameObject();
            var secondObject = new GameObject();
            try
            {
                var firstModule = firstObject.AddComponent<StandaloneInputModule>();
                var secondModule = secondObject.AddComponent<StandaloneInputModule>();
                var calls = new List<string>();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact, calls: calls);
                var dispatcher = new HybridInputDispatcher();
                dispatcher.RegisterProvider(provider);
                var sink = new FakeFrameSink();
                using var registration = dispatcher.RegisterSink(sink);
                registration.AcquireFocus();
                dispatcher.RefreshEnvironment(new HybridInputEnvironment(firstModule, 1, true));
                provider.PressedKeys.Add(KeyCode.LeftShift);
                dispatcher.Drain(1, 1d);
                var firstFrame = sink.Frames[0];
                provider.ReplacePressedKeysOnCollect = false;
                provider.Emit(HybridInputEvent.Composition("old composition", 1.5d));
                calls.Clear();

                dispatcher.RefreshEnvironment(new HybridInputEnvironment(secondModule, 1, true));
                provider.EmitFromStart(0, HybridInputEvent.CommittedText("late", 1.6d));
                dispatcher.Drain(2, 2d);

                Assert.That(calls,
                    Is.EqualTo(new[]
                    {
                        "provider:session:end",
                        "provider:ime:False",
                        "provider:stop",
                        "provider:start",
                        "provider:session:start",
                        "provider:ime:False"
                    }));
                Assert.That(provider.StartCount, Is.EqualTo(2));
                Assert.That(sink.Resets, Does.Contain(HybridInputResetReason.ProviderChanged));
                Assert.That(sink.Frames, Has.Count.EqualTo(3));
                var releasedTailFrame = sink.Frames[1];
                Assert.That(releasedTailFrame.ProviderGeneration, Is.EqualTo(firstFrame.ProviderGeneration));
                Assert.That(releasedTailFrame.Events.Select(inputEvent => inputEvent.Text),
                    Is.EqualTo(new[] { "old composition" }));
                var secondSessionFrame = sink.Frames[2];
                Assert.That(secondSessionFrame.ProviderGeneration > firstFrame.ProviderGeneration, Is.True);
                Assert.That(secondSessionFrame.FocusEpoch > firstFrame.FocusEpoch, Is.True);
                Assert.That(secondSessionFrame.Events, Is.Empty);
                Assert.That(secondSessionFrame.IsKeyPressed(KeyCode.LeftShift), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void FocusEpochPreventsPendingTextFromLeakingToNextOwner()
        {
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var dispatcher = StartedDispatcher(provider);
            var firstSink = new FakeFrameSink();
            var secondSink = new FakeFrameSink();
            using var first = dispatcher.RegisterSink(firstSink);
            using var second = dispatcher.RegisterSink(secondSink);
            first.AcquireFocus();
            provider.Emit(HybridInputEvent.CommittedText("for-first", 1d));

            second.AcquireFocus();
            dispatcher.Drain(1, 1d);

            Assert.That(firstSink.Frames, Has.Count.EqualTo(1));
            Assert.That(firstSink.Frames[0].Events.Select(inputEvent => inputEvent.Text),
                Is.EqualTo(new[] { "for-first" }));
            Assert.That(secondSink.Frames, Has.Count.EqualTo(1));
            Assert.That(secondSink.Frames[0].Events, Is.Empty);
            Assert.That(firstSink.Resets, Does.Contain(HybridInputResetReason.FocusChanged));
        }

        [Test]
        public void UnfocusedTextIsDroppedRatherThanCached()
        {
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var dispatcher = StartedDispatcher(provider);
            provider.Emit(HybridInputEvent.CommittedText("unfocused", 1d));
            var sink = new FakeFrameSink();
            using var registration = dispatcher.RegisterSink(sink);
            registration.AcquireFocus();

            dispatcher.Drain(1, 1d);

            Assert.That(sink.Frames, Has.Count.EqualTo(1));
            Assert.That(sink.Frames[0].Events, Is.Empty);
        }

        [Test]
        public void ApplicationFocusLossCancelsPendingInputAndComposition()
        {
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var dispatcher = StartedDispatcher(provider);
            var sink = new FakeFrameSink();
            using var registration = dispatcher.RegisterSink(sink);
            registration.AcquireFocus();
            registration.SetImeEnabled(true);
            provider.Emit(HybridInputEvent.Composition("pending", 1d));

            dispatcher.RefreshEnvironment(new HybridInputEnvironment(null, 1, applicationFocused: false));
            dispatcher.Drain(1, 1d);

            Assert.That(sink.Frames, Is.Empty);
            Assert.That(sink.Resets, Does.Contain(HybridInputResetReason.ApplicationFocusLost));
            Assert.That(provider.ImeEnabled, Is.False);
        }

        [Test]
        public void OnlyFocusedOwnerCanControlIme()
        {
            var provider = new FakeProvider("provider",
                HybridInputProviderMatch.Exact,
                capabilities: HybridInputCapabilities.ImeControl);
            var dispatcher = StartedDispatcher(provider);
            using var first = dispatcher.RegisterSink(new FakeFrameSink());
            using var second = dispatcher.RegisterSink(new FakeFrameSink());
            first.AcquireFocus();

            Assert.That(second.SetImeEnabled(true), Is.False);
            Assert.That(first.SetImeEnabled(true), Is.True);
            Assert.That(first.SetImeCursorPosition(new Vector2(4f, 8f)), Is.True);
            Assert.That(provider.ImeEnabled, Is.True);
            Assert.That(provider.ImeCursorPosition, Is.EqualTo(new Vector2(4f, 8f)));
        }

        [Test]
        public void KeyStateAndEdgesShareOneSealedFrame()
        {
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var dispatcher = StartedDispatcher(provider);
            var sink = new FakeFrameSink();
            using var registration = dispatcher.RegisterSink(sink);
            registration.AcquireFocus();
            provider.PressedKeys.Add(KeyCode.LeftShift);
            provider.Emit(HybridInputEvent.KeyState(KeyCode.LeftShift, true, 1d));
            provider.Emit(HybridInputEvent.CommittedText("A", 1.1d));

            dispatcher.Drain(7, 2d);

            Assert.That(sink.Frames, Has.Count.EqualTo(1));
            Assert.That(sink.Frames[0].FrameIndex, Is.EqualTo(7));
            Assert.That(sink.Frames[0].Events, Has.Count.EqualTo(2));
            Assert.That(sink.Frames[0].IsKeyPressed(KeyCode.LeftShift), Is.True);
            Assert.That(sink.Frames[0].WasKeyPressed(KeyCode.LeftShift), Is.True);
            Assert.That(sink.Frames[0].WasKeyReleased(KeyCode.LeftShift), Is.False);
        }

        [Test]
        public void SealedFramesReusePreallocatedViewsForEmptyKeyAndTextFrames()
        {
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var dispatcher = StartedDispatcher(provider);
            var sink = new FakeFrameSink();
            using var registration = dispatcher.RegisterSink(sink);
            registration.AcquireFocus();

            dispatcher.Drain(1, 1d);
            provider.PressedKeys.Add(KeyCode.LeftShift);
            provider.Emit(HybridInputEvent.KeyState(KeyCode.LeftShift, true, 2d));
            dispatcher.Drain(2, 2d);
            provider.PressedKeys.Clear();
            provider.Emit(HybridInputEvent.CommittedText("text", 3d));
            dispatcher.Drain(3, 3d);

            Assert.That(sink.Frames, Has.Count.EqualTo(3));
            Assert.That(sink.Frames[0].Events, Is.Empty);
            Assert.That(sink.Frames[1].WasKeyPressed(KeyCode.LeftShift), Is.True);
            Assert.That(sink.Frames[1].IsKeyPressed(KeyCode.LeftShift), Is.True);
            Assert.That(sink.Frames[2].Events[0].Text, Is.EqualTo("text"));
            Assert.That(sink.FrameViews.All(frame => ReferenceEquals(frame, sink.FrameViews[0])), Is.True);
            Assert.That(sink.EventViews.All(events => ReferenceEquals(events, sink.EventViews[0])), Is.True);
            Assert.That(sink.PressedKeyViews.All(keys => ReferenceEquals(keys, sink.PressedKeyViews[0])), Is.True);
        }

        [Test]
        public void WarmedSealedFramesAllocateNothingForEmptyKeyAndTextFrames()
        {
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var dispatcher = StartedDispatcher(provider);
            var sink = new CountingFrameSink();
            using var registration = dispatcher.RegisterSink(sink);
            registration.AcquireFocus();

            dispatcher.Drain(1, 1d);
            provider.PressedKeys.Add(KeyCode.LeftShift);
            provider.Emit(HybridInputEvent.KeyState(KeyCode.LeftShift, true, 2d));
            dispatcher.Drain(2, 2d);
            provider.PressedKeys.Clear();
            provider.Emit(HybridInputEvent.CommittedText("warmup", 3d));
            dispatcher.Drain(3, 3d);

            var beforeEmpty = GC.GetAllocatedBytesForCurrentThread();
            dispatcher.Drain(4, 4d);
            var emptyBytes = GC.GetAllocatedBytesForCurrentThread() - beforeEmpty;

            provider.PressedKeys.Add(KeyCode.LeftShift);
            var beforeKey = GC.GetAllocatedBytesForCurrentThread();
            provider.Emit(HybridInputEvent.KeyState(KeyCode.LeftShift, true, 5d));
            dispatcher.Drain(5, 5d);
            var keyBytes = GC.GetAllocatedBytesForCurrentThread() - beforeKey;

            provider.PressedKeys.Clear();
            var beforeText = GC.GetAllocatedBytesForCurrentThread();
            provider.Emit(HybridInputEvent.CommittedText("text", 6d));
            dispatcher.Drain(6, 6d);
            var textBytes = GC.GetAllocatedBytesForCurrentThread() - beforeText;

            Assert.That(emptyBytes, Is.Zero);
            Assert.That(keyBytes, Is.Zero);
            Assert.That(textBytes, Is.Zero);
            Assert.That(sink.FrameCount, Is.EqualTo(6));
        }

        [Test]
        public void SealedFrameStorageBacksFullNotificationRingDuringReentrantDrain()
        {
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var dispatcher = StartedDispatcher(provider);
            var sink = new ReentrantFrameSink();
            using var registration = dispatcher.RegisterSink(sink);
            registration.AcquireFocus();
            var diagnosticCount = dispatcher.Diagnostics.DiagnosticCount;
            sink.OnFirstFrame = () =>
            {
                for (var i = 0; i < HybridInputDispatcher.MaxPendingNotifications; ++i)
                {
                    dispatcher.Drain(i + 2, i + 2d);
                }
            };

            dispatcher.Drain(1, 1d);

            Assert.That(sink.FrameCount, Is.EqualTo(HybridInputDispatcher.MaxPendingNotifications + 1));
            Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount));
        }

        [Test]
        public void ShortcutModifiersAndEdgesComeFromTheSameImmutableFrame()
        {
            var undoFrame = new HybridInputFrame(1,
                1d,
                "provider",
                1,
                1,
                new[] { HybridInputEvent.KeyState(KeyCode.Z, true, 1d) },
                new[] { KeyCode.LeftControl });
            var edgeWithoutModifier = new HybridInputFrame(2,
                2d,
                "provider",
                1,
                1,
                new[] { HybridInputEvent.KeyState(KeyCode.Z, true, 2d) },
                Array.Empty<KeyCode>());

            Assert.That(InputBoxShortcutUtil.IsUndoDown(undoFrame), Is.True);
            Assert.That(InputBoxShortcutUtil.IsUndoDown(edgeWithoutModifier), Is.False);
        }

        [Test]
        public void TextInputFrameStatePersistsCompositionAcrossEmptyFramesUntilReset()
        {
            var state = new TextInputFrameState();
            var frame = new HybridInputFrame(1,
                1d,
                "provider",
                1,
                1,
                new[]
                {
                    HybridInputEvent.Composition("preedit", 1d)
                },
                Array.Empty<KeyCode>());
            var emptyFrame = new HybridInputFrame(2,
                2d,
                "provider",
                1,
                1,
                Array.Empty<HybridInputEvent>(),
                Array.Empty<KeyCode>());

            Assert.That(state.Apply(frame), Is.Empty);
            Assert.That(state.CompositionText, Is.EqualTo("preedit"));
            Assert.That(state.Apply(emptyFrame), Is.Empty);
            Assert.That(state.CompositionText, Is.EqualTo("preedit"));
            state.Reset();
            Assert.That(state.CompositionText, Is.Empty);
        }

        [Test]
        public void TextInputFrameStateCommitOnlyEndsPreviousFrameComposition()
        {
            var state = new TextInputFrameState();
            state.Apply(InputFrame(HybridInputEvent.Composition("preedit", 1d)));

            var committedText = state.Apply(InputFrame(HybridInputEvent.CommittedText("候", 2d)));
            state.Apply(InputFrame());

            Assert.That(committedText, Is.EqualTo("候"));
            Assert.That(state.CompositionText, Is.Empty);
        }

        [Test]
        public void TextInputFrameStateCompositionThenCommitEndsComposition()
        {
            var state = new TextInputFrameState();

            var committedText = state.Apply(InputFrame(HybridInputEvent.Composition("old", 1d),
                HybridInputEvent.CommittedText("候", 1.1d)));

            Assert.That(committedText, Is.EqualTo("候"));
            Assert.That(state.CompositionText, Is.Empty);
        }

        [Test]
        public void TextInputFrameStateCommitThenCompositionKeepsRemainingPreedit()
        {
            var state = new TextInputFrameState();

            var committedText = state.Apply(InputFrame(HybridInputEvent.CommittedText("候", 1d),
                HybridInputEvent.Composition("remaining", 1.1d)));

            Assert.That(committedText, Is.EqualTo("候"));
            Assert.That(state.CompositionText, Is.EqualTo("remaining"));
        }

        [Test]
        public void TextInputFrameStateReducesInterleavedCompositionAndCommitsInOrder()
        {
            var state = new TextInputFrameState();

            var committedText = state.Apply(InputFrame(HybridInputEvent.Composition("old", 1d),
                HybridInputEvent.CommittedText("a", 1.1d),
                HybridInputEvent.Composition("mid", 1.2d),
                HybridInputEvent.CommittedText("b", 1.3d),
                HybridInputEvent.Composition("final", 1.4d)));

            Assert.That(committedText, Is.EqualTo("ab"));
            Assert.That(state.CompositionText, Is.EqualTo("final"));
        }

        [Test]
        public void DisposingFocusedOwnerResetsItAndPreventsFurtherDelivery()
        {
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var dispatcher = StartedDispatcher(provider);
            var sink = new FakeFrameSink();
            var registration = dispatcher.RegisterSink(sink);
            registration.AcquireFocus();

            registration.Dispose();
            provider.Emit(HybridInputEvent.CommittedText("ignored", 1d));
            dispatcher.Drain(1, 1d);

            Assert.That(sink.Resets, Does.Contain(HybridInputResetReason.OwnerDisabled));
            Assert.That(sink.Frames, Is.Empty);
        }

        [Test]
        public void ResetClearsProvidersBeforeDomainReloadRegistration()
        {
            var dispatcher = new HybridInputDispatcher();
            var first = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            dispatcher.RegisterProvider(first);
            dispatcher.RefreshEnvironment(FocusedEnvironment());

            dispatcher.Reset();
            var replacement = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            dispatcher.RegisterProvider(replacement);
            dispatcher.RefreshEnvironment(FocusedEnvironment());

            Assert.That(dispatcher.Diagnostics.ProviderId, Is.EqualTo("provider"));
            Assert.That(first.StartCount, Is.EqualTo(1));
            Assert.That(replacement.StartCount, Is.EqualTo(1));
        }

        [Test]
        public void RuntimeRegistrationTokenCannotCrossResetEpoch()
        {
            HybridInputRuntime.ResetState();
            try
            {
                var provider = new FakeProvider("runtime-provider", HybridInputProviderMatch.Exact);
                var oldToken = HybridInputRuntime.RegisterProvider(provider);

                HybridInputRuntime.ResetState();
                var currentToken = HybridInputRuntime.RegisterProvider(provider);
                oldToken.Dispose();

                Assert.That(RuntimeRegisterThrows(provider), Is.True);
                currentToken.Dispose();
                using var replacementToken = HybridInputRuntime.RegisterProvider(provider);
                oldToken.Dispose();
            }
            finally
            {
                HybridInputRuntime.ResetState();
            }
        }

        [Test]
        public void MultipleEventSystemsFailClosedBeforeProviderMatching()
        {
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var dispatcher = new HybridInputDispatcher();
            dispatcher.RegisterProvider(provider);

            dispatcher.RefreshEnvironment(new HybridInputEnvironment(null, 2, applicationFocused: true));

            Assert.That(dispatcher.Diagnostics.SelectionStatus, Is.EqualTo(HybridInputSelectionStatus.Conflict));
            Assert.That(provider.StartCount, Is.Zero);
        }

        [Test]
        public void EventsHaveMonotonicSequenceAndTimestamp()
        {
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var dispatcher = StartedDispatcher(provider);
            var sink = new FakeFrameSink();
            using var registration = dispatcher.RegisterSink(sink);
            registration.AcquireFocus();
            provider.Emit(HybridInputEvent.CommittedText("first", 2d));
            provider.Emit(HybridInputEvent.CommittedText("second", 1d));

            dispatcher.Drain(1, 3d);

            var events = sink.Frames[0].Events;
            Assert.That(events[0].Sequence > 0L, Is.True);
            Assert.That(events[1].Sequence > events[0].Sequence, Is.True);
            Assert.That(events[0].Timestamp, Is.EqualTo(2d));
            Assert.That(events[1].Timestamp, Is.EqualTo(2d));
        }

        [Test]
        public void HeldKeysAreReplacedByProviderSnapshotAndClearedAcrossFocusOwners()
        {
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var dispatcher = StartedDispatcher(provider);
            var firstSink = new FakeFrameSink();
            var secondSink = new FakeFrameSink();
            using var first = dispatcher.RegisterSink(firstSink);
            using var second = dispatcher.RegisterSink(secondSink);
            first.AcquireFocus();
            provider.PressedKeys.Add(KeyCode.LeftShift);
            dispatcher.Drain(1, 1d);

            provider.PressedKeys.Clear();
            second.AcquireFocus();
            dispatcher.Drain(2, 2d);

            Assert.That(firstSink.Frames[0].IsKeyPressed(KeyCode.LeftShift), Is.True);
            Assert.That(secondSink.Frames[0].IsKeyPressed(KeyCode.LeftShift), Is.False);
        }

        [Test]
        public void HeldKeysRefreshWithoutFocusWhileTextRemainsDropped()
        {
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var dispatcher = StartedDispatcher(provider);
            provider.PressedKeys.Add(KeyCode.LeftShift);
            provider.Emit(HybridInputEvent.CommittedText("unfocused", 1d));

            dispatcher.Drain(1, 1d);

            Assert.That(dispatcher.IsKeyPressed(KeyCode.LeftShift), Is.True);
            var sink = new FakeFrameSink();
            using var registration = dispatcher.RegisterSink(sink);
            registration.AcquireFocus();
            provider.PressedKeys.Clear();
            dispatcher.Drain(2, 2d);
            Assert.That(sink.Frames[0].Events, Is.Empty);
            Assert.That(dispatcher.IsKeyPressed(KeyCode.LeftShift), Is.False);

            provider.PressedKeys.Add(KeyCode.LeftShift);
            dispatcher.Drain(3, 3d);
            dispatcher.RefreshEnvironment(new HybridInputEnvironment(null, 1, applicationFocused: false));
            Assert.That(dispatcher.IsKeyPressed(KeyCode.LeftShift), Is.False);
        }

        [Test]
        public void FrameEventsAndHeldKeysAreReadOnlySnapshots()
        {
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var dispatcher = StartedDispatcher(provider);
            var sink = new FakeFrameSink();
            using var registration = dispatcher.RegisterSink(sink);
            registration.AcquireFocus();
            provider.PressedKeys.Add(KeyCode.LeftShift);
            provider.Emit(HybridInputEvent.CommittedText("text", 1d));

            dispatcher.Drain(1, 1d);

            var frame = sink.Frames[0];
            Assert.That(frame.Events is HybridInputEvent[], Is.False);
            Assert.That(frame.PressedKeys is KeyCode[], Is.False);
            Assert.That(frame.Events is IList<HybridInputEvent>, Is.False);
            Assert.That(frame.PressedKeys is IList<KeyCode>, Is.False);
        }

        [Test]
        public void ReleasingFocusedOwnerDisablesImeImmediately()
        {
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var dispatcher = StartedDispatcher(provider);
            using var registration = dispatcher.RegisterSink(new FakeFrameSink());
            registration.AcquireFocus();
            registration.SetImeEnabled(true);

            registration.ReleaseFocus();

            Assert.That(provider.ImeEnabled, Is.False);
        }

        [Test]
        public void SessionScopeDropsOldTailAtEveryReleaseHook()
        {
            var firstObject = new GameObject("first");
            var secondObject = new GameObject("second");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var firstSink = new LifecycleSink(firstObject, "first");
                var secondSink = new LifecycleSink(secondObject, "second");
                using var first = dispatcher.RegisterSink(firstSink);
                using var second = dispatcher.RegisterSink(secondSink);
                selection.Select(firstObject);
                first.AcquireFocus();
                var oldScope = provider.CaptureSession(0);
                firstSink.OnFrame = _ =>
                {
                    oldScope.Enqueue(HybridInputEvent.CommittedText("stale-during-flush", 2d));
                    dispatcher.NotifyValueChanged(firstSink, "A", sessionBound: true);
                };
                firstSink.OnValueChangedCallback = _ =>
                    oldScope.Enqueue(HybridInputEvent.CommittedText("stale-during-value", 2.1d));
                firstSink.OnEndEditCallback = _ =>
                    oldScope.Enqueue(HybridInputEvent.CommittedText("stale-during-end", 2.2d));
                secondSink.OnFocusGainedCallback = () =>
                {
                    oldScope.Enqueue(HybridInputEvent.CommittedText("stale-after-B", 2.3d));
                    oldScope.ReplacePressedKeys(new[] { KeyCode.LeftShift });
                    oldScope.ResetDeviceState();
                };
                oldScope.Enqueue(HybridInputEvent.CommittedText("A1", 1d));
                oldScope.Enqueue(HybridInputEvent.CommittedText("A2", 1.1d));

                first.ReleaseFocus();
                selection.Select(secondObject);
                second.AcquireFocus();
                var nextScope = provider.CaptureSession(1);
                nextScope.Enqueue(HybridInputEvent.CommittedText("B1", 3d));
                nextScope.ReplacePressedKeys(Array.Empty<KeyCode>());
                dispatcher.Drain(1, 3d);

                Assert.That(firstSink.Frames, Has.Count.EqualTo(1));
                Assert.That(firstSink.Frames[0].Events.Select(inputEvent => inputEvent.Text),
                    Is.EqualTo(new[] { "A1", "A2" }));
                Assert.That(secondSink.Frames, Has.Count.EqualTo(1));
                Assert.That(secondSink.Frames[0].Events.Select(inputEvent => inputEvent.Text),
                    Is.EqualTo(new[] { "B1" }));
                Assert.That(secondSink.Frames[0].Events[0].Sequence,
                    Is.EqualTo(firstSink.Frames[0].Events[1].Sequence + 1));
                Assert.That(secondSink.Resets, Is.Empty);
                Assert.That(dispatcher.IsKeyPressed(KeyCode.LeftShift), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void UnsupportedFocusedProviderFailsClosed()
        {
            var dispatcher = new HybridInputDispatcher();
            var provider = new UnscopedFocusedProvider();
            dispatcher.RegisterProvider(provider);
            dispatcher.RefreshEnvironment(FocusedEnvironment());
            var sink = new FakeFrameSink();
            using var registration = dispatcher.RegisterSink(sink);

            var acquired = registration.AcquireFocus();
            provider.Emit(HybridInputEvent.CommittedText("ignored", 1d));
            dispatcher.Drain(1, 1d);

            Assert.That(acquired, Is.False);
            Assert.That(registration.IsFocused, Is.False);
            Assert.That(sink.Frames, Is.Empty);
            Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                Is.EqualTo(HybridInputDiagnosticCode.SessionIsolationUnsupported));
        }

        [Test]
        public void SwitchingToUnsupportedProviderReleasesWithoutAutomaticReacquire()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                var useStrictProvider = true;
                var strictProvider = new FakeProvider("strict",
                    _ => useStrictProvider ? HybridInputProviderMatch.Exact : HybridInputProviderMatch.None);
                var unsupportedProvider = new UnscopedFocusedProvider
                {
                    MatchResult = _ => useStrictProvider
                        ? HybridInputProviderMatch.None
                        : HybridInputProviderMatch.Exact
                };
                var dispatcher = new HybridInputDispatcher();
                dispatcher.RegisterProvider(strictProvider);
                dispatcher.RegisterProvider(unsupportedProvider);
                dispatcher.RefreshEnvironment(FocusedEnvironment());
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                registration.AcquireFocus();

                useStrictProvider = false;
                dispatcher.RefreshEnvironment(FocusedEnvironment());

                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost" }));
                Assert.That(registration.AcquireFocus(), Is.False);
                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.SessionIsolationUnsupported));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void WrongThreadSessionCallbackCannotMutateQueueOrSequence()
        {
            var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var dispatcher = StartedDispatcher(provider);
            var sink = new FakeFrameSink();
            using var registration = dispatcher.RegisterSink(sink);
            registration.AcquireFocus();
            var scope = provider.CaptureSession(0);
            var thread = new System.Threading.Thread(() =>
            {
                scope.Enqueue(HybridInputEvent.CommittedText("wrong-thread", 1d));
                scope.ReplacePressedKeys(new[] { KeyCode.LeftShift });
            });

            thread.Start();
            thread.Join();
            scope.Enqueue(HybridInputEvent.CommittedText("main-thread", 2d));
            dispatcher.Drain(1, 2d);

            Assert.That(sink.Frames[0].Events, Has.Count.EqualTo(1));
            Assert.That(sink.Frames[0].Events[0].Text, Is.EqualTo("main-thread"));
            Assert.That(sink.Frames[0].Events[0].Sequence, Is.EqualTo(1));
            Assert.That(sink.Frames[0].IsKeyPressed(KeyCode.LeftShift), Is.False);
        }

        [Test]
        public void DispatcherResetPermanentlyInvalidatesCapturedSessionSink()
        {
            var firstProvider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            var dispatcher = StartedDispatcher(firstProvider);
            var firstSink = new FakeFrameSink();
            var firstRegistration = dispatcher.RegisterSink(firstSink);
            firstRegistration.AcquireFocus();
            var oldScope = firstProvider.CaptureSession(0);

            dispatcher.Reset();
            oldScope.Enqueue(HybridInputEvent.CommittedText("stale", 1d));
            oldScope.ReplacePressedKeys(new[] { KeyCode.LeftShift });
            var replacement = new FakeProvider("provider", HybridInputProviderMatch.Exact);
            dispatcher.RegisterProvider(replacement);
            dispatcher.RefreshEnvironment(FocusedEnvironment());
            var secondSink = new FakeFrameSink();
            using var secondRegistration = dispatcher.RegisterSink(secondSink);
            secondRegistration.AcquireFocus();
            replacement.Emit(HybridInputEvent.CommittedText("current", 2d));
            dispatcher.Drain(1, 2d);

            Assert.That(firstSink.Frames, Is.Empty);
            Assert.That(secondSink.Frames[0].Events, Has.Count.EqualTo(1));
            Assert.That(secondSink.Frames[0].Events[0].Text, Is.EqualTo("current"));
            Assert.That(secondSink.Frames[0].Events[0].Sequence, Is.EqualTo(1));
            Assert.That(secondSink.Frames[0].IsKeyPressed(KeyCode.LeftShift), Is.False);
            firstRegistration.Dispose();
        }

        [Test]
        public void InputRingOverflowDropsWholeBatchAndReleasesSession()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                registration.AcquireFocus();
                var scope = provider.CaptureSession(0);

                provider.PressedKeys.Add(KeyCode.LeftShift);
                dispatcher.Drain(1, 1d);
                provider.ReplacePressedKeysOnCollect = false;
                scope.ReplacePressedKeys(new[] { KeyCode.RightShift });
                Assert.That(dispatcher.IsKeyPressed(KeyCode.LeftShift), Is.True);
                Assert.That(dispatcher.IsKeyPressed(KeyCode.RightShift), Is.False);

                for (var i = 0; i < HybridInputDispatcher.MaxPendingInputEventsPerFocusSession; ++i)
                {
                    scope.Enqueue(HybridInputEvent.CommittedText(i.ToString(), i));
                }
                Assert.That(dispatcher.IsKeyPressed(KeyCode.LeftShift), Is.True);
                Assert.That(dispatcher.IsKeyPressed(KeyCode.RightShift), Is.False);
                scope.Enqueue(HybridInputEvent.CommittedText("overflow", 129d));

                Assert.That(sink.Frames, Has.Count.EqualTo(1));
                Assert.That(sink.Frames[0].IsKeyPressed(KeyCode.LeftShift), Is.True);
                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost" }));
                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.InputEventBufferOverflow));
                Assert.That(dispatcher.IsKeyPressed(KeyCode.LeftShift), Is.False);
                Assert.That(dispatcher.IsKeyPressed(KeyCode.RightShift), Is.False);
                scope.Enqueue(HybridInputEvent.CommittedText("stale", 130d));

                provider.PressedKeys.Clear();
                provider.ReplacePressedKeysOnCollect = true;
                Assert.That(registration.AcquireFocus(), Is.True);
                dispatcher.Drain(2, 2d);
                Assert.That(sink.Frames, Has.Count.EqualTo(2));
                Assert.That(sink.Frames[1].Events, Is.Empty);
                Assert.That(sink.Frames[1].PressedKeys, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void TemporarilyUnavailableConsumerKeepsAndResumesSameFocusSession()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner")
                {
                    CanConsumeInputNow = false
                };
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);

                Assert.That(registration.AcquireFocus(), Is.True);
                var scope = provider.CaptureSession(0);
                scope.Enqueue(HybridInputEvent.CommittedText("paused", 1d));
                dispatcher.Drain(1, 1d);

                Assert.That(registration.IsFocused, Is.True);
                Assert.That(sink.Frames, Is.Empty);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained" }));
                Assert.That(provider.BeginCount, Is.EqualTo(1));
                Assert.That(provider.EndCount, Is.Zero);

                sink.CanConsumeInputNow = true;
                scope.Enqueue(HybridInputEvent.CommittedText("resumed", 2d));
                dispatcher.Drain(2, 2d);

                Assert.That(registration.IsFocused, Is.True);
                Assert.That(sink.Frames, Has.Count.EqualTo(1));
                Assert.That(sink.Frames[0].Events.Select(inputEvent => inputEvent.Text),
                    Is.EqualTo(new[] { "resumed" }));
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained" }));
                Assert.That(provider.BeginCount, Is.EqualTo(1));
                Assert.That(provider.EndCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void FocusReentrancyUsesStickySessionBoundaryWithoutRecursion()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                var firstGained = true;
                sink.OnFocusGainedCallback = () =>
                {
                    if (!firstGained)
                    {
                        return;
                    }
                    firstGained = false;
                    registration.ReleaseFocus();
                    registration.AcquireFocus();
                };

                registration.AcquireFocus();

                Assert.That(sink.Calls,
                    Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost", "owner:Gained" }));
                Assert.That(registration.IsFocused, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ReleaseThenNotificationOverflowCompletesTerminalBeforeAcquireReturns()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                var diagnosticCount = dispatcher.Diagnostics.DiagnosticCount;
                var ownerResetCommitted = false;
                var resetSawUnfocused = false;
                var endSawCommittedReset = false;
                var lostSawCommittedReset = false;
                HybridInputResetReason? observedResetReason = null;
                sink.OnResetCallback = reason =>
                {
                    observedResetReason = reason;
                    resetSawUnfocused = !registration.IsFocused;
                    ownerResetCommitted = true;
                };
                sink.OnEndEditCallback = _ => endSawCommittedReset = ownerResetCommitted;
                sink.OnFocusLostCallback = () => lostSawCommittedReset = ownerResetCommitted;
                sink.OnFocusGainedCallback = () =>
                {
                    registration.ReleaseFocus();
                    for (var i = 0; i <= HybridInputDispatcher.MaxPendingNotifications; ++i)
                    {
                        dispatcher.NotifyValueChanged(sink, i.ToString(), sessionBound: false);
                    }
                };

                Assert.That(registration.AcquireFocus(), Is.False);

                var oldScope = provider.CaptureSession(0);
                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost" }));
                Assert.That(sink.Resets, Is.EqualTo(new[] { HybridInputResetReason.FocusChanged }));
                Assert.That(observedResetReason, Is.EqualTo(HybridInputResetReason.FocusChanged));
                Assert.That(resetSawUnfocused, Is.True);
                Assert.That(endSawCommittedReset, Is.True);
                Assert.That(lostSawCommittedReset, Is.True);
                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.NotificationBufferOverflow));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
                oldScope.Enqueue(HybridInputEvent.CommittedText("stale", 1d));
                dispatcher.Drain(1, 1d);
                Assert.That(sink.Frames, Is.Empty);

                sink.OnFocusGainedCallback = null;
                sink.OnResetCallback = null;
                sink.OnEndEditCallback = null;
                sink.OnFocusLostCallback = null;
                Assert.That(registration.AcquireFocus(), Is.True);
                Assert.That(sink.Calls,
                    Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost", "owner:Gained" }));
                Assert.That(provider.BeginCount, Is.EqualTo(2));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ReleaseThenListenerExceptionCompletesTerminalBeforeAcquireReturns()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                var diagnosticCount = dispatcher.Diagnostics.DiagnosticCount;
                var ownerResetCommitted = false;
                var resetSawUnfocused = false;
                var endSawCommittedReset = false;
                var lostSawCommittedReset = false;
                HybridInputResetReason? observedResetReason = null;
                sink.OnResetCallback = reason =>
                {
                    observedResetReason = reason;
                    resetSawUnfocused = !registration.IsFocused;
                    ownerResetCommitted = true;
                    throw new InvalidOperationException("owner reset");
                };
                sink.OnEndEditCallback = _ => endSawCommittedReset = ownerResetCommitted;
                sink.OnFocusLostCallback = () => lostSawCommittedReset = ownerResetCommitted;
                sink.OnFocusGainedCallback = () =>
                {
                    registration.ReleaseFocus();
                    throw new InvalidOperationException("focus gained");
                };

                Assert.That(registration.AcquireFocus(), Is.False);

                var oldScope = provider.CaptureSession(0);
                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost" }));
                Assert.That(sink.Resets, Is.EqualTo(new[] { HybridInputResetReason.FocusChanged }));
                Assert.That(observedResetReason, Is.EqualTo(HybridInputResetReason.FocusChanged));
                Assert.That(resetSawUnfocused, Is.True);
                Assert.That(endSawCommittedReset, Is.True);
                Assert.That(lostSawCommittedReset, Is.True);
                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.ListenerException));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
                oldScope.Enqueue(HybridInputEvent.CommittedText("stale", 1d));
                dispatcher.Drain(1, 1d);
                Assert.That(sink.Frames, Is.Empty);

                sink.OnFocusGainedCallback = null;
                sink.OnResetCallback = null;
                sink.OnEndEditCallback = null;
                sink.OnFocusLostCallback = null;
                Assert.That(registration.AcquireFocus(), Is.True);
                Assert.That(sink.Calls,
                    Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost", "owner:Gained" }));
                Assert.That(provider.BeginCount, Is.EqualTo(2));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ReleaseThenWorkLimitCompletesTerminalBeforeAcquireReturns()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                var diagnosticCount = dispatcher.Diagnostics.DiagnosticCount;
                var ownerResetCommitted = false;
                var resetSawUnfocused = false;
                var endSawCommittedReset = false;
                var lostSawCommittedReset = false;
                HybridInputResetReason? observedResetReason = null;
                sink.OnResetCallback = reason =>
                {
                    observedResetReason = reason;
                    resetSawUnfocused = !registration.IsFocused;
                    ownerResetCommitted = true;
                };
                sink.OnEndEditCallback = _ => endSawCommittedReset = ownerResetCommitted;
                sink.OnFocusLostCallback = () => lostSawCommittedReset = ownerResetCommitted;
                sink.OnFocusGainedCallback = () =>
                {
                    registration.ReleaseFocus();
                    dispatcher.NotifyValueChanged(sink, "A", sessionBound: false);
                };
                sink.OnValueChangedCallback = value => dispatcher.NotifyValueChanged(sink,
                    value == "A" ? "B" : "A",
                    sessionBound: false);

                Assert.That(registration.AcquireFocus(), Is.False);

                var oldScope = provider.CaptureSession(0);
                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls.Count(call => call == "owner:End"), Is.EqualTo(1));
                Assert.That(sink.Calls.Count(call => call == "owner:Lost"), Is.EqualTo(1));
                Assert.That(sink.Calls[^2], Is.EqualTo("owner:End"));
                Assert.That(sink.Calls[^1], Is.EqualTo("owner:Lost"));
                Assert.That(sink.Resets, Is.EqualTo(new[] { HybridInputResetReason.FocusChanged }));
                Assert.That(observedResetReason, Is.EqualTo(HybridInputResetReason.FocusChanged));
                Assert.That(resetSawUnfocused, Is.True);
                Assert.That(endSawCommittedReset, Is.True);
                Assert.That(lostSawCommittedReset, Is.True);
                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.WorkLimitExceeded));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
                oldScope.Enqueue(HybridInputEvent.CommittedText("stale", 1d));
                dispatcher.Drain(1, 1d);
                Assert.That(sink.Frames, Is.Empty);

                sink.OnFocusGainedCallback = null;
                sink.OnValueChangedCallback = null;
                sink.OnResetCallback = null;
                sink.OnEndEditCallback = null;
                sink.OnFocusLostCallback = null;
                Assert.That(registration.AcquireFocus(), Is.True);
                Assert.That(sink.Calls[^1], Is.EqualTo("owner:Gained"));
                Assert.That(provider.BeginCount, Is.EqualTo(2));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void AbortEmergencyResetsTextInputNativeStateBeforeTerminal()
        {
            var owner = new GameObject("owner");
            var inputObject = new GameObject("text-input", typeof(RectTransform));
            inputObject.SetActive(false);
            try
            {
                using var selection = new SelectionFixture();
                var input = inputObject.AddComponent<TextInput>();
                InvokeRecreateInputBox(input);
                input.SetTextWithoutNotify("committed");
                var inputBox = GetNativeInputBox(input);
                inputBox.SetFocused(true);
                inputBox.SetCaretVisible(true);
                Assert.That(inputBox.SetComposition("preedit"), Is.True);
                SetTextInputPrivateField(input, "compositionActive", true);
                SetTextInputPrivateField(input, "lastCompositionText", "preedit");
                SetTextInputPrivateField(input, "caretVisible", true);

                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                var diagnosticCount = dispatcher.Diagnostics.DiagnosticCount;
                var resetCommitted = false;
                var resetSawUnfocused = false;
                var resetStateCleared = false;
                var endSawCommittedReset = false;
                var endStateCleared = false;
                var lostSawCommittedReset = false;
                var lostStateCleared = false;
                HybridInputResetReason? observedResetReason = null;
                sink.OnResetCallback = reason =>
                {
                    observedResetReason = reason;
                    resetSawUnfocused = !registration.IsFocused;
                    InvokeTextInputReset(input, reason);
                    resetCommitted = true;
                    resetStateCleared = IsTextInputResetStateCleared(input, inputBox);
                };
                sink.OnEndEditCallback = _ =>
                {
                    endSawCommittedReset = resetCommitted;
                    endStateCleared = IsTextInputResetStateCleared(input, inputBox);
                };
                sink.OnFocusLostCallback = () =>
                {
                    lostSawCommittedReset = resetCommitted;
                    lostStateCleared = IsTextInputResetStateCleared(input, inputBox);
                };
                sink.OnFocusGainedCallback = () =>
                {
                    registration.ReleaseFocus();
                    throw new InvalidOperationException("focus gained");
                };

                Assert.That(registration.AcquireFocus(), Is.False);

                Assert.That(resetCommitted, Is.True);
                Assert.That(sink.Resets, Is.EqualTo(new[] { HybridInputResetReason.FocusChanged }));
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost" }));
                Assert.That(observedResetReason, Is.EqualTo(HybridInputResetReason.FocusChanged));
                Assert.That(resetSawUnfocused, Is.True);
                Assert.That(resetStateCleared, Is.True);
                Assert.That(endSawCommittedReset, Is.True);
                Assert.That(endStateCleared, Is.True);
                Assert.That(lostSawCommittedReset, Is.True);
                Assert.That(lostStateCleared, Is.True);
                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.ListenerException));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(inputObject);
            }
        }

        [Test]
        public void ResetUnregisterCannotCancelNormalReleaseTerminal()
        {
            var firstObject = new GameObject("first");
            var secondObject = new GameObject("second");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var calls = new List<string>();
                var firstSink = new LifecycleSink(firstObject, "first", calls);
                var firstRegistration = dispatcher.RegisterSink(firstSink);
                selection.Select(firstObject);
                firstRegistration.AcquireFocus();
                var diagnosticCount = dispatcher.Diagnostics.DiagnosticCount;
                var resetSawUnfocused = false;
                HybridInputResetReason? observedResetReason = null;
                firstSink.OnResetCallback = reason =>
                {
                    observedResetReason = reason;
                    resetSawUnfocused = !firstRegistration.IsFocused;
                    firstRegistration.Dispose();
                };

                firstRegistration.ReleaseFocus();

                Assert.That(observedResetReason, Is.EqualTo(HybridInputResetReason.FocusChanged));
                Assert.That(resetSawUnfocused, Is.True);
                Assert.That(firstSink.Resets, Is.EqualTo(new[] { HybridInputResetReason.FocusChanged }));
                Assert.That(calls, Is.EqualTo(new[] { "first:Gained", "first:End", "first:Lost" }));
                Assert.That(GetRegisteredSinkCount(dispatcher), Is.Zero);
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount));

                var secondSink = new LifecycleSink(secondObject, "second", calls);
                using var secondRegistration = dispatcher.RegisterSink(secondSink);
                selection.Select(secondObject);
                Assert.That(secondRegistration.AcquireFocus(), Is.True);
                Assert.That(calls,
                    Is.EqualTo(new[] { "first:Gained", "first:End", "first:Lost", "second:Gained" }));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void ResetUnregisterCannotCancelAbortedReleaseTerminal()
        {
            var firstObject = new GameObject("first");
            var secondObject = new GameObject("second");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var calls = new List<string>();
                var firstSink = new LifecycleSink(firstObject, "first", calls);
                var firstRegistration = dispatcher.RegisterSink(firstSink);
                selection.Select(firstObject);
                var diagnosticCount = dispatcher.Diagnostics.DiagnosticCount;
                var resetSawUnfocused = false;
                HybridInputResetReason? observedResetReason = null;
                firstSink.OnResetCallback = reason =>
                {
                    observedResetReason = reason;
                    resetSawUnfocused = !firstRegistration.IsFocused;
                    firstRegistration.Dispose();
                };
                firstSink.OnFocusGainedCallback = () =>
                {
                    firstRegistration.ReleaseFocus();
                    for (var i = 0; i <= HybridInputDispatcher.MaxPendingNotifications; ++i)
                    {
                        dispatcher.NotifyValueChanged(firstSink, i.ToString(), sessionBound: false);
                    }
                };

                Assert.That(firstRegistration.AcquireFocus(), Is.False);

                Assert.That(observedResetReason, Is.EqualTo(HybridInputResetReason.FocusChanged));
                Assert.That(resetSawUnfocused, Is.True);
                Assert.That(firstSink.Resets, Is.EqualTo(new[] { HybridInputResetReason.FocusChanged }));
                Assert.That(calls, Is.EqualTo(new[] { "first:Gained", "first:End", "first:Lost" }));
                Assert.That(GetRegisteredSinkCount(dispatcher), Is.Zero);
                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.NotificationBufferOverflow));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));

                var secondSink = new LifecycleSink(secondObject, "second", calls);
                using var secondRegistration = dispatcher.RegisterSink(secondSink);
                selection.Select(secondObject);
                Assert.That(secondRegistration.AcquireFocus(), Is.True);
                Assert.That(calls,
                    Is.EqualTo(new[] { "first:Gained", "first:End", "first:Lost", "second:Gained" }));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void OrdinaryFocusSwitchCompletesTerminalBundleBeforeNextGained()
        {
            var firstObject = new GameObject("first");
            var secondObject = new GameObject("second");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var calls = new List<string>();
                var firstSink = new LifecycleSink(firstObject, "first", calls);
                var secondSink = new LifecycleSink(secondObject, "second", calls);
                using var first = dispatcher.RegisterSink(firstSink);
                using var second = dispatcher.RegisterSink(secondSink);

                selection.Select(firstObject);
                first.AcquireFocus();
                selection.Select(secondObject);
                second.AcquireFocus();

                Assert.That(calls,
                    Is.EqualTo(new[] { "first:Gained", "first:End", "first:Lost", "second:Gained" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void EndEditRefocusRunsAfterFrozenFocusLost()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                registration.AcquireFocus();
                sink.OnEndEditCallback = _ => registration.AcquireFocus();

                registration.ReleaseFocus();

                Assert.That(sink.Calls,
                    Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost", "owner:Gained" }));
                Assert.That(registration.IsFocused, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void OrdinaryOverflowDuringUnregisterCannotClearTerminalBundle()
        {
            var firstObject = new GameObject("first");
            var secondObject = new GameObject("second");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var firstSink = new LifecycleSink(firstObject, "first");
                var firstRegistration = dispatcher.RegisterSink(firstSink);
                selection.Select(firstObject);
                firstRegistration.AcquireFocus();
                var diagnosticCount = dispatcher.Diagnostics.DiagnosticCount;
                firstSink.OnResetCallback = _ =>
                {
                    for (var i = 0; i <= HybridInputDispatcher.MaxPendingNotifications; ++i)
                    {
                        dispatcher.NotifyValueChanged(firstSink, i.ToString(), sessionBound: false);
                    }
                };

                firstRegistration.Dispose();

                Assert.That(firstSink.Calls, Is.EqualTo(new[] { "first:Gained", "first:End", "first:Lost" }));
                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.NotificationBufferOverflow));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));

                var secondSink = new LifecycleSink(secondObject, "second");
                using var secondRegistration = dispatcher.RegisterSink(secondSink);
                selection.Select(secondObject);
                Assert.That(secondRegistration.AcquireFocus(), Is.True);
                Assert.That(secondSink.Calls, Is.EqualTo(new[] { "second:Gained" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void BeginFocusSessionFailureCleansPartialBindingAndCanRecover()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact)
                {
                    BeginFailuresRemaining = 1
                };
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                var diagnosticCount = dispatcher.Diagnostics.DiagnosticCount;

                Assert.That(registration.AcquireFocus(), Is.False);

                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls, Is.Empty);
                Assert.That(provider.BeginCount, Is.EqualTo(1));
                Assert.That(provider.EndCount, Is.EqualTo(1));
                Assert.That(provider.HasBoundSession, Is.False);
                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.FocusSessionStartFailed));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
                var failedScope = provider.CaptureSession(0);
                failedScope.Enqueue(HybridInputEvent.CommittedText("stale", 1d));

                Assert.That(registration.AcquireFocus(), Is.True);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained" }));
                Assert.That(provider.BeginCount, Is.EqualTo(2));
                Assert.That(provider.HasBoundSession, Is.True);
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void EndFocusSessionFailureStillCompletesTerminalBundleAndCanRecover()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                registration.AcquireFocus();
                var oldScope = provider.CaptureSession(0);
                var diagnosticCount = dispatcher.Diagnostics.DiagnosticCount;
                provider.EndFailuresRemaining = 1;

                registration.ReleaseFocus();

                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost" }));
                Assert.That(sink.Resets, Is.EqualTo(new[] { HybridInputResetReason.FocusChanged }));
                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.FocusSessionEndFailed));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
                oldScope.Enqueue(HybridInputEvent.CommittedText("stale", 1d));

                Assert.That(registration.AcquireFocus(), Is.True);
                Assert.That(sink.Calls,
                    Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost", "owner:Gained" }));
                Assert.That(provider.BeginCount, Is.EqualTo(2));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DeviceRestartBeginFailureReleasesSessionAndCanRecover()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                registration.AcquireFocus();
                var oldScope = provider.CaptureSession(0);
                var diagnosticCount = dispatcher.Diagnostics.DiagnosticCount;
                provider.BeginFailuresRemaining = 1;

                oldScope.ResetDeviceState();

                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost" }));
                Assert.That(sink.Resets,
                    Is.EqualTo(new[]
                    {
                        HybridInputResetReason.DeviceChanged,
                        HybridInputResetReason.FocusSessionFailure
                    }));
                Assert.That(provider.BeginCount, Is.EqualTo(2));
                Assert.That(provider.EndCount, Is.EqualTo(2));
                Assert.That(provider.HasBoundSession, Is.False);
                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.FocusSessionRestartFailed));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
                oldScope.Enqueue(HybridInputEvent.CommittedText("stale", 1d));
                provider.CaptureSession(1).Enqueue(HybridInputEvent.CommittedText("failed", 2d));

                Assert.That(registration.AcquireFocus(), Is.True);
                Assert.That(sink.Calls,
                    Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost", "owner:Gained" }));
                Assert.That(provider.BeginCount, Is.EqualTo(3));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DeviceRestartEndFailureReleasesSessionAndCanRecover()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                registration.AcquireFocus();
                var oldScope = provider.CaptureSession(0);
                var diagnosticCount = dispatcher.Diagnostics.DiagnosticCount;
                provider.EndFailuresRemaining = 1;

                oldScope.ResetDeviceState();

                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost" }));
                Assert.That(sink.Resets,
                    Is.EqualTo(new[] { HybridInputResetReason.FocusSessionFailure }));
                Assert.That(provider.BeginCount, Is.EqualTo(1));
                Assert.That(provider.EndCount, Is.EqualTo(1));
                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.FocusSessionEndFailed));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
                oldScope.Enqueue(HybridInputEvent.CommittedText("stale", 1d));
                dispatcher.Drain(1, 1d);
                Assert.That(sink.Frames, Is.Empty);

                Assert.That(registration.AcquireFocus(), Is.True);
                Assert.That(sink.Calls,
                    Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost", "owner:Gained" }));
                Assert.That(provider.BeginCount, Is.EqualTo(2));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DeviceRestartResetExceptionFailsClosedAndCanRecover()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner")
                {
                    OnResetCallback = reason =>
                    {
                        if (reason == HybridInputResetReason.DeviceChanged)
                        {
                            throw new InvalidOperationException("device reset");
                        }
                    }
                };
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                registration.AcquireFocus();
                var oldScope = provider.CaptureSession(0);
                var diagnosticCount = dispatcher.Diagnostics.DiagnosticCount;

                oldScope.ResetDeviceState();

                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost" }));
                Assert.That(sink.Resets, Is.EqualTo(new[] { HybridInputResetReason.DeviceChanged }));
                Assert.That(provider.BeginCount, Is.EqualTo(1));
                Assert.That(provider.EndCount, Is.EqualTo(1));
                Assert.That(provider.HasBoundSession, Is.False);
                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.ListenerException));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
                oldScope.Enqueue(HybridInputEvent.CommittedText("stale", 1d));
                dispatcher.Drain(1, 1d);
                Assert.That(sink.Frames, Is.Empty);

                sink.OnResetCallback = null;
                Assert.That(registration.AcquireFocus(), Is.True);
                Assert.That(sink.Calls,
                    Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost", "owner:Gained" }));
                Assert.That(provider.BeginCount, Is.EqualTo(2));
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount + 1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DeviceRestartResetReleaseDoesNotCreateReplacementSession()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                registration.AcquireFocus();
                var oldScope = provider.CaptureSession(0);
                var diagnosticCount = dispatcher.Diagnostics.DiagnosticCount;
                sink.OnResetCallback = reason =>
                {
                    if (reason == HybridInputResetReason.DeviceChanged)
                    {
                        registration.ReleaseFocus();
                    }
                };

                oldScope.ResetDeviceState();

                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost" }));
                Assert.That(sink.Resets,
                    Is.EqualTo(new[]
                    {
                        HybridInputResetReason.DeviceChanged,
                        HybridInputResetReason.FocusChanged
                    }));
                Assert.That(provider.BeginCount, Is.EqualTo(1));
                Assert.That(provider.EndCount, Is.EqualTo(1));
                Assert.That(provider.HasBoundSession, Is.False);
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount));
                oldScope.Enqueue(HybridInputEvent.CommittedText("stale", 1d));
                dispatcher.Drain(1, 1d);
                Assert.That(sink.Frames, Is.Empty);

                sink.OnResetCallback = null;
                Assert.That(registration.AcquireFocus(), Is.True);
                Assert.That(provider.BeginCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DeviceRestartResetTerminalReacquirePreservesNewSessionIdentity()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                registration.AcquireFocus();
                var oldScope = provider.CaptureSession(0);
                var diagnosticCount = dispatcher.Diagnostics.DiagnosticCount;
                sink.OnResetCallback = reason =>
                {
                    if (reason == HybridInputResetReason.DeviceChanged)
                    {
                        registration.ReleaseFocus();
                    }
                };
                sink.OnEndEditCallback = _ => registration.AcquireFocus();

                oldScope.ResetDeviceState();

                Assert.That(registration.IsFocused, Is.True);
                Assert.That(sink.Calls,
                    Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost", "owner:Gained" }));
                Assert.That(provider.BeginCount, Is.EqualTo(2));
                Assert.That(provider.EndCount, Is.EqualTo(1));
                Assert.That(provider.HasBoundSession, Is.True);
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount));
                oldScope.Enqueue(HybridInputEvent.CommittedText("stale", 1d));
                provider.CaptureSession(1).Enqueue(HybridInputEvent.CommittedText("current", 2d));
                dispatcher.Drain(1, 2d);
                Assert.That(sink.Frames, Has.Count.EqualTo(1));
                Assert.That(sink.Frames[0].Events.Select(inputEvent => inputEvent.Text),
                    Is.EqualTo(new[] { "current" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DeviceRestartResetSelectionChangeDoesNotCreateReplacementSession()
        {
            var owner = new GameObject("owner");
            var other = new GameObject("other");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                registration.AcquireFocus();
                var oldScope = provider.CaptureSession(0);
                var diagnosticCount = dispatcher.Diagnostics.DiagnosticCount;
                sink.OnResetCallback = reason =>
                {
                    if (reason == HybridInputResetReason.DeviceChanged)
                    {
                        selection.Select(other);
                    }
                };

                oldScope.ResetDeviceState();

                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost" }));
                Assert.That(sink.Resets,
                    Is.EqualTo(new[]
                    {
                        HybridInputResetReason.DeviceChanged,
                        HybridInputResetReason.FocusChanged
                    }));
                Assert.That(provider.BeginCount, Is.EqualTo(1));
                Assert.That(provider.EndCount, Is.EqualTo(1));
                Assert.That(provider.HasBoundSession, Is.False);
                Assert.That(dispatcher.Diagnostics.DiagnosticCount, Is.EqualTo(diagnosticCount));
                oldScope.Enqueue(HybridInputEvent.CommittedText("stale", 1d));
                dispatcher.Drain(1, 1d);
                Assert.That(sink.Frames, Is.Empty);

                sink.OnResetCallback = null;
                selection.Select(owner);
                Assert.That(registration.AcquireFocus(), Is.True);
                Assert.That(provider.BeginCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void LifecycleFocusAdmissionRequiresActiveSelectedEventSystemOwner()
        {
            var owner = new GameObject("owner");
            var other = new GameObject("other");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);

                selection.Select(owner);
                sink.IsActiveAndEnabled = false;
                Assert.That(registration.AcquireFocus(), Is.False);
                sink.IsActiveAndEnabled = true;
                selection.Select(other);
                Assert.That(registration.AcquireFocus(), Is.False);
                selection.SetActive(false);
                Assert.That(registration.AcquireFocus(), Is.False);

                selection.SetActive(true);
                selection.Select(owner);
                Assert.That(registration.AcquireFocus(), Is.True);
                selection.SetActive(false);
                dispatcher.Drain(1, 1d);
                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void LifecycleFocusAdmissionFailsWithoutActiveEventSystem()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                selection.SetActive(false);
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);

                Assert.That(registration.AcquireFocus(), Is.False);
                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void OuterDrainReleasesOwnerWhenSelectionDriftsWithoutDeselect()
        {
            var owner = new GameObject("owner");
            var other = new GameObject("other");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                registration.AcquireFocus();

                selection.Select(other);
                dispatcher.Drain(1, 1d);

                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void FrameListenerSelectionDriftReleasesBeforeOuterDrainReturns()
        {
            var owner = new GameObject("owner");
            var other = new GameObject("other");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner")
                {
                    OnFrame = _ => selection.Select(other)
                };
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                registration.AcquireFocus();

                dispatcher.Drain(1, 1d);

                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void ApplicationFocusLossKeepsOwnerUntilRestoreReconcilesSelection()
        {
            var owner = new GameObject("owner");
            var other = new GameObject("other");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                registration.AcquireFocus();

                dispatcher.RefreshEnvironment(new HybridInputEnvironment(null, 1, false));
                selection.Select(other);
                dispatcher.Drain(1, 1d);
                Assert.That(registration.IsFocused, Is.True);

                dispatcher.RefreshEnvironment(FocusedEnvironment());

                Assert.That(registration.IsFocused, Is.False);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained", "owner:End", "owner:Lost" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void ApplicationFocusRestoreWithSameSelectionKeepsSameSessionWithoutRepeatedGained()
        {
            var owner = new GameObject("owner");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var sink = new LifecycleSink(owner, "owner");
                using var registration = dispatcher.RegisterSink(sink);
                selection.Select(owner);
                registration.AcquireFocus();
                var scope = provider.CaptureSession(0);

                dispatcher.RefreshEnvironment(new HybridInputEnvironment(null, 1, false));
                Assert.That(registration.IsFocused, Is.True);
                dispatcher.RefreshEnvironment(FocusedEnvironment());
                scope.Enqueue(HybridInputEvent.CommittedText("resumed", 1d));
                dispatcher.Drain(1, 1d);

                Assert.That(registration.IsFocused, Is.True);
                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Gained" }));
                Assert.That(provider.BeginCount, Is.EqualTo(1));
                Assert.That(provider.EndCount, Is.Zero);
                Assert.That(sink.Frames, Has.Count.EqualTo(1));
                Assert.That(sink.Frames[0].Events.Select(inputEvent => inputEvent.Text),
                    Is.EqualTo(new[] { "resumed" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ValueNotificationLoopStopsAtWorkLimitAndGuardRecovers()
        {
            var owner = new GameObject("owner");
            try
            {
                var dispatcher = new HybridInputDispatcher();
                var sink = new LifecycleSink(owner, "owner");
                var callbackDepth = 0;
                var maxDepth = 0;
                sink.OnValueChangedCallback = value =>
                {
                    ++callbackDepth;
                    maxDepth = Math.Max(maxDepth, callbackDepth);
                    dispatcher.NotifyValueChanged(sink, value == "A" ? "B" : "A", sessionBound: false);
                    --callbackDepth;
                };

                dispatcher.NotifyValueChanged(sink, "A", sessionBound: false);

                Assert.That(maxDepth, Is.EqualTo(1));
                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.WorkLimitExceeded));
                sink.OnValueChangedCallback = null;
                dispatcher.NotifyValueChanged(sink, "recovered", sessionBound: false);
                Assert.That(sink.Calls[^1], Is.EqualTo("owner:Value:recovered"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void NotificationOverflowClearsQueuedCallbacksAndGuardRecovers()
        {
            var owner = new GameObject("owner");
            try
            {
                var dispatcher = new HybridInputDispatcher();
                var sink = new LifecycleSink(owner, "owner");
                sink.OnValueChangedCallback = _ =>
                {
                    for (var i = 0; i <= HybridInputDispatcher.MaxPendingNotifications; ++i)
                    {
                        dispatcher.NotifyValueChanged(sink, i.ToString(), sessionBound: false);
                    }
                };

                dispatcher.NotifyValueChanged(sink, "start", sessionBound: false);

                Assert.That(sink.Calls, Is.EqualTo(new[] { "owner:Value:start" }));
                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.NotificationBufferOverflow));
                sink.OnValueChangedCallback = null;
                dispatcher.NotifyValueChanged(sink, "recovered", sessionBound: false);
                Assert.That(sink.Calls[^1], Is.EqualTo("owner:Value:recovered"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ListenerExceptionStopsTransactionAndGuardRecovers()
        {
            var owner = new GameObject("owner");
            try
            {
                var dispatcher = new HybridInputDispatcher();
                var sink = new LifecycleSink(owner, "owner");
                sink.OnValueChangedCallback = _ => throw new InvalidOperationException("listener");

                dispatcher.NotifyValueChanged(sink, "throws", sessionBound: false);

                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.ListenerException));
                sink.OnValueChangedCallback = null;
                dispatcher.NotifyValueChanged(sink, "recovered", sessionBound: false);
                Assert.That(sink.Calls[^1], Is.EqualTo("owner:Value:recovered"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void TerminalListenerExceptionRemovesUnregisteredSinkAndGuardRecovers()
        {
            var firstObject = new GameObject("first");
            var secondObject = new GameObject("second");
            try
            {
                using var selection = new SelectionFixture();
                var provider = new FakeProvider("provider", HybridInputProviderMatch.Exact);
                var dispatcher = StartedDispatcher(provider);
                var firstSink = new LifecycleSink(firstObject, "first")
                {
                    OnFocusLostCallback = () => throw new InvalidOperationException("focus lost")
                };
                var firstRegistration = dispatcher.RegisterSink(firstSink);
                selection.Select(firstObject);
                firstRegistration.AcquireFocus();

                firstRegistration.Dispose();

                Assert.That(firstSink.Calls, Is.EqualTo(new[] { "first:Gained", "first:End", "first:Lost" }));
                Assert.That(dispatcher.Diagnostics.LastDiagnostic,
                    Is.EqualTo(HybridInputDiagnosticCode.ListenerException));
                Assert.That(GetRegisteredSinkCount(dispatcher), Is.Zero);

                var secondSink = new LifecycleSink(secondObject, "second");
                using var secondRegistration = dispatcher.RegisterSink(secondSink);
                selection.Select(secondObject);
                Assert.That(secondRegistration.AcquireFocus(), Is.True);
                Assert.That(secondSink.Calls, Is.EqualTo(new[] { "second:Gained" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
            }
        }

        [TestCase(null, TextInputLineMode.SingleLine, "")]
        [TestCase("a\r\nb\rc\nd", TextInputLineMode.SingleLine, "abcd")]
        [TestCase("a\r\nb\rc\nd", TextInputLineMode.MultiLine, "a\nb\nc\nd")]
        public void ManagedCanonicalizerNormalizesLineBreaks(string? input,
            TextInputLineMode lineMode,
            string expected)
        {
            Assert.That(TextInput.CanonicalizeTextForLineMode(input!, lineMode), Is.EqualTo(expected));
        }

        [Test]
        public void ManagedCanonicalizerRemovesUnpairedSurrogates()
        {
            var input = new string(new[] { 'A', '\ud800', 'B', '\udc00', 'C' });

            Assert.That(TextInput.CanonicalizeTextForLineMode(input, TextInputLineMode.MultiLine),
                Is.EqualTo("ABC"));
        }

        [Test]
        public void ManagedCanonicalizerPreservesValidSurrogatePair()
        {
            var input = new string(new[] { 'A', '\ud83d', '\ude00', 'B' });

            Assert.That(TextInput.CanonicalizeTextForLineMode(input, TextInputLineMode.MultiLine),
                Is.EqualTo(input));
        }

        [Test]
        public void InactiveProgrammaticTextUsesGatewayAndSilentPath()
        {
            var owner = new GameObject("text-input");
            owner.SetActive(false);
            try
            {
                var input = owner.AddComponent<TextInput>();
                var payloads = new List<string>();
                input.onValueChanged.AddListener(value =>
                {
                    Assert.That(input.Text, Is.EqualTo(value));
                    payloads.Add(value);
                });

                input.Text = "a\r\nb\ud800";
                input.Text = "ab";
                input.SetTextWithoutNotify("silent");

                Assert.That(payloads, Is.EqualTo(new[] { "ab" }));
                Assert.That(input.Text, Is.EqualTo("silent"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void LineModeNormalizationUsesOneGatewayNotification()
        {
            var owner = new GameObject("text-input");
            owner.SetActive(false);
            try
            {
                var input = owner.AddComponent<TextInput>();
                input.lineMode = TextInputLineMode.MultiLine;
                input.SetTextWithoutNotify("a\nb");
                var payloads = new List<string>();
                input.onValueChanged.AddListener(payloads.Add);

                input.lineMode = TextInputLineMode.SingleLine;

                Assert.That(payloads, Is.EqualTo(new[] { "ab" }));
                Assert.That(input.Text, Is.EqualTo("ab"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void NativeRoundTripMatchesManagedCanonicalTextAndGatewayPayload()
        {
            var cases = new[]
            {
                new CanonicalParityCase("a\rb", TextInputLineMode.SingleLine, "ab"),
                new CanonicalParityCase("a\r\nb\nc", TextInputLineMode.SingleLine, "abc"),
                new CanonicalParityCase("a\rb\r\nc", TextInputLineMode.MultiLine, "a\nb\nc"),
                new CanonicalParityCase(new string(new[] { 'A', '\ud83d', '\ude00', 'B' }),
                    TextInputLineMode.MultiLine,
                    new string(new[] { 'A', '\ud83d', '\ude00', 'B' })),
                new CanonicalParityCase(new string(new[] { 'A', '\ud800', 'B', '\udc00', 'C' }),
                    TextInputLineMode.MultiLine,
                    "ABC")
            };

            foreach (var testCase in cases)
            {
                var owner = new GameObject("text-input", typeof(RectTransform));
                owner.SetActive(false);
                try
                {
                    var input = owner.AddComponent<TextInput>();
                    input.lineMode = testCase.LineMode;
                    InvokeRecreateInputBox(input);
                    var payloads = new List<string>();
                    var callbackTexts = new List<string>();
                    var callbackNativeTexts = new List<string>();
                    input.onValueChanged.AddListener(value =>
                    {
                        payloads.Add(value);
                        callbackTexts.Add(input.Text);
                        callbackNativeTexts.Add(GetNativeInputBox(input).Text);
                    });

                    input.Text = testCase.Input;
                    input.Text = testCase.Expected;

                    Assert.That(GetNativeInputBox(input).Text, Is.EqualTo(testCase.Expected));
                    Assert.That(input.Text, Is.EqualTo(testCase.Expected));
                    Assert.That(payloads, Is.EqualTo(new[] { testCase.Expected }));
                    Assert.That(callbackTexts, Is.EqualTo(payloads));
                    Assert.That(callbackNativeTexts, Is.EqualTo(payloads));
                    Assert.That(input.Text, Is.EqualTo(payloads[0]));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(owner);
                }
            }
        }

        [Test]
        public void RecreateInputBoxReadBackCommitsSilently()
        {
            var owner = new GameObject("text-input", typeof(RectTransform));
            owner.SetActive(false);
            try
            {
                var input = owner.AddComponent<TextInput>();
                input.lineMode = TextInputLineMode.MultiLine;
                SetSerializedText(input, new string(new[] { 'a', '\r', '\n', 'b', '\ud800' }));
                var payloads = new List<string>();
                input.onValueChanged.AddListener(payloads.Add);

                InvokeRecreateInputBox(input);
                InvokeRecreateInputBox(input);

                Assert.That(GetNativeInputBox(input).Text, Is.EqualTo("a\nb"));
                Assert.That(input.Text, Is.EqualTo("a\nb"));
                Assert.That(payloads, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PlayerLoopStageIsIdempotentAndRunsImmediatelyAfterScriptUpdate()
        {
            var playerLoop = new PlayerLoopSystem
            {
                subSystemList = new[]
                {
                    new PlayerLoopSystem
                    {
                        type = typeof(UnityEngine.PlayerLoop.Update),
                        subSystemList = new[]
                        {
                            new PlayerLoopSystem { type = typeof(BeforeScriptUpdate) },
                            new PlayerLoopSystem
                            {
                                type = typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate)
                            },
                            new PlayerLoopSystem { type = typeof(AfterScriptUpdate) }
                        }
                    }
                }
            };

            Assert.That(HybridInputPlayerLoop.Configure(ref playerLoop, () => { }), Is.True);
            Assert.That(HybridInputPlayerLoop.Configure(ref playerLoop, () => { }), Is.True);

            var rootSystems = playerLoop.subSystemList!;
            var updateSystems = rootSystems[0].subSystemList!;
            Assert.That(updateSystems.Length, Is.EqualTo(4));
            Assert.That(updateSystems[1].type,
                Is.EqualTo(typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate)));
            Assert.That(updateSystems[2].type?.Name, Is.EqualTo("PostUpdateInputDispatch"));
            Assert.That(updateSystems[3].type, Is.EqualTo(typeof(AfterScriptUpdate)));
            Assert.That(HybridInputPlayerLoop.RemoveDispatchStages(ref playerLoop), Is.EqualTo(1));
            Assert.That(playerLoop.subSystemList![0].subSystemList!.Length, Is.EqualTo(3));
        }

        private static HybridInputDispatcher StartedDispatcher(FakeProvider provider)
        {
            var dispatcher = new HybridInputDispatcher();
            dispatcher.RegisterProvider(provider);
            dispatcher.RefreshEnvironment(FocusedEnvironment());
            return dispatcher;
        }

        private static HybridInputFrame InputFrame(params HybridInputEvent[] events)
        {
            return new HybridInputFrame(1,
                1d,
                "provider",
                1,
                1,
                events,
                Array.Empty<KeyCode>());
        }

        private static HybridInputFrame SnapshotFrame(HybridInputFrame frame)
        {
            return new HybridInputFrame(frame.FrameIndex,
                frame.UnscaledTime,
                frame.ProviderId,
                frame.ProviderGeneration,
                frame.FocusEpoch,
                frame.Events.ToArray(),
                frame.PressedKeys.ToArray());
        }

        private static void InvokeRecreateInputBox(TextInput input)
        {
            typeof(TextInput).GetField("rectTransformCache", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(input, input.GetComponent<RectTransform>());
            typeof(TextInput).GetMethod("RecreateInputBox", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(input, null);
        }

        private static InputBox GetNativeInputBox(TextInput input)
        {
            return (InputBox)typeof(TextInput)
                .GetField("inputBox", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(input)!;
        }

        private static void InvokeTextInputReset(TextInput input, HybridInputResetReason reason)
        {
            typeof(TextInput).GetMethod("OnInputReset", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(input, new object[] { reason });
        }

        private static void SetTextInputPrivateField(TextInput input, string fieldName, object value)
        {
            typeof(TextInput).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(input, value);
        }

        private static T GetTextInputPrivateField<T>(TextInput input, string fieldName)
        {
            return (T)typeof(TextInput).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(input)!;
        }

        private static bool IsTextInputResetStateCleared(TextInput input, InputBox inputBox)
        {
            return !GetTextInputPrivateField<bool>(input, "compositionActive") &&
                   GetTextInputPrivateField<string>(input, "lastCompositionText").Length == 0 &&
                   !GetTextInputPrivateField<bool>(input, "caretVisible") &&
                   !inputBox.Focused &&
                   !inputBox.Selection.HasSelection &&
                   !inputBox.ClearComposition();
        }

        private static void SetSerializedText(TextInput input, string value)
        {
            typeof(TextInput).GetField("m_text", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(input, value);
        }

        private static int GetRegisteredSinkCount(HybridInputDispatcher dispatcher)
        {
            var sinks = typeof(HybridInputDispatcher)
                .GetField("sinks", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(dispatcher)!;
            return (int)sinks.GetType().GetProperty("Count")!.GetValue(sinks)!;
        }

        private static bool RegisterThrows(HybridInputDispatcher dispatcher, IHybridInputProvider provider)
        {
            try
            {
                dispatcher.RegisterProvider(provider);
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        private static bool RuntimeRegisterThrows(IHybridInputProvider provider)
        {
            try
            {
                HybridInputRuntime.RegisterProvider(provider).Dispose();
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        private static HybridInputEnvironment FocusedEnvironment()
        {
            return new HybridInputEnvironment(null, 1, applicationFocused: true);
        }

        private sealed class FakeFrameSink : IHybridInputFrameSink
        {
            internal List<HybridInputFrame> Frames { get; } = new List<HybridInputFrame>();
            internal List<HybridInputFrame> FrameViews { get; } = new List<HybridInputFrame>();
            internal List<IReadOnlyList<HybridInputEvent>> EventViews { get; } =
                new List<IReadOnlyList<HybridInputEvent>>();
            internal List<IReadOnlyList<KeyCode>> PressedKeyViews { get; } =
                new List<IReadOnlyList<KeyCode>>();
            internal List<HybridInputResetReason> Resets { get; } = new List<HybridInputResetReason>();

            public void OnInputFrame(HybridInputFrame frame)
            {
                FrameViews.Add(frame);
                EventViews.Add(frame.Events);
                PressedKeyViews.Add(frame.PressedKeys);
                Frames.Add(SnapshotFrame(frame));
            }

            public void OnInputReset(HybridInputResetReason reason)
            {
                Resets.Add(reason);
            }
        }

        private sealed class CountingFrameSink : IHybridInputFrameSink
        {
            internal int FrameCount { get; private set; }

            public void OnInputFrame(HybridInputFrame frame)
            {
                ++FrameCount;
            }

            public void OnInputReset(HybridInputResetReason reason)
            {
            }
        }

        private sealed class ReentrantFrameSink : IHybridInputFrameSink
        {
            internal int FrameCount { get; private set; }
            internal Action? OnFirstFrame { get; set; }

            public void OnInputFrame(HybridInputFrame frame)
            {
                ++FrameCount;
                if (FrameCount == 1)
                {
                    OnFirstFrame?.Invoke();
                }
            }

            public void OnInputReset(HybridInputResetReason reason)
            {
            }
        }

        private sealed class BeforeScriptUpdate
        {
        }

        private readonly struct CanonicalParityCase
        {
            internal CanonicalParityCase(string input, TextInputLineMode lineMode, string expected)
            {
                Input = input;
                LineMode = lineMode;
                Expected = expected;
            }

            internal string Input { get; }
            internal TextInputLineMode LineMode { get; }
            internal string Expected { get; }
        }

        private sealed class AfterScriptUpdate
        {
        }

        private sealed class SelectionFixture : IDisposable
        {
            private readonly GameObject eventSystemObject;
            private readonly GameObject? previousSelectedObject;
            private readonly bool ownsEventSystem;
            private readonly bool manuallyRegisteredEventSystem;
            private readonly bool previousGameObjectActiveState;
            private readonly bool previousEnabledState;

            internal SelectionFixture()
            {
                var current = EventSystem.current;
                if (current != null)
                {
                    EventSystem = current;
                    eventSystemObject = current.gameObject;
                    previousSelectedObject = current.currentSelectedGameObject;
                    previousGameObjectActiveState = eventSystemObject.activeSelf;
                    previousEnabledState = current.enabled;
                    if (!eventSystemObject.activeSelf)
                    {
                        eventSystemObject.SetActive(true);
                    }
                    if (!current.enabled)
                    {
                        current.enabled = true;
                    }
                    return;
                }

                ownsEventSystem = true;
                eventSystemObject = new GameObject("event-system");
                EventSystem = eventSystemObject.AddComponent<EventSystem>();
                eventSystemObject.AddComponent<StandaloneInputModule>();
                if (EventSystem.current == null)
                {
                    InvokeEventSystemLifecycle(EventSystem, "OnEnable");
                    manuallyRegisteredEventSystem = true;
                }
                previousGameObjectActiveState = true;
                previousEnabledState = true;
            }

            internal EventSystem EventSystem { get; }

            internal void Select(GameObject owner)
            {
                var current = EventSystem.current;
                Assert.That(current, Is.Not.Null, "Selection fixture requires a current EventSystem");
                Assert.That(current!.isActiveAndEnabled, Is.True,
                    "Selection fixture requires an active EventSystem");
                current.SetSelectedGameObject(owner);
                Assert.That(current.currentSelectedGameObject, Is.SameAs(owner));
            }

            internal void SetActive(bool active)
            {
                EventSystem.enabled = active;
            }

            public void Dispose()
            {
                if (ownsEventSystem)
                {
                    if (manuallyRegisteredEventSystem)
                    {
                        InvokeEventSystemLifecycle(EventSystem, "OnDisable");
                    }
                    UnityEngine.Object.DestroyImmediate(eventSystemObject);
                    return;
                }

                if (EventSystem.isActiveAndEnabled)
                {
                    EventSystem.SetSelectedGameObject(previousSelectedObject);
                }
                if (EventSystem.enabled != previousEnabledState)
                {
                    EventSystem.enabled = previousEnabledState;
                }
                if (eventSystemObject.activeSelf != previousGameObjectActiveState)
                {
                    eventSystemObject.SetActive(previousGameObjectActiveState);
                }
            }

            private static void InvokeEventSystemLifecycle(EventSystem eventSystem, string methodName)
            {
                typeof(EventSystem).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(eventSystem, null);
            }
        }

        private sealed class LifecycleSink : IHybridInputLifecycleSink
        {
            private readonly string name;
            private readonly List<string> calls;

            internal LifecycleSink(GameObject owner, string name, List<string>? calls = null)
            {
                Owner = owner;
                this.name = name;
                this.calls = calls ?? new List<string>();
            }

            public GameObject Owner { get; }
            public bool IsActiveAndEnabled { get; set; } = true;
            public bool CanConsumeInputNow { get; set; } = true;
            public string CommittedText { get; set; } = string.Empty;
            internal List<HybridInputFrame> Frames { get; } = new List<HybridInputFrame>();
            internal List<HybridInputResetReason> Resets { get; } = new List<HybridInputResetReason>();
            internal List<string> Calls => calls;
            internal Action<HybridInputFrame>? OnFrame { get; set; }
            internal Action<HybridInputResetReason>? OnResetCallback { get; set; }
            internal Action? OnFocusGainedCallback { get; set; }
            internal Action<string>? OnEndEditCallback { get; set; }
            internal Action? OnFocusLostCallback { get; set; }
            internal Action<string>? OnValueChangedCallback { get; set; }

            public void OnInputFrame(HybridInputFrame frame)
            {
                Frames.Add(SnapshotFrame(frame));
                OnFrame?.Invoke(frame);
            }

            public void OnInputReset(HybridInputResetReason reason)
            {
                Resets.Add(reason);
                OnResetCallback?.Invoke(reason);
            }

            public void OnFocusGained()
            {
                Calls.Add($"{name}:Gained");
                OnFocusGainedCallback?.Invoke();
            }

            public void OnEndEdit(string finalText)
            {
                Calls.Add($"{name}:End");
                OnEndEditCallback?.Invoke(finalText);
            }

            public void OnFocusLost()
            {
                Calls.Add($"{name}:Lost");
                OnFocusLostCallback?.Invoke();
            }

            public void OnValueChanged(string value)
            {
                Calls.Add($"{name}:Value:{value}");
                OnValueChangedCallback?.Invoke(value);
            }
        }

        private sealed class UnscopedFocusedProvider : IHybridInputProvider
        {
            private IHybridInputEventSink? sink;

            public string Id => "unscoped";
            public int Priority => 0;
            public HybridInputProviderKind Kind => HybridInputProviderKind.Custom;
            public HybridInputCapabilities Capabilities => HybridInputCapabilities.KeyState |
                                                           HybridInputCapabilities.CommittedText |
                                                           HybridInputCapabilities.Composition;
            public HybridScrollCapability ScrollCapability => HybridScrollCapability.Unsupported;
            internal Func<HybridInputEnvironment, HybridInputProviderMatch> MatchResult { get; set; } =
                _ => HybridInputProviderMatch.Exact;

            public HybridInputProviderMatch Match(HybridInputEnvironment environment)
            {
                return MatchResult(environment);
            }

            public void Start(IHybridInputEventSink eventSink)
            {
                sink = eventSink;
            }

            public void Stop()
            {
                sink = null;
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

            internal void Emit(HybridInputEvent inputEvent)
            {
                sink?.Enqueue(inputEvent);
            }
        }

        private sealed class FakeProvider : IHybridInputProvider, IHybridInputFocusSessionProvider
        {
            private readonly Func<HybridInputEnvironment, HybridInputProviderMatch> match;
            private readonly List<string>? calls;
            private IHybridInputEventSink? sink;
            private IHybridInputEventSink? sessionSink;
            private readonly List<IHybridInputEventSink> startedSinks = new List<IHybridInputEventSink>();
            private readonly List<IHybridInputEventSink> sessionSinks = new List<IHybridInputEventSink>();

            internal FakeProvider(string id,
                HybridInputProviderMatch match,
                int priority = 0,
                List<string>? calls = null,
                HybridInputCapabilities capabilities = HybridInputCapabilities.KeyState |
                                                                HybridInputCapabilities.CommittedText |
                                                                HybridInputCapabilities.Composition |
                                                                HybridInputCapabilities.ImeControl)
                : this(id, _ => match, calls, priority, capabilities)
            {
            }

            internal FakeProvider(string id,
                Func<HybridInputEnvironment, HybridInputProviderMatch> match,
                List<string>? calls = null,
                int priority = 0,
                HybridInputCapabilities capabilities = HybridInputCapabilities.KeyState |
                                                                HybridInputCapabilities.CommittedText |
                                                                HybridInputCapabilities.Composition |
                                                                HybridInputCapabilities.ImeControl)
            {
                Id = id;
                this.match = match;
                this.calls = calls;
                Priority = priority;
                Capabilities = capabilities;
            }

            public string Id { get; }
            public int Priority { get; }
            public HybridInputProviderKind Kind => HybridInputProviderKind.Custom;
            public HybridInputCapabilities Capabilities { get; }
            public HybridScrollCapability ScrollCapability => HybridScrollCapability.DeltaOnly;
            internal int StartCount { get; private set; }
            internal int BeginCount { get; private set; }
            internal int EndCount { get; private set; }
            internal int BeginFailuresRemaining { get; set; }
            internal int EndFailuresRemaining { get; set; }
            internal bool HasBoundSession => sessionSink != null;
            internal bool ImeEnabled { get; private set; }
            internal Vector2 ImeCursorPosition { get; private set; }
            internal List<KeyCode> PressedKeys { get; } = new List<KeyCode>();
            internal bool ReplacePressedKeysOnCollect { get; set; } = true;

            public HybridInputProviderMatch Match(HybridInputEnvironment environment)
            {
                return match(environment);
            }

            public void Start(IHybridInputEventSink eventSink)
            {
                sink = eventSink;
                startedSinks.Add(eventSink);
                ++StartCount;
                calls?.Add($"{Id}:start");
            }

            public void Stop()
            {
                calls?.Add($"{Id}:stop");
                sessionSink = null;
            }

            public void BeginFocusSession(IHybridInputEventSink nextSessionSink)
            {
                sessionSink = nextSessionSink;
                sessionSinks.Add(nextSessionSink);
                ++BeginCount;
                calls?.Add($"{Id}:session:start");
                if (BeginFailuresRemaining > 0)
                {
                    --BeginFailuresRemaining;
                    throw new InvalidOperationException("begin focus session");
                }
            }

            public void EndFocusSession()
            {
                ++EndCount;
                calls?.Add($"{Id}:session:end");
                if (EndFailuresRemaining > 0)
                {
                    --EndFailuresRemaining;
                    throw new InvalidOperationException("end focus session");
                }
                sessionSink = null;
            }

            public void Collect(HybridInputCollectContext context)
            {
                if (ReplacePressedKeysOnCollect)
                {
                    (sessionSink ?? sink)?.ReplacePressedKeys(PressedKeys);
                }
            }

            public void SetImeEnabled(bool enabled)
            {
                ImeEnabled = enabled;
                calls?.Add($"{Id}:ime:{enabled}");
            }

            public void SetImeCursorPosition(Vector2 position)
            {
                ImeCursorPosition = position;
            }

            internal void Emit(HybridInputEvent inputEvent)
            {
                (sessionSink ?? sink)?.Enqueue(inputEvent);
            }

            internal void EmitFromStart(int startIndex, HybridInputEvent inputEvent)
            {
                startedSinks[startIndex].Enqueue(inputEvent);
            }

            internal IHybridInputEventSink CaptureSession(int sessionIndex)
            {
                return sessionSinks[sessionIndex];
            }
        }
    }
}

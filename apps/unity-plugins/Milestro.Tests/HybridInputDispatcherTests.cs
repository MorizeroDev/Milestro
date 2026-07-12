using System;
using System.Collections.Generic;
using Milestro.Components.Internal;
using Milestro.Input;
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

            Assert.That(calls, Is.EqualTo(new[] { "first:ime:False", "first:stop", "second:start", "second:ime:False" }));
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
                    Is.EqualTo(new[] { "provider:ime:False", "provider:stop", "provider:start", "provider:ime:False" }));
                Assert.That(provider.StartCount, Is.EqualTo(2));
                Assert.That(sink.Resets, Does.Contain(HybridInputResetReason.ProviderChanged));
                Assert.That(sink.Frames, Has.Count.EqualTo(2));
                var secondFrame = sink.Frames[1];
                Assert.That(secondFrame.ProviderGeneration > firstFrame.ProviderGeneration, Is.True);
                Assert.That(secondFrame.FocusEpoch > firstFrame.FocusEpoch, Is.True);
                Assert.That(secondFrame.Events, Is.Empty);
                Assert.That(secondFrame.IsKeyPressed(KeyCode.LeftShift), Is.False);
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

            Assert.That(firstSink.Frames, Is.Empty);
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
            Assert.That(((IList<HybridInputEvent>)frame.Events).IsReadOnly, Is.True);
            Assert.That(((IList<KeyCode>)frame.PressedKeys).IsReadOnly, Is.True);
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

        private sealed class BeforeScriptUpdate
        {
        }

        private sealed class AfterScriptUpdate
        {
        }

        private sealed class FakeProvider : IHybridInputProvider
        {
            private readonly Func<HybridInputEnvironment, HybridInputProviderMatch> match;
            private readonly List<string>? calls;
            private IHybridInputEventSink? sink;
            private readonly List<IHybridInputEventSink> startedSinks = new List<IHybridInputEventSink>();

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
            }

            public void Collect(HybridInputCollectContext context)
            {
                if (ReplacePressedKeysOnCollect)
                {
                    sink?.ReplacePressedKeys(PressedKeys);
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
                sink?.Enqueue(inputEvent);
            }

            internal void EmitFromStart(int startIndex, HybridInputEvent inputEvent)
            {
                startedSinks[startIndex].Enqueue(inputEvent);
            }
        }
    }
}

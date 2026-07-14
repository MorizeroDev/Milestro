using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Milestro.InputSystem.Service;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.InputSystemTests
{
    public class MacScrollPhaseProbeSessionTests
    {
        private const uint MinimalBaseFields =
            (uint)(MacScrollPhaseSampleFields.Sequence |
                   MacScrollPhaseSampleFields.Timestamp |
                   MacScrollPhaseSampleFields.WindowNumber |
                   MacScrollPhaseSampleFields.ScrollingDelta |
                   MacScrollPhaseSampleFields.GesturePhase |
                   MacScrollPhaseSampleFields.MomentumPhase);
        private const uint MinimalResolvedFields = MinimalBaseFields | (uint)MacScrollPhaseSampleFields.GestureId;

        [Test]
        public void DefaultStageStartsAndAutoStopsWithoutPolling()
        {
            var transport = new FakeTransport();
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeSession.DefaultStage);

            session.Start(0d);
            session.Update(1, 4d);

            Assert.That(MacScrollPhaseProbeSession.DefaultStage,
                Is.EqualTo(MacScrollPhaseProbeStage.NativeLifecycle));
            Assert.That(transport.LastStartMode, Is.EqualTo(MacScrollPhaseMonitorMode.PassThrough));
            Assert.That(transport.PollCount, Is.Zero);
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(transport.LastStopLease, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.False);
        }

        [Test]
        public void SamplingStagesStartInCaptureSamplesMode()
        {
            var stages = new[]
            {
                MacScrollPhaseProbeStage.NativePolling,
                MacScrollPhaseProbeStage.InputAction,
                MacScrollPhaseProbeStage.UguiAssociation
            };

            foreach (var stage in stages)
            {
                var transport = new FakeTransport();
                var session = CreateSession(transport, new FakeSink(), stage);

                session.Start(0d);
                session.Update(1, 0d);

                Assert.That(transport.LastStartMode, Is.EqualTo(MacScrollPhaseMonitorMode.CaptureSamples));
                Assert.That(transport.PollCount, Is.EqualTo(1));
                Assert.That(session.RequiresInputAction, Is.EqualTo(stage != MacScrollPhaseProbeStage.NativePolling));
                session.Disable();
            }
        }

        [Test]
        public void NativePropertiesPreservesStageValuesAndOnlyReadsInNativeCallback()
        {
            Assert.That((int)MacScrollPhaseProbeStage.NativeLifecycle, Is.EqualTo(0));
            Assert.That((int)MacScrollPhaseProbeStage.NativePolling, Is.EqualTo(1));
            Assert.That((int)MacScrollPhaseProbeStage.InputAction, Is.EqualTo(2));
            Assert.That((int)MacScrollPhaseProbeStage.UguiAssociation, Is.EqualTo(3));
            Assert.That((int)MacScrollPhaseProbeStage.NativeProperties, Is.EqualTo(4));
            Assert.That((int)MacScrollPhaseProbeStage.NativeEventProperties, Is.EqualTo(5));
            Assert.That((int)MacScrollPhaseProbeStage.NativeEventScalars, Is.EqualTo(6));
            Assert.That((int)MacScrollPhaseProbeStage.NativeLocalPodWrites, Is.EqualTo(7));
            Assert.That((int)MacScrollPhaseProbeStage.NativePhasesOnly, Is.EqualTo(8));
            Assert.That((int)MacScrollPhaseProbeStage.NativePhasesTimestamp, Is.EqualTo(9));
            Assert.That((int)MacScrollPhaseProbeStage.NativePhasesTimestampWindowPod, Is.EqualTo(10));
            Assert.That((int)MacScrollPhaseProbeStage.NativePhasesTimestampWindow, Is.EqualTo(11));
            Assert.That((int)MacScrollPhaseProbeStage.NativePhasesTimestampWindowScrollingDelta, Is.EqualTo(12));
            Assert.That((int)MacScrollPhaseProbeStage.NativeMinimalQueue, Is.EqualTo(13));
            Assert.That((int)MacScrollPhaseProbeStage.NativeMinimalQueueTracker, Is.EqualTo(14));
            Assert.That((int)MacScrollPhaseProbeStage.NativeMinimalPolling, Is.EqualTo(15));
            Assert.That((int)MacScrollPhaseProbeStage.NativeMinimalInputActionTrace, Is.EqualTo(16));

            var transport = new FakeTransport();
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeProperties);

            session.Start(0d);
            session.RecordActionAttachment("must-not-attach");
            session.ObserveAction(1, 1d, new Vector2(1f, -1f), "/Mouse/scroll");
            session.ObserveUgui(1, 7, new Vector2(1f, -1f));
            session.Update(1, 0d);
            session.LateUpdate(1);
            session.Update(2, 4d);

            Assert.That(session.RequiresInputAction, Is.False);
            Assert.That(transport.LastStartMode, Is.EqualTo(MacScrollPhaseMonitorMode.ReadProperties));
            Assert.That(transport.PollCount, Is.Zero);
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.False);
            Assert.That(sink.LastTrace.Contains("action"), Is.False);
            Assert.That(sink.LastTrace.Contains("ugui"), Is.False);
        }

        [Test]
        public void NativeEventPropertiesOnlyReadsEventGettersInNativeCallback()
        {
            var transport = new FakeTransport();
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeEventProperties);

            session.Start(0d);
            session.RecordActionAttachment("must-not-attach");
            session.ObserveAction(1, 1d, new Vector2(1f, -1f), "/Mouse/scroll");
            session.ObserveUgui(1, 7, new Vector2(1f, -1f));
            session.Update(1, 0d);
            session.LateUpdate(1);
            session.Update(2, 4d);

            Assert.That(session.RequiresInputAction, Is.False);
            Assert.That(transport.LastStartMode, Is.EqualTo(MacScrollPhaseMonitorMode.ReadEventProperties));
            Assert.That(transport.PollCount, Is.Zero);
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.False);
            Assert.That(sink.LastTrace.Contains("action"), Is.False);
            Assert.That(sink.LastTrace.Contains("ugui"), Is.False);
        }

        [Test]
        public void NativeEventScalarsExcludesWindowGetterInNativeCallback()
        {
            var transport = new FakeTransport();
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeEventScalars);

            session.Start(0d);
            session.RecordActionAttachment("must-not-attach");
            session.ObserveAction(1, 1d, new Vector2(1f, -1f), "/Mouse/scroll");
            session.ObserveUgui(1, 7, new Vector2(1f, -1f));
            session.Update(1, 0d);
            session.LateUpdate(1);
            session.Update(2, 4d);

            Assert.That(session.RequiresInputAction, Is.False);
            Assert.That(transport.LastStartMode, Is.EqualTo(MacScrollPhaseMonitorMode.ReadEventScalars));
            Assert.That(transport.PollCount, Is.Zero);
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.False);
            Assert.That(sink.LastTrace.Contains("action"), Is.False);
            Assert.That(sink.LastTrace.Contains("ugui"), Is.False);
        }

        [Test]
        public void NativeLocalPodWritesHasNoNativeGetterOrManagedInputPath()
        {
            var transport = new FakeTransport();
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeLocalPodWrites);

            session.Start(0d);
            session.RecordActionAttachment("must-not-attach");
            session.ObserveAction(1, 1d, new Vector2(1f, -1f), "/Mouse/scroll");
            session.ObserveUgui(1, 7, new Vector2(1f, -1f));
            session.Update(1, 0d);
            session.LateUpdate(1);
            session.Update(2, 4d);

            Assert.That(session.RequiresInputAction, Is.False);
            Assert.That(transport.LastStartMode, Is.EqualTo(MacScrollPhaseMonitorMode.WriteLocalPod));
            Assert.That(transport.PollCount, Is.Zero);
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.False);
            Assert.That(sink.LastTrace.Contains("action"), Is.False);
            Assert.That(sink.LastTrace.Contains("ugui"), Is.False);
        }

        [Test]
        public void NativePhasesOnlyOnlyAddsRequiredNativeGetters()
        {
            var transport = new FakeTransport();
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativePhasesOnly);

            session.Start(0d);
            session.RecordActionAttachment("must-not-attach");
            session.ObserveAction(1, 1d, new Vector2(1f, -1f), "/Mouse/scroll");
            session.ObserveUgui(1, 7, new Vector2(1f, -1f));
            session.Update(1, 0d);
            session.LateUpdate(1);
            session.Update(2, 4d);

            Assert.That(session.RequiresInputAction, Is.False);
            Assert.That(transport.LastStartMode, Is.EqualTo(MacScrollPhaseMonitorMode.ReadPhasesOnly));
            Assert.That(transport.PollCount, Is.Zero);
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.False);
            Assert.That(sink.LastTrace.Contains("action"), Is.False);
            Assert.That(sink.LastTrace.Contains("ugui"), Is.False);
        }

        [Test]
        public void NativePhasesTimestampOnlyAddsTimestampGetter()
        {
            var transport = new FakeTransport();
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativePhasesTimestamp);

            session.Start(0d);
            session.RecordActionAttachment("must-not-attach");
            session.ObserveAction(1, 1d, new Vector2(1f, -1f), "/Mouse/scroll");
            session.ObserveUgui(1, 7, new Vector2(1f, -1f));
            session.Update(1, 0d);
            session.LateUpdate(1);
            session.Update(2, 4d);

            Assert.That(session.RequiresInputAction, Is.False);
            Assert.That(transport.LastStartMode, Is.EqualTo(MacScrollPhaseMonitorMode.ReadPhasesTimestamp));
            Assert.That(transport.PollCount, Is.Zero);
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.False);
            Assert.That(sink.LastTrace.Contains("action"), Is.False);
            Assert.That(sink.LastTrace.Contains("ugui"), Is.False);
        }

        [Test]
        public void NativePhasesTimestampWindowPodOnlyAddsFixedWindowWrite()
        {
            var transport = new FakeTransport();
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativePhasesTimestampWindowPod);

            session.Start(0d);
            session.RecordActionAttachment("must-not-attach");
            session.ObserveAction(1, 1d, new Vector2(1f, -1f), "/Mouse/scroll");
            session.ObserveUgui(1, 7, new Vector2(1f, -1f));
            session.Update(1, 0d);
            session.LateUpdate(1);
            session.Update(2, 4d);

            Assert.That(session.RequiresInputAction, Is.False);
            Assert.That(transport.LastStartMode, Is.EqualTo(MacScrollPhaseMonitorMode.WritePhasesTimestampWindowPod));
            Assert.That(transport.PollCount, Is.Zero);
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.False);
            Assert.That(sink.LastTrace.Contains("action"), Is.False);
            Assert.That(sink.LastTrace.Contains("ugui"), Is.False);
        }

        [Test]
        public void NativePhasesTimestampWindowOnlyReplacesWindowRhs()
        {
            var transport = new FakeTransport();
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativePhasesTimestampWindow);

            session.Start(0d);
            session.RecordActionAttachment("must-not-attach");
            session.ObserveAction(1, 1d, new Vector2(1f, -1f), "/Mouse/scroll");
            session.ObserveUgui(1, 7, new Vector2(1f, -1f));
            session.Update(1, 0d);
            session.LateUpdate(1);
            session.Update(2, 4d);

            Assert.That(session.RequiresInputAction, Is.False);
            Assert.That(transport.LastStartMode, Is.EqualTo(MacScrollPhaseMonitorMode.ReadPhasesTimestampWindow));
            Assert.That(transport.PollCount, Is.Zero);
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.False);
            Assert.That(sink.LastTrace.Contains("action"), Is.False);
            Assert.That(sink.LastTrace.Contains("ugui"), Is.False);
        }

        [Test]
        public void NativePhasesTimestampWindowScrollingDeltaOnlyAddsScrollingVector()
        {
            var transport = new FakeTransport();
            var sink = new FakeSink();
            var session = CreateSession(transport, sink,
                MacScrollPhaseProbeStage.NativePhasesTimestampWindowScrollingDelta);

            session.Start(0d);
            session.RecordActionAttachment("must-not-attach");
            session.ObserveAction(1, 1d, new Vector2(1f, -1f), "/Mouse/scroll");
            session.ObserveUgui(1, 7, new Vector2(1f, -1f));
            session.Update(1, 0d);
            session.LateUpdate(1);
            session.Update(2, 4d);

            Assert.That(session.RequiresInputAction, Is.False);
            Assert.That(transport.LastStartMode,
                Is.EqualTo(MacScrollPhaseMonitorMode.ReadPhasesTimestampWindowScrollingDelta));
            Assert.That(transport.PollCount, Is.Zero);
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.False);
            Assert.That(sink.LastTrace.Contains("action"), Is.False);
            Assert.That(sink.LastTrace.Contains("ugui"), Is.False);
        }

        [Test]
        public void NativeMinimalQueueDoesNotPollOrAttachManagedInput()
        {
            var transport = new FakeTransport();
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalQueue);

            session.Start(0d);
            session.RecordActionAttachment("must-not-attach");
            session.ObserveAction(1, 1d, new Vector2(1f, -1f), "/Mouse/scroll");
            session.ObserveUgui(1, 7, new Vector2(1f, -1f));
            session.Update(1, 0d);
            session.LateUpdate(1);
            session.Update(2, 4d);

            Assert.That(session.RequiresInputAction, Is.False);
            Assert.That(transport.LastStartMode, Is.EqualTo(MacScrollPhaseMonitorMode.QueueMinimalSamples));
            Assert.That(transport.PollCount, Is.Zero);
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.False);
            Assert.That(sink.LastTrace.Contains("action"), Is.False);
            Assert.That(sink.LastTrace.Contains("ugui"), Is.False);
        }

        [Test]
        public void NativeMinimalQueueTrackerDoesNotPollOrAttachManagedInput()
        {
            var transport = new FakeTransport();
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalQueueTracker);

            session.Start(0d);
            session.RecordActionAttachment("must-not-attach");
            session.ObserveAction(1, 1d, new Vector2(1f, -1f), "/Mouse/scroll");
            session.ObserveUgui(1, 7, new Vector2(1f, -1f));
            session.Update(1, 0d);
            session.LateUpdate(1);
            session.Update(2, 4d);

            Assert.That(session.RequiresInputAction, Is.False);
            Assert.That(transport.LastStartMode, Is.EqualTo(MacScrollPhaseMonitorMode.QueueMinimalTrackedSamples));
            Assert.That(transport.PollCount, Is.Zero);
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.False);
            Assert.That(sink.LastTrace.Contains("action"), Is.False);
            Assert.That(sink.LastTrace.Contains("ugui"), Is.False);
        }

        [Test]
        public void NativeMinimalPollingUsesTrackedModeAndPollsUpdateAndLateUpdateWithoutManagedInput()
        {
            var transport = new FakeTransport();
            transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(1, gesturePhase: 2)));
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalPolling);

            session.Start(0d);
            session.RecordActionAttachment("must-not-attach");
            session.ObserveAction(1, 1d, new Vector2(1f, -1f), "/Mouse/scroll");
            session.ObserveUgui(1, 7, new Vector2(1f, -1f));
            session.Update(1, 0d);
            transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(2,
                gestureId: 0,
                windowNumber: 99,
                gesturePhase: 1,
                validFields: MinimalBaseFields)));
            session.LateUpdate(1);

            Assert.That(session.RequiresInputAction, Is.False);
            Assert.That(transport.LastStartMode, Is.EqualTo(MacScrollPhaseMonitorMode.QueueMinimalTrackedSamples));
            Assert.That(transport.PollCount, Is.Zero);
            Assert.That(transport.MinimalPollCount, Is.EqualTo(2));
            Assert.That(transport.MinimalInvalidDetailCount, Is.Zero);
            Assert.That(transport.StopCount, Is.Zero);
            Assert.That(sink.FlushCount, Is.Zero);
            session.Disable();
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.False);
            Assert.That(sink.LastTrace.Contains("action"), Is.False);
            Assert.That(sink.LastTrace.Contains("ugui"), Is.False);
        }

        [Test]
        public void NativeMinimalPollingFinalDrainsBeforeDurationStop()
        {
            var transport = new FakeTransport();
            transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(1, gesturePhase: 2)));
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalPolling);
            session.Start(0d);

            session.Update(1, 4d);

            Assert.That(transport.Calls.Count, Is.EqualTo(2));
            Assert.That(transport.Calls[0], Is.EqualTo("minimal-poll"));
            Assert.That(transport.Calls[1], Is.EqualTo("stop"));
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.False);
            Assert.That(sink.LastTrace.IndexOf("minimal source=final", System.StringComparison.Ordinal) <
                        sink.LastTrace.IndexOf("monitor-stop", System.StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void NativeMinimalInputActionTraceUsesMode12WithoutLegacyInputOrAssociation()
        {
            var transport = new FakeTransport();
            var owner = new FakeActionTraceOwner(transport.Calls);
            var sink = new FakeSink();
            var session = CreateSession(transport,
                sink,
                MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                actionTraceOwner: owner);

            session.StartActionTrace(0d);
            session.ObserveAction(1, 1d, Vector2.down, "/must/not/read");
            session.ObserveUgui(1, 7, Vector2.down);
            session.Disable();

            Assert.That(session.TracesInputActionCandidates, Is.True);
            Assert.That(session.RequiresInputAction, Is.False);
            Assert.That(transport.LastStartMode, Is.EqualTo(MacScrollPhaseMonitorMode.QueueMinimalTrackedSamples));
            Assert.That(transport.PollCount, Is.Zero);
            Assert.That(transport.MinimalPollCount, Is.Zero);
            Assert.That(transport.Calls, Is.EqualTo(new[] { "detach", "stop" }));
            Assert.That(sink.LastTrace.Contains("action-trace-attached callback=performed"), Is.True);
            Assert.That(sink.LastTrace.Contains("action frame="), Is.False);
            Assert.That(sink.LastTrace.Contains("ugui frame="), Is.False);
            Assert.That(sink.LastTrace.Contains("associated"), Is.False);
            Assert.That(sink.LastFailed, Is.False);
        }

        [Test]
        public void NativeMinimalInputActionTracePreflightFailureHasNoNativeLifecycle()
        {
            var transport = new FakeTransport();
            var owner = new FakeActionTraceOwner(transport.Calls);
            var sink = new FakeSink();
            var session = CreateSession(transport,
                sink,
                MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                actionTraceOwner: owner);

            session.ReportActionTracePreflightFault("action-readiness-timeout");
            session.Disable();

            Assert.That(transport.StartCount, Is.Zero);
            Assert.That(transport.StopCount, Is.Zero);
            Assert.That(owner.DetachCount, Is.EqualTo(1));
            Assert.That(session.RequiresCleanup, Is.False);
            Assert.That(transport.Calls, Is.EqualTo(new[] { "detach" }));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.True);
            Assert.That(sink.LastTrace.Contains("FAIL action-readiness-timeout"), Is.True);
            Assert.That(sink.LastTrace.Contains("monitor-start stage="), Is.False);
            Assert.That(sink.LastTrace.Contains("monitor-stop"), Is.False);
        }

        [Test]
        public void NativeMinimalInputActionTraceNativeStartFailureDetachesWithoutFakeStop()
        {
            var transport = new FakeTransport { StartResult = 5, StartLease = 0 };
            var owner = new FakeActionTraceOwner(transport.Calls);
            var sink = new FakeSink();
            var session = CreateSession(transport,
                sink,
                MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                actionTraceOwner: owner);

            session.StartActionTrace(2d);

            Assert.That(transport.StartCount, Is.EqualTo(1));
            Assert.That(transport.StopCount, Is.Zero);
            Assert.That(owner.DetachCount, Is.EqualTo(1));
            Assert.That(session.HasLease, Is.False);
            Assert.That(session.RequiresCleanup, Is.False);
            Assert.That(transport.Calls, Is.EqualTo(new[] { "detach" }));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastTrace.Contains("FAIL monitor-start-failed api=0 result=5 lease=0"), Is.True);
            Assert.That(sink.LastTrace.Contains("monitor-start stage="), Is.False);
            Assert.That(sink.LastTrace.Contains("monitor-stop"), Is.False);
            Assert.That(session.TryBeginActionRecord(Environment.CurrentManagedThreadId), Is.False);
        }

        [Test]
        public void NativeMinimalInputActionTracePublishedFailedStartRetriesDetachBeforeStop()
        {
            var transport = new FakeTransport { StartResult = 5, StartLease = 7 };
            var owner = new FakeActionTraceOwner(transport.Calls);
            owner.DetachResults.Enqueue(false);
            owner.DetachResults.Enqueue(true);
            var sink = new FakeSink();
            var session = CreateSession(transport,
                sink,
                MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                actionTraceOwner: owner);

            session.StartActionTrace(2d);

            Assert.That(session.RequiresCleanup, Is.True);
            Assert.That(session.HasLease, Is.True);
            Assert.That(transport.StopCount, Is.Zero);
            Assert.That(sink.FlushCount, Is.Zero);
            Assert.That(transport.Calls, Is.EqualTo(new[] { "detach" }));
            session.Disable();

            Assert.That(session.RequiresCleanup, Is.False);
            Assert.That(session.HasLease, Is.False);
            Assert.That(owner.DetachCount, Is.EqualTo(2));
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(transport.LastStopLease, Is.EqualTo(7));
            Assert.That(transport.Calls, Is.EqualTo(new[] { "detach", "detach", "stop" }));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastTrace.Contains("FAIL monitor-start-failed api=0 result=5 lease=7"), Is.True);
            Assert.That(sink.LastTrace.Contains("FAIL action-detach-failed"), Is.True);
            Assert.That(sink.LastTrace.Contains("monitor-start stage="), Is.False);
            Assert.That(sink.LastTrace.Contains("monitor-stop reason=component-disabled-retry"), Is.True);
        }

        [Test]
        public void NativeMinimalInputActionTracePreflightDetachFailureRetriesWithoutLease()
        {
            var transport = new FakeTransport();
            var owner = new FakeActionTraceOwner(transport.Calls);
            owner.DetachResults.Enqueue(false);
            owner.DetachResults.Enqueue(true);
            var sink = new FakeSink();
            var session = CreateSession(transport,
                sink,
                MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                actionTraceOwner: owner);

            session.ReportActionTracePreflightFault("action-owner-changed-before-ready");

            Assert.That(session.RequiresCleanup, Is.True);
            Assert.That(sink.FlushCount, Is.Zero);
            Assert.That(transport.StopCount, Is.Zero);
            session.Disable();

            Assert.That(session.RequiresCleanup, Is.False);
            Assert.That(owner.DetachCount, Is.EqualTo(2));
            Assert.That(transport.StopCount, Is.Zero);
            Assert.That(transport.Calls, Is.EqualTo(new[] { "detach", "detach" }));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastTrace.Contains("FAIL action-owner-changed-before-ready"), Is.True);
            Assert.That(sink.LastTrace.Contains("FAIL action-detach-failed"), Is.True);
            Assert.That(sink.LastTrace.Contains("monitor-stop"), Is.False);
        }

        [Test]
        public void NativeMinimalInputActionTraceKeepsZeroAndSameFrameRecordsOnIndependentTimeline()
        {
            var transport = new FakeTransport();
            transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(1, gesturePhase: 2)));
            var owner = new FakeActionTraceOwner(transport.Calls);
            var sink = new FakeSink();
            var session = CreateSession(transport,
                sink,
                MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                actionTraceOwner: owner);
            session.StartActionTrace(0d);
            CommitActionRecord(session, 7, 1d, Vector2.zero, 3, 0, 0, 64);
            CommitActionRecord(session, 7, 2d, new Vector2(1f, -2f), 9, 4, 2, 32);

            session.Update(7, 4d);

            Assert.That(transport.Calls, Is.EqualTo(new[] { "minimal-poll", "detach", "stop" }));
            Assert.That(transport.PollCount, Is.Zero);
            Assert.That(transport.MinimalInvalidDetailCount, Is.Zero);
            Assert.That(sink.LastFailed, Is.False);
            Assert.That(sink.LastTrace.Contains("action seq=1 frame=7"), Is.True);
            Assert.That(sink.LastTrace.Contains("phase=3 delta=(0,0) device=3"), Is.True);
            Assert.That(sink.LastTrace.Contains("action seq=2 frame=7"), Is.True);
            Assert.That(sink.LastTrace.Contains("device=9 controlByte=00000004 controlBit=2 controlSize=00000020"),
                Is.True);
            Assert.That(sink.LastTrace.IndexOf("minimal source=final", StringComparison.Ordinal) <
                        sink.LastTrace.IndexOf("action seq=1", StringComparison.Ordinal), Is.True);
            Assert.That(sink.LastTrace.IndexOf("action seq=2", StringComparison.Ordinal) <
                        sink.LastTrace.IndexOf("monitor-stop", StringComparison.Ordinal), Is.True);
            Assert.That(sink.LastTrace.Contains("observedClockOffset"), Is.False);
            Assert.That(sink.LastTrace.Contains("associated"), Is.False);
        }

        [Test]
        public void NativeMinimalInputActionTraceRequiresARecordAndANonZeroCandidateAtDeadline()
        {
            foreach (var addZeroRecord in new[] { false, true })
            {
                var transport = new FakeTransport();
                transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(1, gesturePhase: 2)));
                var owner = new FakeActionTraceOwner(transport.Calls);
                var sink = new FakeSink();
                var session = CreateSession(transport,
                    sink,
                    MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                    actionTraceOwner: owner);
                session.StartActionTrace(0d);
                if (addZeroRecord)
                {
                    CommitActionRecord(session, 1, 1d, Vector2.zero);
                }

                session.Update(1, 4d);

                Assert.That(sink.LastFailed, Is.True);
                Assert.That(sink.LastTrace.Contains(addZeroRecord
                    ? "FAIL no-nonzero-action-record"
                    : "FAIL no-action-record"), Is.True);
                Assert.That(transport.Calls, Is.EqualTo(new[] { "minimal-poll", "detach", "stop" }));
            }
        }

        [Test]
        public void NativeMinimalInputActionTracePrintsEveryAcceptedSlotBeforeOverflowFailure()
        {
            var transport = new FakeTransport();
            var owner = new FakeActionTraceOwner(transport.Calls);
            var sink = new FakeSink();
            var session = CreateSession(transport,
                sink,
                MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                100f,
                owner);
            session.StartActionTrace(0d);
            for (var index = 0; index < MacScrollPhaseProbeSession.MaxActionRecords; ++index)
            {
                CommitActionRecord(session,
                    1,
                    index + 1d,
                    index == 0 ? Vector2.down : Vector2.zero,
                    index + 1,
                    (uint)index,
                    (uint)(index % 8),
                    32);
            }

            Assert.That(session.TryBeginActionRecord(Environment.CurrentManagedThreadId), Is.False);
            session.Update(1, 0d);

            for (var sequence = 1; sequence <= MacScrollPhaseProbeSession.MaxActionRecords; ++sequence)
            {
                Assert.That(sink.LastTrace.Contains($"action seq={sequence} "), Is.True);
            }
            Assert.That(sink.LastTrace.Contains("FAIL action-record-overflow"), Is.True);
            Assert.That(sink.LastTrace.Contains("monitor-stop reason=fail-closed"), Is.True);
            Assert.That(sink.LastTrace.Contains("trace-truncated"), Is.False);
            Assert.That(transport.Calls, Is.EqualTo(new[] { "detach", "stop" }));
            Assert.That(Encoding.UTF8.GetByteCount(sink.LastTrace) <= MacScrollPhaseProbeSession.MaxTraceUtf8Bytes,
                Is.True);
            Assert.That(Utf8CrLfBytes(sink.LastTrace) <= MacScrollPhaseProbeSession.MaxTraceUtf8Bytes, Is.True);
            Assert.That(LineCount(sink.LastTrace) <= MacScrollPhaseProbeSession.MaxConsoleTraceLines, Is.True);
        }

        [Test]
        public void NativeMinimalInputActionTracePreservesReason3WhenActionOverflowIsSecondary()
        {
            var transport = new FakeTransport();
            transport.MinimalPolls.Enqueue(new FakeMinimalPollResponse(0,
                (int)MacScrollPhaseMonitorResult.CaptureInvalid,
                new MacScrollPhaseMinimalPoll((int)MacScrollPhaseCaptureInvalidReason.InvalidGestureTransition,
                    false,
                    false,
                    0,
                    default)));
            transport.MinimalInvalidDetails.Enqueue(new FakeMinimalInvalidDetailResponse(0,
                0,
                MinimalInvalidDetail()));
            var owner = new FakeActionTraceOwner(transport.Calls);
            var sink = new FakeSink();
            var session = CreateSession(transport,
                sink,
                MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                actionTraceOwner: owner);
            session.StartActionTrace(0d);
            for (var index = 0; index < MacScrollPhaseProbeSession.MaxActionRecords; ++index)
            {
                CommitActionRecord(session, 1, index + 1d, Vector2.down);
            }
            Assert.That(session.TryBeginActionRecord(Environment.CurrentManagedThreadId), Is.False);

            session.Update(1, 4d);

            Assert.That(transport.Calls,
                Is.EqualTo(new[] { "minimal-poll", "minimal-invalid-detail", "detach", "stop" }));
            Assert.That(sink.LastTrace.Contains("invalid-detail failure=3"), Is.True);
            Assert.That(sink.LastTrace.Contains(
                "FAIL native-capture-invalid reason=3 secondary=action-record-overflow"), Is.True);
            Assert.That(sink.LastTrace.Contains("trace-truncated"), Is.False);
            AssertTerminalOrder(sink.LastTrace, "invalid-detail failure=3");
        }

        [Test]
        public void NativeMinimalInputActionTraceRejectsWrongThreadBeforeWritingAnySlot()
        {
            var transport = new FakeTransport();
            var owner = new FakeActionTraceOwner(transport.Calls);
            var sink = new FakeSink();
            var session = CreateSession(transport,
                sink,
                MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                actionTraceOwner: owner);
            session.StartActionTrace(0d);

            Assert.That(session.TryBeginActionRecord(Environment.CurrentManagedThreadId + 1), Is.False);
            session.Update(1, 0d);

            Assert.That(sink.LastTrace.Contains("FAIL action-callback-wrong-thread"), Is.True);
            Assert.That(sink.LastTrace.Contains("action seq="), Is.False);
            Assert.That(transport.Calls, Is.EqualTo(new[] { "detach", "stop" }));
        }

        [Test]
        public void NativeMinimalInputActionTraceRetriesDetachBeforeFirstNativeStopAndFlush()
        {
            var transport = new FakeTransport();
            transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(1, gesturePhase: 2)));
            var owner = new FakeActionTraceOwner(transport.Calls);
            owner.DetachResults.Enqueue(false);
            owner.DetachResults.Enqueue(true);
            var sink = new FakeSink();
            var session = CreateSession(transport,
                sink,
                MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                actionTraceOwner: owner);
            session.StartActionTrace(0d);
            CommitActionRecord(session, 1, 1d, Vector2.down);

            session.Update(1, 4d);

            Assert.That(session.HasLease, Is.True);
            Assert.That(transport.StopCount, Is.Zero);
            Assert.That(sink.FlushCount, Is.Zero);
            Assert.That(session.TryBeginActionRecord(Environment.CurrentManagedThreadId), Is.False);
            Assert.That(transport.Calls, Is.EqualTo(new[] { "minimal-poll", "detach" }));

            session.Disable();

            Assert.That(session.HasLease, Is.False);
            Assert.That(transport.Calls, Is.EqualTo(new[] { "minimal-poll", "detach", "detach", "stop" }));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.True);
            Assert.That(sink.LastTrace.Contains("FAIL action-detach-failed"), Is.True);
            Assert.That(sink.LastTrace.IndexOf("FAIL action-detach-failed", StringComparison.Ordinal) <
                        sink.LastTrace.IndexOf("monitor-stop", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void NativeMinimalInputActionTraceRetainsLeaseAcrossNativeStopRetryAfterDetach()
        {
            var transport = new FakeTransport();
            transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(1, gesturePhase: 2)));
            transport.StopResults.Enqueue(5);
            transport.StopResults.Enqueue(0);
            var owner = new FakeActionTraceOwner(transport.Calls);
            var sink = new FakeSink();
            var session = CreateSession(transport,
                sink,
                MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                actionTraceOwner: owner);
            session.StartActionTrace(0d);
            CommitActionRecord(session, 1, 1d, Vector2.down);

            session.Update(1, 4d);

            Assert.That(session.HasLease, Is.True);
            Assert.That(sink.FlushCount, Is.Zero);
            Assert.That(transport.Calls, Is.EqualTo(new[] { "minimal-poll", "detach", "stop" }));

            session.Disable();

            Assert.That(session.HasLease, Is.False);
            Assert.That(owner.DetachCount, Is.EqualTo(1));
            Assert.That(transport.Calls, Is.EqualTo(new[] { "minimal-poll", "detach", "stop", "stop" }));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.True);
            Assert.That(sink.LastTrace.Contains("FAIL monitor-stop-failed"), Is.True);
        }

        [Test]
        public void NativeMinimalInputActionTraceFiveLineTerminalPartitionSurvivesAllRetryClasses()
        {
            var transport = new FakeTransport();
            transport.MinimalPolls.Enqueue(new FakeMinimalPollResponse(0,
                (int)MacScrollPhaseMonitorResult.CaptureInvalid,
                new MacScrollPhaseMinimalPoll((int)MacScrollPhaseCaptureInvalidReason.InvalidGestureTransition,
                    false,
                    false,
                    0,
                    default)));
            transport.MinimalInvalidDetails.Enqueue(new FakeMinimalInvalidDetailResponse(0,
                int.MinValue,
                new MacScrollPhaseMinimalInvalidDetail(int.MinValue,
                    int.MinValue,
                    int.MinValue,
                    long.MinValue,
                    long.MinValue,
                    ulong.MaxValue,
                    ulong.MaxValue,
                    long.MinValue)));
            transport.StartLease = long.MaxValue;
            transport.StopApiResults.Enqueue(long.MinValue);
            transport.StopApiResults.Enqueue(0);
            transport.StopResults.Enqueue(int.MinValue);
            transport.StopResults.Enqueue(0);
            var owner = new FakeActionTraceOwner(transport.Calls);
            owner.DetachResults.Enqueue(false);
            owner.DetachResults.Enqueue(true);
            var sink = new FakeSink();
            var session = CreateSession(transport,
                sink,
                MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                actionTraceOwner: owner);
            session.StartActionTrace(0d);
            CommitActionRecord(session, 1, 1d, Vector2.down);

            session.Update(1, 0d);
            session.Disable();
            Assert.That(sink.FlushCount, Is.Zero);
            session.Disable();

            Assert.That(session.HasLease, Is.False);
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastTrace.Contains("trace-truncated"), Is.False);
            var terminalStart = sink.LastTrace.IndexOf("[MILESTRO_SCROLL_PHASE_POC] invalid-detail",
                StringComparison.Ordinal);
            var terminal = sink.LastTrace.Substring(terminalStart);
            Assert.That(LineCount(terminal), Is.EqualTo(MacScrollPhaseProbeSession.MaxActionTerminalTraceLines));
            Assert.That(Utf8CrLfBytes(terminal),
                Is.LessThanOrEqualTo(MacScrollPhaseProbeSession.MaxActionTerminalTraceUtf8Bytes));
            Assert.That(transport.Calls,
                Is.EqualTo(new[]
                {
                    "minimal-poll",
                    "minimal-invalid-detail",
                    "detach",
                    "detach",
                    "stop",
                    "stop"
                }));
        }

        [Test]
        public void NativeMinimalInputActionTraceValidatesNumericRecordContractAndOrder()
        {
            var invalidRecords = new Action<MacScrollPhaseProbeSession>[]
            {
                session => CommitActionRecord(session, 1, double.NaN, Vector2.down),
                session => CommitActionRecord(session, 1, -1d, Vector2.down),
                session => CommitActionRecord(session, 1, 1d, new Vector2(float.NaN, 0f)),
                session => CommitActionRecord(session, 1, 1d, Vector2.down, phase: 4),
                session => CommitActionRecord(session, 1, 1d, Vector2.down, deviceId: 0),
                session => CommitActionRecord(session, 1, 1d, Vector2.down, byteOffset: uint.MaxValue),
                session => CommitActionRecord(session, 1, 1d, Vector2.down, bitOffset: 8),
                session => CommitActionRecord(session, 1, 1d, Vector2.down, sizeInBits: 0),
                session => CommitActionRecord(session, 1, 1d, Vector2.down, timestampTicks: 0)
            };

            foreach (var commitInvalidRecord in invalidRecords)
            {
                var transport = new FakeTransport();
                var owner = new FakeActionTraceOwner(transport.Calls);
                var sink = new FakeSink();
                var session = CreateSession(transport,
                    sink,
                    MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                    actionTraceOwner: owner);
                session.StartActionTrace(0d);
                commitInvalidRecord(session);

                session.Update(1, 0d);

                Assert.That(sink.LastFailed, Is.True);
                Assert.That(sink.LastTrace.Contains("FAIL action-record-contract-invalid"), Is.True);
                Assert.That(sink.LastTrace.Contains("action seq=1 "), Is.True);
                Assert.That(transport.Calls, Is.EqualTo(new[] { "detach", "stop" }));
            }

            var orderTransport = new FakeTransport();
            var orderOwner = new FakeActionTraceOwner(orderTransport.Calls);
            var orderSink = new FakeSink();
            var orderSession = CreateSession(orderTransport,
                orderSink,
                MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                actionTraceOwner: orderOwner);
            orderSession.StartActionTrace(0d);
            CommitActionRecord(orderSession, 2, 2d, Vector2.down);
            orderSession.Update(2, 0d);
            CommitActionRecord(orderSession, 1, 1d, Vector2.down);

            orderSession.Update(2, 0d);

            Assert.That(orderSink.LastTrace.Contains("FAIL action-record-order-invalid"), Is.True);

            var tickTransport = new FakeTransport();
            var tickOwner = new FakeActionTraceOwner(tickTransport.Calls);
            var tickSink = new FakeSink();
            var tickSession = CreateSession(tickTransport,
                tickSink,
                MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                actionTraceOwner: tickOwner);
            tickSession.StartActionTrace(0d);
            CommitActionRecord(tickSession, 1, 1d, Vector2.down, timestampTicks: long.MaxValue);
            tickSession.Update(1, 0d);
            CommitActionRecord(tickSession, 1, 2d, Vector2.down, timestampTicks: long.MaxValue - 1);

            tickSession.Update(1, 0d);

            Assert.That(tickSink.LastTrace.Contains("FAIL action-record-order-invalid"), Is.True);

            var contextTransport = new FakeTransport();
            var contextOwner = new FakeActionTraceOwner(contextTransport.Calls);
            var contextSink = new FakeSink();
            var contextSession = CreateSession(contextTransport,
                contextSink,
                MacScrollPhaseProbeStage.NativeMinimalInputActionTrace,
                actionTraceOwner: contextOwner);
            contextSession.StartActionTrace(0d);
            var contextTicks = Stopwatch.GetTimestamp();
            CommitActionRecord(contextSession,
                1,
                2d,
                Vector2.down,
                timestampTicks: contextTicks + 1);
            CommitActionRecord(contextSession,
                1,
                1d,
                Vector2.down,
                timestampTicks: contextTicks + 2);

            contextSession.Update(1, 0d);
            contextSession.Disable();

            Assert.That(contextSink.LastFailed, Is.False);
            Assert.That(contextSink.LastTrace.Contains("action seq=1 frame=1"), Is.True);
            Assert.That(contextSink.LastTrace.Contains("contextTime=2"), Is.True);
            Assert.That(contextSink.LastTrace.Contains("action seq=2 frame=1"), Is.True);
            Assert.That(contextSink.LastTrace.Contains("contextTime=1"), Is.True);
            Assert.That(contextSink.LastTrace.Contains("action-record-order-invalid"), Is.False);
            Assert.That(string.Join(",", contextTransport.Calls), Is.EqualTo("minimal-poll,detach,stop"));
        }

        [Test]
        public void NativeMinimalInputActionTraceActionLineHasProvenCrLfUpperBound()
        {
            var record = new MacScrollActionRecord(MacScrollPhaseProbeSession.MaxActionRecords,
                int.MinValue,
                long.MinValue,
                double.MinValue,
                int.MinValue,
                float.MinValue,
                float.MinValue,
                int.MinValue,
                uint.MaxValue,
                uint.MaxValue,
                uint.MaxValue);

            var line = "[MILESTRO_SCROLL_PHASE_POC] " +
                       MacScrollPhaseProbeSession.FormatActionRecord(record, 1) + "\r\n";

            Assert.That(Encoding.UTF8.GetByteCount(line),
                Is.LessThanOrEqualTo(MacScrollPhaseProbeSession.MaxActionRecordTraceUtf8Bytes));
            Assert.That(MacScrollPhaseProbeSession.MaxActionGeneralTraceUtf8Bytes +
                        MacScrollPhaseProbeSession.MaxActionRecords *
                        MacScrollPhaseProbeSession.MaxActionRecordTraceUtf8Bytes +
                        MacScrollPhaseProbeSession.MaxActionTerminalTraceUtf8Bytes,
                Is.EqualTo(MacScrollPhaseProbeSession.MaxTraceUtf8Bytes));
            Assert.That(MacScrollPhaseProbeSession.MaxActionGeneralTraceLines +
                        MacScrollPhaseProbeSession.MaxActionRecords +
                        MacScrollPhaseProbeSession.MaxActionTerminalTraceLines,
                Is.EqualTo(MacScrollPhaseProbeSession.MaxConsoleTraceLines));
        }

        [Test]
        public void NativeMinimalPollingRequiresValidatedCleanBeganAtDeadline()
        {
            var transport = new FakeTransport();
            transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(1,
                gestureId: 0,
                gesturePhase: 7,
                validFields: MinimalBaseFields)));
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalPolling);
            session.Start(0d);

            session.Update(1, 4d);

            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.True);
            Assert.That(sink.LastTrace.Contains("no-resolved-minimal-gesture"), Is.True);
        }

        [Test]
        public void NativeMinimalPollingAcceptsExactly32DrainedSamplesWithoutInventingLateBacklog()
        {
            var transport = new FakeTransport();
            for (var sequence = 1; sequence <= MacScrollPhaseProbeSession.MaxPollsPerFrame; ++sequence)
            {
                var remaining = MacScrollPhaseProbeSession.MaxPollsPerFrame - sequence;
                transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(sequence,
                    gesturePhase: sequence == 1 ? 2 : 3), remaining));
            }
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalPolling);
            session.Start(0d);

            session.Update(1, 0d);
            session.LateUpdate(1);

            Assert.That(transport.MinimalPollCount, Is.EqualTo(MacScrollPhaseProbeSession.MaxPollsPerFrame));
            Assert.That(transport.StopCount, Is.Zero);
            Assert.That(sink.FlushCount, Is.Zero);
            session.Disable();
            Assert.That(sink.LastFailed, Is.False);
        }

        [Test]
        public void NativeMinimalPollingFailsOnlyWhenActual32ndPollReportsRemaining()
        {
            var transport = new FakeTransport();
            for (var sequence = 1; sequence <= MacScrollPhaseProbeSession.MaxPollsPerFrame; ++sequence)
            {
                var remaining = MacScrollPhaseProbeSession.MaxPollsPerFrame - sequence + 1;
                transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(sequence,
                    gesturePhase: sequence == 1 ? 2 : 3), remaining));
            }
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalPolling);
            session.Start(0d);

            session.Update(1, 0d);

            Assert.That(transport.MinimalPollCount, Is.EqualTo(MacScrollPhaseProbeSession.MaxPollsPerFrame));
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.True);
            Assert.That(sink.LastTrace.Contains("minimal-poll-budget-exhausted"), Is.True);
        }

        [Test]
        public void NativeMinimalPollingStopsOnCaptureInvalidWithoutConsumingPayload()
        {
            var transport = new FakeTransport();
            transport.MinimalPolls.Enqueue(new FakeMinimalPollResponse(0,
                (int)MacScrollPhaseMonitorResult.CaptureInvalid,
                new MacScrollPhaseMinimalPoll((int)MacScrollPhaseCaptureInvalidReason.InvalidGestureTransition,
                    false,
                    false,
                    0,
                    default)));
            transport.MinimalInvalidDetails.Enqueue(new FakeMinimalInvalidDetailResponse(0,
                0,
                MinimalInvalidDetail()));
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalPolling);
            session.Start(0d);

            session.Update(1, 0d);

            Assert.That(transport.MinimalPollCount, Is.EqualTo(1));
            Assert.That(transport.MinimalInvalidDetailCount, Is.EqualTo(1));
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.True);
            Assert.That(transport.Calls[0], Is.EqualTo("minimal-poll"));
            Assert.That(transport.Calls[1], Is.EqualTo("minimal-invalid-detail"));
            Assert.That(transport.Calls[2], Is.EqualTo("stop"));
            Assert.That(sink.LastTrace.Contains("invalid-detail failure=3 state=1 prior=7 seq=41"), Is.True);
            Assert.That(sink.LastTrace.Contains("native-capture-invalid reason=3"), Is.True);
            Assert.That(sink.LastTrace.Contains("monitor-stop reason=fail-closed"), Is.True);
            Assert.That(sink.LastTrace.Contains("trace-truncated"), Is.False);
            AssertTerminalOrder(sink.LastTrace, "invalid-detail failure=3");
        }

        [Test]
        public void NativeMinimalPollingNeverQueriesDetailForCapacityOrSequenceFailure()
        {
            foreach (var reason in new[]
                     {
                         MacScrollPhaseCaptureInvalidReason.CapacityExceeded,
                         MacScrollPhaseCaptureInvalidReason.SequenceExhausted
                     })
            {
                var transport = new FakeTransport();
                transport.MinimalPolls.Enqueue(new FakeMinimalPollResponse(0,
                    (int)MacScrollPhaseMonitorResult.CaptureInvalid,
                    new MacScrollPhaseMinimalPoll((int)reason, false, false, 0, default)));
                var sink = new FakeSink();
                var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalPolling);
                session.Start(0d);

                session.Update(1, 0d);

                Assert.That(transport.MinimalInvalidDetailCount, Is.Zero);
                Assert.That(transport.StopCount, Is.EqualTo(1));
                Assert.That(sink.LastTrace.Contains($"native-capture-invalid reason={(int)reason}"), Is.True);
            }
        }

        [Test]
        public void NativeMinimalPollingPreservesReason3WhenDetailQueryOrContractFails()
        {
            var invalidDetails = new[]
            {
                new FakeMinimalInvalidDetailResponse(9, 0, default),
                new FakeMinimalInvalidDetailResponse(0, 5, default),
                new FakeMinimalInvalidDetailResponse(0, 0, default),
                new FakeMinimalInvalidDetailResponse(0, 0, MinimalInvalidDetail(hasDetail: 0)),
                new FakeMinimalInvalidDetailResponse(0, 0, MinimalInvalidDetail(hasDetail: 2)),
                new FakeMinimalInvalidDetailResponse(0, 0, MinimalInvalidDetail(failure: 0)),
                new FakeMinimalInvalidDetailResponse(0, 0, MinimalInvalidDetail(priorTrackerState: 4)),
                new FakeMinimalInvalidDetailResponse(0, 0, MinimalInvalidDetail(sequence: 0)),
                new FakeMinimalInvalidDetailResponse(0, 0, MinimalInvalidDetail(priorTrackerState: 0,
                    priorGestureId: 7)),
                new FakeMinimalInvalidDetailResponse(0, 0, MinimalInvalidDetail(priorTrackerState: 1,
                    priorGestureId: 0)),
                new FakeMinimalInvalidDetailResponse(0, 5, MinimalInvalidDetail())
            };

            foreach (var invalidDetail in invalidDetails)
            {
                var transport = new FakeTransport();
                transport.MinimalPolls.Enqueue(new FakeMinimalPollResponse(0,
                    (int)MacScrollPhaseMonitorResult.CaptureInvalid,
                    new MacScrollPhaseMinimalPoll((int)MacScrollPhaseCaptureInvalidReason.InvalidGestureTransition,
                        false,
                        false,
                        0,
                        default)));
                transport.MinimalInvalidDetails.Enqueue(invalidDetail);
                var sink = new FakeSink();
                var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalPolling);
                session.Start(0d);

                session.Update(1, 0d);

                Assert.That(transport.MinimalInvalidDetailCount, Is.EqualTo(1));
                Assert.That(transport.StopCount, Is.EqualTo(1));
                Assert.That(sink.LastTrace.Contains("invalid-detail-query-failed") ||
                            sink.LastTrace.Contains("invalid-detail-contract-failed"), Is.True);
                Assert.That(sink.LastTrace.Contains("FAIL native-capture-invalid reason=3"), Is.True);
                Assert.That(sink.LastTrace.Contains("monitor-stop reason=fail-closed"), Is.True);
                Assert.That(sink.LastTrace.Contains("trace-truncated"), Is.False);
                AssertTerminalOrder(sink.LastTrace, "invalid-detail-");
            }
        }

        [Test]
        public void NativeMinimalPollingReservesThreeReason3TerminalLinesNearTraceLimit()
        {
            var transport = new FakeTransport();
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalPolling, 100f);
            session.Start(0d);
            for (var sequence = 1; sequence <= 34; ++sequence)
            {
                transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(sequence,
                    gesturePhase: sequence == 1 ? 2 : 3)));
                session.Update(sequence, 0d);
            }
            transport.MinimalPolls.Enqueue(new FakeMinimalPollResponse(0,
                (int)MacScrollPhaseMonitorResult.CaptureInvalid,
                new MacScrollPhaseMinimalPoll((int)MacScrollPhaseCaptureInvalidReason.InvalidGestureTransition,
                    false,
                    false,
                    0,
                    default)));
            transport.MinimalInvalidDetails.Enqueue(new FakeMinimalInvalidDetailResponse(0,
                int.MinValue,
                new MacScrollPhaseMinimalInvalidDetail(int.MinValue,
                    int.MinValue,
                    int.MinValue,
                    long.MinValue,
                    long.MinValue,
                    ulong.MaxValue,
                    ulong.MaxValue,
                    long.MinValue)));

            session.Update(35, 0d);

            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(Encoding.UTF8.GetByteCount(sink.LastTrace) <= MacScrollPhaseProbeSession.MaxTraceUtf8Bytes,
                Is.True);
            Assert.That(LineCount(sink.LastTrace) <= MacScrollPhaseProbeSession.MaxConsoleTraceLines, Is.True);
            Assert.That(sink.LastTrace.Contains("seq=34"), Is.True);
            Assert.That(sink.LastTrace.Contains("invalid-detail-contract-failed"), Is.True);
            Assert.That(sink.LastTrace.Contains("FAIL native-capture-invalid reason=3"), Is.True);
            Assert.That(sink.LastTrace.Contains("monitor-stop reason=fail-closed"), Is.True);
            Assert.That(sink.LastTrace.Contains("trace-truncated"), Is.False);
            AssertTerminalOrder(sink.LastTrace, "invalid-detail-contract-failed");
        }

        [Test]
        public void NativeMinimalPollingRejectsInvalidResultCombinations()
        {
            var responses = new[]
            {
                new FakeMinimalPollResponse(0,
                    (int)MacScrollPhaseMonitorResult.Succeeded,
                    new MacScrollPhaseMinimalPoll((int)MacScrollPhaseCaptureInvalidReason.CapacityExceeded,
                        false,
                        false,
                        0,
                        default)),
                new FakeMinimalPollResponse(0,
                    (int)MacScrollPhaseMonitorResult.CaptureInvalid,
                    new MacScrollPhaseMinimalPoll((int)MacScrollPhaseCaptureInvalidReason.None,
                        false,
                        false,
                        0,
                        default)),
                new FakeMinimalPollResponse(0,
                    (int)MacScrollPhaseMonitorResult.CaptureInvalid,
                    new MacScrollPhaseMinimalPoll((int)MacScrollPhaseCaptureInvalidReason.CapacityExceeded,
                        true,
                        false,
                        0,
                        MinimalSample(1, gesturePhase: 2))),
                new FakeMinimalPollResponse(0,
                    5,
                    new MacScrollPhaseMinimalPoll(0, true, false, 0, MinimalSample(1, gesturePhase: 2)))
            };

            foreach (var response in responses)
            {
                var transport = new FakeTransport();
                transport.MinimalPolls.Enqueue(response);
                var sink = new FakeSink();
                var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalPolling);
                session.Start(0d);

                session.Update(1, 0d);

                Assert.That(transport.StopCount, Is.EqualTo(1));
                Assert.That(sink.LastFailed, Is.True);
            }
        }

        [Test]
        public void NativeMinimalPollingRejectsEmptyAndRemainingContractMismatches()
        {
            var polls = new[]
            {
                new MacScrollPhaseMinimalPoll(0, false, true, 1, default),
                new MacScrollPhaseMinimalPoll(0, true, false, 1, MinimalSample(1, gesturePhase: 2)),
                new MacScrollPhaseMinimalPoll(0, true, true, 0, MinimalSample(1, gesturePhase: 2)),
                new MacScrollPhaseMinimalPoll(0, true, true, 256, MinimalSample(1, gesturePhase: 2))
            };

            foreach (var poll in polls)
            {
                var transport = new FakeTransport();
                transport.MinimalPolls.Enqueue(new FakeMinimalPollResponse(0, 0, poll));
                var sink = new FakeSink();
                var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalPolling);
                session.Start(0d);

                session.Update(1, 0d);

                Assert.That(transport.StopCount, Is.EqualTo(1));
                Assert.That(sink.LastFailed, Is.True);
            }
        }

        [Test]
        public void NativeMinimalPollingRejectsSequenceValidityPhaseAndFiniteContractViolations()
        {
            var invalidSamples = new[]
            {
                MinimalSample(2, gesturePhase: 2),
                MinimalSample(1, gesturePhase: 2, validFields: MinimalBaseFields),
                MinimalSample(1,
                    gesturePhase: 2,
                    validFields: MinimalResolvedFields | (1U << 5)),
                MinimalSample(1,
                    gestureId: 0,
                    gesturePhase: 1,
                    validFields: MinimalResolvedFields),
                MinimalSample(1,
                    gestureId: 0,
                    gesturePhase: 2,
                    validFields: MinimalBaseFields),
                MinimalSample(1, gesturePhase: 0),
                MinimalSample(1, gesturePhase: 2, timestamp: double.NaN),
                MinimalSample(1, gesturePhase: 2, scrollingDeltaX: double.PositiveInfinity)
            };

            foreach (var sample in invalidSamples)
            {
                var transport = new FakeTransport();
                transport.MinimalPolls.Enqueue(MinimalResponse(sample));
                var sink = new FakeSink();
                var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalPolling);
                session.Start(0d);

                session.Update(1, 0d);

                Assert.That(transport.StopCount, Is.EqualTo(1));
                Assert.That(sink.LastFailed, Is.True);
            }
        }

        [Test]
        public void NativeMinimalPollingAcceptsZeroDeltaAndWindowPerCleanGestureWhileIgnoringNoGestureWindow()
        {
            var transport = new FakeTransport();
            transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(1,
                windowNumber: 0,
                scrollingDeltaY: 0d,
                gesturePhase: 2), 3));
            transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(2,
                windowNumber: 0,
                scrollingDeltaY: 0d,
                gesturePhase: 5), 2));
            transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(3,
                gestureId: 0,
                windowNumber: 999,
                scrollingDeltaY: 0d,
                gesturePhase: 1,
                validFields: MinimalBaseFields), 1));
            transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(4,
                gestureId: 2,
                windowNumber: 5,
                scrollingDeltaY: 0d,
                gesturePhase: 2)));
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalPolling);
            session.Start(0d);

            session.Update(1, 0d);
            session.Disable();

            Assert.That(transport.MinimalPollCount, Is.EqualTo(4));
            Assert.That(sink.LastFailed, Is.False);
            Assert.That(sink.LastTrace.Contains("gesture=unavailable"), Is.True);
        }

        [Test]
        public void NativeMinimalPollingRejectsSameGestureWindowChangeAndGestureIdJump()
        {
            var invalidSequences = new[]
            {
                new[]
                {
                    MinimalResponse(MinimalSample(1, windowNumber: 4, gesturePhase: 2), 1),
                    MinimalResponse(MinimalSample(2, windowNumber: 5, gesturePhase: 3))
                },
                new[]
                {
                    MinimalResponse(MinimalSample(1, gesturePhase: 2), 1),
                    MinimalResponse(MinimalSample(2, gestureId: 3, gesturePhase: 2))
                },
                new[]
                {
                    MinimalResponse(MinimalSample(1, gesturePhase: 2), 2),
                    MinimalResponse(MinimalSample(2, gestureId: 2, gesturePhase: 2), 1),
                    MinimalResponse(MinimalSample(3, gestureId: 1, gesturePhase: 3))
                }
            };

            foreach (var responses in invalidSequences)
            {
                var transport = new FakeTransport();
                foreach (var response in responses)
                {
                    transport.MinimalPolls.Enqueue(response);
                }
                var sink = new FakeSink();
                var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalPolling);
                session.Start(0d);

                session.Update(1, 0d);

                Assert.That(transport.StopCount, Is.EqualTo(1));
                Assert.That(sink.LastFailed, Is.True);
            }
        }

        [Test]
        public void NativeMinimalPollingRejectsSequenceGapAfterValidatedFirstSample()
        {
            var transport = new FakeTransport();
            transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(1, gesturePhase: 2), 1));
            transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(3, gesturePhase: 3)));
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalPolling);
            session.Start(0d);

            session.Update(1, 0d);

            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.True);
            Assert.That(sink.LastTrace.Contains("minimal-sequence expected=2 actual=3"), Is.True);
        }

        [Test]
        public void NativeMinimalPollingTraceCapacityFailsClosedAndKeepsLeaseForStopRetry()
        {
            var transport = new FakeTransport();
            transport.StopResults.Enqueue(5);
            transport.StopResults.Enqueue(0);
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeMinimalPolling, 100f);
            session.Start(0d);
            var sequence = 1;
            for (var frame = 1; frame < 100 && !session.IsFinished; ++frame)
            {
                transport.MinimalPolls.Enqueue(MinimalResponse(MinimalSample(sequence,
                    gesturePhase: sequence == 1 ? 2 : 3)));
                session.Update(frame, 0d);
                ++sequence;
            }

            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.True);
            Assert.That(session.HasLease, Is.True);
            Assert.That(Encoding.UTF8.GetByteCount(sink.LastTrace) <= MacScrollPhaseProbeSession.MaxTraceUtf8Bytes,
                Is.True);
            Assert.That(LineCount(sink.LastTrace) <= MacScrollPhaseProbeSession.MaxConsoleTraceLines, Is.True);
            Assert.That(sink.LastTrace.Contains("minimal source=update"), Is.True);
            Assert.That(sink.LastTrace.Contains("FAIL managed-trace-capacity-exhausted"), Is.True);
            Assert.That(sink.LastTrace.Contains("monitor-stop reason=fail-closed"), Is.True);
            Assert.That(sink.LastTrace.Contains("trace-truncated"), Is.False);
            session.Disable();
            Assert.That(session.HasLease, Is.False);
            Assert.That(transport.StopCount, Is.EqualTo(2));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
        }

        [Test]
        public void NativePollingStopsAtSharedFrameBudgetAndFlushesOneCappedBatch()
        {
            var transport = new FakeTransport();
            for (var sequence = 1; sequence <= 100; ++sequence)
            {
                transport.Samples.Enqueue(Sample(sequence));
            }
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativePolling);

            session.Start(0d);
            session.Update(1, 0d);

            Assert.That(transport.PollCount, Is.EqualTo(MacScrollPhaseProbeSession.MaxPollsPerFrame));
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.True);
            Assert.That(Encoding.UTF8.GetByteCount(sink.LastTrace) <= MacScrollPhaseProbeSession.MaxTraceUtf8Bytes,
                Is.True);
            Assert.That(LineCount(sink.LastTrace) <= MacScrollPhaseProbeSession.MaxConsoleTraceLines, Is.True);
        }

        [Test]
        public void SmallCaptureHasNoLiveLogAndFlushesOnceAfterDisable()
        {
            var transport = new FakeTransport();
            transport.Samples.Enqueue(Sample(1));
            transport.Samples.Enqueue(Sample(2));
            transport.Samples.Enqueue(Sample(3));
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativePolling);

            session.Start(0d);
            session.Update(1, 0d);

            Assert.That(transport.PollCount, Is.EqualTo(4));
            Assert.That(sink.FlushCount, Is.Zero);
            session.Disable();
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.False);
        }

        [Test]
        public void PollAndStopFailureRetainsLeaseForDisableRetryWithoutSecondFlush()
        {
            var transport = new FakeTransport { PollResult = 5 };
            transport.StopResults.Enqueue(5);
            transport.StopResults.Enqueue(0);
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativePolling);

            session.Start(0d);
            session.Update(1, 0d);

            Assert.That(session.HasLease, Is.True);
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.True);
            session.Disable();
            Assert.That(session.HasLease, Is.False);
            Assert.That(transport.StopCount, Is.EqualTo(2));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
        }

        [Test]
        public void FailedStartWithPublishedLeaseStillStopsThatOwner()
        {
            var transport = new FakeTransport { StartResult = 3 };
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativeLifecycle);

            session.Start(0d);

            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(transport.LastStopLease, Is.EqualTo(1));
            Assert.That(session.HasLease, Is.False);
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.True);
        }

        [Test]
        public void InputActionCallbackNeverPollsOrLogsLive()
        {
            var transport = new FakeTransport();
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.InputAction);
            session.Start(0d);

            session.RecordActionAttachment("ScrollWheel");
            session.ObserveAction(1, 1d, new Vector2(0f, -1f), "/Mouse/scroll");

            Assert.That(transport.PollCount, Is.Zero);
            Assert.That(sink.FlushCount, Is.Zero);
            session.Disable();
            Assert.That(sink.FlushCount, Is.EqualTo(1));
        }

        [Test]
        public void UpdateAndLateUpdateShareOnePollBudget()
        {
            var transport = new FakeTransport();
            for (var sequence = 1; sequence <= 30; ++sequence)
            {
                transport.Samples.Enqueue(Sample(sequence, delta: Vector2.zero));
            }
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.UguiAssociation);
            session.Start(0d);
            session.Update(1, 0d);
            transport.Samples.Enqueue(Sample(31, delta: Vector2.zero));
            transport.Samples.Enqueue(Sample(32, delta: Vector2.zero));

            session.LateUpdate(1);

            Assert.That(transport.PollCount, Is.EqualTo(MacScrollPhaseProbeSession.MaxPollsPerFrame));
            Assert.That(transport.StopCount, Is.EqualTo(1));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.True);
        }

        [Test]
        public void UguiStageAssociatesAndAcceptsBoundWindowMomentumEnd()
        {
            var transport = new FakeTransport();
            transport.Samples.Enqueue(Sample(1, gesturePhase: 2));
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.UguiAssociation);
            session.Start(0d);
            session.ObserveAction(1, 1d, new Vector2(0f, -1f), "/Mouse/scroll");
            session.ObserveUgui(1, 7, new Vector2(0f, -1f));
            session.Update(1, 0d);
            transport.Samples.Enqueue(Sample(2,
                delta: Vector2.zero,
                gesturePhase: 1,
                momentumPhase: 5));

            session.Update(2, 0d);
            session.Disable();

            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.False);
        }

        [Test]
        public void UguiStageRejectsCrossWindowZeroDeltaMomentumEnd()
        {
            var transport = new FakeTransport();
            transport.Samples.Enqueue(Sample(1, gesturePhase: 2));
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.UguiAssociation);
            session.Start(0d);
            session.ObserveAction(1, 1d, new Vector2(0f, -1f), "/Mouse/scroll");
            session.ObserveUgui(1, 7, new Vector2(0f, -1f));
            session.Update(1, 0d);
            transport.Samples.Enqueue(Sample(2,
                windowNumber: 5,
                keyWindowNumber: 5,
                delta: Vector2.zero,
                gesturePhase: 1,
                momentumPhase: 5));

            session.Update(2, 0d);

            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.True);
        }

        [Test]
        public void TraceCapacityFailureStillEmitsAtMostOneBoundedBatch()
        {
            var transport = new FakeTransport();
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.NativePolling, 100f);
            session.Start(0d);
            var sequence = 1;
            for (var frame = 1; frame < 100 && !session.IsFinished; ++frame)
            {
                transport.Samples.Enqueue(Sample(sequence++));
                session.Update(frame, 0d);
            }

            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.True);
            Assert.That(Encoding.UTF8.GetByteCount(sink.LastTrace) <= MacScrollPhaseProbeSession.MaxTraceUtf8Bytes,
                Is.True);
            Assert.That(LineCount(sink.LastTrace) <= MacScrollPhaseProbeSession.MaxConsoleTraceLines, Is.True);
        }

        [Test]
        public void TraceCapacityUsesUtf8BytesForNonAsciiEvidence()
        {
            var transport = new FakeTransport();
            var sink = new FakeSink();
            var session = CreateSession(transport, sink, MacScrollPhaseProbeStage.InputAction, 100f);
            session.Start(0d);
            var nonAsciiName = new string('\u754c', 100);
            for (var index = 0; index < 100 && !session.IsFinished; ++index)
            {
                session.RecordActionAttachment(nonAsciiName);
            }

            Assert.That(sink.FlushCount, Is.EqualTo(1));
            Assert.That(sink.LastFailed, Is.True);
            Assert.That(Encoding.UTF8.GetByteCount(sink.LastTrace) <= MacScrollPhaseProbeSession.MaxTraceUtf8Bytes,
                Is.True);
            Assert.That(LineCount(sink.LastTrace) <= MacScrollPhaseProbeSession.MaxConsoleTraceLines, Is.True);
        }

        private static MacScrollPhaseProbeSession CreateSession(FakeTransport transport,
            FakeSink sink,
            MacScrollPhaseProbeStage stage,
            float duration = 3f,
            IMacScrollActionTraceOwner? actionTraceOwner = null)
        {
            return new MacScrollPhaseProbeSession(transport, sink, stage, duration, 42, actionTraceOwner);
        }

        private static void CommitActionRecord(MacScrollPhaseProbeSession session,
            int frame,
            double contextTime,
            Vector2 delta,
            int deviceId = 1,
            uint byteOffset = 0,
            uint bitOffset = 0,
            uint sizeInBits = 32,
            int phase = 3,
            long? timestampTicks = null)
        {
            Assert.That(session.TryBeginActionRecord(Environment.CurrentManagedThreadId), Is.True);
            session.CommitActionRecord(frame,
                timestampTicks ?? Stopwatch.GetTimestamp(),
                contextTime,
                phase,
                delta,
                deviceId,
                byteOffset,
                bitOffset,
                sizeInBits);
        }

        private static MacScrollPhaseMinimalSample MinimalSample(long sequence,
            long gestureId = 1,
            long windowNumber = 4,
            double timestamp = 1d,
            double scrollingDeltaX = 0d,
            double scrollingDeltaY = -1d,
            int gesturePhase = 3,
            int momentumPhase = 1,
            uint validFields = MinimalResolvedFields)
        {
            return new MacScrollPhaseMinimalSample(validFields,
                sequence,
                gestureId,
                timestamp,
                windowNumber,
                scrollingDeltaX,
                scrollingDeltaY,
                gesturePhase,
                momentumPhase);
        }

        private static FakeMinimalPollResponse MinimalResponse(MacScrollPhaseMinimalSample sample,
            int remaining = 0,
            int monitorResult = 0,
            int invalidReason = 0,
            long apiResult = 0)
        {
            return new FakeMinimalPollResponse(apiResult,
                monitorResult,
                new MacScrollPhaseMinimalPoll(invalidReason,
                    monitorResult == 0,
                    remaining > 0,
                    remaining,
                    sample));
        }

        private static MacScrollPhaseMinimalInvalidDetail MinimalInvalidDetail(int hasDetail = 1,
            int failure = 3,
            int priorTrackerState = 1,
            long priorGestureId = 7,
            long sequence = 41,
            ulong gesturePhaseBits = 4,
            ulong momentumPhaseBits = 1,
            long windowNumber = 99)
        {
            return new MacScrollPhaseMinimalInvalidDetail(hasDetail,
                failure,
                priorTrackerState,
                priorGestureId,
                sequence,
                gesturePhaseBits,
                momentumPhaseBits,
                windowNumber);
        }

        private static MacScrollPhaseNativeSample Sample(long sequence,
            long windowNumber = 4,
            long keyWindowNumber = 4,
            Vector2? delta = null,
            int gesturePhase = 3,
            int momentumPhase = 1)
        {
            var value = delta ?? new Vector2(0f, -1f);
            return new MacScrollPhaseNativeSample(true,
                sequence,
                1,
                sequence,
                windowNumber,
                keyWindowNumber,
                sequence,
                value.x,
                value.y,
                value.x,
                value.y,
                gesturePhase,
                momentumPhase,
                1,
                0,
                false);
        }

        private static int LineCount(string value)
        {
            return value.Split('\n').Length - 1;
        }

        private static int Utf8CrLfBytes(string value)
        {
            var normalized = value.Replace("\r\n", "\n").Replace("\n", "\r\n");
            return Encoding.UTF8.GetByteCount(normalized);
        }

        private static void AssertTerminalOrder(string trace, string detailMarker)
        {
            var detailIndex = trace.IndexOf(detailMarker, System.StringComparison.Ordinal);
            var failIndex = trace.IndexOf("FAIL native-capture-invalid reason=3", System.StringComparison.Ordinal);
            var stopIndex = trace.IndexOf("monitor-stop reason=fail-closed", System.StringComparison.Ordinal);
            Assert.That(detailIndex >= 0 && detailIndex < failIndex && failIndex < stopIndex, Is.True);
        }

        private sealed class FakeActionTraceOwner : IMacScrollActionTraceOwner
        {
            private readonly List<string> calls;

            internal FakeActionTraceOwner(List<string> calls)
            {
                this.calls = calls;
            }

            internal readonly Queue<bool> DetachResults = new Queue<bool>();
            internal int DetachCount { get; private set; }

            public bool TryDetachActionTrace()
            {
                ++DetachCount;
                calls.Add("detach");
                return DetachResults.Count == 0 || DetachResults.Dequeue();
            }
        }

        private sealed class FakeTransport : IMacScrollPhaseMonitorTransport
        {
            internal readonly Queue<MacScrollPhaseNativeSample> Samples = new Queue<MacScrollPhaseNativeSample>();
            internal readonly Queue<FakeMinimalPollResponse> MinimalPolls = new Queue<FakeMinimalPollResponse>();
            internal readonly Queue<FakeMinimalInvalidDetailResponse> MinimalInvalidDetails =
                new Queue<FakeMinimalInvalidDetailResponse>();
            internal readonly Queue<int> StopResults = new Queue<int>();
            internal readonly Queue<long> StopApiResults = new Queue<long>();
            internal readonly List<string> Calls = new List<string>();

            internal int PollResult { get; set; }
            internal int StartResult { get; set; }
            internal long StartLease { get; set; } = 1;
            internal int PollCount { get; private set; }
            internal int MinimalPollCount { get; private set; }
            internal int MinimalInvalidDetailCount { get; private set; }
            internal int StartCount { get; private set; }
            internal int StopCount { get; private set; }
            internal long LastStopLease { get; private set; }
            internal MacScrollPhaseMonitorMode LastStartMode { get; private set; }

            public long Start(MacScrollPhaseMonitorMode mode, out int result, out long leaseId)
            {
                ++StartCount;
                LastStartMode = mode;
                result = StartResult;
                leaseId = StartLease;
                return 0;
            }

            public long Poll(out int result, long leaseId, out MacScrollPhaseNativeSample sample)
            {
                ++PollCount;
                result = PollResult;
                sample = Samples.Count == 0 ? default : Samples.Dequeue();
                return 0;
            }

            public long PollMinimal(out int result, long leaseId, out MacScrollPhaseMinimalPoll poll)
            {
                ++MinimalPollCount;
                Calls.Add("minimal-poll");
                if (MinimalPolls.Count == 0)
                {
                    result = 0;
                    poll = default;
                    return 0;
                }
                var response = MinimalPolls.Dequeue();
                result = response.MonitorResult;
                poll = response.Poll;
                return response.ApiResult;
            }

            public long GetMinimalInvalidDetail(out int result,
                long leaseId,
                out MacScrollPhaseMinimalInvalidDetail detail)
            {
                ++MinimalInvalidDetailCount;
                Calls.Add("minimal-invalid-detail");
                if (MinimalInvalidDetails.Count == 0)
                {
                    result = 0;
                    detail = default;
                    return 0;
                }
                var response = MinimalInvalidDetails.Dequeue();
                result = response.MonitorResult;
                detail = response.Detail;
                return response.ApiResult;
            }

            public long Stop(out int result, long leaseId)
            {
                ++StopCount;
                Calls.Add("stop");
                LastStopLease = leaseId;
                result = StopResults.Count == 0 ? 0 : StopResults.Dequeue();
                return StopApiResults.Count == 0 ? 0 : StopApiResults.Dequeue();
            }
        }


        private readonly struct FakeMinimalPollResponse
        {
            internal FakeMinimalPollResponse(long apiResult,
                int monitorResult,
                MacScrollPhaseMinimalPoll poll)
            {
                ApiResult = apiResult;
                MonitorResult = monitorResult;
                Poll = poll;
            }

            internal long ApiResult { get; }
            internal int MonitorResult { get; }
            internal MacScrollPhaseMinimalPoll Poll { get; }
        }

        private readonly struct FakeMinimalInvalidDetailResponse
        {
            internal FakeMinimalInvalidDetailResponse(long apiResult,
                int monitorResult,
                MacScrollPhaseMinimalInvalidDetail detail)
            {
                ApiResult = apiResult;
                MonitorResult = monitorResult;
                Detail = detail;
            }

            internal long ApiResult { get; }
            internal int MonitorResult { get; }
            internal MacScrollPhaseMinimalInvalidDetail Detail { get; }
        }

        private sealed class FakeSink : IMacScrollPhaseProbeSink
        {
            internal int FlushCount { get; private set; }
            internal bool LastFailed { get; private set; }
            internal string LastTrace { get; private set; } = string.Empty;

            public void Flush(bool failed, string trace)
            {
                ++FlushCount;
                LastFailed = failed;
                LastTrace = trace;
            }
        }
    }
}

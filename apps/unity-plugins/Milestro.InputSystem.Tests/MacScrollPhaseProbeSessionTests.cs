using System.Collections.Generic;
using System.Text;
using Milestro.InputSystem.Service;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.InputSystemTests
{
    public class MacScrollPhaseProbeSessionTests
    {
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
            float duration = 3f)
        {
            return new MacScrollPhaseProbeSession(transport, sink, stage, duration, 42);
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

        private sealed class FakeTransport : IMacScrollPhaseMonitorTransport
        {
            internal readonly Queue<MacScrollPhaseNativeSample> Samples = new Queue<MacScrollPhaseNativeSample>();
            internal readonly Queue<int> StopResults = new Queue<int>();

            internal int PollResult { get; set; }
            internal int StartResult { get; set; }
            internal int PollCount { get; private set; }
            internal int StopCount { get; private set; }
            internal long LastStopLease { get; private set; }
            internal MacScrollPhaseMonitorMode LastStartMode { get; private set; }

            public long Start(MacScrollPhaseMonitorMode mode, out int result, out long leaseId)
            {
                LastStartMode = mode;
                result = StartResult;
                leaseId = 1;
                return 0;
            }

            public long Poll(out int result, long leaseId, out MacScrollPhaseNativeSample sample)
            {
                ++PollCount;
                result = PollResult;
                sample = Samples.Count == 0 ? default : Samples.Dequeue();
                return 0;
            }

            public long Stop(out int result, long leaseId)
            {
                ++StopCount;
                LastStopLease = leaseId;
                result = StopResults.Count == 0 ? 0 : StopResults.Dequeue();
                return 0;
            }
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

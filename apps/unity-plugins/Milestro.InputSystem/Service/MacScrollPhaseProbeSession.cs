using System;
using System.Text;
using UnityEngine;

namespace Milestro.InputSystem.Service
{
    public enum MacScrollPhaseProbeStage
    {
        NativeLifecycle = 0,
        NativePolling = 1,
        InputAction = 2,
        UguiAssociation = 3,
        NativeProperties = 4,
        NativeEventProperties = 5,
        NativeEventScalars = 6,
        NativeLocalPodWrites = 7,
        NativePhasesOnly = 8,
        NativePhasesTimestamp = 9,
        NativePhasesTimestampWindowPod = 10,
        NativePhasesTimestampWindow = 11
    }

    internal enum MacScrollPhaseMonitorMode
    {
        PassThrough = 0,
        CaptureSamples = 1,
        ReadProperties = 2,
        ReadEventProperties = 3,
        ReadEventScalars = 4,
        WriteLocalPod = 5,
        ReadPhasesOnly = 6,
        ReadPhasesTimestamp = 7,
        WritePhasesTimestampWindowPod = 8,
        ReadPhasesTimestampWindow = 9
    }

    internal readonly struct MacScrollPhaseNativeSample
    {
        internal MacScrollPhaseNativeSample(bool hasSample,
            long sequence,
            long gestureId,
            double timestamp,
            long windowNumber,
            long keyWindowNumber,
            long eventNumber,
            double deltaX,
            double deltaY,
            double scrollingDeltaX,
            double scrollingDeltaY,
            int gesturePhase,
            int momentumPhase,
            int precise,
            int directionInvertedFromDevice,
            bool queueOverflowed)
        {
            HasSample = hasSample;
            Sequence = sequence;
            GestureId = gestureId;
            Timestamp = timestamp;
            WindowNumber = windowNumber;
            KeyWindowNumber = keyWindowNumber;
            EventNumber = eventNumber;
            DeltaX = deltaX;
            DeltaY = deltaY;
            ScrollingDeltaX = scrollingDeltaX;
            ScrollingDeltaY = scrollingDeltaY;
            GesturePhase = gesturePhase;
            MomentumPhase = momentumPhase;
            Precise = precise;
            DirectionInvertedFromDevice = directionInvertedFromDevice;
            QueueOverflowed = queueOverflowed;
        }

        internal bool HasSample { get; }
        internal long Sequence { get; }
        internal long GestureId { get; }
        internal double Timestamp { get; }
        internal long WindowNumber { get; }
        internal long KeyWindowNumber { get; }
        internal long EventNumber { get; }
        internal double DeltaX { get; }
        internal double DeltaY { get; }
        internal double ScrollingDeltaX { get; }
        internal double ScrollingDeltaY { get; }
        internal int GesturePhase { get; }
        internal int MomentumPhase { get; }
        internal int Precise { get; }
        internal int DirectionInvertedFromDevice { get; }
        internal bool QueueOverflowed { get; }

        internal NativeScrollPhaseEvidence ToEvidence()
        {
            return new NativeScrollPhaseEvidence(Sequence,
                GestureId,
                Timestamp,
                WindowNumber,
                KeyWindowNumber,
                EventNumber,
                new Vector2((float)ScrollingDeltaX, (float)ScrollingDeltaY),
                GesturePhase,
                MomentumPhase,
                QueueOverflowed);
        }
    }

    internal interface IMacScrollPhaseMonitorTransport
    {
        long Start(MacScrollPhaseMonitorMode mode, out int result, out long leaseId);
        long Poll(out int result, long leaseId, out MacScrollPhaseNativeSample sample);
        long Stop(out int result, long leaseId);
    }

    internal interface IMacScrollPhaseProbeSink
    {
        void Flush(bool failed, string trace);
    }

    internal sealed class MacScrollPhaseProbeSession
    {
        internal const MacScrollPhaseProbeStage DefaultStage = MacScrollPhaseProbeStage.NativeLifecycle;
        internal const int MaxPollsPerFrame = 32;
        internal const int MaxConsoleTraceLines = 96;
        internal const int MaxTraceUtf8Bytes = 8192;

        private const string LogPrefix = "[MILESTRO_SCROLL_PHASE_POC]";
        private const int MaxBufferedTraceLines = MaxConsoleTraceLines - 2;

        private readonly IMacScrollPhaseMonitorTransport transport;
        private readonly IMacScrollPhaseProbeSink sink;
        private readonly MacScrollPhaseProbeStage stage;
        private readonly MacScrollPhaseMonitorMode monitorMode;
        private readonly float captureDurationSeconds;
        private readonly int ownerId;
        private readonly StringBuilder trace = new StringBuilder(MaxTraceUtf8Bytes);
        private readonly ScrollPhasePollBudget pollBudget = new ScrollPhasePollBudget(MaxPollsPerFrame);
        private readonly ScrollPhaseAssociationTracker associationTracker = new ScrollPhaseAssociationTracker();
        private readonly ScrollPhaseLifecycleTracker lifecycleTracker = new ScrollPhaseLifecycleTracker();

        private int traceLines;
        private int traceUtf8Bytes;
        private double captureStartedAt;
        private long lastNativeSequence;
        private long boundGestureId;
        private long monitorLeaseId;
        private bool started;
        private bool captureFailed;
        private bool captureComplete;
        private bool traceFlushed;

        internal MacScrollPhaseProbeSession(IMacScrollPhaseMonitorTransport transport,
            IMacScrollPhaseProbeSink sink,
            MacScrollPhaseProbeStage stage,
            float captureDurationSeconds,
            int ownerId)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
            this.stage = stage;
            monitorMode = stage == MacScrollPhaseProbeStage.NativeLifecycle
                ? MacScrollPhaseMonitorMode.PassThrough
                : stage == MacScrollPhaseProbeStage.NativeProperties
                    ? MacScrollPhaseMonitorMode.ReadProperties
                    : stage == MacScrollPhaseProbeStage.NativeEventProperties
                        ? MacScrollPhaseMonitorMode.ReadEventProperties
                        : stage == MacScrollPhaseProbeStage.NativeEventScalars
                            ? MacScrollPhaseMonitorMode.ReadEventScalars
                            : stage == MacScrollPhaseProbeStage.NativeLocalPodWrites
                                ? MacScrollPhaseMonitorMode.WriteLocalPod
                                : stage == MacScrollPhaseProbeStage.NativePhasesOnly
                                    ? MacScrollPhaseMonitorMode.ReadPhasesOnly
                                    : stage == MacScrollPhaseProbeStage.NativePhasesTimestamp
                                        ? MacScrollPhaseMonitorMode.ReadPhasesTimestamp
                                        : stage == MacScrollPhaseProbeStage.NativePhasesTimestampWindowPod
                                            ? MacScrollPhaseMonitorMode.WritePhasesTimestampWindowPod
                                            : stage == MacScrollPhaseProbeStage.NativePhasesTimestampWindow
                                                ? MacScrollPhaseMonitorMode.ReadPhasesTimestampWindow
                                                : MacScrollPhaseMonitorMode.CaptureSamples;
            this.captureDurationSeconds = Math.Max(0.25f, captureDurationSeconds);
            this.ownerId = ownerId;
        }

        internal bool RequiresInputAction => stage == MacScrollPhaseProbeStage.InputAction ||
                                             stage == MacScrollPhaseProbeStage.UguiAssociation;
        internal bool IsFinished => captureFailed || captureComplete;
        internal bool HasLease => monitorLeaseId != 0;

        private bool PollsNativeSamples => stage == MacScrollPhaseProbeStage.NativePolling || RequiresInputAction;
        private bool RequiresUguiAssociation => stage == MacScrollPhaseProbeStage.UguiAssociation;

        internal void Start(double now)
        {
            if (started)
            {
                return;
            }

            started = true;
            captureStartedAt = now;
            pollBudget.Reset();
            associationTracker.Reset();
            lifecycleTracker.Reset();
            var apiResult = transport.Start(monitorMode, out var monitorResult, out monitorLeaseId);
            AppendTrace($"monitor-start stage={stage} api={apiResult} result={monitorResult} lease={monitorLeaseId}");
            if (apiResult != 0 || monitorResult != 0 || monitorLeaseId == 0)
            {
                FailCapture("monitor-start-failed");
            }
        }

        internal void Update(int frame, double now)
        {
            if (IsFinished)
            {
                return;
            }
            if (now - captureStartedAt >= captureDurationSeconds)
            {
                CompleteCapture("duration-elapsed");
                return;
            }
            if (PollsNativeSamples)
            {
                DrainNativeSamples("update", frame);
            }
            if (!captureFailed && RequiresUguiAssociation)
            {
                associationTracker.AdvanceFrame(frame);
                SynchronizeAssociation();
            }
        }

        internal void LateUpdate(int frame)
        {
            if (IsFinished || !RequiresUguiAssociation)
            {
                return;
            }

            DrainNativeSamples("late-update", frame);
            if (!captureFailed)
            {
                associationTracker.AdvanceFrame(frame);
                SynchronizeAssociation();
            }
        }

        internal void RecordActionAttachment(string name)
        {
            if (!IsFinished && RequiresInputAction)
            {
                AppendTrace($"action-attached name={name}");
            }
        }

        internal void ObserveAction(int frame, double timestamp, Vector2 delta, string controlPath)
        {
            if (IsFinished || !RequiresInputAction)
            {
                return;
            }
            if (RequiresUguiAssociation)
            {
                associationTracker.ObserveAction(frame, timestamp, delta);
            }
            AppendTrace($"action frame={frame} time={timestamp:F6} delta={Format(delta)} " +
                        $"control={controlPath} bound={boundGestureId}");
            SynchronizeAssociation();
        }

        internal void ObserveUgui(int frame, int pointerEventIdentity, Vector2 delta)
        {
            if (IsFinished || !RequiresUguiAssociation)
            {
                return;
            }
            associationTracker.ObserveUgui(frame, pointerEventIdentity, delta);
            AppendTrace($"ugui frame={frame} event={pointerEventIdentity} delta={Format(delta)} " +
                        $"owner={ownerId} bound={boundGestureId}");
            SynchronizeAssociation();
        }

        internal void Disable()
        {
            if (!IsFinished && RequiresUguiAssociation)
            {
                associationTracker.Complete();
                SynchronizeAssociation();
                if (!captureFailed && boundGestureId == 0)
                {
                    FailCapture("association-not-proven");
                }
                if (!captureFailed)
                {
                    ValidateLifecycleCompletion();
                }
            }

            if (IsFinished)
            {
                StopMonitor("component-disabled-retry");
                return;
            }

            captureComplete = true;
            if (!StopMonitor("component-disabled"))
            {
                captureFailed = true;
                AppendTerminalTrace("FAIL monitor-stop-failed");
            }
            FlushTrace();
        }

        private void DrainNativeSamples(string source, int frame)
        {
            if (!HasLease)
            {
                return;
            }

            while (pollBudget.TryConsume(frame))
            {
                var apiResult = transport.Poll(out var monitorResult, monitorLeaseId, out var sample);
                if (apiResult != 0 || monitorResult != 0)
                {
                    FailCapture($"poll-failed api={apiResult} result={monitorResult}");
                    return;
                }
                if (!sample.HasSample)
                {
                    return;
                }
                if (lastNativeSequence != 0 && sample.Sequence != lastNativeSequence + 1)
                {
                    FailCapture($"native-sequence-gap previous={lastNativeSequence} current={sample.Sequence}");
                    return;
                }
                lastNativeSequence = sample.Sequence;
                if (sample.QueueOverflowed)
                {
                    FailCapture("native-queue-overflow");
                    return;
                }

                var lifecycle = ScrollPhaseLifecycleDecision.None;
                if (RequiresUguiAssociation)
                {
                    var evidence = sample.ToEvidence();
                    associationTracker.ObserveNative(frame, evidence);
                    SynchronizeAssociation();
                    if (captureFailed)
                    {
                        return;
                    }
                    if (associationTracker.AcceptsLifecycle(evidence))
                    {
                        lifecycle = lifecycleTracker.Observe(sample.GestureId,
                            sample.GesturePhase,
                            sample.MomentumPhase);
                    }
                }
                AppendTrace($"native source={source} frame={frame} seq={sample.Sequence} " +
                            $"gesture={sample.GestureId} timestamp={sample.Timestamp:F6} " +
                            $"window={sample.WindowNumber} keyWindow={sample.KeyWindowNumber} " +
                            $"event={sample.EventNumber} delta=({sample.DeltaX:F4},{sample.DeltaY:F4}) " +
                            $"scrolling=({sample.ScrollingDeltaX:F4},{sample.ScrollingDeltaY:F4}) " +
                            $"phase={sample.GesturePhase} momentum={sample.MomentumPhase} " +
                            $"precise={sample.Precise} natural={sample.DirectionInvertedFromDevice} " +
                            $"overflow={(sample.QueueOverflowed ? 1 : 0)} owner={ownerId} " +
                            $"bound={boundGestureId} lifecycle={lifecycle}");
                if (captureFailed)
                {
                    return;
                }
            }

            FailCapture($"poll-budget-exhausted frame={frame} budget={MaxPollsPerFrame}");
        }

        private void AppendTrace(string message)
        {
            if (traceFlushed || captureFailed)
            {
                return;
            }
            var line = $"{LogPrefix} {message}{Environment.NewLine}";
            var lineBytes = Encoding.UTF8.GetByteCount(line);
            if (traceLines >= MaxBufferedTraceLines || traceUtf8Bytes + lineBytes > MaxTraceUtf8Bytes)
            {
                FailCapture("managed-trace-capacity-exhausted");
                return;
            }

            trace.Append(line);
            ++traceLines;
            traceUtf8Bytes += lineBytes;
        }

        private void FailCapture(string reason)
        {
            if (captureFailed)
            {
                return;
            }

            captureFailed = true;
            AppendTerminalTrace($"FAIL {reason}");
            StopMonitor("fail-closed");
            FlushTrace();
        }

        private void CompleteCapture(string reason)
        {
            if (IsFinished)
            {
                return;
            }
            if (RequiresUguiAssociation)
            {
                associationTracker.Complete();
                SynchronizeAssociation();
                if (captureFailed)
                {
                    return;
                }
                if (boundGestureId == 0)
                {
                    FailCapture("association-not-proven");
                    return;
                }
                ValidateLifecycleCompletion();
                if (captureFailed)
                {
                    return;
                }
            }

            captureComplete = true;
            if (!StopMonitor(reason))
            {
                captureFailed = true;
                AppendTerminalTrace("FAIL monitor-stop-failed");
            }
            FlushTrace();
        }

        private void ValidateLifecycleCompletion()
        {
            if (lifecycleTracker.HasReturned)
            {
                return;
            }
            FailCapture(lifecycleTracker.IsPendingEnd
                ? "no-non-timeout-no-momentum-signal"
                : "lifecycle-not-complete");
        }

        private bool StopMonitor(string reason)
        {
            if (!HasLease)
            {
                return true;
            }

            var apiResult = transport.Stop(out var monitorResult, monitorLeaseId);
            AppendTerminalTrace($"monitor-stop reason={reason} api={apiResult} result={monitorResult} " +
                                $"lease={monitorLeaseId}");
            if (apiResult != 0 || monitorResult != 0)
            {
                return false;
            }

            monitorLeaseId = 0;
            return true;
        }

        private void AppendTerminalTrace(string message)
        {
            var line = $"{LogPrefix} {message}{Environment.NewLine}";
            var lineBytes = Encoding.UTF8.GetByteCount(line);
            if (lineBytes > MaxTraceUtf8Bytes)
            {
                line = $"{LogPrefix} terminal-trace-too-large{Environment.NewLine}";
                lineBytes = Encoding.UTF8.GetByteCount(line);
            }
            if (traceLines >= MaxConsoleTraceLines || traceUtf8Bytes + lineBytes > MaxTraceUtf8Bytes)
            {
                trace.Clear();
                line = $"{LogPrefix} trace-truncated {message}{Environment.NewLine}";
                if (Encoding.UTF8.GetByteCount(line) > MaxTraceUtf8Bytes)
                {
                    line = $"{LogPrefix} trace-truncated{Environment.NewLine}";
                }
                trace.Append(line);
                traceLines = 1;
                traceUtf8Bytes = Encoding.UTF8.GetByteCount(line);
                return;
            }
            trace.Append(line);
            ++traceLines;
            traceUtf8Bytes += lineBytes;
        }

        private void FlushTrace()
        {
            if (traceFlushed || trace.Length == 0)
            {
                return;
            }

            traceFlushed = true;
            sink.Flush(captureFailed, trace.ToString());
        }

        private void SynchronizeAssociation()
        {
            if (!RequiresUguiAssociation || IsFinished)
            {
                return;
            }
            if (associationTracker.IsInvalid)
            {
                FailCapture($"association-failed reason={associationTracker.FailureReason}");
                return;
            }
            if (!associationTracker.Association.HasValue)
            {
                return;
            }

            var association = associationTracker.Association.Value;
            if (boundGestureId != 0)
            {
                if (boundGestureId != association.GestureId)
                {
                    FailCapture("association-changed-after-bind");
                }
                return;
            }

            boundGestureId = association.GestureId;
            lifecycleTracker.Bind(boundGestureId);
            AppendTrace($"associated gesture={association.GestureId} window={association.WindowNumber} " +
                        $"ugui={association.PointerEventIdentity} " +
                        $"observedClockOffset={association.ObservedTimestampOffset:F6}");
        }

        private static string Format(Vector2 value)
        {
            return $"({value.x:F4},{value.y:F4})";
        }
    }
}

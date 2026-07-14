using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
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
        NativePhasesTimestampWindow = 11,
        NativePhasesTimestampWindowScrollingDelta = 12,
        NativeMinimalQueue = 13,
        NativeMinimalQueueTracker = 14,
        NativeMinimalPolling = 15,
        NativeMinimalInputActionTrace = 16
    }

    internal enum MacScrollPhaseMonitorResult
    {
        Succeeded = 0,
        CaptureInvalid = 7
    }

    internal enum MacScrollPhaseCaptureInvalidReason
    {
        None = 0,
        CapacityExceeded = 1,
        SequenceExhausted = 2,
        InvalidGestureTransition = 3
    }

    [Flags]
    internal enum MacScrollPhaseSampleFields : uint
    {
        Sequence = 1U << 0,
        GestureId = 1U << 1,
        Timestamp = 1U << 2,
        WindowNumber = 1U << 3,
        ScrollingDelta = 1U << 7,
        GesturePhase = 1U << 8,
        MomentumPhase = 1U << 9
    }

    internal readonly struct MacScrollPhaseMinimalSample
    {
        internal MacScrollPhaseMinimalSample(uint validFields,
            long sequence,
            long gestureId,
            double timestamp,
            long windowNumber,
            double scrollingDeltaX,
            double scrollingDeltaY,
            int gesturePhase,
            int momentumPhase)
        {
            ValidFields = validFields;
            Sequence = sequence;
            GestureId = gestureId;
            Timestamp = timestamp;
            WindowNumber = windowNumber;
            ScrollingDeltaX = scrollingDeltaX;
            ScrollingDeltaY = scrollingDeltaY;
            GesturePhase = gesturePhase;
            MomentumPhase = momentumPhase;
        }

        internal uint ValidFields { get; }
        internal long Sequence { get; }
        internal long GestureId { get; }
        internal double Timestamp { get; }
        internal long WindowNumber { get; }
        internal double ScrollingDeltaX { get; }
        internal double ScrollingDeltaY { get; }
        internal int GesturePhase { get; }
        internal int MomentumPhase { get; }

        internal bool IsZero => ValidFields == 0 && Sequence == 0 && GestureId == 0 && Timestamp == 0d &&
                                WindowNumber == 0 && ScrollingDeltaX == 0d && ScrollingDeltaY == 0d &&
                                GesturePhase == 0 && MomentumPhase == 0;
    }

    internal readonly struct MacScrollPhaseMinimalPoll
    {
        internal MacScrollPhaseMinimalPoll(int captureInvalidReason,
            bool hasSample,
            bool hasMore,
            int remaining,
            MacScrollPhaseMinimalSample sample)
        {
            CaptureInvalidReason = captureInvalidReason;
            HasSample = hasSample;
            HasMore = hasMore;
            Remaining = remaining;
            Sample = sample;
        }

        internal int CaptureInvalidReason { get; }
        internal bool HasSample { get; }
        internal bool HasMore { get; }
        internal int Remaining { get; }
        internal MacScrollPhaseMinimalSample Sample { get; }

        internal bool HasSamplePayload => HasSample || HasMore || Remaining != 0 || !Sample.IsZero;
    }

    internal readonly struct MacScrollPhaseMinimalInvalidDetail
    {
        internal MacScrollPhaseMinimalInvalidDetail(int hasDetail,
            int failure,
            int priorTrackerState,
            long priorGestureId,
            long sequence,
            ulong gesturePhaseBits,
            ulong momentumPhaseBits,
            long windowNumber)
        {
            HasDetail = hasDetail;
            Failure = failure;
            PriorTrackerState = priorTrackerState;
            PriorGestureId = priorGestureId;
            Sequence = sequence;
            GesturePhaseBits = gesturePhaseBits;
            MomentumPhaseBits = momentumPhaseBits;
            WindowNumber = windowNumber;
        }

        internal int HasDetail { get; }
        internal int Failure { get; }
        internal int PriorTrackerState { get; }
        internal long PriorGestureId { get; }
        internal long Sequence { get; }
        internal ulong GesturePhaseBits { get; }
        internal ulong MomentumPhaseBits { get; }
        internal long WindowNumber { get; }

        internal bool HasZeroPayload => Failure == 0 && PriorTrackerState == 0 && PriorGestureId == 0 &&
                                        Sequence == 0 && GesturePhaseBits == 0 && MomentumPhaseBits == 0 &&
                                        WindowNumber == 0;
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
        ReadPhasesTimestampWindow = 9,
        ReadPhasesTimestampWindowScrollingDelta = 10,
        QueueMinimalSamples = 11,
        QueueMinimalTrackedSamples = 12
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
        long PollMinimal(out int result, long leaseId, out MacScrollPhaseMinimalPoll poll);
        long GetMinimalInvalidDetail(out int result,
            long leaseId,
            out MacScrollPhaseMinimalInvalidDetail detail);
        long Stop(out int result, long leaseId);
    }

    internal interface IMacScrollPhaseProbeSink
    {
        void Flush(bool failed, string trace);
    }

    internal interface IMacScrollActionTraceOwner
    {
        bool TryDetachActionTrace();
    }

    internal enum MacScrollActionTraceFault
    {
        None = 0,
        RecordOverflow = 1,
        WrongThread = 2,
        CallbackReadFailed = 3,
        OwnerChanged = 4,
        AttachmentFailed = 5
    }

    internal readonly struct MacScrollActionRecord
    {
        internal MacScrollActionRecord(int sequence,
            int frame,
            long elapsedTicks,
            double contextTime,
            int phase,
            float deltaX,
            float deltaY,
            int deviceId,
            uint byteOffset,
            uint bitOffset,
            uint sizeInBits)
        {
            Sequence = sequence;
            Frame = frame;
            ElapsedTicks = elapsedTicks;
            ContextTime = contextTime;
            Phase = phase;
            DeltaX = deltaX;
            DeltaY = deltaY;
            DeviceId = deviceId;
            ByteOffset = byteOffset;
            BitOffset = bitOffset;
            SizeInBits = sizeInBits;
        }

        internal int Sequence { get; }
        internal int Frame { get; }
        internal long ElapsedTicks { get; }
        internal double ContextTime { get; }
        internal int Phase { get; }
        internal float DeltaX { get; }
        internal float DeltaY { get; }
        internal int DeviceId { get; }
        internal uint ByteOffset { get; }
        internal uint BitOffset { get; }
        internal uint SizeInBits { get; }
    }

    internal sealed class MacScrollPhaseProbeSession
    {
        internal const MacScrollPhaseProbeStage DefaultStage = MacScrollPhaseProbeStage.NativeLifecycle;
        internal const int MaxPollsPerFrame = 32;
        internal const int MaxConsoleTraceLines = 96;
        internal const int MaxTraceUtf8Bytes = 8192;
        internal const int MaxActionRecords = 10;
        internal const int MaxActionRecordTraceUtf8Bytes = 320;

        private const string LogPrefix = "[MILESTRO_SCROLL_PHASE_POC]";
        private const int PerformedActionPhase = 3;
        private const int MaxBufferedTraceLines = MaxConsoleTraceLines - 2;
        private const int MaxMinimalBufferedTraceLines = MaxConsoleTraceLines - 3;
        // Worst-case detail-contract + reason3 FAIL + full-width Stop is 499 UTF-8 bytes with CRLF.
        private const int MaxMinimalTerminalTraceUtf8Bytes = 1024;
        private const int MaxMinimalBufferedTraceUtf8Bytes = MaxTraceUtf8Bytes - MaxMinimalTerminalTraceUtf8Bytes;
        internal const int MaxActionTerminalTraceLines = 5;
        internal const int MaxActionTerminalTraceUtf8Bytes = 2048;
        internal const int MaxActionGeneralTraceLines =
            MaxConsoleTraceLines - MaxActionTerminalTraceLines - MaxActionRecords;
        internal const int MaxActionGeneralTraceUtf8Bytes =
            MaxTraceUtf8Bytes - MaxActionTerminalTraceUtf8Bytes -
            MaxActionRecords * MaxActionRecordTraceUtf8Bytes;
        private const int MaxActionBufferedTraceLines = MaxConsoleTraceLines - MaxActionTerminalTraceLines;
        private const int MaxActionBufferedTraceUtf8Bytes = MaxTraceUtf8Bytes - MaxActionTerminalTraceUtf8Bytes;
        private const uint MinimalBaseSampleFields =
            (uint)(MacScrollPhaseSampleFields.Sequence |
                   MacScrollPhaseSampleFields.Timestamp |
                   MacScrollPhaseSampleFields.WindowNumber |
                   MacScrollPhaseSampleFields.ScrollingDelta |
                   MacScrollPhaseSampleFields.GesturePhase |
                   MacScrollPhaseSampleFields.MomentumPhase);
        private const uint MinimalResolvedSampleFields =
            MinimalBaseSampleFields | (uint)MacScrollPhaseSampleFields.GestureId;

        private readonly IMacScrollPhaseMonitorTransport transport;
        private readonly IMacScrollPhaseProbeSink sink;
        private readonly IMacScrollActionTraceOwner? actionTraceOwner;
        private readonly MacScrollPhaseProbeStage stage;
        private readonly MacScrollPhaseMonitorMode monitorMode;
        private readonly float captureDurationSeconds;
        private readonly int ownerId;
        private readonly StringBuilder trace = new StringBuilder(MaxTraceUtf8Bytes);
        private readonly ScrollPhasePollBudget pollBudget = new ScrollPhasePollBudget(MaxPollsPerFrame);
        private readonly ScrollPhaseAssociationTracker associationTracker = new ScrollPhaseAssociationTracker();
        private readonly ScrollPhaseLifecycleTracker lifecycleTracker = new ScrollPhaseLifecycleTracker();
        private readonly MacScrollActionRecord[]? actionRecords;

        private int traceLines;
        private int traceUtf8Bytes;
        private int actionGeneralTraceLines;
        private int actionGeneralTraceUtf8Bytes;
        private int actionRecordTraceLines;
        private int actionRecordTraceUtf8Bytes;
        private int actionTerminalTraceLines;
        private int actionTerminalTraceUtf8Bytes;
        private double captureStartedAt;
        private long lastNativeSequence;
        private long lastMinimalSequence;
        private long highestResolvedGestureId;
        private long resolvedGestureWindow;
        private long boundGestureId;
        private long monitorLeaseId;
        private bool started;
        private bool captureFailed;
        private bool captureComplete;
        private bool traceFlushed;
        private bool hasValidatedCleanBegan;
        private int actionCaptureGate;
        private int actionCallbackReserved;
        private int actionFault;
        private int actionRecordCount;
        private int drainedActionRecordCount;
        private int mainThreadId;
        private long actionTraceStartedAtTicks;
        private int lastActionFrame;
        private long lastActionElapsedTicks;
        private double lastActionContextTime;
        private bool hasActionRecord;
        private bool hasNonZeroActionDelta;
        private bool hasPendingMinimalInvalidDetail;
        private bool actionTraceDetached;
        private bool actionDetachFailureReported;
        private bool actionStopFailureReported;

        internal MacScrollPhaseProbeSession(IMacScrollPhaseMonitorTransport transport,
            IMacScrollPhaseProbeSink sink,
            MacScrollPhaseProbeStage stage,
            float captureDurationSeconds,
            int ownerId,
            IMacScrollActionTraceOwner? actionTraceOwner = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
            this.actionTraceOwner = actionTraceOwner;
            this.stage = stage;
            actionRecords = stage == MacScrollPhaseProbeStage.NativeMinimalInputActionTrace
                ? new MacScrollActionRecord[MaxActionRecords]
                : null;
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
                                                : stage ==
                                                  MacScrollPhaseProbeStage.NativePhasesTimestampWindowScrollingDelta
                                                    ? MacScrollPhaseMonitorMode.ReadPhasesTimestampWindowScrollingDelta
                                                    : stage == MacScrollPhaseProbeStage.NativeMinimalQueue
                                                        ? MacScrollPhaseMonitorMode.QueueMinimalSamples
                                                        : stage == MacScrollPhaseProbeStage.NativeMinimalQueueTracker ||
                                                          stage == MacScrollPhaseProbeStage.NativeMinimalPolling ||
                                                          stage == MacScrollPhaseProbeStage.NativeMinimalInputActionTrace
                                                            ? MacScrollPhaseMonitorMode.QueueMinimalTrackedSamples
                                                            : MacScrollPhaseMonitorMode.CaptureSamples;
            this.captureDurationSeconds = Math.Max(0.25f, captureDurationSeconds);
            this.ownerId = ownerId;
        }

        internal bool RequiresInputAction => stage == MacScrollPhaseProbeStage.InputAction ||
                                             stage == MacScrollPhaseProbeStage.UguiAssociation;
        internal bool TracesInputActionCandidates =>
            stage == MacScrollPhaseProbeStage.NativeMinimalInputActionTrace;
        internal bool IsFinished => captureFailed || captureComplete;
        internal bool HasLease => monitorLeaseId != 0;

        private bool PollsNativeSamples => stage == MacScrollPhaseProbeStage.NativePolling || RequiresInputAction;
        private bool PollsMinimalSamples => stage == MacScrollPhaseProbeStage.NativeMinimalPolling ||
                                            TracesInputActionCandidates;
        private bool CanReadMinimalInvalidDetail => PollsMinimalSamples &&
                                                    monitorMode == MacScrollPhaseMonitorMode.QueueMinimalTrackedSamples;
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
            lastMinimalSequence = 0;
            highestResolvedGestureId = 0;
            resolvedGestureWindow = 0;
            hasValidatedCleanBegan = false;
            mainThreadId = Environment.CurrentManagedThreadId;
            actionTraceStartedAtTicks = Stopwatch.GetTimestamp();
            actionCaptureGate = 0;
            actionCallbackReserved = 0;
            actionFault = 0;
            actionRecordCount = 0;
            drainedActionRecordCount = 0;
            lastActionFrame = 0;
            lastActionElapsedTicks = 0;
            lastActionContextTime = 0d;
            hasActionRecord = false;
            hasNonZeroActionDelta = false;
            hasPendingMinimalInvalidDetail = false;
            actionTraceDetached = !TracesInputActionCandidates;
            actionDetachFailureReported = false;
            actionStopFailureReported = false;
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
                if (PollsMinimalSamples)
                {
                    DrainMinimalSamples("final", frame);
                    if (captureFailed)
                    {
                        return;
                    }
                    if (!hasValidatedCleanBegan)
                    {
                        FailCapture("no-resolved-minimal-gesture");
                        return;
                    }
                }
                CompleteCapture("duration-elapsed");
                return;
            }
            if (TracesInputActionCandidates && !DrainAndValidateActionRecords())
            {
                return;
            }
            if (PollsMinimalSamples)
            {
                DrainMinimalSamples("update", frame);
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
            if (IsFinished)
            {
                return;
            }

            if (TracesInputActionCandidates && !DrainAndValidateActionRecords())
            {
                return;
            }

            if (PollsMinimalSamples)
            {
                DrainMinimalSamples("late-update", frame);
                return;
            }
            if (!RequiresUguiAssociation)
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

        internal void RecordActionTraceAttachment()
        {
            if (IsFinished || !TracesInputActionCandidates)
            {
                return;
            }
            if (actionTraceOwner == null)
            {
                ReportActionTraceFault(MacScrollActionTraceFault.AttachmentFailed);
                return;
            }

            AppendTrace("action-trace-attached callback=performed");
            if (!captureFailed)
            {
                Volatile.Write(ref actionCaptureGate, 1);
            }
        }

        internal bool TryBeginActionRecord(int callbackThreadId)
        {
            if (!TracesInputActionCandidates || Volatile.Read(ref actionCaptureGate) == 0)
            {
                return false;
            }
            if (callbackThreadId != mainThreadId)
            {
                LatchActionTraceFault(MacScrollActionTraceFault.WrongThread);
                CloseActionTraceGate();
                return false;
            }
            if (Interlocked.CompareExchange(ref actionCallbackReserved, 1, 0) != 0)
            {
                LatchActionTraceFault(MacScrollActionTraceFault.CallbackReadFailed);
                CloseActionTraceGate();
                return false;
            }
            if (Volatile.Read(ref actionRecordCount) >= MaxActionRecords)
            {
                Volatile.Write(ref actionCallbackReserved, 0);
                LatchActionTraceFault(MacScrollActionTraceFault.RecordOverflow);
                CloseActionTraceGate();
                return false;
            }
            return true;
        }

        internal void CommitActionRecord(int frame,
            long timestampTicks,
            double contextTime,
            int phase,
            Vector2 delta,
            int deviceId,
            uint byteOffset,
            uint bitOffset,
            uint sizeInBits)
        {
            if (!TracesInputActionCandidates || Volatile.Read(ref actionCallbackReserved) == 0)
            {
                return;
            }

            var index = Volatile.Read(ref actionRecordCount);
            actionRecords![index] = new MacScrollActionRecord(index + 1,
                frame,
                timestampTicks - actionTraceStartedAtTicks,
                contextTime,
                phase,
                delta.x,
                delta.y,
                deviceId,
                byteOffset,
                bitOffset,
                sizeInBits);
            Volatile.Write(ref actionRecordCount, index + 1);
            Volatile.Write(ref actionCallbackReserved, 0);
        }

        internal void CancelActionRecord()
        {
            if (!TracesInputActionCandidates)
            {
                return;
            }
            LatchActionTraceFault(MacScrollActionTraceFault.CallbackReadFailed);
            Volatile.Write(ref actionCallbackReserved, 0);
            CloseActionTraceGate();
        }

        internal void ReportActionTraceFault(MacScrollActionTraceFault fault)
        {
            if (IsFinished || !TracesInputActionCandidates)
            {
                return;
            }
            LatchActionTraceFault(fault);
            FailCapture(ActionTraceFaultReason(fault));
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
                if (!TracesInputActionCandidates || !HasLease)
                {
                    FlushTrace();
                }
                return;
            }

            if (TracesInputActionCandidates)
            {
                CloseActionTraceGate();
                if (!DrainAndValidateActionRecords())
                {
                    return;
                }
            }

            captureComplete = true;
            if (!StopMonitor("component-disabled"))
            {
                captureFailed = true;
                if (!actionDetachFailureReported)
                {
                    AppendTerminalTrace("FAIL monitor-stop-failed");
                }
            }
            if (!TracesInputActionCandidates || !HasLease)
            {
                FlushTrace();
            }
        }

        private bool DrainAndValidateActionRecords()
        {
            if (TryDrainActionRecords(out var failureReason))
            {
                return true;
            }

            FailCapture(failureReason, true);
            return false;
        }

        private bool TryDrainActionRecords(out string failureReason)
        {
            failureReason = string.Empty;
            var publishedCount = Volatile.Read(ref actionRecordCount);
            while (drainedActionRecordCount < publishedCount)
            {
                var record = actionRecords![drainedActionRecordCount];
                var expectedSequence = drainedActionRecordCount + 1;
                var line = FormatActionRecord(record, Stopwatch.Frequency);
                if (!TryAppendActionRecordTrace(line))
                {
                    failureReason = "managed-trace-capacity-exhausted";
                    return false;
                }
                ++drainedActionRecordCount;

                if (record.Sequence != expectedSequence ||
                    record.Frame < 0 ||
                    record.ElapsedTicks < 0 ||
                    !IsFinite(record.ContextTime) ||
                    record.ContextTime < 0d ||
                    !IsFinite(record.DeltaX) ||
                    !IsFinite(record.DeltaY) ||
                    record.Phase != PerformedActionPhase ||
                    record.DeviceId <= 0 ||
                    record.ByteOffset == uint.MaxValue ||
                    record.BitOffset >= 8 ||
                    record.SizeInBits == 0)
                {
                    failureReason = "action-record-contract-invalid";
                    return false;
                }
                if (hasActionRecord &&
                    (record.Frame < lastActionFrame ||
                     record.ElapsedTicks < lastActionElapsedTicks ||
                     record.ContextTime < lastActionContextTime))
                {
                    failureReason = "action-record-order-invalid";
                    return false;
                }
                lastActionFrame = record.Frame;
                lastActionElapsedTicks = record.ElapsedTicks;
                lastActionContextTime = record.ContextTime;
                hasActionRecord = true;
                if (record.DeltaX != 0f || record.DeltaY != 0f)
                {
                    hasNonZeroActionDelta = true;
                }
            }

            var fault = (MacScrollActionTraceFault)Volatile.Read(ref actionFault);
            if (fault == MacScrollActionTraceFault.None)
            {
                return true;
            }

            failureReason = ActionTraceFaultReason(fault);
            return false;
        }

        internal static string FormatActionRecord(MacScrollActionRecord record, long frequency)
        {
            var elapsedSeconds = record.ElapsedTicks / (double)frequency;
            return string.Format(CultureInfo.InvariantCulture,
                    "action seq={0} frame={1} elapsed={2:G17} ticks={3} frequency={4} " +
                    "contextTime={5:G17} phase={6} delta=({7:G9},{8:G9}) device={9} " +
                    "controlByte={10:X8} controlBit={11} controlSize={12:X8}",
                    record.Sequence,
                    record.Frame,
                    elapsedSeconds,
                    record.ElapsedTicks,
                    frequency,
                    record.ContextTime,
                    record.Phase,
                    record.DeltaX,
                    record.DeltaY,
                    record.DeviceId,
                    record.ByteOffset,
                    record.BitOffset,
                    record.SizeInBits);
        }

        private void LatchActionTraceFault(MacScrollActionTraceFault fault)
        {
            Interlocked.CompareExchange(ref actionFault, (int)fault, (int)MacScrollActionTraceFault.None);
        }

        private void CloseActionTraceGate()
        {
            Interlocked.Exchange(ref actionCaptureGate, 0);
        }

        private static string ActionTraceFaultReason(MacScrollActionTraceFault fault)
        {
            return fault switch
            {
                MacScrollActionTraceFault.RecordOverflow => "action-record-overflow",
                MacScrollActionTraceFault.WrongThread => "action-callback-wrong-thread",
                MacScrollActionTraceFault.CallbackReadFailed => "action-callback-read-failed",
                MacScrollActionTraceFault.OwnerChanged => "action-owner-changed",
                MacScrollActionTraceFault.AttachmentFailed => "action-attach-failed",
                _ => "action-fault-invalid"
            };
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

        private void DrainMinimalSamples(string source, int frame)
        {
            if (!HasLease)
            {
                return;
            }

            while (pollBudget.TryConsume(frame))
            {
                var apiResult = transport.PollMinimal(out var monitorResult, monitorLeaseId, out var poll);
                if (apiResult != 0)
                {
                    FailCapture($"minimal-poll-api-failed api={apiResult}");
                    return;
                }
                if (!ValidateMinimalPollResult(monitorResult, poll))
                {
                    return;
                }
                if (!poll.HasSample)
                {
                    if (poll.HasSamplePayload)
                    {
                        FailCapture("minimal-poll-empty-payload");
                    }
                    return;
                }
                if (poll.Remaining < 0 ||
                    poll.Remaining >= 256 ||
                    poll.HasMore != (poll.Remaining > 0))
                {
                    FailCapture("minimal-poll-remaining-contract");
                    return;
                }
                if (!ValidateMinimalSample(poll.Sample, source, frame, poll.Remaining))
                {
                    return;
                }
                if (!poll.HasMore)
                {
                    return;
                }
                if (pollBudget.PollsThisFrame == MaxPollsPerFrame)
                {
                    FailCapture($"minimal-poll-budget-exhausted frame={frame} remaining={poll.Remaining}");
                    return;
                }
            }
        }

        private bool ValidateMinimalPollResult(int monitorResult, MacScrollPhaseMinimalPoll poll)
        {
            if (monitorResult == (int)MacScrollPhaseMonitorResult.Succeeded)
            {
                if (poll.CaptureInvalidReason != (int)MacScrollPhaseCaptureInvalidReason.None)
                {
                    FailCapture("minimal-poll-success-with-invalid-reason");
                    return false;
                }
                return true;
            }

            if (monitorResult == (int)MacScrollPhaseMonitorResult.CaptureInvalid)
            {
                var knownReason = poll.CaptureInvalidReason >=
                                  (int)MacScrollPhaseCaptureInvalidReason.CapacityExceeded &&
                                  poll.CaptureInvalidReason <=
                                  (int)MacScrollPhaseCaptureInvalidReason.InvalidGestureTransition;
                if (!knownReason || poll.HasSamplePayload)
                {
                    FailCapture("minimal-poll-invalid-result-contract");
                    return false;
                }
                if (poll.CaptureInvalidReason ==
                    (int)MacScrollPhaseCaptureInvalidReason.InvalidGestureTransition)
                {
                    if (TracesInputActionCandidates)
                    {
                        hasPendingMinimalInvalidDetail = true;
                    }
                    else
                    {
                        AppendMinimalInvalidDetailTerminal();
                    }
                }
                FailCapture($"native-capture-invalid reason={poll.CaptureInvalidReason}");
                return false;
            }

            if (poll.CaptureInvalidReason != (int)MacScrollPhaseCaptureInvalidReason.None || poll.HasSamplePayload)
            {
                FailCapture("minimal-poll-error-result-contract");
                return false;
            }
            FailCapture($"minimal-poll-failed result={monitorResult}");
            return false;
        }

        private bool ValidateMinimalSample(MacScrollPhaseMinimalSample sample,
            string source,
            int frame,
            int remaining)
        {
            var noGesture = IsNoGesturePhasePair(sample.GesturePhase, sample.MomentumPhase);
            var resolved = IsResolvedPhasePair(sample.GesturePhase, sample.MomentumPhase);
            if (!noGesture && !resolved)
            {
                FailCapture("minimal-phase-combination-invalid");
                return false;
            }

            var expectedFields = resolved ? MinimalResolvedSampleFields : MinimalBaseSampleFields;
            if (sample.ValidFields != expectedFields)
            {
                FailCapture($"minimal-valid-fields expected={expectedFields:X} actual={sample.ValidFields:X}");
                return false;
            }

            var expectedSequence = lastMinimalSequence == 0 ? 1 : lastMinimalSequence + 1;
            if (sample.Sequence != expectedSequence)
            {
                FailCapture($"minimal-sequence expected={expectedSequence} actual={sample.Sequence}");
                return false;
            }
            if (!IsFinite(sample.Timestamp) ||
                !IsFinite(sample.ScrollingDeltaX) ||
                !IsFinite(sample.ScrollingDeltaY))
            {
                FailCapture("minimal-non-finite-value");
                return false;
            }
            if (noGesture && sample.GestureId != 0)
            {
                FailCapture("minimal-no-gesture-id-present");
                return false;
            }

            var nextHighestGestureId = highestResolvedGestureId;
            var nextGestureWindow = resolvedGestureWindow;
            var cleanBegan = sample.GesturePhase == 2 && sample.MomentumPhase == 1;
            if (resolved)
            {
                if (sample.GestureId <= 0)
                {
                    FailCapture("minimal-resolved-gesture-id-invalid");
                    return false;
                }
                if (highestResolvedGestureId == 0)
                {
                    if (sample.GestureId != 1 || !cleanBegan)
                    {
                        FailCapture("minimal-first-gesture-not-clean-began");
                        return false;
                    }
                    nextHighestGestureId = sample.GestureId;
                    nextGestureWindow = sample.WindowNumber;
                }
                else if (sample.GestureId == highestResolvedGestureId)
                {
                    if (sample.WindowNumber != resolvedGestureWindow)
                    {
                        FailCapture("minimal-gesture-window-changed");
                        return false;
                    }
                }
                else
                {
                    if (sample.GestureId != highestResolvedGestureId + 1 || !cleanBegan)
                    {
                        FailCapture("minimal-gesture-id-trajectory");
                        return false;
                    }
                    nextHighestGestureId = sample.GestureId;
                    nextGestureWindow = sample.WindowNumber;
                }
            }

            var gesture = resolved ? sample.GestureId.ToString() : "unavailable";
            AppendTrace($"minimal source={source} frame={frame} seq={sample.Sequence} " +
                        $"valid=0x{sample.ValidFields:X} gesture={gesture} timestamp={sample.Timestamp:F6} " +
                        $"window={sample.WindowNumber} scrolling=({sample.ScrollingDeltaX:F4}," +
                        $"{sample.ScrollingDeltaY:F4}) phase={sample.GesturePhase} " +
                        $"momentum={sample.MomentumPhase} remaining={remaining}");
            if (captureFailed)
            {
                return false;
            }

            lastMinimalSequence = sample.Sequence;
            if (resolved)
            {
                highestResolvedGestureId = nextHighestGestureId;
                resolvedGestureWindow = nextGestureWindow;
                if (cleanBegan)
                {
                    hasValidatedCleanBegan = true;
                }
            }
            return true;
        }

        private void AppendMinimalInvalidDetailTerminal()
        {
            if (!CanReadMinimalInvalidDetail)
            {
                AppendTerminalTrace("invalid-detail-contract-failed capability=0");
                return;
            }

            var apiResult = transport.GetMinimalInvalidDetail(out var monitorResult,
                monitorLeaseId,
                out var detail);
            if (apiResult != 0)
            {
                AppendTerminalTrace($"invalid-detail-query-failed api={apiResult} result={monitorResult} " +
                                    $"has={detail.HasDetail}");
                return;
            }

            if (!HasValidMinimalInvalidDetailContract(monitorResult, detail) ||
                detail.HasDetail == 0)
            {
                AppendTerminalTrace($"invalid-detail-contract-failed api={apiResult} result={monitorResult} " +
                                    $"has={detail.HasDetail} failure={detail.Failure} " +
                                    $"state={detail.PriorTrackerState} prior={detail.PriorGestureId} " +
                                    $"seq={detail.Sequence} gestureBits=0x{detail.GesturePhaseBits:X} " +
                                    $"momentumBits=0x{detail.MomentumPhaseBits:X} window={detail.WindowNumber}");
                return;
            }

            AppendTerminalTrace($"invalid-detail failure={detail.Failure} state={detail.PriorTrackerState} " +
                                $"prior={detail.PriorGestureId} seq={detail.Sequence} " +
                                $"gestureBits=0x{detail.GesturePhaseBits:X} " +
                                $"momentumBits=0x{detail.MomentumPhaseBits:X} window={detail.WindowNumber}");
        }

        private static bool HasValidMinimalInvalidDetailContract(int monitorResult,
            MacScrollPhaseMinimalInvalidDetail detail)
        {
            if (detail.HasDetail != 0 && detail.HasDetail != 1)
            {
                return false;
            }
            if (monitorResult != (int)MacScrollPhaseMonitorResult.Succeeded)
            {
                return detail.HasDetail == 0 && detail.HasZeroPayload;
            }
            if (detail.HasDetail == 0)
            {
                return detail.HasZeroPayload;
            }

            var validFailure = detail.Failure >= 1 && detail.Failure <= 4;
            var validState = detail.PriorTrackerState >= 0 && detail.PriorTrackerState <= 3;
            var validPriorId = validState && (detail.PriorTrackerState == 0
                ? detail.PriorGestureId == 0
                : detail.PriorGestureId > 0);
            return validFailure && validState && validPriorId && detail.Sequence > 0;
        }

        private static bool IsNoGesturePhasePair(int gesturePhase, int momentumPhase)
        {
            return gesturePhase == 1 && momentumPhase == 1 ||
                   gesturePhase == 7 && momentumPhase == 1 ||
                   gesturePhase == 1 && momentumPhase == 7;
        }

        private static bool IsResolvedPhasePair(int gesturePhase, int momentumPhase)
        {
            var gestureLifecycle = gesturePhase >= 2 && gesturePhase <= 6 && momentumPhase == 1;
            var sameEventMomentum = gesturePhase == 5 && momentumPhase == 2;
            var momentumLifecycle = gesturePhase == 1 && momentumPhase >= 2 && momentumPhase <= 6;
            return gestureLifecycle || sameEventMomentum || momentumLifecycle;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private void AppendTrace(string message)
        {
            if (TryAppendBufferedTrace(message))
            {
                return;
            }
            if (!traceFlushed && !captureFailed)
            {
                FailCapture("managed-trace-capacity-exhausted");
            }
        }

        private bool TryAppendBufferedTrace(string message)
        {
            if (traceFlushed || captureFailed)
            {
                return false;
            }
            var line = $"{LogPrefix} {message}{Environment.NewLine}";
            var lineBytes = Encoding.UTF8.GetByteCount(line);
            if (TracesInputActionCandidates)
            {
                if (actionGeneralTraceLines >= MaxActionGeneralTraceLines ||
                    actionGeneralTraceUtf8Bytes + lineBytes > MaxActionGeneralTraceUtf8Bytes)
                {
                    return false;
                }
            }
            else
            {
                var maxBufferedLines = PollsMinimalSamples ? MaxMinimalBufferedTraceLines : MaxBufferedTraceLines;
                var maxBufferedBytes = PollsMinimalSamples ? MaxMinimalBufferedTraceUtf8Bytes : MaxTraceUtf8Bytes;
                if (traceLines >= maxBufferedLines || traceUtf8Bytes + lineBytes > maxBufferedBytes)
                {
                    return false;
                }
            }

            trace.Append(line);
            ++traceLines;
            traceUtf8Bytes += lineBytes;
            if (TracesInputActionCandidates)
            {
                ++actionGeneralTraceLines;
                actionGeneralTraceUtf8Bytes += lineBytes;
            }
            return true;
        }

        private bool TryAppendActionRecordTrace(string message)
        {
            if (traceFlushed || captureFailed)
            {
                return false;
            }
            var line = $"{LogPrefix} {message}{Environment.NewLine}";
            var lineBytes = Encoding.UTF8.GetByteCount(line);
            if (lineBytes > MaxActionRecordTraceUtf8Bytes ||
                actionRecordTraceLines >= MaxActionRecords ||
                actionRecordTraceUtf8Bytes + lineBytes >
                MaxActionRecords * MaxActionRecordTraceUtf8Bytes ||
                traceLines >= MaxActionBufferedTraceLines ||
                traceUtf8Bytes + lineBytes > MaxActionBufferedTraceUtf8Bytes)
            {
                return false;
            }

            trace.Append(line);
            ++traceLines;
            traceUtf8Bytes += lineBytes;
            ++actionRecordTraceLines;
            actionRecordTraceUtf8Bytes += lineBytes;
            return true;
        }

        private void FailCapture(string reason, bool actionRecordsHandled = false)
        {
            if (captureFailed)
            {
                return;
            }

            string? secondaryReason = null;
            if (TracesInputActionCandidates)
            {
                CloseActionTraceGate();
                if (!actionRecordsHandled && !TryDrainActionRecords(out var actionFailureReason))
                {
                    if (hasPendingMinimalInvalidDetail &&
                        reason == "native-capture-invalid reason=3")
                    {
                        secondaryReason = actionFailureReason;
                    }
                    else
                    {
                        reason = actionFailureReason;
                    }
                }
            }

            captureFailed = true;
            if (hasPendingMinimalInvalidDetail)
            {
                hasPendingMinimalInvalidDetail = false;
                AppendMinimalInvalidDetailTerminal();
            }
            AppendTerminalTrace(secondaryReason == null
                ? $"FAIL {reason}"
                : $"FAIL {reason} secondary={secondaryReason}");
            StopMonitor("fail-closed");
            if (!TracesInputActionCandidates || !HasLease)
            {
                FlushTrace();
            }
        }

        private void CompleteCapture(string reason)
        {
            if (IsFinished)
            {
                return;
            }
            if (TracesInputActionCandidates)
            {
                CloseActionTraceGate();
                if (!DrainAndValidateActionRecords())
                {
                    return;
                }
                if (!hasActionRecord)
                {
                    FailCapture("no-action-record", true);
                    return;
                }
                if (!hasNonZeroActionDelta)
                {
                    FailCapture("no-nonzero-action-record", true);
                    return;
                }
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
                if (!actionDetachFailureReported)
                {
                    AppendTerminalTrace("FAIL monitor-stop-failed");
                }
            }
            if (!TracesInputActionCandidates || !HasLease)
            {
                FlushTrace();
            }
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

            if (TracesInputActionCandidates && !actionTraceDetached)
            {
                var detached = false;
                try
                {
                    detached = actionTraceOwner?.TryDetachActionTrace() == true;
                }
                catch (Exception)
                {
                    detached = false;
                }
                if (!detached)
                {
                    if (!actionDetachFailureReported)
                    {
                        actionDetachFailureReported = true;
                        AppendTerminalTrace("FAIL action-detach-failed");
                    }
                    return false;
                }
                actionTraceDetached = true;
            }

            var apiResult = transport.Stop(out var monitorResult, monitorLeaseId);
            var stopSucceeded = apiResult == 0 && monitorResult == 0;
            if (!TracesInputActionCandidates || stopSucceeded || !actionStopFailureReported)
            {
                AppendTerminalTrace($"monitor-stop reason={reason} api={apiResult} result={monitorResult} " +
                                    $"lease={monitorLeaseId}");
            }
            if (apiResult != 0 || monitorResult != 0)
            {
                actionStopFailureReported = true;
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
            var actionTerminalOverflow = TracesInputActionCandidates &&
                                         (actionTerminalTraceLines >= MaxActionTerminalTraceLines ||
                                          actionTerminalTraceUtf8Bytes + lineBytes >
                                          MaxActionTerminalTraceUtf8Bytes);
            if (actionTerminalOverflow ||
                traceLines >= MaxConsoleTraceLines ||
                traceUtf8Bytes + lineBytes > MaxTraceUtf8Bytes)
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
            if (TracesInputActionCandidates)
            {
                ++actionTerminalTraceLines;
                actionTerminalTraceUtf8Bytes += lineBytes;
            }
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

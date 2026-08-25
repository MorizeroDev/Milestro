using System;
using System.Collections.Generic;
using System.Threading;
using Milestro.Configuration;

namespace Milestro.Skia
{
    internal readonly struct NativeRenderDiagnosticsSnapshot
    {
        internal const uint ExpectedAbiVersion = 1;
        internal const uint ExpectedStructSize = 64;

        internal NativeRenderDiagnosticsSnapshot(uint abiVersion,
            uint structSize,
            ulong acceptedSubmissionCount,
            ulong rejectedSubmissionCount,
            int hasLastAcceptedSubmission,
            int lastAcceptedGraphicsBackend,
            int lastAcceptedRasterWidth,
            int lastAcceptedRasterHeight,
            float lastAcceptedEffectiveScale,
            ulong lastAcceptedDeviceEpoch,
            ulong currentDeviceEpoch)
        {
            AbiVersion = abiVersion;
            StructSize = structSize;
            AcceptedSubmissionCount = acceptedSubmissionCount;
            RejectedSubmissionCount = rejectedSubmissionCount;
            HasLastAcceptedSubmission = hasLastAcceptedSubmission != 0;
            LastAcceptedGraphicsBackend = lastAcceptedGraphicsBackend;
            LastAcceptedRasterWidth = lastAcceptedRasterWidth;
            LastAcceptedRasterHeight = lastAcceptedRasterHeight;
            LastAcceptedEffectiveScale = lastAcceptedEffectiveScale;
            LastAcceptedDeviceEpoch = lastAcceptedDeviceEpoch;
            CurrentDeviceEpoch = currentDeviceEpoch;
        }

        internal uint AbiVersion { get; }
        internal uint StructSize { get; }
        internal ulong AcceptedSubmissionCount { get; }
        internal ulong RejectedSubmissionCount { get; }
        internal bool HasLastAcceptedSubmission { get; }
        internal int LastAcceptedGraphicsBackend { get; }
        internal int LastAcceptedRasterWidth { get; }
        internal int LastAcceptedRasterHeight { get; }
        internal float LastAcceptedEffectiveScale { get; }
        internal ulong LastAcceptedDeviceEpoch { get; }
        internal ulong CurrentDeviceEpoch { get; }

        internal bool HasExpectedAbi => AbiVersion == ExpectedAbiVersion && StructSize == ExpectedStructSize;
    }

    internal readonly struct RenderSurfaceDiagnosticsSnapshot
    {
        internal RenderSurfaceDiagnosticsSnapshot(NativeRenderDiagnosticsSnapshot native,
            RenderSurfaceCounterSnapshot counters,
            RenderSurfaceBudgetSnapshot budget,
            float requestedEffectiveScale,
            float effectiveScale,
            ulong deviceEpoch)
        {
            Native = native;
            Counters = counters;
            Budget = budget;
            RequestedEffectiveScale = requestedEffectiveScale;
            EffectiveScale = effectiveScale;
            DeviceEpoch = deviceEpoch;
        }

        internal NativeRenderDiagnosticsSnapshot Native { get; }
        internal RenderSurfaceCounterSnapshot Counters { get; }
        internal RenderSurfaceBudgetSnapshot Budget { get; }
        internal float RequestedEffectiveScale { get; }
        internal float EffectiveScale { get; }
        internal ulong DeviceEpoch { get; }
    }

    internal readonly struct RenderSurfaceDiagnostic
    {
        internal RenderSurfaceDiagnostic(RenderSurfaceFailureReason reason,
            UnitySkiaGraphicsBackend backend,
            float requestedScale,
            float clampedScale,
            float effectiveScale,
            int attemptIndex,
            ulong deviceEpoch,
            long ledgerGeneration,
            long requestedBytes,
            RenderSurfaceBudgetSnapshot budget)
        {
            Reason = reason;
            Backend = backend;
            RequestedScale = requestedScale;
            ClampedScale = clampedScale;
            EffectiveScale = effectiveScale;
            AttemptIndex = attemptIndex;
            DeviceEpoch = deviceEpoch;
            LedgerGeneration = ledgerGeneration;
            RequestedBytes = requestedBytes;
            CommittedBytes = budget.CommittedBytes;
            ReservedBytes = budget.ReservedBytes;
        }

        internal RenderSurfaceFailureReason Reason { get; }
        internal UnitySkiaGraphicsBackend Backend { get; }
        internal float RequestedScale { get; }
        internal float ClampedScale { get; }
        internal float EffectiveScale { get; }
        internal int AttemptIndex { get; }
        internal ulong DeviceEpoch { get; }
        internal long LedgerGeneration { get; }
        internal long RequestedBytes { get; }
        internal long CommittedBytes { get; }
        internal long ReservedBytes { get; }
    }

    internal readonly struct RenderSurfaceCounterSnapshot
    {
        internal RenderSurfaceCounterSnapshot(long allocationAttempts,
            long allocationSuccesses,
            long allocationFailures,
            long validationFailures,
            long suppressedAttempts,
            long atomicSwaps,
            long retirements)
        {
            AllocationAttempts = allocationAttempts;
            AllocationSuccesses = allocationSuccesses;
            AllocationFailures = allocationFailures;
            ValidationFailures = validationFailures;
            SuppressedAttempts = suppressedAttempts;
            AtomicSwaps = atomicSwaps;
            Retirements = retirements;
        }

        internal long AllocationAttempts { get; }
        internal long AllocationSuccesses { get; }
        internal long AllocationFailures { get; }
        internal long ValidationFailures { get; }
        internal long SuppressedAttempts { get; }
        internal long AtomicSwaps { get; }
        internal long Retirements { get; }
    }

    internal sealed class RenderSurfaceCounters
    {
        private long allocationAttempts;
        private long allocationSuccesses;
        private long allocationFailures;
        private long validationFailures;
        private long suppressedAttempts;
        private long atomicSwaps;
        private long retirements;

        internal void RecordAllocationAttempt() => Interlocked.Increment(ref allocationAttempts);
        internal void RecordAllocationSuccess() => Interlocked.Increment(ref allocationSuccesses);
        internal void RecordAllocationFailure() => Interlocked.Increment(ref allocationFailures);
        internal void RecordValidationFailure() => Interlocked.Increment(ref validationFailures);
        internal void RecordSuppressedAttempt() => Interlocked.Increment(ref suppressedAttempts);
        internal void RecordAtomicSwap() => Interlocked.Increment(ref atomicSwaps);
        internal void RecordRetirement() => Interlocked.Increment(ref retirements);

        internal RenderSurfaceCounterSnapshot Snapshot()
        {
            return new RenderSurfaceCounterSnapshot(Interlocked.Read(ref allocationAttempts),
                Interlocked.Read(ref allocationSuccesses),
                Interlocked.Read(ref allocationFailures),
                Interlocked.Read(ref validationFailures),
                Interlocked.Read(ref suppressedAttempts),
                Interlocked.Read(ref atomicSwaps),
                Interlocked.Read(ref retirements));
        }
    }

    internal sealed class RenderSurfaceDiagnosticDeduplicator
    {
        private readonly object sync = new object();
        private readonly HashSet<DiagnosticKey> emitted = new HashSet<DiagnosticKey>();

        internal bool ShouldEmit(RenderSurfaceFailureReason reason, ulong deviceEpoch)
        {
            lock (sync)
            {
                return emitted.Add(new DiagnosticKey(reason, deviceEpoch));
            }
        }

        internal bool ShouldEmit(RenderSurfaceDiagnostic diagnostic)
        {
            return ShouldEmit(diagnostic.Reason, diagnostic.DeviceEpoch);
        }

        private readonly struct DiagnosticKey : IEquatable<DiagnosticKey>
        {
            internal DiagnosticKey(RenderSurfaceFailureReason reason, ulong deviceEpoch)
            {
                Reason = reason;
                DeviceEpoch = deviceEpoch;
            }

            private RenderSurfaceFailureReason Reason { get; }
            private ulong DeviceEpoch { get; }

            public bool Equals(DiagnosticKey other)
            {
                return Reason == other.Reason && DeviceEpoch == other.DeviceEpoch;
            }

            public override bool Equals(object obj)
            {
                return obj is DiagnosticKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)Reason * 397) ^ DeviceEpoch.GetHashCode();
                }
            }
        }
    }

    internal static class RenderSurfaceConfigurationFingerprint
    {
        internal static ulong Compute(RenderSurfaceConfiguration configuration)
        {
            if (configuration == null)
            {
                return 0;
            }

            unchecked
            {
                const ulong offsetBasis = 14695981039346656037UL;
                var hash = offsetBasis;
                hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(
                    configuration.MaxScreenSpaceRasterScale));
                hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(
                    configuration.MinimumFallbackScale));
                hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(configuration.ScaleQuantum));
                hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(configuration.ScaleHysteresis));
                hash = Mix(hash, (ulong)configuration.ConservativeMaxTextureEdge);
                hash = Mix(hash, (ulong)configuration.MaxPixelsPerSurface);
                hash = Mix(hash, (ulong)configuration.MaxBytesPerSurface);
                hash = Mix(hash, (ulong)configuration.MaxGlobalBytes);
                hash = Mix(hash, (ulong)configuration.MaxTransitionBytes);
                hash = Mix(hash, (ulong)configuration.MaxAttemptsPerRequestAndEpoch);
                return hash;
            }
        }

        private static ulong Mix(ulong hash, ulong value)
        {
            unchecked
            {
                return (hash ^ value) * 1099511628211UL;
            }
        }
    }
}

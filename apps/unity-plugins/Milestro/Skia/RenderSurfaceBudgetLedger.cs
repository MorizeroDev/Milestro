using System;
using System.Threading;
using Milestro.Configuration;

namespace Milestro.Skia
{
    internal readonly struct RenderSurfaceBudgetSnapshot
    {
        internal RenderSurfaceBudgetSnapshot(long generation,
            long committedBytes,
            long reservedBytes,
            long reservationCount,
            long reservationFailureCount,
            long commitCount,
            long retireCount,
            long releaseCount)
        {
            Generation = generation;
            CommittedBytes = committedBytes;
            ReservedBytes = reservedBytes;
            ReservationCount = reservationCount;
            ReservationFailureCount = reservationFailureCount;
            CommitCount = commitCount;
            RetireCount = retireCount;
            ReleaseCount = releaseCount;
        }

        internal long Generation { get; }
        internal long CommittedBytes { get; }
        internal long ReservedBytes { get; }
        internal long ReservationCount { get; }
        internal long ReservationFailureCount { get; }
        internal long CommitCount { get; }
        internal long RetireCount { get; }
        internal long ReleaseCount { get; }
    }

    internal sealed class RenderSurfaceBudgetLedger
    {
        private readonly object sync = new object();
        private long generation = 1;
        private long committedBytes;
        private long reservedBytes;
        private long reservationCount;
        private long reservationFailureCount;
        private long commitCount;
        private long retireCount;
        private long releaseCount;

        internal long Generation
        {
            get
            {
                lock (sync)
                {
                    return generation;
                }
            }
        }

        internal bool TryReserve(long requestedBytes,
            long replacingBytes,
            RenderSurfaceConfiguration configuration,
            out RenderSurfaceBudgetReservation reservation,
            out RenderSurfaceFailureReason failureReason)
        {
            reservation = null!;
            failureReason = RenderSurfaceFailureReason.None;
            lock (sync)
            {
                if (requestedBytes <= 0 || replacingBytes < 0 || configuration == null)
                {
                    ++reservationFailureCount;
                    failureReason = RenderSurfaceFailureReason.InvalidRequest;
                    return false;
                }

                if (!TryAdd(replacingBytes, requestedBytes, out var transitionBytes) ||
                    configuration.MaxTransitionBytes <= 0 ||
                    transitionBytes > configuration.MaxTransitionBytes)
                {
                    ++reservationFailureCount;
                    failureReason = RenderSurfaceFailureReason.TransitionBudget;
                    return false;
                }

                if (!TryAdd(committedBytes, reservedBytes, out var usedBytes) ||
                    !TryAdd(usedBytes, requestedBytes, out var nextUsedBytes) ||
                    configuration.MaxGlobalBytes <= 0 ||
                    nextUsedBytes > configuration.MaxGlobalBytes)
                {
                    ++reservationFailureCount;
                    failureReason = RenderSurfaceFailureReason.GlobalBudget;
                    return false;
                }

                reservedBytes += requestedBytes;
                ++reservationCount;
                reservation = new RenderSurfaceBudgetReservation(this, requestedBytes);
                return true;
            }
        }

        internal RenderSurfaceBudgetSnapshot Snapshot()
        {
            lock (sync)
            {
                return new RenderSurfaceBudgetSnapshot(generation,
                    committedBytes,
                    reservedBytes,
                    reservationCount,
                    reservationFailureCount,
                    commitCount,
                    retireCount,
                    releaseCount);
            }
        }

        private RenderSurfaceBudgetLease CommitReservation(long bytes)
        {
            lock (sync)
            {
                if (bytes <= 0 || reservedBytes < bytes)
                {
                    throw new InvalidOperationException("Milestro render-surface reservation cannot be committed twice.");
                }

                reservedBytes -= bytes;
                committedBytes += bytes;
                ++commitCount;
                return new RenderSurfaceBudgetLease(this, bytes);
            }
        }

        private void CancelReservation(long bytes)
        {
            lock (sync)
            {
                if (bytes <= 0 || reservedBytes < bytes)
                {
                    return;
                }

                reservedBytes -= bytes;
                AdvanceGeneration();
            }
        }

        private void MarkRetired()
        {
            lock (sync)
            {
                ++retireCount;
            }
        }

        private void ReleaseCommitted(long bytes)
        {
            lock (sync)
            {
                if (bytes <= 0 || committedBytes < bytes)
                {
                    return;
                }

                committedBytes -= bytes;
                ++releaseCount;
                AdvanceGeneration();
            }
        }

        private void AdvanceGeneration()
        {
            if (generation < long.MaxValue)
            {
                ++generation;
            }
        }

        private static bool TryAdd(long left, long right, out long sum)
        {
            sum = 0;
            if (left < 0 || right < 0 || left > long.MaxValue - right)
            {
                return false;
            }

            sum = left + right;
            return true;
        }

        internal sealed class RenderSurfaceBudgetReservation : IDisposable
        {
            private readonly RenderSurfaceBudgetLedger owner;
            private readonly long bytes;
            private int state;

            internal RenderSurfaceBudgetReservation(RenderSurfaceBudgetLedger owner, long bytes)
            {
                this.owner = owner;
                this.bytes = bytes;
            }

            internal RenderSurfaceBudgetLease Commit()
            {
                if (Interlocked.CompareExchange(ref state, 1, 0) != 0)
                {
                    throw new InvalidOperationException("Milestro render-surface reservation is no longer active.");
                }

                return owner.CommitReservation(bytes);
            }

            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref state, 2, 0) == 0)
                {
                    owner.CancelReservation(bytes);
                }
            }
        }

        internal sealed class RenderSurfaceBudgetLease : IDisposable
        {
            private readonly RenderSurfaceBudgetLedger owner;
            private readonly long bytes;
            private int state;

            internal RenderSurfaceBudgetLease(RenderSurfaceBudgetLedger owner, long bytes)
            {
                this.owner = owner;
                this.bytes = bytes;
            }

            internal long Bytes => bytes;

            internal void MarkRetired()
            {
                if (Interlocked.CompareExchange(ref state, 1, 0) == 0)
                {
                    owner.MarkRetired();
                }
            }

            public void Dispose()
            {
                var previous = Interlocked.Exchange(ref state, 2);
                if (previous != 2)
                {
                    owner.ReleaseCommitted(bytes);
                }
            }
        }
    }
}

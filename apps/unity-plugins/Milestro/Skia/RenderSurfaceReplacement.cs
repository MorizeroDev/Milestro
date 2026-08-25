using System;
using Milestro.Configuration;

namespace Milestro.Skia
{
    internal readonly struct RenderSurfaceAllocationResult<TTarget> where TTarget : class
    {
        private RenderSurfaceAllocationResult(TTarget? target,
            RenderSurfaceFailureReason failureReason)
        {
            Target = target;
            FailureReason = failureReason;
        }

        internal TTarget? Target { get; }
        internal RenderSurfaceFailureReason FailureReason { get; }
        internal bool Success => Target != null && FailureReason == RenderSurfaceFailureReason.None;

        internal static RenderSurfaceAllocationResult<TTarget> Succeeded(TTarget target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return new RenderSurfaceAllocationResult<TTarget>(target, RenderSurfaceFailureReason.None);
        }

        internal static RenderSurfaceAllocationResult<TTarget> Failed(RenderSurfaceFailureReason failureReason,
            TTarget? target = null)
        {
            if (failureReason == RenderSurfaceFailureReason.None)
            {
                throw new ArgumentOutOfRangeException(nameof(failureReason));
            }

            return new RenderSurfaceAllocationResult<TTarget>(target, failureReason);
        }
    }

    internal sealed class RenderSurfaceReplacement<TTarget> where TTarget : class
    {
        private readonly object sync = new object();
        private readonly RenderSurfaceBudgetLedger ledger;
        private TTarget? currentTarget;
        private RenderSurfaceBudgetLedger.RenderSurfaceBudgetLease? currentLease;

        internal RenderSurfaceReplacement(RenderSurfaceBudgetLedger ledger)
        {
            this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        }

        internal TTarget? CurrentTarget
        {
            get
            {
                lock (sync)
                {
                    return currentTarget;
                }
            }
        }

        internal long CurrentBytes
        {
            get
            {
                lock (sync)
                {
                    return currentLease?.Bytes ?? 0;
                }
            }
        }

        internal bool TryReplace(long requestedBytes,
            RenderSurfaceConfiguration configuration,
            Func<RenderSurfaceAllocationResult<TTarget>> allocate,
            Action<TTarget> releaseRejected,
            Action<TTarget, RenderSurfaceBudgetLedger.RenderSurfaceBudgetLease> retire,
            out RenderSurfaceFailureReason failureReason)
        {
            if (allocate == null)
            {
                throw new ArgumentNullException(nameof(allocate));
            }

            if (releaseRejected == null)
            {
                throw new ArgumentNullException(nameof(releaseRejected));
            }

            if (retire == null)
            {
                throw new ArgumentNullException(nameof(retire));
            }

            RenderSurfaceBudgetLedger.RenderSurfaceBudgetReservation reservation;
            lock (sync)
            {
                if (!ledger.TryReserve(requestedBytes,
                        currentLease?.Bytes ?? 0,
                        configuration,
                        out reservation,
                        out failureReason))
                {
                    return false;
                }
            }

            RenderSurfaceAllocationResult<TTarget> allocation;
            try
            {
                allocation = allocate();
            }
            catch
            {
                reservation.Dispose();
                failureReason = RenderSurfaceFailureReason.Allocation;
                return false;
            }

            var nextTarget = allocation.Target;
            if (nextTarget == null || allocation.FailureReason != RenderSurfaceFailureReason.None)
            {
                try
                {
                    if (nextTarget != null)
                    {
                        releaseRejected(nextTarget);
                    }
                }
                finally
                {
                    reservation.Dispose();
                }

                failureReason = allocation.FailureReason;
                return false;
            }

            var nextLease = reservation.Commit();
            TTarget? previousTarget;
            RenderSurfaceBudgetLedger.RenderSurfaceBudgetLease? previousLease;
            lock (sync)
            {
                previousTarget = currentTarget;
                previousLease = currentLease;
                currentTarget = nextTarget;
                currentLease = nextLease;
            }

            if (previousTarget != null && previousLease != null)
            {
                previousLease.MarkRetired();
                try
                {
                    retire(previousTarget, previousLease);
                }
                catch
                {
                    previousLease.Dispose();
                    throw;
                }
            }

            failureReason = RenderSurfaceFailureReason.None;
            return true;
        }

        internal void Clear(Action<TTarget, RenderSurfaceBudgetLedger.RenderSurfaceBudgetLease> retire)
        {
            if (retire == null)
            {
                throw new ArgumentNullException(nameof(retire));
            }

            TTarget? previousTarget;
            RenderSurfaceBudgetLedger.RenderSurfaceBudgetLease? previousLease;
            lock (sync)
            {
                previousTarget = currentTarget;
                previousLease = currentLease;
                currentTarget = null;
                currentLease = null;
            }

            if (previousTarget == null || previousLease == null)
            {
                return;
            }

            previousLease.MarkRetired();
            try
            {
                retire(previousTarget, previousLease);
            }
            catch
            {
                previousLease.Dispose();
                throw;
            }
        }
    }
}

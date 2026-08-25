using System;

namespace Milestro.Skia
{
    internal readonly struct RenderSurfaceRecoveryKey : IEquatable<RenderSurfaceRecoveryKey>
    {
        internal RenderSurfaceRecoveryKey(RenderSurfaceRasterRequest request,
            RenderSurfaceDescriptor descriptor,
            ulong deviceEpoch,
            long ledgerGeneration,
            ulong configurationFingerprint)
        {
            LogicalWidth = request.LogicalWidth;
            LogicalHeight = request.LogicalHeight;
            DesiredScaleBits = BitConverter.SingleToInt32Bits(request.DesiredScale);
            Backend = descriptor.Backend;
            PreferredFormat = descriptor.TextureDescriptor.PreferredFormat;
            ColorSpace = descriptor.TextureDescriptor.ColorSpace;
            UseSrgbStorage = descriptor.TextureDescriptor.UseSrgbStorage;
            ClearBeforeDraw = descriptor.TextureDescriptor.ClearBeforeDraw;
            MsaaSamples = descriptor.TextureDescriptor.MsaaSamples;
            ResolveStrategy = descriptor.TextureDescriptor.ResolveStrategy;
            MipLevelCount = descriptor.MipLevelCount;
            DeviceEpoch = deviceEpoch;
            LedgerGeneration = ledgerGeneration;
            ConfigurationFingerprint = configurationFingerprint;
        }

        private int LogicalWidth { get; }
        private int LogicalHeight { get; }
        private int DesiredScaleBits { get; }
        private UnitySkiaGraphicsBackend Backend { get; }
        private UnitySkiaRenderTextureFormat PreferredFormat { get; }
        private UnityEngine.ColorSpace ColorSpace { get; }
        private bool UseSrgbStorage { get; }
        private bool ClearBeforeDraw { get; }
        private int MsaaSamples { get; }
        private UnitySkiaRenderTextureResolveStrategy ResolveStrategy { get; }
        private int MipLevelCount { get; }
        private ulong DeviceEpoch { get; }
        private long LedgerGeneration { get; }
        private ulong ConfigurationFingerprint { get; }

        public bool Equals(RenderSurfaceRecoveryKey other)
        {
            return EqualsIgnoringLedgerGeneration(other) &&
                   LedgerGeneration == other.LedgerGeneration;
        }

        internal bool EqualsIgnoringLedgerGeneration(RenderSurfaceRecoveryKey other)
        {
            return LogicalWidth == other.LogicalWidth &&
                   LogicalHeight == other.LogicalHeight &&
                   DesiredScaleBits == other.DesiredScaleBits &&
                   Backend == other.Backend &&
                   PreferredFormat == other.PreferredFormat &&
                   ColorSpace == other.ColorSpace &&
                   UseSrgbStorage == other.UseSrgbStorage &&
                   ClearBeforeDraw == other.ClearBeforeDraw &&
                   MsaaSamples == other.MsaaSamples &&
                   ResolveStrategy == other.ResolveStrategy &&
                   MipLevelCount == other.MipLevelCount &&
                   DeviceEpoch == other.DeviceEpoch &&
                   ConfigurationFingerprint == other.ConfigurationFingerprint;
        }

        public override bool Equals(object obj)
        {
            return obj is RenderSurfaceRecoveryKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = LogicalWidth;
                hash = (hash * 397) ^ LogicalHeight;
                hash = (hash * 397) ^ DesiredScaleBits;
                hash = (hash * 397) ^ (int)Backend;
                hash = (hash * 397) ^ (int)PreferredFormat;
                hash = (hash * 397) ^ (int)ColorSpace;
                hash = (hash * 397) ^ UseSrgbStorage.GetHashCode();
                hash = (hash * 397) ^ ClearBeforeDraw.GetHashCode();
                hash = (hash * 397) ^ MsaaSamples;
                hash = (hash * 397) ^ (int)ResolveStrategy;
                hash = (hash * 397) ^ MipLevelCount;
                hash = (hash * 397) ^ DeviceEpoch.GetHashCode();
                hash = (hash * 397) ^ LedgerGeneration.GetHashCode();
                hash = (hash * 397) ^ ConfigurationFingerprint.GetHashCode();
                return hash;
            }
        }
    }

    internal readonly struct RenderSurfaceRecoveryAttempt
    {
        internal RenderSurfaceRecoveryAttempt(RenderSurfaceRecoveryKey key, int index)
        {
            Key = key;
            Index = index;
        }

        internal RenderSurfaceRecoveryKey Key { get; }
        internal int Index { get; }
    }

    internal sealed class RenderSurfaceRecoveryController
    {
        private const int MaximumBackoffShift = 6;
        private readonly object sync = new object();
        private RenderSurfaceRecoveryKey currentKey;
        private bool hasCurrentKey;
        private bool suppressed;
        private int attemptCount;
        private int maximumAttempts;
        private int activeAttemptIndex = -1;
        private ulong nextAttemptTick;
        private RenderSurfaceFailureReason lastFailureReason;

        internal bool TryBeginAttempt(RenderSurfaceRecoveryKey key,
            ulong currentTick,
            int maxAttempts,
            out RenderSurfaceRecoveryAttempt attempt,
            out RenderSurfaceFailureReason suppressionReason)
        {
            attempt = default;
            suppressionReason = RenderSurfaceFailureReason.None;
            lock (sync)
            {
                if (maxAttempts <= 0)
                {
                    suppressionReason = RenderSurfaceFailureReason.InvalidConfiguration;
                    return false;
                }

                if (!hasCurrentKey)
                {
                    ResetForKey(key, maxAttempts);
                }
                else if (!currentKey.Equals(key))
                {
                    if (ShouldPreserveStateAcrossLedgerChange(key))
                    {
                        currentKey = key;
                    }
                    else
                    {
                        ResetForKey(key, maxAttempts);
                    }
                }

                if (suppressed || attemptCount >= maximumAttempts)
                {
                    suppressionReason = RenderSurfaceFailureReason.RetryExhausted;
                    return false;
                }

                if (activeAttemptIndex >= 0 || currentTick < nextAttemptTick)
                {
                    suppressionReason = lastFailureReason == RenderSurfaceFailureReason.None
                        ? RenderSurfaceFailureReason.RetryExhausted
                        : lastFailureReason;
                    return false;
                }

                var index = attemptCount++;
                activeAttemptIndex = index;
                attempt = new RenderSurfaceRecoveryAttempt(currentKey, index);
                return true;
            }
        }

        internal void RecordFailure(RenderSurfaceRecoveryAttempt attempt,
            RenderSurfaceFailureReason reason,
            ulong currentTick)
        {
            lock (sync)
            {
                if (!IsCurrentActiveAttempt(attempt))
                {
                    return;
                }

                activeAttemptIndex = -1;
                lastFailureReason = reason;
                if (!IsRetryableAllocationFailure(reason) || attemptCount >= maximumAttempts)
                {
                    suppressed = true;
                    return;
                }

                var shift = Math.Min(attemptCount - 1, MaximumBackoffShift);
                var delay = 1UL << shift;
                nextAttemptTick = ulong.MaxValue - currentTick < delay
                    ? ulong.MaxValue
                    : currentTick + delay;
            }
        }

        internal void RecordSuccess(RenderSurfaceRecoveryAttempt attempt)
        {
            lock (sync)
            {
                if (!IsCurrentActiveAttempt(attempt))
                {
                    return;
                }

                hasCurrentKey = false;
                activeAttemptIndex = -1;
                attemptCount = 0;
                maximumAttempts = 0;
                suppressed = false;
                nextAttemptTick = 0;
                lastFailureReason = RenderSurfaceFailureReason.None;
            }
        }

        private bool IsCurrentActiveAttempt(RenderSurfaceRecoveryAttempt attempt)
        {
            return hasCurrentKey &&
                   currentKey.Equals(attempt.Key) &&
                   activeAttemptIndex == attempt.Index;
        }

        private void ResetForKey(RenderSurfaceRecoveryKey key, int maxAttempts)
        {
            currentKey = key;
            hasCurrentKey = true;
            suppressed = false;
            attemptCount = 0;
            maximumAttempts = maxAttempts;
            activeAttemptIndex = -1;
            nextAttemptTick = 0;
            lastFailureReason = RenderSurfaceFailureReason.None;
        }

        private bool ShouldPreserveStateAcrossLedgerChange(RenderSurfaceRecoveryKey key)
        {
            return activeAttemptIndex < 0 &&
                   currentKey.EqualsIgnoringLedgerGeneration(key) &&
                   lastFailureReason != RenderSurfaceFailureReason.TransitionBudget &&
                   lastFailureReason != RenderSurfaceFailureReason.GlobalBudget;
        }

        private static bool IsRetryableAllocationFailure(RenderSurfaceFailureReason reason)
        {
            switch (reason)
            {
                case RenderSurfaceFailureReason.Allocation:
                case RenderSurfaceFailureReason.TextureCreation:
                case RenderSurfaceFailureReason.TextureValidation:
                case RenderSurfaceFailureReason.NativeHandle:
                    return true;
                default:
                    return false;
            }
        }
    }
}

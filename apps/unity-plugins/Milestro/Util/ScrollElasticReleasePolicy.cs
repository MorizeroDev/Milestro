using Milestro.Input;

namespace Milestro.Util
{
    internal sealed class ScrollElasticReleasePolicy
    {
        private double releaseAt = -1d;
        private long gestureId;
        private bool gestureActive;
        private bool momentumActive;

        internal bool IsPending => releaseAt >= 0d;

        internal void Observe(HybridScrollMetadata metadata, double eventTime, float releaseDelaySeconds)
        {
            switch (metadata.Capability)
            {
                case HybridScrollCapability.Phased:
                    ObservePhased(metadata, NormalizeTime(eventTime));
                    break;
                case HybridScrollCapability.DeltaOnly:
                    gestureId = 0L;
                    gestureActive = false;
                    momentumActive = false;
                    releaseAt = NormalizeTime(eventTime) + NormalizeDelay(releaseDelaySeconds);
                    break;
                default:
                    Cancel();
                    break;
            }
        }

        internal void ReleaseImmediately(double eventTime)
        {
            gestureId = 0L;
            gestureActive = false;
            momentumActive = false;
            releaseAt = NormalizeTime(eventTime);
        }

        internal bool TryBeginReturn(double currentTime)
        {
            if (!IsPending || !IsFinite(currentTime) || currentTime < releaseAt)
            {
                return false;
            }

            releaseAt = -1d;
            return true;
        }

        internal void Cancel()
        {
            releaseAt = -1d;
            gestureId = 0L;
            gestureActive = false;
            momentumActive = false;
        }

        private void ObservePhased(HybridScrollMetadata metadata, double eventTime)
        {
            if (gestureId != metadata.GestureId)
            {
                gestureId = metadata.GestureId;
                gestureActive = false;
                momentumActive = false;
            }

            var ended = UpdatePhase(metadata.GesturePhase, ref gestureActive);
            ended |= UpdatePhase(metadata.MomentumPhase, ref momentumActive);
            releaseAt = ended && !gestureActive && !momentumActive ? eventTime : -1d;
        }

        private static bool UpdatePhase(HybridInputPhase phase, ref bool active)
        {
            switch (phase)
            {
                case HybridInputPhase.Began:
                case HybridInputPhase.Changed:
                case HybridInputPhase.Stationary:
                    active = true;
                    return false;
                case HybridInputPhase.Ended:
                case HybridInputPhase.Canceled:
                    active = false;
                    return true;
                case HybridInputPhase.None:
                    active = false;
                    return false;
                default:
                    return false;
            }
        }

        private static double NormalizeTime(double value)
        {
            return IsFinite(value) ? System.Math.Max(0d, value) : 0d;
        }

        private static float NormalizeDelay(float value)
        {
            return FloatUtil.IsFinite(value) ? UnityEngine.Mathf.Max(0f, value) : 0f;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}

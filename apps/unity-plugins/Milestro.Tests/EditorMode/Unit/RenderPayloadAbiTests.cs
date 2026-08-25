using Milestro.Skia;
using NUnit.Framework;
using RenderPayloadAbiInfo = Milestro.Skia.UnitySkiaRenderTextureSurface.RenderPayloadAbiInfo;

namespace Milestro.Tests
{
    public class RenderPayloadAbiTests
    {
        [Test]
        public void ExactManagedPayloadLayoutMatches()
        {
            Assert.That(UnitySkiaRenderTextureSurface.PayloadAbiMatches(
                UnitySkiaRenderTextureSurface.ManagedPayloadAbiInfo), Is.True);
        }

        [Test]
        public void OldVersionAndChangedLayoutFailClosed()
        {
            var current = UnitySkiaRenderTextureSurface.ManagedPayloadAbiInfo;

            Assert.That(UnitySkiaRenderTextureSurface.PayloadAbiMatches(new RenderPayloadAbiInfo(
                    current.AbiVersion + 1,
                    current.LayoutFingerprint,
                    current.TargetSize,
                    current.SubmissionSize,
                    current.TargetEffectiveScaleOffset,
                    current.TargetDeviceEpochOffset,
                    current.SubmissionTargetOffset,
                    current.SubmissionCompletedOffset)),
                Is.False);
            Assert.That(UnitySkiaRenderTextureSurface.PayloadAbiMatches(new RenderPayloadAbiInfo(
                    current.AbiVersion,
                    current.LayoutFingerprint + 1,
                    current.TargetSize,
                    current.SubmissionSize,
                    current.TargetEffectiveScaleOffset,
                    current.TargetDeviceEpochOffset,
                    current.SubmissionTargetOffset,
                    current.SubmissionCompletedOffset)),
                Is.False);
            Assert.That(UnitySkiaRenderTextureSurface.PayloadAbiMatches(new RenderPayloadAbiInfo(
                    current.AbiVersion,
                    current.LayoutFingerprint,
                    current.TargetSize,
                    current.SubmissionSize + 1,
                    current.TargetEffectiveScaleOffset,
                    current.TargetDeviceEpochOffset,
                    current.SubmissionTargetOffset,
                    current.SubmissionCompletedOffset)),
                Is.False);
            Assert.That(UnitySkiaRenderTextureSurface.PayloadAbiMatches(new RenderPayloadAbiInfo(
                    current.AbiVersion,
                    current.LayoutFingerprint,
                    current.TargetSize,
                    current.SubmissionSize,
                    current.TargetEffectiveScaleOffset,
                    current.TargetDeviceEpochOffset,
                    current.SubmissionTargetOffset,
                    current.SubmissionCompletedOffset + 1)),
                Is.False);
        }

        [Test]
        public void NativeDiagnosticsVersionAndSizeFailClosed()
        {
            Assert.That(Diagnostics(NativeRenderDiagnosticsSnapshot.ExpectedAbiVersion,
                NativeRenderDiagnosticsSnapshot.ExpectedStructSize).HasExpectedAbi, Is.True);
            Assert.That(Diagnostics(NativeRenderDiagnosticsSnapshot.ExpectedAbiVersion + 1,
                NativeRenderDiagnosticsSnapshot.ExpectedStructSize).HasExpectedAbi, Is.False);
            Assert.That(Diagnostics(NativeRenderDiagnosticsSnapshot.ExpectedAbiVersion,
                NativeRenderDiagnosticsSnapshot.ExpectedStructSize + 1).HasExpectedAbi, Is.False);
        }

        private static NativeRenderDiagnosticsSnapshot Diagnostics(uint abiVersion, uint structSize)
        {
            return new NativeRenderDiagnosticsSnapshot(abiVersion,
                structSize,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1);
        }
    }
}

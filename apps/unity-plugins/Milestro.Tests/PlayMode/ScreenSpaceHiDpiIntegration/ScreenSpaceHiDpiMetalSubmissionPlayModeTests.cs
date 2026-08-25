using System;
using System.Collections;
using Milestro.Skia;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Milestro.Tests.ScreenSpaceHiDpi.Integration.PlayMode
{
    public sealed class ScreenSpaceHiDpiMetalSubmissionPlayModeTests
    {
        private const int RasterWidth = 64;
        private const int RasterHeight = 48;
        private const int MaxSubmitFrames = 120;
        private const int MaxCompletionFrames = 300;

        private static readonly byte[] PngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJ" +
            "AAAADUlEQVR42mP8z8BQDwAFgwJ/lmD3WQAAAABJRU5ErkJggg==");

        [UnityTest]
        public IEnumerator RealMetalSubmissionMatchesNativeAndManagedDiagnostics()
        {
            Assert.That(SystemInfo.graphicsDeviceType,
                Is.EqualTo(GraphicsDeviceType.Metal),
                "SCREEN_SPACE_HIDPI_METAL_REQUIRED: PlayMode smoke must run with Unity's Metal graphics device.");

            UnityAutoRenderTextureSurface surface = null;
            RenderTexture renderTexture = null;
            MilestroImage image = null;
            Action<UnitySkiaRenderTextureSurface.RenderSubmissionStatus> completionHandler = null;
            var completionCount = 0;
            UnitySkiaRenderTextureSurface.RenderSubmissionStatus? completionStatus = null;

            try
            {
                surface = new UnityAutoRenderTextureSurface(RasterWidth, RasterHeight);
                Assert.That(surface.Backend, Is.EqualTo(UnitySkiaGraphicsBackend.Metal));

                renderTexture = surface.RenderTexture;
                Assert.That(renderTexture, Is.Not.Null, "SCREEN_SPACE_HIDPI_RENDER_TEXTURE_MISSING");
                Assert.That(renderTexture.IsCreated(), Is.True, "SCREEN_SPACE_HIDPI_RENDER_TEXTURE_NOT_CREATED");

                var before = surface.DiagnosticsSnapshot;
                var expectedAccepted = checked(before.Native.AcceptedSubmissionCount + 1UL);
                image = MilestroImage.MakeFromBytes(PngBytes);
                Assert.That(image.Width, Is.EqualTo(1), "SCREEN_SPACE_HIDPI_DRAW_IMAGE_WIDTH");
                Assert.That(image.Height, Is.EqualTo(1), "SCREEN_SPACE_HIDPI_DRAW_IMAGE_HEIGHT");

                var commands = new UnitySkiaRenderCommandList();
                commands.DrawImage(image, new Rect(0f, 0f, RasterWidth, RasterHeight));
                Assert.That(commands.Count, Is.EqualTo(1), "SCREEN_SPACE_HIDPI_DRAW_COMMAND_COUNT");

                completionHandler = status =>
                {
                    ++completionCount;
                    completionStatus = status;
                };
                surface.RenderEventCompleted += completionHandler;

                var submitted = false;
                for (var frame = 0; frame < MaxSubmitFrames && !submitted; ++frame)
                {
                    submitted = surface.TrySubmit(commands);
                    if (!submitted)
                    {
                        yield return null;
                    }
                }
                Assert.That(submitted,
                    Is.True,
                    "SCREEN_SPACE_HIDPI_SUBMIT_TIMEOUT: real Metal surface never exposed a usable native target.");

                var diagnosticsReady = false;
                RenderSurfaceDiagnosticsSnapshot after = default;
                for (var frame = 0; frame < MaxCompletionFrames && !diagnosticsReady; ++frame)
                {
                    after = surface.DiagnosticsSnapshot;
                    Assert.That(after.Native.RejectedSubmissionCount,
                        Is.EqualTo(before.Native.RejectedSubmissionCount),
                        "SCREEN_SPACE_HIDPI_UNEXPECTED_NATIVE_REJECTION");
                    Assert.That(after.Native.AcceptedSubmissionCount,
                        Is.LessThanOrEqualTo(expectedAccepted),
                        "SCREEN_SPACE_HIDPI_UNEXPECTED_EXTRA_NATIVE_SUBMISSION");
                    diagnosticsReady = completionStatus.HasValue &&
                                       after.Native.AcceptedSubmissionCount == expectedAccepted;
                    if (!diagnosticsReady)
                    {
                        yield return null;
                    }
                }

                Assert.That(diagnosticsReady,
                    Is.True,
                    "SCREEN_SPACE_HIDPI_COMPLETION_TIMEOUT: accepted native submission did not complete in bounded frames.");
                Assert.That(completionCount, Is.EqualTo(1), "SCREEN_SPACE_HIDPI_COMPLETION_COUNT");
                Assert.That(completionStatus,
                    Is.EqualTo(UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn),
                    "SCREEN_SPACE_HIDPI_METAL_DRAW_NOT_COMPLETED");

                var managedWidth = surface.Width;
                var managedHeight = surface.Height;
                var managedScale = surface.EffectiveRasterScale;
                var managedEpoch = surface.DeviceEpoch;
                after = surface.DiagnosticsSnapshot;

                Assert.That(managedWidth, Is.EqualTo(RasterWidth));
                Assert.That(managedHeight, Is.EqualTo(RasterHeight));
                Assert.That(managedScale, Is.GreaterThan(0f));
                Assert.That(managedEpoch, Is.GreaterThan(0UL));
                Assert.That(after.Native.AcceptedSubmissionCount, Is.EqualTo(expectedAccepted));
                Assert.That(after.Native.RejectedSubmissionCount,
                    Is.EqualTo(before.Native.RejectedSubmissionCount));
                Assert.That(after.Native.HasLastAcceptedSubmission, Is.True);
                Assert.That(after.Native.LastAcceptedGraphicsBackend,
                    Is.EqualTo((int)UnitySkiaGraphicsBackend.Metal));
                Assert.That(after.Native.LastAcceptedRasterWidth, Is.EqualTo(managedWidth));
                Assert.That(after.Native.LastAcceptedRasterHeight, Is.EqualTo(managedHeight));
                Assert.That(after.Native.LastAcceptedEffectiveScale,
                    Is.EqualTo(managedScale).Within(0.0001f));
                Assert.That(after.Native.LastAcceptedDeviceEpoch, Is.EqualTo(managedEpoch));
                Assert.That(after.Native.CurrentDeviceEpoch, Is.EqualTo(managedEpoch));
                Assert.That(after.EffectiveScale, Is.EqualTo(managedScale).Within(0.0001f));
                Assert.That(after.DeviceEpoch, Is.EqualTo(managedEpoch));
            }
            finally
            {
                try
                {
                    if (surface != null && completionHandler != null)
                    {
                        surface.RenderEventCompleted -= completionHandler;
                    }
                    if (image != null)
                    {
                        if (surface != null)
                        {
                            surface.DisposeResourceAfterPendingDraws(image);
                        }
                        else
                        {
                            image.Dispose();
                        }
                    }
                }
                finally
                {
                    surface?.Dispose();
                }
            }

            Assert.That(renderTexture == null || !renderTexture.IsCreated(),
                Is.True,
                "SCREEN_SPACE_HIDPI_RENDER_TEXTURE_NOT_RELEASED");
            yield return null;
        }
    }
}

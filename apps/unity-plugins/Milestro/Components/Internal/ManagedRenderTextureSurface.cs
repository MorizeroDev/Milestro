using System;
using Milestro.Configuration;
using Milestro.Skia;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal sealed class ManagedRenderTextureSurface : IDisposable
    {
        private readonly RenderSurfaceRecoveryController recoveryController = new RenderSurfaceRecoveryController();
        private readonly RenderSurfaceDiagnosticDeduplicator diagnosticDeduplicator =
            new RenderSurfaceDiagnosticDeduplicator();
        private UnityAutoRenderTextureSurface? surface;
        private float stableDesiredScale;
        private ulong attemptTick;

        internal event Action<UnitySkiaRenderTextureSurface.RenderSubmissionStatus>? RenderEventCompleted;

        internal UnitySkiaGraphicsBackend Backend => surface?.Backend ??
                                                     UnityAutoRenderTextureSurface.SelectBackendForCurrentGraphicsDevice();
        internal ColorSpace ColorSpace => surface?.ColorSpace ?? UnitySkiaRenderTextureDescriptor.DefaultColorSpace;
        internal Rect DisplayUvRect => surface?.DisplayUvRect ?? new Rect(0f, 0f, 1f, 1f);
        internal Texture? Texture => surface?.Texture;
        internal int Width => surface?.Width ?? 0;
        internal int Height => surface?.Height ?? 0;
        internal float EffectiveRasterScale => surface?.EffectiveRasterScale ?? 1f;
        internal bool HasSurface => surface != null;

        internal bool EnsureExact(Vector2Int sizePixels, ColorSpace colorSpace, out bool changed)
        {
            changed = false;
            sizePixels = NormalizeSize(sizePixels);
            stableDesiredScale = 0f;
            if (surface == null || surface.ColorSpace != colorSpace)
            {
                var descriptor = new UnitySkiaRenderTextureDescriptor(sizePixels.x, sizePixels.y, colorSpace)
                {
                    VulkanBackend = MilestroConfiguration.Configuration?.RenderSurface?.VulkanBackend ??
                                    UnitySkiaVulkanBackend.Direct
                };
                var next = new UnityAutoRenderTextureSurface(descriptor);
                SwapSurface(next);
                changed = true;
                return true;
            }

            var previousTexture = surface.Texture;
            var previousWidth = surface.Width;
            var previousHeight = surface.Height;
            var previousScale = surface.EffectiveRasterScale;
            var previousEpoch = surface.DeviceEpoch;
            surface.Resize(sizePixels.x, sizePixels.y);
            changed = previousTexture != surface.Texture ||
                      previousWidth != surface.Width ||
                      previousHeight != surface.Height ||
                      !Mathf.Approximately(previousScale, surface.EffectiveRasterScale) ||
                      previousEpoch != surface.DeviceEpoch;
            return true;
        }

        internal bool TryEnsureScreenSpace(Vector2Int logicalSize,
            float desiredScale,
            ColorSpace colorSpace,
            UnityEngine.Object? logContext,
            out bool changed)
        {
            changed = false;
            AdvanceAttemptTick();
            var configuration = MilestroConfiguration.Configuration?.RenderSurface;
            UnitySkiaGraphicsBackend backend;
            try
            {
                backend = Backend;
            }
            catch
            {
                return false;
            }

            if (configuration == null)
            {
                EmitDiagnostic(RenderSurfaceFailureReason.InvalidConfiguration,
                    backend,
                    desiredScale,
                    0f,
                    0f,
                    -1,
                    0,
                    logContext);
                return false;
            }

            if (!RenderSurfacePolicy.TryQuantizeDesiredScale(desiredScale,
                    configuration.ScaleQuantum,
                    configuration.ScaleHysteresis,
                    configuration.MaxScreenSpaceRasterScale,
                    stableDesiredScale,
                    out var quantizedScale))
            {
                EmitDiagnostic(RenderSurfaceFailureReason.InvalidRequest,
                    backend,
                    desiredScale,
                    0f,
                    0f,
                    -1,
                    0,
                    logContext);
                return false;
            }

            logicalSize = NormalizeSize(logicalSize);
            var request = new RenderSurfaceRasterRequest(logicalSize.x, logicalSize.y, quantizedScale);
            var textureDescriptor = new UnitySkiaRenderTextureDescriptor(logicalSize.x,
                logicalSize.y,
                colorSpace)
            {
                VulkanBackend = configuration.VulkanBackend
            };
            var descriptor = new RenderSurfaceDescriptor(backend, textureDescriptor, 1);
            if (!RenderSurfacePolicy.TryBuildCandidatePlan(request,
                    ResolveRuntimeCaps(backend),
                    configuration,
                    descriptor,
                    out var plan,
                    out var planFailure))
            {
                EmitDiagnostic(planFailure,
                    backend,
                    quantizedScale,
                    0f,
                    0f,
                    -1,
                    0,
                    logContext);
                return false;
            }

            var budget = UnitySkiaRenderTextureSurface.BudgetLedger.Snapshot();
            ulong deviceEpoch;
            try
            {
                deviceEpoch = UnityAutoRenderTextureSurface.ReadCurrentDeviceEpoch();
            }
            catch
            {
                EmitDiagnostic(RenderSurfaceFailureReason.DeviceEpoch,
                    backend,
                    plan.RequestedScale,
                    plan.ClampedScale,
                    0f,
                    -1,
                    0,
                    logContext);
                return false;
            }

            var recoveryKey = new RenderSurfaceRecoveryKey(request,
                descriptor,
                deviceEpoch,
                budget.Generation,
                RenderSurfaceConfigurationFingerprint.Compute(configuration));
            if (!recoveryController.TryBeginAttempt(recoveryKey,
                    attemptTick,
                    configuration.MaxAttemptsPerRequestAndEpoch,
                    out var recoveryAttempt,
                    out _))
            {
                return false;
            }

            var lastFailure = RenderSurfaceFailureReason.RetryExhausted;
            for (var index = 0; index < plan.Candidates.Count; ++index)
            {
                var candidate = plan.Candidates[index];
                if (TryApplyCandidate(candidate, colorSpace, configuration, out changed, out lastFailure))
                {
                    stableDesiredScale = quantizedScale;
                    recoveryController.RecordSuccess(recoveryAttempt);
                    return true;
                }

                EmitDiagnostic(lastFailure,
                    backend,
                    plan.RequestedScale,
                    plan.ClampedScale,
                    candidate.EffectiveScale,
                    index,
                    candidate.ByteCount,
                    logContext);
            }

            recoveryController.RecordFailure(recoveryAttempt, lastFailure, attemptTick);
            return false;
        }

        internal bool TrySubmit(UnitySkiaRenderCommandList commands)
        {
            return surface != null && surface.TrySubmit(commands);
        }

        internal bool TrySubmitSlimTextNoAlloc(UnitySkiaRenderTextureSurface.SlimTextNoAllocSubmission submission,
            Vector2 baseline,
            bool drawText)
        {
            return surface != null && surface.TrySubmitSlimTextNoAlloc(submission, baseline, drawText);
        }

        internal void DisposeResourceAfterPendingDraws(IDisposable resource)
        {
            if (surface != null)
            {
                surface.DisposeResourceAfterPendingDraws(resource);
            }
            else
            {
                resource?.Dispose();
            }
        }

        public void Dispose()
        {
            if (surface == null)
            {
                return;
            }

            surface.RenderEventCompleted -= HandleRenderEventCompleted;
            surface.Dispose();
            surface = null;
            stableDesiredScale = 0f;
        }

        private bool TryApplyCandidate(RenderSurfaceCandidate candidate,
            ColorSpace colorSpace,
            RenderSurfaceConfiguration configuration,
            out bool changed,
            out RenderSurfaceFailureReason failureReason)
        {
            changed = false;
            if (surface == null)
            {
                if (!UnityAutoRenderTextureSurface.TryCreate(candidate,
                        colorSpace,
                        configuration,
                        out var created,
                        out failureReason))
                {
                    return false;
                }

                SwapSurface(created!);
                changed = true;
                return true;
            }

            var previousTexture = surface.Texture;
            var previousWidth = surface.Width;
            var previousHeight = surface.Height;
            var previousScale = surface.EffectiveRasterScale;
            var previousEpoch = surface.DeviceEpoch;
            if (!surface.TryResize(candidate, colorSpace, configuration, out failureReason))
            {
                return false;
            }

            changed = previousTexture != surface.Texture ||
                      previousWidth != surface.Width ||
                      previousHeight != surface.Height ||
                      !Mathf.Approximately(previousScale, surface.EffectiveRasterScale) ||
                      previousEpoch != surface.DeviceEpoch;
            return true;
        }

        private void SwapSurface(UnityAutoRenderTextureSurface next)
        {
            var previous = surface;
            if (previous != null)
            {
                previous.RenderEventCompleted -= HandleRenderEventCompleted;
            }

            surface = next;
            surface.RenderEventCompleted += HandleRenderEventCompleted;
            previous?.Dispose();
        }

        private void HandleRenderEventCompleted(UnitySkiaRenderTextureSurface.RenderSubmissionStatus status)
        {
            RenderEventCompleted?.Invoke(status);
        }

        private void EmitDiagnostic(RenderSurfaceFailureReason reason,
            UnitySkiaGraphicsBackend backend,
            float requestedScale,
            float clampedScale,
            float effectiveScale,
            int attemptIndex,
            long requestedBytes,
            UnityEngine.Object? logContext)
        {
            var budget = UnitySkiaRenderTextureSurface.BudgetLedger.Snapshot();
            var epoch = surface?.DeviceEpoch ?? 0;
            var diagnostic = new RenderSurfaceDiagnostic(reason,
                backend,
                requestedScale,
                clampedScale,
                effectiveScale,
                attemptIndex,
                epoch,
                budget.Generation,
                requestedBytes,
                budget);
            if (!diagnosticDeduplicator.ShouldEmit(diagnostic))
            {
                return;
            }

            Debug.LogWarning("Milestro screen-space render surface rejected a raster candidate: " +
                             $"reason={reason} backend={backend} requestedScale={requestedScale:F4} " +
                             $"clampedScale={clampedScale:F4} effectiveScale={effectiveScale:F4} " +
                             $"attempt={attemptIndex} epoch={epoch} ledgerGeneration={budget.Generation} " +
                             $"requestedBytes={requestedBytes} committedBytes={budget.CommittedBytes} " +
                             $"reservedBytes={budget.ReservedBytes}.",
                logContext);
        }

        private static RenderSurfaceRuntimeCaps ResolveRuntimeCaps(UnitySkiaGraphicsBackend backend)
        {
            var backendEdge = backend == UnitySkiaGraphicsBackend.OpenGLES ? 8192 : 16384;
            return new RenderSurfaceRuntimeCaps(SystemInfo.maxTextureSize, backendEdge);
        }

        private void AdvanceAttemptTick()
        {
            if (attemptTick < ulong.MaxValue)
            {
                ++attemptTick;
            }
        }

        private static Vector2Int NormalizeSize(Vector2Int sizePixels)
        {
            return new Vector2Int(Mathf.Max(1, sizePixels.x), Mathf.Max(1, sizePixels.y));
        }
    }
}

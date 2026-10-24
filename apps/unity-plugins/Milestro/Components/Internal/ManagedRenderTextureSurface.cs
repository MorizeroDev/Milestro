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
        private RenderTextureAtlas.Lease? atlasLease;
        private float stableDesiredScale;
        private ulong attemptTick;
        private bool hasCompletedOutput;
        private bool hasScreenSpaceRequest;
        private Vector2Int screenSpaceLogicalSize;
        private float screenSpaceScale;
        private ColorSpace screenSpaceColorSpace;
        private UnitySkiaGraphicsBackend screenSpaceBackend;
        private ulong screenSpaceDeviceEpoch;
        private ulong screenSpaceConfigurationFingerprint;

        internal event Action<UnitySkiaRenderTextureSurface.RenderSubmissionStatus>? RenderEventCompleted;

        internal bool AllowAtlas { get; set; } = true;
        internal UnitySkiaGraphicsBackend Backend => atlasLease?.Backend ?? surface?.Backend ??
                                                     UnityAutoRenderTextureSurface.SelectBackendForCurrentGraphicsDevice();
        internal ColorSpace ColorSpace => atlasLease?.ColorSpace ?? surface?.ColorSpace ?? UnitySkiaRenderTextureDescriptor.DefaultColorSpace;
        internal Rect DisplayUvRect => atlasLease?.DisplayUvRect ?? surface?.DisplayUvRect ?? new Rect(0f, 0f, 1f, 1f);
        internal Texture? Texture => atlasLease?.Texture ?? surface?.Texture;
        internal bool HasCompletedOutput => hasCompletedOutput && Texture != null;
        internal int Width => atlasLease?.Content.width ?? surface?.Width ?? 0;
        internal int Height => atlasLease?.Content.height ?? surface?.Height ?? 0;
        internal float EffectiveRasterScale => atlasLease?.Scale ?? surface?.EffectiveRasterScale ?? 1f;
        internal bool HasSurface => atlasLease?.IsLive ?? (surface != null);

        internal bool EnsureExact(Vector2Int sizePixels, ColorSpace colorSpace, out bool changed)
        {
            changed = false;
            sizePixels = NormalizeSize(sizePixels);
            hasScreenSpaceRequest = false;
            stableDesiredScale = 0f;
            var configuration = MilestroConfiguration.Configuration?.RenderSurface;
            var pixels = (long)sizePixels.x * sizePixels.y;
            var candidate = new RenderSurfaceCandidate(sizePixels.x, sizePixels.y, sizePixels.x, sizePixels.y,
                1f, pixels, pixels * 4);
            if (configuration != null && TryEnsureAtlas(candidate, colorSpace, configuration, out changed))
            {
                return true;
            }
            ReleaseAtlas();
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
            if (changed)
            {
                hasCompletedOutput = false;
            }
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
            var configurationFingerprint = RenderSurfaceConfigurationFingerprint.Compute(configuration);
            ulong deviceEpoch;
            try
            {
                deviceEpoch = UnityAutoRenderTextureSurface.ReadCurrentDeviceEpoch();
            }
            catch
            {
                EmitDiagnostic(RenderSurfaceFailureReason.DeviceEpoch,
                    backend,
                    quantizedScale,
                    0f,
                    0f,
                    -1,
                    0,
                    logContext);
                return false;
            }

            if (MatchesScreenSpaceRequest(logicalSize,
                    quantizedScale,
                    colorSpace,
                    backend,
                    deviceEpoch,
                    configurationFingerprint))
            {
                return true;
            }

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
            var recoveryKey = new RenderSurfaceRecoveryKey(request,
                descriptor,
                deviceEpoch,
                budget.Generation,
                configurationFingerprint);
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
                    RememberScreenSpaceRequest(logicalSize,
                        quantizedScale,
                        colorSpace,
                        backend,
                        deviceEpoch,
                        configurationFingerprint);
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

        internal bool SupportsSlimTextFastSubmit => atlasLease != null;

        internal bool CanSubmitSlimTextFast()
        {
            return atlasLease != null && atlasLease.TryBeginSubmit();
        }

        internal bool TrySubmitSlimTextFast(ReusableTextDrawSnapshot snapshot, Vector2 baseline)
        {
            return atlasLease != null && atlasLease.TrySubmitSlimText(snapshot, baseline);
        }

        internal bool TrySubmitEmptyFast()
        {
            return atlasLease != null && atlasLease.TrySubmitEmpty();
        }

        internal bool TrySubmit(UnitySkiaRenderCommandList commands)
        {
            if (atlasLease != null)
            {
                return atlasLease.TrySubmit(commands);
            }
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
            if (atlasLease != null)
            {
                atlasLease.DisposeResourceAfterPendingDraws(resource);
            }
            else if (surface != null)
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
            ReleaseAtlas();
            if (surface == null)
            {
                return;
            }

            surface.RenderEventCompleted -= HandleRenderEventCompleted;
            surface.Dispose();
            surface = null;
            stableDesiredScale = 0f;
            hasCompletedOutput = false;
        }

        private bool TryApplyCandidate(RenderSurfaceCandidate candidate,
            ColorSpace colorSpace,
            RenderSurfaceConfiguration configuration,
            out bool changed,
            out RenderSurfaceFailureReason failureReason)
        {
            changed = false;
            failureReason = RenderSurfaceFailureReason.None;
            if (TryEnsureAtlas(candidate, colorSpace, configuration, out changed))
            {
                return true;
            }
            ReleaseAtlas();
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
            if (changed)
            {
                hasCompletedOutput = false;
            }
            return true;
        }

        private bool TryEnsureAtlas(RenderSurfaceCandidate candidate,
            ColorSpace colorSpace,
            RenderSurfaceConfiguration configuration,
            out bool changed)
        {
            changed = false;
            if (!AllowAtlas || !configuration.EnableAtlas)
            {
                return false;
            }
            var epoch = UnityAutoRenderTextureSurface.ReadCurrentDeviceEpoch();
            if (atlasLease != null && atlasLease.Matches(candidate, colorSpace, epoch, configuration))
            {
                return true;
            }
            if (!RenderTextureAtlas.TryAllocate(candidate, colorSpace, configuration, epoch, out var next))
            {
                return false;
            }
            ReleaseAtlas();
            if (surface != null)
            {
                surface.RenderEventCompleted -= HandleRenderEventCompleted;
                surface.Dispose();
                surface = null;
            }
            atlasLease = next;
            atlasLease!.Completed += HandleRenderEventCompleted;
            hasCompletedOutput = false;
            changed = true;
            return true;
        }

        private void ReleaseAtlas()
        {
            if (atlasLease == null)
            {
                return;
            }
            atlasLease.Completed -= HandleRenderEventCompleted;
            atlasLease.Dispose();
            atlasLease = null;
            hasCompletedOutput = false;
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
            hasCompletedOutput = false;
            previous?.Dispose();
        }

        private void HandleRenderEventCompleted(UnitySkiaRenderTextureSurface.RenderSubmissionStatus status)
        {
            if (status == UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn)
            {
                hasCompletedOutput = true;
            }

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

        private bool MatchesScreenSpaceRequest(Vector2Int logicalSize,
            float scale,
            ColorSpace colorSpace,
            UnitySkiaGraphicsBackend backend,
            ulong deviceEpoch,
            ulong configurationFingerprint)
        {
            return hasScreenSpaceRequest &&
                   screenSpaceLogicalSize == logicalSize &&
                   screenSpaceScale == scale &&
                   screenSpaceColorSpace == colorSpace &&
                   screenSpaceBackend == backend &&
                   screenSpaceDeviceEpoch == deviceEpoch &&
                   screenSpaceConfigurationFingerprint == configurationFingerprint &&
                   HasLiveOutput;
        }

        private void RememberScreenSpaceRequest(Vector2Int logicalSize,
            float scale,
            ColorSpace colorSpace,
            UnitySkiaGraphicsBackend backend,
            ulong deviceEpoch,
            ulong configurationFingerprint)
        {
            screenSpaceLogicalSize = logicalSize;
            screenSpaceScale = scale;
            screenSpaceColorSpace = colorSpace;
            screenSpaceBackend = backend;
            screenSpaceDeviceEpoch = deviceEpoch;
            screenSpaceConfigurationFingerprint = configurationFingerprint;
            hasScreenSpaceRequest = true;
        }

        private bool HasLiveOutput => atlasLease != null
            ? atlasLease.IsLive && atlasLease.Texture != null
            : surface != null && surface.Texture != null;

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

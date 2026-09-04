using System;
using System.Collections.Generic;
using Milestro.Configuration;

namespace Milestro.Skia
{
    internal enum RenderSurfaceFailureReason
    {
        None = 0,
        InvalidRequest = 1,
        InvalidRuntimeCaps = 2,
        InvalidConfiguration = 3,
        EdgeLimit = 4,
        PixelBudget = 5,
        ByteBudget = 6,
        TransitionBudget = 7,
        GlobalBudget = 8,
        Allocation = 9,
        TextureCreation = 10,
        TextureValidation = 11,
        NativeHandle = 12,
        DeviceEpoch = 13,
        RetryExhausted = 14
    }

    internal readonly struct RenderSurfaceRuntimeCaps
    {
        internal RenderSurfaceRuntimeCaps(int unityMaxTextureEdge,
            int backendMaxTextureEdge)
        {
            UnityMaxTextureEdge = unityMaxTextureEdge;
            BackendMaxTextureEdge = backendMaxTextureEdge;
        }

        internal int UnityMaxTextureEdge { get; }
        internal int BackendMaxTextureEdge { get; }
    }

    internal readonly struct RenderSurfaceRasterRequest
    {
        internal RenderSurfaceRasterRequest(int logicalWidth, int logicalHeight, float desiredScale)
        {
            LogicalWidth = logicalWidth;
            LogicalHeight = logicalHeight;
            DesiredScale = desiredScale;
        }

        internal int LogicalWidth { get; }
        internal int LogicalHeight { get; }
        internal float DesiredScale { get; }
    }

    internal readonly struct RenderSurfaceDescriptor
    {
        internal RenderSurfaceDescriptor(UnitySkiaGraphicsBackend backend,
            UnitySkiaRenderTextureDescriptor textureDescriptor,
            int mipLevelCount)
        {
            Backend = backend;
            TextureDescriptor = textureDescriptor;
            MipLevelCount = mipLevelCount;
        }

        internal UnitySkiaGraphicsBackend Backend { get; }
        internal UnitySkiaRenderTextureDescriptor TextureDescriptor { get; }
        internal int MipLevelCount { get; }
    }

    internal readonly struct RenderSurfaceCandidate
    {
        internal RenderSurfaceCandidate(int logicalWidth,
            int logicalHeight,
            int rasterWidth,
            int rasterHeight,
            float effectiveScale,
            long pixelCount,
            long byteCount)
        {
            LogicalWidth = logicalWidth;
            LogicalHeight = logicalHeight;
            RasterWidth = rasterWidth;
            RasterHeight = rasterHeight;
            EffectiveScale = effectiveScale;
            PixelCount = pixelCount;
            ByteCount = byteCount;
        }

        internal int LogicalWidth { get; }
        internal int LogicalHeight { get; }
        internal int RasterWidth { get; }
        internal int RasterHeight { get; }
        internal float EffectiveScale { get; }
        internal long PixelCount { get; }
        internal long ByteCount { get; }
    }

    internal sealed class RenderSurfaceCandidatePlan
    {
        internal RenderSurfaceCandidatePlan(float requestedScale,
            float clampedScale,
            IReadOnlyList<RenderSurfaceCandidate> candidates)
        {
            RequestedScale = requestedScale;
            ClampedScale = clampedScale;
            Candidates = candidates;
        }

        internal float RequestedScale { get; }
        internal float ClampedScale { get; }
        internal IReadOnlyList<RenderSurfaceCandidate> Candidates { get; }
    }

    internal static class RenderSurfacePolicy
    {
        private const double ScaleEpsilon = 0.000001d;

        internal static bool TryBuildCandidatePlan(RenderSurfaceRasterRequest request,
            RenderSurfaceRuntimeCaps runtimeCaps,
            RenderSurfaceConfiguration configuration,
            RenderSurfaceDescriptor descriptor,
            out RenderSurfaceCandidatePlan plan,
            out RenderSurfaceFailureReason failureReason)
        {
            plan = null!;
            failureReason = RenderSurfaceFailureReason.None;
            if (request.LogicalWidth <= 0 || request.LogicalHeight <= 0 || !IsFinitePositive(request.DesiredScale))
            {
                failureReason = RenderSurfaceFailureReason.InvalidRequest;
                return false;
            }

            if (runtimeCaps.UnityMaxTextureEdge <= 0 || runtimeCaps.BackendMaxTextureEdge <= 0)
            {
                failureReason = RenderSurfaceFailureReason.InvalidRuntimeCaps;
                return false;
            }

            if (!ConfigurationIsValid(configuration))
            {
                failureReason = RenderSurfaceFailureReason.InvalidConfiguration;
                return false;
            }

            if (!RenderSurfaceDescriptorAccounting.TryResolveBytesPerPixel(descriptor, out var bytesPerPixel))
            {
                failureReason = RenderSurfaceFailureReason.InvalidConfiguration;
                return false;
            }

            var maxEdge = Math.Min(configuration.ConservativeMaxTextureEdge,
                Math.Min(runtimeCaps.UnityMaxTextureEdge, runtimeCaps.BackendMaxTextureEdge));
            if (maxEdge <= 0)
            {
                failureReason = RenderSurfaceFailureReason.EdgeLimit;
                return false;
            }

            if (!TryMultiply(request.LogicalWidth, request.LogicalHeight, out var logicalPixels))
            {
                failureReason = RenderSurfaceFailureReason.InvalidRequest;
                return false;
            }

            var maximumPixelCount = Math.Min(configuration.MaxPixelsPerSurface,
                configuration.MaxBytesPerSurface / bytesPerPixel);
            if (maximumPixelCount <= 0)
            {
                failureReason = RenderSurfaceFailureReason.ByteBudget;
                return false;
            }

            var edgeScale = Math.Min((double)maxEdge / request.LogicalWidth,
                (double)maxEdge / request.LogicalHeight);
            var pixelScale = Math.Sqrt((double)maximumPixelCount / logicalPixels);
            var clampedScale = Math.Min(request.DesiredScale,
                Math.Min(configuration.MaxScreenSpaceRasterScale, Math.Min(edgeScale, pixelScale)));
            if (!IsFinitePositive(clampedScale) || clampedScale + ScaleEpsilon < configuration.MinimumFallbackScale)
            {
                failureReason = RenderSurfaceFailureReason.PixelBudget;
                return false;
            }

            if (!TryFindLargestCandidate(request,
                    configuration.MinimumFallbackScale,
                    clampedScale,
                    maxEdge,
                    maximumPixelCount,
                    bytesPerPixel,
                    out var primaryCandidate))
            {
                failureReason = RenderSurfaceFailureReason.PixelBudget;
                return false;
            }

            var candidates = new List<RenderSurfaceCandidate>(configuration.MaxAttemptsPerRequestAndEpoch)
            {
                primaryCandidate
            };

            var nextScale = Math.Floor((primaryCandidate.EffectiveScale - ScaleEpsilon) /
                                       configuration.ScaleQuantum) * configuration.ScaleQuantum;
            while (candidates.Count < configuration.MaxAttemptsPerRequestAndEpoch &&
                   nextScale + ScaleEpsilon >= configuration.MinimumFallbackScale)
            {
                TryAppendCandidate(candidates,
                    request,
                    nextScale,
                    maxEdge,
                    maximumPixelCount,
                    bytesPerPixel);
                nextScale -= configuration.ScaleQuantum;
            }

            plan = new RenderSurfaceCandidatePlan(request.DesiredScale,
                primaryCandidate.EffectiveScale,
                candidates);
            return true;
        }

        internal static bool TryQuantizeDesiredScale(float desiredScale,
            float quantum,
            float maximumScale,
            out float quantizedScale)
        {
            return TryQuantizeDesiredScale(desiredScale,
                quantum,
                0f,
                maximumScale,
                0f,
                out quantizedScale);
        }

        internal static bool TryQuantizeDesiredScale(float desiredScale,
            float quantum,
            float hysteresis,
            float maximumScale,
            float previousScale,
            out float quantizedScale)
        {
            quantizedScale = 0f;
            if (!IsFinitePositive(desiredScale) ||
                !IsFinitePositive(quantum) ||
                !IsFiniteNonNegative(hysteresis) ||
                !IsFinitePositive(maximumScale) ||
                (previousScale != 0f && !IsFinitePositive(previousScale)))
            {
                return false;
            }

            var clamped = Math.Min(desiredScale, maximumScale);
            if (previousScale > 0f)
            {
                var stableScale = Math.Min(previousScale, maximumScale);
                var lowerBoundary = Math.Max(0d, stableScale - quantum - hysteresis);
                var upperBoundary = Math.Min(maximumScale, stableScale + hysteresis);
                if (clamped + ScaleEpsilon >= lowerBoundary && clamped <= upperBoundary + ScaleEpsilon)
                {
                    quantizedScale = stableScale;
                    return true;
                }
            }

            var quantized = Math.Ceiling(clamped / quantum) * quantum;
            if (!IsFinitePositive(quantized))
            {
                return false;
            }

            quantizedScale = (float)Math.Min(quantized, maximumScale);
            return IsFinitePositive(quantizedScale);
        }

        private static bool TryFindLargestCandidate(RenderSurfaceRasterRequest request,
            float minimumScale,
            double maximumScale,
            int maxEdge,
            long maximumPixelCount,
            int bytesPerPixel,
            out RenderSurfaceCandidate candidate)
        {
            candidate = default;
            if (!IsFinitePositive(minimumScale) || !IsFinitePositive(maximumScale))
            {
                return false;
            }

            var upperScale = (float)maximumScale;
            while ((double)upperScale > maximumScale && upperScale > 0f)
            {
                upperScale = PreviousPositiveFloat(upperScale);
            }

            if (!IsFinitePositive(upperScale) || upperScale + ScaleEpsilon < minimumScale)
            {
                return false;
            }

            if (!TryCreateCandidate(request,
                    minimumScale,
                    maxEdge,
                    maximumPixelCount,
                    bytesPerPixel,
                    out candidate))
            {
                return false;
            }

            var lowBits = BitConverter.SingleToInt32Bits(minimumScale);
            var highBits = BitConverter.SingleToInt32Bits(upperScale);
            while (lowBits <= highBits)
            {
                var middleBits = (int)((long)lowBits + ((long)highBits - lowBits) / 2L);
                var middleScale = BitConverter.Int32BitsToSingle(middleBits);
                if (TryCreateCandidate(request,
                        middleScale,
                        maxEdge,
                        maximumPixelCount,
                        bytesPerPixel,
                        out var middleCandidate))
                {
                    candidate = middleCandidate;
                    lowBits = middleBits + 1;
                }
                else
                {
                    highBits = middleBits - 1;
                }
            }

            return true;
        }

        private static float PreviousPositiveFloat(float value)
        {
            var bits = BitConverter.SingleToInt32Bits(value);
            return bits > 0 ? BitConverter.Int32BitsToSingle(bits - 1) : 0f;
        }

        private static void TryAppendCandidate(List<RenderSurfaceCandidate> candidates,
            RenderSurfaceRasterRequest request,
            double scale,
            int maxEdge,
            long maximumPixelCount,
            int bytesPerPixel)
        {
            if (!TryCreateCandidate(request,
                    scale,
                    maxEdge,
                    maximumPixelCount,
                    bytesPerPixel,
                    out var candidate))
            {
                return;
            }

            if (candidates.Count != 0)
            {
                var previous = candidates[candidates.Count - 1];
                if (previous.RasterWidth == candidate.RasterWidth &&
                    previous.RasterHeight == candidate.RasterHeight)
                {
                    return;
                }
            }

            candidates.Add(candidate);
        }

        private static bool TryCreateCandidate(RenderSurfaceRasterRequest request,
            double scale,
            int maxEdge,
            long maximumPixelCount,
            int bytesPerPixel,
            out RenderSurfaceCandidate candidate)
        {
            candidate = default;
            if (!IsFinitePositive(scale) ||
                !TryCeilProduct(request.LogicalWidth, scale, out var width) ||
                !TryCeilProduct(request.LogicalHeight, scale, out var height) ||
                width > maxEdge ||
                height > maxEdge ||
                !TryMultiply(width, height, out var pixels) ||
                pixels > maximumPixelCount ||
                !TryMultiply(pixels, bytesPerPixel, out var bytes))
            {
                return false;
            }

            candidate = new RenderSurfaceCandidate(request.LogicalWidth,
                request.LogicalHeight,
                width,
                height,
                (float)scale,
                pixels,
                bytes);
            return true;
        }

        private static bool ConfigurationIsValid(RenderSurfaceConfiguration configuration)
        {
            return configuration != null &&
                   IsFinitePositive(configuration.MaxScreenSpaceRasterScale) &&
                   IsFinitePositive(configuration.MinimumFallbackScale) &&
                   IsFinitePositive(configuration.ScaleQuantum) &&
                   IsFiniteNonNegative(configuration.ScaleHysteresis) &&
                   configuration.ScaleHysteresis < configuration.ScaleQuantum &&
                   configuration.MinimumFallbackScale <= configuration.MaxScreenSpaceRasterScale &&
                   configuration.ConservativeMaxTextureEdge > 0 &&
                   configuration.MaxPixelsPerSurface > 0 &&
                   configuration.MaxBytesPerSurface > 0 &&
                   configuration.MaxGlobalBytes > 0 &&
                   configuration.MaxTransitionBytes > 0 &&
                   configuration.MaxAttemptsPerRequestAndEpoch > 0 &&
                   (configuration.VulkanBackend == UnitySkiaVulkanBackend.Direct ||
                    configuration.VulkanBackend == UnitySkiaVulkanBackend.StagingCopy);
        }

        private static bool TryCeilProduct(int value, double scale, out int result)
        {
            result = 0;
            var product = value * scale;
            if (!IsFinitePositive(product) || product > int.MaxValue)
            {
                return false;
            }

            var ceiling = Math.Ceiling(product);
            if (ceiling < 1d || ceiling > int.MaxValue)
            {
                return false;
            }

            result = (int)ceiling;
            return true;
        }

        private static bool TryMultiply(long left, long right, out long product)
        {
            product = 0;
            if (left <= 0 || right <= 0 || left > long.MaxValue / right)
            {
                return false;
            }

            product = left * right;
            return true;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }

    internal static class RenderSurfaceDescriptorAccounting
    {
        internal static bool TryResolveBytesPerPixel(RenderSurfaceDescriptor descriptor,
            out int bytesPerPixel)
        {
            bytesPerPixel = 0;
            if (!IsSupportedBackend(descriptor.Backend) ||
                descriptor.MipLevelCount != 1 ||
                descriptor.TextureDescriptor.MsaaSamples != 1 ||
                descriptor.TextureDescriptor.ResolveStrategy != UnitySkiaRenderTextureResolveStrategy.None)
            {
                return false;
            }

            switch (descriptor.TextureDescriptor.PreferredFormat)
            {
                case UnitySkiaRenderTextureFormat.Auto:
                case UnitySkiaRenderTextureFormat.Bgra32:
                case UnitySkiaRenderTextureFormat.Rgba32:
                    bytesPerPixel = 4;
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsSupportedBackend(UnitySkiaGraphicsBackend backend)
        {
            switch (backend)
            {
                case UnitySkiaGraphicsBackend.Metal:
                case UnitySkiaGraphicsBackend.Direct3D12:
                case UnitySkiaGraphicsBackend.Vulkan:
                case UnitySkiaGraphicsBackend.OpenGL:
                case UnitySkiaGraphicsBackend.OpenGLES:
                    return true;
                default:
                    return false;
            }
        }
    }
}

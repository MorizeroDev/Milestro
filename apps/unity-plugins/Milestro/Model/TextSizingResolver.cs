using System;

namespace Milestro.Model
{
    /// <summary>
    /// Resolved min/preferred width at the design font size. The provisional width is the
    /// width used by later font-size fitting before Unity assigns the final width.
    /// </summary>
    public readonly struct TextPreferredWidthResolution
    {
        public TextPreferredWidthResolution(float designFontSize,
            float minWidth,
            float preferredWidth,
            float provisionalWidth,
            ResolvedMargin margin,
            TextIntrinsicMetrics metrics)
        {
            DesignFontSize = designFontSize;
            MinWidth = minWidth;
            PreferredWidth = preferredWidth;
            ProvisionalWidth = provisionalWidth;
            Margin = margin;
            Metrics = metrics;
        }

        public float DesignFontSize { get; }
        public float MinWidth { get; }
        public float PreferredWidth { get; }
        public float ProvisionalWidth { get; }
        public ResolvedMargin Margin { get; }
        public TextIntrinsicMetrics Metrics { get; }
    }

    /// <summary>
    /// Resolved effective font size and its intrinsic metrics. Content-sized axes are outputs,
    /// so they never participate as Auto Font Size growth targets.
    /// </summary>
    public readonly struct TextFontSizeResolution
    {
        public TextFontSizeResolution(float effectiveFontSize,
            ResolvedMargin margin,
            TextIntrinsicMetrics metrics)
        {
            EffectiveFontSize = effectiveFontSize;
            Margin = margin;
            Metrics = metrics;
        }

        public float EffectiveFontSize { get; }
        public ResolvedMargin Margin { get; }
        public TextIntrinsicMetrics Metrics { get; }
    }

    public static class TextSizingResolver
    {
        private const int MaxFitIterations = 32;
        private const float FitTolerance = 0.001f;

        /// <summary>
        /// Resolves CSS-like min-content, max-content and fit-content width at design size.
        /// </summary>
        public static TextPreferredWidthResolution ResolvePreferredWidth(
            ITextIntrinsicMeasurement measurement,
            TextSizingRequest request,
            Func<float, ResolvedMargin> resolveMargin)
        {
            if (measurement == null)
            {
                throw new ArgumentNullException(nameof(measurement));
            }

            if (resolveMargin == null)
            {
                throw new ArgumentNullException(nameof(resolveMargin));
            }

            var designFontSize = Positive(request.DesignFontSize);
            var margin = resolveMargin(designFontSize);
            var metrics = measurement.Measure(designFontSize, float.PositiveInfinity);
            var width = ResolveWidth(request, metrics, margin);
            return new TextPreferredWidthResolution(designFontSize,
                width.MinWidth,
                width.PreferredWidth,
                width.ProvisionalWidth,
                margin,
                metrics);
        }

        /// <summary>
        /// Resolves Auto Font Size. Fully fixed axes retain the existing grow/shrink behavior.
        /// Any content-sized axis caps the search at design size, making fitting shrink-only.
        /// </summary>
        public static TextFontSizeResolution ResolveEffectiveFontSize(
            ITextIntrinsicMeasurement measurement,
            TextSizingRequest request,
            Func<float, ResolvedMargin> resolveMargin,
            TextPreferredWidthResolution preferredWidth)
        {
            if (measurement == null)
            {
                throw new ArgumentNullException(nameof(measurement));
            }

            if (resolveMargin == null)
            {
                throw new ArgumentNullException(nameof(resolveMargin));
            }

            var designFontSize = Positive(request.DesignFontSize);
            var effectiveFontSize = designFontSize;
            if (request.AutoFontSize)
            {
                effectiveFontSize = ResolveAutoFontSize(measurement,
                    request,
                    resolveMargin,
                    preferredWidth,
                    designFontSize);
            }

            var margin = resolveMargin(effectiveFontSize);
            var metrics = measurement.Measure(effectiveFontSize, float.PositiveInfinity);
            return new TextFontSizeResolution(effectiveFontSize, margin, metrics);
        }

        /// <summary>
        /// Resolves the final used size at the width Unity assigned. Auto height and the renderer
        /// both use this same final width and wrap decision.
        /// </summary>
        public static TextSizingResult ResolveLayoutAtAssignedWidth(
            ITextIntrinsicMeasurement measurement,
            TextSizingRequest request,
            TextFontSizeResolution fontSizeResolution,
            float assignedWidth)
        {
            if (measurement == null)
            {
                throw new ArgumentNullException(nameof(measurement));
            }

            var margin = fontSizeResolution.Margin;
            var width = ResolveWidth(request,
                fontSizeResolution.Metrics,
                margin);
            var usedWidth = IsFinite(assignedWidth)
                ? NonNegative(assignedWidth)
                : width.PreferredWidth;
            var contentWidth = Math.Max(0f, usedWidth - margin.FixedHorizontalSize);
            var metrics = measurement.Measure(fontSizeResolution.EffectiveFontSize,
                request.Wrap ? contentWidth : float.PositiveInfinity);
            var usedHeight = ResolveHeight(request, metrics, margin);
            return new TextSizingResult(request,
                usedWidth,
                usedHeight,
                fontSizeResolution.EffectiveFontSize,
                margin,
                metrics,
                width.MinWidth,
                width.PreferredWidth);
        }

        public static TextSizingResult Resolve(
            ITextIntrinsicMeasurement measurement,
            TextSizingRequest request,
            Func<float, ResolvedMargin> resolveMargin,
            float? assignedWidth = null)
        {
            if (request.WidthMode == TextWidthMode.Fixed &&
                request.HeightMode == TextHeightMode.Fixed &&
                !request.AutoFontSize)
            {
                var designFontSize = Positive(request.DesignFontSize);
                return new TextSizingResult(request,
                    NonNegative(request.FixedWidth),
                    NonNegative(request.FixedHeight),
                    designFontSize,
                    resolveMargin(designFontSize),
                    default,
                    0f,
                    0f);
            }

            var width = ResolvePreferredWidth(measurement, request, resolveMargin);
            var fontSize = ResolveEffectiveFontSize(measurement,
                request,
                resolveMargin,
                width);
            return ResolveLayoutAtAssignedWidth(measurement,
                request,
                fontSize,
                assignedWidth ?? width.ProvisionalWidth);
        }

        private static float ResolveAutoFontSize(ITextIntrinsicMeasurement measurement,
            TextSizingRequest request,
            Func<float, ResolvedMargin> resolveMargin,
            TextPreferredWidthResolution preferredWidth,
            float designFontSize)
        {
            var hasContentSizing = request.HasContentSizing;
            var widthTarget = ResolveWidthTarget(request);
            var heightTarget = ResolveHeightTarget(request);
            var fitWidth = IsFinite(widthTarget);
            var fitHeight = IsFinite(heightTarget);
            if ((!fitWidth && !fitHeight) ||
                (hasContentSizing && Fits(designFontSize,
                    measurement,
                    request,
                    resolveMargin,
                    preferredWidth,
                    widthTarget,
                    heightTarget)))
            {
                return designFontSize;
            }

            var minSize = Math.Max(0.1f, NonNegative(request.MinFontSize));
            var maxSize = Math.Max(minSize, NonNegative(request.MaxFontSize));
            if (hasContentSizing)
            {
                maxSize = Math.Min(maxSize, designFontSize);
                minSize = Math.Min(minSize, maxSize);
            }

            if (Fits(maxSize,
                    measurement,
                    request,
                    resolveMargin,
                    preferredWidth,
                    widthTarget,
                    heightTarget))
            {
                return maxSize;
            }

            if (!Fits(minSize,
                    measurement,
                    request,
                    resolveMargin,
                    preferredWidth,
                    widthTarget,
                    heightTarget))
            {
                // Existing overflow policy takes over below the configured minimum.
                return minSize;
            }

            var low = minSize;
            var high = maxSize;
            for (var iteration = 0;
                 iteration < MaxFitIterations && high - low > FitTolerance;
                 ++iteration)
            {
                var candidate = (low + high) * 0.5f;
                if (Fits(candidate,
                        measurement,
                        request,
                        resolveMargin,
                        preferredWidth,
                        widthTarget,
                        heightTarget))
                {
                    low = candidate;
                }
                else
                {
                    high = candidate;
                }
            }

            return low;
        }

        private static bool Fits(float fontSize,
            ITextIntrinsicMeasurement measurement,
            TextSizingRequest request,
            Func<float, ResolvedMargin> resolveMargin,
            TextPreferredWidthResolution designWidth,
            float widthTarget,
            float heightTarget)
        {
            var margin = resolveMargin(fontSize);
            var candidateWidth = ResolveWidth(request,
                measurement.Measure(fontSize, float.PositiveInfinity),
                margin);
            var contentProbeWidth = candidateWidth.ProvisionalWidth - margin.FixedHorizontalSize;
            if (IsFinite(widthTarget))
            {
                contentProbeWidth = Math.Min(contentProbeWidth,
                    Math.Max(0f, widthTarget - margin.FixedHorizontalSize));
            }

            if (!IsFinite(contentProbeWidth))
            {
                contentProbeWidth = Math.Max(0f,
                    designWidth.ProvisionalWidth - margin.FixedHorizontalSize);
            }

            var metrics = measurement.Measure(fontSize,
                request.Wrap ? Math.Max(0.01f, contentProbeWidth) : float.PositiveInfinity);
            var widthFits = !IsFinite(widthTarget) ||
                            (request.Wrap
                                ? metrics.MinContentWidth + margin.FixedHorizontalSize <= widthTarget + FitTolerance
                                : metrics.ContentWidth + margin.FixedHorizontalSize <= widthTarget + FitTolerance);
            var heightFits = !IsFinite(heightTarget) ||
                             metrics.ContentHeight + margin.FixedVerticalSize <= heightTarget + FitTolerance;
            return widthFits && heightFits;
        }

        private static TextWidthValue ResolveWidth(TextSizingRequest request,
            TextIntrinsicMetrics metrics,
            ResolvedMargin margin)
        {
            var minContentWidth = Math.Max(0f, metrics.MinContentWidth + margin.FixedHorizontalSize);
            if (request.WidthMode == TextWidthMode.Fixed)
            {
                var fixedWidth = NonNegative(request.FixedWidth);
                return new TextWidthValue(minContentWidth,
                    -1f,
                    fixedWidth);
            }

            var maxContentWidth = Math.Max(0f, metrics.MaxContentWidth + margin.FixedHorizontalSize);
            var preferredWidth = maxContentWidth;
            if (request.WidthMode == TextWidthMode.FitContent)
            {
                var containingContentWidth = float.IsPositiveInfinity(request.AvailableWidth)
                    ? float.PositiveInfinity
                    : Math.Max(0f, request.AvailableWidth - margin.FixedHorizontalSize);
                var contentWidth = Math.Max(metrics.MinContentWidth,
                    Math.Min(metrics.MaxContentWidth, containingContentWidth));
                preferredWidth = Math.Max(0f, contentWidth + margin.FixedHorizontalSize);
            }

            return new TextWidthValue(minContentWidth, preferredWidth, preferredWidth);
        }

        private static float ResolveHeight(TextSizingRequest request,
            TextIntrinsicMetrics metrics,
            ResolvedMargin margin)
        {
            if (request.HeightMode == TextHeightMode.Fixed)
            {
                return NonNegative(request.FixedHeight);
            }

            var autoHeight = Math.Max(0f, metrics.ContentHeight + margin.FixedVerticalSize);
            return IsFinite(request.HeightCap)
                ? Math.Min(autoHeight, NonNegative(request.HeightCap))
                : autoHeight;
        }

        private static float ResolveWidthTarget(TextSizingRequest request)
        {
            return request.WidthMode == TextWidthMode.Fixed
                ? NonNegative(request.FixedWidth)
                : request.WidthCap;
        }

        private static float ResolveHeightTarget(TextSizingRequest request)
        {
            return request.HeightMode == TextHeightMode.Fixed
                ? NonNegative(request.FixedHeight)
                : request.HeightCap;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float NonNegative(float value)
        {
            return IsFinite(value) ? Math.Max(0f, value) : 0f;
        }

        private static float Positive(float value)
        {
            return Math.Max(0.1f, NonNegative(value));
        }

        private readonly struct TextWidthValue
        {
            public TextWidthValue(float minWidth, float preferredWidth, float provisionalWidth)
            {
                MinWidth = minWidth;
                PreferredWidth = preferredWidth;
                ProvisionalWidth = provisionalWidth;
            }

            public float MinWidth { get; }
            public float PreferredWidth { get; }
            public float ProvisionalWidth { get; }
        }
    }
}

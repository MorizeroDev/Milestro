using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Components.Internal
{
    internal static class TextSizingBounds
    {
        /// <summary>
        /// Returns the nearest stable ancestor size. Ancestors whose width or height is fitted
        /// from their own content are skipped so that content sizing cannot feed back into
        /// itself. LayoutGroup padding is removed from the constraining ancestor.
        /// </summary>
        public static Vector2 ExternalAvailableSize(RectTransform rect)
        {
            var current = rect != null ? rect.parent as RectTransform : null;
            var result = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var hasWidth = false;
            var hasHeight = false;
            while (current != null)
            {
                var fitter = current.GetComponent<ContentSizeFitter>();
                var widthContentSized = fitter != null &&
                                        fitter.isActiveAndEnabled &&
                                        fitter.horizontalFit != ContentSizeFitter.FitMode.Unconstrained;
                var heightContentSized = fitter != null &&
                                         fitter.isActiveAndEnabled &&
                                         fitter.verticalFit != ContentSizeFitter.FitMode.Unconstrained;

                if (!widthContentSized && !hasWidth)
                {
                    var width = current.rect.width;
                    if (!float.IsNaN(width) && !float.IsInfinity(width))
                    {
                        result.x = Mathf.Max(0f, width);
                        hasWidth = true;
                    }
                }

                if (!heightContentSized && !hasHeight)
                {
                    var height = current.rect.height;
                    if (!float.IsNaN(height) && !float.IsInfinity(height))
                    {
                        result.y = Mathf.Max(0f, height);
                        hasHeight = true;
                    }
                }

                if (hasWidth && hasHeight)
                {
                    break;
                }

                current = current.parent as RectTransform;
            }

            // Padding belongs to the layout group that owns the child. It is irrelevant when
            // the nearest ancestor on that axis is itself content-sized.
            var parent = rect != null ? rect.parent as RectTransform : null;
            if (parent != null)
            {
                var fitter = parent.GetComponent<ContentSizeFitter>();
                var group = parent.GetComponent<LayoutGroup>();
                if (group != null && group.isActiveAndEnabled)
                {
                    if ((fitter == null || !fitter.isActiveAndEnabled ||
                         fitter.horizontalFit == ContentSizeFitter.FitMode.Unconstrained))
                    {
                        result.x = Mathf.Max(0f, result.x - group.padding.horizontal);
                    }

                    if ((fitter == null || !fitter.isActiveAndEnabled ||
                         fitter.verticalFit == ContentSizeFitter.FitMode.Unconstrained))
                    {
                        result.y = Mathf.Max(0f, result.y - group.padding.vertical);
                    }
                }
            }

            return result;
        }

        public static float AvailableWidth(RectTransform rect)
        {
            return ExternalAvailableSize(rect).x;
        }
    }
}

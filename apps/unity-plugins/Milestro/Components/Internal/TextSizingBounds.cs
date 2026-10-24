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
            RectTransform? widthSource = null;
            RectTransform? heightSource = null;
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
                        widthSource = current;
                    }
                }

                if (!heightContentSized && !hasHeight)
                {
                    var height = current.rect.height;
                    if (!float.IsNaN(height) && !float.IsInfinity(height))
                    {
                        result.y = Mathf.Max(0f, height);
                        hasHeight = true;
                        heightSource = current;
                    }
                }

                if (hasWidth && hasHeight)
                {
                    break;
                }

                current = current.parent as RectTransform;
            }

            // Padding belongs to the layout group that owns the constraining ancestor. Width
            // and height can be constrained by different ancestors, so resolve each axis
            // independently. This also handles a content-sized parent between the child and
            // the stable layout group.
            result.x = SubtractLayoutPadding(result.x, widthSource, horizontal: true);
            result.y = SubtractLayoutPadding(result.y, heightSource, horizontal: false);

            return result;
        }

        public static float AvailableWidth(RectTransform rect)
        {
            return ExternalAvailableSize(rect).x;
        }

        private static float SubtractLayoutPadding(float size, RectTransform? source, bool horizontal)
        {
            var group = source != null ? source.GetComponent<LayoutGroup>() : null;
            if (group == null || !group.isActiveAndEnabled)
            {
                return size;
            }

            var padding = horizontal ? group.padding.horizontal : group.padding.vertical;
            return Mathf.Max(0f, size - padding);
        }
    }
}

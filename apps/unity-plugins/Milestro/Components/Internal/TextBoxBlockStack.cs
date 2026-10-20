
using System;
using System.Collections.Generic;
using Milestro.Util;

namespace Milestro.Components.Internal
{
    /// <summary>
    /// Pure vertical stacking math for multi-paragraph TextBox content. Given per-block
    /// heights and margins, produces the top offset of every block and the total content
    /// height. Follows CSS block layout: adjacent vertical margins collapse to the larger
    /// one, the component-level paragraph spacing participates as a default gap, and the
    /// first/last block margins apply at the content edges (no collapsing with the
    /// container, mirroring a block formatting context).
    /// </summary>
    internal static class TextBoxBlockStack
    {
        /// <summary>
        /// Lays out blocks vertically. All inputs are in the same (unscaled) unit space.
        /// </summary>
        /// <param name="heights">Block heights.</param>
        /// <param name="marginTops">Resolved top margins, one per block.</param>
        /// <param name="marginBottoms">Resolved bottom margins, one per block.</param>
        /// <param name="paragraphSpacing">Extra gap inserted between adjacent blocks.</param>
        /// <param name="tops">Output list receiving each block's top offset. Cleared first.</param>
        /// <returns>Total stacked content height.</returns>
        internal static float Layout(IReadOnlyList<float> heights,
            IReadOnlyList<float> marginTops,
            IReadOnlyList<float> marginBottoms,
            float paragraphSpacing,
            List<float> tops)
        {
            tops.Clear();
            var count = heights.Count;
            if (count == 0)
            {
                return 0f;
            }

            if (!FloatUtil.IsFinite(paragraphSpacing) || paragraphSpacing < 0f)
            {
                paragraphSpacing = 0f;
            }

            var y = SanitizeMargin(marginTops[0]);
            for (var index = 0; index < count; ++index)
            {
                tops.Add(y);
                var height = FloatUtil.IsFinite(heights[index]) && heights[index] > 0f
                    ? heights[index]
                    : 0f;
                y += height;
                if (index + 1 < count)
                {
                    y += paragraphSpacing + Collapse(marginBottoms[index], marginTops[index + 1]);
                }
                else
                {
                    y += SanitizeMargin(marginBottoms[index]);
                }
            }

            return y;
        }

        internal static float Collapse(float marginBottom, float marginTop)
        {
            return Math.Max(SanitizeMargin(marginBottom), SanitizeMargin(marginTop));
        }

        private static float SanitizeMargin(float value)
        {
            return FloatUtil.IsFinite(value) && value > 0f ? value : 0f;
        }
    }
}

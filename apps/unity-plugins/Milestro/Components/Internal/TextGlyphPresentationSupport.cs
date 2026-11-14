using UnityEngine.UI;

namespace Milestro.Components.Internal
{
    internal static class TextGlyphPresentationSupport
    {
        internal static bool HasSoftRectMask(MaskableGraphic graphic)
        {
            if (!graphic.maskable)
            {
                return false;
            }

            var ancestor = graphic.transform.parent;
            while (ancestor != null)
            {
                var mask = ancestor.GetComponent<RectMask2D>();
                if (mask != null && mask.isActiveAndEnabled &&
                    (mask.softness.x != 0 || mask.softness.y != 0))
                {
                    return true;
                }
                ancestor = ancestor.parent;
            }
            return false;
        }
    }
}

using UnityEngine;

namespace Milestro.Components
{
    public readonly struct TextBoxElementGeometry
    {
        internal TextBoxElementGeometry(Rect contentRect, Rect canvasRect)
        {
            ContentRect = contentRect;
            CanvasRect = canvasRect;
        }

        /// <summary>
        /// Bounding rect in unscrolled content coordinates. The origin is the content's top-left,
        /// the y axis points down, and the units match <see cref="TextBoxScrollState"/>.
        /// </summary>
        public Rect ContentRect { get; }

        /// <summary>
        /// Bounding rect in the TextBox RectTransform's local canvas coordinates. The y axis
        /// points up. The rect includes the current scroll offset and is not clipped to the viewport.
        /// </summary>
        public Rect CanvasRect { get; }
    }
}

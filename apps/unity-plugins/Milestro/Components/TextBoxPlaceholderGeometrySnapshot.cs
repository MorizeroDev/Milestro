using System.Collections.Generic;
using UnityEngine;

namespace Milestro.Components
{
    public readonly struct TextBoxPlaceholderGeometry
    {
        internal TextBoxPlaceholderGeometry(string key, int startUtf16, Rect rect, bool visible)
        {
            Key = key;
            StartUtf16 = startUtf16;
            Rect = rect;
            Visible = visible;
        }

        public string Key { get; }
        public int StartUtf16 { get; }
        public int EndUtf16 => StartUtf16 + 1;
        public Rect Rect { get; }
        public bool Visible { get; }
    }

    public readonly struct TextBoxPlaceholderGeometrySnapshot
    {
        internal TextBoxPlaceholderGeometrySnapshot(long contentVersion, long generation,
            IReadOnlyList<TextBoxPlaceholderGeometry> geometry, Vector2 paintPosition, Rect clipRect, float fitScale)
        {
            ContentVersion = contentVersion;
            Generation = generation;
            Geometry = geometry;
            PaintPosition = paintPosition;
            ClipRect = clipRect;
            FitScale = fitScale;
        }

        public long ContentVersion { get; }
        public long Generation { get; }
        public IReadOnlyList<TextBoxPlaceholderGeometry> Geometry { get; }
        public Vector2 PaintPosition { get; }
        public Rect ClipRect { get; }
        public float FitScale { get; }
    }
}

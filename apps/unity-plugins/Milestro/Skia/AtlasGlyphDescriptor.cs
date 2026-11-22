using UnityEngine;

namespace Milestro.Skia
{
    internal readonly struct AtlasGlyphDescriptor
    {
        internal readonly uint TypefaceId, GlyphId, FontFlags, PackedColor;
        internal readonly float FontSize, FontScaleX, FontSkewX, X, Y;

        internal AtlasGlyphDescriptor(GlyphFontDescriptor font, GlyphPlacement placement)
        {
            TypefaceId = font.TypefaceId;
            GlyphId = placement.GlyphId;
            FontFlags = font.FontFlags;
            PackedColor = font.PackedColor;
            FontSize = font.FontSize;
            FontScaleX = font.FontScaleX;
            FontSkewX = font.FontSkewX;
            X = placement.X;
            Y = placement.Y;
        }

        internal AtlasGlyphDescriptor(GlyphLayoutDescriptor glyph)
        {
            TypefaceId = glyph.TypefaceId;
            GlyphId = glyph.GlyphId;
            FontFlags = glyph.FontFlags;
            PackedColor = glyph.PackedColor;
            FontSize = glyph.FontSize;
            FontScaleX = glyph.FontScaleX;
            FontSkewX = glyph.FontSkewX;
            X = glyph.X;
            Y = glyph.Y;
        }

        internal Color32 Color => new Color32((byte)(PackedColor >> 16), (byte)(PackedColor >> 8),
            (byte)PackedColor, (byte)(PackedColor >> 24));
    }
}

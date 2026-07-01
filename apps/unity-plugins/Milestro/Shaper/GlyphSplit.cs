using System.Collections.Generic;
using Milestro.Model;
using Milestro.Skia.TextLayout;
using UnityEngine;

namespace Milestro.Shaper
{
    public class GlyphSplit
    {
        public SplittedGlyphInfo Info { get; }
        public Texture2D Texture { get; }
        public bool MoveTextureOwnership { get; }
        public List<SpriteInfo> Sprites { get; } = new List<SpriteInfo>();

        public GlyphSplit(SplittedGlyphInfo info, Texture2D texture, bool moveTextureOwnership)
        {
            Info = info;
            Texture = texture;
            MoveTextureOwnership = moveTextureOwnership;

            foreach (var boundItem in info.Bounds)
            {
                ProcessBoundItem(boundItem);
            }
        }

        private void ProcessBoundItem(Bound boundItem)
        {
            var width = boundItem.Right - boundItem.Left;
            var height = boundItem.Top - boundItem.Bottom;

            var rect = new Rect(boundItem.Left, boundItem.Bottom, width, height);

            if (IsRectOutsideTexture(rect))
            {
                Sprites.Add(new SpriteInfo { Sprite = null, Position = new Vector2(boundItem.Left, boundItem.Bottom) });
                return;
            }

            var clippedRect = GetClippedRect(rect);
            var sprite = Sprite.Create(Texture, clippedRect, new Vector2(0, 0));

            var texturePosition = new Vector2(
                Mathf.Max(boundItem.Left, 0),
                Mathf.Max(boundItem.Bottom, 0)
            );

            var position = new Vector2(boundItem.Left, boundItem.Bottom);

            Sprites.Add(new SpriteInfo { Sprite = sprite, TexturePosition = texturePosition, Position = position });
        }

        private bool IsRectOutsideTexture(Rect rect)
        {
            return rect.xMax <= 0 || rect.yMax <= 0 || rect.xMin >= Texture.width || rect.yMin >= Texture.height;
        }

        private Rect GetClippedRect(Rect rect)
        {
            var xMin = Mathf.Max(rect.xMin, 0);
            var yMin = Mathf.Max(rect.yMin, 0);
            var xMax = Mathf.Min(rect.xMax, Texture.width);
            var yMax = Mathf.Min(rect.yMax, Texture.height);

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        ~GlyphSplit()
        {
            if (MoveTextureOwnership)
            {
                UnityEngine.Object.DestroyImmediate(Texture);
            }
        }
    }
}

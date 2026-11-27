using System;
using UnityEngine;

namespace Milestro.ResourceManagement
{
    public readonly struct ImageResource
    {
        public ImageResource(Sprite sprite,
            InlineResourceLength? suggestedWidth = null, InlineResourceLength? suggestedHeight = null)
        {
            if (!sprite) throw new ArgumentNullException(nameof(sprite));
            if (suggestedWidth.HasValue && suggestedWidth.Value.Value <= 0f)
                throw new ArgumentOutOfRangeException(nameof(suggestedWidth));
            if (suggestedHeight.HasValue && suggestedHeight.Value.Value <= 0f)
                throw new ArgumentOutOfRangeException(nameof(suggestedHeight));
            Sprite = sprite;
            IntrinsicSize = sprite.rect.size;
            SuggestedWidth = suggestedWidth;
            SuggestedHeight = suggestedHeight;
        }

        public Sprite Sprite { get; }
        public Vector2 IntrinsicSize { get; }
        public InlineResourceLength? SuggestedWidth { get; }
        public InlineResourceLength? SuggestedHeight { get; }
    }
}

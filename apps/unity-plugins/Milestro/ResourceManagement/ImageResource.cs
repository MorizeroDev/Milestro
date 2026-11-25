using System;
using UnityEngine;

namespace Milestro.ResourceManagement
{
    public readonly struct ImageResource
    {
        public ImageResource(Sprite sprite, object? releaseToken = null)
        {
            if (!sprite) throw new ArgumentNullException(nameof(sprite));
            Sprite = sprite;
            IntrinsicSize = sprite.rect.size;
            ReleaseToken = releaseToken;
        }

        public Sprite Sprite { get; }
        public Vector2 IntrinsicSize { get; }
        public object? ReleaseToken { get; }
    }
}

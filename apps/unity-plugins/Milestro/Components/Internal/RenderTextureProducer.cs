using UnityEngine;

namespace Milestro.Components.Internal
{
    public abstract class RenderTextureProducer : MonoBehaviour
    {
        public abstract Texture? OutputTexture { get; }
        public abstract Rect OutputUvRect { get; }
        public abstract int OutputWidth { get; }
        public abstract int OutputHeight { get; }
        public abstract bool HasOutput { get; }
        public abstract long OutputVersion { get; }
    }
}

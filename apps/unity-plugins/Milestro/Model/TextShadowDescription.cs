using UnityEngine;

namespace Milestro.Model
{
    public class TextShadowDescription
    {
        public Color32 Color { get; }
        public Vector2 Offset { get; }
        public double BlurSigma { get; }

        public TextShadowDescription(Color32 color, Vector2 offset, double blurSigma)
        {
            Color = color;
            Offset = offset;
            BlurSigma = blurSigma;
        }
    }
}

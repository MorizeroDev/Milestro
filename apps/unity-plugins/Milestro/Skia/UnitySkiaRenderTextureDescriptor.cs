using UnityEngine;

namespace Milestro.Skia
{
    public struct UnitySkiaRenderTextureDescriptor
    {
        public int Width;
        public int Height;
        public bool Srgb;
        public bool ClearBeforeDraw;
        public int MsaaSamples;
        public UnitySkiaRenderTextureResolveStrategy ResolveStrategy;
        public UnitySkiaRenderTextureFormat PreferredFormat;

        public static bool DefaultSrgb => QualitySettings.activeColorSpace == ColorSpace.Linear;

        public UnitySkiaRenderTextureDescriptor(int width, int height)
            : this(width, height, DefaultSrgb)
        {
        }

        public UnitySkiaRenderTextureDescriptor(int width, int height, bool srgb)
        {
            Width = width;
            Height = height;
            Srgb = srgb;
            ClearBeforeDraw = true;
            MsaaSamples = 1;
            ResolveStrategy = UnitySkiaRenderTextureResolveStrategy.None;
            PreferredFormat = UnitySkiaRenderTextureFormat.Auto;
        }
    }
}

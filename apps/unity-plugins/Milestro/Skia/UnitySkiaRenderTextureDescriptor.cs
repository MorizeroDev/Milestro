using UnityEngine;

namespace Milestro.Skia
{
    public struct UnitySkiaRenderTextureDescriptor
    {
        public int Width;
        public int Height;
        public UnityEngine.ColorSpace ColorSpace;
        public bool UseSrgbStorage;
        public bool ClearBeforeDraw;
        public int MsaaSamples;
        public UnitySkiaRenderTextureResolveStrategy ResolveStrategy;
        public UnitySkiaRenderTextureFormat PreferredFormat;

        public static UnityEngine.ColorSpace DefaultColorSpace =>
            QualitySettings.activeColorSpace == UnityEngine.ColorSpace.Linear
                ? UnityEngine.ColorSpace.Linear
                : UnityEngine.ColorSpace.Gamma;

        public UnitySkiaRenderTextureDescriptor(int width, int height)
            : this(width, height, DefaultColorSpace)
        {
        }

        public UnitySkiaRenderTextureDescriptor(int width, int height, UnityEngine.ColorSpace colorSpace)
        {
            Width = width;
            Height = height;
            ColorSpace = colorSpace == UnityEngine.ColorSpace.Linear ? UnityEngine.ColorSpace.Linear : UnityEngine.ColorSpace.Gamma;
            UseSrgbStorage = false;
            ClearBeforeDraw = true;
            MsaaSamples = 1;
            ResolveStrategy = UnitySkiaRenderTextureResolveStrategy.None;
            PreferredFormat = UnitySkiaRenderTextureFormat.Auto;
        }

        public UnitySkiaRenderTextureDescriptor(int width, int height, bool srgb)
            : this(width, height, srgb ? UnityEngine.ColorSpace.Linear : UnityEngine.ColorSpace.Gamma)
        {
        }
    }
}

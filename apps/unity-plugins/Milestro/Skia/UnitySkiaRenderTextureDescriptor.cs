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

        public UnitySkiaRenderTextureDescriptor(int width, int height, bool srgb = true)
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

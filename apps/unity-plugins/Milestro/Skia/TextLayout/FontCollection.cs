using Milestro.Binding;

namespace Milestro.Skia.TextLayout
{
    public static class FontCollection
    {
        public static void ClearCaches()
        {
            BindingC.SkiaFontCollectionClearCaches();
        }

        public static bool FallbackEnabled
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaFontCollectionIsFontFallbackEnabled(out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaFontCollectionSetFontFallbackEnabled(value ? 1 : 0)
                );
        }
    }
}

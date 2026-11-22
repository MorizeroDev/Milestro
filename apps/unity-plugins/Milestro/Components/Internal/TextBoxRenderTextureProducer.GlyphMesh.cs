using Milestro.Skia;

namespace Milestro.Components.Internal
{
    public partial class TextBoxRenderTextureProducer
    {
        private TextPipelineFreshness retiredFreshness;
        internal TextGlyphMeshOutput? GlyphMeshOutput => (renderTarget as TextBoxRenderTarget)?.GlyphMeshOutput;
        internal TextPipelineFreshness Freshness => (renderTarget as TextBoxRenderTarget)?.Freshness ?? retiredFreshness;
        internal void BeginFreshnessWindow()
        {
            if (RenderTarget is TextBoxRenderTarget target)
            {
                target.Freshness.BeginWindow();
            }
        }

        private void UpdateGlyphMeshPresentationCapability()
        {
            if (renderTarget is TextBoxRenderTarget target)
            {
                var graphic = GetComponent<TextBox>();
                target.SetGlyphMeshPresentationAllowed(graphic != null && graphic.SupportsGlyphMeshPresentation);
            }
        }
    }
}

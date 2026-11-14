using Milestro.Skia;

namespace Milestro.Components.Internal
{
    public partial class TextBoxRenderTextureProducer
    {
        internal TextGlyphMeshOutput? GlyphMeshOutput => (renderTarget as TextBoxRenderTarget)?.GlyphMeshOutput;

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

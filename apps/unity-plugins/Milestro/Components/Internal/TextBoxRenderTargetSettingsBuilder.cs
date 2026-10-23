using Milestro.Model;

namespace Milestro.Components.Internal
{
    internal static class TextBoxRenderTargetSettingsBuilder
    {
        public static TextBoxRenderTargetSettings BuildFrom(TextBoxRenderTextureProducer producer)
        {
            return new TextBoxRenderTargetSettings(producer.content,
                producer.margin,
                producer.paragraphStyle,
                producer.textStyle,
                producer.wrapMode,
                producer.textOverflow,
                producer.enableAutoSizing,
                producer.fontSizeMin,
                producer.fontSizeMax);
        }
    }
}

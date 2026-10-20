using Milestro.Model;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class TextBoxSpacingSettingsTests
    {
        [Test]
        public void DefaultsLeaveSpacingAtZero()
        {
            var settings = new TextBoxRenderTargetSettings("content",
                null,
                new ParagraphStyleSettings(),
                new TextStyleSettings(),
                TextBoxWrapMode.Wrap,
                TextOverflow.Clip);

            Assert.AreEqual(0f, settings.TextStyle.letterSpacing);
            Assert.AreEqual(0f, settings.TextStyle.wordSpacing);
            Assert.AreEqual(0f, settings.TextStyle.lineHeight);
        }

        [Test]
        public void ConstructorThreadsStyleSettings()
        {
            var paragraphStyle = new ParagraphStyleSettings();
            paragraphStyle.textAlign = TextAlign.Center;
            paragraphStyle.singleLine = true;
            var textStyle = new TextStyleSettings();
            textStyle.letterSpacing = 2.5f;
            textStyle.wordSpacing = 8f;
            textStyle.lineHeight = 1.6f;
            textStyle.slant = FontSlant.Italic;

            var settings = new TextBoxRenderTargetSettings("content",
                null,
                paragraphStyle,
                textStyle,
                TextBoxWrapMode.Wrap,
                TextOverflow.Clip);

            Assert.AreSame(paragraphStyle, settings.ParagraphStyle);
            Assert.AreSame(textStyle, settings.TextStyle);
            Assert.AreEqual(TextAlign.Center, settings.ParagraphStyle.textAlign);
            Assert.AreEqual(2.5f, settings.TextStyle.letterSpacing);
            Assert.AreEqual(8f, settings.TextStyle.wordSpacing);
            Assert.AreEqual(1.6f, settings.TextStyle.lineHeight);
            Assert.AreEqual(FontSlant.Italic, settings.TextStyle.slant);
        }

        [Test]
        public void NullStyleSettingsFallBackToDefaults()
        {
            var settings = new TextBoxRenderTargetSettings("content",
                null,
                null,
                null,
                TextBoxWrapMode.Wrap,
                TextOverflow.Clip);

            Assert.IsNotNull(settings.ParagraphStyle);
            Assert.IsNotNull(settings.TextStyle);
            Assert.AreEqual(TextAlign.Start, settings.ParagraphStyle.textAlign);
            Assert.AreEqual(FontWeight.Normal, settings.TextStyle.weight);
        }
    }
}

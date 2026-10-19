using System;
using System.Linq;
using Milestro.Extensions;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class ScriptTagParserTests
    {
        [Test]
        public void SuperscriptScalesSizeAndRaisesBaseline()
        {
            var payload = Parse("x<sup>2</sup>");

            Assert.That(PlainText(payload), Is.EqualTo("x2"));
            var script = payload.Body[1].TextStyle;
            Assert.That(script.FontSizeScale, Is.EqualTo(RichTextParser.RichTextParser.ScriptFontSizeScale));
            Assert.That(script.BaselineShiftEm,
                Is.EqualTo(RichTextParser.RichTextParser.SuperscriptBaselineShiftEm));
            Assert.That(script.FontSize, Is.Null);
            Assert.That(script.BaselineShift, Is.Null);
        }

        [Test]
        public void SubscriptScalesSizeAndLowersBaseline()
        {
            var payload = Parse("H<sub>2</sub>O");

            Assert.That(PlainText(payload), Is.EqualTo("H2O"));
            var script = payload.Body[1].TextStyle;
            Assert.That(script.FontSizeScale, Is.EqualTo(RichTextParser.RichTextParser.ScriptFontSizeScale));
            Assert.That(script.BaselineShiftEm,
                Is.EqualTo(RichTextParser.RichTextParser.SubscriptBaselineShiftEm));
        }

        [Test]
        public void NestedScriptTagsComposeMultiplicativelyAndAdditively()
        {
            var payload = Parse("<sup>a<sup>b</sup></sup>");

            var outer = payload.Body[0].TextStyle;
            Assert.That(outer.FontSizeScale, Is.EqualTo(0.5f));
            Assert.That(outer.BaselineShiftEm, Is.EqualTo(0.5f));

            var inner = payload.Body[1].TextStyle;
            Assert.That(inner.FontSizeScale, Is.EqualTo(0.25f));
            Assert.That(inner.BaselineShiftEm, Is.EqualTo(1f));
        }

        [Test]
        public void ScriptTagsKeepInheritedStyles()
        {
            var payload = Parse("<b>a<sup>2</sup></b>");

            var script = payload.Body[1].TextStyle;
            Assert.That(script.FontWeight, Is.EqualTo(Milestro.Model.FontWeight.Bold));
            Assert.That(script.FontSizeScale, Is.EqualTo(0.5f));
        }

        [Test]
        public void MergedStyleScalesFontAndShiftsBaselineAgainstUnscaledSize()
        {
            var payload = Parse("x<sup>2</sup>");

            using var baseStyle = new TextStyle { FontSize = 32 };
            using var merged = payload.Body[1].TextStyle.ToTextStyle(baseStyle);

            Assert.That(merged.FontSize, Is.EqualTo(16f));
            Assert.That(merged.BaselineShift, Is.EqualTo(0.5f * 32f));
        }

        [Test]
        public void ScriptScalingAppliesOnTopOfExplicitFontSize()
        {
            var payload = Parse("<font size=\"40\">x<sup>2</sup></font>");

            using var baseStyle = new TextStyle { FontSize = 32 };
            using var merged = payload.Body[1].TextStyle.ToTextStyle(baseStyle);

            Assert.That(merged.FontSize, Is.EqualTo(20f));
            Assert.That(merged.BaselineShift, Is.EqualTo(0.5f * 40f));
        }

        [Test]
        public void VoffsetTakesPrecedenceOverScriptBaselineShift()
        {
            var payload = Parse("<span voffset=\"7\">x<sub>2</sub></span>");

            using var baseStyle = new TextStyle { FontSize = 32 };
            using var merged = payload.Body[1].TextStyle.ToTextStyle(baseStyle);

            Assert.That(merged.FontSize, Is.EqualTo(16f));
            Assert.That(merged.BaselineShift, Is.EqualTo(7f));
        }

        [Test]
        public void SpanAttributesSetScaleAndEmShiftAbsolutely()
        {
            var payload = Parse("<span fscale=\"0.75\" voffset-em=\"0.1\">x</span>");

            var style = payload.Body[0].TextStyle;
            Assert.That(style.FontSizeScale, Is.EqualTo(0.75f));
            Assert.That(style.BaselineShiftEm, Is.EqualTo(0.1f));

            using var baseStyle = new TextStyle { FontSize = 32 };
            using var merged = style.ToTextStyle(baseStyle);
            Assert.That(merged.FontSize, Is.EqualTo(24f));
            Assert.That(merged.BaselineShift, Is.EqualTo(0.1f * 32f));
        }

        [Test]
        public void NonPositiveFscaleIsIgnored()
        {
            var payload = Parse("<span fscale=\"0\">x</span><span fscale=\"-1\">y</span>");

            Assert.That(payload.Body[0].TextStyle.FontSizeScale, Is.Null);
            Assert.That(payload.Body[1].TextStyle.FontSizeScale, Is.Null);
        }

        [Test]
        public void ScriptStateRoundTripsThroughGenerateRichText()
        {
            var payload = Parse("<sub><sup>x</sup></sub>");
            var state = payload.Body[0].TextStyle;

            var markup = state.GenerateRichText() + "x</span>";
            var reparsed = Parse(markup);
            var reparsedState = reparsed.Body[0].TextStyle;

            Assert.That(reparsedState.FontSizeScale, Is.EqualTo(state.FontSizeScale));
            Assert.That(reparsedState.BaselineShiftEm, Is.EqualTo(state.BaselineShiftEm));
        }

        [Test]
        public void ScriptTagsAreCaseSensitive()
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText("<SUP>x</SUP>");

            Assert.Throws<InvalidCastException>(() => parser.ConvertToSegments());
        }

        private static ParagraphPayload Parse(string markup)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(markup);
            return parser.ConvertToSegments();
        }

        private static string PlainText(ParagraphPayload payload)
        {
            return string.Concat(payload.Body.Select(segment => segment.Content ?? ""));
        }
    }
}

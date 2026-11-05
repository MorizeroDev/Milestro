using System;
using System.Linq;
using Milestro.Extensions;
using Milestro.Model;
using Milestro.RichText.TMPro;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Milestro.Tests
{
    public class TMProRichTextParserTests
    {
        [Test]
        public void ProviderRegistersTmproForEditorUse()
        {
            var provider = new TMProRichTextParserProvider();
            Assert.That(provider.Id, Is.EqualTo("tmpro"));
            Assert.That(provider.DisplayName, Is.EqualTo("TMPro"));
            Assert.That(provider.CreateParser(), Is.TypeOf<TMProRichTextParser>());
            Assert.That(RichTextParserService.TryCreate(TMProRichTextParser.Id, out var registered), Is.True);
            Assert.That(registered, Is.TypeOf<TMProRichTextParser>());
        }

        [Test]
        public void EmptyAndPlainTextArePreserved()
        {
            var empty = Parse("");
            var plain = Parse("a & b < 3 > 2");
            Assert.That(empty.Blocks.Count, Is.EqualTo(1));
            Assert.That(PlainText(empty), Is.Empty);
            Assert.That(empty.Body, Is.Empty);
            Assert.That(PlainText(plain), Is.EqualTo("a & b < 3 > 2"));
        }

        [Test]
        public void BasicAndNestedStylesAreCaseInsensitive()
        {
            var payload = Parse("a<B>bold <I>both</I></B>c<U>u</U><S>s</S>" +
                                "<STRIKETHROUGH>strike</STRIKETHROUGH>");
            Assert.That(PlainText(payload), Is.EqualTo("abold bothcusstrike"));
            Assert.That(Segment(payload, "bold ").TextStyle.FontWeight, Is.EqualTo(700));
            var both = Segment(payload, "both").TextStyle;
            Assert.That(both.FontWeight, Is.EqualTo(700));
            Assert.That(both.Italic, Is.True);
            Assert.That(Segment(payload, "u").TextStyle.Underline, Is.True);
            Assert.That(Segment(payload, "s").TextStyle.Strikethrough, Is.True);
            Assert.That(Segment(payload, "strike").TextStyle.Strikethrough, Is.True);
        }

        [Test]
        public void AlphaComposesWithForegroundWithoutChangingRgb()
        {
            var payload = Parse("<color=#3366ff80><alpha=#80>x</alpha></color><alpha=#40>y</alpha>");
            Assert.That(Segment(payload, "x").TextStyle.Alpha, Is.EqualTo(128f / 255f));
            Assert.That(Segment(payload, "y").TextStyle.Alpha, Is.EqualTo(64f / 255f));
            using var baseStyle = new TextStyle();
            baseStyle.Color = new Color32(255, 255, 255, 255);
            using var colored = Segment(payload, "x").TextStyle.ToTextStyle(baseStyle);
            using var inherited = Segment(payload, "y").TextStyle.ToTextStyle(baseStyle);
            Assert.That((Color32)colored.Color, Is.EqualTo(new Color32(0x33, 0x66, 0xff, 0x40)));
            Assert.That((Color32)inherited.Color, Is.EqualTo(new Color32(0xff, 0xff, 0xff, 0x40)));
        }

        [Test]
        public void ColorSizeFontAndWeightComposeOnOneRun()
        {
            var payload = Parse("<color=#3366ff><size=24><font=\"Arial\">" +
                                "<font-weight=700>x</font-weight></font></size></color>");
            var style = Segment(payload, "x").TextStyle;
            Assert.That((Color32)style.Color.Value, Is.EqualTo(new Color32(0x33, 0x66, 0xff, 0xff)));
            Assert.That(style.FontSize, Is.EqualTo(24f));
            Assert.That(style.FontWeight, Is.EqualTo(700));
            Assert.That(style.FontFamilies, Is.EqualTo(new[] { "Arial" }));
        }

        [Test]
        public void HexColorShorthandUsesColorClosingTag()
        {
            var payload = Parse("<#3c8>x</color>");
            Assert.That(PlainText(payload), Is.EqualTo("x"));
            Assert.That((Color32)Segment(payload, "x").TextStyle.Color.Value,
                Is.EqualTo(new Color32(0x33, 0xcc, 0x88, 0xff)));
        }

        [Test]
        public void MetricsScriptsAndMarkMapToCoreStyles()
        {
            var metrics = Parse("<size=150%><cspace=2px><voffset=25%>x</voffset></cspace></size>");
            var scripts = Parse("<sup>up</sup><sub>down</sub>");
            var marked = Parse("<mark=#3366ff80>x</mark>");
            var style = Segment(metrics, "x").TextStyle;
            Assert.That(style.FontSizeScale, Is.EqualTo(1.5f));
            Assert.That(style.LetterSpacing, Is.EqualTo(2f));
            Assert.That(style.BaselineShiftEm, Is.EqualTo(0.25f));
            Assert.That(Segment(scripts, "up").TextStyle.FontSizeScale, Is.EqualTo(0.5f));
            Assert.That(Segment(scripts, "up").TextStyle.BaselineShiftEm, Is.EqualTo(0.5f));
            Assert.That(Segment(scripts, "down").TextStyle.BaselineShiftEm, Is.EqualTo(-0.25f));
            Assert.That((Color32)Segment(marked, "x").TextStyle.BackgroundColor.Value,
                Is.EqualTo(new Color32(0x33, 0x66, 0xff, 0x80)));
        }

        [TestCase("center", TextAlign.Center)]
        [TestCase("left", TextAlign.Left)]
        [TestCase("right", TextAlign.Right)]
        [TestCase("justified", TextAlign.Justify)]
        public void AlignmentCreatesStyledParagraphBlocks(string value, TextAlign expected)
        {
            var payload = Parse("before<align=" + value + ">middle</align>after");
            Assert.That(payload.Blocks.Select(payload.GetPlainText),
                Is.EqualTo(new[] { "before", "middle", "after" }));
            Assert.That(payload.Blocks[1].Style.TextAlign, Is.EqualTo(expected));
        }

        [Test]
        public void ParagraphMeasurementsMapToBlockStyles()
        {
            var indent = Parse("<indent=2em>x</indent>");
            var lineIndent = Parse("<size=20><line-indent=25%>x</line-indent></size>");
            var emLineIndent = Parse("<size=20><line-indent=2em>x</line-indent></size>");
            var margin = Parse("<margin=4px>x</margin>");
            var marginLeft = Parse("<margin-left=6>x</margin-left>");
            var marginRight = Parse("<margin-right=8px>x</margin-right>");
            var width = Parse("<width=75%>x</width>");

            Assert.That(indent.Blocks[0].Style.MarginLeft.Value, Is.EqualTo(2f));
            Assert.That(indent.Blocks[0].Style.MarginLeft.Unit, Is.EqualTo(MarginUnit.Em));
            Assert.That(lineIndent.Blocks[0].Style.TextIndent.Value.Value, Is.EqualTo(25f));
            Assert.That(lineIndent.Blocks[0].Style.TextIndent.Value.IsPercentage, Is.True);
            Assert.That(emLineIndent.Blocks[0].Style.TextIndentEm, Is.EqualTo(2f));
            Assert.That(emLineIndent.Blocks[0].Style.TextIndentFontSize, Is.EqualTo(20f));
            Assert.That(margin.Blocks[0].Style.MarginLeft.Value, Is.EqualTo(4f));
            Assert.That(margin.Blocks[0].Style.MarginRight.Value, Is.EqualTo(4f));
            Assert.That(marginLeft.Blocks[0].Style.MarginLeft.Value, Is.EqualTo(6f));
            Assert.That(marginLeft.Blocks[0].Style.MarginLeft.Unit, Is.EqualTo(MarginUnit.Px));
            Assert.That(marginRight.Blocks[0].Style.MarginRight.Value, Is.EqualTo(8f));
            Assert.That(width.Blocks[0].Style.Width.Value.Value, Is.EqualTo(75f));
            Assert.That(width.Blocks[0].Style.Width.Value.Unit, Is.EqualTo(MarginUnit.ContainerPercent));
        }

        [Test]
        public void CaseTransformsAndNobrUseCoreBehavior()
        {
            var transformed = Parse("<smallcaps>Ab</smallcaps><uppercase>cd</uppercase>" +
                                    "<lowercase>EF</lowercase>");
            var noWrap = Parse("a<nobr>b<b>b</b> cc</nobr>d");
            Assert.That(PlainText(transformed), Is.EqualTo("ABCDef"));
            Assert.That(Segment(transformed, "CD").TextStyle.TextTransform,
                Is.EqualTo(TextTransform.Uppercase));
            Assert.That(Segment(transformed, "ef").TextStyle.TextTransform,
                Is.EqualTo(TextTransform.Lowercase));
            Assert.That(PlainText(noWrap), Is.EqualTo("abb ccd"));
            var range = noWrap.NoWrapAnnotations.Single();
            Assert.That(range.StartUtf16, Is.EqualTo(1));
            Assert.That(range.EndUtf16, Is.EqualTo(6));
        }

        [Test]
        public void BreaksLinksAndNoparseFollowPayloadRules()
        {
            var breaks = Parse("a<br>b<br/>c");
            var linked = Parse("曲😀<link=\"a>b\">链😀</link>尾");
            var literal = Parse("<noparse><b>&amp;<sprite=0></noparse><i>x</i>");
            Assert.That(PlainText(breaks), Is.EqualTo("a\nb\nc"));
            Assert.That(PlainText(linked), Is.EqualTo("曲😀链😀尾"));
            var link = linked.Links.Single();
            Assert.That(link.Href, Is.EqualTo("a>b"));
            Assert.That(link.StartUtf16, Is.EqualTo("曲😀".Length));
            Assert.That(link.EndUtf16, Is.EqualTo("曲😀链😀".Length));
            Assert.That(PlainText(literal), Is.EqualTo("<b>&amp;<sprite=0>x"));
            Assert.That(Segment(literal, "x").TextStyle.Italic, Is.True);
        }

        [Test]
        public void UnsupportedTagsWarnAndRemainLiteralWhileChildrenStillParse()
        {
            const string open = "<foo data=\"x>y\">";
            ExpectWarning("foo");
            ExpectWarning("foo");
            var payload = Parse("a" + open + "<b>c</b></foo>d");
            Assert.That(PlainText(payload), Is.EqualTo("a" + open + "c</foo>d"));
            Assert.That(Segment(payload, "c").TextStyle.FontWeight, Is.EqualTo(700));
        }

        [Test]
        public void UnsupportedMultiAttributeAndTmproTagsWarn()
        {
            const string sprite = "<sprite name=\"Icon\"/>";
            ExpectWarning("sprite");
            ExpectWarning("rotate");
            ExpectWarning("rotate");
            var payload = Parse(sprite + "<rotate=30>x</rotate>");
            Assert.That(PlainText(payload), Is.EqualTo(sprite + "<rotate=30>x</rotate>"));
        }

        [Test]
        public void InvalidNewTagValuesWarnAndRemainLiteral()
        {
            ExpectWarning("alpha");
            ExpectWarning("alpha");
            ExpectWarning("width");
            ExpectWarning("width");
            var payload = Parse("<alpha=50%>x</alpha><width=0>y</width>");
            Assert.That(PlainText(payload), Is.EqualTo("<alpha=50%>x</alpha><width=0>y</width>"));
        }

        [Test]
        public void LineHeightMapsToRunHeightMultiplier()
        {
            var payload = Parse("a<line-height=85%>b<font=\"NotoSansSymbols2-Regular SDF\">x</font>" +
                                "</line-height>c");

            Assert.That(PlainText(payload), Is.EqualTo("abxc"));
            Assert.That(Segment(payload, "a").TextStyle.LineHeight, Is.Null);
            Assert.That(Segment(payload, "b").TextStyle.LineHeight, Is.EqualTo(0.85f));
            Assert.That(Segment(payload, "x").TextStyle.LineHeight, Is.EqualTo(0.85f));
            Assert.That(Segment(payload, "c").TextStyle.LineHeight, Is.Null);
            using var baseStyle = new TextStyle();
            using var resolved = Segment(payload, "x").TextStyle.ToTextStyle(baseStyle);
            Assert.That(resolved.Height, Is.EqualTo(0.85f));
            Assert.That(resolved.HeightOverride, Is.True);
        }

        [Test]
        public void NestedLinksWarnAndRemainLiteralWithoutLosingOuterLink()
        {
            ExpectWarning("link");
            ExpectWarning("link");
            var payload = Parse("<link=outer>a<link=inner>b</link>c</link>");
            const string expected = "a<link=inner>b</link>c";
            Assert.That(PlainText(payload), Is.EqualTo(expected));
            Assert.That(payload.Links.Single().Href, Is.EqualTo("outer"));
            Assert.That(payload.Links.Single().EndUtf16, Is.EqualTo(expected.Length));
        }

        [Test]
        public void MismatchedAndInvalidTagsWarnAndRemainLiteral()
        {
            ExpectWarning("i");
            ExpectWarning("size");
            ExpectWarning("size");
            var mismatched = Parse("<b>x</i>y</b>");
            var invalid = Parse("<size=huge>x</size>");
            Assert.That(PlainText(mismatched), Is.EqualTo("x</i>y"));
            Assert.That(Segment(mismatched, "x</i>y").TextStyle.FontWeight, Is.EqualTo(700));
            Assert.That(PlainText(invalid), Is.EqualTo("<size=huge>x</size>"));
        }

        [Test]
        public void UnclosedTagsCloseAtEndAndParserInstancesAreReusable()
        {
            var parser = new TMProRichTextParser();
            var unclosed = parser.Parse("<color=red>x", new RichTextParseContext());
            const string source = "<b>曲😀</b><link=\"target\">链😀</link>";
            var first = parser.Parse(source, new RichTextParseContext());
            var second = parser.Parse(source, new RichTextParseContext());
            Assert.That(PlainText(unclosed), Is.EqualTo("x"));
            Assert.That(Segment(unclosed, "x").TextStyle.Color.HasValue, Is.True);
            Assert.That(PlainText(second), Is.EqualTo(PlainText(first)));
            Assert.That(second.Links.Single().StartUtf16, Is.EqualTo(first.Links.Single().StartUtf16));
            Assert.That(second.Links.Single().EndUtf16, Is.EqualTo(first.Links.Single().EndUtf16));
        }

        [Test]
        public void NullArgumentsThrow()
        {
            var parser = new TMProRichTextParser();
            Assert.Throws<ArgumentNullException>(() => parser.Parse(null, new RichTextParseContext()));
            Assert.Throws<ArgumentNullException>(() => parser.Parse("x", null));
        }

        private static ParagraphPayload Parse(string source)
        {
            return new TMProRichTextParser().Parse(source, new RichTextParseContext());
        }

        private static string PlainText(ParagraphPayload payload)
        {
            return string.Concat(payload.Blocks.Select(payload.GetPlainText));
        }

        private static TextSegment Segment(ParagraphPayload payload, string content)
        {
            return payload.Body.Single(segment => segment.Content == content);
        }

        private static void ExpectWarning(string tagName)
        {
            LogAssert.Expect(LogType.Warning,
                "Milestro TMPro parser: unsupported or unmatched tag '" + tagName +
                "'; treating it as plain text.");
        }
    }
}

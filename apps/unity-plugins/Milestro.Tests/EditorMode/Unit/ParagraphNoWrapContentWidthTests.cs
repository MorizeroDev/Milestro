using System.Collections.Generic;
using System.IO;
using System.Text;
using Milestro.Extensions;
using Milestro.RichTextParser;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
using Milestro.Unicode;
using NUnit.Framework;
using UnityEngine;
using Path = System.IO.Path;

namespace Milestro.Tests
{
    public sealed class ParagraphNoWrapContentWidthTests
    {
        [OneTimeSetUp]
        public void InitializeTextResources()
        {
            if (!Icu.IsLoaded())
            {
                var data = Resources.Load<TextAsset>("Milestro/icudtl.dat");
                Assert.That(data, Is.Not.Null, "The text fixture requires ICU data.");
                Icu.LoadIcuFronMemory(data.bytes);
            }
            for (var directory = new DirectoryInfo(Application.dataPath); directory != null;
                 directory = directory.Parent)
            {
                var font = Path.Combine(directory.FullName, "tests", "data", "font", "LXGWWenKai-Regular.ttf");
                if (!File.Exists(font)) continue;
                FontRegistry.RegisterFontFromPath(font);
                return;
            }
            Assert.Fail("The text fixture requires tests/data/font/LXGWWenKai-Regular.ttf.");
        }

        private static IEnumerable<TestCaseData> TextCases()
        {
            yield return new TestCaseData("").SetName("{m}_Empty");
            yield return new TestCaseData("ASCII 12").SetName("{m}_Ascii");
            yield return new TestCaseData("天地").SetName("{m}_Bmp");
            yield return new TestCaseData("A😀").SetName("{m}_SupplementaryTail");
            yield return new TestCaseData("A\0B").SetName("{m}_EmbeddedNull");
            yield return new TestCaseData("A\uD800").SetName("{m}_UnpairedHighTail");
            yield return new TestCaseData("A\uDC00").SetName("{m}_UnpairedLowTail");
            yield return new TestCaseData("\uD800\uD800\uDC00").SetName("{m}_HighThenPair");
            yield return new TestCaseData("\uDC00\uD800\uDC00\uD800").SetName("{m}_LowPairHigh");
            yield return new TestCaseData("Ae\u0301").SetName("{m}_CombiningTail");
            yield return new TestCaseData("A👩\u200D💻").SetName("{m}_ZwjTail");
            yield return new TestCaseData("A\u2764\uFE0F").SetName("{m}_VariationSelectorTail");
            yield return new TestCaseData("A  \nB  ").SetName("{m}_HardBreakAndTrailingSpaces");
        }

        [TestCaseSource(nameof(TextCases))]
        public void Utf8ReplacementPreservesUtf16Extent(string text)
        {
            var encoded = Encoding.UTF8.GetBytes(text);
            var decoded = Encoding.UTF8.GetString(encoded);
            Assert.That(decoded.Length, Is.EqualTo(text.Length));
        }

        [TestCaseSource(nameof(TextCases))]
        public void WidthMatchesEncodedTextAndLeavesProbeGeometryUnchanged(string text)
        {
            var decoded = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text));
            using var paragraph = BuildParagraph(text);
            using var encodedParagraph = BuildParagraph(decoded);
            var probeWidth = Paragraph.ResolveNoWrapProbeLayoutWidth(text, 36f, 80f);
            paragraph.Layout(probeWidth);
            encodedParagraph.Layout(probeWidth);
            var height = paragraph.Height;
            var longestLine = paragraph.LongestLine;
            var rects = paragraph.GetRectsForRange(0, text.Length);
            if (text.Length > 0)
            {
                Assert.That(rects, Is.Not.Empty, "The fixture must shape actual text.");
                Assert.That(height, Is.GreaterThan(0f));
                Assert.That(longestLine, Is.GreaterThan(0f));
            }

            var width = paragraph.ResolveNoWrapContentWidth(text);

            Assert.That(width, Is.EqualTo(encodedParagraph.ResolveNoWrapContentWidth(decoded)).Within(0.001f));
            Assert.That(paragraph.Height, Is.EqualTo(height));
            Assert.That(paragraph.LongestLine, Is.EqualTo(longestLine));
            Assert.That(paragraph.GetRectsForRange(0, text.Length), Is.EqualTo(rects));
            Assert.That(width, Is.GreaterThanOrEqualTo(0f));
            foreach (var rect in rects)
                Assert.That(width + 0.001f, Is.GreaterThanOrEqualTo(rect.width));

            paragraph.Layout(Paragraph.ResolveNoWrapLayoutWidth(80f, width));
            Assert.That(paragraph.ResolveNoWrapContentWidth(text), Is.EqualTo(width).Within(0.01f));
        }

        [Test]
        public void NullAndEmptyTextHaveTheSameQueryExtent()
        {
            using var paragraph = BuildParagraph("");
            paragraph.Layout(1024f);
            Assert.That(paragraph.ResolveNoWrapContentWidth(null),
                Is.EqualTo(paragraph.ResolveNoWrapContentWidth("")));
        }

        [Test]
        public void SurrogatePairSplitAcrossStyleRunsMatchesIndependentReplacement()
        {
            using var paragraphStyle = new ParagraphStyle();
            using var firstStyle = new TextStyle { FontSize = 36f };
            using var secondStyle = new TextStyle { FontSize = 36f };
            firstStyle.SetFontFamilies(new List<string> { "LXGW WenKai" });
            secondStyle.SetFontFamilies(new List<string> { "LXGW WenKai" });
            using var builder = new ParagraphBuilder(paragraphStyle);
            builder.PushStyle(firstStyle);
            builder.AddText("A\uD83D");
            builder.Pop();
            builder.PushStyle(secondStyle);
            builder.AddText("\uDE00");
            builder.Pop();
            using var paragraph = builder.Build();
            using var replacement = BuildParagraph("A\uFFFD\uFFFD");
            paragraph.Layout(1048576f);
            replacement.Layout(1048576f);

            Assert.That(paragraph.ResolveNoWrapContentWidth("A😀"),
                Is.EqualTo(replacement.ResolveNoWrapContentWidth("A\uFFFD\uFFFD")).Within(0.001f));
            Assert.That(paragraph.GetRectsForRange(0, 3), Is.EqualTo(replacement.GetRectsForRange(0, 3)));
        }

        [Test]
        public void BlockLocalTextIncludesTrailingNativePlaceholder()
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText("<p>😀<placeholder width=\"250\" height=\"24\"/></p>" +
                             "<p>e\u0301<placeholder width=\"180\" height=\"24\"/></p>");
            var payload = parser.ConvertToSegments();
            using var paragraphStyle = new ParagraphStyle();
            using var textStyle = new TextStyle { FontSize = 36f };
            textStyle.SetFontFamilies(new List<string> { "LXGW WenKai" });
            var paragraphs = payload.ToParagraphs(paragraphStyle, textStyle);
            try
            {
                Assert.That(paragraphs.Count, Is.EqualTo(2));
                var expectedTexts = new[] { "😀\uFFFC", "e\u0301\uFFFC" };
                for (var index = 0; index < paragraphs.Count; ++index)
                {
                    var text = payload.GetPlainText(payload.Blocks[index]);
                    Assert.That(text, Is.EqualTo(expectedTexts[index]));
                    var paragraph = paragraphs[index];
                    paragraph.Layout(Paragraph.ResolveNoWrapProbeLayoutWidth(text, 36f, 80f));
                    var objects = paragraph.GetRectsForPlaceholders();
                    Assert.That(objects, Has.Count.EqualTo(1));
                    Assert.That(paragraph.GetRectsForRange(text.Length - 1, text.Length),
                        Is.EqualTo(objects));
                    var width = paragraph.ResolveNoWrapContentWidth(text);
                    Assert.That(width + 0.001f, Is.GreaterThanOrEqualTo(objects[0].width));
                    paragraph.Layout(Paragraph.ResolveNoWrapLayoutWidth(80f, width));
                    Assert.That(paragraph.GetRectsForPlaceholders()[0].width,
                        Is.EqualTo(objects[0].width).Within(0.001f));
                }
            }
            finally
            {
                foreach (var paragraph in paragraphs) paragraph.Dispose();
            }
        }

        private static Paragraph BuildParagraph(string text)
        {
            using var paragraphStyle = new ParagraphStyle();
            using var textStyle = new TextStyle { FontSize = 36f };
            textStyle.SetFontFamilies(new List<string> { "LXGW WenKai" });
            using var builder = new ParagraphBuilder(paragraphStyle);
            builder.PushStyle(textStyle);
            builder.AddText(text);
            return builder.Build();
        }
    }
}

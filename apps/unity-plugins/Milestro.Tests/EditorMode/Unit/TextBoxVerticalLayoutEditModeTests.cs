using System;
using System.Linq;
using System.Text.RegularExpressions;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Extensions;
using Milestro.Model;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Milestro.Tests
{
    public sealed class TextBoxVerticalLayoutEditModeTests
    {
        private const string MixedText = "「漢字ABC2026かな」、。ー";
        private const string LinkedMixedText = "<a href=\"vertical\">" + MixedText + "</a>";

        [TestCase("over", RubyPosition.Above, false)]
        [TestCase("under", RubyPosition.Above, true)]
        public void RB14_RubyMarkupAndCssStayVertical(string position, RubyPosition expected, bool diagnostic)
        {
            var content = "<ruby style=\"ruby-position: " + position +
                "; ruby-align: start; ruby-overhang: none; --milestro-ruby-size: 0.75; " +
                "--milestro-ruby-gap: 4; --milestro-ruby-overflow: inline\">" +
                "漢<img width=\"30\" height=\"20\"/><rp>(</rp><rt>かなABC</rt><rp>)</rp></ruby>";
            LogAssert.Expect(LogType.Error, "<img> inside <ruby> is ignored");
            LogAssert.Expect(LogType.Error, "<img> inside <ruby> is ignored");
            var prepared = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(VerticalSettings(content));
            Assert.That(prepared.UsesHorizontalFallback, Is.False);
            Assert.That(prepared.Diagnostic != null, Is.EqualTo(diagnostic));
            if (diagnostic) Assert.That(prepared.Diagnostic!.Message, Does.Contain("under defaulted to Above"));
            Assert.That(prepared.Payload.RubyAnnotations, Has.Count.EqualTo(1));
            var ruby = prepared.Payload.RubyAnnotations[0];
            Assert.That(ruby.Text, Is.EqualTo("かなABC"));
            Assert.That(ruby.Style.Position, Is.EqualTo(expected));
            Assert.That(ruby.Style.Alignment, Is.EqualTo(RubyAlignment.Start));
            Assert.That(ruby.Style.Overhang, Is.EqualTo(RubyOverhang.None));
            Assert.That(ruby.Style.Overflow, Is.EqualTo(RubyOverflow.Inline));
            Assert.That(ruby.Style.FontSizeMultiplier, Is.EqualTo(0.75f));
            Assert.That(ruby.Style.Gap, Is.EqualTo(4f));
            Assert.That(prepared.Payload.Placeholders, Is.Empty);
            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidatePayload(prepared.Payload, prepared.Settings));
            using var paragraphStyle = VerticalStyle().ToParagraphStyle(false);
            using var textStyle = TextStyle().ToTextStyle();
            using var paragraph = prepared.Payload.ToParagraph(paragraphStyle, textStyle);
            paragraph.LayoutWithInlineExtent(200);
            Assert.That(paragraph.LayoutMetrics.Columns.Count, Is.EqualTo(1));
            Assert.That(paragraph.LayoutMetrics.Columns[0].Utf16End, Is.EqualTo(1));
        }

        [TestCase("start")]
        [TestCase("center")]
        [TestCase("space-around")]
        [TestCase("space-between")]
        public void RB01_RB03_RB04_RubyAllocationAndSelectionAcrossAlignments(string alignment)
        {
            var markup = "<ruby style=\"ruby-align: " + alignment + "; ruby-overhang: none\">" +
                "漢<rt>あいうえおかきく</rt></ruby>";
            using var plain = BuildParagraph("漢", VerticalStyle(), TextStyle(40f, 0f));
            using var ruby = BuildParagraph(markup, VerticalStyle(), TextStyle(40f, 0f));
            plain.LayoutWithInlineExtent(400);
            ruby.LayoutWithInlineExtent(400);
            Assert.That(ruby.LayoutMetrics.PhysicalSize.x, Is.GreaterThan(plain.LayoutMetrics.PhysicalSize.x));
            Assert.That(ruby.LayoutMetrics.Columns[0].PhysicalBaseline,
                Is.EqualTo(plain.LayoutMetrics.Columns[0].PhysicalBaseline).Within(0.01f));
            Assert.That(ruby.LayoutMetrics.MaxIntrinsicInlineExtent, Is.GreaterThan(100f));
            Assert.That(ruby.GetRectsForRange(0, 1)[0].width,
                Is.EqualTo(ruby.LayoutMetrics.Columns[0].BlockThickness).Within(0.01f));
            using var overhang = BuildParagraph(markup.Replace("ruby-overhang: none", "ruby-overhang: auto"),
                VerticalStyle(), TextStyle(40f, 0f));
            overhang.LayoutWithInlineExtent(400);
            // The platform SkFont fallback may include the annotation's ink in
            // intrinsic extent; it must still retain at least the base extent.
            Assert.That(overhang.LayoutMetrics.MaxIntrinsicInlineExtent,
                Is.GreaterThanOrEqualTo(plain.LayoutMetrics.MaxIntrinsicInlineExtent));
        }

        [Test]
        public void RB08_LineHeightReservesOnlyTheSpaceRubyNeeds()
        {
            const string markup = "<ruby>東京<rt>とうきょう</rt></ruby>の夜";
            var referenceInline = 0f;
            foreach (var height in new[] { 0f, 1f, 1.5f, 2f, 3f })
            {
                using var ruby = BuildParagraph(markup, VerticalStyle(), TextStyle(40f, height));
                using var plain = BuildParagraph("東京の夜", VerticalStyle(), TextStyle(40f, height));
                ruby.LayoutWithInlineExtent(400);
                plain.LayoutWithInlineExtent(400);
                var column = ruby.LayoutMetrics.Columns[0];
                Assert.That(column.PhysicalBaseline,
                    Is.EqualTo(plain.LayoutMetrics.Columns[0].PhysicalBaseline).Within(0.01f));
                if (height == 0f) referenceInline = ruby.LayoutMetrics.LongestInlineExtent;
                Assert.That(ruby.LayoutMetrics.LongestInlineExtent, Is.EqualTo(referenceInline).Within(0.01f));
                Assert.That(ruby.GetRectsForRange(0, 2)[0].width,
                    Is.EqualTo(column.BlockThickness).Within(0.01f));
                if (height == 3f) Assert.That(column.BlockThickness, Is.EqualTo(120f).Within(3f));
            }
        }

        [Test]
        public void RB05_RB07_RubyWrapAndEllipsisHideWholeSourceGroup()
        {
            const string markup = "天<ruby style=\"ruby-overhang: none\">漢字<rt>かんじ</rt></ruby>地";
            using var paragraph = BuildParagraph(markup, VerticalStyle(), TextStyle(40f, 0f));
            paragraph.LayoutWithInlineExtent(100);
            Assert.That(paragraph.LayoutMetrics.Columns.Count, Is.EqualTo(3));
            Assert.That(paragraph.LayoutMetrics.Columns[1].Utf16Start, Is.EqualTo(1));
            Assert.That(paragraph.LayoutMetrics.Columns[1].Utf16End, Is.EqualTo(3));
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(markup);
            using var style = VerticalStyle().ToParagraphStyle(false);
            style.MaxLines = 1;
            style.SetEllipsis("…");
            using var textStyle = TextStyle(40f, 0f).ToTextStyle();
            using var ellipsis = parser.ConvertToSegments().ToParagraph(style, textStyle);
            ellipsis.LayoutWithInlineExtent(100);
            Assert.That(ellipsis.GetRectsForRange(1, 3), Is.Empty);
            Assert.That(ellipsis.LayoutMetrics.Columns[0].Utf16End, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void RB08_RB10_RB11_RubyBlocksPreserveLocalRangesAndSpacing()
        {
            var blocks = new[] {
                "<p style=\"text-indent: 12px\"><ruby><sup>漢</sup>字<rt>かな</rt></ruby>天地</p>",
                "<p style=\"text-align: justify\"><u><ruby>日月<rt>にちげつ</rt></ruby></u>玄黃宇宙</p>",
            };
            var prepared = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(VerticalSettings(string.Concat(blocks)));
            using var style = VerticalStyle().ToParagraphStyle(false);
            using var textStyle = TextStyle(30f, 1.5f).ToTextStyle();
            var paragraphs = prepared.Payload.ToParagraphs(style, textStyle);
            try
            {
                var combined = TextBoxVerticalBlockLayout.Layout(paragraphs, 160, 17);
                for (var index = 0; index < blocks.Length; ++index)
                {
                    using var single = BuildParagraph(blocks[index], VerticalStyle(), TextStyle(30f, 1.5f));
                    single.LayoutWithInlineExtent(160);
                    Assert.That(paragraphs[index].LayoutMetrics.PhysicalSize, Is.EqualTo(single.LayoutMetrics.PhysicalSize));
                    Assert.That(paragraphs[index].GetRectsForRange(0, 2), Is.EqualTo(single.GetRectsForRange(0, 2)));
                }
                Assert.That(combined.PhysicalSize.x,
                    Is.EqualTo(paragraphs.Sum(paragraph => paragraph.LayoutMetrics.PhysicalSize.x) + 17).Within(0.01f));
            }
            finally { foreach (var paragraph in paragraphs) paragraph.Dispose(); }
        }

        [Test]
        public void RB13_ManagedVerticalBelowFailsAndRecoversThroughNewParagraph()
        {
            using var style = VerticalStyle().ToParagraphStyle(false);
            using var textStyle = TextStyle().ToTextStyle();
            style.SetTextStyle(textStyle);
            using var builder = new ParagraphBuilder(style);
            builder.AddRubyText("漢字", "かな", new RubyStyle { Position = RubyPosition.Below });
            using var paragraph = builder.Build();
            Assert.Throws<Exception>(() => paragraph.LayoutWithInlineExtent(200));
            using var valid = BuildParagraph("<ruby>漢字<rt>かな</rt></ruby>", VerticalStyle(), TextStyle());
            Assert.DoesNotThrow(() => valid.LayoutWithInlineExtent(200));
        }

        [Test]
        public void RB06_RB12_OverflowPolicyAndAutoSizeUseDerivedRubyGeometry()
        {
            const string markup = "<ruby style=\"ruby-align: center; ruby-overhang: none; " +
                "--milestro-ruby-overflow: inline\">漢<rt>あいうえおかきく</rt></ruby>";
            using var inline = BuildParagraph(markup, VerticalStyle(), TextStyle(40f, 0f));
            using var preserve = BuildParagraph(markup.Replace("overflow: inline", "overflow: preserve-base"),
                VerticalStyle(), TextStyle(40f, 0f));
            inline.LayoutWithInlineExtent(80);
            preserve.LayoutWithInlineExtent(80);
            Assert.That(preserve.LayoutMetrics.MaxIntrinsicInlineExtent,
                Is.EqualTo(inline.LayoutMetrics.MaxIntrinsicInlineExtent));
            Assert.That(preserve.LayoutMetrics.InkBounds.yMin, Is.LessThan(inline.LayoutMetrics.InkBounds.yMin));
            var settings = new TextBoxRenderTargetSettings("<a href=\"fit\"><ruby style=\"ruby-overhang: none\">" +
                "漢字<rt>かな</rt></ruby></a>", Margin.FixedZero(), VerticalStyle(), TextStyle(30f, 0f),
                XmlRichTextParser.Id, TextBoxWrapMode.Wrap, TextOverflow.Clip, true, 15f, 15f);
            using var target = new TextBoxRenderTarget();
            target.Rebuild(TextBoxRenderViewport.Invisible(new Vector2Int(200, 160), default),
                ColorSpace.Gamma, settings, true, null);
            var scale = (float)typeof(TextBoxRenderTarget).GetField("fitScale",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(target);
            Assert.That(scale, Is.EqualTo(0.5f));
            using var unscaled = BuildParagraph(settings.Content, VerticalStyle(), TextStyle(30f, 0f));
            unscaled.LayoutWithInlineExtent(320);
            Assert.That(target.ContentSize.x,
                Is.EqualTo(unscaled.LayoutMetrics.PhysicalSize.x * scale).Within(0.01f));
            Assert.That(unscaled.LayoutMetrics.Columns[0].BlockThickness, Is.GreaterThan(30));
        }

        [TestCase(TextBoxWrapMode.Wrap, 80, 100, 12f, 60f, 0.4f, 1f)]
        [TestCase(TextBoxWrapMode.Wrap, 160, 120, 6f, 60f, 0.8f, 1.34f)]
        [TestCase(TextBoxWrapMode.Wrap, 400, 400, 12f, 60f, 2f, 2f)]
        [TestCase(TextBoxWrapMode.Wrap, 10, 10, 24f, 60f, 0.8f, 0.8f)]
        [TestCase(TextBoxWrapMode.NoWrap, 300, 100, 3f, 60f, 0.1f, 0.5f)]
        public void AF01_AF05_VirtualViewportFitsBothAxesAndClamps(TextBoxWrapMode wrap,
            int width, int height, float minimum, float maximum, float expectedMin, float expectedMax)
        {
            var settings = new TextBoxRenderTargetSettings("天地玄黃宇宙洪荒日月盈昃",
                Margin.FixedZero(), VerticalStyle(), TextStyle(30f, 0f), XmlRichTextParser.Id,
                wrap, TextOverflow.Clip, true, minimum, maximum);
            using var target = new TextBoxRenderTarget();
            Assert.That(target.Rebuild(TextBoxRenderViewport.Invisible(new Vector2Int(width, height), default),
                ColorSpace.Gamma, settings, true, null), Is.True);
            var scale = (float)typeof(TextBoxRenderTarget).GetField("fitScale",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(target);
            Assert.That(scale, Is.InRange(expectedMin, expectedMax));
            if (width == 10)
                Assert.That(target.MaxScrollOffset.x + target.MaxScrollOffset.y, Is.GreaterThan(0f));
            else
            {
                Assert.That(target.ContentSize.x, Is.LessThanOrEqualTo(width + 0.01f));
                Assert.That(target.ContentSize.y, Is.LessThanOrEqualTo(height + 0.01f));
            }
        }

        [Test]
        public void AF03_AF14_NormalizationAndSearchRemainBounded()
        {
            var bounds = TextAutoFontSizeSettings.Default;
            bounds.fontSizeMin = float.NaN;
            bounds.fontSizeMax = -1f;
            bounds.Normalize();
            Assert.That(bounds.fontSizeMin, Is.EqualTo(0.1f));
            Assert.That(bounds.fontSizeMax, Is.EqualTo(0.1f));
            var calls = 0;
            var scale = TextAutoFontSizeScale.SearchFit(candidate =>
            {
                ++calls;
                return new Vector2(100f, 50f);
            }, 75f, 100f, 0.1f, 2f);
            Assert.That(scale, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(calls, Is.LessThanOrEqualTo(34));
        }

        [Test]
        public void AF07_ExplicitEffectiveFontSizeOverridesSearchBounds()
        {
            var request = new TextSizingRequest(TextWidthMode.Fixed, TextHeightMode.Fixed,
                300f, 200f, 300f, true, 30f);
            var sizing = new TextSizingResult(request, 300f, 200f, 45f, default, default, 0f, 0f);
            var settings = new TextBoxRenderTargetSettings("天", Margin.FixedZero(),
                VerticalStyle(), TextStyle(30f, 0f), XmlRichTextParser.Id, TextBoxWrapMode.Wrap,
                TextOverflow.Clip, true, 6f, 12f, sizing);
            using var target = new TextBoxRenderTarget();
            target.Rebuild(TextBoxRenderViewport.Invisible(new Vector2Int(300, 200), default),
                ColorSpace.Gamma, settings, true, null);
            var scale = (float)typeof(TextBoxRenderTarget).GetField("fitScale",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(target);
            Assert.That(scale, Is.EqualTo(1.5f));
            Assert.That(target.ContentSize.x, Is.EqualTo(45f).Within(25f));
        }

        [Test]
        public void AF06_AutoHeightDisablesSearchAndReportsIssue()
        {
            var settings = new TextBoxRenderTargetSettings("天", Margin.FixedZero(), VerticalStyle(),
                TextStyle(30f, 0f), XmlRichTextParser.Id, TextBoxWrapMode.Wrap, TextOverflow.Clip,
                true, 6f, 60f, verticalHeightMode: TextHeightMode.Auto);
            var prepared = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
            Assert.That(prepared.Settings.AutoFontSize, Is.False);
            Assert.That(prepared.Diagnostic!.Message, Does.Contain("auto font sizing"));
        }

        [Test]
        public void RealMixedTextUsesPhysicalMetricsAndUtf16ColumnRanges()
        {
            using var paragraph = BuildParagraph(MixedText, VerticalStyle(), TextStyle());
            paragraph.LayoutWithInlineExtent(180f);

            var metrics = paragraph.LayoutMetrics;

            Assert.That(paragraph.IsVertical, Is.True);
            Assert.That(metrics.WritingMode, Is.EqualTo(WritingMode.VerticalRl));
            Assert.That(metrics.PhysicalSize.y, Is.EqualTo(180f).Within(1f));
            Assert.That(metrics.PhysicalSize.x, Is.GreaterThan(0f));
            Assert.That(metrics.Columns.Count, Is.GreaterThan(1));
            Assert.That(metrics.ContentBounds.width, Is.GreaterThan(0f));
            Assert.That(metrics.InkBounds.width, Is.GreaterThan(0f));
            Assert.That(float.IsFinite(metrics.LongestInlineExtent), Is.True);
            Assert.That(metrics.MaxIntrinsicInlineExtent,
                Is.GreaterThanOrEqualTo(metrics.MinIntrinsicInlineExtent));

            var previousEnd = 0UL;
            foreach (var column in metrics.Columns)
            {
                Assert.That(column.PhysicalBounds.xMax, Is.LessThanOrEqualTo(metrics.PhysicalSize.x + 0.5f));
                Assert.That(column.Utf16Start, Is.GreaterThanOrEqualTo(previousEnd));
                Assert.That(column.Utf16End, Is.GreaterThanOrEqualTo(column.Utf16Start));
                Assert.That(column.Utf16End, Is.LessThanOrEqualTo((ulong)MixedText.Length));
                Assert.That(column.Utf16EndExcludingWhitespace,
                    Is.LessThanOrEqualTo(column.Utf16End));
                Assert.That(column.Utf16EndIncludingNewline,
                    Is.LessThanOrEqualTo((ulong)MixedText.Length));
                previousEnd = column.Utf16End;
            }

            Assert.That(previousEnd, Is.EqualTo((ulong)MixedText.Length));
        }

        [Test]
        public void SmallerInlineExtentAddsColumnsWithoutChangingVerticalAxis()
        {
            using var tall = BuildParagraph(MixedText, VerticalStyle(), TextStyle());
            using var shortParagraph = BuildParagraph(MixedText, VerticalStyle(), TextStyle());

            tall.LayoutWithInlineExtent(240f);
            shortParagraph.LayoutWithInlineExtent(90f);

            var tallMetrics = tall.LayoutMetrics;
            var shortMetrics = shortParagraph.LayoutMetrics;

            Assert.That(shortMetrics.Columns.Count, Is.GreaterThan(tallMetrics.Columns.Count));
            Assert.That(tallMetrics.PhysicalSize.y, Is.EqualTo(240f).Within(1f));
            Assert.That(shortMetrics.PhysicalSize.y, Is.EqualTo(90f).Within(1f));
            Assert.That(tallMetrics.PhysicalSize.x, Is.GreaterThan(0f));
            Assert.That(shortMetrics.PhysicalSize.x, Is.GreaterThan(tallMetrics.PhysicalSize.x));
            Assert.That(tallMetrics.Columns[0].PhysicalBounds.xMin,
                Is.GreaterThanOrEqualTo(tallMetrics.Columns[^1].PhysicalBounds.xMin));
        }

        [Test]
        public void AlignmentMovesColumnsAlongTheInlineAxisOnly()
        {
            using var natural = BuildParagraph("天", VerticalStyle(TextAlign.Start), TextStyle());
            natural.LayoutWithInlineExtent(float.PositiveInfinity);
            var extent = natural.LayoutMetrics.LongestInlineExtent;
            Assert.That(extent, Is.GreaterThan(0f));

            using var start = BuildParagraph("天", VerticalStyle(TextAlign.Start), TextStyle());
            using var end = BuildParagraph("天", VerticalStyle(TextAlign.End), TextStyle());
            using var center = BuildParagraph("天", VerticalStyle(TextAlign.Center), TextStyle());
            start.LayoutWithInlineExtent(300f);
            end.LayoutWithInlineExtent(300f);
            center.LayoutWithInlineExtent(300f);

            var startMetrics = start.LayoutMetrics;
            var endMetrics = end.LayoutMetrics;
            var centerMetrics = center.LayoutMetrics;
            Assert.That(startMetrics.Columns[0].PhysicalBounds.yMin, Is.EqualTo(0f).Within(0.5f));
            Assert.That(endMetrics.Columns[0].PhysicalBounds.yMin,
                Is.EqualTo(300f - extent).Within(0.5f));
            Assert.That(centerMetrics.Columns[0].PhysicalBounds.yMin,
                Is.EqualTo((300f - extent) * 0.5f).Within(0.5f));
            Assert.That(endMetrics.PhysicalSize.x, Is.EqualTo(startMetrics.PhysicalSize.x).Within(0.1f));
            Assert.That(centerMetrics.PhysicalSize.x, Is.EqualTo(startMetrics.PhysicalSize.x).Within(0.1f));
        }

        [Test]
        public void JustifyIsAcceptedAndExpandsOnlySoftWrappedColumns()
        {
            using var natural = BuildParagraph("天", VerticalStyle(TextAlign.Start), TextStyle());
            natural.LayoutWithInlineExtent(float.PositiveInfinity);
            var advance = natural.LayoutMetrics.LongestInlineExtent;
            Assert.That(advance, Is.GreaterThan(0f));
            var height = advance * 4f + 12f;
            var step = (height - advance * 4f) / 3f;
            const string text = "天地玄黃宇宙洪荒";

            using var start = BuildParagraph(text, VerticalStyle(TextAlign.Start), TextStyle());
            using var justified = BuildParagraph(text, VerticalStyle(TextAlign.Justify), TextStyle());
            start.LayoutWithInlineExtent(height);
            justified.LayoutWithInlineExtent(height);

            var startMetrics = start.LayoutMetrics;
            var metrics = justified.LayoutMetrics;
            Assert.That(metrics.Columns.Count, Is.EqualTo(2));
            Assert.That(metrics.Columns[0].InlineExtent, Is.EqualTo(height).Within(0.05f));
            Assert.That(metrics.Columns[1].InlineExtent, Is.EqualTo(advance * 4f).Within(0.05f));
            Assert.That(startMetrics.Columns[0].InlineExtent,
                Is.EqualTo(advance * 4f).Within(0.05f));
            Assert.That(metrics.LongestInlineExtent, Is.EqualTo(height).Within(0.05f));
            Assert.That(metrics.MinIntrinsicInlineExtent,
                Is.EqualTo(startMetrics.MinIntrinsicInlineExtent).Within(0.05f));
            Assert.That(metrics.MaxIntrinsicInlineExtent,
                Is.EqualTo(startMetrics.MaxIntrinsicInlineExtent).Within(0.05f));

            var startBox = start.GetRectsForRange(3, 4)[0];
            var justifiedBox = justified.GetRectsForRange(3, 4)[0];
            Assert.That(justifiedBox.yMin, Is.EqualTo(startBox.yMin + 3f * step).Within(0.05f));
            var startNextColumn = start.GetRectsForRange(4, 5)[0];
            var justifiedNextColumn = justified.GetRectsForRange(4, 5)[0];
            Assert.That(justifiedNextColumn.yMin, Is.EqualTo(startNextColumn.yMin).Within(0.05f));

            var settings = VerticalSettings("<a href=\"justify-link\">" + text + "</a>",
                VerticalStyle(TextAlign.Justify));
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(settings.Content);
            var payload = parser.ConvertToSegments();
            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidatePayload(payload, settings));
            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
            Assert.That(preparation.UsesHorizontalFallback, Is.False);
            Assert.That(preparation.Diagnostic, Is.Null);
            Assert.That(preparation.Settings.ParagraphStyle.textAlign, Is.EqualTo(TextAlign.Justify));
        }

        [Test]
        public void InfiniteExtentKeepsHardBreaksAndHandlesEmptyText()
        {
            using var empty = BuildParagraph("", VerticalStyle(), TextStyle());
            empty.LayoutWithInlineExtent(float.PositiveInfinity);
            var emptyMetrics = empty.LayoutMetrics;
            Assert.That(emptyMetrics.Columns.Count, Is.EqualTo(1));
            Assert.That(emptyMetrics.Columns[0].Utf16Start, Is.EqualTo(0UL));
            Assert.That(emptyMetrics.Columns[0].Utf16End, Is.EqualTo(0UL));
            Assert.That(float.IsFinite(emptyMetrics.PhysicalSize.x), Is.True);
            Assert.That(float.IsFinite(emptyMetrics.PhysicalSize.y), Is.True);

            using var newline = BuildParagraph("A\nB", VerticalStyle(), TextStyle());
            newline.LayoutWithInlineExtent(float.PositiveInfinity);
            var newlineMetrics = newline.LayoutMetrics;
            Assert.That(newlineMetrics.Columns.Count, Is.EqualTo(2));
            Assert.That(newlineMetrics.Columns[0].HardBreak, Is.True);
            Assert.That(newlineMetrics.Columns[0].Utf16Start, Is.EqualTo(0UL));
            Assert.That(newlineMetrics.Columns[0].Utf16End, Is.EqualTo(2UL));
            Assert.That(newlineMetrics.Columns[0].Utf16EndIncludingNewline, Is.EqualTo(2UL));
            Assert.That(newlineMetrics.Columns[1].Utf16Start, Is.EqualTo(2UL));
            Assert.That(newlineMetrics.Columns[1].Utf16End, Is.EqualTo(3UL));
            Assert.That(float.IsFinite(newlineMetrics.PhysicalSize.y), Is.True);
        }

        [Test]
        public void VerticalLayoutWidthMatchesInlineExtentAndOnlyHorizontalEntryPointsReject()
        {
            using var byWidth = BuildParagraph(MixedText, VerticalStyle(), TextStyle());
            using var byInlineExtent = BuildParagraph(MixedText, VerticalStyle(), TextStyle());
            byWidth.Layout(180f);
            byInlineExtent.LayoutWithInlineExtent(180f);

            var widthMetrics = byWidth.LayoutMetrics;
            var inlineMetrics = byInlineExtent.LayoutMetrics;
            Assert.That(widthMetrics.PhysicalSize.x, Is.EqualTo(inlineMetrics.PhysicalSize.x).Within(0.05f));
            Assert.That(widthMetrics.PhysicalSize.y, Is.EqualTo(inlineMetrics.PhysicalSize.y).Within(0.05f));
            Assert.That(widthMetrics.Columns.Count, Is.EqualTo(inlineMetrics.Columns.Count));
            for (var index = 0; index < widthMetrics.Columns.Count; ++index)
            {
                Assert.That(widthMetrics.Columns[index].Utf16Start,
                    Is.EqualTo(inlineMetrics.Columns[index].Utf16Start));
                Assert.That(widthMetrics.Columns[index].Utf16End,
                    Is.EqualTo(inlineMetrics.Columns[index].Utf16End));
                Assert.That(widthMetrics.Columns[index].PhysicalBounds.xMin,
                    Is.EqualTo(inlineMetrics.Columns[index].PhysicalBounds.xMin).Within(0.05f));
                Assert.That(widthMetrics.Columns[index].InlineExtent,
                    Is.EqualTo(inlineMetrics.Columns[index].InlineExtent).Within(0.05f));
            }

            Assert.That(byWidth.Height,
                Is.EqualTo(byWidth.LayoutMetrics.PhysicalSize.y).Within(0.05f));
            Assert.That(byWidth.LongestLine,
                Is.EqualTo(byWidth.LayoutMetrics.LongestInlineExtent).Within(0.05f));
            Assert.Throws<NotSupportedException>(() => _ = byWidth.MinIntrinsicWidth);
            Assert.Throws<NotSupportedException>(() => _ = byWidth.MaxIntrinsicWidth);
            Assert.Throws<NotSupportedException>(() => byWidth.ResolveNoWrapContentWidth(MixedText));
        }

        [Test]
        public void FailedVerticalLayoutWidthClearsPhysicalMetricsAndRecovers()
        {
            using var paragraph = BuildParagraph(MixedText, VerticalStyle(), TextStyle());
            paragraph.LayoutWithInlineExtent(180f);
            Assert.That(paragraph.Height, Is.GreaterThan(0f));

            Assert.Throws<Exception>(() => paragraph.Layout(-1f));
            Assert.Throws<Exception>(() => _ = paragraph.LayoutMetrics);
            Assert.That(paragraph.Height, Is.EqualTo(0f));
            Assert.That(paragraph.LongestLine, Is.EqualTo(0f));

            Assert.Throws<Exception>(() => paragraph.Layout(float.NaN));
            Assert.Throws<Exception>(() => _ = paragraph.LayoutMetrics);
            Assert.That(paragraph.Height, Is.EqualTo(0f));
            Assert.That(paragraph.LongestLine, Is.EqualTo(0f));

            Assert.DoesNotThrow(() => paragraph.Layout(180f));
            Assert.That(paragraph.Height, Is.GreaterThan(0f));
            Assert.That(paragraph.LayoutMetrics.Columns, Is.Not.Empty);
        }

        [Test]
        public void HorizontalDefaultStillUsesTheExistingWidthLayout()
        {
            using var paragraph = BuildParagraph(MixedText, new ParagraphStyleSettings(), TextStyle());

            Assert.That(paragraph.IsVertical, Is.False);
            paragraph.Layout(240f);
            Assert.That(paragraph.WritingMode, Is.EqualTo(WritingMode.HorizontalTb));
            Assert.That(paragraph.Height, Is.GreaterThan(0f));
            Assert.That(paragraph.MaxIntrinsicWidth, Is.GreaterThan(0f));
            Assert.Throws<NotSupportedException>(() => _ = paragraph.LayoutMetrics);
        }

        [Test]
        public void LatinHardBreaksKeepPrecedingRunMetrics()
        {
            using var plain = BuildParagraph("ABC", VerticalStyle(), TextStyle());
            using var lf = BuildParagraph("ABC\n", VerticalStyle(), TextStyle());
            using var crlf = BuildParagraph("ABC\r\n", VerticalStyle(), TextStyle());
            using var first = BuildParagraph("AB", VerticalStyle(), TextStyle());
            using var second = BuildParagraph("CD", VerticalStyle(), TextStyle());
            using var twoColumns = BuildParagraph("AB\nCD", VerticalStyle(), TextStyle());

            plain.LayoutWithInlineExtent(float.PositiveInfinity);
            lf.LayoutWithInlineExtent(float.PositiveInfinity);
            crlf.LayoutWithInlineExtent(float.PositiveInfinity);
            first.LayoutWithInlineExtent(float.PositiveInfinity);
            second.LayoutWithInlineExtent(float.PositiveInfinity);
            twoColumns.LayoutWithInlineExtent(float.PositiveInfinity);

            var plainMetrics = plain.LayoutMetrics;
            Assert.That(plainMetrics.Columns.Count, Is.EqualTo(1));
            Assert.That(plainMetrics.Columns[0].InlineExtent, Is.GreaterThan(0f));

            foreach (var (paragraph, label) in new[] { (lf, "LF"), (crlf, "CRLF") })
            {
                var metrics = paragraph.LayoutMetrics;
                Assert.That(metrics.Columns.Count, Is.EqualTo(2), label);
                Assert.That(metrics.Columns[0].HardBreak, Is.True, label);
                Assert.That(metrics.Columns[0].InlineExtent,
                    Is.EqualTo(plainMetrics.Columns[0].InlineExtent).Within(0.05f), label);
                Assert.That(metrics.Columns[0].BlockThickness,
                    Is.EqualTo(plainMetrics.Columns[0].BlockThickness).Within(0.05f), label);
                Assert.That(metrics.PhysicalSize.y,
                    Is.EqualTo(plainMetrics.PhysicalSize.y).Within(0.05f), label);
                Assert.That(metrics.PhysicalSize.x,
                    Is.GreaterThanOrEqualTo(plainMetrics.PhysicalSize.x), label);
                Assert.That(metrics.InkBounds.width,
                    Is.EqualTo(plainMetrics.InkBounds.width).Within(0.05f), label);
                Assert.That(metrics.InkBounds.height,
                    Is.EqualTo(plainMetrics.InkBounds.height).Within(0.05f), label);
            }

            var firstMetrics = first.LayoutMetrics;
            var secondMetrics = second.LayoutMetrics;
            var twoMetrics = twoColumns.LayoutMetrics;
            Assert.That(twoMetrics.Columns.Count, Is.EqualTo(2));
            Assert.That(twoMetrics.Columns[0].HardBreak, Is.True);
            Assert.That(twoMetrics.Columns[1].HardBreak, Is.False);
            Assert.That(twoMetrics.Columns[0].InlineExtent,
                Is.EqualTo(firstMetrics.Columns[0].InlineExtent).Within(0.05f));
            Assert.That(twoMetrics.Columns[0].BlockThickness,
                Is.EqualTo(firstMetrics.Columns[0].BlockThickness).Within(0.05f));
            Assert.That(twoMetrics.Columns[1].InlineExtent,
                Is.EqualTo(secondMetrics.Columns[0].InlineExtent).Within(0.05f));
            Assert.That(twoMetrics.Columns[1].BlockThickness,
                Is.EqualTo(secondMetrics.Columns[0].BlockThickness).Within(0.05f));
            Assert.That(twoMetrics.PhysicalSize.x,
                Is.EqualTo(firstMetrics.PhysicalSize.x + secondMetrics.PhysicalSize.x).Within(0.05f));
        }

        [Test]
        public void VerticalScrollConvertsPhysicalInputBeforeClampAndElastic()
        {
            Assert.That(TextBox.ResolveScrollXContentDelta(false, 2.5f), Is.EqualTo(2.5f));
            Assert.That(TextBox.ResolveScrollXContentDelta(false, -2.5f), Is.EqualTo(-2.5f));
            Assert.That(TextBox.ResolveScrollXContentDelta(true, 2.5f), Is.EqualTo(-2.5f));
            Assert.That(TextBox.ResolveScrollXContentDelta(true, -2.5f), Is.EqualTo(2.5f));
            Assert.That(TextBox.ResolveScrollXContentDelta(true, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void ManagedWritingModeRejectsInvalidValuesAndInvalidExtents()
        {
            using var paragraph = BuildParagraph(MixedText, VerticalStyle(), TextStyle());
            Assert.Throws<Exception>(() => paragraph.LayoutWithInlineExtent(-1f));
            Assert.Throws<Exception>(() => paragraph.LayoutWithInlineExtent(float.NaN));

            using var style = new ParagraphStyle();
            Assert.That(style.WritingMode, Is.EqualTo(WritingMode.HorizontalTb));
            style.WritingMode = WritingMode.VerticalRl;
            Assert.That(style.WritingMode, Is.EqualTo(WritingMode.VerticalRl));
            Assert.Throws<Exception>(() => style.WritingMode = (WritingMode)99);
        }

        [TestCase("مرحبا بالعالم")]
        [TestCase("שלום עולם")]
        public void StructuralUnsupportedVerticalContentUsesHorizontalFallback(string content)
        {
            var settings = VerticalSettings(content);
            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);

            Assert.That(preparation.UsesHorizontalFallback, Is.True);
            Assert.That(preparation.Settings.ParagraphStyle.writingMode,
                Is.EqualTo(WritingMode.HorizontalTb));
            Assert.That(preparation.Diagnostic, Is.Not.Null);
            Assert.That(preparation.Diagnostic!.Message, Does.Contain("horizontal fallback"));
            Assert.That(preparation.Payload.Blocks.Count, Is.GreaterThan(0));
            Assert.That(settings.ParagraphStyle.writingMode, Is.EqualTo(WritingMode.VerticalRl),
                "Preparation must not modify the caller's style.");
        }

        [TestCase(WritingMode.HorizontalTb, "天地", false)]
        [TestCase(WritingMode.VerticalRl, "天地", true)]
        [TestCase(WritingMode.VerticalRl, "<ruby>漢<rt>かん</rt></ruby>", true)]
        [TestCase(WritingMode.VerticalRl, "مرحبا", false)]
        [TestCase((WritingMode)99, "天地", false)]
        public void ViewportBlockMarginsOnlyApplyToEffectiveVerticalLayout(WritingMode writingMode,
            string content, bool vertical)
        {
            var style = VerticalStyle();
            style.writingMode = writingMode;
            var settings = VerticalSettings(
                "<p style=\"margin-top: 12px; margin-top: 2vw; margin-bottom: 1em; margin-bottom: 3vh\">" +
                content + "</p>", style);
            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
            Assert.That(preparation.Settings.ParagraphStyle.writingMode,
                Is.EqualTo(vertical ? WritingMode.VerticalRl : WritingMode.HorizontalTb));
            var block = preparation.Payload.Blocks[0];
            Assert.That(block.Style.MarginTop.Unit, Is.EqualTo(vertical ? MarginUnit.Vw : MarginUnit.Px));
            Assert.That(block.Style.MarginTop.Value, Is.EqualTo(vertical ? 2f : 12f));
            Assert.That(block.Style.MarginBottom.Unit, Is.EqualTo(vertical ? MarginUnit.Vh : MarginUnit.Em));
            Assert.That(block.Style.MarginBottom.Value, Is.EqualTo(vertical ? 3f : 1f));
        }

        [Test]
        public void LegalVerticalDecorationsAreRetainedWithoutLeavingVerticalLayout()
        {
            var settings = VerticalSettings("<u><s>underline</s></u>");
            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);

            Assert.That(preparation.UsesHorizontalFallback, Is.False);
            Assert.That(preparation.Settings.ParagraphStyle.writingMode,
                Is.EqualTo(WritingMode.VerticalRl));
            Assert.That(preparation.Diagnostic, Is.Null);
            var decorated = preparation.Payload.Blocks[0].Segments;
            Assert.That(decorated, Is.Not.Empty);
            Assert.That(decorated.All(segment => segment.TextStyle?.Underline ?? false), Is.True);
            Assert.That(decorated.All(segment => segment.TextStyle?.Strikethrough ?? false), Is.True);
        }

        [Test]
        public void LegalVerticalDecorationsProduceNativeInkWithoutChangingLayout()
        {
            using var plain = BuildParagraph("漢", VerticalStyle(), TextStyle(36f, 0f));
            using var underlined = BuildParagraph("<u>漢</u>", VerticalStyle(), TextStyle(36f, 0f));
            using var through = BuildParagraph("<s>漢</s>", VerticalStyle(), TextStyle(36f, 0f));
            using var combined = BuildParagraph("<u><s>漢</s></u>", VerticalStyle(), TextStyle(36f, 0f));
            foreach (var paragraph in new[] { plain, underlined, through, combined })
            {
                paragraph.LayoutWithInlineExtent(300f);
            }

            var reference = plain.LayoutMetrics;
            foreach (var paragraph in new[] { underlined, through, combined })
            {
                var metrics = paragraph.LayoutMetrics;
                Assert.That(metrics.PhysicalSize.x, Is.EqualTo(reference.PhysicalSize.x).Within(0.01f));
                Assert.That(metrics.PhysicalSize.y, Is.EqualTo(reference.PhysicalSize.y).Within(0.01f));
                Assert.That(metrics.ContentBounds, Is.EqualTo(reference.ContentBounds));
                Assert.That(metrics.LongestInlineExtent,
                    Is.EqualTo(reference.LongestInlineExtent).Within(0.01f));
                Assert.That(metrics.MinIntrinsicInlineExtent,
                    Is.EqualTo(reference.MinIntrinsicInlineExtent).Within(0.01f));
                Assert.That(metrics.MaxIntrinsicInlineExtent,
                    Is.EqualTo(reference.MaxIntrinsicInlineExtent).Within(0.01f));
                Assert.That(metrics.Columns.Count, Is.EqualTo(reference.Columns.Count));
                Assert.That(metrics.InkBounds.width, Is.GreaterThan(0f));
                Assert.That(metrics.InkBounds.height, Is.GreaterThan(0f));
            }

            foreach (var paragraph in new[] { underlined, combined })
            {
                Assert.That(paragraph.LayoutMetrics.InkBounds.xMin,
                    Is.LessThan(reference.InkBounds.xMin));
            }

            var underlinedInk = underlined.LayoutMetrics.InkBounds;
            var throughInk = through.LayoutMetrics.InkBounds;
            var combinedInk = combined.LayoutMetrics.InkBounds;
            Assert.That(underlinedInk.xMin, Is.LessThan(throughInk.xMin));
            Assert.That(combinedInk.xMin, Is.LessThanOrEqualTo(underlinedInk.xMin));
        }

        [Test]
        public void HardBreakMarkupStaysInsideOneParagraph()
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText("first<br/>second");
            var payload = parser.ConvertToSegments();
            Assert.That(payload.Blocks.Count, Is.EqualTo(1));
            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidatePayload(payload,
                VerticalSettings("first<br/>second")));

            using var paragraph = BuildParagraph("first<br/>second", VerticalStyle(), TextStyle());
            paragraph.LayoutWithInlineExtent(float.PositiveInfinity);
            var metrics = paragraph.LayoutMetrics;
            Assert.That(metrics.Columns.Count, Is.EqualTo(2));
            Assert.That(metrics.Columns[0].HardBreak, Is.True);
            Assert.That(metrics.Columns[1].Utf16Start, Is.EqualTo(6UL));
        }

        [Test]
        public void BidiNeutralPunctuationIsNotRejectedAsStrongRtl()
        {
            const string content = "\u060C漢";
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(content);
            var payload = parser.ConvertToSegments();

            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidatePayload(payload,
                VerticalSettings(content)));

            using var paragraph = BuildParagraph(content, VerticalStyle(), TextStyle());
            Assert.DoesNotThrow(() => paragraph.LayoutWithInlineExtent(float.PositiveInfinity));
            Assert.That(paragraph.LayoutMetrics.Columns, Is.Not.Empty);
        }

        [Test]
        public void HardBreakDoesNotShapeTofuOrAdvanceTheLastVisibleColumn()
        {
            using var plain = BuildParagraph("漢", VerticalStyle(), TextStyle());
            using var lf = BuildParagraph("漢\n", VerticalStyle(), TextStyle());
            using var crlf = BuildParagraph("漢\r\n", VerticalStyle(), TextStyle());

            plain.LayoutWithInlineExtent(float.PositiveInfinity);
            lf.LayoutWithInlineExtent(float.PositiveInfinity);
            crlf.LayoutWithInlineExtent(float.PositiveInfinity);

            var plainMetrics = plain.LayoutMetrics;
            Assert.That(plainMetrics.Columns.Count, Is.EqualTo(1));
            Assert.That(plainMetrics.Columns[0].InlineExtent, Is.GreaterThan(0f));

            foreach (var (paragraph, label) in new[] { (lf, "LF"), (crlf, "CRLF") })
            {
                var metrics = paragraph.LayoutMetrics;
                Assert.That(metrics.Columns.Count, Is.EqualTo(2), label);
                Assert.That(metrics.Columns[0].HardBreak, Is.True, label);
                Assert.That(metrics.Columns[0].InlineExtent,
                    Is.EqualTo(plainMetrics.Columns[0].InlineExtent).Within(0.05f), label);
                Assert.That(metrics.InkBounds.width,
                    Is.EqualTo(plainMetrics.InkBounds.width).Within(0.05f), label);
                Assert.That(metrics.InkBounds.height,
                    Is.EqualTo(plainMetrics.InkBounds.height).Within(0.05f), label);
            }
        }

        [Test]
        public void LineHeightUsesNaturalMetricsForNonPositiveAndFontMultiplierForPositive()
        {
            using var natural = BuildParagraph("漢", VerticalStyle(), TextStyle(32f, 0f));
            using var negative = BuildParagraph("漢", VerticalStyle(), TextStyle(32f, -1f));
            using var scaled = BuildParagraph("漢", VerticalStyle(), TextStyle(32f, 1.5f));

            natural.LayoutWithInlineExtent(float.PositiveInfinity);
            negative.LayoutWithInlineExtent(float.PositiveInfinity);
            scaled.LayoutWithInlineExtent(float.PositiveInfinity);

            var naturalMetrics = natural.LayoutMetrics;
            var negativeMetrics = negative.LayoutMetrics;
            var scaledMetrics = scaled.LayoutMetrics;
            Assert.That(negativeMetrics.PhysicalSize.x,
                Is.EqualTo(naturalMetrics.PhysicalSize.x).Within(0.05f));
            Assert.That(negativeMetrics.Columns[0].BlockThickness,
                Is.EqualTo(naturalMetrics.Columns[0].BlockThickness).Within(0.05f));
            Assert.That(scaledMetrics.PhysicalSize.x, Is.EqualTo(48f).Within(0.05f));
            Assert.That(scaledMetrics.Columns.Count, Is.EqualTo(1));
            Assert.That(scaledMetrics.Columns[0].BlockThickness, Is.EqualTo(48f).Within(0.05f));
            Assert.That(scaledMetrics.LongestInlineExtent,
                Is.EqualTo(naturalMetrics.LongestInlineExtent).Within(0.05f));
        }

        [Test]
        public void LineHeightMultiplierUsesEachResolvedRichTextFontSize()
        {
            using var paragraph = BuildParagraph("<font size=\"40\">漢</font>",
                VerticalStyle(), TextStyle(32f, 1.5f));
            paragraph.LayoutWithInlineExtent(float.PositiveInfinity);

            var metrics = paragraph.LayoutMetrics;
            Assert.That(metrics.Columns.Count, Is.EqualTo(1));
            Assert.That(metrics.Columns[0].BlockThickness, Is.EqualTo(60f).Within(0.05f));
            Assert.That(metrics.PhysicalSize.x, Is.EqualTo(60f).Within(0.05f));
        }

        [Test]
        public void LineHeightRejectsOnlyNonFiniteValues()
        {
            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidateSettings(
                VerticalSettings(MixedText, textStyle: TextStyle(32f, 1.5f))));
            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidateSettings(
                VerticalSettings(MixedText, textStyle: TextStyle(32f, -1f))));

            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var settings = VerticalSettings(MixedText, textStyle: TextStyle(32f, invalid));
                Assert.Throws<NotSupportedException>(() =>
                    TextBoxVerticalLayoutSupport.ValidateSettings(settings));

                var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
                Assert.That(preparation.Settings.TextStyle.lineHeight, Is.EqualTo(0f));
                Assert.That(preparation.Settings.ParagraphStyle.writingMode,
                    Is.EqualTo(WritingMode.VerticalRl));
                Assert.That(preparation.Diagnostic!.Message, Does.Contain("lineHeight="));
            }

            using var paragraph = BuildParagraph(MixedText, VerticalStyle(), TextStyle(32f, 1.5f));
            paragraph.LayoutWithInlineExtent(180f);
            Assert.That(paragraph.Height, Is.EqualTo(180f).Within(0.05f));
            Assert.That(paragraph.LayoutMetrics.Columns, Is.Not.Empty);
        }

        [Test]
        public void ControlCharactersAndTabsFollowVerticalTextSemantics()
        {
            foreach (var content in new[]
                     {
                         "漢\t字", "漢\u00AD字", "漢\u200E字", "漢\u200F字", "漢\u061C字"
                     })
            {
                var settings = VerticalSettings(content, textStyle: TextStyle(32f, 1.5f));
                Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidateSettings(settings));

                var parser = new RichTextParser.RichTextParser();
                parser.ParseText(content);
                var payload = parser.ConvertToSegments();
                Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidatePayload(payload, settings));

                using var paragraph = BuildParagraph(content, VerticalStyle(), TextStyle(32f, 1.5f));
                Assert.DoesNotThrow(() => paragraph.LayoutWithInlineExtent(float.PositiveInfinity));
                var metrics = paragraph.LayoutMetrics;
                Assert.That(metrics.Columns.Count, Is.GreaterThan(0));
                Assert.That(metrics.Columns[0].BlockThickness, Is.EqualTo(48f).Within(8f));
                Assert.That(paragraph.Height, Is.GreaterThan(0f));
            }

            using var ab = BuildParagraph("AB", VerticalStyle(), TextStyle(32f, 1.5f));
            using var space = BuildParagraph("A B", VerticalStyle(), TextStyle(32f, 1.5f));
            using var tab = BuildParagraph("A\tB", VerticalStyle(), TextStyle(32f, 1.5f));
            ab.LayoutWithInlineExtent(float.PositiveInfinity);
            space.LayoutWithInlineExtent(float.PositiveInfinity);
            tab.LayoutWithInlineExtent(float.PositiveInfinity);
            Assert.That(tab.LayoutMetrics.Columns[0].InlineExtent,
                Is.EqualTo(space.LayoutMetrics.Columns[0].InlineExtent).Within(0.05f));
            Assert.That(space.LayoutMetrics.Columns[0].InlineExtent,
                Is.GreaterThan(ab.LayoutMetrics.Columns[0].InlineExtent));
        }

        [Test]
        public void LowLevelValidationRemainsStrict()
        {
            var ellipsis = VerticalSettings(MixedText);
            ellipsis = new TextBoxRenderTargetSettings(ellipsis.Content,
                ellipsis.Margin,
                ellipsis.ParagraphStyle,
                ellipsis.TextStyle,
                ellipsis.ParserId,
                ellipsis.WrapMode,
                TextOverflow.Ellipsis);
            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidateSettings(ellipsis));
            var ellipsisPreparation =
                TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(ellipsis);
            Assert.That(ellipsisPreparation.Diagnostic, Is.Null);
            Assert.That(ellipsisPreparation.Settings.TextOverflow,
                Is.EqualTo(TextOverflow.Ellipsis));

            var rtlStyle = VerticalStyle(TextAlign.Start, TextDirection.Rtl);
            Assert.Throws<NotSupportedException>(() => TextBoxVerticalLayoutSupport.ValidateSettings(
                VerticalSettings(MixedText, rtlStyle)));

            var spacedStyle = VerticalStyle();
            spacedStyle.paragraphSpacing = 8f;
            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidateSettings(
                VerticalSettings(MixedText, spacedStyle)));

            var indented = VerticalSettings("<p style=\"text-indent: 12px\">" + MixedText + "</p>");
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(indented.Content);
            var payload = parser.ConvertToSegments();
            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidatePayload(payload, indented));
            Assert.That(payload.Blocks[0].Style.TextIndent!.Value.Value, Is.EqualTo(12f).Within(0.01f));

            var autoMargin = new Margin
            {
                Left = new MarginValue { Auto = true },
                Top = new MarginValue { Auto = true, Value = 12f, Unit = MarginUnit.Em },
                Right = new MarginValue { Auto = true },
                Bottom = new MarginValue { Auto = true, Value = 25f, Unit = MarginUnit.ContainerPercent },
            };
            var autoSettings = new TextBoxRenderTargetSettings(MixedText,
                autoMargin,
                VerticalStyle(),
                TextStyle(),
                XmlRichTextParser.Id,
                TextBoxWrapMode.Wrap,
                TextOverflow.Clip);
            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidateSettings(autoSettings));
            var autoPreparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(autoSettings);
            Assert.That(autoPreparation.Diagnostic, Is.Null);
            Assert.That(autoPreparation.Settings.Margin.Left.Auto, Is.True);
            Assert.That(autoPreparation.Settings.Margin.Top.Auto, Is.True);
            Assert.That(autoPreparation.Settings.Margin.Top.Value, Is.EqualTo(12f));
            Assert.That(autoPreparation.Settings.Margin.Top.Unit, Is.EqualTo(MarginUnit.Em));
            Assert.That(autoPreparation.Settings.Margin.Right.Auto, Is.True);
            Assert.That(autoPreparation.Settings.Margin.Bottom.Auto, Is.True);
            Assert.That(autoPreparation.Settings.Margin.Bottom.Value, Is.EqualTo(25f));
            Assert.That(autoPreparation.Settings.Margin.Bottom.Unit,
                Is.EqualTo(MarginUnit.ContainerPercent));
        }

        [Test]
        public void SingleLineEllipsisUsesFiniteContentHeightForNativeLayout()
        {
            var margin = new Margin
            {
                Top = new MarginValue { Value = 10f },
                Bottom = new MarginValue { Value = 10f },
            };
            var paragraphStyle = VerticalStyle();
            paragraphStyle.singleLine = true;

            foreach (var wrapMode in new[] { TextBoxWrapMode.Wrap, TextBoxWrapMode.NoWrap })
            {
                var settings = new TextBoxRenderTargetSettings(MixedText,
                    margin,
                    paragraphStyle,
                    TextStyle(36f, 0f),
                    XmlRichTextParser.Id,
                    wrapMode,
                    TextOverflow.Ellipsis);
                using var target = new TextBoxRenderTarget();
                var inlineExtent = target.ResolveVerticalInlineExtent(
                    settings,
                    new Vector2Int(200, 120));
                Assert.That(inlineExtent, Is.EqualTo(100f).Within(0.01f), wrapMode.ToString());

                var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
                Assert.That(preparation.Diagnostic, Is.Null);
                Assert.That(preparation.Settings.TextOverflow,
                    Is.EqualTo(TextOverflow.Ellipsis));
                using var nativeParagraphStyle =
                    preparation.Settings.ParagraphStyle.ToParagraphStyle(true);
                using var nativeTextStyle = preparation.Settings.TextStyle.ToTextStyle();
                var paragraphs = preparation.Payload.ToParagraphs(nativeParagraphStyle,
                    nativeTextStyle);
                Assert.That(paragraphs, Has.Count.EqualTo(1));
                using var paragraph = paragraphs[0];
                paragraph.LayoutWithInlineExtent(inlineExtent);
                var metrics = paragraph.LayoutMetrics;
                Assert.That(metrics.Columns.Count, Is.EqualTo(1));
                Assert.That(metrics.Columns[0].Utf16End,
                    Is.LessThan((ulong)MixedText.Length));
                Assert.That(metrics.Columns[0].InlineExtent,
                    Is.LessThanOrEqualTo(100f + 0.05f));
                Assert.That(metrics.PhysicalSize.y, Is.EqualTo(100f).Within(0.05f));
            }

            var singleLineClip = new TextBoxRenderTargetSettings(MixedText,
                margin,
                paragraphStyle,
                TextStyle(),
                XmlRichTextParser.Id,
                TextBoxWrapMode.NoWrap,
                TextOverflow.Clip);
            using (var target = new TextBoxRenderTarget())
            {
                Assert.That(float.IsPositiveInfinity(target.ResolveVerticalInlineExtent(
                    singleLineClip,
                    new Vector2Int(200, 120))), Is.True);
            }

            var multiLineNoWrap = new TextBoxRenderTargetSettings(MixedText,
                margin,
                VerticalStyle(),
                TextStyle(),
                XmlRichTextParser.Id,
                TextBoxWrapMode.NoWrap,
                TextOverflow.Ellipsis);
            using (var target = new TextBoxRenderTarget())
            {
                Assert.That(float.IsPositiveInfinity(target.ResolveVerticalInlineExtent(
                    multiLineNoWrap,
                    new Vector2Int(200, 120))), Is.True);
            }
        }

        [Test]
        public void AllRecoverableConfigurationIssuesUseOneEffectiveSnapshot()
        {
            const string content = "<u><font size=\"0\">漢</font></u>";
            var paragraphStyle = VerticalStyle(TextAlign.Left, TextDirection.Rtl);
            paragraphStyle.paragraphSpacing = 8f;
            var textStyle = TextStyle(0f, float.NaN);
            textStyle.letterSpacing = -2f;
            textStyle.wordSpacing = float.PositiveInfinity;
            var margin = new Margin
            {
                Left = new MarginValue { Auto = true },
                Top = new MarginValue { Value = 4f },
                Right = new MarginValue { Auto = true },
                Bottom = new MarginValue { Value = 6f },
            };
            var requested = new TextBoxRenderTargetSettings(content,
                margin,
                paragraphStyle,
                textStyle,
                XmlRichTextParser.Id,
                TextBoxWrapMode.Wrap,
                TextOverflow.Ellipsis,
                autoFontSize: true);

            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(requested);
            var effective = preparation.Settings;

            Assert.That(effective.ParagraphStyle.writingMode, Is.EqualTo(WritingMode.VerticalRl));
            Assert.That(effective.ParagraphStyle.textDirection, Is.EqualTo(TextDirection.Ltr));
            Assert.That(effective.ParagraphStyle.textAlign, Is.EqualTo(TextAlign.Start));
            Assert.That(effective.ParagraphStyle.paragraphSpacing, Is.EqualTo(8f));
            Assert.That(effective.TextStyle.size, Is.EqualTo(new TextStyleSettings().size));
            Assert.That(effective.TextStyle.lineHeight, Is.EqualTo(0f));
            Assert.That(effective.TextStyle.letterSpacing, Is.EqualTo(0f));
            Assert.That(effective.TextStyle.wordSpacing, Is.EqualTo(0f));
            Assert.That(effective.TextOverflow, Is.EqualTo(TextOverflow.Ellipsis));
            Assert.That(effective.AutoFontSize, Is.False);
            Assert.That(effective.Margin.Left.Auto, Is.True);
            Assert.That(effective.Margin.Right.Auto, Is.True);
            Assert.That(effective.Margin.Top.Value, Is.EqualTo(4f));
            Assert.That(effective.Margin.Bottom.Value, Is.EqualTo(6f));
            Assert.That(preparation.Payload.Blocks[0].Segments[0].TextStyle?.FontSize, Is.Null);
            Assert.That(preparation.Payload.Blocks[0].Segments[0].TextStyle?.Underline ?? false, Is.True);

            var diagnostic = preparation.Diagnostic!.Message;
            Assert.That(diagnostic, Does.Contain("textDirection=Rtl"));
            Assert.That(diagnostic, Does.Contain("textAlign=Left"));
            Assert.That(diagnostic, Does.Not.Contain("paragraphSpacing=8"));
            Assert.That(diagnostic, Does.Contain("fontSize=0"));
            Assert.That(diagnostic, Does.Contain("lineHeight=NaN"));
            Assert.That(diagnostic, Does.Contain("letterSpacing=-2"));
            Assert.That(diagnostic, Does.Contain("wordSpacing="));
            Assert.That(diagnostic, Does.Not.Contain("margin."));
            Assert.That(diagnostic, Does.Not.Contain("textOverflow=Ellipsis"));
            Assert.That(diagnostic, Does.Contain("auto font sizing"));
            Assert.That(diagnostic, Does.Not.Contain("underline"));
            Assert.That(diagnostic, Does.Not.Contain("strikethrough"));

            Assert.That(requested.ParagraphStyle.textDirection, Is.EqualTo(TextDirection.Rtl));
            Assert.That(requested.ParagraphStyle.textAlign, Is.EqualTo(TextAlign.Left));
            Assert.That(requested.ParagraphStyle.paragraphSpacing, Is.EqualTo(8f));
            Assert.That(requested.TextStyle.size, Is.EqualTo(0f));
            Assert.That(float.IsNaN(requested.TextStyle.lineHeight), Is.True);
            Assert.That(requested.TextStyle.letterSpacing, Is.EqualTo(-2f));
            Assert.That(requested.TextOverflow, Is.EqualTo(TextOverflow.Ellipsis));
            Assert.That(requested.Margin.Left.Auto, Is.True);
            Assert.That(requested.AutoFontSize, Is.True);
            Assert.That(requested.Content, Is.EqualTo(content));
        }

        [Test]
        public void InvalidWritingModesFallBackToHorizontal()
        {
            var style = VerticalStyle();
            style.writingMode = (WritingMode)99;
            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(
                VerticalSettings(MixedText, style));

            Assert.That(preparation.UsesHorizontalFallback, Is.True);
            Assert.That(preparation.Settings.ParagraphStyle.writingMode,
                Is.EqualTo(WritingMode.HorizontalTb));
            Assert.That(preparation.Diagnostic!.Message, Does.Contain("writingMode=99"));
        }

        [Test]
        public void StructuralHorizontalFallbackSanitizesInvalidParagraphEnums()
        {
            const string content =
                "<p><a href=\"first\">مرحبا</a></p><p><ruby>漢<rt>かん</rt></ruby></p>";
            var paragraphStyle = VerticalStyle((TextAlign)99, (TextDirection)99);
            var requested = VerticalSettings(content, paragraphStyle);

            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(requested);

            Assert.That(preparation.UsesHorizontalFallback, Is.True);
            Assert.That(preparation.Settings.ParagraphStyle.writingMode,
                Is.EqualTo(WritingMode.HorizontalTb));
            Assert.That(preparation.Settings.ParagraphStyle.textDirection,
                Is.EqualTo(TextDirection.Ltr));
            Assert.That(preparation.Settings.ParagraphStyle.textAlign,
                Is.EqualTo(TextAlign.Start));
            Assert.That(preparation.Payload.Blocks.Count, Is.EqualTo(2));
            Assert.That(preparation.Payload.Links.Count, Is.EqualTo(1));
            Assert.That(preparation.Payload.RubyAnnotations.Count, Is.EqualTo(1));
            Assert.That(preparation.Diagnostic!.Message, Does.Contain("strong RTL content"));
            Assert.That(preparation.Diagnostic.Message, Does.Contain("textDirection=99"));
            Assert.That(preparation.Diagnostic.Message, Does.Contain("textAlign=99"));
            Assert.That(requested.ParagraphStyle.textDirection, Is.EqualTo((TextDirection)99));
            Assert.That(requested.ParagraphStyle.textAlign, Is.EqualTo((TextAlign)99));
        }

        [Test]
        public void SharedStylesAreIsolatedAcrossDegradedAndRecoveredPreparations()
        {
            var sharedParagraphStyle = VerticalStyle();
            var sharedTextStyle = TextStyle(32f, float.NaN);
            var requestedA = VerticalSettings(LinkedMixedText, sharedParagraphStyle, sharedTextStyle);
            var requestedB = VerticalSettings("<p>" + MixedText + "</p>",
                sharedParagraphStyle,
                sharedTextStyle);

            var firstA = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(requestedA);
            var firstB = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(requestedB);

            Assert.That(firstA.Settings.TextStyle, Is.Not.SameAs(firstB.Settings.TextStyle));
            Assert.That(firstA.Settings.TextStyle.lineHeight, Is.EqualTo(0f));
            Assert.That(firstB.Settings.TextStyle.lineHeight, Is.EqualTo(0f));
            Assert.That(float.IsNaN(sharedTextStyle.lineHeight), Is.True,
                "A degraded component must not write its fallback back into the shared style.");
            Assert.That(firstA.Diagnostic!.Message, Does.Contain("lineHeight=NaN"));
            Assert.That(firstB.Diagnostic!.Message, Does.Contain("lineHeight=NaN"));

            sharedTextStyle.lineHeight = 1.5f;
            var recoveredA = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(requestedA);
            var recoveredB = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(requestedB);

            Assert.That(recoveredA.Diagnostic, Is.Null);
            Assert.That(recoveredB.Diagnostic, Is.Null);
            Assert.That(recoveredA.Settings.TextStyle.lineHeight, Is.EqualTo(1.5f));
            Assert.That(recoveredB.Settings.TextStyle.lineHeight, Is.EqualTo(1.5f));
            Assert.That(recoveredA.Settings.TextStyle, Is.Not.SameAs(recoveredB.Settings.TextStyle));

            sharedTextStyle.lineHeight = float.NaN;
            var degradedAgain = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(requestedA);
            Assert.That(degradedAgain.Diagnostic!.Message, Does.Contain("lineHeight=NaN"));
            Assert.That(float.IsNaN(sharedTextStyle.lineHeight), Is.True);
        }

        [Test]
        public void SharedAutoMarginsAreClonedAndIsolatedDuringPreparation()
        {
            var shared = new Margin
            {
                Left = new MarginValue { Auto = true },
                Top = new MarginValue { Value = 7f },
                Right = new MarginValue { Auto = true, Value = 12f, Unit = MarginUnit.Vw },
                Bottom = new MarginValue { Value = 9f },
            };
            var firstStyle = VerticalStyle(TextAlign.End);
            var secondStyle = VerticalStyle(TextAlign.Center, TextDirection.Rtl);
            var firstRequest = new TextBoxRenderTargetSettings(MixedText,
                shared,
                firstStyle,
                TextStyle(),
                XmlRichTextParser.Id,
                TextBoxWrapMode.Wrap,
                TextOverflow.Clip);
            var secondRequest = new TextBoxRenderTargetSettings(MixedText,
                shared,
                secondStyle,
                TextStyle(),
                XmlRichTextParser.Id,
                TextBoxWrapMode.Wrap,
                TextOverflow.Clip);

            var first = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(firstRequest).Settings;
            var second = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(secondRequest).Settings;
            Assert.That(first.Margin, Is.Not.SameAs(shared));
            Assert.That(second.Margin, Is.Not.SameAs(shared));
            Assert.That(first.Margin, Is.Not.SameAs(second.Margin));
            Assert.That(first.Margin.Left.Auto, Is.True);
            Assert.That(first.Margin.Right.Auto, Is.True);
            Assert.That(first.Margin.Right.Value, Is.EqualTo(12f));
            Assert.That(first.Margin.Right.Unit, Is.EqualTo(MarginUnit.Vw));
            Assert.That(second.Margin.Left.Auto, Is.True);
            Assert.That(second.Margin.Right.Auto, Is.True);

            first.Margin.Left = new MarginValue { Value = 99f };
            first.Margin.Bottom.Value = 42f;
            Assert.That(second.Margin.Left.Auto, Is.True);
            Assert.That(second.Margin.Bottom.Value, Is.EqualTo(9f));
            Assert.That(shared.Left.Auto, Is.True);
            Assert.That(shared.Left.Value, Is.EqualTo(0f));
            Assert.That(shared.Right.Auto, Is.True);
            Assert.That(shared.Right.Value, Is.EqualTo(12f));
            Assert.That(shared.Right.Unit, Is.EqualTo(MarginUnit.Vw));
            Assert.That(shared.Bottom.Value, Is.EqualTo(9f));
        }

        [Test]
        public void ConfigurationDiagnosticsDeduplicateAndReturnAfterRecovery()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Automatic RenderTexture diagnostics require a graphics device.");
            var viewport = TextBoxRenderViewport.Fixed(new Vector2Int(200, 100),
                default,
                Vector2.zero,
                Vector2.zero);
            var invalid = VerticalSettings(LinkedMixedText, textStyle: TextStyle(32f, float.NaN));
            var valid = VerticalSettings(LinkedMixedText, textStyle: TextStyle(32f, 1.5f));
            using var target = new TextBoxRenderTarget();

            LogAssert.Expect(LogType.Exception, new Regex("lineHeight=NaN defaulted to 0"));
            target.Rebuild(viewport, ColorSpace.Gamma, invalid, true, null);
            target.Rebuild(viewport, ColorSpace.Gamma, invalid, false, null);
            LogAssert.NoUnexpectedReceived();

            target.Rebuild(viewport, ColorSpace.Gamma, valid, true, null);
            LogAssert.NoUnexpectedReceived();

            LogAssert.Expect(LogType.Exception, new Regex("lineHeight=NaN defaulted to 0"));
            target.Rebuild(viewport, ColorSpace.Gamma, invalid, true, null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void IntrinsicMeasurementRejectsVerticalLayout()
        {
            var settings = VerticalSettings(MixedText);
            Assert.Throws<NotSupportedException>(() => new TextBoxIntrinsicMeasurement(settings));
        }

        [Test]
        public void VerticalTextIndentUsesFirstColumnGeometryAcrossUnitsAndQueries()
        {
            using var unit = BuildParagraph("天", VerticalStyle(), TextStyle());
            unit.LayoutWithInlineExtent(float.PositiveInfinity);
            var advance = unit.LayoutMetrics.LongestInlineExtent;
            Assert.That(advance, Is.GreaterThan(0f));

            using var positive = BuildParagraph(
                "<p style=\"text-indent: 24px\">天地玄黃天地玄黃</p>",
                VerticalStyle(), TextStyle());
            positive.LayoutWithInlineExtent(advance * 3f);
            var positiveMetrics = positive.LayoutMetrics;
            Assert.That(positiveMetrics.Columns.Count, Is.GreaterThan(1));
            Assert.That(positiveMetrics.Columns[0].PhysicalBounds.yMin,
                Is.EqualTo(24f).Within(0.05f));
            for (var index = 1; index < positiveMetrics.Columns.Count; ++index)
            {
                Assert.That(positiveMetrics.Columns[index].PhysicalBounds.yMin,
                    Is.EqualTo(0f).Within(0.05f));
            }
            Assert.That(positive.GetRectsForRange(0, 1)[0].yMin,
                Is.EqualTo(24f).Within(0.05f));
            var secondStart = (int)positiveMetrics.Columns[1].Utf16Start;
            Assert.That(positive.GetRectsForRange(secondStart, secondStart + 1)[0].yMin,
                Is.EqualTo(0f).Within(0.05f));

            using var negative = BuildParagraph(
                "<p style=\"text-indent: -30px\">天</p>", VerticalStyle(), TextStyle());
            negative.LayoutWithInlineExtent(300f);
            Assert.That(negative.LayoutMetrics.Columns[0].PhysicalBounds.yMin,
                Is.EqualTo(-30f).Within(0.05f));
            Assert.That(negative.GetRectsForRange(0, 1)[0].yMin,
                Is.EqualTo(-30f).Within(0.05f));

            using var percent = BuildParagraph(
                "<p style=\"text-indent: 25%\">天地玄黃天地</p>",
                VerticalStyle(), TextStyle());
            percent.LayoutWithInlineExtent(advance * 4f);
            using var pixels = BuildParagraph(
                "<p style=\"text-indent: " + advance + "px\">天地玄黃天地</p>",
                VerticalStyle(), TextStyle());
            pixels.LayoutWithInlineExtent(advance * 4f);
            var percentMetrics = percent.LayoutMetrics;
            var pixelMetrics = pixels.LayoutMetrics;
            Assert.That(percentMetrics.Columns.Count, Is.EqualTo(pixelMetrics.Columns.Count));
            for (var index = 0; index < percentMetrics.Columns.Count; ++index)
            {
                Assert.That(percentMetrics.Columns[index].PhysicalBounds.yMin,
                    Is.EqualTo(pixelMetrics.Columns[index].PhysicalBounds.yMin).Within(0.05f));
                Assert.That(percentMetrics.Columns[index].InlineExtent,
                    Is.EqualTo(pixelMetrics.Columns[index].InlineExtent).Within(0.05f));
            }

            using var em = BuildParagraph(
                "<p style=\"text-indent: -2em\">天</p>",
                VerticalStyle(), TextStyle(24f, 0f));
            em.LayoutWithInlineExtent(300f);
            Assert.That(em.LayoutMetrics.Columns[0].PhysicalBounds.yMin,
                Is.EqualTo(-48f).Within(0.05f));

            using var childFont = BuildParagraph(
                "<p style=\"text-indent: -2em\"><font size=\"40\">天</font></p>",
                VerticalStyle(), TextStyle(24f, 0f));
            childFont.LayoutWithInlineExtent(300f);
            Assert.That(childFont.LayoutMetrics.Columns[0].PhysicalBounds.yMin,
                Is.EqualTo(-48f).Within(0.05f));

            using var hardBreak = BuildParagraph(
                "<p style=\"text-indent: 24px\">漢<br/>字</p>",
                VerticalStyle(), TextStyle());
            hardBreak.LayoutWithInlineExtent(300f);
            var hardMetrics = hardBreak.LayoutMetrics;
            Assert.That(hardMetrics.Columns.Count, Is.EqualTo(2));
            Assert.That(hardMetrics.Columns[0].PhysicalBounds.yMin,
                Is.EqualTo(24f).Within(0.05f));
            Assert.That(hardMetrics.Columns[1].PhysicalBounds.yMin,
                Is.EqualTo(0f).Within(0.05f));

            using var centered = BuildParagraph(
                "<p style=\"text-indent: 30px\">天</p>",
                VerticalStyle(TextAlign.Center), TextStyle());
            centered.LayoutWithInlineExtent(300f);
            var centeredMetrics = centered.LayoutMetrics;
            var centeredExtent = centeredMetrics.Columns[0].InlineExtent;
            Assert.That(centeredMetrics.Columns[0].PhysicalBounds.yMin,
                Is.EqualTo(30f + (300f - 30f - centeredExtent) * 0.5f).Within(0.05f));
        }

        [Test]
        public void VerticalTextIndentHandlesWrapModesMaxLinesAndDegenerateHeights()
        {
            using var unit = BuildParagraph("天", VerticalStyle(), TextStyle());
            unit.LayoutWithInlineExtent(float.PositiveInfinity);
            var advance = unit.LayoutMetrics.LongestInlineExtent;

            using var noWrapPercent = BuildParagraph(
                "<p style=\"text-indent: 25%\">天地玄黃</p>", VerticalStyle(), TextStyle());
            noWrapPercent.LayoutWithInlineExtent(float.PositiveInfinity);
            Assert.That(noWrapPercent.LayoutMetrics.Columns[0].PhysicalBounds.yMin,
                Is.EqualTo(0f).Within(0.05f));

            var singleLine = VerticalStyle();
            singleLine.singleLine = true;
            using var singleLinePercent = BuildParagraph(
                "<p style=\"text-indent: 25%\">天地玄黃</p>", singleLine, TextStyle());
            singleLinePercent.LayoutWithInlineExtent(float.PositiveInfinity);
            Assert.That(singleLinePercent.LayoutMetrics.Columns.Count, Is.EqualTo(1));
            Assert.That(singleLinePercent.LayoutMetrics.Columns[0].PhysicalBounds.yMin,
                Is.EqualTo(0f).Within(0.05f));

            using var pastHeight = BuildParagraph(
                "<p style=\"text-indent: " + advance * 2f + "px\">天地</p>",
                VerticalStyle(), TextStyle());
            pastHeight.LayoutWithInlineExtent(advance);
            var pastHeightMetrics = pastHeight.LayoutMetrics;
            Assert.That(pastHeightMetrics.Columns.Count, Is.EqualTo(2));
            Assert.That(pastHeightMetrics.Columns[0].PhysicalBounds.yMin,
                Is.EqualTo(advance * 2f).Within(0.05f));
            Assert.That(pastHeightMetrics.Columns[0].InlineExtent,
                Is.EqualTo(advance).Within(0.05f));
            Assert.That(pastHeightMetrics.Columns[1].PhysicalBounds.yMin,
                Is.EqualTo(0f).Within(0.05f));

            using var zeroHeight = BuildParagraph(
                "<p style=\"text-indent: 30px\">天地</p>", VerticalStyle(), TextStyle());
            zeroHeight.LayoutWithInlineExtent(0f);
            var zeroMetrics = zeroHeight.LayoutMetrics;
            Assert.That(zeroMetrics.Columns.Count, Is.EqualTo(2));
            Assert.That(zeroMetrics.Columns[0].PhysicalBounds.yMin,
                Is.EqualTo(30f).Within(0.05f));
            Assert.That(zeroMetrics.Columns[1].PhysicalBounds.yMin,
                Is.EqualTo(0f).Within(0.05f));
            Assert.That(zeroMetrics.PhysicalSize.y, Is.EqualTo(0f).Within(0.05f));

            using var zeroPercent = BuildParagraph(
                "<p style=\"text-indent: 200%\">天</p>", VerticalStyle(), TextStyle());
            zeroPercent.LayoutWithInlineExtent(0f);
            Assert.That(zeroPercent.LayoutMetrics.Columns[0].PhysicalBounds.yMin,
                Is.EqualTo(0f).Within(0.05f));

            using var empty = BuildParagraph(
                "<p style=\"text-indent: 30px\"></p>", VerticalStyle(), TextStyle());
            empty.LayoutWithInlineExtent(300f);
            var emptyMetrics = empty.LayoutMetrics;
            Assert.That(emptyMetrics.Columns.Count, Is.EqualTo(1));
            Assert.That(emptyMetrics.LongestInlineExtent, Is.EqualTo(0f));
            Assert.That(emptyMetrics.InkBounds, Is.EqualTo(Rect.zero));
        }

        [Test]
        public void VerticalTextIndentValidationAcceptsFiniteValuesAndRejectsInvalidOnes()
        {
            foreach (var markup in new[]
                     {
                         "<p style=\"text-indent: 12px\">text</p>",
                         "<p style=\"text-indent: -2em\">text</p>",
                         "<p style=\"text-indent: 25%\">text</p>",
                         "<p style=\"text-indent: -25%\">text</p>",
                         "<p style=\"text-indent: 0\">text</p>",
                     })
            {
                var settings = VerticalSettings(markup);
                var parser = new RichTextParser.RichTextParser();
                parser.ParseText(settings.Content);
                var payload = parser.ConvertToSegments();
                Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidatePayload(payload, settings));
                var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
                Assert.That(preparation.Diagnostic, Is.Null, markup);
            }

            var finitePayload = ParagraphPayload.MakeText("text");
            finitePayload.Blocks[0].Style.TextIndent = TextIndent.Percentage(25f);
            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidatePayload(
                finitePayload, VerticalSettings("text")));

            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var invalidPayload = ParagraphPayload.MakeText("text");
                invalidPayload.Blocks[0].Style.TextIndent = TextIndent.Pixels(invalid);
                Assert.Throws<NotSupportedException>(() =>
                    TextBoxVerticalLayoutSupport.ValidatePayload(invalidPayload,
                        VerticalSettings("text")));

                var invalidEm = ParagraphPayload.MakeText("text");
                invalidEm.Blocks[0].Style.TextIndentEm = invalid;
                Assert.Throws<NotSupportedException>(() =>
                    TextBoxVerticalLayoutSupport.ValidatePayload(invalidEm,
                        VerticalSettings("text")));
            }
        }

        [Test]
        public void EmIndentOverflowDefaultsToZeroAndStillProducesLayout()
        {
            var settings = VerticalSettings(
                "<p style=\"text-indent: 3e38em\">漢</p>");

            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);

            Assert.That(preparation.Diagnostic, Is.Not.Null);
            Assert.That(preparation.Diagnostic!.Message, Does.Contain("textIndentEm"));
            Assert.That(preparation.Payload.Blocks[0].Style.TextIndentEm,
                Is.EqualTo(0f));

            using var paragraphStyle = preparation.Settings.ParagraphStyle.ToParagraphStyle(false);
            using var textStyleObject = preparation.Settings.TextStyle.ToTextStyle();
            var paragraphs = preparation.Payload.ToParagraphs(paragraphStyle, textStyleObject);
            Assert.That(paragraphs, Has.Count.EqualTo(1));
            using var paragraph = paragraphs[0];
            Assert.DoesNotThrow(() => paragraph.LayoutWithInlineExtent(300f));

            var metrics = paragraph.LayoutMetrics;
            Assert.That(metrics.Columns[0].PhysicalBounds.yMin,
                Is.EqualTo(0f).Within(0.05f));
            Assert.That(float.IsFinite(metrics.PhysicalSize.x), Is.True);
            Assert.That(float.IsFinite(metrics.LongestInlineExtent), Is.True);
        }

        [Test]
        public void PlainLinksDoNotIntroduceUnsupportedDecorations()
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(LinkedMixedText);
            var payload = parser.ConvertToSegments();

            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidatePayload(payload,
                VerticalSettings(LinkedMixedText)));
            Assert.That(payload.Links.Count, Is.EqualTo(1));
            Assert.That(payload.Links[0].StartUtf16, Is.EqualTo(0));
            Assert.That(payload.Links[0].EndUtf16, Is.EqualTo(MixedText.Length));
        }

        [TestCase("<sup>天地Ab</sup>", -18f, 18f)]
        [TestCase("<sub>天地Ab</sub>", 9f, 18f)]
        [TestCase("<span voffset=\"60\">天地Ab</span>", -60f, 36f)]
        [TestCase("<span voffset-em=\"-2\">天地Ab</span>", 72f, 36f)]
        public void BaselineShiftSurvivesVerticalPreparation(string markup, float shift, float size)
        {
            var settings = VerticalSettings(markup);
            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
            Assert.That(preparation.Diagnostic, Is.Null);
            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidatePayload(preparation.Payload, settings));
            using var baseStyle = TextStyle().ToTextStyle();
            var segment = preparation.Payload.Blocks[0].Segments[0];
            using var merged = segment.TextStyle.ToTextStyle(baseStyle);
            Assert.That(merged.FontSize, Is.EqualTo(size));
            Assert.That(merged.BaselineShift, Is.EqualTo(shift));
            using var paragraph = BuildParagraph(markup, VerticalStyle(), TextStyle());
            paragraph.LayoutWithInlineExtent(300f);
            Assert.That(paragraph.LayoutMetrics.Columns.Count, Is.EqualTo(1));
            Assert.That(paragraph.LayoutMetrics.PhysicalSize.x, Is.GreaterThanOrEqualTo(size));
            Assert.That(paragraph.LayoutMetrics.InkBounds.width, Is.GreaterThan(0f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteBaselineShiftStillFailsStrictValidation(float shift)
        {
            foreach (var em in new[] { false, true })
            {
                var payload = ParagraphPayload.MakeText("天地");
                var style = payload.Blocks[0].Segments[0].TextStyle;
                if (em) style.BaselineShiftEm = shift;
                else style.BaselineShift = shift;
                Assert.Throws<NotSupportedException>(() =>
                    TextBoxVerticalLayoutSupport.ValidatePayload(payload, VerticalSettings("天地")));
            }
        }

        [TestCase("voffset", "NaN")]
        [TestCase("voffset", "Infinity")]
        [TestCase("voffset-em", "NaN")]
        [TestCase("voffset-em", "-Infinity")]
        public void NonFiniteBaselineShiftRetainsVerticalSanitizerDiagnostics(string attribute, string value)
        {
            var settings = VerticalSettings($"<span {attribute}=\"{value}\">天</span>");
            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
            Assert.That(preparation.Diagnostic, Is.Not.Null);
            Assert.That(preparation.Diagnostic!.Message, Does.Contain("inline baseline shift defaulted to 0"));
            var style = preparation.Payload.Blocks[0].Segments[0].TextStyle;
            Assert.That(style.BaselineShift, Is.EqualTo(0f));
            Assert.That(style.BaselineShiftEm, Is.EqualTo(0f));
            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidatePayload(preparation.Payload, settings));
        }

        [Test]
        public void FiniteEmBaselineShiftOverflowIsNotSilentlySanitized()
        {
            var settings = VerticalSettings("<span voffset-em=\"3e38\">天</span>");
            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
            Assert.That(preparation.Diagnostic, Is.Null);
            Assert.That(preparation.Payload.Blocks[0].Segments[0].TextStyle.BaselineShiftEm,
                Is.EqualTo(3e38f));
            using var paragraph = BuildParagraph(settings.Content, VerticalStyle(), TextStyle());
            Assert.That(() => paragraph.LayoutWithInlineExtent(200f), Throws.Exception);
            Assert.That(() => paragraph.LayoutMetrics, Throws.Exception);
        }

        [TestCase("<p>天地玄黃</p><p>宇宙</p>", 2)]
        [TestCase("天地<p>玄黃</p>宇宙", 3)]
        [TestCase("<p>天地</p><p></p><p>宇宙<br/>洪荒</p>", 3)]
        [TestCase("<p></p>", 1)]
        [TestCase("天地<br/>宇宙", 1)]
        public void MultiBlockFramesAndMeasurementUseAllParagraphs(string content, int count)
        {
            var style = VerticalStyle();
            style.paragraphSpacing = 17f;
            var settings = VerticalSettings(content, style);
            var prepared = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
            Assert.That(prepared.Diagnostic, Is.Null);
            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidatePayload(prepared.Payload, settings));
            using var nativeStyle = style.ToParagraphStyle(false);
            using var textStyle = TextStyle().ToTextStyle();
            var paragraphs = prepared.Payload.ToParagraphs(nativeStyle, textStyle);
            try
            {
                Assert.That(paragraphs.Count, Is.EqualTo(count));
                var layout = TextBoxVerticalBlockLayout.Layout(paragraphs, 90f, 17f);
                var widths = paragraphs.Select(paragraph => paragraph.LayoutMetrics.PhysicalSize.x).ToArray();
                Assert.That(layout.PhysicalSize.x, Is.EqualTo(widths.Sum() + 17f * (count - 1)).Within(0.01f));
                Assert.That(layout.PhysicalSize.y,
                    Is.EqualTo(paragraphs.Max(paragraph => paragraph.LayoutMetrics.PhysicalSize.y)));
                Assert.That(layout.FrameOffsets[count - 1].x, Is.EqualTo(0f).Within(0.01f));
                for (var index = 0; index < count; ++index)
                {
                    Assert.That(layout.FrameOffsets[index].y, Is.Zero);
                    if (index > 0)
                    {
                        Assert.That(layout.FrameOffsets[index - 1].x -
                                    layout.FrameOffsets[index].x - widths[index],
                            Is.EqualTo(17f).Within(0.01f));
                    }
                }
                using var measurement = new TextBoxVerticalIntrinsicMeasurement(settings);
                Assert.That(measurement.Measure(90f).PhysicalWidth, Is.EqualTo(layout.PhysicalSize.x));
                Assert.That(measurement.Measure(90f).PhysicalWidth, Is.EqualTo(layout.PhysicalSize.x));
                using var target = new TextBoxRenderTarget();
                Assert.That(target.Rebuild(TextBoxRenderViewport.Invisible(new Vector2Int(100, 90), default),
                    ColorSpace.Gamma, settings, true, null), Is.True);
                Assert.That(target.IsVertical, Is.True);
                Assert.That(target.ContentSize, Is.EqualTo(layout.PhysicalSize));
            }
            finally
            {
                foreach (var paragraph in paragraphs) paragraph.Dispose();
            }
        }

        [Test]
        public void MultiBlockMarginsAreSupportedWithoutDefaulting()
        {
            var settings = VerticalSettings("<p>天</p><p style=\"margin-top: 12px; margin-bottom: 9px\">地</p>");
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(settings.Content);
            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidatePayload(parser.ConvertToSegments(), settings));
            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
            Assert.That(preparation.UsesHorizontalFallback, Is.False);
            Assert.That(preparation.Diagnostic, Is.Null);
            Assert.DoesNotThrow(() => TextBoxVerticalLayoutSupport.ValidatePayload(preparation.Payload, preparation.Settings));
        }

        [TestCase(MarginUnit.Px, 10f)]
        [TestCase(MarginUnit.Em, 360f)]
        [TestCase(MarginUnit.ContainerPercent, 20f)]
        [TestCase(MarginUnit.Vw, 80f)]
        [TestCase(MarginUnit.Vh, 60f)]
        public void BlockMarginUnitsUseBaseFontExternalHeightAndRootViewport(MarginUnit unit, float expected)
        {
            var settings = new TextBoxRenderTargetSettings("<p>天</p>", new Margin(),
                VerticalStyle(), TextStyle(), XmlRichTextParser.Id, TextBoxWrapMode.Wrap,
                TextOverflow.Clip,
                verticalMarginContext: new MarginResolveContext(123f, 456f, 36f, 800f, 600f,
                    hasViewport: true));
            var margin = new MarginValue { Value = 10f, Unit = unit };
            var resolved = TextBoxVerticalBlockLayout.ResolveMargins(new[] { margin },
                new[] { margin }, 200f, settings);
            Assert.That(resolved[0], Is.EqualTo(new Vector2(expected, expected)));
        }

        [TestCase(TextBoxWrapMode.Wrap, false, TextOverflow.Clip, 80f)]
        [TestCase(TextBoxWrapMode.NoWrap, false, TextOverflow.Clip, 80f)]
        [TestCase(TextBoxWrapMode.Wrap, true, TextOverflow.Clip, 80f)]
        [TestCase(TextBoxWrapMode.NoWrap, true, TextOverflow.Ellipsis, 80f)]
        [TestCase(TextBoxWrapMode.Wrap, false, TextOverflow.Clip, 10f)]
        public void BlockMarginsShareMeasurementFramesBoundsAndEffectiveExtent(TextBoxWrapMode wrap,
            bool singleLine, TextOverflow overflow, float height)
        {
            var style = VerticalStyle();
            style.singleLine = singleLine;
            style.paragraphSpacing = 17f;
            var settings = new TextBoxRenderTargetSettings(
                "<a href=\"both\"><p style=\"margin-top: 12px; margin-bottom: 9px\">天地玄黃宇宙</p>" +
                "<p style=\"margin-top: 5px; margin-bottom: 23px\"></p>" +
                "<p style=\"margin-top: 31px; margin-bottom: 7px\">洪荒日月</p></a>",
                new Margin(), style, TextStyle(), XmlRichTextParser.Id, wrap, overflow);
            var prepared = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
            using var nativeStyle = style.ToParagraphStyle(overflow == TextOverflow.Ellipsis);
            using var nativeTextStyle = TextStyle().ToTextStyle();
            var paragraphs = prepared.Payload.ToParagraphs(nativeStyle, nativeTextStyle);
            try
            {
                var margins = TextBoxVerticalBlockLayout.ResolveMargins(
                    prepared.Payload.Blocks.Select(block => block.Style.MarginTop).ToArray(),
                    prepared.Payload.Blocks.Select(block => block.Style.MarginBottom).ToArray(), height, settings);
                var finite = TextBoxVerticalBlockLayout.UsesFiniteInlineExtent(settings);
                var layout = TextBoxVerticalBlockLayout.Layout(paragraphs,
                    finite ? height : float.PositiveInfinity, 17f, margins);
                var expectedHeight = 0f;
                for (var index = 0; index < paragraphs.Count; ++index)
                {
                    var metrics = paragraphs[index].LayoutMetrics;
                    Assert.That(layout.FrameOffsets[index].y, Is.EqualTo(margins[index].x));
                    expectedHeight = Math.Max(expectedHeight, margins[index].x + metrics.PhysicalSize.y + margins[index].y);
                    using var independent = BuildParagraph(index == 0 ? "天地玄黃宇宙" : index == 1 ? "" : "洪荒日月",
                        style, TextStyle());
                    independent.LayoutWithInlineExtent(finite ? Math.Max(0f, height - margins[index].x - margins[index].y) : float.PositiveInfinity);
                    Assert.That(metrics.PhysicalSize.x, Is.EqualTo(independent.LayoutMetrics.PhysicalSize.x));
                    if (index > 0)
                        Assert.That(layout.FrameOffsets[index - 1].x - layout.FrameOffsets[index].x - metrics.PhysicalSize.x,
                            Is.EqualTo(17f).Within(0.01f));
                }
                Assert.That(layout.PhysicalSize.y, Is.EqualTo(expectedHeight));
                var zeroMargins = TextBoxVerticalBlockLayout.Compose(paragraphs.Select(paragraph => paragraph.LayoutMetrics).ToArray(), 17f);
                Assert.That(layout.PhysicalSize.x, Is.EqualTo(zeroMargins.PhysicalSize.x));
                Assert.That(layout.FrameOffsets.Select(offset => offset.x), Is.EqualTo(zeroMargins.FrameOffsets.Select(offset => offset.x)));
                var visible = paragraphs.Select((paragraph, index) => new { Bounds = paragraph.LayoutMetrics.ContentBounds, Offset = layout.FrameOffsets[index] })
                    .Where(item => item.Bounds.width > 0f && item.Bounds.height > 0f).ToArray();
                if (visible.Length > 0)
                {
                    Assert.That(layout.ContentBounds.yMin, Is.EqualTo(visible.Min(item => item.Bounds.yMin + item.Offset.y)).Within(0.01f));
                    Assert.That(layout.ContentBounds.yMax, Is.EqualTo(visible.Max(item => item.Bounds.yMax + item.Offset.y)).Within(0.01f));
                }
                using var measurement = new TextBoxVerticalIntrinsicMeasurement(settings);
                Assert.That(measurement.Measure(height).PhysicalWidth, Is.EqualTo(layout.PhysicalSize.x));
                Assert.That(measurement.Measure(height).PhysicalHeight, Is.EqualTo(layout.PhysicalSize.y));
                using var target = new TextBoxRenderTarget();
                Assert.That(target.Rebuild(TextBoxRenderViewport.Invisible(new Vector2Int(100, (int)height), default),
                    ColorSpace.Gamma, settings, true, null), Is.True);
                Assert.That(target.ContentSize, Is.EqualTo(layout.PhysicalSize));
            }
            finally
            {
                foreach (var paragraph in paragraphs) paragraph.Dispose();
            }
        }

        [TestCase(-1f, MarginUnit.Px, false)]
        [TestCase(float.NaN, MarginUnit.Em, false)]
        [TestCase(float.PositiveInfinity, MarginUnit.Vh, false)]
        [TestCase(0f, (MarginUnit)999, false)]
        [TestCase(0f, MarginUnit.Px, true)]
        public void InvalidBlockMarginFailsStrictValidation(float value, MarginUnit unit, bool auto)
        {
            var settings = VerticalSettings("<p>天</p>");
            var prepared = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
            prepared.Payload.Blocks[0].Style.MarginTop = new MarginValue { Value = value, Unit = unit, Auto = auto };
            Assert.Throws<NotSupportedException>(() => TextBoxVerticalLayoutSupport.ValidatePayload(prepared.Payload, settings));
        }

        [Test]
        public void EmptyBlockMarginAddsPhysicalHeightWithoutInventingContentBounds()
        {
            var empty = new ParagraphLayoutMetrics(WritingMode.VerticalRl, new Vector2(20f, 0f),
                Rect.zero, Rect.zero, 0f, 0f, 0f, Array.Empty<ParagraphColumnMetrics>());
            var visible = new ParagraphLayoutMetrics(WritingMode.VerticalRl, new Vector2(30f, 40f),
                new Rect(2f, 3f, 25f, 35f), Rect.zero, 40f, 40f, 40f, Array.Empty<ParagraphColumnMetrics>());
            var metrics = new[] { empty, visible };
            var layout = TextBoxVerticalBlockLayout.Compose(metrics, 17f,
                new[] { new Vector2(200f, 300f), new Vector2(11f, 19f) });
            Assert.That(layout.PhysicalSize, Is.EqualTo(new Vector2(67f, 500f)));
            Assert.That(layout.FrameOffsets[0], Is.EqualTo(new Vector2(47f, 200f)));
            Assert.That(layout.FrameOffsets[1], Is.EqualTo(new Vector2(0f, 11f)));
            Assert.That(layout.ContentBounds, Is.EqualTo(new Rect(2f, 14f, 25f, 35f)));
            Assert.Throws<InvalidOperationException>(() => TextBoxVerticalBlockLayout.Compose(metrics,
                17f, new[] { new Vector2(float.MaxValue, float.MaxValue), Vector2.zero }));
        }

        [Test]
        public void DerivedMarginOverflowFailsAndDoesNotPoisonMeasurementCache()
        {
            var settings = VerticalSettings("<p style=\"margin-top: 3e38%\">天</p><p>地</p>");
            using var measurement = new TextBoxVerticalIntrinsicMeasurement(settings);
            Assert.Throws<InvalidOperationException>(() => measurement.Measure(1000f));
            Assert.That(measurement.Measure(0f).PhysicalWidth, Is.GreaterThan(0f));
            Assert.Throws<InvalidOperationException>(() => TextBoxVerticalBlockLayout.ResolveMargins(
                new[] { new MarginValue { Value = float.MaxValue, Unit = MarginUnit.Em } },
                new[] { default(MarginValue) }, 100f, settings));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(-1f)]
        public void InvalidParagraphSpacingIsRejectedStrictlyAndDefaultedForRendering(float spacing)
        {
            var style = VerticalStyle();
            style.paragraphSpacing = spacing;
            Assert.That(style.paragraphSpacing, Is.Zero);
            typeof(ParagraphStyleSettings).GetField("m_paragraphSpacing",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(style, spacing);
            var settings = VerticalSettings("<p>天</p><p>地</p>", style);
            Assert.Throws<NotSupportedException>(() => TextBoxVerticalLayoutSupport.ValidateSettings(settings));
            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
            Assert.That(preparation.Diagnostic, Is.Not.Null);
            Assert.That(preparation.Settings.ParagraphStyle.paragraphSpacing, Is.Zero);
            Assert.That(preparation.UsesHorizontalFallback, Is.False);
        }

        [Test]
        public void MultiBlockStylesKeepIndependentNativeGeometry()
        {
            var blocks = new[]
            {
                "<p style=\"text-indent: 12px; text-align: end\"><u>天地</u></p>",
                "<p style=\"text-align: justify\"><sup>玄</sup>黃宇宙洪荒日月盈昃</p>",
                "<p style=\"text-align: center\"><span voffset=\"-9\">辰</span>宿</p>",
            };
            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(VerticalSettings(string.Concat(blocks)));
            Assert.That(preparation.Diagnostic, Is.Null);
            using var nativeStyle = VerticalStyle().ToParagraphStyle(false);
            using var textStyle = TextStyle(24f, 1.5f).ToTextStyle();
            var paragraphs = preparation.Payload.ToParagraphs(nativeStyle, textStyle);
            try
            {
                var combined = TextBoxVerticalBlockLayout.Layout(paragraphs, 100f, 9f);
                for (var index = 0; index < blocks.Length; ++index)
                {
                    using var single = BuildParagraph(blocks[index], VerticalStyle(), TextStyle(24f, 1.5f));
                    single.LayoutWithInlineExtent(100f);
                    Assert.That(paragraphs[index].LayoutMetrics.PhysicalSize, Is.EqualTo(single.LayoutMetrics.PhysicalSize));
                    Assert.That(paragraphs[index].LayoutMetrics.ContentBounds, Is.EqualTo(single.LayoutMetrics.ContentBounds));
                    Assert.That(paragraphs[index].GetRectsForRange(0, 1), Is.EqualTo(single.GetRectsForRange(0, 1)));
                }
                Assert.That(combined.ContentBounds.width, Is.GreaterThan(0f));
                var largeSpacing = TextBoxVerticalBlockLayout.Compose(
                    paragraphs.Select(paragraph => paragraph.LayoutMetrics).ToArray(), 1e30f);
                Assert.That(largeSpacing.FrameOffsets[2], Is.EqualTo(Vector2.zero));
                Assert.Throws<InvalidOperationException>(() => TextBoxVerticalBlockLayout.Layout(paragraphs, 100f, float.MaxValue));
                Assert.That(TextBoxVerticalBlockLayout.Layout(paragraphs, 100f, 9f).PhysicalSize,
                    Is.EqualTo(combined.PhysicalSize));
            }
            finally
            {
                foreach (var paragraph in paragraphs) paragraph.Dispose();
            }
        }

        [Test]
        public void TC09_TateChuYokoRangesStayBlockLocalAndDoNotChangeBlockSpacing()
        {
            const string annotatedMarkup =
                "<p><span style=\"text-combine-upright: all\">12</span></p><p>天地</p>";
            const string plainMarkup = "<p>12</p><p>天地</p>";
            var annotated = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(
                VerticalSettings(annotatedMarkup));
            Assert.That(annotated.Diagnostic, Is.Null);
            Assert.That(annotated.Payload.TateChuYokoAnnotations, Has.Count.EqualTo(1));
            Assert.That(annotated.Payload.TateChuYokoAnnotations[0].StartUtf16, Is.EqualTo(0));
            Assert.That(annotated.Payload.TateChuYokoAnnotations[0].EndUtf16, Is.EqualTo(2));

            using var nativeStyle = VerticalStyle().ToParagraphStyle(false);
            using var textStyle = TextStyle(40f, 0f).ToTextStyle();
            var annotatedParagraphs = annotated.Payload.ToParagraphs(nativeStyle, textStyle);
            var plainParagraphs = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(
                VerticalSettings(plainMarkup)).Payload.ToParagraphs(nativeStyle, textStyle);
            try
            {
                Assert.That(annotatedParagraphs, Has.Count.EqualTo(2));
                Assert.That(plainParagraphs, Has.Count.EqualTo(2));
                foreach (var paragraph in annotatedParagraphs) paragraph.LayoutWithInlineExtent(400f);
                foreach (var paragraph in plainParagraphs) paragraph.LayoutWithInlineExtent(400f);
                var groupRects = annotatedParagraphs[0].GetRectsForRange(0, 2);
                Assert.That(groupRects, Has.Count.EqualTo(1));
                Assert.That(groupRects[0].height, Is.EqualTo(40f).Within(0.5f));
                Assert.That(annotatedParagraphs[1].LayoutMetrics.PhysicalSize,
                    Is.EqualTo(plainParagraphs[1].LayoutMetrics.PhysicalSize));

                var annotatedBlocks = TextBoxVerticalBlockLayout.Layout(annotatedParagraphs, 400f, 13f);
                var plainBlocks = TextBoxVerticalBlockLayout.Layout(plainParagraphs, 400f, 13f);
                Assert.That(annotatedBlocks.PhysicalSize.x,
                    Is.EqualTo(plainBlocks.PhysicalSize.x).Within(0.01f));
                Assert.That(annotatedBlocks.FrameOffsets[0], Is.EqualTo(plainBlocks.FrameOffsets[0]));
                Assert.That(annotatedBlocks.FrameOffsets[1], Is.EqualTo(plainBlocks.FrameOffsets[1]));
            }
            finally
            {
                foreach (var paragraph in annotatedParagraphs) paragraph.Dispose();
                foreach (var paragraph in plainParagraphs) paragraph.Dispose();
            }
        }

        [Test]
        public void TC10_AutoFontSizeUsesUnscaledTateChuYokoCellAndSqueeze()
        {
            const string markup =
                "<a href=\"fit\"><span style=\"text-combine-upright: all\">ABC</span></a>";
            var settings = new TextBoxRenderTargetSettings(markup, Margin.FixedZero(), VerticalStyle(),
                TextStyle(30f, 0f), XmlRichTextParser.Id, TextBoxWrapMode.Wrap, TextOverflow.Clip,
                true, 15f, 15f);
            using var target = new TextBoxRenderTarget();
            target.Rebuild(TextBoxRenderViewport.Invisible(new Vector2Int(200, 160), default),
                ColorSpace.Gamma, settings, true, null);
            var scale = (float)typeof(TextBoxRenderTarget).GetField("fitScale",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)!.GetValue(target);
            Assert.That(scale, Is.EqualTo(0.5f));

            using var unscaled = BuildParagraph(markup, VerticalStyle(), TextStyle(30f, 0f));
            unscaled.LayoutWithInlineExtent(320f);
            var cell = unscaled.GetRectsForRange(0, 3);
            Assert.That(cell, Has.Count.EqualTo(1));
            Assert.That(cell[0].width, Is.EqualTo(30f).Within(0.5f));
            Assert.That(target.ContentSize.x,
                Is.EqualTo(unscaled.LayoutMetrics.PhysicalSize.x * scale).Within(0.01f));
        }

        [Test]
        public void TC18_TateChuYokoSizingParsesDefaultMixedAndInvalid()
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(
                "<span style=\"text-combine-upright: all\">12</span>");
            var defaults = parser.ConvertToSegments();
            Assert.That(defaults.TateChuYokoAnnotations, Has.Count.EqualTo(1));
            Assert.That(defaults.TateChuYokoAnnotations[0].Sizing,
                Is.EqualTo(TateChuYokoSizing.Squeeze));

            parser = new RichTextParser.RichTextParser();
            parser.ParseText(
                "<span style=\"text-combine-upright: all; --milestro-text-combine-upright-sizing: grow\">ABC</span>" +
                "<span style=\"text-combine-upright: all; --milestro-text-combine-upright-sizing: squeeze\">12</span>");
            var mixed = parser.ConvertToSegments();
            Assert.That(mixed.TateChuYokoAnnotations, Has.Count.EqualTo(2));
            Assert.That(mixed.TateChuYokoAnnotations[0].Sizing,
                Is.EqualTo(TateChuYokoSizing.Grow));
            Assert.That(mixed.TateChuYokoAnnotations[1].Sizing,
                Is.EqualTo(TateChuYokoSizing.Squeeze));

            var invalid = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(VerticalSettings(
                "<span style=\"text-combine-upright: all; --milestro-text-combine-upright-sizing: stretch\">12</span>"));
            Assert.That(invalid.Diagnostic, Is.Not.Null);
            Assert.That(invalid.Diagnostic!.Message,
                Does.Contain("--milestro-text-combine-upright-sizing=stretch defaulted to squeeze"));
            Assert.That(invalid.Payload.TateChuYokoAnnotations, Has.Count.EqualTo(1));
            Assert.That(invalid.Payload.TateChuYokoAnnotations[0].Sizing,
                Is.EqualTo(TateChuYokoSizing.Squeeze));
        }

        [Test]
        public void TC19_GrowColumnWidthEntersPhysicalSizeAndAtomicWrap()
        {
            const string growMarkup =
                "<span style=\"text-combine-upright: all; --milestro-text-combine-upright-sizing: grow\">ABC</span>天";
            const string squeezeMarkup =
                "<span style=\"text-combine-upright: all\">ABC</span>天";
            using var grow = BuildParagraph(growMarkup, VerticalStyle(), TextStyle(40f, 0f));
            using var squeeze = BuildParagraph(squeezeMarkup, VerticalStyle(), TextStyle(40f, 0f));
            grow.LayoutWithInlineExtent(400f);
            squeeze.LayoutWithInlineExtent(400f);
            var growCell = grow.GetRectsForRange(0, 3);
            var squeezeCell = squeeze.GetRectsForRange(0, 3);
            Assert.That(growCell, Has.Count.EqualTo(1));
            Assert.That(squeezeCell, Has.Count.EqualTo(1));
            Assert.That(growCell[0].width, Is.GreaterThan(squeezeCell[0].width + 1f));
            Assert.That(growCell[0].height, Is.EqualTo(40f).Within(0.5f));
            Assert.That(grow.LayoutMetrics.PhysicalSize.x,
                Is.GreaterThan(squeeze.LayoutMetrics.PhysicalSize.x));
            Assert.That(growCell[0].width,
                Is.EqualTo(grow.LayoutMetrics.Columns[0].BlockThickness).Within(0.01f));

            const string wrappedMarkup =
                "天<span style=\"text-combine-upright: all; --milestro-text-combine-upright-sizing: grow\">ABC</span>地";
            using var wrapped = BuildParagraph(wrappedMarkup, VerticalStyle(), TextStyle(40f, 0f));
            wrapped.LayoutWithInlineExtent(50f);
            Assert.That(wrapped.LayoutMetrics.Columns.Count, Is.EqualTo(3));
            Assert.That(wrapped.LayoutMetrics.Columns[1].Utf16Start, Is.EqualTo(1));
            Assert.That(wrapped.LayoutMetrics.Columns[1].Utf16End, Is.EqualTo(4));
        }

        [Test]
        public void TC21_GrowAutoFontSizeUsesNaturalCellAtFitScale()
        {
            const string markup =
                "<a href=\"fit\"><span style=\"text-combine-upright: all; " +
                "--milestro-text-combine-upright-sizing: grow\">ABC</span></a>";
            var settings = new TextBoxRenderTargetSettings(markup, Margin.FixedZero(), VerticalStyle(),
                TextStyle(30f, 0f), XmlRichTextParser.Id, TextBoxWrapMode.Wrap, TextOverflow.Clip,
                true, 15f, 15f);
            using var target = new TextBoxRenderTarget();
            target.Rebuild(TextBoxRenderViewport.Invisible(new Vector2Int(200, 160), default),
                ColorSpace.Gamma, settings, true, null);
            var scale = (float)typeof(TextBoxRenderTarget).GetField("fitScale",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)!.GetValue(target);
            Assert.That(scale, Is.EqualTo(0.5f));

            using var unscaled = BuildParagraph(markup, VerticalStyle(), TextStyle(30f, 0f));
            unscaled.LayoutWithInlineExtent(320f);
            var cell = unscaled.GetRectsForRange(0, 3);
            Assert.That(cell, Has.Count.EqualTo(1));
            Assert.That(cell[0].width, Is.GreaterThan(30f));
            Assert.That(target.ContentSize.x,
                Is.EqualTo(unscaled.LayoutMetrics.PhysicalSize.x * scale).Within(0.01f));
        }

        [Test]
        public void TC21_GrowMultiBlockKeepsSecondBlockMetricsAndSpacing()
        {
            const string growMarkup =
                "<p><span style=\"text-combine-upright: all; --milestro-text-combine-upright-sizing: grow\">ABC</span></p><p>天地</p>";
            const string squeezeMarkup =
                "<p><span style=\"text-combine-upright: all\">ABC</span></p><p>天地</p>";
            var growPreparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(
                VerticalSettings(growMarkup));
            var squeezePreparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(
                VerticalSettings(squeezeMarkup));
            Assert.That(growPreparation.Diagnostic, Is.Null);
            Assert.That(squeezePreparation.Diagnostic, Is.Null);
            using var nativeStyle = VerticalStyle().ToParagraphStyle(false);
            using var textStyle = TextStyle(40f, 0f).ToTextStyle();
            var growParagraphs = growPreparation.Payload.ToParagraphs(nativeStyle, textStyle);
            var squeezeParagraphs = squeezePreparation.Payload.ToParagraphs(nativeStyle, textStyle);
            try
            {
                foreach (var paragraph in growParagraphs) paragraph.LayoutWithInlineExtent(400f);
                foreach (var paragraph in squeezeParagraphs) paragraph.LayoutWithInlineExtent(400f);
                var growBlocks = TextBoxVerticalBlockLayout.Layout(growParagraphs, 400f, 9f);
                var squeezeBlocks = TextBoxVerticalBlockLayout.Layout(squeezeParagraphs, 400f, 9f);
                Assert.That(growBlocks.PhysicalSize.x,
                    Is.GreaterThan(squeezeBlocks.PhysicalSize.x + 1f));
                Assert.That(growBlocks.PhysicalSize.y,
                    Is.EqualTo(squeezeBlocks.PhysicalSize.y).Within(0.01f));
                Assert.That(growParagraphs[1].LayoutMetrics.PhysicalSize,
                    Is.EqualTo(squeezeParagraphs[1].LayoutMetrics.PhysicalSize));
                Assert.That(growBlocks.PhysicalSize.x - squeezeBlocks.PhysicalSize.x,
                    Is.EqualTo(growParagraphs[0].LayoutMetrics.PhysicalSize.x -
                               squeezeParagraphs[0].LayoutMetrics.PhysicalSize.x).Within(0.01f));
            }
            finally
            {
                foreach (var paragraph in growParagraphs) paragraph.Dispose();
                foreach (var paragraph in squeezeParagraphs) paragraph.Dispose();
            }
        }

        [Test]
        public void TC12_ComputedCombineRangesSplitAtPlaceholderAndRuby()
        {
            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(VerticalSettings(
                "<span style=\"text-combine-upright: all\">1<img width=\"20\" height=\"20\"/>" +
                "2<ruby>漢<rt>か</rt></ruby>3</span>"));
            Assert.That(preparation.Diagnostic, Is.Null);
            var ranges = preparation.Payload.TateChuYokoAnnotations;
            Assert.That(ranges, Has.Count.EqualTo(3));
            Assert.That((ranges[0].StartUtf16, ranges[0].EndUtf16), Is.EqualTo((0, 1)));
            Assert.That((ranges[1].StartUtf16, ranges[1].EndUtf16), Is.EqualTo((2, 3)));
            Assert.That((ranges[2].StartUtf16, ranges[2].EndUtf16), Is.EqualTo((4, 5)));
        }

        [Test]
        public void TC12_NoneOverridesInheritedAllAndArbitraryInlineElementsAcceptStyle()
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(
                "<span style=\"text-combine-upright: all\">1<span style=\"text-combine-upright: none\">2</span>3</span>");
            var nested = parser.ConvertToSegments();
            Assert.That(nested.TateChuYokoAnnotations, Has.Count.EqualTo(2));
            Assert.That((nested.TateChuYokoAnnotations[0].StartUtf16,
                nested.TateChuYokoAnnotations[0].EndUtf16), Is.EqualTo((0, 1)));
            Assert.That((nested.TateChuYokoAnnotations[1].StartUtf16,
                nested.TateChuYokoAnnotations[1].EndUtf16), Is.EqualTo((2, 3)));

            parser = new RichTextParser.RichTextParser();
            parser.ParseText(
                "<b style=\"text-combine-upright: all\">12</b><em>X</em>" +
                "<a href=\"anchor\" style=\"text-combine-upright: all\">34</a>");
            var inline = parser.ConvertToSegments();
            Assert.That(inline.TateChuYokoAnnotations, Has.Count.EqualTo(2));
            Assert.That((inline.TateChuYokoAnnotations[0].StartUtf16,
                inline.TateChuYokoAnnotations[0].EndUtf16), Is.EqualTo((0, 2)));
            Assert.That((inline.TateChuYokoAnnotations[1].StartUtf16,
                inline.TateChuYokoAnnotations[1].EndUtf16), Is.EqualTo((3, 5)));
        }

        [Test]
        public void TC12_InvalidCombineValueDefaultsToNoneAndReportsIssue()
        {
            var vertical = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(VerticalSettings(
                "<span style=\"text-combine-upright: digits\">12</span>"));
            Assert.That(vertical.Diagnostic, Is.Not.Null);
            Assert.That(vertical.Diagnostic!.Message,
                Does.Contain("text-combine-upright=digits defaulted to none"));
            Assert.That(vertical.Payload.TateChuYokoAnnotations, Is.Empty);

            var horizontal = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(
                new TextBoxRenderTargetSettings(
                    "<span style=\"text-combine-upright: digits\">12</span>",
                    Margin.FixedZero(),
                    new ParagraphStyleSettings { writingMode = WritingMode.HorizontalTb },
                    TextStyle(30f, 0f),
                    XmlRichTextParser.Id,
                    TextBoxWrapMode.Wrap,
                    TextOverflow.Clip));
            Assert.That(horizontal.Diagnostic, Is.Not.Null);
            Assert.That(horizontal.Diagnostic!.Message,
                Does.Contain("text-combine-upright=digits defaulted to none"));
            Assert.That(horizontal.Payload.TateChuYokoAnnotations, Is.Empty);
        }

        [Test]
        public void OR12_BlockAndInlineOrientationInheritanceEmitsContiguousRanges()
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(
                "<p style=\"text-orientation: upright\">A<b>B</b>" +
                "<span style=\"text-orientation: mixed\">C</span>D</p>" +
                "<p style=\"text-orientation: sideways\">EF</p>");
            var ranges = parser.ConvertToSegments().OrientationAnnotations;
            Assert.That(ranges, Has.Count.EqualTo(3));
            AssertOrientationRange(ranges[0], 0, 2, VerticalTextOrientation.Upright);
            AssertOrientationRange(ranges[1], 3, 4, VerticalTextOrientation.Upright);
            AssertOrientationRange(ranges[2], 4, 6, VerticalTextOrientation.Sideways);

            parser = new RichTextParser.RichTextParser();
            parser.ParseText(
                "<p style=\"text-orientation: sideways\"><a href=\"anchor\">AB</a>" +
                "<em style=\"text-orientation: upright\">C</em></p>");
            var arbitraryInline = parser.ConvertToSegments().OrientationAnnotations;
            Assert.That(arbitraryInline, Has.Count.EqualTo(2));
            AssertOrientationRange(arbitraryInline[0], 0, 2, VerticalTextOrientation.Sideways);
            AssertOrientationRange(arbitraryInline[1], 2, 3, VerticalTextOrientation.Upright);
        }

        [Test]
        public void OR12_InvalidOrientationDefaultsToMixedAndReportsIssue()
        {
            const string markup =
                "<p style=\"text-orientation: upright\">A" +
                "<span style=\"text-orientation: sideways-right\">B</span>C</p>";
            var vertical = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(
                VerticalSettings(markup));
            Assert.That(vertical.Diagnostic, Is.Not.Null);
            Assert.That(vertical.Diagnostic!.Message,
                Does.Contain("text-orientation=sideways-right defaulted to mixed"));
            var ranges = vertical.Payload.OrientationAnnotations;
            Assert.That(ranges, Has.Count.EqualTo(2));
            AssertOrientationRange(ranges[0], 0, 1, VerticalTextOrientation.Upright);
            AssertOrientationRange(ranges[1], 2, 3, VerticalTextOrientation.Upright);

            var horizontal = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(
                new TextBoxRenderTargetSettings(
                    markup,
                    Margin.FixedZero(),
                    new ParagraphStyleSettings { writingMode = WritingMode.HorizontalTb },
                    TextStyle(30f, 0f),
                    XmlRichTextParser.Id,
                    TextBoxWrapMode.Wrap,
                    TextOverflow.Clip));
            Assert.That(horizontal.Diagnostic, Is.Not.Null);
            Assert.That(horizontal.Diagnostic!.Message,
                Does.Contain("text-orientation=sideways-right defaulted to mixed"));
        }

        [Test]
        public void OR09_ManagedSidewaysRangeKeepsTateChuYokoAnnotationButDisablesItsCell()
        {
            const string mixedMarkup =
                "<span style=\"text-combine-upright: all\">ABC</span>";
            const string sidewaysMarkup =
                "<p style=\"text-orientation: sideways\">" + mixedMarkup + "</p>";
            var parsed = new RichTextParser.RichTextParser();
            parsed.ParseText(mixedMarkup);
            var mixedPayload = parsed.ConvertToSegments();
            Assert.That(mixedPayload.TateChuYokoAnnotations, Has.Count.EqualTo(1));
            Assert.That(mixedPayload.OrientationAnnotations, Is.Empty);

            parsed = new RichTextParser.RichTextParser();
            parsed.ParseText(sidewaysMarkup);
            var sidewaysPayload = parsed.ConvertToSegments();
            Assert.That(sidewaysPayload.TateChuYokoAnnotations, Has.Count.EqualTo(1));
            Assert.That(sidewaysPayload.OrientationAnnotations, Has.Count.EqualTo(1));
            AssertOrientationRange(sidewaysPayload.OrientationAnnotations[0], 0, 3,
                VerticalTextOrientation.Sideways);

            using var mixed = BuildParagraph(mixedMarkup, VerticalStyle(), TextStyle(40f, 0f));
            using var sideways = BuildParagraph(sidewaysMarkup, VerticalStyle(), TextStyle(40f, 0f));
            mixed.LayoutWithInlineExtent(400);
            sideways.LayoutWithInlineExtent(400);
            Assert.That(sideways.LayoutMetrics.Columns.Count, Is.EqualTo(1));
            Assert.That(sideways.LayoutMetrics.LongestInlineExtent,
                Is.GreaterThan(mixed.LayoutMetrics.LongestInlineExtent + 10f));
        }

        [Test]
        public void OR13_ManagedOrientationVariantsKeepIndependentCachedLayouts()
        {
            const string uprightMarkup =
                "<p style=\"text-orientation: upright\">ABC</p>";
            const string sidewaysMarkup =
                "<p style=\"text-orientation: sideways\">ABC</p>";
            using var upright = BuildParagraph(uprightMarkup, VerticalStyle(), TextStyle(40f, 0f));
            using var sideways = BuildParagraph(sidewaysMarkup, VerticalStyle(), TextStyle(40f, 0f));
            using var uprightAgain = BuildParagraph(uprightMarkup, VerticalStyle(), TextStyle(40f, 0f));
            upright.LayoutWithInlineExtent(400);
            sideways.LayoutWithInlineExtent(400);
            uprightAgain.LayoutWithInlineExtent(400);

            Assert.That(upright.LayoutMetrics.LongestInlineExtent,
                Is.GreaterThan(sideways.LayoutMetrics.LongestInlineExtent + 10f));
            Assert.That(uprightAgain.LayoutMetrics.LongestInlineExtent,
                Is.EqualTo(upright.LayoutMetrics.LongestInlineExtent).Within(0.01f));
            Assert.That(uprightAgain.LayoutMetrics.PhysicalSize,
                Is.EqualTo(upright.LayoutMetrics.PhysicalSize));
        }

        private static void AssertOrientationRange(OrientationAnnotation annotation,
            int startUtf16, int endUtf16, VerticalTextOrientation orientation)
        {
            Assert.That((annotation.StartUtf16, annotation.EndUtf16, annotation.Orientation),
                Is.EqualTo((startUtf16, endUtf16, orientation)));
        }

        [Test]
        public void VerticalBlockMarginsAddOuterSpacingAndCollapseBetweenColumns()
        {
            var metrics = new[]
            {
                new ParagraphLayoutMetrics(WritingMode.VerticalRl, new Vector2(40f, 100f),
                    new Rect(0f, 0f, 40f, 100f), new Rect(0f, 0f, 40f, 100f),
                    100f, 40f, 100f, Array.Empty<ParagraphColumnMetrics>()),
                new ParagraphLayoutMetrics(WritingMode.VerticalRl, new Vector2(30f, 80f),
                    new Rect(0f, 0f, 30f, 80f), new Rect(0f, 0f, 30f, 80f),
                    80f, 30f, 80f, Array.Empty<ParagraphColumnMetrics>()),
            };
            var inlineMargins = new[] { new Vector2(4f, 6f), new Vector2(8f, 10f) };
            var blockMargins = new[] { new Vector2(20f, 5f), new Vector2(7f, 11f) };

            var layout = TextBoxVerticalBlockLayout.Compose(metrics, 17f,
                inlineMargins, blockMargins);

            Assert.That(layout.PhysicalSize, Is.EqualTo(new Vector2(125f, 110f)));
            Assert.That(layout.FrameOffsets[0], Is.EqualTo(new Vector2(65f, 4f)));
            Assert.That(layout.FrameOffsets[1], Is.EqualTo(new Vector2(11f, 8f)));
        }

        [Test]
        public void VerticalIntrinsicWidthIncludesCollapsedRightAndLeftMargins()
        {
            var baselineStyle = VerticalStyle();
            baselineStyle.paragraphSpacing = 17f;
            var marginedStyle = VerticalStyle();
            marginedStyle.paragraphSpacing = 17f;
            using var baseline = new TextBoxVerticalIntrinsicMeasurement(
                VerticalSettings("<p>天</p><p>地</p>", baselineStyle));
            using var margined = new TextBoxVerticalIntrinsicMeasurement(VerticalSettings(
                "<p style=\"margin-right: 20px; margin-left: 5px\">天</p>" +
                "<p style=\"margin-right: 7px; margin-left: 11px\">地</p>", marginedStyle));

            var expected = baseline.Measure(100f).PhysicalWidth;
            var actual = margined.Measure(100f).PhysicalWidth;

            Assert.That(actual - expected, Is.EqualTo(38f).Within(0.01f));
        }

        private static Paragraph BuildParagraph(string content,
            ParagraphStyleSettings paragraphSettings,
            TextStyleSettings textSettings)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(content);
            var payload = parser.ConvertToSegments();
            using var paragraphStyle = paragraphSettings.ToParagraphStyle(false);
            using var textStyle = textSettings.ToTextStyle();
            var paragraphs = payload.ToParagraphs(paragraphStyle, textStyle);
            Assert.That(paragraphs, Has.Count.EqualTo(1));
            return paragraphs[0];
        }

        private static TextBoxRenderTargetSettings VerticalSettings(string content,
            ParagraphStyleSettings? paragraphStyle = null,
            TextStyleSettings? textStyle = null)
        {
            return new TextBoxRenderTargetSettings(content,
                new Margin(),
                paragraphStyle ?? VerticalStyle(),
                textStyle ?? TextStyle(),
                XmlRichTextParser.Id,
                TextBoxWrapMode.Wrap,
                TextOverflow.Clip);
        }

        private static ParagraphStyleSettings VerticalStyle(TextAlign align = TextAlign.Start,
            TextDirection direction = TextDirection.Ltr)
        {
            return new ParagraphStyleSettings
            {
                writingMode = WritingMode.VerticalRl,
                textAlign = align,
                textDirection = direction,
            };
        }

        private static TextStyleSettings TextStyle()
        {
            return TextStyle(36f, 0f);
        }

        private static TextStyleSettings TextStyle(float size, float lineHeight)
        {
            return new TextStyleSettings
            {
                size = size,
                lineHeight = lineHeight,
                locale = "ja",
            };
        }
    }
}

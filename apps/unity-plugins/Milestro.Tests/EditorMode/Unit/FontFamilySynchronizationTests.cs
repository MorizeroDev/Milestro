using System.Collections.Generic;
using Milestro.Configuration;
using Milestro.Extensions;
using Milestro.RichTextParser;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class FontFamilySynchronizationTests
    {
        private MilestroConfiguration previous;

        [SetUp]
        public void SetUp()
        {
            previous = MilestroConfiguration.Configuration;
            MilestroConfiguration.Configuration = new MilestroConfiguration();
        }

        [TearDown]
        public void TearDown()
        {
            MilestroConfiguration.Configuration = previous;
            using var font = FontRegistry.ResolveFont("system-ui");
        }

        [Test]
        public void InPlaceCollectionReplacementAndDeletionReachResolution()
        {
            var configuration = MilestroConfiguration.Configuration.FontFamily;
            configuration.SetKeywordMapping("phase10", "system-ui");
            AssertResolvesLike("phase10", "system-ui");
            configuration.SetKeywordMapping("phase10", "serif");
            AssertResolvesLike("phase10", "serif");
            Assert.That(configuration.TryGetKeywordMapping("phase10", out var families), Is.True);
            ((IList<FontFamilyToken>)families)[0] = FontFamilyToken.Bare("monospace");
            AssertResolvesLike("phase10", "monospace");
            configuration.SetKeywordMappings(new Dictionary<string, string[]> { ["phase10"] = new[] { "sans-serif" } });
            AssertResolvesLike("phase10", "sans-serif");
            configuration.ResetKeywordMapping("phase10");
            AssertResolvesLike("phase10", "phase10");
            configuration.SetKeywordMapping("phase10", "serif");
            AssertResolvesLike("phase10", "serif");
            configuration.ResetKeywordMappings();
            AssertResolvesLike("phase10", "phase10");
        }

        [Test]
        public void ConfigurationReplacementNullAndEquivalentValuesReachResolution()
        {
            foreach (var family in new[] { "serif", "serif", "monospace" })
            {
                var configuration = new FontFamilyConfiguration();
                configuration.SetKeywordMapping(" PHASE10 ", family);
                MilestroConfiguration.Configuration.FontFamily = configuration;
                AssertResolvesLike("phase10", family);
            }
            MilestroConfiguration.Configuration.FontFamily = null;
            AssertResolvesLike("phase10", "phase10");
            MilestroConfiguration.Configuration = new MilestroConfiguration();
            MilestroConfiguration.Configuration.FontFamily.SetKeywordMapping("phase10", "serif");
            AssertResolvesLike("phase10", "serif");
            MilestroConfiguration.Configuration = null;
            AssertResolvesLike("phase10", "phase10");
        }

        [Test]
        public void ProjectionAndStrutSynchronizationPreserveUsableParagraphs()
        {
            MilestroConfiguration.Configuration.FontFamily.SetKeywordMapping("phase10", "system-ui");
            using var source = new TextStyle { FontSize = 16 };
            source.SetFontFamilies(new List<string> { "phase10" });
            var state = new TextStyleState();
            using var strut = new StrutStyle();
            strut.SetFontFamilies(new List<string> { "phase10" });
            for (var index = 0; index < 3; index++)
            {
                using var projected = state.ToTextStyle(source);
                using var paragraphStyle = new ParagraphStyle();
                paragraphStyle.SetTextStyle(projected);
                using var builder = new ParagraphBuilder(paragraphStyle);
                builder.PushStyle(projected);
                builder.AddText("Ab12CD34");
                builder.Pop();
                using var paragraph = builder.Build();
                paragraph.Layout(400);
                Assert.That(paragraph.Height, Is.GreaterThan(0));
                Assert.That(paragraph.ResolveNoWrapContentWidth("Ab12CD34"), Is.GreaterThan(0));
            }
        }

        private static void AssertResolvesLike(string alias, string expected)
        {
            using var actual = FontRegistry.ResolveFont(alias, fallbackToSystem: false);
            using var reference = FontRegistry.ResolveFont(expected, fallbackToSystem: false);
            Assert.That(actual.MeasureText("Wi汉Ab12").AdvanceX,
                Is.EqualTo(reference.MeasureText("Wi汉Ab12").AdvanceX).Within(0.001f));
        }
    }
}

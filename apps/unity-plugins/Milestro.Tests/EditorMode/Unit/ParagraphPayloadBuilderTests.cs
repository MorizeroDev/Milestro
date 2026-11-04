using Milestro.RichTextParser;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class ParagraphPayloadBuilderTests
    {
        [Test]
        public void AppendTextWithoutStyleCreatesAnonymousBlock()
        {
            var payload = new ParagraphPayloadBuilder()
                .AppendText("hello")
                .Build();

            Assert.That(payload.Blocks.Count, Is.EqualTo(1));
            Assert.That(payload.GetPlainText(payload.Blocks[0]), Is.EqualTo("hello"));
            Assert.That(payload.Blocks[0].IsExplicit, Is.False);
        }

        [Test]
        public void BeginBlockCreatesStackedBlocksWithUtf16Offsets()
        {
            var payload = new ParagraphPayloadBuilder()
                .BeginBlock()
                .AppendText("ab")
                .BeginBlock()
                .AppendText("cde")
                .Build();

            Assert.That(payload.Blocks.Count, Is.EqualTo(2));
            Assert.That(payload.GetPlainText(payload.Blocks[0]), Is.EqualTo("ab"));
            Assert.That(payload.GetPlainText(payload.Blocks[1]), Is.EqualTo("cde"));
            Assert.That(payload.Blocks[1].StartUtf16, Is.EqualTo(2));
            Assert.That(payload.Blocks[1].EndUtf16, Is.EqualTo(5));
            Assert.That(payload.Blocks[0].IsExplicit, Is.True);
        }

        [Test]
        public void LinkRangeTracksUtf16Offsets()
        {
            var payload = new ParagraphPayloadBuilder()
                .AppendText("see ")
                .BeginLink("https://example.com", "ref")
                .AppendText("details")
                .EndLink()
                .AppendText("!")
                .Build();

            Assert.That(payload.Links.Count, Is.EqualTo(1));
            var link = payload.Links[0];
            Assert.That(link.Href, Is.EqualTo("https://example.com"));
            Assert.That(link.Id, Is.EqualTo("ref"));
            Assert.That(link.StartUtf16, Is.EqualTo(4));
            Assert.That(link.EndUtf16, Is.EqualTo(11));
        }

        [Test]
        public void BuildClosesUnterminatedLink()
        {
            var payload = new ParagraphPayloadBuilder()
                .BeginLink("open")
                .AppendText("x")
                .Build();

            Assert.That(payload.Links.Count, Is.EqualTo(1));
            Assert.That(payload.Links[0].EndUtf16, Is.EqualTo(1));
        }

        [Test]
        public void NestedLinksThrow()
        {
            var builder = new ParagraphPayloadBuilder().BeginLink("outer");

            Assert.Throws<System.InvalidOperationException>(() => builder.BeginLink("inner"));
        }

        [Test]
        public void AppendPlaceholderAdvancesOneUtf16Char()
        {
            var payload = new ParagraphPayloadBuilder()
                .AppendText("a")
                .AppendPlaceholder(16f, 16f, "icon")
                .AppendText("b")
                .Build();

            Assert.That(payload.Placeholders.Count, Is.EqualTo(1));
            var placeholder = payload.Placeholders[0];
            Assert.That(placeholder.StartUtf16, Is.EqualTo(1));
            Assert.That(placeholder.EndUtf16, Is.EqualTo(2));
            Assert.That(placeholder.Key, Is.EqualTo("icon"));
            // GetPlainText inserts U+FFFC for each placeholder inside the block.
            Assert.That(payload.GetPlainText(payload.Blocks[0]), Is.EqualTo("a\uFFFCb"));
        }

        [Test]
        public void Utf16OffsetsRemainConsistentAcrossCjkEmojiAndBlocks()
        {
            const string prefix = "曲😀";
            const string linked = "链😀";
            var payload = new ParagraphPayloadBuilder()
                .BeginBlock()
                .AppendText(prefix)
                .BeginLink("target")
                .AppendText(linked)
                .EndLink()
                .AppendPlaceholder(16f, 16f, "icon")
                .BeginBlock()
                .AppendText("尾")
                .Build();

            var linkEnd = prefix.Length + linked.Length;
            var secondBlockStart = linkEnd + 1;
            Assert.That(payload.Links[0].StartUtf16, Is.EqualTo(prefix.Length));
            Assert.That(payload.Links[0].EndUtf16, Is.EqualTo(linkEnd));
            Assert.That(payload.Placeholders[0].StartUtf16, Is.EqualTo(linkEnd));
            Assert.That(payload.Blocks[0].EndUtf16, Is.EqualTo(secondBlockStart));
            Assert.That(payload.Blocks[1].StartUtf16, Is.EqualTo(secondBlockStart));
            Assert.That(payload.Blocks[1].EndUtf16, Is.EqualTo(secondBlockStart + 1));
        }

        [Test]
        public void LinksUseDocumentOrderOccurrencesAcrossBlocks()
        {
            var payload = new ParagraphPayloadBuilder()
                .BeginLink("same", "same")
                .AppendText("one")
                .EndLink()
                .BeginBlock()
                .BeginLink("same", "same")
                .AppendText("two")
                .EndLink()
                .Build();

            Assert.That(payload.Links.Count, Is.EqualTo(2));
            Assert.That(payload.Links[0].Occurrence, Is.EqualTo(0));
            Assert.That(payload.Links[1].Occurrence, Is.EqualTo(1));
            Assert.That(payload.Links[1].StartUtf16, Is.EqualTo(3));
            Assert.That(payload.Links[1].EndUtf16, Is.EqualTo(6));
        }

        [Test]
        public void AppendPlaceholderRejectsInvalidSize()
        {
            var builder = new ParagraphPayloadBuilder();

            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                builder.AppendPlaceholder(0f, 16f, "icon"));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                builder.AppendPlaceholder(float.NaN, 16f, "icon"));
        }
    }
}

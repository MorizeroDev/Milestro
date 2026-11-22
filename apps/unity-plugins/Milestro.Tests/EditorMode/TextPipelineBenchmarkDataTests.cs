using System.Collections.Generic;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class TextPipelineBenchmarkDataTests
    {
        [Test]
        public void PairedRunsHaveIdenticalCorpusAndSeedChangesTheChecksum()
        {
            var first = new TextPipelineBenchmarkData(7, 9, 12345, TextPipelineBenchmarkData.Alphanumeric);
            var second = new TextPipelineBenchmarkData(7, 9, 12345, TextPipelineBenchmarkData.Alphanumeric);
            var otherSeed = new TextPipelineBenchmarkData(7, 9, 12346, TextPipelineBenchmarkData.Alphanumeric);
            Assert.That(second.Checksum, Is.EqualTo(first.Checksum));
            Assert.That(second.Frames, Is.EqualTo(first.Frames));
            Assert.That(otherSeed.Checksum, Is.Not.EqualTo(first.Checksum));
        }

        [Test]
        public void PrewarmCoversTheWholeMeasuredAlphabetWithTheSameTextLength()
        {
            var data = new TextPipelineBenchmarkData(3, 9, 12345, TextPipelineBenchmarkData.Alphanumeric);
            var prewarmed = new HashSet<char>();
            foreach (var text in data.Prewarm)
            {
                Assert.That(text.Length, Is.EqualTo(TextPipelineBenchmarkData.TextLength));
                foreach (var character in text)
                {
                    prewarmed.Add(character);
                }
            }
            foreach (var text in data.Frames)
            {
                foreach (var character in text)
                {
                    Assert.That(prewarmed.Contains(character), Is.True);
                }
            }
            Assert.That(prewarmed.Count, Is.EqualTo(TextPipelineBenchmarkData.Alphanumeric.Length));
        }

        [Test]
        public void PercentilesInterpolateAndDoNotMutateFrameOrder()
        {
            var samples = new[] { 9d, 1d, 5d, 3d };
            Assert.That(TextPipelineBenchmarkData.Percentile(samples, 0.5), Is.EqualTo(4d));
            Assert.That(TextPipelineBenchmarkData.Percentile(samples, 0.95), Is.EqualTo(8.4d).Within(0.00001));
            Assert.That(samples, Is.EqualTo(new[] { 9d, 1d, 5d, 3d }));
        }
    }
}

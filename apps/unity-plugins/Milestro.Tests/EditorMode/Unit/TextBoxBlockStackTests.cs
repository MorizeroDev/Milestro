
using System.Collections.Generic;
using Milestro.Components.Internal;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class TextBoxBlockStackTests
    {
        [Test]
        public void SingleBlockStartsAtItsTopMargin()
        {
            var tops = new List<float>();
            var total = TextBoxBlockStack.Layout(new[] { 40f },
                new[] { 8f },
                new[] { 12f },
                0f,
                tops);

            Assert.That(tops, Is.EqualTo(new[] { 8f }));
            Assert.That(total, Is.EqualTo(60f));
        }

        [Test]
        public void AdjacentMarginsCollapseToTheLargerOne()
        {
            var tops = new List<float>();
            var total = TextBoxBlockStack.Layout(new[] { 40f, 30f },
                new[] { 0f, 20f },
                new[] { 12f, 0f },
                0f,
                tops);

            Assert.That(tops, Is.EqualTo(new[] { 0f, 60f }));
            Assert.That(total, Is.EqualTo(90f));
        }

        [Test]
        public void ParagraphSpacingAddsBetweenBlocksOnly()
        {
            var tops = new List<float>();
            var total = TextBoxBlockStack.Layout(new[] { 10f, 10f, 10f },
                new[] { 0f, 0f, 0f },
                new[] { 0f, 0f, 0f },
                5f,
                tops);

            Assert.That(tops, Is.EqualTo(new[] { 0f, 15f, 30f }));
            Assert.That(total, Is.EqualTo(40f));
        }

        [Test]
        public void SpacingAndCollapsedMarginsAccumulate()
        {
            var tops = new List<float>();
            var total = TextBoxBlockStack.Layout(new[] { 10f, 10f },
                new[] { 2f, 4f },
                new[] { 6f, 8f },
                3f,
                tops);

            Assert.That(tops, Is.EqualTo(new[] { 2f, 21f }));
            Assert.That(total, Is.EqualTo(39f));
        }

        [Test]
        public void NegativeAndNonFiniteValuesAreSanitized()
        {
            var tops = new List<float>();
            var total = TextBoxBlockStack.Layout(new[] { -5f, float.NaN, 10f },
                new[] { -1f, 0f, 0f },
                new[] { 0f, float.PositiveInfinity, 0f },
                -2f,
                tops);

            Assert.That(tops, Is.EqualTo(new[] { 0f, 0f, 0f }));
            Assert.That(total, Is.EqualTo(10f));
        }

        [Test]
        public void EmptyInputProducesZeroHeight()
        {
            var tops = new List<float>();
            var total = TextBoxBlockStack.Layout(new float[0],
                new float[0],
                new float[0],
                10f,
                tops);

            Assert.That(tops, Is.Empty);
            Assert.That(total, Is.EqualTo(0f));
        }
    }
}

using Milestro.Model;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class ParagraphStyleSettingsTests
    {
        [Test]
        public void DefaultsMatchTextBoxComponentDefaults()
        {
            var style = new ParagraphStyleSettings();

            Assert.AreEqual(TextAlign.Start, style.textAlign);
            Assert.AreEqual(TextDirection.Ltr, style.textDirection);
            Assert.IsFalse(style.singleLine);
            Assert.AreEqual("…", style.ellipsisString);
        }

        [Test]
        public void PropertyChangeRaisesChanged()
        {
            var style = new ParagraphStyleSettings();
            var notifications = 0;
            style.Changed += () => notifications++;

            style.textAlign = TextAlign.Center;
            style.textDirection = TextDirection.Rtl;
            style.singleLine = true;
            style.ellipsisString = "...";

            Assert.AreEqual(4, notifications);
        }

        [Test]
        public void NoOpAssignmentDoesNotRaiseChanged()
        {
            var style = new ParagraphStyleSettings();
            var notifications = 0;
            style.Changed += () => notifications++;

            style.textAlign = TextAlign.Start;
            style.textDirection = TextDirection.Ltr;
            style.singleLine = false;
            style.ellipsisString = "…";

            Assert.AreEqual(0, notifications);
        }

        [Test]
        public void NullEllipsisStringBecomesEmpty()
        {
            var style = new ParagraphStyleSettings();

            style.ellipsisString = null!;

            Assert.AreEqual("", style.ellipsisString);
        }
    }
}

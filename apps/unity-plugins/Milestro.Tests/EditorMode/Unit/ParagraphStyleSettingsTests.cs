using Milestro.Model;
using NUnit.Framework;
using UnityEngine;

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
            Assert.AreEqual(WritingMode.HorizontalTb, style.writingMode);
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
            style.writingMode = WritingMode.VerticalRl;
            style.singleLine = true;
            style.ellipsisString = "...";

            Assert.AreEqual(5, notifications);
        }

        [Test]
        public void NoOpAssignmentDoesNotRaiseChanged()
        {
            var style = new ParagraphStyleSettings();
            var notifications = 0;
            style.Changed += () => notifications++;

            style.textAlign = TextAlign.Start;
            style.textDirection = TextDirection.Ltr;
            style.writingMode = WritingMode.HorizontalTb;
            style.singleLine = false;
            style.ellipsisString = "…";

            Assert.AreEqual(0, notifications);
        }

        [Test]
        public void LegacySerializedDataWithoutWritingModeStaysHorizontal()
        {
            var style = JsonUtility.FromJson<ParagraphStyleSettings>(
                "{\"m_textAlign\":0,\"m_textDirection\":0,\"m_singleLine\":false}");

            Assert.That(style, Is.Not.Null);
            Assert.That(style!.writingMode, Is.EqualTo(WritingMode.HorizontalTb));
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

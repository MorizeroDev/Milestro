using System.Collections.Generic;
using Milestro.Model;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.Tests
{
    public class TextStyleSettingsTests
    {
        [Test]
        public void DefaultsMatchTextBoxComponentDefaults()
        {
            var style = new TextStyleSettings();

            Assert.AreEqual(new List<string> { "system-ui" }, style.fontFamilies);
            Assert.AreEqual(36f, style.size);
            Assert.AreEqual(FontWeight.Normal, style.weight);
            Assert.AreEqual(FontWidth.Normal, style.width);
            Assert.AreEqual(FontSlant.Upright, style.slant);
            Assert.AreEqual(Color.white, style.textColor);
            Assert.AreEqual(0f, style.letterSpacing);
            Assert.AreEqual(0f, style.wordSpacing);
            Assert.AreEqual(0f, style.lineHeight);
            Assert.AreEqual("zh-Hans", style.locale);
        }

        [Test]
        public void PropertyChangeRaisesChanged()
        {
            var style = new TextStyleSettings();
            var notifications = 0;
            style.Changed += () => notifications++;

            style.size = 48f;
            style.weight = FontWeight.Bold;
            style.slant = FontSlant.Italic;
            style.width = FontWidth.Condensed;
            style.textColor = Color.red;
            style.letterSpacing = 2.5f;
            style.wordSpacing = 8f;
            style.lineHeight = 1.6f;
            style.locale = "en";
            style.fontFamilies = new List<string> { "sans-serif" };

            Assert.AreEqual(10, notifications);
        }

        [Test]
        public void NoOpAssignmentDoesNotRaiseChanged()
        {
            var style = new TextStyleSettings();
            var notifications = 0;
            style.Changed += () => notifications++;

            style.size = 36f;
            style.weight = FontWeight.Normal;
            style.slant = FontSlant.Upright;
            style.width = FontWidth.Normal;
            style.textColor = Color.white;
            style.letterSpacing = 0f;
            style.wordSpacing = 0f;
            style.lineHeight = 0f;
            style.locale = "zh-Hans";

            Assert.AreEqual(0, notifications);
        }

        [Test]
        public void NullValuesAreNormalized()
        {
            var style = new TextStyleSettings();

            style.fontFamilies = null!;
            Assert.IsNotNull(style.fontFamilies);
            Assert.AreEqual(0, style.fontFamilies.Count);

            style.locale = null!;
            Assert.AreEqual("", style.locale);
        }
    }
}

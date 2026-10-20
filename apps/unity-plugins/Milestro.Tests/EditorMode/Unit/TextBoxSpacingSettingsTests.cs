using System.Collections.Generic;
using Milestro.Model;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.Tests
{
    public class TextBoxSpacingSettingsTests
    {
        [Test]
        public void DefaultsLeaveSpacingAtZero()
        {
            var settings = new TextBoxRenderTargetSettings("content",
                null,
                new List<string>(),
                TextAlign.Start,
                TextDirection.Ltr,
                TextBoxWrapMode.Wrap,
                false,
                TextOverflow.Clip,
                "\u2026",
                36f,
                400,
                Color.white,
                "en");

            Assert.AreEqual(0f, settings.LetterSpacing);
            Assert.AreEqual(0f, settings.WordSpacing);
            Assert.AreEqual(0f, settings.LineHeight);
        }

        [Test]
        public void ConstructorThreadsSpacingValues()
        {
            var settings = new TextBoxRenderTargetSettings("content",
                null,
                new List<string>(),
                TextAlign.Start,
                TextDirection.Ltr,
                TextBoxWrapMode.Wrap,
                false,
                TextOverflow.Clip,
                "\u2026",
                36f,
                400,
                Color.white,
                "en",
                letterSpacing: 2.5f,
                wordSpacing: 8f,
                lineHeight: 1.6f);

            Assert.AreEqual(2.5f, settings.LetterSpacing);
            Assert.AreEqual(8f, settings.WordSpacing);
            Assert.AreEqual(1.6f, settings.LineHeight);
        }
    }
}

using System.Collections.Generic;
using Milestro.Extensions;
using Milestro.Model;
using Milestro.RichTextParser;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.Tests
{
    public class RichTextStyleProjectionTests
    {
        [Test]
        public void UnstyledProjectionPreservesInheritedFieldsAndOwnsMutableCaches()
        {
            using var source = CreateBase();
            source.GetFontFamilies().Add("external-list-mutation");
            using var projected = new TextStyleState().ToTextStyle(source);

            Assert.That(projected.Color, Is.EqualTo(source.Color));
            Assert.That(projected.BackgroundColor, Is.EqualTo(source.BackgroundColor));
            Assert.That(projected.Decoration, Is.EqualTo(source.Decoration));
            Assert.That(projected.DecorationMode, Is.EqualTo(source.DecorationMode));
            Assert.That(projected.DecorationColor, Is.EqualTo(source.DecorationColor));
            Assert.That(projected.DecorationStyle, Is.EqualTo(source.DecorationStyle));
            Assert.That(projected.DecorationThicknessMultiplier, Is.EqualTo(source.DecorationThicknessMultiplier));
            Assert.That(projected.FontSize, Is.EqualTo(source.FontSize));
            Assert.That(projected.Locale, Is.EqualTo(source.Locale));
            Assert.That(projected.BaselineShift, Is.EqualTo(source.BaselineShift));
            Assert.That(projected.Height, Is.EqualTo(source.Height));
            Assert.That(projected.HeightOverride, Is.EqualTo(source.HeightOverride));
            Assert.That(projected.HalfLeading, Is.EqualTo(source.HalfLeading));
            Assert.That(projected.LetterSpacing, Is.EqualTo(source.LetterSpacing));
            Assert.That(projected.WordSpacing, Is.EqualTo(source.WordSpacing));
            Assert.That(projected.TextBaseline, Is.EqualTo(source.TextBaseline));
            Assert.That(projected.GetFontFeatureNumber(), Is.EqualTo(2));
            projected.GetFontStyle(out var weight, out var width, out var slant);
            Assert.That(weight, Is.EqualTo(650));
            Assert.That(width, Is.EqualTo(FontWidth.Condensed));
            Assert.That(slant, Is.EqualTo(FontSlant.Oblique));
            Assert.That(projected.GetFontFamilies(), Has.Count.EqualTo(2));
            Assert.That(projected.GetFontFamilyTokens()[0].Kind, Is.EqualTo(FontFamilyTokenKind.Exact));
            Assert.That(projected.GetShadows(), Has.Count.EqualTo(1));
            Assert.That(projected.GetShadows()[0].Color, Is.EqualTo(new Color32(1, 2, 3, 90)));
            Assert.That(projected.GetShadows()[0].Offset, Is.EqualTo(new Vector2(2, 3)));
            Assert.That(projected.GetShadows()[0].BlurSigma, Is.EqualTo(4));

            source.ResetShadow();
            source.SetFontFamilies(new List<string> { "changed-source" });
            source.Locale = "fr";
            source.FontSize = 40;
            source.ClearBackgroundColor();
            Assert.That(projected.GetShadows(), Has.Count.EqualTo(1));
            Assert.That(projected.GetFontFamilyTokens()[0].Value, Is.EqualTo("serif"));
            Assert.That(projected.Locale, Is.EqualTo("ja"));
            Assert.That(projected.FontSize, Is.EqualTo(20));
            Assert.That(projected.HasBackground, Is.True);
            projected.GetFontFamilies().Clear();
            Assert.That(source.GetFontFamilies(), Is.EqualTo(new[] { "changed-source" }));
        }

        [Test]
        public void ExplicitOverridesKeepBaseStyleUnchanged()
        {
            using var source = CreateBase();
            var state = new TextStyleState
            {
                Underline = true,
                Strikethrough = true,
                FontWeight = 700,
                FontSlant = FontSlant.Italic,
                FontFamilyTokens = new List<FontFamilyToken> { FontFamilyToken.Exact("override-family") },
                Color = new Color32(100, 120, 140, 200),
                Alpha = 0.5f,
                BackgroundColor = new Color32(10, 30, 50, 70),
                FontSize = 24,
                FontSizeScale = 0.5f,
                BaselineShiftEm = 0.25f,
                Locale = "en",
                LineHeight = 1.5f,
                LetterSpacing = 3,
            };
            state.FontFeatureOperations.Add(new FontFeatureOperation(FontFeatureOperationType.Remove, "liga"));
            state.FontFeatureOperations.Add(new FontFeatureOperation(FontFeatureOperationType.Set, "kern", 0));
            using var projected = state.ToTextStyle(source);
            Assert.That(projected.Decoration, Is.EqualTo(TextDecoration.Overline |
                TextDecoration.Underline | TextDecoration.LineThrough));
            Assert.That(projected.Color, Is.EqualTo(new Color32(100, 120, 140, 100)));
            Assert.That(projected.BackgroundColor, Is.EqualTo(new Color32(10, 30, 50, 70)));
            Assert.That(projected.FontSize, Is.EqualTo(12));
            Assert.That(projected.BaselineShift, Is.EqualTo(-4));
            Assert.That(projected.Locale, Is.EqualTo("en"));
            Assert.That(projected.Height, Is.EqualTo(1.5f));
            Assert.That(projected.HeightOverride, Is.True);
            Assert.That(projected.LetterSpacing, Is.EqualTo(3));
            Assert.That(projected.GetFontFamilyTokens()[0].Value, Is.EqualTo("override-family"));
            Assert.That(projected.GetFontFeatureNumber(), Is.EqualTo(2));
            projected.GetFontStyle(out var weight, out var width, out var slant);
            Assert.That(weight, Is.EqualTo(700));
            Assert.That(width, Is.EqualTo(FontWidth.Condensed));
            Assert.That(slant, Is.EqualTo(FontSlant.Italic));
            Assert.That(source.FontSize, Is.EqualTo(20));
            Assert.That(source.Locale, Is.EqualTo("ja"));
            Assert.That(source.Decoration, Is.EqualTo(TextDecoration.Overline));
            Assert.That(source.Color, Is.EqualTo(new Color32(30, 60, 90, 180)));
            Assert.That(source.GetFontFeatureNumber(), Is.EqualTo(2));
            Assert.That(source.GetFontFamilyTokens()[0].Value, Is.EqualTo("serif"));
        }

        [Test]
        public void ImportedStyleRetainsExistingManagedCacheInheritance()
        {
            using var source = CreateBase();
            using var paragraphStyle = new ParagraphStyle();
            paragraphStyle.SetTextStyle(source);
            using var imported = paragraphStyle.GetTextStyle();
            using var projected = new TextStyleState().ToTextStyle(imported);
            Assert.That(projected.Locale, Is.Empty);
            Assert.That(projected.GetShadows(), Is.Empty);
            Assert.That(projected.GetFontFamilies(), Is.EqualTo(new[] { "sans-serif" }));
            Assert.That(projected.FontSize, Is.EqualTo(20));
            Assert.That(projected.BackgroundColor, Is.EqualTo(source.BackgroundColor));
            using var projectedAgain = new TextStyleState().ToTextStyle(projected);
            Assert.That(projectedAgain.Locale, Is.Empty);
            Assert.That(projectedAgain.GetShadows(), Is.Empty);
            Assert.That(projectedAgain.GetFontFamilies(), Is.EqualTo(new[] { "sans-serif" }));
        }

        [Test]
        public void DefaultAndAbsoluteBaselineProjectionPreserveExistingSemantics()
        {
            using var source = new TextStyle();
            using var projected = new TextStyleState { BaselineShift = 3, BaselineShiftEm = 10 }
                .ToTextStyle(source);
            Assert.That(projected.BaselineShift, Is.EqualTo(-3));
            Assert.That(projected.GetFontFamilies(), Is.EqualTo(new[] { "sans-serif" }));
            Assert.That(projected.HasBackground, Is.False);
            Assert.That(projected.IsPlaceholder, Is.False);
            Assert.That(projected.FontSize, Is.EqualTo(source.FontSize));
        }

        private static TextStyle CreateBase()
        {
            var source = new TextStyle
            {
                Color = new Color32(30, 60, 90, 180),
                BackgroundColor = new Color32(20, 40, 60, 80),
                Decoration = TextDecoration.Overline,
                DecorationMode = TextDecorationMode.Through,
                DecorationColor = new Color32(80, 90, 100, 255),
                DecorationStyle = TextDecorationStyle.Wavy,
                DecorationThicknessMultiplier = 1.25f,
                FontSize = 20,
                Locale = "ja",
                BaselineShift = 2,
                Height = 1.2f,
                HeightOverride = true,
                HalfLeading = true,
                LetterSpacing = 0.5f,
                WordSpacing = 0.75f,
                TextBaseline = TextBaseline.Ideographic,
            };
            source.SetFontStyle(650, FontWidth.Condensed, FontSlant.Oblique);
            source.SetFontFamilyTokens(new List<FontFamilyToken>
                { FontFamilyToken.Exact("serif"), FontFamilyToken.Bare("sans-serif") });
            source.AddShadow(new Color32(1, 2, 3, 90), new Vector2(2, 3), 4);
            source.SetFontFeature("liga", 0);
            return source;
        }
    }
}

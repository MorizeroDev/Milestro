#include "Milestro/skia/textlayout/TextStyle.h"
#include "include/core/SkTypeface.h"

#include <gtest/gtest.h>

namespace milestro_text = milestro::skia::textlayout;
using milestro::skia::FontFamilyToken;
using milestro::skia::FontFamilyTokenKind;

TEST(SkiaTextStyleProjection, KeepsFeatureValuesAndOwnsIndependentState) {
    milestro_text::TextStyle source;
    source.resetFontFeatures();
    source.addFontFeature(SkString("liga"), 0);
    source.addFontFeature(SkString("salt"), 3);
    source.setFontFamilyTokens({FontFamilyToken::Exact("sans-serif"), FontFamilyToken::Bare("serif")});
    source.setLocale(SkString("zh-Hant"));
    source.addShadow(SK_ColorBLUE, SkPoint::Make(2, 3), 4);

    auto projected = source.richTextBase();
    const auto projectedStyle = projected.spawn();
    ASSERT_EQ(projectedStyle.getFontFeatures().size(), 2u);
    EXPECT_EQ(projectedStyle.getFontFeatures()[0].fName, SkString("liga"));
    EXPECT_EQ(projectedStyle.getFontFeatures()[0].fValue, 0);
    EXPECT_EQ(projectedStyle.getFontFeatures()[1].fName, SkString("salt"));
    EXPECT_EQ(projectedStyle.getFontFeatures()[1].fValue, 3);
    ASSERT_EQ(projected.getFontFamilyTokens().size(), 2u);
    EXPECT_EQ(projected.getFontFamilyTokens()[0].kind, FontFamilyTokenKind::Exact);
    EXPECT_EQ(projected.getFontFamilyTokens()[1].kind, FontFamilyTokenKind::Bare);
    EXPECT_EQ(projected.getLocale(), SkString("zh-Hant"));
    ASSERT_EQ(projectedStyle.getShadows().size(), 1u);

    projected.setFontFeature(SkString("liga"), 1);
    projected.removeFontFeature(SkString("salt"));
    projected.setFontFamilyTokens({FontFamilyToken::Bare("monospace")});
    projected.setLocale(SkString("en"));
    projected.resetShadows();
    const auto sourceStyle = source.spawn();
    ASSERT_EQ(sourceStyle.getFontFeatures().size(), 2u);
    EXPECT_EQ(sourceStyle.getFontFeatures()[0].fValue, 0);
    EXPECT_EQ(sourceStyle.getFontFeatures()[1].fValue, 3);
    EXPECT_EQ(source.getFontFamilyTokens()[0].value, "sans-serif");
    EXPECT_EQ(source.getLocale(), SkString("zh-Hant"));
    EXPECT_EQ(sourceStyle.getShadows().size(), 1u);
}

TEST(SkiaTextStyleProjection, ExcludesNonInheritedPainterAndPlaceholderState) {
    ::skia::textlayout::TextStyle nativeStyle;
    nativeStyle.setLocale(SkString("en"));
    nativeStyle.setTypeface(SkTypeface::MakeEmpty());
    nativeStyle.setPlaceholder();
    SkPaint foreground;
    foreground.setColor(SK_ColorRED);
    foreground.setStyle(SkPaint::kStroke_Style);
    nativeStyle.setForegroundPaint(foreground);
    SkPaint background;
    background.setColor(SK_ColorBLUE);
    background.setStyle(SkPaint::kStroke_Style);
    nativeStyle.setBackgroundPaint(background);
    nativeStyle.setColor(SK_ColorGREEN);
    nativeStyle.setFontSize(23);
    nativeStyle.setBaselineShift(-3);
    nativeStyle.setHeight(1.5f);
    nativeStyle.setHeightOverride(true);
    nativeStyle.setHalfLeading(true);
    nativeStyle.setLetterSpacing(2);
    nativeStyle.setWordSpacing(4);
    milestro_text::TextStyle source(nativeStyle);

    const auto projected = source.richTextBase().spawn();
    EXPECT_EQ(projected.getTypeface(), nullptr);
    EXPECT_FALSE(projected.isPlaceholder());
    EXPECT_FALSE(projected.hasForeground());
    EXPECT_TRUE(projected.hasBackground());
    EXPECT_EQ(projected.getBackground().getColor(), SK_ColorBLUE);
    EXPECT_EQ(projected.getBackground().getStyle(), SkPaint::kFill_Style);
    EXPECT_EQ(projected.getColor(), SK_ColorGREEN);
    EXPECT_FLOAT_EQ(projected.getFontSize(), 23);
    EXPECT_FLOAT_EQ(projected.getBaselineShift(), -3);
    EXPECT_FLOAT_EQ(projected.getHeight(), 1.5f);
    EXPECT_TRUE(projected.getHeightOverride());
    EXPECT_TRUE(projected.getHalfLeading());
    EXPECT_FLOAT_EQ(projected.getLetterSpacing(), 2);
    EXPECT_FLOAT_EQ(projected.getWordSpacing(), 4);
    const auto unchanged = source.spawn();
    EXPECT_NE(unchanged.getTypeface(), nullptr);
    EXPECT_TRUE(unchanged.isPlaceholder());
    EXPECT_TRUE(unchanged.hasForeground());
    EXPECT_EQ(unchanged.getBackground().getStyle(), SkPaint::kStroke_Style);
}

TEST(SkiaTextStyleProjection, DistinguishesDefaultAndExplicitEmptyFontDeclaration) {
    milestro_text::TextStyle source;
    const auto defaultProjection = source.richTextBase();
    ASSERT_EQ(defaultProjection.getFontFamilyTokens().size(), 1u);
    EXPECT_EQ(defaultProjection.getFontFamilyTokens()[0].kind, FontFamilyTokenKind::Bare);
    EXPECT_EQ(defaultProjection.getFontFamilyTokens()[0].value, "sans-serif");
    EXPECT_FALSE(source.hasFontFamilyTokens());
    source.setFontFamilyTokens({});
    const auto emptyProjection = source.richTextBase();
    EXPECT_TRUE(emptyProjection.hasFontFamilyTokens());
    EXPECT_TRUE(emptyProjection.getFontFamilyTokens().empty());
    EXPECT_TRUE(emptyProjection.getFontFamilies().empty());
}

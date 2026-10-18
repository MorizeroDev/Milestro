#include "../../../include/Milestro/skia/textlayout/ParagraphStyle.h"
#include "modules/skparagraph/include/DartTypes.h"

#include <gtest/gtest.h>

namespace milestro_textlayout = milestro::skia::textlayout;
namespace skia_textlayout = skia::textlayout;

TEST(SkiaParagraphStyleTest, DefaultDirectionMakesStartAlignmentLeft) {
    milestro_textlayout::ParagraphStyle style;

    style.setTextAlign(skia_textlayout::TextAlign::kStart);

    EXPECT_EQ(style.getTextDirection(), skia_textlayout::TextDirection::kLtr);
    EXPECT_EQ(style.effective_align(), skia_textlayout::TextAlign::kLeft);
}

TEST(SkiaParagraphStyleTest, StartAndEndAlignmentResolveFromParagraphDirection) {
    milestro_textlayout::ParagraphStyle style;

    style.setTextAlign(skia_textlayout::TextAlign::kStart);
    style.setTextDirection(skia_textlayout::TextDirection::kLtr);
    EXPECT_EQ(style.effective_align(), skia_textlayout::TextAlign::kLeft);

    style.setTextDirection(skia_textlayout::TextDirection::kRtl);
    EXPECT_EQ(style.effective_align(), skia_textlayout::TextAlign::kRight);

    style.setTextAlign(skia_textlayout::TextAlign::kEnd);
    style.setTextDirection(skia_textlayout::TextDirection::kLtr);
    EXPECT_EQ(style.effective_align(), skia_textlayout::TextAlign::kRight);

    style.setTextDirection(skia_textlayout::TextDirection::kRtl);
    EXPECT_EQ(style.effective_align(), skia_textlayout::TextAlign::kLeft);
}

TEST(SkiaParagraphStyleTest, DefaultStylesCanBeCopiedAndReadAcrossLibraryBoundary) {
    milestro_textlayout::TextStyle textStyle;
    milestro_textlayout::TextStyle textStyleCopy = textStyle;
    EXPECT_TRUE(textStyleCopy.getLocale().isEmpty());
    textStyleCopy.setLocale(SkString("en"));
    textStyle = std::move(textStyleCopy);
    EXPECT_STREQ(textStyle.getLocale().c_str(), "en");
    textStyle.setLocale(SkString());
    EXPECT_TRUE(textStyle.getLocale().isEmpty());

    milestro_textlayout::ParagraphStyle paragraphStyle;
    milestro_textlayout::ParagraphStyle paragraphStyleCopy = paragraphStyle;
    EXPECT_TRUE(paragraphStyleCopy.getEllipsis().isEmpty());
    paragraphStyleCopy.setEllipsis(SkString("..."));
    paragraphStyleCopy.setTextIndent(12.5f, true);
    paragraphStyle = std::move(paragraphStyleCopy);
    EXPECT_STREQ(paragraphStyle.getEllipsis().c_str(), "...");
    EXPECT_FLOAT_EQ(paragraphStyle.getTextIndent(), 12.5f);
    EXPECT_TRUE(paragraphStyle.getTextIndentIsPercentage());
    paragraphStyle.setEllipsis(SkString());
    EXPECT_TRUE(paragraphStyle.getEllipsis().isEmpty());
    paragraphStyle.setTextIndent(-4, false);
    EXPECT_FLOAT_EQ(paragraphStyle.getTextIndent(), -4);
    EXPECT_FALSE(paragraphStyle.getTextIndentIsPercentage());
}

TEST(SkiaTextStyleTest, FontFeaturesCanBeSetRemovedAndCopied) {
    milestro_textlayout::TextStyle source;
    source.setLocale(SkString("en"));
    source.addFontFeature(SkString("liga"), 0);
    source.addFontFeature(SkString("kern"), 0);
    source.addFontFeature(SkString("liga"), 0);
    source.setFontFeature(SkString("liga"), 1);

    auto features = source.spawn().getFontFeatures();
    ASSERT_EQ(features.size(), 2u);
    EXPECT_STREQ(features[0].fName.c_str(), "liga");
    EXPECT_EQ(features[0].fValue, 1);
    EXPECT_STREQ(features[1].fName.c_str(), "kern");
    EXPECT_EQ(features[1].fValue, 0);

    milestro_textlayout::TextStyle copy;
    copy.setLocale(SkString("en"));
    copy.addFontFeature(SkString("tnum"), 1);
    copy.copyFontFeaturesFrom(source);
    copy.removeFontFeature(SkString("kern"));

    features = copy.spawn().getFontFeatures();
    ASSERT_EQ(features.size(), 1u);
    EXPECT_STREQ(features[0].fName.c_str(), "liga");
    EXPECT_EQ(features[0].fValue, 1);
    EXPECT_EQ(source.getFontFeatureNumber(), 2u);
}

TEST(SkiaTextStyleTest, FontFeaturesAffectShapingStyleComparison) {
    milestro_textlayout::TextStyle first;
    milestro_textlayout::TextStyle second;
    first.setLocale(SkString("en"));
    second.setLocale(SkString("en"));
    EXPECT_TRUE(first.spawn().equalsByFonts(second.spawn()));

    first.setFontFeature(SkString("kern"), 0);
    EXPECT_FALSE(first.spawn().equalsByFonts(second.spawn()));

    second.setFontFeature(SkString("kern"), 0);
    EXPECT_TRUE(first.spawn().equalsByFonts(second.spawn()));
}

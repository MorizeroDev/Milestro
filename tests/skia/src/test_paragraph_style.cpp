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
    paragraphStyle = std::move(paragraphStyleCopy);
    EXPECT_STREQ(paragraphStyle.getEllipsis().c_str(), "...");
    paragraphStyle.setEllipsis(SkString());
    EXPECT_TRUE(paragraphStyle.getEllipsis().isEmpty());
}

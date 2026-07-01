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

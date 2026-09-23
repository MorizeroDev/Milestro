#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/log/log.h>
#include "milestro_game_retcode.h"
#include "Milestro/skia/textlayout/ParagraphBuilder.h"
#include <cmath>

namespace {

bool validRubyStyle(float fontSizeMultiplier,
                    float gap,
                    int32_t position,
                    int32_t alignment,
                    int32_t overhang,
                    int32_t overflow) {
    return std::isfinite(fontSizeMultiplier) && fontSizeMultiplier > 0 && std::isfinite(gap) && gap >= 0 &&
           position >= 0 && position <= 1 && alignment >= 0 && alignment <= 4 && overhang >= 0 && overhang <= 1 &&
           overflow >= 0 && overflow <= 1;
}

}

extern "C" {
int64_t MilestroSkiaTextlayoutParagraphBuilderCreate(milestro::skia::textlayout::ParagraphBuilder *&ret,
                                                     milestro::skia::textlayout::ParagraphStyle *style) try {
    ret = new milestro::skia::textlayout::ParagraphBuilder(style);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphBuilderDestroy(milestro::skia::textlayout::ParagraphBuilder *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphBuilderPushStyle(milestro::skia::textlayout::ParagraphBuilder *b,
                                                        milestro::skia::textlayout::TextStyle *style) try {
    b->pushStyle(style);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphBuilderPop(milestro::skia::textlayout::ParagraphBuilder *b) try {
    b->pop();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphBuilderBuild(milestro::skia::textlayout::ParagraphBuilder *b,
                                                    milestro::skia::textlayout::Paragraph *&paragraph) try {
    paragraph = b->build();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphBuilderAddText(milestro::skia::textlayout::ParagraphBuilder *b,
                                                      uint8_t *text, size_t len) try {
    b->addText(reinterpret_cast<const char *>(text), len);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphBuilderAddRuby(milestro::skia::textlayout::ParagraphBuilder* b,
                                                    size_t startUtf8, size_t endUtf8,
                                                    const uint8_t* text, size_t len) try {
    if (b == nullptr || text == nullptr || len == 0 || startUtf8 >= endUtf8)
        return MILESTRO_API_RET_FAILED;
    b->addRuby(startUtf8, endUtf8, reinterpret_cast<const char*>(text), len);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphBuilderAddRubyWithStyle(milestro::skia::textlayout::ParagraphBuilder* b,
                                                               size_t startUtf8,
                                                               size_t endUtf8,
                                                               const uint8_t* text,
                                                               size_t len,
                                                               milestro::skia::textlayout::TextStyle* rubyTextStyle,
                                                               float fontSizeMultiplier,
                                                               float gap,
                                                               int32_t position,
                                                               int32_t alignment,
                                                               int32_t overhang,
                                                               int32_t overflow) try {
    if (b == nullptr || text == nullptr || len == 0 || startUtf8 >= endUtf8 ||
        !validRubyStyle(fontSizeMultiplier, gap, position, alignment, overhang, overflow))
        return MILESTRO_API_RET_FAILED;
    milestro::skia::textlayout::RubyStyle style;
    style.textStyle = rubyTextStyle;
    style.fontSizeMultiplier = fontSizeMultiplier;
    style.gap = gap;
    style.position = static_cast<milestro::skia::textlayout::RubyPosition>(position);
    style.alignment = static_cast<milestro::skia::textlayout::RubyAlignment>(alignment);
    style.overhang = static_cast<milestro::skia::textlayout::RubyOverhang>(overhang);
    style.overflow = static_cast<milestro::skia::textlayout::RubyOverflow>(overflow);
    b->addRuby(startUtf8, endUtf8, reinterpret_cast<const char*>(text), len, style);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}
}

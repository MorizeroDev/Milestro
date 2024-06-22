#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/log/log.h>
#include "milestro_game_retcode.h"
#include "Milestro/skia/textlayout/ParagraphBuilder.h"

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
}

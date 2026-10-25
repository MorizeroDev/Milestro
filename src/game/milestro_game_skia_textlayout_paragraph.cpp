#include <Milestro/game/milestro_game_interface.h>
#include "milestro_game_retcode.h"
#include "Milestro/skia/textlayout/NoWrapLayout.h"
#include "Milestro/skia/textlayout/Paragraph.h"
#include "Milestro/util/milestro_strutil.h"
#include "src/core/SkUTF.h"

#include <cmath>
#include <cstddef>
#include <limits>
#include <unicode/uchar.h>

extern "C" {
int64_t MilestroSkiaTextlayoutParagraphDestroy(milestro::skia::textlayout::Paragraph *&ret) {
    auto* owned = ret;
    ret = nullptr;
    try {
        if (owned != nullptr) {
            owned->release();
        }
        return MILESTRO_API_RET_OK;
    } catch (...) {
        return MILESTRO_API_STATUS_MAKE(MILESTRO_API_STATUS_SEVERITY_WARNING,
                                        MILESTRO_API_STATUS_FLAG_CONSUMED,
                                        MILESTRO_API_STATUS_DOMAIN_NATIVE,
                                        MILESTRO_API_STATUS_OPERATION_DESTROY,
                                        MILESTRO_API_STATUS_DETAIL_UNEXPECTED_EXCEPTION);
    }
}

int64_t MilestroSkiaTextlayoutParagraphRetain(milestro::skia::textlayout::Paragraph *p) try {
    if (p == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    p->retain();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphLayout(milestro::skia::textlayout::Paragraph *p, float width) try {
    if (p == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    p->layout(width);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphLayoutWithInlineExtent(milestro::skia::textlayout::Paragraph *p,
                                                              float inlineExtent) try {
    if (p == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    p->layoutWithInlineExtent(inlineExtent);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutContainsStrongRtl(uint8_t *text,
                                                uint64_t size,
                                                int32_t &containsStrongRtl) try {
    containsStrongRtl = 0;
    if (size == 0) {
        return MILESTRO_API_RET_OK;
    }
    if (text == nullptr || size > static_cast<uint64_t>(std::numeric_limits<std::ptrdiff_t>::max())) {
        return MILESTRO_API_RET_FAILED;
    }

    const char *current = reinterpret_cast<const char *>(text);
    const char *end = current + size;
    while (current < end) {
        const SkUnichar codepoint = SkUTF::NextUTF8WithReplacement(&current, end);
        // Explicit bidi controls such as RLM and ALM have an R/AL direction but
        // are formatting marks, not strong RTL content. Vertical layout skips
        // them without painting a glyph.
        if (u_hasBinaryProperty(codepoint, UCHAR_BIDI_CONTROL)) {
            continue;
        }
        const UCharDirection direction = u_charDirection(codepoint);
        if (direction == U_RIGHT_TO_LEFT || direction == U_RIGHT_TO_LEFT_ARABIC) {
            containsStrongRtl = 1;
            break;
        }
    }
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphGetWritingMode(milestro::skia::textlayout::Paragraph *p,
                                                      int32_t &writingMode) try {
    if (p == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    writingMode = static_cast<int32_t>(p->getWritingMode());
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphGetLayoutMetrics(milestro::skia::textlayout::Paragraph *p,
                                                        float &physicalWidth,
                                                        float &physicalHeight,
                                                        float &contentLeft,
                                                        float &contentTop,
                                                        float &contentRight,
                                                        float &contentBottom,
                                                        float &inkLeft,
                                                        float &inkTop,
                                                        float &inkRight,
                                                        float &inkBottom,
                                                        float &longestInlineExtent,
                                                        float &minIntrinsicInlineExtent,
                                                        float &maxIntrinsicInlineExtent,
                                                        uint64_t &columnCount) try {
    if (p == nullptr || !p->isVertical()) {
        return MILESTRO_API_RET_FAILED;
    }

    const auto &metrics = p->getLayoutMetrics();
    physicalWidth = metrics.physicalSize.width();
    physicalHeight = metrics.physicalSize.height();
    contentLeft = metrics.contentBounds.left();
    contentTop = metrics.contentBounds.top();
    contentRight = metrics.contentBounds.right();
    contentBottom = metrics.contentBounds.bottom();
    inkLeft = metrics.inkBounds.left();
    inkTop = metrics.inkBounds.top();
    inkRight = metrics.inkBounds.right();
    inkBottom = metrics.inkBounds.bottom();
    longestInlineExtent = metrics.longestInlineExtent;
    minIntrinsicInlineExtent = metrics.minIntrinsicInlineExtent;
    maxIntrinsicInlineExtent = metrics.maxIntrinsicInlineExtent;
    columnCount = static_cast<uint64_t>(metrics.columns.size());
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphGetColumnMetrics(milestro::skia::textlayout::Paragraph *p,
                                                        uint64_t columnIndex,
                                                        uint64_t &lineNumber,
                                                        int32_t &hardBreak,
                                                        float &left,
                                                        float &top,
                                                        float &right,
                                                        float &bottom,
                                                        float &inlineExtent,
                                                        float &blockThickness,
                                                        float &physicalBaseline,
                                                        uint64_t &utf16Start,
                                                        uint64_t &utf16End,
                                                        uint64_t &utf16EndExcludingWhitespace,
                                                        uint64_t &utf16EndIncludingNewline) try {
    if (p == nullptr || !p->isVertical() ||
        columnIndex > std::numeric_limits<size_t>::max()) {
        return MILESTRO_API_RET_FAILED;
    }

    const auto &metrics = p->getLayoutMetrics();
    const size_t index = static_cast<size_t>(columnIndex);
    if (index >= metrics.columns.size()) {
        return MILESTRO_API_RET_FAILED;
    }

    const auto &column = metrics.columns[index];
    lineNumber = static_cast<uint64_t>(column.lineNumber);
    hardBreak = column.hardBreak ? 1 : 0;
    left = column.physicalBounds.left();
    top = column.physicalBounds.top();
    right = column.physicalBounds.right();
    bottom = column.physicalBounds.bottom();
    inlineExtent = column.inlineExtent;
    blockThickness = column.blockThickness;
    physicalBaseline = column.physicalBaseline;
    utf16Start = static_cast<uint64_t>(column.utf16Start);
    utf16End = static_cast<uint64_t>(column.utf16End);
    utf16EndExcludingWhitespace = static_cast<uint64_t>(column.utf16EndExcludingWhitespace);
    utf16EndIncludingNewline = static_cast<uint64_t>(column.utf16EndIncludingNewline);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphGetHeight(milestro::skia::textlayout::Paragraph *p, float &height) try {
    height = p->getHeight();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphGetLongestLine(milestro::skia::textlayout::Paragraph *p,
                                                      float &longestLine) try {
    longestLine = p->getLongestLine();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphGetMinIntrinsicWidth(milestro::skia::textlayout::Paragraph *p,
                                                            float &minIntrinsicWidth) try {
    minIntrinsicWidth = p->getMinIntrinsicWidth();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphGetMaxIntrinsicWidth(milestro::skia::textlayout::Paragraph *p,
                                                            float &maxIntrinsicWidth) try {
    maxIntrinsicWidth = p->getMaxIntrinsicWidth();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphResolveNoWrapContentWidth(milestro::skia::textlayout::Paragraph *p,
                                                                 void *text,
                                                                 uint64_t size,
                                                                 float &width) try {
    width = p->resolveNoWrapContentWidth(static_cast<const char *>(text), static_cast<size_t>(size));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphHitTestRange(milestro::skia::textlayout::Paragraph* p,
                                                    uint64_t startUtf16,
                                                    uint64_t endUtf16,
                                                    float x,
                                                    float y,
                                                    int32_t& hit) try {
    if (p == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    if (startUtf16 > std::numeric_limits<size_t>::max() || endUtf16 > std::numeric_limits<size_t>::max()) {
        hit = 0;
        return MILESTRO_API_RET_OK;
    }

    hit = p->hitTestRange(static_cast<size_t>(startUtf16), static_cast<size_t>(endUtf16), x, y) ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphForEachRectInRange(milestro::skia::textlayout::Paragraph* p,
                                                          uint64_t startUtf16,
                                                          uint64_t endUtf16,
                                                          void* context,
                                                          MilestroSkiaTextlayoutParagraphRectCallback callback) try {
    if (p == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    if (startUtf16 > std::numeric_limits<size_t>::max() || endUtf16 > std::numeric_limits<size_t>::max()) {
        return MILESTRO_API_RET_OK;
    }

    return p->forEachRectInRange(static_cast<size_t>(startUtf16),
                                 static_cast<size_t>(endUtf16),
                                 context,
                                 callback);
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphGetRectsForPlaceholders(
        milestro::skia::textlayout::Paragraph* p, void* context,
        MilestroSkiaTextlayoutParagraphRectCallback callback) try {
    if (p == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }
    return p->getRectsForPlaceholders(context, callback);
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutResolveNoWrapProbeLayoutWidth(uint64_t textSize,
                                                            float fontSize,
                                                            float viewportWidth,
                                                            float &width) try {
    width = milestro::skia::textlayout::ResolveNoWrapProbeLayoutWidth(static_cast<size_t>(textSize),
                                                                      fontSize,
                                                                      viewportWidth);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutResolveNoWrapLayoutWidth(float viewportWidth,
                                                       float contentWidth,
                                                       float &width) try {
    width = milestro::skia::textlayout::ResolveNoWrapLayoutWidth(viewportWidth, contentWidth);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphPaint(milestro::skia::textlayout::Paragraph *p,
                                             milestro::skia::Canvas *canvas,
                                             float x, float y) try {
    p->paint(canvas, x, y);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphSplitGlyph(milestro::skia::textlayout::Paragraph *p,
                                                  void *context,
                                                  float x, float y,
                                                  MilestroSkiaTextlayoutParagraphSplitGlyphCallback callback
) try {
    return p->splitGlyph(x, y, context, callback);
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphToSDF(milestro::skia::textlayout::Paragraph *p,
                                             int32_t sdfWidth, int32_t sdfHeight, float sdfScale,
                                             float x, float y,
                                             void *distanceField
) try {
    return p->toSDF(sdfWidth, sdfHeight, sdfScale, x, y, reinterpret_cast<uint8_t *>(distanceField));
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphToPath(milestro::skia::textlayout::Paragraph *p,
                                              milestro::skia::Path *&path,
                                              float x, float y
) try {
    auto ret = p->toPath(x, y);
    if (ret == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }
    path = ret;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

}

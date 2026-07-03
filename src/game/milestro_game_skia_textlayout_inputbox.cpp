#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/game/milestro_game_model.h>

#include "Milestro/skia/textlayout/InputBox.h"
#include "milestro_game_retcode.h"

#include "include/core/SkColor.h"
#include "modules/skparagraph/include/DartTypes.h"

namespace {

skia::textlayout::Affinity ToAffinity(int32_t affinity) {
    return affinity == static_cast<int32_t>(skia::textlayout::Affinity::kUpstream)
                   ? skia::textlayout::Affinity::kUpstream
                   : skia::textlayout::Affinity::kDownstream;
}

milestro::skia::textlayout::TextBoundarySnapMode ToSnapMode(int32_t mode) {
    switch (mode) {
        case 0:
            return milestro::skia::textlayout::TextBoundarySnapMode::Previous;
        case 1:
            return milestro::skia::textlayout::TextBoundarySnapMode::Next;
        case 2:
            return milestro::skia::textlayout::TextBoundarySnapMode::Nearest;
        default:
            return milestro::skia::textlayout::TextBoundarySnapMode::Nearest;
    }
}

} // namespace

extern "C" {

int64_t MilestroSkiaTextlayoutInputBoxCreate(milestro::skia::textlayout::InputBox *&ret,
                                             milestro::skia::textlayout::ParagraphStyle *paragraphStyle,
                                             milestro::skia::textlayout::TextStyle *textStyle) try {
    ret = new milestro::skia::textlayout::InputBox(paragraphStyle, textStyle);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxDestroy(milestro::skia::textlayout::InputBox *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxCreateDrawSnapshot(
        milestro::skia::textlayout::InputBox *inputBox,
        milestro::skia::textlayout::InputBoxDrawSnapshot *&ret) try {
    if (inputBox == nullptr) {
        ret = nullptr;
        return MILESTRO_API_RET_FAILED;
    }

    ret = inputBox->createDrawSnapshot().release();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxDrawSnapshotDestroy(
        milestro::skia::textlayout::InputBoxDrawSnapshot *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxSetText(milestro::skia::textlayout::InputBox *inputBox,
                                              void *text,
                                              uint64_t size) try {
    inputBox->setText(static_cast<const char *>(text), static_cast<size_t>(size));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxGetText(milestro::skia::textlayout::InputBox *inputBox,
                                              uint8_t *&ptr,
                                              uint64_t &size) try {
    const auto &text = inputBox->getText();
    ptr = reinterpret_cast<uint8_t *>(const_cast<char *>(text.data()));
    size = text.size();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxSetViewport(milestro::skia::textlayout::InputBox *inputBox,
                                                  float width,
                                                  float height) try {
    inputBox->setViewport(width, height);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxSetCaretColor(milestro::skia::textlayout::InputBox *inputBox,
                                                    int32_t r,
                                                    int32_t g,
                                                    int32_t b,
                                                    int32_t a) try {
    inputBox->setCaretColor(SkColorSetARGB(static_cast<U8CPU>(a),
                                           static_cast<U8CPU>(r),
                                           static_cast<U8CPU>(g),
                                           static_cast<U8CPU>(b)));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxSetSelectionColor(milestro::skia::textlayout::InputBox *inputBox,
                                                        int32_t r,
                                                        int32_t g,
                                                        int32_t b,
                                                        int32_t a) try {
    inputBox->setSelectionColor(SkColorSetARGB(static_cast<U8CPU>(a),
                                               static_cast<U8CPU>(r),
                                               static_cast<U8CPU>(g),
                                               static_cast<U8CPU>(b)));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxSetCaretWidth(milestro::skia::textlayout::InputBox *inputBox,
                                                    float width) try {
    inputBox->setCaretWidth(width);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxSetCaretVisible(milestro::skia::textlayout::InputBox *inputBox,
                                                      int32_t visible) try {
    inputBox->setCaretVisible(visible != 0);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxInsertText(milestro::skia::textlayout::InputBox *inputBox,
                                                 void *text,
                                                 uint64_t size) try {
    inputBox->insertText(static_cast<const char *>(text), static_cast<size_t>(size));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxSetComposition(milestro::skia::textlayout::InputBox *inputBox,
                                                     void *text,
                                                     uint64_t size,
                                                     int32_t &changed) try {
    changed = inputBox->setComposition(static_cast<const char *>(text), static_cast<size_t>(size)) ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxCommitComposition(milestro::skia::textlayout::InputBox *inputBox,
                                                        void *text,
                                                        uint64_t size,
                                                        int32_t &changed) try {
    changed = inputBox->commitComposition(static_cast<const char *>(text), static_cast<size_t>(size)) ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxClearComposition(milestro::skia::textlayout::InputBox *inputBox,
                                                       int32_t &changed) try {
    changed = inputBox->clearComposition() ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxDeleteBackward(milestro::skia::textlayout::InputBox *inputBox,
                                                     int32_t &changed) try {
    changed = inputBox->deleteBackward() ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxDeleteForward(milestro::skia::textlayout::InputBox *inputBox,
                                                    int32_t &changed) try {
    changed = inputBox->deleteForward() ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxUndo(milestro::skia::textlayout::InputBox *inputBox,
                                           int32_t &changed) try {
    changed = inputBox->undo() ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxRedo(milestro::skia::textlayout::InputBox *inputBox,
                                           int32_t &changed) try {
    changed = inputBox->redo() ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxBreakUndoGroup(
        milestro::skia::textlayout::InputBox *inputBox) try {
    inputBox->breakUndoGroup();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxMovePrevious(milestro::skia::textlayout::InputBox *inputBox,
                                                   int32_t &changed) try {
    changed = inputBox->movePrevious() ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxMoveNext(milestro::skia::textlayout::InputBox *inputBox,
                                               int32_t &changed) try {
    changed = inputBox->moveNext() ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxMovePreviousExtendingSelection(
        milestro::skia::textlayout::InputBox *inputBox,
        int32_t extendSelection,
        int32_t &changed) try {
    changed = inputBox->movePrevious(extendSelection != 0) ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxMoveNextExtendingSelection(
        milestro::skia::textlayout::InputBox *inputBox,
        int32_t extendSelection,
        int32_t &changed) try {
    changed = inputBox->moveNext(extendSelection != 0) ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxHitTest(milestro::skia::textlayout::InputBox *inputBox,
                                              float x,
                                              float y,
                                              int32_t &changed) try {
    changed = inputBox->hitTest(x, y) ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxHitTestExtendingSelection(
        milestro::skia::textlayout::InputBox *inputBox,
        float x,
        float y,
        int32_t extendSelection,
        int32_t &changed) try {
    changed = inputBox->hitTest(x, y, extendSelection != 0) ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxEnsureCaretVisible(
        milestro::skia::textlayout::InputBox *inputBox) try {
    inputBox->ensureCaretVisible();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxScrollByX(
        milestro::skia::textlayout::InputBox *inputBox,
        float delta,
        int32_t &changed) try {
    changed = inputBox->scrollByX(delta) ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxGetCursor(milestro::skia::textlayout::InputBox *inputBox,
                                                uint64_t &utf8Offset,
                                                uint64_t &utf16Offset,
                                                int32_t &affinity) try {
    utf8Offset = inputBox->getCursorUtf8();
    utf16Offset = inputBox->getCursorUtf16();
    affinity = static_cast<int32_t>(inputBox->getAffinity());
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxSetCursorUtf8(milestro::skia::textlayout::InputBox *inputBox,
                                                    uint64_t utf8Offset,
                                                    int32_t affinity) try {
    inputBox->setCursorUtf8(static_cast<size_t>(utf8Offset), ToAffinity(affinity));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxGetSelection(milestro::skia::textlayout::InputBox *inputBox,
                                                   uint64_t &anchorUtf8,
                                                   uint64_t &focusUtf8,
                                                   uint64_t &startUtf8,
                                                   uint64_t &endUtf8,
                                                   int32_t &anchorAffinity,
                                                   int32_t &focusAffinity,
                                                   int32_t &hasSelection) try {
    const auto selection = inputBox->getSelection();
    anchorUtf8 = selection.anchorUtf8;
    focusUtf8 = selection.focusUtf8;
    startUtf8 = selection.startUtf8;
    endUtf8 = selection.endUtf8;
    anchorAffinity = selection.anchorAffinity;
    focusAffinity = selection.focusAffinity;
    hasSelection = selection.hasSelection ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxGetSelectedText(milestro::skia::textlayout::InputBox *inputBox,
                                                      milestro::game::model::BytesWrapper *&ret) try {
    ret = nullptr;
    if (inputBox == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    const auto selection = inputBox->getSelection();
    if (!selection.hasSelection) {
        ret = new milestro::game::model::BytesWrapper(std::string());
        return MILESTRO_API_RET_OK;
    }

    const auto &text = inputBox->getText();
    if (selection.startUtf8 > selection.endUtf8 || selection.endUtf8 > text.size()) {
        return MILESTRO_API_RET_FAILED;
    }

    ret = new milestro::game::model::BytesWrapper(text.substr(selection.startUtf8,
                                                             selection.endUtf8 - selection.startUtf8));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxSetSelectionUtf8(
        milestro::skia::textlayout::InputBox *inputBox,
        uint64_t anchorUtf8,
        uint64_t focusUtf8,
        int32_t anchorAffinity,
        int32_t focusAffinity,
        int32_t &changed) try {
    changed = inputBox->setSelectionUtf8(static_cast<size_t>(anchorUtf8),
                                         static_cast<size_t>(focusUtf8),
                                         ToAffinity(anchorAffinity),
                                         ToAffinity(focusAffinity)) ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxClearSelection(
        milestro::skia::textlayout::InputBox *inputBox,
        int32_t &changed) try {
    changed = inputBox->clearSelection() ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxSelectAll(milestro::skia::textlayout::InputBox *inputBox,
                                                int32_t &changed) try {
    changed = inputBox->selectAll() ? 1 : 0;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxUtf8ToUtf16(milestro::skia::textlayout::InputBox *inputBox,
                                                  uint64_t utf8Offset,
                                                  uint64_t &utf16Offset) try {
    utf16Offset = inputBox->utf8ToUtf16(static_cast<size_t>(utf8Offset));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxUtf16ToUtf8(milestro::skia::textlayout::InputBox *inputBox,
                                                  uint64_t utf16Offset,
                                                  uint64_t &utf8Offset) try {
    utf8Offset = inputBox->utf16ToUtf8(static_cast<size_t>(utf16Offset));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxSnapUtf8(milestro::skia::textlayout::InputBox *inputBox,
                                               uint64_t utf8Offset,
                                               int32_t mode,
                                               uint64_t &snappedUtf8Offset) try {
    snappedUtf8Offset = inputBox->snapUtf8(static_cast<size_t>(utf8Offset), ToSnapMode(mode));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxGetCaretRect(milestro::skia::textlayout::InputBox *inputBox,
                                                   float &left,
                                                   float &top,
                                                   float &right,
                                                   float &bottom) try {
    const auto rect = inputBox->getCaretRect();
    left = rect.left;
    top = rect.top;
    right = rect.right;
    bottom = rect.bottom;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxGetCompositionRect(milestro::skia::textlayout::InputBox *inputBox,
                                                         float &left,
                                                         float &top,
                                                         float &right,
                                                         float &bottom) try {
    const auto rect = inputBox->getCompositionRect();
    left = rect.left;
    top = rect.top;
    right = rect.right;
    bottom = rect.bottom;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxGetMetrics(milestro::skia::textlayout::InputBox *inputBox,
                                                 float &height,
                                                 float &longestLine,
                                                 float &minIntrinsicWidth,
                                                 float &maxIntrinsicWidth,
                                                 float &contentWidth,
                                                 float &scrollX,
                                                 float &viewportWidth,
                                                 float &viewportHeight) try {
    const auto metrics = inputBox->getMetrics();
    height = metrics.height;
    longestLine = metrics.longestLine;
    minIntrinsicWidth = metrics.minIntrinsicWidth;
    maxIntrinsicWidth = metrics.maxIntrinsicWidth;
    contentWidth = metrics.contentWidth;
    scrollX = metrics.scrollX;
    viewportWidth = metrics.viewportWidth;
    viewportHeight = metrics.viewportHeight;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxGetLineCount(milestro::skia::textlayout::InputBox *inputBox,
                                                   uint64_t &lineCount) try {
    lineCount = inputBox->getLineCount();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutInputBoxGetLineMetrics(milestro::skia::textlayout::InputBox *inputBox,
                                                     uint64_t lineNumber,
                                                     uint64_t &startUtf8,
                                                     uint64_t &endUtf8,
                                                     float &ascent,
                                                     float &descent,
                                                     float &unscaledAscent,
                                                     float &height,
                                                     float &width,
                                                     float &left,
                                                     float &baseline) try {
    milestro::skia::textlayout::InputBoxLineMetrics metrics;
    if (!inputBox->getLineMetrics(static_cast<size_t>(lineNumber), metrics)) {
        return MILESTRO_API_RET_FAILED;
    }

    startUtf8 = metrics.startUtf8;
    endUtf8 = metrics.endUtf8;
    ascent = metrics.ascent;
    descent = metrics.descent;
    unscaledAscent = metrics.unscaledAscent;
    height = metrics.height;
    width = metrics.width;
    left = metrics.left;
    baseline = metrics.baseline;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

}

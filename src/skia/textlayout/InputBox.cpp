#include "Milestro/skia/textlayout/InputBox.h"

#include "include/core/SkCanvas.h"
#include "include/core/SkFontMetrics.h"
#include "include/core/SkPaint.h"
#include "include/core/SkSpan.h"
#include "include/core/SkString.h"
#include "modules/skparagraph/include/ParagraphBuilder.h"

#include <algorithm>
#include <cmath>
#include <limits>
#include <utility>

namespace milestro::skia::textlayout {

namespace {

constexpr SkScalar kSingleLineLayoutWidth = 1048576.0f;
constexpr SkScalar kCaretScrollPadding = 2.0f;
constexpr SkScalar kCompositionUnderlineHeight = 1.5f;
constexpr SkScalar kFallbackAscentRatio = 0.8f;

struct VerticalMetrics {
    SkScalar ascent = 0.0f;
    SkScalar descent = 0.0f;
    SkScalar height = 0.0f;
};

bool IsUtf8Continuation(unsigned char value) {
    return (value & 0xC0U) == 0x80U;
}

bool IsUtf8CodepointStart(const std::string& text, size_t offset) {
    return offset == 0 || offset == text.size() ||
           (offset < text.size() && !IsUtf8Continuation(static_cast<unsigned char>(text[offset])));
}

float ToFloat(double value) {
    return static_cast<float>(value);
}

SkScalar ClampScalar(SkScalar value, SkScalar minValue, SkScalar maxValue) {
    return std::min(std::max(value, minValue), maxValue);
}

bool IsFinitePositive(SkScalar value) {
    return std::isfinite(value) && value > 0.0f;
}

SkScalar EmptyCaretX(::skia::textlayout::TextAlign align, SkScalar viewportWidth, SkScalar caretWidth) {
    const auto maxLeft = std::max<SkScalar>(0.0f, viewportWidth - caretWidth);
    switch (align) {
        case ::skia::textlayout::TextAlign::kCenter:
            return maxLeft * 0.5f;
        case ::skia::textlayout::TextAlign::kRight:
        case ::skia::textlayout::TextAlign::kEnd:
            return maxLeft;
        case ::skia::textlayout::TextAlign::kLeft:
        case ::skia::textlayout::TextAlign::kJustify:
        case ::skia::textlayout::TextAlign::kStart:
        default:
            return 0.0f;
    }
}

bool HasGlyphClusterStart(SkUnicode::CodeUnitFlags flags) {
    return (flags & SkUnicode::CodeUnitFlags::kGlyphClusterStart) ==
           SkUnicode::CodeUnitFlags::kGlyphClusterStart;
}

VerticalMetrics ResolveStyleVerticalMetrics(const ::skia::textlayout::TextStyle& textStyle) {
    const SkScalar fontSize = IsFinitePositive(textStyle.getFontSize()) ? textStyle.getFontSize() : 1.0f;

    SkFontMetrics fontMetrics;
    textStyle.getFontMetrics(&fontMetrics);

    auto ascent = std::isfinite(fontMetrics.fAscent) && fontMetrics.fAscent < 0.0f ? -fontMetrics.fAscent : 0.0f;
    auto descent = std::isfinite(fontMetrics.fDescent) && fontMetrics.fDescent > 0.0f ? fontMetrics.fDescent : 0.0f;
    const auto leading = std::isfinite(fontMetrics.fLeading) && fontMetrics.fLeading > 0.0f ? fontMetrics.fLeading : 0.0f;

    if (!IsFinitePositive(ascent) && !IsFinitePositive(descent)) {
        ascent = fontSize * kFallbackAscentRatio;
        descent = fontSize - ascent;
    } else if (!IsFinitePositive(ascent)) {
        ascent = std::max(fontSize - descent, fontSize * kFallbackAscentRatio);
    } else if (!IsFinitePositive(descent)) {
        descent = std::max(fontSize - ascent, fontSize * (1.0f - kFallbackAscentRatio));
    }

    return VerticalMetrics{
            ascent,
            descent,
            std::max(fontSize, ascent + descent + leading),
    };
}

} // namespace

TextBoundaryMap::TextBoundaryMap() {
    rebuild(std::string());
}

TextBoundaryMap::TextBoundaryMap(std::string text) {
    rebuild(std::move(text));
}

void TextBoundaryMap::rebuild(std::string text) {
    text_ = std::move(text);
    rebuildUtfMapping();
    rebuildBoundaries();
}

size_t TextBoundaryMap::utf8ToUtf16(size_t utf8Offset) const {
    if (utf16ForUtf8_.empty()) {
        return 0;
    }

    const auto index = std::min(utf8Offset, utf16ForUtf8_.size() - 1);
    return utf16ForUtf8_[index];
}

size_t TextBoundaryMap::utf16ToUtf8(size_t utf16Offset) const {
    if (utf8ForUtf16_.empty()) {
        return 0;
    }

    const auto index = std::min(utf16Offset, utf8ForUtf16_.size() - 1);
    return utf8ForUtf16_[index];
}

bool TextBoundaryMap::isBoundary(size_t utf8Offset) const {
    return std::binary_search(boundariesUtf8_.begin(), boundariesUtf8_.end(), utf8Offset);
}

size_t TextBoundaryMap::previousBoundary(size_t utf8Offset) const {
    if (boundariesUtf8_.empty()) {
        return 0;
    }

    const auto clamped = std::min(utf8Offset, text_.size());
    auto it = std::lower_bound(boundariesUtf8_.begin(), boundariesUtf8_.end(), clamped);
    if (it == boundariesUtf8_.begin()) {
        return *it;
    }
    if (it == boundariesUtf8_.end() || *it >= clamped) {
        --it;
    }
    return *it;
}

size_t TextBoundaryMap::nextBoundary(size_t utf8Offset) const {
    if (boundariesUtf8_.empty()) {
        return 0;
    }

    const auto clamped = std::min(utf8Offset, text_.size());
    auto it = std::upper_bound(boundariesUtf8_.begin(), boundariesUtf8_.end(), clamped);
    if (it == boundariesUtf8_.end()) {
        return boundariesUtf8_.back();
    }
    return *it;
}

size_t TextBoundaryMap::nearestBoundary(size_t utf8Offset) const {
    if (boundariesUtf8_.empty()) {
        return 0;
    }

    const auto clamped = std::min(utf8Offset, text_.size());
    auto next = std::lower_bound(boundariesUtf8_.begin(), boundariesUtf8_.end(), clamped);
    if (next == boundariesUtf8_.begin()) {
        return *next;
    }
    if (next == boundariesUtf8_.end()) {
        return boundariesUtf8_.back();
    }

    auto prev = next;
    --prev;
    return clamped - *prev <= *next - clamped ? *prev : *next;
}

size_t TextBoundaryMap::snapUtf8(size_t utf8Offset, TextBoundarySnapMode mode) const {
    switch (mode) {
        case TextBoundarySnapMode::Previous:
            return previousBoundary(utf8Offset);
        case TextBoundarySnapMode::Next:
            return nextBoundary(utf8Offset);
        case TextBoundarySnapMode::Nearest:
            return nearestBoundary(utf8Offset);
        default:
            return nearestBoundary(utf8Offset);
    }
}

size_t TextBoundaryMap::boundaryAt(size_t index) const {
    if (boundariesUtf8_.empty()) {
        return 0;
    }

    return boundariesUtf8_[std::min(index, boundariesUtf8_.size() - 1)];
}

void TextBoundaryMap::rebuildUtfMapping() {
    utf16ForUtf8_.clear();
    utf8ForUtf16_.clear();

    const SkSpan<const char> textSpan(text_.data(), text_.size());
    const bool mapped = SkUnicode::extractUtfConversionMapping(
            textSpan,
            [this](size_t utf8Offset) {
                utf8ForUtf16_.push_back(utf8Offset);
            },
            [this](size_t utf16Offset) {
                utf16ForUtf8_.push_back(utf16Offset);
            });

    if (mapped && !utf16ForUtf8_.empty() && !utf8ForUtf16_.empty()) {
        return;
    }

    utf16ForUtf8_.assign(text_.size() + 1, 0);
    utf8ForUtf16_.assign(text_.size() + 1, 0);
    for (size_t i = 0; i <= text_.size(); ++i) {
        utf16ForUtf8_[i] = i;
        utf8ForUtf16_[i] = i;
    }
}

void TextBoundaryMap::rebuildBoundaries() {
    boundariesUtf8_.clear();
    boundariesUtf8_.push_back(0);

    if (text_.empty()) {
        return;
    }

    auto unicode = milestro::skia::GetUnicodeProvider()->unwrap();
    skia_private::TArray<SkUnicode::CodeUnitFlags, true> flags;
    const bool hasFlags = unicode != nullptr &&
                          unicode->computeCodeUnitFlags(text_.data(),
                                                         static_cast<int>(text_.size()),
                                                         false,
                                                         &flags);

    bool sawGlyphClusterFlag = false;
    if (hasFlags) {
        const auto flagsSize = static_cast<size_t>(flags.size());
        for (size_t i = 0; i < std::min(text_.size(), flagsSize); ++i) {
            if (HasGlyphClusterStart(flags[static_cast<int>(i)])) {
                sawGlyphClusterFlag = true;
                break;
            }
        }
    }

    if (hasFlags) {
        const auto flagsSize = static_cast<size_t>(flags.size());
        for (size_t i = 1; i < text_.size() && i < flagsSize; ++i) {
            if (!IsUtf8CodepointStart(text_, i)) {
                continue;
            }

            const bool graphemeStart = SkUnicode::hasGraphemeStartFlag(flags[static_cast<int>(i)]);
            const bool glyphClusterStart = HasGlyphClusterStart(flags[static_cast<int>(i)]);
            const bool validStart = sawGlyphClusterFlag ? graphemeStart && glyphClusterStart : graphemeStart;
            if (validStart) {
                boundariesUtf8_.push_back(i);
            }
        }
    } else {
        for (size_t i = 1; i < text_.size(); ++i) {
            if (IsUtf8CodepointStart(text_, i)) {
                boundariesUtf8_.push_back(i);
            }
        }
    }

    boundariesUtf8_.push_back(text_.size());
    std::sort(boundariesUtf8_.begin(), boundariesUtf8_.end());
    boundariesUtf8_.erase(std::unique(boundariesUtf8_.begin(), boundariesUtf8_.end()), boundariesUtf8_.end());
}

InputBox::InputBox() {
    configureInputParagraphMetrics();
}

InputBox::InputBox(ParagraphStyle* paragraphStyle, TextStyle* textStyle) : InputBox() {
    if (paragraphStyle != nullptr) {
        paragraphStyle_ = paragraphStyle->unwrap();
        textStyle_ = paragraphStyle_.getTextStyle();
    }
    if (textStyle != nullptr) {
        textStyle_ = textStyle->spawn();
        paragraphStyle_.setTextStyle(textStyle_);
    }
    configureInputParagraphMetrics();
}

void InputBox::setText(const char* text, size_t length) {
    compositionText_.clear();
    replaceText(sanitizeSingleLine(text, length), cursorUtf8_);
}

void InputBox::setViewport(SkScalar width, SkScalar height) {
    viewportWidth_ = std::max<SkScalar>(1.0f, width);
    viewportHeight_ = std::max<SkScalar>(1.0f, height);
    paragraphDirty_ = true;
    ensureCaretVisible();
}

void InputBox::setCaretWidth(SkScalar width) {
    caretWidth_ = std::max<SkScalar>(1.0f, width);
}

void InputBox::setCursorUtf8(size_t utf8Offset, ::skia::textlayout::Affinity affinity) {
    cursorUtf8_ = boundaryMap_.snapUtf8(utf8Offset, TextBoundarySnapMode::Nearest);
    affinity_ = affinity;
    resetSelectionToCursor();
    ensureCaretVisible();
}

void InputBox::insertText(const char* text, size_t length) {
    auto insert = sanitizeSingleLine(text, length);
    if (insert.empty()) {
        return;
    }

    compositionText_.clear();
    if (hasSelection()) {
        replaceSelectionWith(std::move(insert));
        return;
    }

    auto next = boundaryMap_.text();
    const auto cursor = boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest);
    next.insert(cursor, insert);
    replaceText(std::move(next), cursor + insert.size());
}

bool InputBox::setComposition(const char* text, size_t length) {
    auto composition = sanitizeSingleLine(text, length);
    if (composition == compositionText_) {
        return false;
    }

    compositionText_ = std::move(composition);
    markCompositionDirty();
    ensureCaretVisible();
    return true;
}

bool InputBox::commitComposition(const char* text, size_t length) {
    auto commitText = sanitizeSingleLine(text, length);
    if (commitText.empty()) {
        commitText = compositionText_;
    }

    const bool hadComposition = !compositionText_.empty();
    compositionText_.clear();
    if (commitText.empty()) {
        if (hadComposition) {
            markCompositionDirty();
        }
        return hadComposition;
    }

    if (hasSelection()) {
        replaceSelectionWith(std::move(commitText));
        return true;
    }

    auto next = boundaryMap_.text();
    const auto cursor = boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest);
    next.insert(cursor, commitText);
    replaceText(std::move(next), cursor + commitText.size());
    return true;
}

bool InputBox::clearComposition() {
    if (compositionText_.empty()) {
        return false;
    }

    compositionText_.clear();
    markCompositionDirty();
    ensureCaretVisible();
    return true;
}

InputBoxSelection InputBox::getSelection() const {
    InputBoxSelection selection;
    selection.anchorUtf8 = selectionAnchorUtf8_;
    selection.focusUtf8 = selectionFocusUtf8_;
    selection.startUtf8 = selectionStartUtf8();
    selection.endUtf8 = selectionEndUtf8();
    selection.anchorAffinity = static_cast<int32_t>(selectionAnchorAffinity_);
    selection.focusAffinity = static_cast<int32_t>(selectionFocusAffinity_);
    selection.hasSelection = selection.endUtf8 > selection.startUtf8;
    return selection;
}

bool InputBox::hasSelection() const {
    return selectionEndUtf8() > selectionStartUtf8();
}

bool InputBox::setSelectionUtf8(size_t anchorUtf8,
                                size_t focusUtf8,
                                ::skia::textlayout::Affinity anchorAffinity,
                                ::skia::textlayout::Affinity focusAffinity) {
    const auto oldAnchor = selectionAnchorUtf8_;
    const auto oldFocus = selectionFocusUtf8_;
    const auto oldAnchorAffinity = selectionAnchorAffinity_;
    const auto oldFocusAffinity = selectionFocusAffinity_;
    const auto oldCursor = cursorUtf8_;
    const auto oldAffinity = affinity_;

    selectionAnchorUtf8_ = boundaryMap_.snapUtf8(anchorUtf8, TextBoundarySnapMode::Nearest);
    selectionFocusUtf8_ = boundaryMap_.snapUtf8(focusUtf8, TextBoundarySnapMode::Nearest);
    selectionAnchorAffinity_ = anchorAffinity;
    selectionFocusAffinity_ = focusAffinity;
    cursorUtf8_ = selectionFocusUtf8_;
    affinity_ = focusAffinity;
    ensureCaretVisible();

    return selectionAnchorUtf8_ != oldAnchor ||
           selectionFocusUtf8_ != oldFocus ||
           selectionAnchorAffinity_ != oldAnchorAffinity ||
           selectionFocusAffinity_ != oldFocusAffinity ||
           cursorUtf8_ != oldCursor ||
           affinity_ != oldAffinity;
}

bool InputBox::clearSelection() {
    const bool changed = hasSelection();
    resetSelectionToCursor();
    return changed;
}

bool InputBox::selectAll() {
    return setSelectionUtf8(0,
                            boundaryMap_.utf8Length(),
                            ::skia::textlayout::Affinity::kDownstream,
                            ::skia::textlayout::Affinity::kDownstream);
}

bool InputBox::deleteBackward() {
    const bool clearedComposition = clearComposition();
    if (deleteSelection()) {
        return true;
    }

    const auto cursor = boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest);
    if (cursor == 0) {
        return clearedComposition;
    }

    const auto previous = boundaryMap_.previousBoundary(cursor);
    auto next = boundaryMap_.text();
    next.erase(previous, cursor - previous);
    replaceText(std::move(next), previous);
    return true;
}

bool InputBox::deleteForward() {
    const bool clearedComposition = clearComposition();
    if (deleteSelection()) {
        return true;
    }

    const auto cursor = boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest);
    if (cursor >= boundaryMap_.utf8Length()) {
        return clearedComposition;
    }

    const auto nextBoundary = boundaryMap_.nextBoundary(cursor);
    auto next = boundaryMap_.text();
    next.erase(cursor, nextBoundary - cursor);
    replaceText(std::move(next), cursor);
    return true;
}

bool InputBox::movePrevious(bool extendSelection) {
    const bool clearedComposition = clearComposition();
    if (!extendSelection && hasSelection()) {
        cursorUtf8_ = selectionStartUtf8();
        affinity_ = ::skia::textlayout::Affinity::kDownstream;
        resetSelectionToCursor();
        ensureCaretVisible();
        return true;
    }

    const auto previous = boundaryMap_.previousBoundary(cursorUtf8_);
    if (previous == cursorUtf8_) {
        return clearedComposition;
    }

    if (extendSelection) {
        const auto anchor = hasSelection() ? selectionAnchorUtf8_ : cursorUtf8_;
        const auto anchorAffinity = hasSelection() ? selectionAnchorAffinity_ : affinity_;
        setSelectionUtf8(anchor, previous, anchorAffinity, ::skia::textlayout::Affinity::kDownstream);
    } else {
        cursorUtf8_ = previous;
        affinity_ = ::skia::textlayout::Affinity::kDownstream;
        resetSelectionToCursor();
        ensureCaretVisible();
    }
    return true;
}

bool InputBox::moveNext(bool extendSelection) {
    const bool clearedComposition = clearComposition();
    if (!extendSelection && hasSelection()) {
        cursorUtf8_ = selectionEndUtf8();
        affinity_ = ::skia::textlayout::Affinity::kDownstream;
        resetSelectionToCursor();
        ensureCaretVisible();
        return true;
    }

    const auto next = boundaryMap_.nextBoundary(cursorUtf8_);
    if (next == cursorUtf8_) {
        return clearedComposition;
    }

    if (extendSelection) {
        const auto anchor = hasSelection() ? selectionAnchorUtf8_ : cursorUtf8_;
        const auto anchorAffinity = hasSelection() ? selectionAnchorAffinity_ : affinity_;
        setSelectionUtf8(anchor, next, anchorAffinity, ::skia::textlayout::Affinity::kDownstream);
    } else {
        cursorUtf8_ = next;
        affinity_ = ::skia::textlayout::Affinity::kDownstream;
        resetSelectionToCursor();
        ensureCaretVisible();
    }
    return true;
}

bool InputBox::hitTest(SkScalar x, SkScalar y, bool extendSelection) {
    rebuildParagraphIfNeeded();
    if (paragraph_ == nullptr) {
        return false;
    }

    const auto oldCursor = cursorUtf8_;
    const auto oldAffinity = affinity_;
    const auto hit = paragraph_->getGlyphPositionAtCoordinate(x + scrollX_, y);
    const auto displayMap = displayBoundaryMap();
    const auto displayUtf8 = displayMap.utf16ToUtf8(hit.position < 0 ? 0 : static_cast<size_t>(hit.position));
    const auto utf8 = committedUtf8FromDisplay(displayUtf8);
    const bool clearedComposition = clearComposition();
    const auto snappedUtf8 = boundaryMap_.snapUtf8(utf8, TextBoundarySnapMode::Nearest);
    bool selectionChanged = false;
    if (extendSelection) {
        const auto anchor = hasSelection() ? selectionAnchorUtf8_ : oldCursor;
        const auto anchorAffinity = hasSelection() ? selectionAnchorAffinity_ : oldAffinity;
        selectionChanged = setSelectionUtf8(anchor, snappedUtf8, anchorAffinity, hit.affinity);
    } else {
        cursorUtf8_ = snappedUtf8;
        affinity_ = hit.affinity;
        selectionChanged = clearSelection();
        resetSelectionToCursor();
        ensureCaretVisible();
    }
    return clearedComposition || selectionChanged || cursorUtf8_ != oldCursor || affinity_ != oldAffinity;
}

void InputBox::ensureCaretVisible() {
    rebuildParagraphIfNeeded();
    const auto width = contentWidth();
    const auto maxScroll = std::max<SkScalar>(0.0f, width - viewportWidth_);
    auto caret = getCaretRect();

    if (caret.left - scrollX_ < kCaretScrollPadding) {
        scrollX_ = caret.left - kCaretScrollPadding;
    } else if (caret.right - scrollX_ > viewportWidth_ - kCaretScrollPadding) {
        scrollX_ = caret.right - viewportWidth_ + kCaretScrollPadding;
    }

    scrollX_ = ClampScalar(scrollX_, 0.0f, maxScroll);
}

InputBoxCaretRect InputBox::getCaretRect() {
    return getCaretRectForDisplayOffset(displayCaretUtf8());
}

InputBoxCaretRect InputBox::getCaretRectForDisplayOffset(size_t displayUtf8) {
    rebuildParagraphIfNeeded();
    InputBoxCaretRect rect;
    if (paragraph_ == nullptr) {
        rect.right = caretWidth_;
        rect.bottom = ToFloat(ResolveStyleVerticalMetrics(textStyle_).height);
        return rect;
    }

    ::skia::textlayout::LineMetrics lineMetrics;
    const auto displayMap = displayBoundaryMap();
    const auto utf16Length = displayMap.utf16Length();
    const auto caretUtf16 = displayMap.utf8ToUtf16(displayUtf8);
    auto lineProbeUtf16 = caretUtf16;
    if (utf16Length > 0 && lineProbeUtf16 >= utf16Length) {
        lineProbeUtf16 = utf16Length - 1;
    }
    auto lineNumber = paragraph_->getLineNumberAtUTF16Offset(lineProbeUtf16);
    if (lineNumber < 0) {
        lineNumber = 0;
    }

    const bool hasLineMetrics = paragraph_->getLineMetricsAt(lineNumber, &lineMetrics);
    const auto fallbackMetrics = ResolveStyleVerticalMetrics(textStyle_);
    auto top = 0.0f;
    auto bottom = ToFloat(fallbackMetrics.height);
    if (hasLineMetrics) {
        top = ToFloat(lineMetrics.fBaseline - lineMetrics.fAscent);
        bottom = ToFloat(lineMetrics.fBaseline + lineMetrics.fDescent);
        if (!(bottom > top)) {
            top = 0.0f;
            bottom = ToFloat(fallbackMetrics.height);
        }
    }

    SkScalar x = EmptyCaretX(paragraphStyle_.effective_align(), viewportWidth_, caretWidth_);
    if (utf16Length > 0) {
        ::skia::textlayout::Paragraph::GlyphInfo glyphInfo;
        auto glyphProbeUtf16 = caretUtf16;
        if (glyphProbeUtf16 >= utf16Length) {
            glyphProbeUtf16 = utf16Length - 1;
        }

        if (paragraph_->getGlyphInfoAtUTF16Offset(glyphProbeUtf16, &glyphInfo)) {
            const bool atClusterEnd = caretUtf16 >= glyphInfo.fGraphemeClusterTextRange.end;
            const bool rtl = glyphInfo.fDirection == ::skia::textlayout::TextDirection::kRtl;
            if (atClusterEnd) {
                x = rtl ? glyphInfo.fGraphemeLayoutBounds.left() : glyphInfo.fGraphemeLayoutBounds.right();
            } else {
                x = rtl ? glyphInfo.fGraphemeLayoutBounds.right() : glyphInfo.fGraphemeLayoutBounds.left();
            }
        } else {
            x = contentWidth();
        }
    }

    rect.left = x;
    rect.right = x + caretWidth_;
    rect.top = top;
    rect.bottom = bottom;
    return rect;
}

InputBoxCaretRect InputBox::getCompositionRect() {
    rebuildParagraphIfNeeded();
    if (paragraph_ == nullptr || compositionText_.empty()) {
        return getCaretRect();
    }

    const auto displayMap = displayBoundaryMap();
    const auto startUtf16 = displayMap.utf8ToUtf16(displayCompositionStartUtf8());
    const auto endUtf16 = displayMap.utf8ToUtf16(displayCompositionEndUtf8());
    if (endUtf16 <= startUtf16) {
        return getCaretRect();
    }

    auto boxes = paragraph_->getRectsForRange(static_cast<unsigned>(startUtf16),
                                              static_cast<unsigned>(endUtf16),
                                              ::skia::textlayout::RectHeightStyle::kTight,
                                              ::skia::textlayout::RectWidthStyle::kTight);
    if (boxes.empty()) {
        return getCaretRect();
    }

    auto rect = boxes.front().rect;
    for (size_t i = 1; i < boxes.size(); ++i) {
        rect.join(boxes[i].rect);
    }

    return InputBoxCaretRect{
            ToFloat(rect.left()),
            ToFloat(rect.top()),
            ToFloat(rect.right()),
            ToFloat(rect.bottom()),
    };
}

std::vector<InputBoxCaretRect> InputBox::getSelectionRects() {
    if (!hasSelection() || !compositionText_.empty()) {
        return {};
    }

    return getRectsForDisplayRange(selectionStartUtf8(), selectionEndUtf8());
}

InputBoxMetrics InputBox::getMetrics() {
    rebuildParagraphIfNeeded();

    InputBoxMetrics metrics;
    metrics.viewportWidth = viewportWidth_;
    metrics.viewportHeight = viewportHeight_;
    metrics.scrollX = scrollX_;
    if (paragraph_ == nullptr) {
        return metrics;
    }

    metrics.height = paragraph_->getHeight();
    metrics.longestLine = paragraph_->getLongestLine();
    metrics.minIntrinsicWidth = paragraph_->getMinIntrinsicWidth();
    metrics.maxIntrinsicWidth = paragraph_->getMaxIntrinsicWidth();
    metrics.contentWidth = contentWidth();
    return metrics;
}

size_t InputBox::getLineCount() {
    rebuildParagraphIfNeeded();
    return paragraph_ == nullptr ? 0 : paragraph_->lineNumber();
}

bool InputBox::getLineMetrics(size_t lineNumber, InputBoxLineMetrics& metrics) {
    rebuildParagraphIfNeeded();
    if (paragraph_ == nullptr) {
        return false;
    }

    ::skia::textlayout::LineMetrics lineMetrics;
    if (!paragraph_->getLineMetricsAt(static_cast<int>(lineNumber), &lineMetrics)) {
        return false;
    }

    metrics.startUtf8 = lineMetrics.fStartIndex;
    metrics.endUtf8 = lineMetrics.fEndIndex;
    metrics.ascent = ToFloat(lineMetrics.fAscent);
    metrics.descent = ToFloat(lineMetrics.fDescent);
    metrics.unscaledAscent = ToFloat(lineMetrics.fUnscaledAscent);
    metrics.height = ToFloat(lineMetrics.fHeight);
    metrics.width = ToFloat(lineMetrics.fWidth);
    metrics.left = ToFloat(lineMetrics.fLeft);
    metrics.baseline = ToFloat(lineMetrics.fBaseline);
    metrics.lineNumber = lineMetrics.fLineNumber;
    return true;
}

void InputBox::paint(SkCanvas* canvas, SkScalar x, SkScalar y, SkScalar width, SkScalar height) {
    if (canvas == nullptr) {
        return;
    }

    rebuildParagraphIfNeeded();
    canvas->save();
    const auto clipWidth = width > 0.0f ? width : viewportWidth_;
    const auto clipHeight = height > 0.0f ? height : viewportHeight_;
    canvas->clipRect(SkRect::MakeXYWH(x, y, clipWidth, clipHeight));

    const auto selectionRects = getSelectionRects();
    if (!selectionRects.empty()) {
        SkPaint paint;
        paint.setColor(selectionColor_);
        paint.setStyle(SkPaint::kFill_Style);
        for (const auto& rect: selectionRects) {
            canvas->drawRect(SkRect::MakeLTRB(x + rect.left - scrollX_,
                                              y + rect.top,
                                              x + rect.right - scrollX_,
                                              y + rect.bottom),
                             paint);
        }
    }

    if (paragraph_ != nullptr) {
        paragraph_->paint(canvas, x - scrollX_, y);
    }

    if (!compositionText_.empty()) {
        const auto composition = getCompositionRect();
        SkPaint paint;
        paint.setColor(caretColor_);
        paint.setStyle(SkPaint::kFill_Style);
        const auto underlineTop = std::max(composition.top, composition.bottom - kCompositionUnderlineHeight);
        canvas->drawRect(SkRect::MakeLTRB(x + composition.left - scrollX_,
                                          y + underlineTop,
                                          x + composition.right - scrollX_,
                                          y + composition.bottom),
                         paint);
    }

    if (caretVisible_) {
        const auto caret = getCaretRect();
        SkPaint paint;
        paint.setColor(caretColor_);
        paint.setStyle(SkPaint::kFill_Style);
        canvas->drawRect(SkRect::MakeLTRB(x + caret.left - scrollX_,
                                          y + caret.top,
                                          x + caret.right - scrollX_,
                                          y + caret.bottom),
                         paint);
    }

    canvas->restore();
}

std::unique_ptr<InputBoxDrawSnapshot> InputBox::createDrawSnapshot() {
    rebuildParagraphIfNeeded();
    auto paragraph = buildParagraph();
    auto caretRect = getCaretRect();
    auto compositionRect = getCompositionRect();
    auto selectionRects = getSelectionRects();
    auto metrics = getMetrics();
    return std::make_unique<InputBoxDrawSnapshot>(std::move(paragraph),
                                                  caretRect,
                                                  metrics,
                                                  compositionRect,
                                                  std::move(selectionRects),
                                                  caretWidth_,
                                                  caretColor_,
                                                  selectionColor_,
                                                  caretVisible_,
                                                  !compositionText_.empty());
}

std::string InputBox::sanitizeSingleLine(const char* text, size_t length) {
    std::string result;
    if (text == nullptr || length == 0) {
        return result;
    }

    result.reserve(length);
    for (size_t i = 0; i < length; ++i) {
        if (text[i] == '\r' || text[i] == '\n') {
            continue;
        }
        result.push_back(text[i]);
    }
    return result;
}

void InputBox::configureInputParagraphMetrics() {
    paragraphStyle_.setMaxLines(1);
    paragraphStyle_.setTextStyle(textStyle_);

    auto strutStyle = paragraphStyle_.getStrutStyle();
    strutStyle.setStrutEnabled(true);
    strutStyle.setForceStrutHeight(true);
    strutStyle.setFontFamilies(textStyle_.getFontFamilies());
    strutStyle.setFontStyle(textStyle_.getFontStyle());
    if (IsFinitePositive(textStyle_.getFontSize())) {
        strutStyle.setFontSize(textStyle_.getFontSize());
    }
    if (IsFinitePositive(textStyle_.getHeight())) {
        strutStyle.setHeight(textStyle_.getHeight());
    }
    strutStyle.setHeightOverride(textStyle_.getHeightOverride());
    strutStyle.setHalfLeading(textStyle_.getHalfLeading());
    paragraphStyle_.setStrutStyle(strutStyle);
}

void InputBox::rebuildParagraphIfNeeded() {
    if (!paragraphDirty_ && paragraph_ != nullptr) {
        return;
    }

    paragraph_ = buildParagraph();
    paragraphDirty_ = false;
    scrollX_ = ClampScalar(scrollX_, 0.0f, std::max<SkScalar>(0.0f, contentWidth() - viewportWidth_));
}

std::unique_ptr<::skia::textlayout::Paragraph> InputBox::buildParagraph() const {
    return buildParagraphForText(displayText());
}

std::unique_ptr<::skia::textlayout::Paragraph> InputBox::buildParagraphForText(const std::string& text) const {
    auto fontCollection = GetFontCollection();
    auto unicodeProvider = GetUnicodeProvider();
    auto builder = ::skia::textlayout::ParagraphBuilder::make(paragraphStyle_,
                                                              fontCollection->unwrap(),
                                                              unicodeProvider->unwrap());
    builder->pushStyle(textStyle_);
    builder->addText(text.c_str(), text.size());
    auto paragraph = builder->Build();
    paragraph->layout(kSingleLineLayoutWidth);
    const auto measuredWidth =
            std::max<SkScalar>(paragraph->getLongestLine(), paragraph->getMaxIntrinsicWidth());
    paragraph->layout(std::max(viewportWidth_, measuredWidth));
    return paragraph;
}

std::string InputBox::displayText() const {
    if (compositionText_.empty()) {
        return boundaryMap_.text();
    }

    auto text = boundaryMap_.text();
    const auto start = displayCompositionStartUtf8();
    const auto end = displayCompositionReplacedEndUtf8();
    text.replace(start, end - start, compositionText_);
    return text;
}

TextBoundaryMap InputBox::displayBoundaryMap() const {
    return TextBoundaryMap(displayText());
}

size_t InputBox::displayCaretUtf8() const {
    if (compositionText_.empty()) {
        return boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest);
    }

    return displayCompositionStartUtf8() + compositionText_.size();
}

size_t InputBox::displayCompositionStartUtf8() const {
    if (hasSelection()) {
        return selectionStartUtf8();
    }

    return boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest);
}

size_t InputBox::displayCompositionEndUtf8() const {
    return displayCompositionStartUtf8() + compositionText_.size();
}

size_t InputBox::displayCompositionReplacedEndUtf8() const {
    if (hasSelection()) {
        return selectionEndUtf8();
    }

    return displayCompositionStartUtf8();
}

size_t InputBox::committedUtf8FromDisplay(size_t displayUtf8) const {
    if (compositionText_.empty()) {
        return displayUtf8;
    }

    const auto start = displayCompositionStartUtf8();
    const auto end = displayCompositionEndUtf8();
    const auto replacedEnd = displayCompositionReplacedEndUtf8();
    if (displayUtf8 <= start) {
        return displayUtf8;
    }
    if (displayUtf8 < end) {
        return start;
    }
    return displayUtf8 - compositionText_.size() + (replacedEnd - start);
}

std::vector<InputBoxCaretRect> InputBox::getRectsForDisplayRange(size_t startUtf8, size_t endUtf8) {
    rebuildParagraphIfNeeded();
    if (paragraph_ == nullptr || endUtf8 <= startUtf8) {
        return {};
    }

    const auto displayMap = displayBoundaryMap();
    const auto startUtf16 = displayMap.utf8ToUtf16(startUtf8);
    const auto endUtf16 = displayMap.utf8ToUtf16(endUtf8);
    if (endUtf16 <= startUtf16) {
        return {};
    }

    auto boxes = paragraph_->getRectsForRange(static_cast<unsigned>(startUtf16),
                                              static_cast<unsigned>(endUtf16),
                                              ::skia::textlayout::RectHeightStyle::kTight,
                                              ::skia::textlayout::RectWidthStyle::kTight);

    std::vector<InputBoxCaretRect> rects;
    rects.reserve(boxes.size());
    for (const auto& box: boxes) {
        if (!(box.rect.right() > box.rect.left()) || !(box.rect.bottom() > box.rect.top())) {
            continue;
        }

        rects.push_back(InputBoxCaretRect{
                ToFloat(box.rect.left()),
                ToFloat(box.rect.top()),
                ToFloat(box.rect.right()),
                ToFloat(box.rect.bottom()),
        });
    }
    return rects;
}

size_t InputBox::selectionStartUtf8() const {
    const auto anchor = boundaryMap_.snapUtf8(selectionAnchorUtf8_, TextBoundarySnapMode::Nearest);
    const auto focus = boundaryMap_.snapUtf8(selectionFocusUtf8_, TextBoundarySnapMode::Nearest);
    return std::min(anchor, focus);
}

size_t InputBox::selectionEndUtf8() const {
    const auto anchor = boundaryMap_.snapUtf8(selectionAnchorUtf8_, TextBoundarySnapMode::Nearest);
    const auto focus = boundaryMap_.snapUtf8(selectionFocusUtf8_, TextBoundarySnapMode::Nearest);
    return std::max(anchor, focus);
}

void InputBox::resetSelectionToCursor() {
    selectionAnchorUtf8_ = boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest);
    selectionFocusUtf8_ = selectionAnchorUtf8_;
    selectionAnchorAffinity_ = affinity_;
    selectionFocusAffinity_ = affinity_;
}

bool InputBox::replaceSelectionWith(std::string replacement) {
    if (!hasSelection()) {
        return false;
    }

    const auto start = selectionStartUtf8();
    const auto end = selectionEndUtf8();
    auto next = boundaryMap_.text();
    next.replace(start, end - start, replacement);
    const auto cursor = start + replacement.size();
    replaceText(std::move(next), cursor);
    return true;
}

bool InputBox::deleteSelection() {
    if (!hasSelection()) {
        return false;
    }

    return replaceSelectionWith(std::string());
}

SkScalar InputBox::contentWidth() {
    rebuildParagraphIfNeeded();
    if (paragraph_ == nullptr) {
        return viewportWidth_;
    }

    return std::max(viewportWidth_, std::max(paragraph_->getLongestLine(), paragraph_->getMaxIntrinsicWidth()));
}

void InputBox::replaceText(std::string text, size_t requestedCursor) {
    boundaryMap_.rebuild(std::move(text));
    compositionText_.clear();
    cursorUtf8_ = boundaryMap_.snapUtf8(std::min(requestedCursor, boundaryMap_.utf8Length()),
                                        TextBoundarySnapMode::Nearest);
    affinity_ = ::skia::textlayout::Affinity::kDownstream;
    resetSelectionToCursor();
    paragraphDirty_ = true;
    ensureCaretVisible();
}

void InputBox::markCompositionDirty() {
    paragraphDirty_ = true;
}

InputBoxDrawSnapshot::InputBoxDrawSnapshot(std::unique_ptr<::skia::textlayout::Paragraph> paragraph,
                                           InputBoxCaretRect caretRect,
                                           InputBoxMetrics metrics,
                                           InputBoxCaretRect compositionRect,
                                           std::vector<InputBoxCaretRect> selectionRects,
                                           SkScalar caretWidth,
                                           SkColor caretColor,
                                           SkColor selectionColor,
                                           bool caretVisible,
                                           bool compositionVisible)
        : paragraph_(std::move(paragraph)),
          caretRect_(caretRect),
          metrics_(metrics),
          compositionRect_(compositionRect),
          selectionRects_(std::move(selectionRects)),
          caretWidth_(caretWidth),
          caretColor_(caretColor),
          selectionColor_(selectionColor),
          caretVisible_(caretVisible),
          compositionVisible_(compositionVisible) {}

void InputBoxDrawSnapshot::paint(SkCanvas* canvas, SkScalar x, SkScalar y, SkScalar width, SkScalar height) const {
    if (canvas == nullptr) {
        return;
    }

    canvas->save();
    const auto clipWidth = width > 0.0f ? width : metrics_.viewportWidth;
    const auto clipHeight = height > 0.0f ? height : metrics_.viewportHeight;
    canvas->clipRect(SkRect::MakeXYWH(x, y, clipWidth, clipHeight));

    if (!selectionRects_.empty()) {
        SkPaint paint;
        paint.setColor(selectionColor_);
        paint.setStyle(SkPaint::kFill_Style);
        for (const auto& rect: selectionRects_) {
            canvas->drawRect(SkRect::MakeLTRB(x + rect.left - metrics_.scrollX,
                                              y + rect.top,
                                              x + rect.right - metrics_.scrollX,
                                              y + rect.bottom),
                             paint);
        }
    }

    if (paragraph_ != nullptr) {
        paragraph_->paint(canvas, x - metrics_.scrollX, y);
    }

    if (compositionVisible_) {
        SkPaint paint;
        paint.setColor(caretColor_);
        paint.setStyle(SkPaint::kFill_Style);
        const auto underlineTop =
                std::max(compositionRect_.top, compositionRect_.bottom - kCompositionUnderlineHeight);
        canvas->drawRect(SkRect::MakeLTRB(x + compositionRect_.left - metrics_.scrollX,
                                          y + underlineTop,
                                          x + compositionRect_.right - metrics_.scrollX,
                                          y + compositionRect_.bottom),
                         paint);
    }

    if (caretVisible_) {
        SkPaint paint;
        paint.setColor(caretColor_);
        paint.setStyle(SkPaint::kFill_Style);
        const auto caretRight = std::max(caretRect_.right, caretRect_.left + caretWidth_);
        canvas->drawRect(SkRect::MakeLTRB(x + caretRect_.left - metrics_.scrollX,
                                          y + caretRect_.top,
                                          x + caretRight - metrics_.scrollX,
                                          y + caretRect_.bottom),
                         paint);
    }

    canvas->restore();
}

} // namespace milestro::skia::textlayout

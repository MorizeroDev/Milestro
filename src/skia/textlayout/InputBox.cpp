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

bool HasGlyphClusterStart(SkUnicode::CodeUnitFlags flags) {
    return (flags & SkUnicode::CodeUnitFlags::kGlyphClusterStart) ==
           SkUnicode::CodeUnitFlags::kGlyphClusterStart;
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
    paragraphStyle_.setMaxLines(1);
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
    paragraphStyle_.setMaxLines(1);
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
    ensureCaretVisible();
}

void InputBox::insertText(const char* text, size_t length) {
    auto insert = sanitizeSingleLine(text, length);
    if (insert.empty()) {
        return;
    }

    compositionText_.clear();
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

bool InputBox::deleteBackward() {
    const bool clearedComposition = clearComposition();
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

bool InputBox::movePrevious() {
    const bool clearedComposition = clearComposition();
    const auto previous = boundaryMap_.previousBoundary(cursorUtf8_);
    if (previous == cursorUtf8_) {
        return clearedComposition;
    }

    cursorUtf8_ = previous;
    affinity_ = ::skia::textlayout::Affinity::kDownstream;
    ensureCaretVisible();
    return true;
}

bool InputBox::moveNext() {
    const bool clearedComposition = clearComposition();
    const auto next = boundaryMap_.nextBoundary(cursorUtf8_);
    if (next == cursorUtf8_) {
        return clearedComposition;
    }

    cursorUtf8_ = next;
    affinity_ = ::skia::textlayout::Affinity::kDownstream;
    ensureCaretVisible();
    return true;
}

bool InputBox::hitTest(SkScalar x, SkScalar y) {
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
    cursorUtf8_ = boundaryMap_.snapUtf8(utf8, TextBoundarySnapMode::Nearest);
    affinity_ = hit.affinity;
    ensureCaretVisible();
    return clearedComposition || cursorUtf8_ != oldCursor || affinity_ != oldAffinity;
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
        rect.bottom = viewportHeight_;
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
    const float top = hasLineMetrics ? ToFloat(lineMetrics.fBaseline - lineMetrics.fAscent) : 0.0f;
    const float bottom = hasLineMetrics ? ToFloat(lineMetrics.fBaseline + lineMetrics.fDescent) : viewportHeight_;

    SkScalar x = 0.0f;
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
    auto metrics = getMetrics();
    return std::make_unique<InputBoxDrawSnapshot>(std::move(paragraph),
                                                  caretRect,
                                                  metrics,
                                                  compositionRect,
                                                  caretWidth_,
                                                  caretColor_,
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
    paragraph->layout(paragraphLayoutWidth());
    return paragraph;
}

std::string InputBox::displayText() const {
    if (compositionText_.empty()) {
        return boundaryMap_.text();
    }

    auto text = boundaryMap_.text();
    const auto cursor = boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest);
    text.insert(cursor, compositionText_);
    return text;
}

TextBoundaryMap InputBox::displayBoundaryMap() const {
    return TextBoundaryMap(displayText());
}

size_t InputBox::displayCaretUtf8() const {
    return boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest) + compositionText_.size();
}

size_t InputBox::displayCompositionStartUtf8() const {
    return boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest);
}

size_t InputBox::displayCompositionEndUtf8() const {
    return displayCompositionStartUtf8() + compositionText_.size();
}

size_t InputBox::committedUtf8FromDisplay(size_t displayUtf8) const {
    if (compositionText_.empty()) {
        return displayUtf8;
    }

    const auto start = displayCompositionStartUtf8();
    const auto end = displayCompositionEndUtf8();
    if (displayUtf8 <= start) {
        return displayUtf8;
    }
    if (displayUtf8 < end) {
        return start;
    }
    return displayUtf8 - compositionText_.size();
}

SkScalar InputBox::paragraphLayoutWidth() const {
    return std::max(viewportWidth_, kSingleLineLayoutWidth);
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
                                           SkScalar caretWidth,
                                           SkColor caretColor,
                                           bool caretVisible,
                                           bool compositionVisible)
        : paragraph_(std::move(paragraph)),
          caretRect_(caretRect),
          metrics_(metrics),
          compositionRect_(compositionRect),
          caretWidth_(caretWidth),
          caretColor_(caretColor),
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

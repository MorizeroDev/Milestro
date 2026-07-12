#include "Milestro/skia/textlayout/InputBox.h"

#include "Milestro/skia/textlayout/NoWrapLayout.h"

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

constexpr SkScalar kCaretScrollPadding = 2.0f;
constexpr SkScalar kCompositionUnderlineHeight = 1.5f;
constexpr SkScalar kFallbackAscentRatio = 0.8f;
constexpr size_t kMaxEditHistoryGroups = 100;
constexpr size_t kMaxEditHistoryBytes = 1024 * 1024;
constexpr size_t kInputBoxMaxLines = 1 << 20;
constexpr auto kEditMergeTimeout = std::chrono::milliseconds(1000);

struct VerticalMetrics {
    SkScalar ascent = 0.0f;
    SkScalar descent = 0.0f;
    SkScalar height = 0.0f;
};

struct DisplayTextBuilder {
    std::string text;
    std::vector<size_t> committedUtf8ForDisplayUtf8 = {0};
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

SkScalar DefaultBaselineY(SkScalar viewportHeight, const VerticalMetrics& metrics) {
    if (!IsFinitePositive(viewportHeight) || !IsFinitePositive(metrics.height)) {
        return metrics.ascent;
    }

    return std::max<SkScalar>(0.0f, (viewportHeight - metrics.height) * 0.5f) + metrics.ascent;
}

SkScalar BaselineOffsetY(SkScalar viewportHeight, SkScalar lineBaseline, const VerticalMetrics& metrics) {
    if (!std::isfinite(lineBaseline)) {
        return 0.0f;
    }
    return DefaultBaselineY(viewportHeight, metrics) - lineBaseline;
}

size_t LineMetricStartUtf8(const ::skia::textlayout::LineMetrics& lineMetrics, const TextBoundaryMap& map) {
    return map.utf16ToUtf8(static_cast<size_t>(lineMetrics.fStartIndex));
}

size_t LineMetricEndUtf8(const ::skia::textlayout::LineMetrics& lineMetrics, const TextBoundaryMap& map) {
    const auto end = map.utf16ToUtf8(static_cast<size_t>(lineMetrics.fEndIndex));
    const auto& text = map.text();
    if (lineMetrics.fHardBreak && end > 0 && end <= text.size() && text[end - 1] == '\n') {
        return end - 1;
    }
    return end;
}

size_t LineMetricEndIncludingNewlineUtf8(const ::skia::textlayout::LineMetrics& lineMetrics,
                                         const TextBoundaryMap& map) {
    return map.utf16ToUtf8(static_cast<size_t>(lineMetrics.fEndIncludingNewline));
}

bool HasPositiveArea(const InputBoxCaretRect& rect) {
    return rect.right - rect.left > 0.001f && rect.bottom - rect.top > 0.001f;
}

bool IntersectRects(const InputBoxCaretRect& a, const InputBoxCaretRect& b, InputBoxCaretRect& intersection) {
    intersection.left = std::max(a.left, b.left);
    intersection.top = std::max(a.top, b.top);
    intersection.right = std::min(a.right, b.right);
    intersection.bottom = std::min(a.bottom, b.bottom);
    return HasPositiveArea(intersection);
}

std::vector<InputBoxCaretRect> SubtractRect(const InputBoxCaretRect& rect, const InputBoxCaretRect& cut) {
    InputBoxCaretRect intersection;
    if (!IntersectRects(rect, cut, intersection)) {
        return {rect};
    }

    std::vector<InputBoxCaretRect> pieces;
    pieces.reserve(4);
    const InputBoxCaretRect candidates[] = {
            {rect.left, rect.top, rect.right, intersection.top},
            {rect.left, intersection.bottom, rect.right, rect.bottom},
            {rect.left, intersection.top, intersection.left, intersection.bottom},
            {intersection.right, intersection.top, rect.right, intersection.bottom},
    };
    for (const auto& candidate: candidates) {
        if (HasPositiveArea(candidate)) {
            pieces.push_back(candidate);
        }
    }
    return pieces;
}

std::vector<InputBoxCaretRect> RemoveSelectionRectOverlaps(const std::vector<InputBoxCaretRect>& rects) {
    std::vector<InputBoxCaretRect> result;
    result.reserve(rects.size());
    for (const auto& rect: rects) {
        if (!HasPositiveArea(rect)) {
            continue;
        }

        std::vector<InputBoxCaretRect> fragments = {rect};
        for (const auto& existing: result) {
            std::vector<InputBoxCaretRect> nextFragments;
            for (const auto& fragment: fragments) {
                auto pieces = SubtractRect(fragment, existing);
                nextFragments.insert(nextFragments.end(), pieces.begin(), pieces.end());
            }
            fragments = std::move(nextFragments);
            if (fragments.empty()) {
                break;
            }
        }

        result.insert(result.end(), fragments.begin(), fragments.end());
    }
    return result;
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
    return (flags & SkUnicode::CodeUnitFlags::kGlyphClusterStart) == SkUnicode::CodeUnitFlags::kGlyphClusterStart;
}

VerticalMetrics ResolveStyleVerticalMetrics(const ::skia::textlayout::TextStyle& textStyle) {
    const SkScalar fontSize = IsFinitePositive(textStyle.getFontSize()) ? textStyle.getFontSize() : 1.0f;

    SkFontMetrics fontMetrics;
    textStyle.getFontMetrics(&fontMetrics);

    auto ascent = std::isfinite(fontMetrics.fAscent) && fontMetrics.fAscent < 0.0f ? -fontMetrics.fAscent : 0.0f;
    auto descent = std::isfinite(fontMetrics.fDescent) && fontMetrics.fDescent > 0.0f ? fontMetrics.fDescent : 0.0f;
    const auto leading =
            std::isfinite(fontMetrics.fLeading) && fontMetrics.fLeading > 0.0f ? fontMetrics.fLeading : 0.0f;

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

void AppendDisplayByte(DisplayTextBuilder& builder, char ch, size_t committedUtf8BeforeByte) {
    builder.text.push_back(ch);
    builder.committedUtf8ForDisplayUtf8.push_back(committedUtf8BeforeByte);
}

void AppendRawDisplayRange(DisplayTextBuilder& builder,
                           const std::string& text,
                           size_t startUtf8,
                           size_t endUtf8,
                           size_t committedUtf8Start) {
    if (endUtf8 <= startUtf8) {
        builder.committedUtf8ForDisplayUtf8.back() = committedUtf8Start;
        return;
    }

    builder.committedUtf8ForDisplayUtf8.back() = committedUtf8Start;
    for (size_t offset = startUtf8; offset < endUtf8; ++offset) {
        AppendDisplayByte(builder, text[offset], committedUtf8Start + offset - startUtf8 + 1);
    }
    builder.committedUtf8ForDisplayUtf8.back() = committedUtf8Start + endUtf8 - startUtf8;
}

void AppendCompositionDisplayRange(DisplayTextBuilder& builder,
                                   const std::string& text,
                                   size_t startUtf8,
                                   size_t endUtf8,
                                   size_t compositionStartUtf8,
                                   size_t compositionReplacedEndUtf8) {
    if (endUtf8 <= startUtf8) {
        builder.committedUtf8ForDisplayUtf8.back() = compositionReplacedEndUtf8;
        return;
    }

    builder.committedUtf8ForDisplayUtf8.back() = compositionStartUtf8;
    for (size_t offset = startUtf8; offset < endUtf8; ++offset) {
        AppendDisplayByte(builder, text[offset], compositionStartUtf8);
    }
    builder.committedUtf8ForDisplayUtf8.back() = compositionReplacedEndUtf8;
}

bool IsSingleNewlineCluster(const std::string& text, size_t startUtf8, size_t endUtf8) {
    return endUtf8 == startUtf8 + 1 && startUtf8 < text.size() && text[startUtf8] == '\n';
}

void AppendMaskedCluster(DisplayTextBuilder& builder,
                         const std::string& maskText,
                         size_t committedUtf8Start,
                         size_t committedUtf8End) {
    builder.committedUtf8ForDisplayUtf8.back() = committedUtf8Start;
    for (size_t offset = 0; offset < maskText.size(); ++offset) {
        AppendDisplayByte(builder, maskText[offset], committedUtf8Start);
    }
    builder.committedUtf8ForDisplayUtf8.back() = committedUtf8End;
}

std::vector<size_t> BuildDisplayUtf8ForCommittedUtf8(const std::vector<size_t>& committedUtf8ForDisplayUtf8,
                                                     size_t committedUtf8Length) {
    constexpr size_t kUnset = std::numeric_limits<size_t>::max();
    std::vector<size_t> displayUtf8ForCommittedUtf8(committedUtf8Length + 1, kUnset);

    for (size_t displayUtf8 = 0; displayUtf8 < committedUtf8ForDisplayUtf8.size(); ++displayUtf8) {
        const auto committedUtf8 = committedUtf8ForDisplayUtf8[displayUtf8];
        if (committedUtf8 <= committedUtf8Length && displayUtf8ForCommittedUtf8[committedUtf8] == kUnset) {
            displayUtf8ForCommittedUtf8[committedUtf8] = displayUtf8;
        }
    }

    size_t previousDisplayUtf8 = 0;
    for (size_t committedUtf8 = 0; committedUtf8 < displayUtf8ForCommittedUtf8.size(); ++committedUtf8) {
        if (displayUtf8ForCommittedUtf8[committedUtf8] == kUnset) {
            displayUtf8ForCommittedUtf8[committedUtf8] = previousDisplayUtf8;
        } else {
            previousDisplayUtf8 = displayUtf8ForCommittedUtf8[committedUtf8];
        }
    }

    return displayUtf8ForCommittedUtf8;
}

std::string FirstDisplayClusterOrDefault(std::string text, const std::string& fallback) {
    if (text.empty()) {
        return fallback;
    }

    TextBoundaryMap map(std::move(text));
    if (map.utf8Length() == 0 || map.boundaryCount() < 2) {
        return fallback;
    }

    const auto start = map.boundaryAt(0);
    const auto end = map.boundaryAt(1);
    const auto& value = map.text();
    if (end <= start || IsSingleNewlineCluster(value, start, end)) {
        return fallback;
    }

    return value.substr(start, end - start);
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

size_t InputBox::DisplayTextState::committedUtf8FromDisplay(size_t displayUtf8) const {
    if (committedUtf8ForDisplayUtf8.empty()) {
        return 0;
    }

    return committedUtf8ForDisplayUtf8[std::min(displayUtf8, committedUtf8ForDisplayUtf8.size() - 1)];
}

size_t InputBox::DisplayTextState::displayUtf8FromCommitted(size_t committedUtf8) const {
    if (displayUtf8ForCommittedUtf8.empty()) {
        return 0;
    }

    return displayUtf8ForCommittedUtf8[std::min(committedUtf8, displayUtf8ForCommittedUtf8.size() - 1)];
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
                          unicode->computeCodeUnitFlags(text_.data(), static_cast<int>(text_.size()), false, &flags);

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
        // Do not copy the wrapped Skia ParagraphStyle wholesale across the dylib boundary.
        // In Skia debug builds, a default empty SkString from another image's gEmptyRec can
        // trip SkString::validate() during the copy. InputBox only needs paragraph-level
        // primitive settings; text style is provided separately and re-applied below.
        paragraphStyle_ = ::skia::textlayout::ParagraphStyle();
        paragraphStyle_.setTextDirection(paragraphStyle->getTextDirection());
        paragraphStyle_.setTextAlign(paragraphStyle->getTextAlign());
        paragraphStyle_.setHeight(paragraphStyle->getHeight());
        paragraphStyle_.setTextHeightBehavior(paragraphStyle->getTextHeightBehavior());
        paragraphStyle_.setReplaceTabCharacters(paragraphStyle->getReplaceTabCharacters());
        paragraphStyle_.setApplyRoundingHack(paragraphStyle->getApplyRoundingHack());
        singleLineInput_ = !paragraphStyle->unlimited_lines() && paragraphStyle->getMaxLines() == 1;
        if (!paragraphStyle->hintingIsOn()) {
            paragraphStyle_.turnHintingOff();
        }
    }
    if (textStyle != nullptr) {
        textStyleDeclaration_ = *textStyle;
        textStyle_ = textStyle->spawn();
        paragraphStyle_.setTextStyle(textStyle_);
    }
    configureInputParagraphMetrics();
}

InputBox::~InputBox() = default;

void InputBox::setText(const char* text, size_t length) {
    compositionText_.clear();
    resetPreferredCaretX();
    replaceText(sanitizePlainText(text, length), cursorUtf8_);
    clearEditHistory();
}

void InputBox::setViewport(SkScalar width, SkScalar height) {
    viewportWidth_ = std::max<SkScalar>(1.0f, width);
    viewportHeight_ = std::max<SkScalar>(1.0f, height);
    markParagraphDirty();
    ensureCaretVisible();
}

void InputBox::setSoftWrap(bool softWrap) {
    if (softWrap_ == softWrap) {
        return;
    }

    softWrap_ = softWrap;
    markParagraphDirty();
    if (softWrap_) {
        scrollX_ = 0.0f;
    }
    ensureCaretVisible();
}

void InputBox::setFocused(bool focused) {
    if (focused_ == focused) {
        return;
    }

    focused_ = focused;
    markParagraphDirty();
    if (focused_) {
        ensureCaretVisible();
    }
}

void InputBox::setTextOverflow(TextOverflow textOverflow) {
    if (textOverflow_ == textOverflow) {
        return;
    }

    textOverflow_ = textOverflow;
    markParagraphDirty();
}

void InputBox::setEllipsis(const char* text, size_t length) {
    auto next = sanitizePlainText(text, length);
    if (next.empty()) {
        next = "\xE2\x80\xA6";
    }

    if (ellipsisText_ == next) {
        return;
    }

    ellipsisText_ = std::move(next);
    markParagraphDirty();
}

void InputBox::setMaskInput(bool maskInput) {
    if (maskInput_ == maskInput) {
        return;
    }

    maskInput_ = maskInput;
    markParagraphDirty();
    ensureCaretVisible();
}

void InputBox::setMaskChar(const char* text, size_t length) {
    const auto next = FirstDisplayClusterOrDefault(sanitizePlainText(text, length), "*");
    if (maskText_ == next) {
        return;
    }

    maskText_ = next;
    if (maskInput_) {
        markParagraphDirty();
        ensureCaretVisible();
    }
}

void InputBox::setCaretWidth(SkScalar width) {
    caretWidth_ = std::max<SkScalar>(0.0f, width);
}

void InputBox::setAutoMargin(bool left, bool top, bool right, bool bottom) {
    if (autoMarginLeft_ == left && autoMarginTop_ == top && autoMarginRight_ == right && autoMarginBottom_ == bottom) {
        return;
    }

    autoMarginLeft_ = left;
    autoMarginTop_ = top;
    autoMarginRight_ = right;
    autoMarginBottom_ = bottom;
    markSelectionRectsDirty();
    ensureCaretVisible();
}

void InputBox::setCursorUtf8(size_t utf8Offset, ::skia::textlayout::Affinity affinity) {
    const auto oldCursor = cursorUtf8_;
    const auto oldAffinity = affinity_;
    const auto oldAnchor = selectionAnchorUtf8_;
    const auto oldFocus = selectionFocusUtf8_;
    const auto oldAnchorAffinity = selectionAnchorAffinity_;
    const auto oldFocusAffinity = selectionFocusAffinity_;

    cursorUtf8_ = boundaryMap_.snapUtf8(utf8Offset, TextBoundarySnapMode::Nearest);
    affinity_ = affinity;
    resetSelectionToCursor();
    resetPreferredCaretX();
    if (cursorUtf8_ != oldCursor || affinity_ != oldAffinity || selectionAnchorUtf8_ != oldAnchor ||
        selectionFocusUtf8_ != oldFocus || selectionAnchorAffinity_ != oldAnchorAffinity ||
        selectionFocusAffinity_ != oldFocusAffinity) {
        breakUndoGroup();
    }
    ensureCaretVisible();
}

void InputBox::insertText(const char* text, size_t length) {
    auto insert = sanitizePlainText(text, length);
    if (insert.empty()) {
        return;
    }

    const auto before = captureEditState();
    compositionText_.clear();
    if (hasSelection()) {
        replaceSelectionWith(std::move(insert));
        recordEdit(before, EditKind::ReplaceSelection);
        return;
    }

    auto next = boundaryMap_.text();
    const auto cursor = boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest);
    next.insert(cursor, insert);
    replaceText(std::move(next), cursor + insert.size());
    recordEdit(before, EditKind::Typing);
}

bool InputBox::setComposition(const char* text, size_t length) {
    auto composition = sanitizePlainText(text, length);
    if (composition == compositionText_) {
        return false;
    }

    compositionText_ = std::move(composition);
    markCompositionDirty();
    ensureCaretVisible();
    return true;
}

bool InputBox::commitComposition(const char* text, size_t length) {
    auto commitText = sanitizePlainText(text, length);
    if (commitText.empty()) {
        commitText = compositionText_;
    }

    const auto before = captureEditState();
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
        recordEdit(before, EditKind::ImeCommit);
        return true;
    }

    auto next = boundaryMap_.text();
    const auto cursor = boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest);
    next.insert(cursor, commitText);
    replaceText(std::move(next), cursor + commitText.size());
    recordEdit(before, EditKind::ImeCommit);
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

    const bool changed = selectionAnchorUtf8_ != oldAnchor || selectionFocusUtf8_ != oldFocus ||
                         selectionAnchorAffinity_ != oldAnchorAffinity || selectionFocusAffinity_ != oldFocusAffinity ||
                         cursorUtf8_ != oldCursor || affinity_ != oldAffinity;
    if (changed) {
        markSelectionStateDirty();
        breakUndoGroup();
    }
    resetPreferredCaretX();
    ensureCaretVisible();
    return changed;
}

bool InputBox::clearSelection() {
    const bool changed = hasSelection();
    resetSelectionToCursor();
    if (changed) {
        breakUndoGroup();
    }
    return changed;
}

bool InputBox::selectAll() {
    return setSelectionUtf8(0,
                            boundaryMap_.utf8Length(),
                            ::skia::textlayout::Affinity::kDownstream,
                            ::skia::textlayout::Affinity::kDownstream);
}

bool InputBox::deleteBackward() {
    const auto before = captureEditState();
    const bool clearedComposition = clearComposition();
    if (deleteSelection()) {
        recordEdit(before, EditKind::DeleteSelection);
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
    recordEdit(before, EditKind::DeleteBackward);
    return true;
}

bool InputBox::deleteForward() {
    const auto before = captureEditState();
    const bool clearedComposition = clearComposition();
    if (deleteSelection()) {
        recordEdit(before, EditKind::DeleteSelection);
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
    recordEdit(before, EditKind::DeleteForward);
    return true;
}

bool InputBox::movePrevious(bool extendSelection) {
    resetPreferredCaretX();
    const auto oldCursor = cursorUtf8_;
    const auto oldAffinity = affinity_;
    const bool clearedComposition = clearComposition();
    if (!extendSelection && hasSelection()) {
        cursorUtf8_ = selectionStartUtf8();
        affinity_ = ::skia::textlayout::Affinity::kDownstream;
        resetSelectionToCursor();
        ensureCaretVisible();
        breakUndoGroup();
        return true;
    }

    const auto previous = boundaryMap_.previousBoundary(cursorUtf8_);
    if (previous == cursorUtf8_) {
        if (clearedComposition) {
            breakUndoGroup();
        }
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
    if (clearedComposition || cursorUtf8_ != oldCursor || affinity_ != oldAffinity) {
        breakUndoGroup();
    }
    return true;
}

bool InputBox::moveNext(bool extendSelection) {
    resetPreferredCaretX();
    const auto oldCursor = cursorUtf8_;
    const auto oldAffinity = affinity_;
    const bool clearedComposition = clearComposition();
    if (!extendSelection && hasSelection()) {
        cursorUtf8_ = selectionEndUtf8();
        affinity_ = ::skia::textlayout::Affinity::kDownstream;
        resetSelectionToCursor();
        ensureCaretVisible();
        breakUndoGroup();
        return true;
    }

    const auto next = boundaryMap_.nextBoundary(cursorUtf8_);
    if (next == cursorUtf8_) {
        if (clearedComposition) {
            breakUndoGroup();
        }
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
    if (clearedComposition || cursorUtf8_ != oldCursor || affinity_ != oldAffinity) {
        breakUndoGroup();
    }
    return true;
}

bool InputBox::moveUp(bool extendSelection) {
    return moveVertical(-1, extendSelection);
}

bool InputBox::moveDown(bool extendSelection) {
    return moveVertical(1, extendSelection);
}

bool InputBox::moveLineStart(bool extendSelection) {
    resetPreferredCaretX();
    const bool clearedComposition = clearComposition();
    ::skia::textlayout::LineMetrics lineMetrics;
    int lineNumber = 0;
    const auto displayState = displayTextState();
    const auto displayCursor =
            displayState.displayUtf8FromCommitted(boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest));
    if (!resolveLineMetricsForDisplayUtf8(displayCursor, affinity_, lineMetrics, lineNumber)) {
        if (clearedComposition) {
            breakUndoGroup();
        }
        return clearedComposition;
    }

    const auto displayMap = displayBoundaryMap();
    const bool moved = moveToUtf8(committedUtf8FromDisplay(LineMetricStartUtf8(lineMetrics, displayMap)),
                                  ::skia::textlayout::Affinity::kDownstream,
                                  extendSelection,
                                  true);
    if (clearedComposition && !moved) {
        breakUndoGroup();
    }
    return clearedComposition || moved;
}

bool InputBox::moveLineEnd(bool extendSelection) {
    resetPreferredCaretX();
    const bool clearedComposition = clearComposition();
    ::skia::textlayout::LineMetrics lineMetrics;
    int lineNumber = 0;
    const auto displayState = displayTextState();
    const auto displayCursor =
            displayState.displayUtf8FromCommitted(boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest));
    if (!resolveLineMetricsForDisplayUtf8(displayCursor, affinity_, lineMetrics, lineNumber)) {
        if (clearedComposition) {
            breakUndoGroup();
        }
        return clearedComposition;
    }

    const auto displayMap = displayBoundaryMap();
    const bool moved = moveToUtf8(committedUtf8FromDisplay(LineMetricEndUtf8(lineMetrics, displayMap)),
                                  ::skia::textlayout::Affinity::kUpstream,
                                  extendSelection,
                                  true);
    if (clearedComposition && !moved) {
        breakUndoGroup();
    }
    return clearedComposition || moved;
}

bool InputBox::moveDocumentStart(bool extendSelection) {
    resetPreferredCaretX();
    const bool clearedComposition = clearComposition();
    const bool moved = moveToUtf8(0, ::skia::textlayout::Affinity::kDownstream, extendSelection, true);
    if (clearedComposition && !moved) {
        breakUndoGroup();
    }
    return clearedComposition || moved;
}

bool InputBox::moveDocumentEnd(bool extendSelection) {
    resetPreferredCaretX();
    const bool clearedComposition = clearComposition();
    const bool moved =
            moveToUtf8(boundaryMap_.utf8Length(), ::skia::textlayout::Affinity::kUpstream, extendSelection, true);
    if (clearedComposition && !moved) {
        breakUndoGroup();
    }
    return clearedComposition || moved;
}

bool InputBox::hitTest(SkScalar x, SkScalar y, bool extendSelection) {
    resetPreferredCaretX();
    rebuildParagraphIfNeeded();
    if (paragraph_ == nullptr) {
        return false;
    }

    const auto oldCursor = cursorUtf8_;
    const auto oldAffinity = affinity_;
    const auto hit =
            paragraph_->getGlyphPositionAtCoordinate(x + scrollX_ - visualOffsetX(), y + scrollY_ - visualOffsetY());
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
    const bool changed = clearedComposition || selectionChanged || cursorUtf8_ != oldCursor || affinity_ != oldAffinity;
    if (changed) {
        breakUndoGroup();
    }
    return changed;
}

bool InputBox::undo() {
    if (undoStack_.empty()) {
        return false;
    }

    auto group = std::move(undoStack_.back());
    undoStack_.pop_back();
    restoreEditState(group.before);
    redoStack_.push_back(std::move(group));
    breakUndoGroup();
    return true;
}

bool InputBox::redo() {
    if (redoStack_.empty()) {
        return false;
    }

    auto group = std::move(redoStack_.back());
    redoStack_.pop_back();
    restoreEditState(group.after);
    undoStack_.push_back(std::move(group));
    pruneEditHistory(undoStack_);
    breakUndoGroup();
    return true;
}

void InputBox::breakUndoGroup() {
    undoMergeBarrier_ = true;
}

void InputBox::ensureCaretVisible() {
    rebuildParagraphIfNeeded();
    ensureRectVisible(activeEnsureVisibleRect());
}

bool InputBox::scrollByX(SkScalar delta) {
    if (!std::isfinite(delta)) {
        return false;
    }

    rebuildParagraphIfNeeded();
    const auto previous = scrollX_;
    if (softWrap_) {
        scrollX_ = 0.0f;
        return scrollX_ != previous;
    }

    const auto maxScrollX = std::max<SkScalar>(0.0f, contentWidth() - viewportWidth_);
    scrollX_ = ClampScalar(scrollX_ + delta, 0.0f, maxScrollX);
    return scrollX_ != previous;
}

bool InputBox::scrollByY(SkScalar delta) {
    if (!std::isfinite(delta)) {
        return false;
    }

    rebuildParagraphIfNeeded();
    const auto previous = scrollY_;
    scrollY_ = ClampScalar(scrollY_ + delta, 0.0f, maxScrollY());
    return scrollY_ != previous;
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
    int lineNumber = 0;
    const bool hasLineMetrics = resolveLineMetricsForDisplayUtf8(displayUtf8, affinity_, lineMetrics, lineNumber);
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
    if (hasLineMetrics) {
        x = caretXForDisplayOffset(displayUtf8, lineMetrics, displayMap);
    } else if (utf16Length > 0) {
        x = contentWidth();
    }

    const auto offsetX = visualOffsetX();
    rect.left = x + offsetX;
    rect.right = x + caretWidth_ + offsetX;
    const auto offsetY = visualOffsetY();
    rect.top = top + offsetY;
    rect.bottom = bottom + offsetY;
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

    auto rect = boxes.back().rect;

    const auto offsetX = visualOffsetX();
    const auto offsetY = visualOffsetY();
    return InputBoxCaretRect{
            ToFloat(rect.left() + offsetX),
            ToFloat(rect.top() + offsetY),
            ToFloat(rect.right() + offsetX),
            ToFloat(rect.bottom() + offsetY),
    };
}

std::vector<InputBoxCaretRect> InputBox::getSelectionRects() {
    const auto selectionRects = getSelectionRectsSnapshot();
    if (selectionRects == nullptr) {
        return {};
    }

    return *selectionRects;
}

InputBoxMetrics InputBox::getMetrics() {
    rebuildParagraphIfNeeded();
    return metricsForParagraph(paragraph_.get(), scrollX_, scrollY_, contentWidth());
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

    const auto displayState = displayTextState();
    const auto displayMap = TextBoundaryMap(displayState.text);
    metrics.startUtf8 = displayState.committedUtf8FromDisplay(LineMetricStartUtf8(lineMetrics, displayMap));
    metrics.endUtf8 = displayState.committedUtf8FromDisplay(LineMetricEndUtf8(lineMetrics, displayMap));
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
    const auto ellipsisDisplay = shouldUseEllipsisDisplay();
    auto paintParagraph = ellipsisDisplay ? getPaintParagraphSnapshot() : nullptr;
    auto* paragraph = ellipsisDisplay ? paintParagraph.get() : paragraph_.get();
    const auto paintScrollX = ellipsisDisplay ? 0.0f : scrollX_;
    const auto paintScrollY = ellipsisDisplay ? 0.0f : scrollY_;
    const auto offsetX = ellipsisDisplay ? visualOffsetXForParagraph(paragraph) : visualOffsetX();
    const auto offsetY = ellipsisDisplay ? visualOffsetYForParagraph(paragraph) : visualOffsetY();
    const auto clipToViewport = shouldClipDisplayToViewport();
    canvas->save();
    if (clipToViewport) {
        const auto clipWidth = width > 0.0f ? width : viewportWidth_;
        const auto clipHeight = height > 0.0f ? height : viewportHeight_;
        canvas->clipRect(SkRect::MakeXYWH(x, y, clipWidth, clipHeight));
    }

    const auto selectionRects = ellipsisDisplay ? nullptr : getSelectionRectsSnapshot();
    if (selectionRects != nullptr && !selectionRects->empty()) {
        SkPaint paint;
        paint.setColor(selectionColor_);
        paint.setStyle(SkPaint::kFill_Style);
        for (const auto& rect: *selectionRects) {
            canvas->drawRect(SkRect::MakeLTRB(x + rect.left - paintScrollX,
                                              y + rect.top - paintScrollY,
                                              x + rect.right - paintScrollX,
                                              y + rect.bottom - paintScrollY),
                             paint);
        }
    }

    if (paragraph != nullptr) {
        paragraph->paint(canvas, x + offsetX - paintScrollX, y + offsetY - paintScrollY);
    }

    if (!ellipsisDisplay && !compositionText_.empty()) {
        const auto composition = getCompositionRect();
        SkPaint paint;
        paint.setColor(caretColor_);
        paint.setStyle(SkPaint::kFill_Style);
        const auto underlineTop = std::max(composition.top, composition.bottom - kCompositionUnderlineHeight);
        canvas->drawRect(SkRect::MakeLTRB(x + composition.left - paintScrollX,
                                          y + underlineTop - paintScrollY,
                                          x + composition.right - paintScrollX,
                                          y + composition.bottom - paintScrollY),
                         paint);
    }

    if (!ellipsisDisplay && caretVisible_ && caretWidth_ > 0.0f) {
        const auto caret = getCaretRect();
        SkPaint paint;
        paint.setColor(caretColor_);
        paint.setStyle(SkPaint::kFill_Style);
        canvas->drawRect(SkRect::MakeLTRB(x + caret.left - paintScrollX,
                                          y + caret.top - paintScrollY,
                                          x + caret.right - paintScrollX,
                                          y + caret.bottom - paintScrollY),
                         paint);
    }

    canvas->restore();
}

std::unique_ptr<InputBoxDrawSnapshot> InputBox::createDrawSnapshot() {
    rebuildParagraphIfNeeded();
    const auto ellipsisDisplay = shouldUseEllipsisDisplay();
    auto paragraph = getPaintParagraphSnapshot();
    auto caretRect = getCaretRect();
    auto compositionRect = getCompositionRect();
    auto selectionRects = ellipsisDisplay ? nullptr : getSelectionRectsSnapshot();
    const auto snapshotScrollX = ellipsisDisplay ? 0.0f : scrollX_;
    const auto snapshotScrollY = ellipsisDisplay ? 0.0f : scrollY_;
    const auto snapshotContentWidth = ellipsisDisplay ? viewportWidth_ : contentWidth();
    auto metrics = metricsForParagraph(paragraph.get(), snapshotScrollX, snapshotScrollY, snapshotContentWidth);
    const auto snapshotOffsetX = ellipsisDisplay ? visualOffsetXForParagraph(paragraph.get()) : visualOffsetX();
    const auto snapshotOffsetY = ellipsisDisplay ? visualOffsetYForParagraph(paragraph.get()) : visualOffsetY();
    return std::make_unique<InputBoxDrawSnapshot>(std::move(paragraph),
                                                  caretRect,
                                                  metrics,
                                                  compositionRect,
                                                  std::move(selectionRects),
                                                  caretWidth_,
                                                  snapshotOffsetX,
                                                  snapshotOffsetY,
                                                  caretColor_,
                                                  selectionColor_,
                                                  !ellipsisDisplay && caretVisible_,
                                                  !ellipsisDisplay && !compositionText_.empty(),
                                                  shouldClipDisplayToViewport());
}

std::string InputBox::sanitizePlainText(const char* text, size_t length) {
    std::string result;
    if (text == nullptr || length == 0) {
        return result;
    }

    result.reserve(length);
    for (size_t i = 0; i < length; ++i) {
        if (text[i] == '\r') {
            result.push_back('\n');
            if (i + 1 < length && text[i + 1] == '\n') {
                ++i;
            }
            continue;
        }
        result.push_back(text[i]);
    }
    return result;
}

void InputBox::configureInputParagraphMetrics() {
    paragraphStyle_.setMaxLines(kInputBoxMaxLines);
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

void InputBox::markParagraphDirty() {
    paragraphDirty_ = true;
    paintParagraphDirty_ = true;
    selectionRectsDirty_ = true;
    paintParagraphCache_.reset();
    selectionRectsCache_.reset();
}

void InputBox::markSelectionStateDirty() {
    if (!compositionText_.empty()) {
        markParagraphDirty();
        return;
    }

    markSelectionRectsDirty();
}

void InputBox::markSelectionRectsDirty() {
    selectionRectsDirty_ = true;
    selectionRectsCache_.reset();
}

void InputBox::rebuildParagraphIfNeeded() {
    if (!paragraphDirty_ && paragraph_ != nullptr) {
        return;
    }

    paragraph_ = buildParagraph();
    paragraphDirty_ = false;
    const auto maxScrollX = softWrap_ ? 0.0f : std::max<SkScalar>(0.0f, contentWidth() - viewportWidth_);
    scrollX_ = ClampScalar(scrollX_, 0.0f, maxScrollX);
    scrollY_ = ClampScalar(scrollY_, 0.0f, maxScrollY());
}

std::unique_ptr<::skia::textlayout::Paragraph> InputBox::buildParagraph() const {
    return buildParagraphForText(displayText(), false);
}

std::unique_ptr<::skia::textlayout::Paragraph> InputBox::buildParagraphForText(const std::string& text,
                                                                               bool ellipsize) const {
    auto fontCollection = GetFontCollection();
    auto unicodeProvider = GetUnicodeProvider();
    auto paragraphStyle = paragraphStyle_;
    if (ellipsize) {
        paragraphStyle.setMaxLines(1);
        paragraphStyle.setEllipsis(SkString(ellipsisText_.c_str()));
    }

    auto builder = fontCollection->MakeInputParagraphBuilder(
            paragraphStyle,
            textStyleDeclaration_,
            unicodeProvider->unwrap());
    builder->addText(text.c_str(), text.size());
    auto paragraph = builder->Build();
    if (softWrap_ || ellipsize) {
        paragraph->layout(viewportWidth_);
        return paragraph;
    }

    paragraph->layout(ResolveNoWrapProbeLayoutWidth(text, textStyle_, viewportWidth_));
    const auto measuredWidth = ResolveNoWrapContentWidth(paragraph.get(), text);
    paragraph->layout(ResolveNoWrapLayoutWidth(viewportWidth_, measuredWidth));
    return paragraph;
}

std::unique_ptr<::skia::textlayout::Paragraph> InputBox::buildPaintParagraph() const {
    return buildParagraphForText(displayText(), shouldUseEllipsisDisplay());
}

std::shared_ptr<::skia::textlayout::Paragraph> InputBox::getPaintParagraphSnapshot() {
    rebuildParagraphIfNeeded();
    if (paintParagraphDirty_ || paintParagraphCache_ == nullptr) {
        auto paragraph = buildPaintParagraph();
        paintParagraphCache_ = std::shared_ptr<::skia::textlayout::Paragraph>(std::move(paragraph));
        paintParagraphDirty_ = false;
    }

    return paintParagraphCache_;
}

std::shared_ptr<const std::vector<InputBoxCaretRect>> InputBox::getSelectionRectsSnapshot() {
    rebuildParagraphIfNeeded();
    if (selectionRectsDirty_ || selectionRectsCache_ == nullptr) {
        selectionRectsCache_ = std::make_shared<std::vector<InputBoxCaretRect>>(buildSelectionRects());
        selectionRectsDirty_ = false;
    }

    return selectionRectsCache_;
}

bool InputBox::shouldUseEllipsisDisplay() const {
    return !focused_ && singleLineInput_ && textOverflow_ == TextOverflow::Ellipsis && compositionText_.empty();
}

bool InputBox::shouldClipDisplayToViewport() const {
    return focused_ || !singleLineInput_ || textOverflow_ != TextOverflow::Overflow;
}

InputBoxMetrics InputBox::metricsForParagraph(::skia::textlayout::Paragraph* paragraph,
                                              SkScalar scrollX,
                                              SkScalar scrollY,
                                              SkScalar contentWidthValue) const {
    InputBoxMetrics metrics;
    metrics.viewportWidth = viewportWidth_;
    metrics.viewportHeight = viewportHeight_;
    metrics.scrollX = scrollX;
    metrics.scrollY = scrollY;
    if (paragraph == nullptr) {
        return metrics;
    }

    metrics.height = paragraph->getHeight();
    metrics.longestLine = paragraph->getLongestLine();
    metrics.minIntrinsicWidth = paragraph->getMinIntrinsicWidth();
    metrics.maxIntrinsicWidth = paragraph->getMaxIntrinsicWidth();
    metrics.contentWidth = contentWidthValue;
    return metrics;
}

std::string InputBox::displayText() const {
    return displayTextState().text;
}

InputBox::DisplayTextState InputBox::displayTextState() const {
    DisplayTextBuilder builder;
    const auto& committedText = boundaryMap_.text();
    const auto compositionStart = compositionStartUtf8();
    const auto replacedEnd = compositionReplacedEndUtf8();

    auto appendCommittedRange = [&](size_t startUtf8, size_t endUtf8) {
        if (!maskInput_) {
            AppendRawDisplayRange(builder, committedText, startUtf8, endUtf8, startUtf8);
            return;
        }

        TextBoundaryMap committedMap(committedText);
        for (size_t boundaryIndex = 0; boundaryIndex + 1 < committedMap.boundaryCount(); ++boundaryIndex) {
            const auto clusterStart = committedMap.boundaryAt(boundaryIndex);
            const auto clusterEnd = committedMap.boundaryAt(boundaryIndex + 1);
            if (clusterEnd <= startUtf8) {
                continue;
            }
            if (clusterStart >= endUtf8) {
                break;
            }
            if (clusterStart < startUtf8 || clusterEnd > endUtf8) {
                continue;
            }

            if (IsSingleNewlineCluster(committedText, clusterStart, clusterEnd)) {
                AppendRawDisplayRange(builder, committedText, clusterStart, clusterEnd, clusterStart);
            } else {
                AppendMaskedCluster(builder, maskText_, clusterStart, clusterEnd);
            }
        }
        builder.committedUtf8ForDisplayUtf8.back() = endUtf8;
    };

    size_t compositionDisplayStart = 0;
    size_t compositionDisplayEnd = 0;
    auto appendComposition = [&]() {
        if (compositionText_.empty()) {
            return;
        }

        const auto displayStart = builder.text.size();
        if (!maskInput_) {
            AppendCompositionDisplayRange(builder,
                                          compositionText_,
                                          0,
                                          compositionText_.size(),
                                          compositionStart,
                                          replacedEnd);
        } else {
            TextBoundaryMap compositionMap(compositionText_);
            builder.committedUtf8ForDisplayUtf8.back() = compositionStart;
            for (size_t boundaryIndex = 0; boundaryIndex + 1 < compositionMap.boundaryCount(); ++boundaryIndex) {
                const auto clusterStart = compositionMap.boundaryAt(boundaryIndex);
                const auto clusterEnd = compositionMap.boundaryAt(boundaryIndex + 1);
                const auto& text = compositionMap.text();
                if (IsSingleNewlineCluster(text, clusterStart, clusterEnd)) {
                    AppendCompositionDisplayRange(builder,
                                                  text,
                                                  clusterStart,
                                                  clusterEnd,
                                                  compositionStart,
                                                  compositionStart);
                } else {
                    AppendMaskedCluster(builder, maskText_, compositionStart, compositionStart);
                }
            }
            builder.committedUtf8ForDisplayUtf8.back() = replacedEnd;
        }
        const auto displayEnd = builder.text.size();
        compositionDisplayStart = displayStart;
        compositionDisplayEnd = displayEnd;
    };

    if (compositionText_.empty()) {
        appendCommittedRange(0, committedText.size());
    } else {
        appendCommittedRange(0, compositionStart);
        appendComposition();
        appendCommittedRange(replacedEnd, committedText.size());
    }

    DisplayTextState state;
    state.text = std::move(builder.text);
    state.committedUtf8ForDisplayUtf8 = std::move(builder.committedUtf8ForDisplayUtf8);
    state.displayUtf8ForCommittedUtf8 =
            BuildDisplayUtf8ForCommittedUtf8(state.committedUtf8ForDisplayUtf8, boundaryMap_.utf8Length());
    if (!compositionText_.empty()) {
        state.compositionStartUtf8 = compositionDisplayStart;
        state.compositionEndUtf8 = std::max(compositionDisplayStart, compositionDisplayEnd);
    } else {
        state.compositionStartUtf8 =
                state.displayUtf8FromCommitted(boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest));
        state.compositionEndUtf8 = state.compositionStartUtf8;
    }
    return state;
}

TextBoundaryMap InputBox::displayBoundaryMap() const {
    return TextBoundaryMap(displayTextState().text);
}

size_t InputBox::displayCaretUtf8() const {
    if (compositionText_.empty()) {
        return displayTextState().displayUtf8FromCommitted(
                boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest));
    }

    return displayTextState().compositionEndUtf8;
}

size_t InputBox::displayCompositionStartUtf8() const {
    return displayTextState().compositionStartUtf8;
}

size_t InputBox::displayCompositionEndUtf8() const {
    return displayTextState().compositionEndUtf8;
}

size_t InputBox::compositionStartUtf8() const {
    if (hasSelection()) {
        return selectionStartUtf8();
    }

    return boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest);
}

size_t InputBox::compositionReplacedEndUtf8() const {
    if (hasSelection()) {
        return selectionEndUtf8();
    }

    return compositionStartUtf8();
}

size_t InputBox::committedUtf8FromDisplay(size_t displayUtf8) const {
    return displayTextState().committedUtf8FromDisplay(displayUtf8);
}

std::vector<InputBoxCaretRect> InputBox::buildSelectionRects() {
    if (!hasSelection() || !compositionText_.empty()) {
        return {};
    }

    return getRectsForCommittedRange(selectionStartUtf8(), selectionEndUtf8());
}

std::vector<InputBoxCaretRect> InputBox::getRectsForCommittedRange(size_t startUtf8, size_t endUtf8) {
    rebuildParagraphIfNeeded();
    if (paragraph_ == nullptr || endUtf8 <= startUtf8) {
        return {};
    }

    const auto displayState = displayTextState();
    const auto displayMap = TextBoundaryMap(displayState.text);
    startUtf8 = displayState.displayUtf8FromCommitted(startUtf8);
    endUtf8 = displayState.displayUtf8FromCommitted(endUtf8);
    if (endUtf8 <= startUtf8) {
        return {};
    }

    const auto startUtf16 = displayMap.utf8ToUtf16(startUtf8);
    const auto endUtf16 = displayMap.utf8ToUtf16(endUtf8);
    if (endUtf16 <= startUtf16) {
        return {};
    }

    auto boxes = paragraph_->getRectsForRange(static_cast<unsigned>(startUtf16),
                                              static_cast<unsigned>(endUtf16),
                                              ::skia::textlayout::RectHeightStyle::kTight,
                                              ::skia::textlayout::RectWidthStyle::kTight);

    // Keep SkParagraph's visual x slices, but normalize highlight height per line across font fallback runs.
    const auto offsetX = visualOffsetX();
    const auto offsetY = visualOffsetY();
    const auto fallbackMetrics = ResolveStyleVerticalMetrics(textStyle_);
    std::vector<InputBoxCaretRect> rects;
    rects.reserve(boxes.size());
    for (const auto& box: boxes) {
        if (!(box.rect.right() > box.rect.left()) || !(box.rect.bottom() > box.rect.top())) {
            continue;
        }

        auto selectionTop = ToFloat(box.rect.top() + offsetY);
        auto selectionBottom = ToFloat(box.rect.bottom() + offsetY);
        const auto lineProbeY = (box.rect.top() + box.rect.bottom()) * 0.5;
        for (size_t line = 0; line < getLineCount(); ++line) {
            ::skia::textlayout::LineMetrics lineMetrics;
            if (!getLineMetricsAt(static_cast<int>(line), lineMetrics)) {
                continue;
            }

            const auto lineTop = lineMetrics.fBaseline - lineMetrics.fAscent;
            const auto lineBottom = lineMetrics.fBaseline + lineMetrics.fDescent;
            if (lineProbeY + 0.001 < lineTop || lineProbeY - 0.001 > lineBottom) {
                continue;
            }

            selectionTop = ToFloat(lineTop + offsetY);
            selectionBottom = ToFloat(lineBottom + offsetY);
            break;
        }

        if (!(selectionBottom > selectionTop)) {
            selectionTop = ToFloat(offsetY);
            selectionBottom = ToFloat(offsetY + fallbackMetrics.height);
        }

        rects.push_back(InputBoxCaretRect{
                ToFloat(box.rect.left() + offsetX),
                selectionTop,
                ToFloat(box.rect.right() + offsetX),
                selectionBottom,
        });
    }
    return RemoveSelectionRectOverlaps(rects);
}

bool InputBox::getLineMetricsAt(int lineNumber, ::skia::textlayout::LineMetrics& lineMetrics) const {
    return paragraph_ != nullptr && paragraph_->getLineMetricsAt(lineNumber, &lineMetrics);
}

bool InputBox::resolveLineMetricsForDisplayUtf8(size_t displayUtf8,
                                                ::skia::textlayout::Affinity affinity,
                                                ::skia::textlayout::LineMetrics& lineMetrics,
                                                int& lineNumber) {
    rebuildParagraphIfNeeded();
    if (paragraph_ == nullptr || paragraph_->lineNumber() == 0) {
        return false;
    }

    const auto lineCount = static_cast<int>(paragraph_->lineNumber());
    const auto displayMap = displayBoundaryMap();
    const auto displayLength = displayMap.utf8Length();
    const auto clamped = std::min(displayUtf8, displayLength);
    ::skia::textlayout::LineMetrics lastMetrics;
    int lastLine = 0;

    for (int line = 0; line < lineCount; ++line) {
        ::skia::textlayout::LineMetrics current;
        if (!getLineMetricsAt(line, current)) {
            continue;
        }

        lastMetrics = current;
        lastLine = line;
        const auto start = LineMetricStartUtf8(current, displayMap);
        const auto end = LineMetricEndUtf8(current, displayMap);
        const auto endIncludingNewline = std::max(LineMetricEndIncludingNewlineUtf8(current, displayMap), end);
        if (clamped < start) {
            continue;
        }
        if (clamped == start) {
            lineMetrics = current;
            lineNumber = line;
            return true;
        }
        if (clamped < end) {
            lineMetrics = current;
            lineNumber = line;
            return true;
        }
        if (clamped == end) {
            if (!current.fHardBreak && affinity == ::skia::textlayout::Affinity::kDownstream && line + 1 < lineCount) {
                ::skia::textlayout::LineMetrics next;
                if (getLineMetricsAt(line + 1, next) && LineMetricStartUtf8(next, displayMap) == clamped) {
                    lineMetrics = next;
                    lineNumber = line + 1;
                    return true;
                }
            }

            lineMetrics = current;
            lineNumber = line;
            return true;
        }
        if (clamped < endIncludingNewline) {
            lineMetrics = current;
            lineNumber = line;
            return true;
        }
        if (clamped == endIncludingNewline) {
            if (line + 1 < lineCount) {
                ::skia::textlayout::LineMetrics next;
                if (getLineMetricsAt(line + 1, next) && LineMetricStartUtf8(next, displayMap) == clamped) {
                    lineMetrics = next;
                    lineNumber = line + 1;
                    return true;
                }
            }

            const auto& displayTextValue = displayMap.text();
            if (clamped == displayLength && current.fHardBreak && !displayTextValue.empty() &&
                displayTextValue.back() == '\n') {
                const auto utf16 = displayMap.utf8ToUtf16(clamped);
                lineMetrics = current;
                lineMetrics.fStartIndex = utf16;
                lineMetrics.fEndIndex = utf16;
                lineMetrics.fEndExcludingWhitespaces = utf16;
                lineMetrics.fEndIncludingNewline = utf16;
                lineMetrics.fHardBreak = false;
                lineMetrics.fWidth = 0.0;
                lineMetrics.fLeft = EmptyCaretX(paragraphStyle_.effective_align(), viewportWidth_, caretWidth_);
                const auto lineAdvance = std::isfinite(current.fHeight) && current.fHeight > 0.0
                                                 ? current.fHeight
                                                 : current.fAscent + current.fDescent;
                if (std::isfinite(lineAdvance) && lineAdvance > 0.0) {
                    lineMetrics.fBaseline = current.fBaseline + lineAdvance;
                }
                lineMetrics.fLineNumber = current.fLineNumber + 1;
                lineMetrics.fLineMetrics.clear();
                lineNumber = line + 1;
                return true;
            }

            lineMetrics = current;
            lineNumber = line;
            return true;
        }
    }

    lineMetrics = lastMetrics;
    lineNumber = lastLine;
    return true;
}

SkScalar InputBox::caretXForDisplayOffset(size_t displayUtf8,
                                          const ::skia::textlayout::LineMetrics& lineMetrics,
                                          const TextBoundaryMap& displayMap) {
    if (displayMap.utf8Length() == 0) {
        return EmptyCaretX(paragraphStyle_.effective_align(), viewportWidth_, caretWidth_);
    }

    const auto lineStartUtf8 = LineMetricStartUtf8(lineMetrics, displayMap);
    const auto lineEndUtf8 = LineMetricEndUtf8(lineMetrics, displayMap);
    if (displayUtf8 <= lineStartUtf8) {
        return ToFloat(lineMetrics.fLeft);
    }

    const auto caretUtf16 = displayMap.utf8ToUtf16(displayUtf8);
    const auto utf16Length = displayMap.utf16Length();
    if (utf16Length == 0) {
        return ToFloat(lineMetrics.fLeft);
    }

    ::skia::textlayout::Paragraph::GlyphInfo glyphInfo;
    auto glyphProbeUtf16 = caretUtf16;
    if (displayUtf8 >= lineEndUtf8 && lineEndUtf8 > lineStartUtf8) {
        glyphProbeUtf16 = displayMap.utf8ToUtf16(displayMap.previousBoundary(lineEndUtf8));
    } else if (glyphProbeUtf16 >= utf16Length) {
        glyphProbeUtf16 = utf16Length - 1;
    }

    if (!paragraph_->getGlyphInfoAtUTF16Offset(glyphProbeUtf16, &glyphInfo)) {
        return ToFloat(lineMetrics.fLeft + lineMetrics.fWidth);
    }

    const bool atClusterEnd = caretUtf16 >= glyphInfo.fGraphemeClusterTextRange.end;
    const bool rtl = glyphInfo.fDirection == ::skia::textlayout::TextDirection::kRtl;
    if (atClusterEnd) {
        return rtl ? glyphInfo.fGraphemeLayoutBounds.left() : glyphInfo.fGraphemeLayoutBounds.right();
    }
    return rtl ? glyphInfo.fGraphemeLayoutBounds.right() : glyphInfo.fGraphemeLayoutBounds.left();
}

size_t InputBox::displayUtf8ForLineX(int lineNumber, SkScalar x, ::skia::textlayout::Affinity& affinity) {
    x -= visualOffsetX();
    ::skia::textlayout::LineMetrics lineMetrics;
    if (!getLineMetricsAt(lineNumber, lineMetrics)) {
        affinity = ::skia::textlayout::Affinity::kDownstream;
        return displayTextState().displayUtf8FromCommitted(
                boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest));
    }

    const auto lineLeft = ToFloat(lineMetrics.fLeft);
    const auto lineRight = ToFloat(lineMetrics.fLeft + lineMetrics.fWidth);
    const auto displayMap = displayBoundaryMap();
    const auto lineStartUtf8 = LineMetricStartUtf8(lineMetrics, displayMap);
    const auto lineEndUtf8 = LineMetricEndUtf8(lineMetrics, displayMap);
    if (x <= lineLeft || lineEndUtf8 <= lineStartUtf8) {
        affinity = ::skia::textlayout::Affinity::kDownstream;
        return lineStartUtf8;
    }
    if (x >= lineRight) {
        affinity = ::skia::textlayout::Affinity::kUpstream;
        return lineEndUtf8;
    }

    const auto lineTop = ToFloat(lineMetrics.fBaseline - lineMetrics.fAscent);
    const auto lineBottom = ToFloat(lineMetrics.fBaseline + lineMetrics.fDescent);
    const auto y = (lineTop + lineBottom) * 0.5f;
    const auto hit = paragraph_->getGlyphPositionAtCoordinate(x, y);
    auto displayUtf8 = displayMap.utf16ToUtf8(hit.position < 0 ? 0 : static_cast<size_t>(hit.position));
    displayUtf8 = std::max<size_t>(lineStartUtf8, std::min<size_t>(displayUtf8, lineEndUtf8));
    affinity = hit.affinity;
    return displayUtf8;
}

bool InputBox::moveToUtf8(size_t targetUtf8,
                          ::skia::textlayout::Affinity targetAffinity,
                          bool extendSelection,
                          bool shouldResetPreferredCaretX) {
    const auto oldCursor = cursorUtf8_;
    const auto oldAffinity = affinity_;
    const auto target =
            boundaryMap_.snapUtf8(std::min(targetUtf8, boundaryMap_.utf8Length()), TextBoundarySnapMode::Nearest);

    bool changed = false;
    if (extendSelection) {
        const auto anchor = hasSelection() ? selectionAnchorUtf8_ : cursorUtf8_;
        const auto anchorAffinity = hasSelection() ? selectionAnchorAffinity_ : affinity_;
        changed = setSelectionUtf8(anchor, target, anchorAffinity, targetAffinity);
    } else {
        cursorUtf8_ = target;
        affinity_ = targetAffinity;
        const bool hadSelection = hasSelection();
        resetSelectionToCursor();
        ensureCaretVisible();
        changed = hadSelection || cursorUtf8_ != oldCursor || affinity_ != oldAffinity;
        if (changed) {
            breakUndoGroup();
        }
    }

    if (shouldResetPreferredCaretX) {
        resetPreferredCaretX();
    }
    return changed;
}

bool InputBox::moveVertical(int lineDelta, bool extendSelection) {
    const bool clearedComposition = clearComposition();
    rebuildParagraphIfNeeded();
    if (paragraph_ == nullptr || paragraph_->lineNumber() == 0 || lineDelta == 0) {
        if (clearedComposition) {
            breakUndoGroup();
        }
        return clearedComposition;
    }

    ::skia::textlayout::LineMetrics currentLineMetrics;
    int currentLine = 0;
    const auto displayState = displayTextState();
    const auto displayCursor =
            displayState.displayUtf8FromCommitted(boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest));
    if (!resolveLineMetricsForDisplayUtf8(displayCursor, affinity_, currentLineMetrics, currentLine)) {
        if (clearedComposition) {
            breakUndoGroup();
        }
        return clearedComposition;
    }

    const auto lineCount = static_cast<int>(paragraph_->lineNumber());
    const auto targetLine = currentLine + lineDelta;
    if (targetLine < 0 || targetLine >= lineCount) {
        if (clearedComposition) {
            breakUndoGroup();
        }
        return clearedComposition;
    }

    if (!std::isfinite(preferredCaretX_)) {
        preferredCaretX_ = getCaretRect().left;
    }
    const auto targetX = preferredCaretX_;
    ::skia::textlayout::Affinity targetAffinity = ::skia::textlayout::Affinity::kDownstream;
    const auto displayTarget = displayUtf8ForLineX(targetLine, targetX, targetAffinity);
    const auto target = committedUtf8FromDisplay(displayTarget);
    const bool moved = moveToUtf8(target, targetAffinity, extendSelection, false);
    preferredCaretX_ = targetX;
    if (clearedComposition && !moved) {
        breakUndoGroup();
    }
    return clearedComposition || moved;
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

SkScalar InputBox::visualOffsetX() {
    rebuildParagraphIfNeeded();
    return visualOffsetXForParagraph(paragraph_.get());
}

SkScalar InputBox::visualOffsetXForParagraph(::skia::textlayout::Paragraph* paragraph) const {
    if ((!autoMarginLeft_ && !autoMarginRight_) || paragraph == nullptr) {
        return 0.0f;
    }

    const auto contentWidthValue = visualContentWidthForParagraph(paragraph);
    if (!IsFinitePositive(viewportWidth_) || !IsFinitePositive(contentWidthValue) ||
        contentWidthValue >= viewportWidth_) {
        return 0.0f;
    }

    const auto spare = viewportWidth_ - contentWidthValue;
    if (autoMarginLeft_ && autoMarginRight_) {
        return spare * 0.5f;
    }
    return autoMarginLeft_ ? spare : 0.0f;
}

SkScalar InputBox::visualContentWidthForParagraph(::skia::textlayout::Paragraph* paragraph) const {
    if (paragraph == nullptr) {
        return caretWidth_;
    }

    auto width = std::max<SkScalar>(0.0f, paragraph->getLongestLine());
    width = std::max(width, paragraph->getMaxIntrinsicWidth());
    if (!softWrap_) {
        width = std::max(width, ResolveNoWrapContentWidth(paragraph, displayText()));
    }

    return std::max<SkScalar>(caretWidth_, std::ceil(width + caretWidth_));
}

SkScalar InputBox::visualOffsetY() {
    rebuildParagraphIfNeeded();
    return visualOffsetYForParagraph(paragraph_.get());
}

SkScalar InputBox::visualOffsetYForParagraph(::skia::textlayout::Paragraph* paragraph) const {
    const auto verticalMetrics = ResolveStyleVerticalMetrics(textStyle_);
    if (paragraph == nullptr) {
        if (!autoMarginTop_ && !autoMarginBottom_) {
            return 0.0f;
        }
        const auto spare = viewportHeight_ - verticalMetrics.height;
        if (!(spare > 0.0f)) {
            return 0.0f;
        }
        if (autoMarginTop_ && autoMarginBottom_) {
            return spare * 0.5f;
        }
        return autoMarginTop_ ? spare : 0.0f;
    }

    if (!autoMarginTop_ && !autoMarginBottom_) {
        return 0.0f;
    }

    ::skia::textlayout::LineMetrics lineMetrics;
    const bool hasLineMetrics = paragraph->getLineMetricsAt(0, &lineMetrics);
    if (!hasLineMetrics) {
        return BaselineOffsetY(viewportHeight_, verticalMetrics.ascent, verticalMetrics);
    }

    if (paragraph->lineNumber() <= 1) {
        if (autoMarginTop_ && autoMarginBottom_) {
            return BaselineOffsetY(viewportHeight_, lineMetrics.fBaseline, verticalMetrics);
        }

        if (autoMarginTop_) {
            const auto lineBottom = lineMetrics.fBaseline + lineMetrics.fDescent;
            if (!std::isfinite(lineBottom) || lineBottom >= viewportHeight_) {
                return 0.0f;
            }
            return viewportHeight_ - lineBottom;
        }

        return 0.0f;
    }

    const auto height = paragraph->getHeight();
    if (!IsFinitePositive(viewportHeight_) || !IsFinitePositive(height) || height >= viewportHeight_) {
        return 0.0f;
    }

    const auto spare = viewportHeight_ - height;
    if (autoMarginTop_ && autoMarginBottom_) {
        return spare * 0.5f;
    }
    return autoMarginTop_ ? spare : 0.0f;
}

SkScalar InputBox::contentHeight() {
    rebuildParagraphIfNeeded();
    if (paragraph_ == nullptr) {
        return viewportHeight_;
    }

    return std::max(viewportHeight_, std::ceil(paragraph_->getHeight() + visualOffsetY()));
}

SkScalar InputBox::maxScrollY() {
    if (paragraph_ == nullptr) {
        return 0.0f;
    }

    return std::max<SkScalar>(0.0f, std::ceil(paragraph_->getHeight() + visualOffsetY() - viewportHeight_));
}

InputBoxCaretRect InputBox::activeEnsureVisibleRect() {
    if (!compositionText_.empty()) {
        return getCompositionRect();
    }

    return getCaretRect();
}

void InputBox::ensureRectVisible(const InputBoxCaretRect& rect) {
    const auto maxScrollX = std::max<SkScalar>(0.0f, contentWidth() - viewportWidth_);
    const auto maxScrollYValue = maxScrollY();
    const auto left = static_cast<SkScalar>(std::min(rect.left, rect.right));
    const auto right = static_cast<SkScalar>(std::max(rect.left, rect.right));
    const auto top = static_cast<SkScalar>(std::min(rect.top, rect.bottom));
    const auto bottom = static_cast<SkScalar>(std::max(rect.top, rect.bottom));
    if (!std::isfinite(left) || !std::isfinite(right) || !std::isfinite(top) || !std::isfinite(bottom)) {
        scrollX_ = ClampScalar(scrollX_, 0.0f, maxScrollX);
        scrollY_ = ClampScalar(scrollY_, 0.0f, maxScrollYValue);
        return;
    }

    const auto visibleLeft = kCaretScrollPadding;
    const auto visibleRight = std::max<SkScalar>(visibleLeft, viewportWidth_ - kCaretScrollPadding);
    const auto visibleWidth = std::max<SkScalar>(0.0f, visibleRight - visibleLeft);
    const auto rectWidth = std::max<SkScalar>(0.0f, right - left);

    if (rectWidth <= visibleWidth) {
        if (left - scrollX_ < visibleLeft) {
            scrollX_ = left - visibleLeft;
        } else if (right - scrollX_ > visibleRight) {
            scrollX_ = right - visibleRight;
        }
    } else if (right - scrollX_ > visibleRight) {
        scrollX_ = right - visibleRight;
    } else if (left - scrollX_ < visibleLeft) {
        scrollX_ = left - visibleLeft;
    }

    scrollX_ = ClampScalar(scrollX_, 0.0f, maxScrollX);

    const auto visibleTop = kCaretScrollPadding;
    const auto visibleBottom = std::max<SkScalar>(visibleTop, viewportHeight_ - kCaretScrollPadding);
    const auto visibleHeight = std::max<SkScalar>(0.0f, visibleBottom - visibleTop);
    const auto rectHeight = std::max<SkScalar>(0.0f, bottom - top);

    if (rectHeight <= visibleHeight) {
        if (top - scrollY_ < visibleTop) {
            scrollY_ = top - visibleTop;
        } else if (bottom - scrollY_ > visibleBottom) {
            scrollY_ = bottom - visibleBottom;
        }
    } else if (bottom - scrollY_ > visibleBottom) {
        scrollY_ = bottom - visibleBottom;
    } else if (top - scrollY_ < visibleTop) {
        scrollY_ = top - visibleTop;
    }

    scrollY_ = ClampScalar(scrollY_, 0.0f, maxScrollYValue);
}

bool InputBox::resetSelectionToCursor() {
    const auto oldAnchor = selectionAnchorUtf8_;
    const auto oldFocus = selectionFocusUtf8_;
    const auto oldAnchorAffinity = selectionAnchorAffinity_;
    const auto oldFocusAffinity = selectionFocusAffinity_;

    selectionAnchorUtf8_ = boundaryMap_.snapUtf8(cursorUtf8_, TextBoundarySnapMode::Nearest);
    selectionFocusUtf8_ = selectionAnchorUtf8_;
    selectionAnchorAffinity_ = affinity_;
    selectionFocusAffinity_ = affinity_;
    const bool changed = selectionAnchorUtf8_ != oldAnchor || selectionFocusUtf8_ != oldFocus ||
                         selectionAnchorAffinity_ != oldAnchorAffinity || selectionFocusAffinity_ != oldFocusAffinity;
    if (changed) {
        markSelectionStateDirty();
    }
    return changed;
}

void InputBox::resetPreferredCaretX() {
    preferredCaretX_ = std::numeric_limits<SkScalar>::quiet_NaN();
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
    if (softWrap_) {
        return viewportWidth_;
    }
    if (paragraph_ == nullptr) {
        return viewportWidth_;
    }

    const auto measuredWidth = ResolveNoWrapContentWidth(paragraph_.get(), displayText());
    if (!(measuredWidth > 0.0f)) {
        return viewportWidth_;
    }

    return std::max(viewportWidth_, std::ceil(measuredWidth + caretWidth_));
}

void InputBox::replaceText(std::string text, size_t requestedCursor) {
    boundaryMap_.rebuild(std::move(text));
    compositionText_.clear();
    cursorUtf8_ =
            boundaryMap_.snapUtf8(std::min(requestedCursor, boundaryMap_.utf8Length()), TextBoundarySnapMode::Nearest);
    affinity_ = ::skia::textlayout::Affinity::kDownstream;
    resetSelectionToCursor();
    resetPreferredCaretX();
    markParagraphDirty();
    ensureCaretVisible();
}

InputBox::EditState InputBox::captureEditState() const {
    return EditState{
            boundaryMap_.text(),
            cursorUtf8_,
            affinity_,
            selectionAnchorUtf8_,
            selectionFocusUtf8_,
            selectionAnchorAffinity_,
            selectionFocusAffinity_,
    };
}

void InputBox::restoreEditState(const EditState& state) {
    boundaryMap_.rebuild(state.text);
    compositionText_.clear();
    cursorUtf8_ =
            boundaryMap_.snapUtf8(std::min(state.cursorUtf8, boundaryMap_.utf8Length()), TextBoundarySnapMode::Nearest);
    affinity_ = state.affinity;
    selectionAnchorUtf8_ = boundaryMap_.snapUtf8(std::min(state.selectionAnchorUtf8, boundaryMap_.utf8Length()),
                                                 TextBoundarySnapMode::Nearest);
    selectionFocusUtf8_ = boundaryMap_.snapUtf8(std::min(state.selectionFocusUtf8, boundaryMap_.utf8Length()),
                                                TextBoundarySnapMode::Nearest);
    selectionAnchorAffinity_ = state.selectionAnchorAffinity;
    selectionFocusAffinity_ = state.selectionFocusAffinity;
    resetPreferredCaretX();
    markParagraphDirty();
    ensureCaretVisible();
}

void InputBox::recordEdit(const EditState& before, EditKind kind) {
    const auto after = captureEditState();
    if (editStateEquals(before, after)) {
        return;
    }

    redoStack_.clear();
    const auto now = std::chrono::steady_clock::now();
    if (!undoStack_.empty() && canMergeEdit(undoStack_.back(), kind, before, now)) {
        undoStack_.back().after = after;
        undoStack_.back().updatedAt = now;
    } else {
        undoStack_.push_back(EditGroup{before, after, kind, now});
    }
    pruneEditHistory(undoStack_);
    undoMergeBarrier_ = false;
}

void InputBox::clearEditHistory() {
    undoStack_.clear();
    redoStack_.clear();
    breakUndoGroup();
}

bool InputBox::canMergeEdit(const EditGroup& group,
                            EditKind kind,
                            const EditState& before,
                            std::chrono::steady_clock::time_point now) const {
    if (undoMergeBarrier_ || group.kind != kind || !isMergeableEditKind(kind)) {
        return false;
    }
    if (!editStateEquals(group.after, before)) {
        return false;
    }
    return now - group.updatedAt <= kEditMergeTimeout;
}

void InputBox::pruneEditHistory(std::vector<EditGroup>& stack) {
    while (stack.size() > kMaxEditHistoryGroups) {
        stack.erase(stack.begin());
    }
    while (stack.size() > 1 && editHistoryByteCost(stack) > kMaxEditHistoryBytes) {
        stack.erase(stack.begin());
    }
}

bool InputBox::isMergeableEditKind(EditKind kind) {
    return kind == EditKind::Typing || kind == EditKind::DeleteBackward || kind == EditKind::DeleteForward;
}

bool InputBox::editStateEquals(const EditState& left, const EditState& right) {
    return left.text == right.text && left.cursorUtf8 == right.cursorUtf8 && left.affinity == right.affinity &&
           left.selectionAnchorUtf8 == right.selectionAnchorUtf8 &&
           left.selectionFocusUtf8 == right.selectionFocusUtf8 &&
           left.selectionAnchorAffinity == right.selectionAnchorAffinity &&
           left.selectionFocusAffinity == right.selectionFocusAffinity;
}

size_t InputBox::editStateByteCost(const EditState& state) {
    return state.text.size() + 6 * sizeof(size_t) + 3 * sizeof(int32_t);
}

size_t InputBox::editGroupByteCost(const EditGroup& group) {
    return editStateByteCost(group.before) + editStateByteCost(group.after);
}

size_t InputBox::editHistoryByteCost(const std::vector<EditGroup>& stack) {
    size_t total = 0;
    for (const auto& group: stack) {
        total += editGroupByteCost(group);
    }
    return total;
}

void InputBox::markCompositionDirty() {
    markParagraphDirty();
}

InputBoxDrawSnapshot::InputBoxDrawSnapshot(std::shared_ptr<::skia::textlayout::Paragraph> paragraph,
                                           InputBoxCaretRect caretRect,
                                           InputBoxMetrics metrics,
                                           InputBoxCaretRect compositionRect,
                                           std::shared_ptr<const std::vector<InputBoxCaretRect>> selectionRects,
                                           SkScalar caretWidth,
                                           SkScalar visualOffsetX,
                                           SkScalar visualOffsetY,
                                           SkColor caretColor,
                                           SkColor selectionColor,
                                           bool caretVisible,
                                           bool compositionVisible,
                                           bool clipToViewport)
    : paragraph_(std::move(paragraph)), caretRect_(caretRect), metrics_(metrics), compositionRect_(compositionRect),
      selectionRects_(std::move(selectionRects)), caretWidth_(caretWidth), visualOffsetX_(visualOffsetX),
      visualOffsetY_(visualOffsetY), caretColor_(caretColor), selectionColor_(selectionColor),
      caretVisible_(caretVisible), compositionVisible_(compositionVisible), clipToViewport_(clipToViewport) {
}

void InputBoxDrawSnapshot::paint(SkCanvas* canvas, SkScalar x, SkScalar y, SkScalar width, SkScalar height) const {
    if (canvas == nullptr) {
        return;
    }

    canvas->save();
    if (clipToViewport_) {
        const auto clipWidth = width > 0.0f ? width : metrics_.viewportWidth;
        const auto clipHeight = height > 0.0f ? height : metrics_.viewportHeight;
        canvas->clipRect(SkRect::MakeXYWH(x, y, clipWidth, clipHeight));
    }

    if (selectionRects_ != nullptr && !selectionRects_->empty()) {
        SkPaint paint;
        paint.setColor(selectionColor_);
        paint.setStyle(SkPaint::kFill_Style);
        for (const auto& rect: *selectionRects_) {
            canvas->drawRect(SkRect::MakeLTRB(x + rect.left - metrics_.scrollX,
                                              y + rect.top - metrics_.scrollY,
                                              x + rect.right - metrics_.scrollX,
                                              y + rect.bottom - metrics_.scrollY),
                             paint);
        }
    }

    if (paragraph_ != nullptr) {
        paragraph_->paint(canvas, x + visualOffsetX_ - metrics_.scrollX, y + visualOffsetY_ - metrics_.scrollY);
    }

    if (compositionVisible_) {
        SkPaint paint;
        paint.setColor(caretColor_);
        paint.setStyle(SkPaint::kFill_Style);
        const auto underlineTop = std::max(compositionRect_.top, compositionRect_.bottom - kCompositionUnderlineHeight);
        canvas->drawRect(SkRect::MakeLTRB(x + compositionRect_.left - metrics_.scrollX,
                                          y + underlineTop - metrics_.scrollY,
                                          x + compositionRect_.right - metrics_.scrollX,
                                          y + compositionRect_.bottom - metrics_.scrollY),
                         paint);
    }

    if (caretVisible_ && caretWidth_ > 0.0f) {
        SkPaint paint;
        paint.setColor(caretColor_);
        paint.setStyle(SkPaint::kFill_Style);
        const auto caretRight = std::max(caretRect_.right, caretRect_.left + caretWidth_);
        canvas->drawRect(SkRect::MakeLTRB(x + caretRect_.left - metrics_.scrollX,
                                          y + caretRect_.top - metrics_.scrollY,
                                          x + caretRight - metrics_.scrollX,
                                          y + caretRect_.bottom - metrics_.scrollY),
                         paint);
    }

    canvas->restore();
}

} // namespace milestro::skia::textlayout

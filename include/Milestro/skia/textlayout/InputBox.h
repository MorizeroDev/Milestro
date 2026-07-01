#ifndef MILESTRO_SKIA_TEXTLAYOUT_INPUTBOX_H
#define MILESTRO_SKIA_TEXTLAYOUT_INPUTBOX_H

#include "FontCollection.h"
#include "Milestro/common/milestro_export_macros.h"
#include "../unicode/Unicode.h"
#include "ParagraphStyle.h"
#include "TextStyle.h"

#include "include/core/SkColor.h"
#include "include/core/SkRect.h"
#include "modules/skparagraph/include/Paragraph.h"
#include "modules/skunicode/include/SkUnicode.h"

#include <cstddef>
#include <cstdint>
#include <memory>
#include <string>
#include <vector>

namespace milestro::skia::textlayout {

enum class TextBoundarySnapMode : int32_t {
    Previous = 0,
    Next = 1,
    Nearest = 2,
};

struct InputBoxCaretRect {
    float left = 0.0f;
    float top = 0.0f;
    float right = 0.0f;
    float bottom = 0.0f;
};

struct InputBoxMetrics {
    float height = 0.0f;
    float longestLine = 0.0f;
    float minIntrinsicWidth = 0.0f;
    float maxIntrinsicWidth = 0.0f;
    float contentWidth = 0.0f;
    float scrollX = 0.0f;
    float viewportWidth = 0.0f;
    float viewportHeight = 0.0f;
};

struct InputBoxLineMetrics {
    uint64_t startUtf8 = 0;
    uint64_t endUtf8 = 0;
    float ascent = 0.0f;
    float descent = 0.0f;
    float unscaledAscent = 0.0f;
    float height = 0.0f;
    float width = 0.0f;
    float left = 0.0f;
    float baseline = 0.0f;
    uint64_t lineNumber = 0;
};

class InputBoxDrawSnapshot;

class MILESTRO_API TextBoundaryMap {
public:
    TextBoundaryMap();
    explicit TextBoundaryMap(std::string text);

    void rebuild(std::string text);

    const std::string& text() const { return text_; }
    size_t utf8Length() const { return text_.size(); }
    size_t utf16Length() const { return utf8ForUtf16_.empty() ? 0 : utf8ForUtf16_.size() - 1; }

    size_t utf8ToUtf16(size_t utf8Offset) const;
    size_t utf16ToUtf8(size_t utf16Offset) const;

    bool isBoundary(size_t utf8Offset) const;
    size_t previousBoundary(size_t utf8Offset) const;
    size_t nextBoundary(size_t utf8Offset) const;
    size_t nearestBoundary(size_t utf8Offset) const;
    size_t snapUtf8(size_t utf8Offset, TextBoundarySnapMode mode) const;
    size_t boundaryCount() const { return boundariesUtf8_.size(); }
    size_t boundaryAt(size_t index) const;

private:
    std::string text_;
    std::vector<size_t> utf16ForUtf8_;
    std::vector<size_t> utf8ForUtf16_;
    std::vector<size_t> boundariesUtf8_;

    void rebuildUtfMapping();
    void rebuildBoundaries();
};

class MILESTRO_API InputBox {
public:
    InputBox();
    InputBox(ParagraphStyle* paragraphStyle, TextStyle* textStyle);

    void setText(const char* text, size_t length);
    const std::string& getText() const { return boundaryMap_.text(); }

    void setViewport(SkScalar width, SkScalar height);
    void setCaretColor(SkColor color) { caretColor_ = color; }
    void setCaretWidth(SkScalar width);
    void setCaretVisible(bool visible) { caretVisible_ = visible; }

    size_t getCursorUtf8() const { return cursorUtf8_; }
    size_t getCursorUtf16() const { return boundaryMap_.utf8ToUtf16(cursorUtf8_); }
    ::skia::textlayout::Affinity getAffinity() const { return affinity_; }
    void setCursorUtf8(size_t utf8Offset, ::skia::textlayout::Affinity affinity);

    size_t utf8ToUtf16(size_t utf8Offset) const { return boundaryMap_.utf8ToUtf16(utf8Offset); }
    size_t utf16ToUtf8(size_t utf16Offset) const { return boundaryMap_.utf16ToUtf8(utf16Offset); }
    size_t snapUtf8(size_t utf8Offset, TextBoundarySnapMode mode) const {
        return boundaryMap_.snapUtf8(utf8Offset, mode);
    }

    void insertText(const char* text, size_t length);
    bool deleteBackward();
    bool deleteForward();
    bool movePrevious();
    bool moveNext();
    bool hitTest(SkScalar x, SkScalar y);

    void ensureCaretVisible();
    InputBoxCaretRect getCaretRect();
    InputBoxMetrics getMetrics();
    size_t getLineCount();
    bool getLineMetrics(size_t lineNumber, InputBoxLineMetrics& metrics);

    void paint(SkCanvas* canvas, SkScalar x, SkScalar y, SkScalar width, SkScalar height);
    std::unique_ptr<InputBoxDrawSnapshot> createDrawSnapshot();

private:
    ::skia::textlayout::ParagraphStyle paragraphStyle_;
    ::skia::textlayout::TextStyle textStyle_;
    TextBoundaryMap boundaryMap_;
    std::unique_ptr<::skia::textlayout::Paragraph> paragraph_;
    size_t cursorUtf8_ = 0;
    ::skia::textlayout::Affinity affinity_ = ::skia::textlayout::Affinity::kDownstream;
    SkScalar viewportWidth_ = 1.0f;
    SkScalar viewportHeight_ = 1.0f;
    SkScalar scrollX_ = 0.0f;
    SkScalar caretWidth_ = 2.0f;
    SkColor caretColor_ = SK_ColorWHITE;
    bool caretVisible_ = true;
    bool paragraphDirty_ = true;

    static std::string sanitizeSingleLine(const char* text, size_t length);

    void rebuildParagraphIfNeeded();
    std::unique_ptr<::skia::textlayout::Paragraph> buildParagraph() const;
    SkScalar paragraphLayoutWidth() const;
    SkScalar contentWidth();
    void replaceText(std::string text, size_t requestedCursor);
};

class MILESTRO_API InputBoxDrawSnapshot {
public:
    InputBoxDrawSnapshot(std::unique_ptr<::skia::textlayout::Paragraph> paragraph,
                         InputBoxCaretRect caretRect,
                         InputBoxMetrics metrics,
                         SkScalar caretWidth,
                         SkColor caretColor,
                         bool caretVisible);

    void paint(SkCanvas* canvas, SkScalar x, SkScalar y, SkScalar width, SkScalar height) const;

private:
    std::unique_ptr<::skia::textlayout::Paragraph> paragraph_;
    InputBoxCaretRect caretRect_;
    InputBoxMetrics metrics_;
    SkScalar caretWidth_ = 1.0f;
    SkColor caretColor_ = SK_ColorWHITE;
    bool caretVisible_ = false;
};

} // namespace milestro::skia::textlayout

#endif // MILESTRO_SKIA_TEXTLAYOUT_INPUTBOX_H

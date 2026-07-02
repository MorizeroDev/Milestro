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

struct InputBoxSelection {
    uint64_t anchorUtf8 = 0;
    uint64_t focusUtf8 = 0;
    uint64_t startUtf8 = 0;
    uint64_t endUtf8 = 0;
    int32_t anchorAffinity = 0;
    int32_t focusAffinity = 0;
    bool hasSelection = false;
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
    void setSelectionColor(SkColor color) { selectionColor_ = color; }
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
    bool setComposition(const char* text, size_t length);
    bool commitComposition(const char* text, size_t length);
    bool clearComposition();
    bool hasComposition() const { return !compositionText_.empty(); }
    InputBoxSelection getSelection() const;
    bool hasSelection() const;
    bool setSelectionUtf8(size_t anchorUtf8,
                          size_t focusUtf8,
                          ::skia::textlayout::Affinity anchorAffinity,
                          ::skia::textlayout::Affinity focusAffinity);
    bool clearSelection();
    bool selectAll();
    bool deleteBackward();
    bool deleteForward();
    bool movePrevious(bool extendSelection = false);
    bool moveNext(bool extendSelection = false);
    bool hitTest(SkScalar x, SkScalar y, bool extendSelection = false);

    void ensureCaretVisible();
    InputBoxCaretRect getCaretRect();
    InputBoxCaretRect getCompositionRect();
    std::vector<InputBoxCaretRect> getSelectionRects();
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
    SkColor selectionColor_ = SkColorSetARGB(0x66, 0x33, 0x7D, 0xFF);
    bool caretVisible_ = true;
    bool paragraphDirty_ = true;
    std::string compositionText_;
    size_t selectionAnchorUtf8_ = 0;
    size_t selectionFocusUtf8_ = 0;
    ::skia::textlayout::Affinity selectionAnchorAffinity_ = ::skia::textlayout::Affinity::kDownstream;
    ::skia::textlayout::Affinity selectionFocusAffinity_ = ::skia::textlayout::Affinity::kDownstream;

    static std::string sanitizeSingleLine(const char* text, size_t length);

    void configureInputParagraphMetrics();
    void rebuildParagraphIfNeeded();
    std::unique_ptr<::skia::textlayout::Paragraph> buildParagraph() const;
    std::unique_ptr<::skia::textlayout::Paragraph> buildParagraphForText(const std::string& text) const;
    std::string displayText() const;
    TextBoundaryMap displayBoundaryMap() const;
    size_t displayCaretUtf8() const;
    size_t displayCompositionStartUtf8() const;
    size_t displayCompositionEndUtf8() const;
    size_t displayCompositionReplacedEndUtf8() const;
    size_t committedUtf8FromDisplay(size_t displayUtf8) const;
    InputBoxCaretRect getCaretRectForDisplayOffset(size_t displayUtf8);
    std::vector<InputBoxCaretRect> getRectsForDisplayRange(size_t startUtf8, size_t endUtf8);
    size_t selectionStartUtf8() const;
    size_t selectionEndUtf8() const;
    void resetSelectionToCursor();
    bool replaceSelectionWith(std::string replacement);
    bool deleteSelection();
    SkScalar contentWidth();
    void replaceText(std::string text, size_t requestedCursor);
    void markCompositionDirty();
};

class MILESTRO_API InputBoxDrawSnapshot {
public:
    InputBoxDrawSnapshot(std::unique_ptr<::skia::textlayout::Paragraph> paragraph,
                         InputBoxCaretRect caretRect,
                         InputBoxMetrics metrics,
                         InputBoxCaretRect compositionRect,
                         std::vector<InputBoxCaretRect> selectionRects,
                         SkScalar caretWidth,
                         SkColor caretColor,
                         SkColor selectionColor,
                         bool caretVisible,
                         bool compositionVisible);

    void paint(SkCanvas* canvas, SkScalar x, SkScalar y, SkScalar width, SkScalar height) const;

private:
    std::unique_ptr<::skia::textlayout::Paragraph> paragraph_;
    InputBoxCaretRect caretRect_;
    InputBoxMetrics metrics_;
    InputBoxCaretRect compositionRect_;
    std::vector<InputBoxCaretRect> selectionRects_;
    SkScalar caretWidth_ = 1.0f;
    SkColor caretColor_ = SK_ColorWHITE;
    SkColor selectionColor_ = SkColorSetARGB(0x66, 0x33, 0x7D, 0xFF);
    bool caretVisible_ = false;
    bool compositionVisible_ = false;
};

} // namespace milestro::skia::textlayout

#endif // MILESTRO_SKIA_TEXTLAYOUT_INPUTBOX_H

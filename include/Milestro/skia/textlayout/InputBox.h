#ifndef MILESTRO_SKIA_TEXTLAYOUT_INPUTBOX_H
#define MILESTRO_SKIA_TEXTLAYOUT_INPUTBOX_H

#include "../unicode/Unicode.h"
#include "FontCollection.h"
#include "Milestro/common/milestro_export_macros.h"
#include "ParagraphStyle.h"
#include "TextStyle.h"

#include "include/core/SkColor.h"
#include "include/core/SkRect.h"
#include "modules/skparagraph/include/Paragraph.h"
#include "modules/skunicode/include/SkUnicode.h"

#include <chrono>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <memory>
#include <string>
#include <vector>

namespace milestro::skia::textlayout {

enum class TextBoundarySnapMode : int32_t {
    Previous = 0,
    Next = 1,
    Nearest = 2,
};

enum class TextOverflow : int32_t {
    Clip = 0,
    Ellipsis = 1,
    Overflow = 2,
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
    float scrollY = 0.0f;
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

    const std::string& text() const {
        return text_;
    }
    size_t utf8Length() const {
        return text_.size();
    }
    size_t utf16Length() const {
        return utf8ForUtf16_.empty() ? 0 : utf8ForUtf16_.size() - 1;
    }

    size_t utf8ToUtf16(size_t utf8Offset) const;
    size_t utf16ToUtf8(size_t utf16Offset) const;

    bool isBoundary(size_t utf8Offset) const;
    size_t previousBoundary(size_t utf8Offset) const;
    size_t nextBoundary(size_t utf8Offset) const;
    size_t nearestBoundary(size_t utf8Offset) const;
    size_t snapUtf8(size_t utf8Offset, TextBoundarySnapMode mode) const;
    size_t boundaryCount() const {
        return boundariesUtf8_.size();
    }
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
    ~InputBox();

    void setText(const char* text, size_t length);
    const std::string& getText() const {
        return boundaryMap_.text();
    }

    void setViewport(SkScalar width, SkScalar height);
    void setSoftWrap(bool softWrap);
    bool getSoftWrap() const {
        return softWrap_;
    }
    void setFocused(bool focused);
    bool getFocused() const {
        return focused_;
    }
    void setTextOverflow(TextOverflow textOverflow);
    TextOverflow getTextOverflow() const {
        return textOverflow_;
    }
    void setEllipsis(const char* text, size_t length);
    const std::string& getEllipsis() const {
        return ellipsisText_;
    }
    void setMaskInput(bool maskInput);
    bool getMaskInput() const {
        return maskInput_;
    }
    void setMaskChar(const char* text, size_t length);
    const std::string& getMaskChar() const {
        return maskText_;
    }
    void setCaretColor(SkColor color) {
        caretColor_ = color;
    }
    void setSelectionColor(SkColor color) {
        selectionColor_ = color;
    }
    void setCaretWidth(SkScalar width);
    void setCaretVisible(bool visible) {
        caretVisible_ = visible;
    }
    void setAutoMargin(bool left, bool top, bool right, bool bottom);
    bool getAutoMarginLeft() const {
        return autoMarginLeft_;
    }
    bool getAutoMarginTop() const {
        return autoMarginTop_;
    }
    bool getAutoMarginRight() const {
        return autoMarginRight_;
    }
    bool getAutoMarginBottom() const {
        return autoMarginBottom_;
    }

    size_t getCursorUtf8() const {
        return cursorUtf8_;
    }
    size_t getCursorUtf16() const {
        return boundaryMap_.utf8ToUtf16(cursorUtf8_);
    }
    ::skia::textlayout::Affinity getAffinity() const {
        return affinity_;
    }
    void setCursorUtf8(size_t utf8Offset, ::skia::textlayout::Affinity affinity);

    size_t utf8ToUtf16(size_t utf8Offset) const {
        return boundaryMap_.utf8ToUtf16(utf8Offset);
    }
    size_t utf16ToUtf8(size_t utf16Offset) const {
        return boundaryMap_.utf16ToUtf8(utf16Offset);
    }
    size_t snapUtf8(size_t utf8Offset, TextBoundarySnapMode mode) const {
        return boundaryMap_.snapUtf8(utf8Offset, mode);
    }

    void insertText(const char* text, size_t length);
    bool setComposition(const char* text, size_t length);
    bool commitComposition(const char* text, size_t length);
    bool clearComposition();
    bool hasComposition() const {
        return !compositionText_.empty();
    }
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
    bool moveUp(bool extendSelection = false);
    bool moveDown(bool extendSelection = false);
    bool moveLineStart(bool extendSelection = false);
    bool moveLineEnd(bool extendSelection = false);
    bool moveDocumentStart(bool extendSelection = false);
    bool moveDocumentEnd(bool extendSelection = false);
    bool hitTest(SkScalar x, SkScalar y, bool extendSelection = false);
    bool undo();
    bool redo();
    void breakUndoGroup();

    void ensureCaretVisible();
    bool scrollByX(SkScalar delta);
    bool scrollByY(SkScalar delta);
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
    SkScalar scrollY_ = 0.0f;
    SkScalar caretWidth_ = 2.0f;
    SkColor caretColor_ = SK_ColorWHITE;
    SkColor selectionColor_ = SkColorSetARGB(0x66, 0x33, 0x7D, 0xFF);
    bool caretVisible_ = true;
    bool softWrap_ = true;
    bool focused_ = true;
    bool singleLineInput_ = true;
    bool maskInput_ = false;
    bool autoMarginLeft_ = false;
    bool autoMarginTop_ = false;
    bool autoMarginRight_ = false;
    bool autoMarginBottom_ = false;
    bool paragraphDirty_ = true;
    bool paintParagraphDirty_ = true;
    bool selectionRectsDirty_ = true;
    TextOverflow textOverflow_ = TextOverflow::Clip;
    std::string ellipsisText_ = "\xE2\x80\xA6";
    std::string maskText_ = "*";
    std::string compositionText_;
    size_t selectionAnchorUtf8_ = 0;
    size_t selectionFocusUtf8_ = 0;
    ::skia::textlayout::Affinity selectionAnchorAffinity_ = ::skia::textlayout::Affinity::kDownstream;
    ::skia::textlayout::Affinity selectionFocusAffinity_ = ::skia::textlayout::Affinity::kDownstream;
    std::shared_ptr<::skia::textlayout::Paragraph> paintParagraphCache_;
    std::shared_ptr<const std::vector<InputBoxCaretRect>> selectionRectsCache_;

    enum class EditKind {
        Typing,
        DeleteBackward,
        DeleteForward,
        DeleteSelection,
        ReplaceSelection,
        ImeCommit,
    };

    struct EditState {
        std::string text;
        size_t cursorUtf8 = 0;
        ::skia::textlayout::Affinity affinity = ::skia::textlayout::Affinity::kDownstream;
        size_t selectionAnchorUtf8 = 0;
        size_t selectionFocusUtf8 = 0;
        ::skia::textlayout::Affinity selectionAnchorAffinity = ::skia::textlayout::Affinity::kDownstream;
        ::skia::textlayout::Affinity selectionFocusAffinity = ::skia::textlayout::Affinity::kDownstream;
    };

    struct EditGroup {
        EditState before;
        EditState after;
        EditKind kind = EditKind::Typing;
        std::chrono::steady_clock::time_point updatedAt;
    };

    std::vector<EditGroup> undoStack_;
    std::vector<EditGroup> redoStack_;
    bool undoMergeBarrier_ = true;
    SkScalar preferredCaretX_ = std::numeric_limits<SkScalar>::quiet_NaN();

    static std::string sanitizePlainText(const char* text, size_t length);

    struct DisplayTextState {
        std::string text;
        std::vector<size_t> committedUtf8ForDisplayUtf8;
        std::vector<size_t> displayUtf8ForCommittedUtf8;
        size_t compositionStartUtf8 = 0;
        size_t compositionEndUtf8 = 0;

        size_t committedUtf8FromDisplay(size_t displayUtf8) const;
        size_t displayUtf8FromCommitted(size_t committedUtf8) const;
    };

    void configureInputParagraphMetrics();
    void markParagraphDirty();
    void markSelectionStateDirty();
    void markSelectionRectsDirty();
    void rebuildParagraphIfNeeded();
    std::unique_ptr<::skia::textlayout::Paragraph> buildParagraph() const;
    std::unique_ptr<::skia::textlayout::Paragraph> buildParagraphForText(const std::string& text, bool ellipsize) const;
    std::unique_ptr<::skia::textlayout::Paragraph> buildPaintParagraph() const;
    std::shared_ptr<::skia::textlayout::Paragraph> getPaintParagraphSnapshot();
    std::shared_ptr<const std::vector<InputBoxCaretRect>> getSelectionRectsSnapshot();
    std::vector<InputBoxCaretRect> buildSelectionRects();
    bool shouldUseEllipsisDisplay() const;
    bool shouldClipDisplayToViewport() const;
    InputBoxMetrics metricsForParagraph(::skia::textlayout::Paragraph* paragraph,
                                        SkScalar scrollX,
                                        SkScalar scrollY,
                                        SkScalar contentWidth) const;
    std::string displayText() const;
    DisplayTextState displayTextState() const;
    TextBoundaryMap displayBoundaryMap() const;
    size_t displayCaretUtf8() const;
    size_t displayCompositionStartUtf8() const;
    size_t displayCompositionEndUtf8() const;
    size_t compositionStartUtf8() const;
    size_t compositionReplacedEndUtf8() const;
    size_t committedUtf8FromDisplay(size_t displayUtf8) const;
    InputBoxCaretRect getCaretRectForDisplayOffset(size_t displayUtf8);
    std::vector<InputBoxCaretRect> getRectsForCommittedRange(size_t startUtf8, size_t endUtf8);
    bool resolveLineMetricsForDisplayUtf8(size_t displayUtf8,
                                          ::skia::textlayout::Affinity affinity,
                                          ::skia::textlayout::LineMetrics& lineMetrics,
                                          int& lineNumber);
    bool getLineMetricsAt(int lineNumber, ::skia::textlayout::LineMetrics& lineMetrics) const;
    size_t displayUtf8ForLineX(int lineNumber, SkScalar x, ::skia::textlayout::Affinity& affinity);
    SkScalar caretXForDisplayOffset(size_t displayUtf8,
                                    const ::skia::textlayout::LineMetrics& lineMetrics,
                                    const TextBoundaryMap& displayMap);
    bool moveToUtf8(size_t targetUtf8,
                    ::skia::textlayout::Affinity targetAffinity,
                    bool extendSelection,
                    bool resetPreferredCaretX);
    bool moveVertical(int lineDelta, bool extendSelection);
    size_t selectionStartUtf8() const;
    size_t selectionEndUtf8() const;
    SkScalar visualOffsetX();
    SkScalar visualOffsetXForParagraph(::skia::textlayout::Paragraph* paragraph) const;
    SkScalar visualContentWidthForParagraph(::skia::textlayout::Paragraph* paragraph) const;
    SkScalar visualOffsetY();
    SkScalar visualOffsetYForParagraph(::skia::textlayout::Paragraph* paragraph) const;
    SkScalar contentHeight();
    SkScalar maxScrollY();
    InputBoxCaretRect activeEnsureVisibleRect();
    void ensureRectVisible(const InputBoxCaretRect& rect);
    bool resetSelectionToCursor();
    void resetPreferredCaretX();
    bool replaceSelectionWith(std::string replacement);
    bool deleteSelection();
    SkScalar contentWidth();
    void replaceText(std::string text, size_t requestedCursor);
    EditState captureEditState() const;
    void restoreEditState(const EditState& state);
    void recordEdit(const EditState& before, EditKind kind);
    void clearEditHistory();
    bool canMergeEdit(const EditGroup& group,
                      EditKind kind,
                      const EditState& before,
                      std::chrono::steady_clock::time_point now) const;
    void pruneEditHistory(std::vector<EditGroup>& stack);
    static bool isMergeableEditKind(EditKind kind);
    static bool editStateEquals(const EditState& left, const EditState& right);
    static size_t editStateByteCost(const EditState& state);
    static size_t editGroupByteCost(const EditGroup& group);
    static size_t editHistoryByteCost(const std::vector<EditGroup>& stack);
    void markCompositionDirty();
};

class MILESTRO_API InputBoxDrawSnapshot {
public:
    InputBoxDrawSnapshot(std::shared_ptr<::skia::textlayout::Paragraph> paragraph,
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
                         bool clipToViewport);

    void paint(SkCanvas* canvas, SkScalar x, SkScalar y, SkScalar width, SkScalar height) const;

private:
    std::shared_ptr<::skia::textlayout::Paragraph> paragraph_;
    InputBoxCaretRect caretRect_;
    InputBoxMetrics metrics_;
    InputBoxCaretRect compositionRect_;
    std::shared_ptr<const std::vector<InputBoxCaretRect>> selectionRects_;
    SkScalar caretWidth_ = 1.0f;
    SkScalar visualOffsetX_ = 0.0f;
    SkScalar visualOffsetY_ = 0.0f;
    SkColor caretColor_ = SK_ColorWHITE;
    SkColor selectionColor_ = SkColorSetARGB(0x66, 0x33, 0x7D, 0xFF);
    bool caretVisible_ = false;
    bool compositionVisible_ = false;
    bool clipToViewport_ = true;
};

} // namespace milestro::skia::textlayout

#endif // MILESTRO_SKIA_TEXTLAYOUT_INPUTBOX_H

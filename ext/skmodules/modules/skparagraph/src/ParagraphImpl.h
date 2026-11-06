// Copyright 2019 Google LLC
#ifndef ParagraphImpl_DEFINED
#define ParagraphImpl_DEFINED

#include "include/core/SkFont.h"
#include "include/core/SkPaint.h"
#include "include/core/SkPicture.h"
#include "include/core/SkPoint.h"
#include "include/core/SkRect.h"
#include "include/core/SkRefCnt.h"
#include "include/core/SkScalar.h"
#include "include/core/SkSpan.h"
#include "include/core/SkString.h"
#include "include/core/SkTypes.h"
#include "include/private/SkOnce.h"
#include "include/private/SkTArray.h"
#include "include/private/SkTemplates.h"
#include "modules/skparagraph/include/DartTypes.h"
#include "modules/skparagraph/include/FontCollection.h"
#include "modules/skparagraph/include/Paragraph.h"
#include "modules/skparagraph/include/ParagraphCache.h"
#include "modules/skparagraph/include/ParagraphStyle.h"
#include "modules/skparagraph/include/TextShadow.h"
#include "modules/skparagraph/include/TextStyle.h"
#include "modules/skparagraph/src/Run.h"
#include "modules/skparagraph/src/RubyLayout.h"
#include "modules/skparagraph/src/TextLine.h"
#include "modules/skunicode/include/SkUnicode.h"
#include "src/core/SkBitmaskEnum.h"
#include "src/core/SkTHash.h"

#include <algorithm>
#include <memory>
#include <string>
#include <vector>

class SkCanvas;

namespace skia {
namespace textlayout {

class LineMetrics;
class TextLine;

struct VerticalAffixRun {
    SkFont font;
    bool sideways = false;
    SkScalar inlineOffset = 0;
    std::vector<SkGlyphID> glyphs;
    std::vector<SkPoint> positions;
    std::vector<SkPoint> offsets;
};

struct VerticalAffix {
    TextStyle style;
    TextIndex insertion = 0;
    SkScalar inlineStart = 0;
    SkScalar advance = 0;
    std::vector<VerticalAffixRun> runs;
};

struct VerticalColumnGeometry {
    SkScalar inlineSourceStart = 0;
    SkScalar blockAscent = 0;
    SkScalar blockDescent = 0;
    bool hardBreak = false;
    ClusterRange clusters = EMPTY_CLUSTERS;
    TextRange text = EMPTY_TEXT;
    SkScalar inlineContentExtent = 0;
    SkScalar inlineExtent = 0;
    bool justified = false;
    size_t justificationOpportunityCount = 0;
    SkScalar justificationStep = 0;
    std::vector<SkScalar> justificationShifts;
    bool ellipsized = false;
    VerticalAffix affix;
    SkScalar rubyGrowth = 0;
    SkScalar rubyBaseAscent = 0;
};

struct VerticalRubySpan {
    RubyAnnotation input;
    ClusterRange clusters;
    std::unique_ptr<Paragraph> annotation;
    SkRect bounds;
    SkScalar inlineStart;
    SkScalar inlineExtent;
    SkScalar annotationOffset;
    SkScalar baseOffset;
    SkScalar baseExtent;
    SkScalar overflowShift = 0;
};

struct VerticalTateChuYokoSpan {
    TextRange range = EMPTY_TEXT;
    ClusterRange clusters = EMPTY_CLUSTERS;
    std::unique_ptr<Paragraph> content;
    TextStyle style;
    SkScalar naturalWidth = 0;
    SkScalar lineAscent = 0;
    SkScalar lineDescent = 0;
    SkScalar inlineStart = 0;
    SkScalar inlineExtent = 0;
    SkScalar cellWidth = 0;
    SkScalar squeeze = 1;
    SkScalar blockOffset = 0;
    TateChuYokoSizing sizing = TateChuYokoSizing::kSqueeze;
    bool styleIsDefault = false;
};

struct VerticalDecorationSegment {
    size_t column = 0;
    TextDecoration decoration = TextDecoration::kNoDecoration;
    SkRect rect = SkRect::MakeEmpty();
    SkColor color = SK_ColorTRANSPARENT;
    SkScalar thickness = 0;
};

struct VerticalBackgroundSegment {
    size_t column = 0;
    SkRect rect = SkRect::MakeEmpty();
    ParagraphPainter::SkPaintOrID paint;
};

template <typename T>
bool operator==(const SkSpan<T>& a, const SkSpan<T>& b) {
    return a.size() == b.size() && a.begin() == b.begin();
}

template <typename T>
bool operator<=(const SkSpan<T>& a, const SkSpan<T>& b) {
    return a.begin() >= b.begin() && a.end() <= b.end();
}

template <typename TStyle>
struct StyleBlock {
    StyleBlock() : fRange(EMPTY_RANGE), fStyle() { }
    StyleBlock(size_t start, size_t end, const TStyle& style) : fRange(start, end), fStyle(style) {}
    StyleBlock(TextRange textRange, const TStyle& style) : fRange(textRange), fStyle(style) {}
    void add(TextRange tail) {
        SkASSERT(fRange.end == tail.start);
        fRange = TextRange(fRange.start, fRange.start + fRange.width() + tail.width());
    }
    TextRange fRange;
    TStyle fStyle;
};

struct ResolvedFontDescriptor {
    ResolvedFontDescriptor(TextIndex index, SkFont font)
            : fFont(std::move(font)), fTextStart(index) {}
    SkFont fFont;
    TextIndex fTextStart;
};

enum InternalState {
  kUnknown = 0,
  kIndexed = 1,     // Text is indexed
  kShaped = 2,      // Text is shaped
  kLineBroken = 5,
  kFormatted = 6,
  kDrawn = 7
};

/*
struct BidiRegion {
    BidiRegion(size_t start, size_t end, uint8_t dir)
        : text(start, end), direction(dir) { }
    TextRange text;
    uint8_t direction;
};
*/
class ParagraphImpl final : public Paragraph {

public:

    ParagraphImpl(const SkString& text,
                  ParagraphStyle style,
                  skia_private::TArray<Block> blocks,
                  skia_private::TArray<Placeholder> placeholders,
                  sk_sp<FontCollection> fonts,
                  sk_sp<SkUnicode> unicode);

    ParagraphImpl(const std::u16string& utf16text,
                  ParagraphStyle style,
                  skia_private::TArray<Block> blocks,
                  skia_private::TArray<Placeholder> placeholders,
                  sk_sp<FontCollection> fonts,
                  sk_sp<SkUnicode> unicode);

    ~ParagraphImpl() override;

    void layout(SkScalar width) override;
    void layoutWithInlineExtent(SkScalar inlineExtent) override;
    const ParagraphLayoutMetrics& getLayoutMetrics() const override;
    void paint(SkCanvas* canvas, SkScalar x, SkScalar y) override;
    void paint(ParagraphPainter* canvas, SkScalar x, SkScalar y) override;
    std::vector<TextBox> getRectsForRange(unsigned start,
                                          unsigned end,
                                          RectHeightStyle rectHeightStyle,
                                          RectWidthStyle rectWidthStyle) override;
    std::vector<TextBox> getRectsForPlaceholders() override;
    void getLineMetrics(std::vector<LineMetrics>&) override;
    PositionWithAffinity getGlyphPositionAtCoordinate(SkScalar dx, SkScalar dy) override;
    SkRange<size_t> getWordBoundary(unsigned offset) override;

    bool getApplyRoundingHack() const { return fParagraphStyle.getApplyRoundingHack(); }

    size_t lineNumber() override { return fLines.size(); }

    TextLine& addLine(SkVector offset, SkVector advance,
                      TextRange textExcludingSpaces, TextRange text, TextRange textIncludingNewlines,
                      ClusterRange clusters, ClusterRange clustersWithGhosts, SkScalar widthWithSpaces,
                      InternalLineMetrics sizes);

    SkSpan<const char> text() const { return SkSpan<const char>(fText.c_str(), fText.size()); }
    InternalState state() const { return fState; }
    SkSpan<Run> runs() { return SkSpan<Run>(fRuns.data(), fRuns.size()); }
    SkSpan<Block> styles() {
        return SkSpan<Block>(fTextStyles.data(), fTextStyles.size());
    }
    SkSpan<Placeholder> placeholders() {
        return SkSpan<Placeholder>(fPlaceholders.data(), fPlaceholders.size());
    }
    SkSpan<TextLine> lines() {
        return SkSpan<TextLine>(fLines.data(), fLines.size());
    }
    SkSpan<const VerticalColumnGeometry> verticalColumns() const {
        return SkSpan<const VerticalColumnGeometry>(fVerticalColumns.data(), fVerticalColumns.size());
    }
    const ParagraphStyle& paragraphStyle() const {
        return fParagraphStyle;
    }
    SkSpan<Cluster> clusters() {
        return SkSpan<Cluster>(fClusters.begin(), fClusters.size());
    }
    sk_sp<FontCollection> fontCollection() const {
        return fFontCollection;
    }
    void formatLines(SkScalar maxWidth);
    SkScalar textIndent() const {
        return fTextIndent;
    }
    SkScalar firstLineWidth(SkScalar maxWidth) const {
        return SkIsFinite(maxWidth) ? std::max(SkScalar(0), maxWidth - fTextIndent) : maxWidth;
    }
    SkScalar firstLineOffset() const {
        return fParagraphStyle.getTextDirection() == TextDirection::kLtr ? fTextIndent : 0;
    }
    void setRubyAnnotations(std::vector<RubyAnnotation> annotations);
    bool hasRuby() const { return !fRubyAnnotations.empty(); }
    void setTateChuYokoAnnotations(std::vector<TateChuYokoAnnotation> annotations) {
        fTateChuYokoAnnotations = std::move(annotations);
        fTateChuYokoPrepared = false;
    }
    void setOrientationOverrides(std::vector<OrientationOverride> overrides) {
        fOrientationOverrides = std::move(overrides);
        std::sort(fOrientationOverrides.begin(), fOrientationOverrides.end(),
                  [](const OrientationOverride& a, const OrientationOverride& b) {
                      return a.range.start < b.range.start;
                  });
    }
    void setNoWrapRanges(std::vector<TextRange> ranges) {
        fNoWrapRanges = std::move(ranges);
    }
    bool hasTateChuYoko() const { return !fTateChuYokoAnnotations.empty(); }
    bool hasOrientationOverrides() const { return !fOrientationOverrides.empty(); }
    void prepareVerticalTateChuYoko();
    const VerticalTateChuYokoSpan* verticalTateChuYokoAtCluster(ClusterIndex cluster) const;
    const VerticalTateChuYokoSpan* verticalTateChuYokoStartingAt(ClusterIndex cluster) const;
    std::vector<std::pair<const VerticalTateChuYokoSpan*, SkPoint>>
    verticalTateChuYokoPlacements(size_t column) const;
    void prepareRuby();
    void positionRuby();
    void addRubyMetrics(ClusterRange clusters, InternalLineMetrics& metrics) const;
    const std::vector<RubyLayoutUnit>& rubyUnits() const { return fRubyUnits; }
    std::vector<RubyPlacement> rubyPlacements(const TextLine& line) const;
    const RubySpan* rubySpanAt(TextIndex index) const;
    bool rubyBoundary(ClusterIndex index) const;
    void ensureUTF16Mapping();
    skia_private::TArray<TextIndex> countSurroundingGraphemes(TextRange textRange) const;
    TextIndex findNextGraphemeBoundary(TextIndex utf8) const;
    TextIndex findPreviousGraphemeBoundary(TextIndex utf8) const;
    TextIndex findNextGlyphClusterBoundary(TextIndex utf8) const;
    TextIndex findPreviousGlyphClusterBoundary(TextIndex utf8) const;
    size_t getUTF16Index(TextIndex index) const {
        return fUTF16IndexForUTF8Index[index];
    }

    bool strutEnabled() const { return paragraphStyle().getStrutStyle().getStrutEnabled(); }
    bool strutForceHeight() const {
        return paragraphStyle().getStrutStyle().getForceStrutHeight();
    }
    bool strutHeightOverride() const {
        return paragraphStyle().getStrutStyle().getHeightOverride();
    }
    InternalLineMetrics strutMetrics() const { return fStrutMetrics; }

    SkString getEllipsis() const;

    SkSpan<const char> text(TextRange textRange);
    SkSpan<Cluster> clusters(ClusterRange clusterRange);
    Cluster& cluster(ClusterIndex clusterIndex);
    ClusterIndex clusterIndex(TextIndex textIndex) {
        auto clusterIndex = this->fClustersIndexFromCodeUnit[textIndex];
        SkASSERT(clusterIndex != EMPTY_INDEX);
        return clusterIndex;
    }
    Run& run(RunIndex runIndex) {
        SkASSERT(runIndex < SkToSizeT(fRuns.size()));
        return fRuns[runIndex];
    }

    Run& runByCluster(ClusterIndex clusterIndex);
    SkSpan<Block> blocks(BlockRange blockRange);
    Block& block(BlockIndex blockIndex);
    skia_private::TArray<ResolvedFontDescriptor> resolvedFonts() const {
        return fFontSwitches;
    }

    void markDirty() override {
        if (this->isVertical()) {
            this->invalidateVerticalLayout();
        }
        if (fState > kIndexed) {
            fState = kIndexed;
        }
    }

    int32_t unresolvedGlyphs() override;
    std::unordered_set<SkUnichar> unresolvedCodepoints() override;
    void addUnresolvedCodepoints(TextRange textRange);

    void setState(InternalState state);
    sk_sp<SkPicture> getPicture() { return fPicture; }

    SkScalar widthWithTrailingSpaces() { return fMaxWidthWithTrailingSpaces; }

    void resetContext();
    void resolveStrut();

    bool computeCodeUnitProperties();
    void applyNoWrapRanges();
    void applySpacingAndBuildClusterTable();
    void applyCjkPunctuationCompression();
    void buildClusterTable();
    bool shapeTextIntoEndlessLine();
    void breakShapedTextIntoLines(SkScalar maxWidth);
    bool validateVerticalConfiguration(SkScalar inlineExtent);
    void layoutVertical(SkScalar inlineExtent);
    bool shapeVerticalRuns();
    void prepareVerticalRuby();
    void validateVerticalOrientationOverrides();
    SkScalar verticalRubyShift(ClusterIndex cluster) const;
    std::vector<std::pair<const VerticalRubySpan*, SkPoint>> verticalRubyPlacements(size_t column) const;
    void breakShapedTextIntoVerticalColumns(SkScalar maxInlineExtent);
    void paintVertical(ParagraphPainter* painter, SkScalar x, SkScalar y);
    std::vector<TextBox> getVerticalRectsForRange(unsigned start, unsigned end, bool inlineBox = false);
    PositionWithAffinity getVerticalGlyphPositionAtCoordinate(SkScalar dx, SkScalar dy);
    bool getVerticalGlyphClusterAt(TextIndex codeUnitIndex, GlyphClusterInfo* glyphInfo);
    bool getVerticalClosestGlyphClusterAt(SkScalar dx, SkScalar dy, GlyphClusterInfo* glyphInfo);
    bool getVerticalGlyphInfoAtUTF16Offset(size_t codeUnitIndex, GlyphInfo* glyphInfo);
    bool getVerticalClosestUTF16GlyphInfoAt(SkScalar dx, SkScalar dy, GlyphInfo* glyphInfo);
    void resolveVerticalMetrics();
    void resolveVerticalDecorations();
    void resolveVerticalBackgrounds();
    void ensureVerticalLayoutReady() const;
    void invalidateVerticalLayout();
    int getVerticalColumnForCluster(ClusterIndex clusterIndex) const;
    bool getVerticalClusterCell(ClusterIndex clusterIndex, SkRect* cell) const;
    SkScalar getVerticalClusterShift(ClusterIndex clusterIndex) const;
    SkScalar getVerticalClusterShiftAfter(ClusterIndex clusterIndex) const;
    void extendedVisitVertical(const ExtendedVisitor& visitor);
    int getVerticalPath(int lineNumber, SkPath* dest);

    void updateTextAlign(TextAlign textAlign) override;
    void updateFontSize(size_t from, size_t to, SkScalar fontSize) override;
    void updateForegroundPaint(size_t from, size_t to, SkPaint paint) override;
    void updateBackgroundPaint(size_t from, size_t to, SkPaint paint) override;

    void visit(const Visitor&) override;
    void extendedVisit(const ExtendedVisitor&) override;
    int getPath(int lineNumber, SkPath* dest) override;
    bool containsColorFontOrBitmap(SkTextBlob* textBlob) override;
    bool containsEmoji(SkTextBlob* textBlob) override;

    int getLineNumberAt(TextIndex codeUnitIndex) const override;
    int getLineNumberAtUTF16Offset(size_t codeUnitIndex) override;
    bool getLineMetricsAt(int lineNumber, LineMetrics* lineMetrics) const override;
    TextRange getActualTextRange(int lineNumber, bool includeSpaces) const override;
    bool getGlyphClusterAt(TextIndex codeUnitIndex, GlyphClusterInfo* glyphInfo) override;
    bool getClosestGlyphClusterAt(SkScalar dx,
                                  SkScalar dy,
                                  GlyphClusterInfo* glyphInfo) override;

    bool getGlyphInfoAtUTF16Offset(size_t codeUnitIndex, GlyphInfo* graphemeInfo) override;
    bool getClosestUTF16GlyphInfoAt(SkScalar dx, SkScalar dy, GlyphInfo* graphemeInfo) override;
    SkFont getFontAt(TextIndex codeUnitIndex) const override;
    SkFont getFontAtUTF16Offset(size_t codeUnitIndex) override;
    std::vector<FontInfo> getFonts() const override;

    InternalLineMetrics getEmptyMetrics() const { return fEmptyMetrics; }
    InternalLineMetrics getStrutMetrics() const { return fStrutMetrics; }

    BlockRange findAllBlocks(TextRange textRange);

    void resetShifts() {
        for (auto& run : fRuns) {
            run.resetJustificationShifts();
        }
    }

    bool codeUnitHasProperty(size_t index, SkUnicode::CodeUnitFlags property) const {
        return (fCodeUnitProperties[index] & property) == property;
    }

    bool isNoWrapBoundary(TextIndex index, bool atLineStart) const {
        for (const auto& range : fNoWrapRanges) {
            if (atLineStart && index == range.start && index < range.end) {
                return true;
            }
            if (range.start < index &&
                (index < range.end || (index == range.end && index == fText.size()))) {
                return true;
            }
        }
        return false;
    }

    sk_sp<SkUnicode> getUnicode() { return fUnicode; }

private:
    friend class ParagraphBuilder;
    friend class ParagraphCacheKey;
    friend class ParagraphCacheValue;
    friend class ParagraphCache;

    friend class TextWrapper;
    friend class OneLineShaper;

    void computeEmptyMetrics();

    // Input
    std::vector<RubyAnnotation> fRubyAnnotations;
    std::vector<TateChuYokoAnnotation> fTateChuYokoAnnotations;
    std::vector<OrientationOverride> fOrientationOverrides;
    std::vector<TextRange> fNoWrapRanges;
    std::vector<RubySpan> fRubySpans;
    std::vector<RubyLayoutUnit> fRubyUnits;
    std::vector<std::vector<SkPoint>> fRubyOffsets;
    bool fRubyPrepared = false;
    bool fTateChuYokoPrepared = false;
    skia_private::TArray<StyleBlock<SkScalar>> fLetterSpaceStyles;
    skia_private::TArray<StyleBlock<SkScalar>> fWordSpaceStyles;
    skia_private::TArray<StyleBlock<SkPaint>> fBackgroundStyles;
    skia_private::TArray<StyleBlock<SkPaint>> fForegroundStyles;
    skia_private::TArray<StyleBlock<std::vector<TextShadow>>> fShadowStyles;
    skia_private::TArray<StyleBlock<Decoration>> fDecorationStyles;
    skia_private::TArray<Block> fTextStyles; // TODO: take out only the font stuff
    skia_private::TArray<Placeholder> fPlaceholders;
    SkString fText;

    // Internal structures
    InternalState fState;
    skia_private::TArray<Run> fRuns;                // kShaped
    skia_private::TArray<Cluster, true> fClusters;  // kClusterized (cached: text, word spacing, letter spacing, resolved fonts)
    skia_private::TArray<SkUnicode::CodeUnitFlags, true> fCodeUnitProperties;
    skia_private::TArray<size_t, true> fClustersIndexFromCodeUnit;
    std::vector<size_t> fWords;
    std::vector<SkUnicode::BidiRegion> fBidiRegions;
    // These two arrays are used in measuring methods (getRectsForRange, getGlyphPositionAtCoordinate)
    // They are filled lazily whenever they need and cached
    skia_private::TArray<TextIndex, true> fUTF8IndexForUTF16Index;
    skia_private::TArray<size_t, true> fUTF16IndexForUTF8Index;
    SkOnce fillUTF16MappingOnce;
    size_t fUnresolvedGlyphs;
    // Null if all codepoints resolved. ParagraphCache shares the set, so it does not change once
    // shaping is done; the next shaping makes a new set
    std::shared_ptr<std::unordered_set<SkUnichar>> fUnresolvedCodepoints;

    skia_private::TArray<TextLine, false> fLines;   // kFormatted   (cached: width, max lines, ellipsis, text align)
    sk_sp<SkPicture> fPicture;          // kRecorded    (cached: text styles)
    ParagraphLayoutMetrics fLayoutMetrics;
    std::vector<VerticalColumnGeometry> fVerticalColumns;
    std::vector<VerticalRubySpan> fVerticalRuby;
    std::vector<VerticalTateChuYokoSpan> fVerticalTateChuYoko;
    std::vector<VerticalDecorationSegment> fVerticalDecorations;
    std::vector<VerticalBackgroundSegment> fVerticalBackgrounds;

    skia_private::TArray<ResolvedFontDescriptor> fFontSwitches;

    InternalLineMetrics fEmptyMetrics;
    InternalLineMetrics fStrutMetrics;

    SkScalar fOldWidth;
    SkScalar fOldHeight;
    SkScalar fOldInlineExtent;
    SkScalar fMaxWidthWithTrailingSpaces;
    SkScalar fTextIndent;

    sk_sp<SkUnicode> fUnicode;
    bool fHasLineBreaks;
    bool fHasWhitespacesInside;
    TextIndex fTrailingSpaces;
};
}  // namespace textlayout
}  // namespace skia


#endif  // ParagraphImpl_DEFINED

// Copyright 2026 Milestro contributors
#include "modules/skparagraph/src/ParagraphImpl.h"
#include "modules/skparagraph/src/ParagraphBuilderImpl.h"

#include "include/core/SkFontMetrics.h"
#include "include/core/SkPathBuilder.h"
#include "include/core/SkTextBlob.h"
#include "include/private/SkDebug.h"
#include "include/private/SkTo.h"
#include "modules/skparagraph/src/ParagraphPainterImpl.h"
#include "skttbshaper/TtbShaper.h"
#include "src/core/SkUTF.h"

#include <unicode/ubidi.h>
#include <unicode/uchar.h>
#include <unicode/uscript.h>

#include <algorithm>
#include <cmath>
#include <limits>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

namespace skia {
namespace textlayout {
namespace {

constexpr size_t kNoBreak = std::numeric_limits<size_t>::max();

bool IsFiniteNonNegative(SkScalar value) {
    return SkIsFinite(value) && value >= 0;
}

bool HasDecoration(TextDecoration type, TextDecoration decoration) {
    return (static_cast<int>(type) & static_cast<int>(decoration)) != 0;
}

bool IsInfinitePositive(SkScalar value) {
    return std::isinf(value) && value > 0;
}

constexpr int kSupportedVerticalDecorationMask =
        static_cast<int>(TextDecoration::kUnderline) |
        static_cast<int>(TextDecoration::kLineThrough);

bool IsSupportedVerticalDecoration(TextDecoration decoration) {
    const int bits = static_cast<int>(decoration);
    return decoration != TextDecoration::kNoDecoration &&
           (bits & ~kSupportedVerticalDecorationMask) == 0;
}

enum class VerticalOpportunityKind {
    Neutral,
    Asian,
    Latin,
};

VerticalOpportunityKind ClassifyVerticalOpportunity(SkUnichar codepoint) {
    const uint32_t categoryMask = U_MASK(u_charType(codepoint));
    if (categoryMask & (U_GC_Z_MASK | U_GC_C_MASK)) {
        return VerticalOpportunityKind::Neutral;
    }
    if (categoryMask & (U_GC_M_MASK | U_GC_N_MASK)) {
        return VerticalOpportunityKind::Latin;
    }
    // Punctuation and symbols, including the kana middle dot and wave dash,
    // advertise Japanese script extensions. They are still not Asian units.
    if (categoryMask & (U_GC_P_MASK | U_GC_S_MASK)) {
        return VerticalOpportunityKind::Neutral;
    }
    // Script_Extensions keeps kana-length marks and iteration marks with their
    // base script without maintaining a private code point list.
    if (uscript_hasScript(codepoint, USCRIPT_HAN) ||
        uscript_hasScript(codepoint, USCRIPT_HIRAGANA) ||
        uscript_hasScript(codepoint, USCRIPT_KATAKANA) ||
        uscript_hasScript(codepoint, USCRIPT_HANGUL) ||
        uscript_hasScript(codepoint, USCRIPT_BOPOMOFO)) {
        return VerticalOpportunityKind::Asian;
    }
    if (uscript_hasScript(codepoint, USCRIPT_LATIN)) {
        return VerticalOpportunityKind::Latin;
    }
    return VerticalOpportunityKind::Neutral;
}

bool IsWordLike(VerticalOpportunityKind kind) {
    return kind == VerticalOpportunityKind::Asian || kind == VerticalOpportunityKind::Latin;
}

SkScalar VerticalClusterShift(const VerticalColumnGeometry& column, ClusterIndex clusterIndex) {
    if (column.justificationShifts.empty() || clusterIndex < column.clusters.start ||
        clusterIndex >= column.clusters.end) {
        return 0;
    }
    return column.justificationShifts[clusterIndex - column.clusters.start];
}

SkScalar VerticalClusterShiftAfter(const VerticalColumnGeometry& column, ClusterIndex clusterIndex) {
    if (column.justificationShifts.empty() || clusterIndex < column.clusters.start ||
        clusterIndex >= column.clusters.end) {
        return 0;
    }
    return column.justificationShifts[clusterIndex - column.clusters.start + 1];
}

bool IsStrongRtl(SkUnichar codepoint) {
    const auto direction = u_charDirection(codepoint);
    return direction == U_RIGHT_TO_LEFT || direction == U_RIGHT_TO_LEFT_ARABIC;
}

bool IsBidiControl(SkUnichar codepoint) {
    return u_hasBinaryProperty(codepoint, UCHAR_BIDI_CONTROL);
}

bool IsIgnoredVerticalControl(SkUnichar codepoint) {
    return codepoint == 0x00AD || IsBidiControl(codepoint);
}

bool IsIgnoredVerticalControlAt(const SkString& text, TextIndex start) {
    if (start >= text.size()) {
        return false;
    }
    const char* current = text.c_str() + start;
    const char* end = text.c_str() + text.size();
    return IsIgnoredVerticalControl(SkUTF::NextUTF8WithReplacement(&current, end));
}

bool IsEnclosingMark(SkUnichar codepoint) {
    return u_charType(codepoint) == U_ENCLOSING_MARK;
}

bool IsUprightOrientation(SkUnichar codepoint) {
    return u_getIntPropertyValue(codepoint, UCHAR_VERTICAL_ORIENTATION) != U_VO_ROTATED;
}

SkFourByteTag TagFromName(const SkString& name) {
    if (name.size() != 4) {
        return 0;
    }
    const auto* bytes = reinterpret_cast<const unsigned char*>(name.c_str());
    return SkSetFourByteTag(bytes[0], bytes[1], bytes[2], bytes[3]);
}

const Block* FindBlock(const skia_private::TArray<Block>& blocks, TextIndex index) {
    for (const auto& block: blocks) {
        if (block.fRange.start <= index && index < block.fRange.end) {
            return &block;
        }
    }
    return blocks.empty() ? nullptr : &blocks.back();
}

VerticalTextOrientation OrientationOverrideAt(const std::vector<OrientationOverride>& overrides,
                                              TextIndex index) {
    const auto next = std::upper_bound(
            overrides.begin(), overrides.end(), index,
            [](TextIndex value, const OrientationOverride& override) {
                return value < override.range.start;
            });
    if (next == overrides.begin()) {
        return VerticalTextOrientation::kMixed;
    }
    const auto& candidate = *(next - 1);
    return index < candidate.range.end ? candidate.orientation : VerticalTextOrientation::kMixed;
}

bool IsSidewaysGrapheme(const char* text,
                        TextIndex start,
                        TextIndex end,
                        const std::vector<OrientationOverride>* overrides = nullptr) {
    if (overrides != nullptr) {
        const VerticalTextOrientation orientation = OrientationOverrideAt(*overrides, start);
        if (orientation == VerticalTextOrientation::kUpright) {
            return false;
        }
        if (orientation == VerticalTextOrientation::kSideways) {
            return true;
        }
    }

    bool first = true;
    SkUnichar base = 0;
    const char* current = text + start;
    const char* limit = text + end;
    bool enclosing = false;
    while (current < limit) {
        const SkUnichar codepoint = SkUTF::NextUTF8WithReplacement(&current, limit);
        if (IsEnclosingMark(codepoint)) {
            enclosing = true;
        }
        if (first && !u_hasBinaryProperty(codepoint, UCHAR_DEFAULT_IGNORABLE_CODE_POINT)) {
            base = codepoint;
            first = false;
        }
    }
    if (first) {
        return false;
    }
    if (enclosing) {
        return false;
    }
    return !IsUprightOrientation(base);
}

SkRect RotateClockwiseBounds(const SkRect& bounds) {
    // x' = -y, y' = x. Bounds are relative to the baseline.
    return SkRect::MakeLTRB(-bounds.bottom(), bounds.left(), -bounds.top(), bounds.right());
}

void RotateClockwise(SkPoint* point) {
    const SkScalar x = point->fX;
    point->fX = -point->fY;
    point->fY = x;
}

SkMatrix ClockwiseRotation() {
    SkMatrix matrix;
    matrix.setAll(0, -1, 0, 1, 0, 0, 0, 0, 1);
    return matrix;
}

SkPoint ReverseClockwise(const SkPoint& point) {
    return SkPoint::Make(point.fY, -point.fX);
}

SkScalar SidewaysBlockOffset(const Run& run) {
    if (!run.isSideways()) {
        return 0;
    }
    SkFontMetrics metrics;
    run.font().getMetrics(&metrics);
    if (!SkIsFinite(metrics.fAscent) || !SkIsFinite(metrics.fDescent)) {
        return 0;
    }
    // Sideways glyphs rotate about their alphabetic baseline. Move that
    // baseline onto the vertical central baseline shared with upright glyphs.
    return (metrics.fAscent + metrics.fDescent) * 0.5f;
}

SkScalar VerticalRunBlockOffset(const Run& run) {
    return SidewaysBlockOffset(run) - run.baselineShift();
}

SkScalar ResolveVerticalDecorationThickness(const Run& run,
                                            TextDecoration decoration,
                                            SkScalar multiplier) {
    SkFontMetrics metrics;
    run.font().getMetrics(&metrics);
    const SkScalar fontSize = run.font().getSize();
    SkScalar thickness = fontSize / 14.0f;
    if (decoration == TextDecoration::kUnderline) {
        if ((metrics.fFlags & SkFontMetrics::kUnderlineThicknessIsValid_Flag) != 0 &&
            SkIsFinite(metrics.fUnderlineThickness) && metrics.fUnderlineThickness > 0) {
            thickness = metrics.fUnderlineThickness;
        }
    } else if (decoration == TextDecoration::kLineThrough) {
        if ((metrics.fFlags & SkFontMetrics::kStrikeoutThicknessIsValid_Flag) != 0 &&
            SkIsFinite(metrics.fStrikeoutThickness) && metrics.fStrikeoutThickness > 0) {
            thickness = metrics.fStrikeoutThickness;
        }
    }
    thickness *= multiplier;
    return SkIsFinite(thickness) && thickness > 0 ? thickness : 0;
}

SkScalar ResolveVerticalUnderlineCenter(const Run& run,
                                        SkScalar blockCenter,
                                        SkScalar thickness) {
    SkFontMetrics metrics;
    run.font().getMetrics(&metrics);
    SkScalar position = thickness;
    if ((metrics.fFlags & SkFontMetrics::kUnderlinePositionIsValid_Flag) != 0 &&
        SkIsFinite(metrics.fUnderlinePosition) && metrics.fUnderlinePosition > 0) {
        position = metrics.fUnderlinePosition;
    }
    return blockCenter - run.font().getSize() * 0.5f - position;
}

struct VerticalBlockMetrics {
    double ascent;
    double descent;
    double thickness;
};

VerticalBlockMetrics ResolveVerticalBlockMetrics(double naturalAscent,
                                                 double naturalDescent,
                                                 SkScalar fontSize,
                                                 SkScalar heightMultiplier,
                                                 bool halfLeading) {
    if (heightMultiplier == 0) {
        return {naturalAscent, naturalDescent, naturalAscent + naturalDescent};
    }

    const SkScalar targetThickness = heightMultiplier * fontSize;
    const double naturalThickness = naturalAscent + naturalDescent;
    if (!SkIsFinite(targetThickness) || targetThickness <= 0 ||
        !std::isfinite(naturalThickness) || naturalThickness <= 0) {
        throw std::invalid_argument("vertical-rl line height override has invalid block metrics");
    }

    // Keep the target thickness and side metrics in double until the column is
    // complete. Rounding each run's sides to float before taking the maxima can
    // lose a tiny target, especially across otherwise identical runs.
    const double target = static_cast<double>(targetThickness);
    const double midpoint = (naturalAscent - naturalDescent) * 0.5;
    const double targetAscent = halfLeading
            ? midpoint + target * 0.5
            : target * (naturalAscent / naturalThickness);
    const double targetDescent = halfLeading
            ? target * 0.5 - midpoint
            : target * (naturalDescent / naturalThickness);
    if (!std::isfinite(targetAscent) || !std::isfinite(targetDescent)) {
        throw std::invalid_argument("vertical-rl line height override produced non-finite block metrics");
    }
    return {targetAscent, targetDescent, targetThickness};
}

VerticalBlockMetrics VerticalBlockMetricsForRun(const Run& run) {
    return ResolveVerticalBlockMetrics(run.blockAscent(), run.blockDescent(),
                                       run.font().getSize(), run.heightMultiplier(),
                                       run.useHalfLeading());
}

const TextStyle* StyleAt(const skia_private::TArray<Block>& styles, TextIndex index) {
    const Block* block = FindBlock(styles, index);
    return block == nullptr ? nullptr : &block->fStyle;
}

ParagraphPainter::SkPaintOrID PaintForStyle(const TextStyle& style) {
    if (style.hasForeground()) {
        return style.getForegroundPaintOrID();
    }
    SkPaint paint;
    paint.setColor(style.getColor());
    return paint;
}

bool GlyphInColumn(const Run& run, size_t glyphIndex, const VerticalColumnGeometry& column) {
    const TextIndex glyphText = run.textRange().start + run.clusterIndex(glyphIndex);
    return glyphText >= column.text.start && glyphText < column.text.end;
}

SkScalar ResolveVerticalTextIndent(const ParagraphStyle& style, SkScalar inlineExtent) {
    const SkScalar value = style.getTextIndent();
    if (!SkIsFinite(value)) {
        throw std::invalid_argument("vertical-rl text indent must be finite");
    }

    if (!style.getTextIndentIsPercentage()) {
        return value;
    }
    if (!SkIsFinite(inlineExtent)) {
        return 0;
    }

    const double resolved = static_cast<double>(value) *
                            static_cast<double>(inlineExtent) / 100.0;
    if (!std::isfinite(resolved) ||
        resolved > static_cast<double>(std::numeric_limits<SkScalar>::max()) ||
        resolved < -static_cast<double>(std::numeric_limits<SkScalar>::max())) {
        throw std::invalid_argument("vertical-rl percentage text indent is not representable");
    }
    return SkDoubleToScalar(resolved);
}

bool VerticalExtentExceeds(SkScalar value, SkScalar limit) {
    const SkScalar difference = value - limit;
    // HarfBuzz advances are quantized independently from the shaped source
    // runs. Keep exact-fit comparisons stable across those rounding paths.
    return difference > 0 && !SkScalarNearlyZero(difference, 0.05f);
}

bool TypefaceSupportsCodepoint(const sk_sp<SkTypeface>& typeface, SkUnichar codepoint) {
    return typeface != nullptr && codepoint >= 0 && typeface->unicharToGlyph(codepoint) != 0;
}

SkUnichar FirstMeaningfulAffixCodepoint(const char* text, size_t start, size_t end) {
    const char* current = text + start;
    const char* limit = text + end;
    SkUnichar first = -1;
    while (current < limit) {
        const SkUnichar codepoint = SkUTF::NextUTF8WithReplacement(&current, limit);
        if (first < 0) {
            first = codepoint;
        }
        if (!u_hasBinaryProperty(codepoint, UCHAR_DEFAULT_IGNORABLE_CODE_POINT)) {
            return codepoint;
        }
    }
    return first;
}

SkFourByteTag AffixScriptTag(SkUnichar codepoint) {
    UErrorCode error = U_ZERO_ERROR;
    const UScriptCode script = uscript_getScript(codepoint, &error);
    if (U_FAILURE(error)) {
        return 0;
    }
    const char* name = uscript_getShortName(script);
    if (name == nullptr) {
        return 0;
    }
    char tag[4] = {' ', ' ', ' ', ' '};
    for (int index = 0; index < 4 && name[index] != '\0'; ++index) {
        tag[index] = name[index];
    }
    return SkSetFourByteTag(tag[0], tag[1], tag[2], tag[3]);
}

sk_sp<SkTypeface> ResolveAffixTypeface(FontCollection* collection,
                                       const TextStyle& style,
                                       const sk_sp<SkTypeface>& preferred,
                                       SkUnichar codepoint) {
    if (TypefaceSupportsCodepoint(preferred, codepoint)) {
        return preferred;
    }
    for (const auto& typeface: collection->findTypefaces(style.getFontFamilies(),
                                                         style.getFontStyle(),
                                                         style.getFontArguments())) {
        if (TypefaceSupportsCodepoint(typeface, codepoint)) {
            return typeface;
        }
    }
    if (!collection->fontFallbackEnabled()) {
        return nullptr;
    }
    sk_sp<SkTypeface> fallback = collection->defaultFallback(codepoint,
                                                             style.getFontFamilies(),
                                                             style.getFontStyle(),
                                                             style.getLocale(),
                                                             style.getFontArguments());
    if (fallback == nullptr && codepoint >= 0 &&
        u_hasBinaryProperty(codepoint, UCHAR_EMOJI)) {
        fallback = collection->defaultEmojiFallback(codepoint,
                                                    style.getFontStyle(),
                                                    style.getLocale());
    }
    return fallback;
}

VerticalAffix ShapeVerticalAffix(FontCollection* collection,
                                 SkUnicode* unicode,
                                 const TextStyle& style,
                                 const SkString& text,
                                 TextIndex insertion,
                                 const sk_sp<SkTypeface>& preferredTypeface) {
    VerticalAffix affix;
    affix.style = style;
    affix.insertion = insertion;
    if (text.isEmpty()) {
        return affix;
    }

    skia_private::TArray<SkUnicode::CodeUnitFlags, true> boundaries;
    std::string affixStorage(text.c_str(), text.size());
    if (unicode == nullptr ||
        !unicode->computeCodeUnitFlags(affixStorage.data(), text.size(), false, &boundaries)) {
        throw std::runtime_error("vertical-rl failed to segment the ellipsis affix");
    }

    const SkString locale = style.getLocale();
    size_t segmentStart = 0;
    while (segmentStart < text.size()) {
        size_t segmentEnd = segmentStart + 1;
        while (segmentEnd < text.size() &&
               !SkUnicode::hasGraphemeStartFlag(boundaries[segmentEnd])) {
            ++segmentEnd;
        }
        bool sideways = IsSidewaysGrapheme(text.c_str(), segmentStart, segmentEnd);
        const SkUnichar codepoint =
                FirstMeaningfulAffixCodepoint(text.c_str(), segmentStart, segmentEnd);
        // The default ellipsis is a typographic upright mark in vertical text.
        if (codepoint == 0x2026) {
            sideways = false;
        }
        const sk_sp<SkTypeface> typeface = ResolveAffixTypeface(collection,
                                                                style,
                                                                preferredTypeface,
                                                                codepoint);
        const SkFourByteTag script = AffixScriptTag(codepoint >= 0 ? codepoint : 0);
        size_t runEnd = segmentEnd;
        while (runEnd < text.size()) {
            size_t nextEnd = runEnd + 1;
            while (nextEnd < text.size() &&
                   !SkUnicode::hasGraphemeStartFlag(boundaries[nextEnd])) {
                ++nextEnd;
            }
            const SkUnichar nextCodepoint =
                    FirstMeaningfulAffixCodepoint(text.c_str(), runEnd, nextEnd);
            const sk_sp<SkTypeface> nextTypeface = ResolveAffixTypeface(collection,
                                                                        style,
                                                                        preferredTypeface,
                                                                        nextCodepoint);
            if (IsSidewaysGrapheme(text.c_str(), runEnd, nextEnd) != sideways ||
                AffixScriptTag(nextCodepoint >= 0 ? nextCodepoint : 0) != script ||
                (nextTypeface == nullptr ? 0 : nextTypeface->uniqueID()) !=
                        (typeface == nullptr ? 0 : typeface->uniqueID())) {
                break;
            }
            runEnd = nextEnd;
        }

        SkFont font(typeface, style.getFontSize());
        font.setEdging(style.getFontEdging());
        font.setHinting(style.getFontHinting());
        font.setSubpixel(style.getSubpixel());
        std::vector<TtbFeature> features;
        for (const auto& feature: style.getFontFeatures()) {
            features.push_back({TagFromName(feature.fName),
                                static_cast<uint32_t>(feature.fValue),
                                0,
                                text.size()});
        }
        TtbShapeRequest request;
        request.utf8 = std::string_view(text.c_str(), text.size());
        request.runStart = segmentStart;
        request.runLength = runEnd - segmentStart;
        request.font = font;
        request.script = script;
        request.language = std::string_view(locale.c_str(), locale.size());
        request.orientation = sideways ? TtbOrientation::kSideways : TtbOrientation::kUpright;
        request.sidewaysDirection = TtbHorizontalDirection::kLeftToRight;
        request.features = SkSpan<const TtbFeature>(features.data(), features.size());

        TtbShapedRun shaped;
        if (!ShapeTtbRun(request, &shaped)) {
            throw std::runtime_error("vertical-rl failed to shape the ellipsis affix");
        }
        const SkScalar advance = sideways ? shaped.advance.fX : shaped.advance.fY;
        if (!SkIsFinite(advance) || advance < 0) {
            throw std::invalid_argument("vertical-rl ellipsis affix has an invalid advance");
        }

        VerticalAffixRun run;
        run.font = font;
        run.sideways = sideways;
        run.inlineOffset = affix.advance;
        run.glyphs.reserve(shaped.glyphs.size());
        run.positions.reserve(shaped.glyphs.size());
        run.offsets.reserve(shaped.glyphs.size());
        for (const auto& glyph: shaped.glyphs) {
            SkPoint position = glyph.position;
            SkPoint offset = glyph.offset;
            if (sideways) {
                RotateClockwise(&position);
                RotateClockwise(&offset);
            }
            run.glyphs.push_back(glyph.glyph);
            run.positions.push_back(position);
            run.offsets.push_back(offset);
        }
        affix.advance += advance;
        if (!SkIsFinite(affix.advance)) {
            throw std::invalid_argument("vertical-rl ellipsis affix advance overflowed");
        }
        affix.runs.push_back(std::move(run));
        segmentStart = runEnd;
    }
    return affix;
}

SkScalar AffixRunBlockOffset(const VerticalAffixRun& run) {
    if (!run.sideways) {
        return 0;
    }
    SkFontMetrics metrics;
    run.font.getMetrics(&metrics);
    if (!SkIsFinite(metrics.fAscent) || !SkIsFinite(metrics.fDescent)) {
        return 0;
    }
    return (metrics.fAscent + metrics.fDescent) * 0.5f;
}

template <typename Visitor>
void ForEachVerticalAffixGlyph(const TextLine& line,
                               const VerticalColumnGeometry& column,
                               Visitor&& visitor) {
    if (!column.ellipsized) {
        return;
    }
    const SkScalar baselineX = line.offset().x() + column.blockDescent -
                              column.affix.style.getBaselineShift();
    const SkScalar inlineOrigin = line.offset().y() - column.inlineSourceStart;
    for (const auto& run: column.affix.runs) {
        const SkScalar runOriginX = baselineX + AffixRunBlockOffset(run);
        const SkScalar runOriginY = inlineOrigin + column.affix.inlineStart +
                                    run.inlineOffset;
        for (size_t index = 0; index < run.glyphs.size(); ++index) {
            const SkPoint position = run.positions[index] + run.offsets[index];
            const SkPoint origin = SkPoint::Make(runOriginX + position.x(),
                                                 runOriginY + position.y());
            visitor(run, index, position, origin);
        }
    }
}

} // namespace

bool ParagraphImpl::validateVerticalConfiguration(SkScalar inlineExtent) {
    if (!IsFiniteNonNegative(inlineExtent) && !IsInfinitePositive(inlineExtent)) {
        throw std::invalid_argument("vertical-rl layout requires a finite non-negative height or +infinity");
    }

    const auto& style = fParagraphStyle;
    if (style.getTextDirection() != TextDirection::kLtr) {
        throw std::invalid_argument("vertical-rl does not support RTL base direction");
    }
    if (style.getTextAlign() != TextAlign::kStart && style.getTextAlign() != TextAlign::kEnd &&
        style.getTextAlign() != TextAlign::kCenter && style.getTextAlign() != TextAlign::kJustify) {
        throw std::invalid_argument("vertical-rl supports only Start, End, Center, and Justify alignment");
    }
    (void)ResolveVerticalTextIndent(style, inlineExtent);
    if (style.getStrutStyle().getStrutEnabled()) {
        throw std::invalid_argument("vertical-rl does not support strut styles");
    }
    if (style.getMaxLines() == 0) {
        throw std::invalid_argument("vertical-rl maxLines must be unlimited or a positive column count");
    }
    if (style.getTextHeightBehavior() != TextHeightBehavior::kAll) {
        throw std::invalid_argument("vertical-rl does not support TextHeightBehavior");
    }
    // ParagraphBuilder always appends one zero-width sentinel placeholder. Only
    // placeholders with source text represent caller-visible placeholders.
    for (const auto& placeholder : fPlaceholders) {
        if (placeholder.fRange.width() == 0) {
            continue;
        }
        const auto& value = placeholder.fStyle;
        if (!SkIsFinite(value.fWidth) || value.fWidth <= 0 ||
            !SkIsFinite(value.fHeight) || value.fHeight <= 0 ||
            value.fAlignment != PlaceholderAlignment::kMiddle ||
            value.fBaseline != TextBaseline::kAlphabetic || value.fBaselineOffset != 0) {
            throw std::invalid_argument("vertical-rl placeholders require positive finite dimensions and Middle/Alphabetic/0 alignment");
        }
    }
    for (const auto& ruby : fRubyAnnotations) {
        if (ruby.style.position != RubyPosition::Above) {
            throw std::invalid_argument("vertical-rl ruby requires Above position");
        }
    }
    auto validateStyle = [](const TextStyle& textStyle) {
        if (!SkIsFinite(textStyle.getFontSize()) || textStyle.getFontSize() <= 0) {
            throw std::invalid_argument("vertical-rl requires a finite positive font size");
        }
        if (!SkIsFinite(textStyle.getBaselineShift())) {
            throw std::invalid_argument("vertical-rl requires a finite baseline shift");
        }
        if (textStyle.getHeightOverride()) {
            const SkScalar height = textStyle.getHeight();
            if (!SkIsFinite(height) || height <= 0) {
                throw std::invalid_argument("vertical-rl requires a finite positive line height override");
            }
            const SkScalar targetThickness = height * textStyle.getFontSize();
            if (!SkIsFinite(targetThickness) || targetThickness <= 0) {
                throw std::invalid_argument("vertical-rl line height override must produce a finite positive column thickness");
            }
        }
        const TextDecoration decorationType = textStyle.getDecorationType();
        if (decorationType != TextDecoration::kNoDecoration) {
            if (!IsSupportedVerticalDecoration(decorationType)) {
                throw std::invalid_argument(
                        "vertical-rl supports only underline and line-through text decorations");
            }
            if (textStyle.getDecorationMode() != TextDecorationMode::kThrough) {
                throw std::invalid_argument(
                        "vertical-rl supports only through text decoration mode");
            }
            if (textStyle.getDecorationStyle() != TextDecorationStyle::kSolid) {
                throw std::invalid_argument(
                        "vertical-rl supports only solid text decoration styles");
            }
            const SkScalar multiplier = textStyle.getDecorationThicknessMultiplier();
            if (!SkIsFinite(multiplier) || multiplier <= 0) {
                throw std::invalid_argument(
                        "vertical-rl requires a finite positive decoration thickness multiplier");
            }
        }
        if (!SkIsFinite(textStyle.getLetterSpacing()) || textStyle.getLetterSpacing() < 0) {
            throw std::invalid_argument("vertical-rl requires finite non-negative letter spacing");
        }
        if (!SkIsFinite(textStyle.getWordSpacing()) || textStyle.getWordSpacing() < 0) {
            throw std::invalid_argument("vertical-rl requires finite non-negative word spacing");
        }
    };

    validateStyle(style.getTextStyle());
    for (const auto& block: fTextStyles) {
        if (block.fStyle.isPlaceholder()) {
            continue;
        }
        validateStyle(block.fStyle);
    }

    if (fText.isEmpty()) {
        return true;
    }
    if (!fUnicode) {
        throw std::runtime_error("vertical-rl requires a Unicode provider");
    }
    if (fState < kIndexed) {
        if (!this->computeCodeUnitProperties()) {
            throw std::runtime_error("vertical-rl failed to compute Unicode properties");
        }
        fState = kIndexed;
    }

    const char* current = fText.c_str();
    const char* end = current + fText.size();
    while (current < end) {
        const SkUnichar codepoint = SkUTF::NextUTF8WithReplacement(&current, end);
        if (IsIgnoredVerticalControl(codepoint)) {
            SkDebugf("vertical-rl: skipping control U+%04X without painting a glyph\n", codepoint);
            continue;
        }
        if (IsStrongRtl(codepoint)) {
            throw std::invalid_argument("vertical-rl does not support strong RTL content");
        }
    }

    for (const auto& block: fTextStyles) {
        if (block.fRange.start == 0 || block.fRange.start >= fText.size()) {
            continue;
        }
        if (!codeUnitHasProperty(block.fRange.start, SkUnicode::CodeUnitFlags::kGraphemeStart)) {
            throw std::invalid_argument("vertical-rl does not support style switches inside a grapheme");
        }
    }
    return true;
}

void ParagraphImpl::validateVerticalOrientationOverrides() {
    if (fOrientationOverrides.empty()) {
        return;
    }
    for (size_t index = 0; index < fOrientationOverrides.size(); ++index) {
        const auto& override = fOrientationOverrides[index];
        if (override.orientation != VerticalTextOrientation::kMixed &&
            override.orientation != VerticalTextOrientation::kUpright &&
            override.orientation != VerticalTextOrientation::kSideways) {
            throw std::invalid_argument("text-orientation override value is invalid");
        }
        if (override.range.start >= override.range.end || override.range.end > fText.size()) {
            throw std::invalid_argument("text-orientation override range is empty or out of bounds");
        }
        if (index > 0 && override.range.start < fOrientationOverrides[index - 1].range.end) {
            throw std::invalid_argument("text-orientation override ranges overlap");
        }
    }

    std::string original(fText.c_str(), fText.size());
    skia_private::TArray<SkUnicode::CodeUnitFlags, true> rawFlags;
    if (!fUnicode->computeCodeUnitFlags(original.data(), original.size(), false, &rawFlags)) {
        throw std::runtime_error("text-orientation override Unicode analysis failed");
    }
    auto isGraphemeStart = [&](TextIndex index) {
        if (index >= rawFlags.size()) {
            return index == fText.size();
        }
        return SkUnicode::hasGraphemeStartFlag(rawFlags[index]);
    };
    for (const auto& override : fOrientationOverrides) {
        if (!isGraphemeStart(override.range.start) || !isGraphemeStart(override.range.end)) {
            throw std::invalid_argument(
                    "text-orientation override is not aligned to grapheme boundaries");
        }
    }
}

bool ParagraphImpl::shapeVerticalRuns() {
    if (!this->isVertical()) {
        return true;
    }

    // HTML treats a tab as collapsible whitespace in normal text flow. This
    // engine has no white-space/tab-size modes, so vertical shaping maps each
    // tab to one space while retaining the original source text and ranges.
    std::string shapedText(fText.c_str(), fText.size());
    if (shapedText.find('\t') != std::string::npos) {
        std::replace(shapedText.begin(), shapedText.end(), '\t', ' ');
    }
    const char* shapingText = shapedText.c_str();

    // Vertical layout is LTR-only. Explicit bidi controls are skipped above, so
    // an odd source bidi level produced by those controls must not leak into
    // the forced vertical shaping runs. Keeping every run LTR preserves logical
    // cluster order for ranges, hit testing, and column breaking.
    skia_private::TArray<Run> verticalRuns;
    size_t unresolvedGlyphs = 0;
    size_t runIndex = 0;
    SkScalar inlineCursor = 0;

    for (size_t sourceIndex = 0; sourceIndex < fRuns.size(); ++sourceIndex) {
        const Run& source = fRuns[sourceIndex];
        const TextRange sourceText = source.textRange();
        if (sourceText.empty()) {
            continue;
        }

        if (source.isPlaceholder()) {
            const auto& placeholder = *source.placeholderStyle();
            const SkShaper::RunHandler::RunInfo info = {
                    source.font(), 0, 0, "", SkPoint::Make(0, placeholder.fHeight), 0,
                    SkShaper::RunHandler::Range(0, sourceText.width())};
            verticalRuns.emplace_back(this, info, sourceText.start, 0, false, 0, runIndex++, 0);
            Run& run = verticalRuns.back();
            run.fVertical = true;
            run.fSideways = false;
            run.fPlaceholderIndex = source.fPlaceholderIndex;
            run.fAdvance = SkVector::Make(0, placeholder.fHeight);
            run.fOffset = SkVector::Make(0, inlineCursor);
            run.allocateGlyphData(0, false);
            run.fGlyphData->positions()[0] = SkPoint::Make(0, 0);
            inlineCursor += placeholder.fHeight;
            continue;
        }

        TextIndex segmentStart = sourceText.start;
        while (segmentStart < sourceText.end) {
            if (!codeUnitHasProperty(segmentStart, SkUnicode::CodeUnitFlags::kGraphemeStart)) {
                throw std::runtime_error("vertical-rl font fallback split a grapheme cluster");
            }
            TextIndex segmentEnd = findNextGraphemeBoundary(segmentStart + 1);
            auto appendZeroGlyphRun = [&](TextIndex zeroStart, TextIndex zeroEnd) {
                const size_t zeroLength = zeroEnd - zeroStart;
                const SkShaper::RunHandler::RunInfo info = {
                        source.font(),
                        0,
                        source.fScript,
                        source.fLanguage.c_str(),
                        SkPoint::Make(0, 0),
                        0,
                        SkShaper::RunHandler::Range(0, zeroLength),
                };
                verticalRuns.emplace_back(this,
                                          info,
                                          zeroStart,
                                          source.heightMultiplier(),
                                          source.useHalfLeading(),
                                          source.baselineShift(),
                                          runIndex,
                                          0);
                Run& run = verticalRuns.back();
                run.fVertical = true;
                run.fSideways = false;
                run.fAdvance = SkVector::Make(0, 0);
                run.fOffset = SkVector::Make(0, inlineCursor);
                run.allocateGlyphData(0, false);
                run.fGlyphData->positions()[0] = SkPoint::Make(0, 0);
                run.fBlockAscent = 0;
                run.fBlockDescent = 0;
                ++runIndex;
            };
            if (codeUnitHasProperty(segmentEnd, SkUnicode::CodeUnitFlags::kHardLineBreakBefore)) {
                // A hard break is its own grapheme. Keep the preceding text in a
                // normal shaped run instead of turning the whole segment into a
                // zero-glyph run when the break shares the preceding orientation.
                // The break carries no glyph and must not widen the column.
                // An empty line still gets the empty-metrics fallback later.
                appendZeroGlyphRun(segmentStart, segmentEnd);
                segmentStart = segmentEnd;
                continue;
            }

            if (IsIgnoredVerticalControlAt(fText, segmentStart)) {
                // Keep the source range and cluster mapping, but do not paint a
                // soft hyphen or explicit bidi control in vertical layout.
                appendZeroGlyphRun(segmentStart, segmentEnd);
                segmentStart = segmentEnd;
                continue;
            }

            const bool sideways = IsSidewaysGrapheme(shapingText, segmentStart, segmentEnd,
                                                     &fOrientationOverrides);
            while (segmentEnd < sourceText.end) {
                const TextIndex next = findNextGraphemeBoundary(segmentEnd + 1);
                if (codeUnitHasProperty(next, SkUnicode::CodeUnitFlags::kHardLineBreakBefore)) {
                    break;
                }
                if (IsSidewaysGrapheme(shapingText, segmentEnd, next,
                                       &fOrientationOverrides) != sideways) {
                    break;
                }
                segmentEnd = next;
            }

            const size_t segmentLength = segmentEnd - segmentStart;
            const Block* block = FindBlock(fTextStyles, segmentStart);
            std::vector<TtbFeature> features;
            if (block != nullptr) {
                for (const auto& feature: block->fStyle.getFontFeatures()) {
                    features.push_back({TagFromName(feature.fName),
                                        static_cast<uint32_t>(feature.fValue),
                                        segmentStart,
                                        segmentEnd});
                }
            }

            TtbShapeRequest request;
            request.utf8 = std::string_view(shapingText, fText.size());
            request.runStart = segmentStart;
            request.runLength = segmentLength;
            request.font = source.font();
            request.script = source.fScript;
            request.language = std::string_view(source.fLanguage.c_str(), source.fLanguage.size());
            request.orientation = sideways ? TtbOrientation::kSideways : TtbOrientation::kUpright;
            request.sidewaysDirection = TtbHorizontalDirection::kLeftToRight;
            request.features = SkSpan<const TtbFeature>(features.data(), features.size());

            TtbShapedRun shaped;
            if (!ShapeTtbRun(request, &shaped)) {
                throw std::runtime_error("vertical-rl failed to shape a resolved run");
            }
            unresolvedGlyphs += shaped.unresolvedGlyphs;

            const SkScalar inlineAdvance = sideways ? shaped.advance.fX : shaped.advance.fY;
            const size_t glyphCount = shaped.glyphs.size();
            const SkShaper::RunHandler::RunInfo info = {
                    source.font(),
                    0,
                    source.fScript,
                    source.fLanguage.c_str(),
                    SkPoint::Make(0, inlineAdvance),
                    glyphCount,
                    SkShaper::RunHandler::Range(0, segmentLength),
            };

            verticalRuns.emplace_back(this,
                                      info,
                                      segmentStart,
                                      source.heightMultiplier(),
                                      source.useHalfLeading(),
                                      source.baselineShift(),
                                      runIndex,
                                      0);
            Run& run = verticalRuns.back();
            run.fVertical = true;
            run.fSideways = sideways;
            run.fVchwEnabled = shaped.vchwEnabled;
            run.fAdvance = SkVector::Make(0, inlineAdvance);
            run.fOffset = SkVector::Make(0, inlineCursor);
            run.allocateGlyphData(glyphCount, true);

            std::vector<SkGlyphID> glyphs;
            glyphs.reserve(glyphCount);
            for (size_t index = 0; index < glyphCount; ++index) {
                const TtbGlyph& glyph = shaped.glyphs[index];
                SkPoint position = glyph.position;
                SkPoint offset = glyph.offset;
                if (sideways) {
                    RotateClockwise(&position);
                    RotateClockwise(&offset);
                }
                run.fGlyphData->glyphs()[index] = glyph.glyph;
                run.fGlyphData->positions()[index] = position;
                run.fGlyphData->offsets()[index] = offset;
                const size_t cluster = glyph.clusterUtf8 >= segmentStart ? glyph.clusterUtf8 - segmentStart : 0;
                run.fGlyphData->clusterIndexes()[index] = SkToU32(std::min(cluster, segmentLength));
                glyphs.push_back(glyph.glyph);
            }
            run.fGlyphData->positions()[glyphCount] = SkPoint::Make(0, inlineAdvance);
            run.fGlyphData->clusterIndexes()[glyphCount] = SkToU32(segmentLength);

            run.fBlockAscent = 0;
            run.fBlockDescent = 0;
            if (sideways) {
                std::vector<SkRect> bounds(glyphCount);
                if (glyphCount > 0) {
                    run.font().getBounds(SkSpan<const SkGlyphID>(glyphs.data(), glyphs.size()),
                                         SkSpan<SkRect>(bounds.data(), bounds.size()),
                                         nullptr);
                }
                // Sideways runs are centered on the same em-box baseline as
                // upright ideographs. The shaper's block offset moves the
                // font's vertical origin onto that baseline; the em box keeps
                // empty or narrowly inked Latin runs from collapsing the column.
                const SkScalar halfEm = run.font().getSize() * 0.5f;
                const SkScalar blockOffset = SidewaysBlockOffset(run);
                run.fBlockAscent = halfEm;
                run.fBlockDescent = halfEm;
                for (size_t index = 0; index < glyphCount; ++index) {
                    const SkRect rotated = RotateClockwiseBounds(bounds[index]);
                    const SkScalar originX =
                            blockOffset + run.fGlyphData->positions()[index].fX + run.fGlyphData->offsets()[index].fX;
                    run.fBlockAscent = std::max(run.fBlockAscent, originX + rotated.fRight);
                    run.fBlockDescent = std::max(run.fBlockDescent, -(originX + rotated.fLeft));
                }
            } else {
                const SkScalar halfEm = run.font().getSize() * 0.5f;
                run.fBlockAscent = halfEm;
                run.fBlockDescent = halfEm;
                std::vector<SkRect> bounds(glyphCount);
                if (glyphCount > 0) {
                    run.font().getBounds(SkSpan<const SkGlyphID>(glyphs.data(), glyphs.size()),
                                         SkSpan<SkRect>(bounds.data(), bounds.size()),
                                         nullptr);
                }
                for (size_t index = 0; index < glyphCount; ++index) {
                    const SkScalar baselineX = run.fGlyphData->positions()[index].fX + run.fGlyphData->offsets()[index].fX;
                    run.fBlockAscent = std::max(run.fBlockAscent, baselineX + bounds[index].fRight);
                    run.fBlockDescent = std::max(run.fBlockDescent, -(baselineX + bounds[index].fLeft));
                }
            }
            run.fBlockAscent = SkIsFinite(run.fBlockAscent) ? run.fBlockAscent : 0;
            run.fBlockDescent = SkIsFinite(run.fBlockDescent) ? run.fBlockDescent : 0;

            inlineCursor += inlineAdvance;
            segmentStart = segmentEnd;
            ++runIndex;
        }
    }

    fRuns = std::move(verticalRuns);
    fUnresolvedGlyphs = unresolvedGlyphs;
    return true;
}

void ParagraphImpl::prepareVerticalRuby() {
    fVerticalRuby.clear();
    std::string original(fText.c_str(), fText.size());
    skia_private::TArray<SkUnicode::CodeUnitFlags, true> rawFlags;
    if (!fUnicode->computeCodeUnitFlags(original.data(), original.size(), false, &rawFlags)) {
        throw std::runtime_error("vertical ruby Unicode analysis failed");
    }
    std::vector<SkScalar> leading(fClusters.size(), 0);
    std::vector<SkScalar> trailing(fClusters.size(), 0);
    auto before = [](RubyAlignment alignment, SkScalar extra) {
        return alignment == RubyAlignment::Start ? 0 :
               alignment == RubyAlignment::End ? extra : extra * 0.5f;
    };
    for (const auto& input : fRubyAnnotations) {
        const auto range = input.baseRange;
        if (!SkUnicode::hasGraphemeStartFlag(rawFlags[range.start]) ||
            !SkUnicode::hasGraphemeStartFlag(rawFlags[range.end])) {
            throw std::invalid_argument("ruby raw-grapheme boundary rejected");
        }
        const auto first = clusterIndex(range.start);
        const auto last = clusterIndex(range.end - 1) + 1;
        if (fClusters[first].textRange().start != range.start ||
            fClusters[last - 1].textRange().end != range.end) {
            throw std::invalid_argument("ruby shaped-cluster boundary rejected");
        }
        SkScalar baseAdvance = 0;
        SkRect baseInk = SkRect::MakeEmpty();
        const auto& firstCluster = fClusters[first];
        const SkScalar sourceStart = firstCluster.run().offset().y() +
                                    firstCluster.run().inlinePosition(firstCluster.startPos());
        for (size_t index = first; index < last; ++index) {
            if (fClusters[index].isHardBreak() || fClusters[index].run().isPlaceholder()) {
                throw std::invalid_argument("ruby range contains placeholder or hard line break");
            }
            const auto& cluster = fClusters[index];
            const auto& run = cluster.run();
            for (size_t glyph = cluster.startPos(); glyph < cluster.endPos(); ++glyph) {
                SkRect bounds = run.font().getBounds(run.fGlyphData->glyphs()[glyph], nullptr);
                if (run.isSideways()) {
                    bounds = RotateClockwiseBounds(bounds);
                }
                bounds.offset(run.fGlyphData->positions()[glyph] + run.fGlyphData->offsets()[glyph] +
                              SkPoint::Make(0, run.offset().y() - sourceStart));
                baseInk.join(bounds);
            }
            baseAdvance += cluster.width();
        }
        const SkScalar safetyBefore = -std::min(SkScalar(0), baseInk.top());
        const SkScalar safetyAfter = std::max(baseAdvance, baseInk.bottom()) - baseAdvance;
        const SkScalar baseExtent = baseAdvance + safetyBefore + safetyAfter;
        fUnicode->forEachCodepoint(original.data() + range.start, range.width(),
                [](SkUnichar codepoint, int, int, int) {
            if (IsBidiControl(codepoint)) {
                throw std::invalid_argument("ruby range contains bidi controls");
            }
        });
        auto annotationText = input.text;
        skia_private::TArray<SkUnicode::CodeUnitFlags, true> annotationFlags;
        if (!fUnicode->computeCodeUnitFlags(annotationText.data(), annotationText.size(), false,
                                           &annotationFlags)) {
            throw std::runtime_error("ruby annotation Unicode analysis failed");
        }
        for (auto flags : annotationFlags) {
            if (SkUnicode::hasHardLineBreakFlag(flags)) {
                throw std::invalid_argument("ruby annotation must be single-line");
            }
        }
        auto textStyle = input.style.textStyle.value_or(*StyleAt(fTextStyles, range.start));
        textStyle.setFontSize(textStyle.getFontSize() * input.style.fontSizeMultiplier);
        textStyle.setBaselineShift(0);
        textStyle.setHeightOverride(false);
        textStyle.setHeight(0);
        auto build = [&](const TextStyle& annotationStyle) {
            auto style = fParagraphStyle;
            style.setTextStyle(annotationStyle);
            style.setTextAlign(TextAlign::kStart);
            style.setTextIndent(0, false);
            style.setMaxLines(std::numeric_limits<size_t>::max());
            style.setEllipsis(SkString());
            style.setEllipsis(std::u16string());
            ParagraphBuilderImpl builder(style, fFontCollection, fUnicode);
            builder.addText(input.text.data(), input.text.size());
            auto paragraph = builder.Build();
            paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
            const auto& metrics = paragraph->getLayoutMetrics();
            SkRect bounds = SkRect::MakeWH(metrics.physicalSize.width(), metrics.longestInlineExtent);
            bounds.join(metrics.inkBounds);
            return std::make_pair(std::move(paragraph), bounds);
        };
        auto [annotation, bounds] = build(textStyle);
        size_t graphemes = 0;
        for (size_t index = 0; index < input.text.size(); ++index) {
            graphemes += SkUnicode::hasGraphemeStartFlag(annotationFlags[index]) ? 1 : 0;
        }
        const bool distribute = input.style.alignment == RubyAlignment::SpaceAround ? graphemes > 0 :
                input.style.alignment == RubyAlignment::SpaceBetween && graphemes > 1;
        if (distribute && bounds.height() < baseExtent) {
            auto trialStyle = textStyle;
            trialStyle.setLetterSpacing(textStyle.getLetterSpacing() + 1);
            auto [trial, trialBounds] = build(trialStyle);
            const SkScalar step = trialBounds.height() - bounds.height();
            if (step > 0) {
                textStyle.setLetterSpacing(textStyle.getLetterSpacing() +
                        (baseExtent - bounds.height()) / step);
                std::tie(annotation, bounds) = build(textStyle);
            }
        }
        const SkScalar extent = input.style.overhang == RubyOverhang::Allow ? baseExtent :
                std::max(baseExtent, bounds.height());
        const SkScalar extra = extent - baseExtent;
        if (input.style.alignment == RubyAlignment::SpaceAround) {
            const SkScalar spacing = extra / static_cast<SkScalar>(last - first);
            for (size_t index = first; index < last; ++index) {
                leading[index] = spacing * 0.5f;
                trailing[index] = spacing * 0.5f;
            }
        } else if (input.style.alignment == RubyAlignment::SpaceBetween && last - first > 1) {
            for (size_t index = first; index + 1 < last; ++index) {
                trailing[index] = extra / static_cast<SkScalar>(last - first - 1);
            }
        } else {
            leading[first] = before(input.style.alignment, extra);
            trailing[last - 1] = extra - leading[first];
        }
        leading[first] += safetyBefore;
        trailing[last - 1] += safetyAfter;
        fVerticalRuby.push_back({input, {first, last}, std::move(annotation), bounds, 0, extent,
                before(input.style.alignment, extent - bounds.height()) - bounds.top(),
                leading[first], baseExtent});
    }
    skia_private::TArray<Run> detached;
    for (const auto& run : fRuns) {
        detached.emplace_back(run, true);
    }
    fRuns = std::move(detached);
    SkScalar cumulative = 0;
    for (auto& run : fRuns) {
        run.fOffset.fY += cumulative;
        SkScalar shift = 0;
        run.iterateThroughClusters([&](Cluster* cluster) {
            const size_t index = cluster - fClusters.begin();
            for (size_t glyph = cluster->startPos(); glyph < cluster->endPos(); ++glyph) {
                run.fGlyphData->positions()[glyph].fY += shift;
                run.fGlyphData->offsets()[glyph].fY += leading[index];
            }
            shift += leading[index] + trailing[index];
            cluster->space(leading[index] + trailing[index]);
        });
        run.fGlyphData->positions()[run.size()].fY += shift;
        run.fAdvance.fY += shift;
        cumulative += shift;
    }
    for (auto& span : fVerticalRuby) {
        const auto& cluster = fClusters[span.clusters.start];
        span.inlineStart = cluster.run().offset().y() +
                           cluster.run().inlinePosition(cluster.startPos());
    }
    fRubyPrepared = true;
}

void ParagraphImpl::prepareVerticalTateChuYoko() {
    fVerticalTateChuYoko.clear();
    if (fTateChuYokoAnnotations.empty()) {
        fTateChuYokoPrepared = true;
        return;
    }

    std::string original(fText.c_str(), fText.size());
    skia_private::TArray<SkUnicode::CodeUnitFlags, true> rawFlags;
    if (!fUnicode->computeCodeUnitFlags(original.data(), original.size(), false, &rawFlags)) {
        throw std::runtime_error("vertical tate-chu-yoko Unicode analysis failed");
    }
    auto isGraphemeStart = [&](TextIndex index) {
        if (index >= rawFlags.size()) {
            return index == fText.size();
        }
        return SkUnicode::hasGraphemeStartFlag(rawFlags[index]);
    };

    std::vector<TateChuYokoAnnotation> annotations = fTateChuYokoAnnotations;
    std::sort(annotations.begin(), annotations.end(),
              [](const TateChuYokoAnnotation& a, const TateChuYokoAnnotation& b) {
        return a.range.start < b.range.start;
    });
    for (size_t index = 1; index < annotations.size(); ++index) {
        if (annotations[index].range.start < annotations[index - 1].range.end) {
            throw std::invalid_argument("tate-chu-yoko ranges overlap or nest");
        }
    }

    auto styleAt = [this](TextIndex index) -> const TextStyle& {
        const TextStyle* style = StyleAt(fTextStyles, index);
        return style == nullptr ? fParagraphStyle.getTextStyle() : *style;
    };
    for (const auto& annotation : annotations) {
        const TextRange range = annotation.range;
        if (range.start >= range.end || range.end > fText.size()) {
            throw std::invalid_argument("tate-chu-yoko range is empty or out of bounds");
        }
        if (!isGraphemeStart(range.start) || !isGraphemeStart(range.end)) {
            throw std::invalid_argument("tate-chu-yoko range is not aligned to grapheme boundaries");
        }

        bool hasContent = false;
        const char* current = fText.c_str() + range.start;
        const char* limit = fText.c_str() + range.end;
        while (current < limit) {
            const SkUnichar codepoint = SkUTF::NextUTF8WithReplacement(&current, limit);
            if (codepoint == 0xFFFC) {
                throw std::invalid_argument("tate-chu-yoko range contains a placeholder");
            }
            if (!IsIgnoredVerticalControl(codepoint)) {
                hasContent = true;
            }
        }
        if (!hasContent) {
            throw std::invalid_argument("tate-chu-yoko range has no visible grapheme");
        }

        const ClusterIndex first = clusterIndex(range.start);
        const ClusterIndex last = clusterIndex(range.end - 1) + 1;
        if (first >= last || fClusters[first].textRange().start != range.start ||
            fClusters[last - 1].textRange().end != range.end) {
            throw std::invalid_argument("tate-chu-yoko range is not aligned to shaped clusters");
        }
        const TextStyle* firstStyle = StyleAt(fTextStyles, range.start);
        if (firstStyle == nullptr) {
            firstStyle = &fParagraphStyle.getTextStyle();
        }
        for (ClusterIndex index = first; index < last; ++index) {
            const Cluster& cluster = fClusters[index];
            if (cluster.isHardBreak()) {
                throw std::invalid_argument("tate-chu-yoko range contains a hard line break");
            }
            const Run* run = cluster.runOrNull();
            if (run == nullptr || run->isPlaceholder()) {
                throw std::invalid_argument("tate-chu-yoko range contains a placeholder");
            }
            const TextStyle* clusterStyle = StyleAt(fTextStyles, cluster.textRange().start);
            if (clusterStyle == nullptr) {
                clusterStyle = &fParagraphStyle.getTextStyle();
            }
            if (!clusterStyle->equals(*firstStyle)) {
                throw std::invalid_argument(
                        "tate-chu-yoko range crosses a resolved text style boundary");
            }
        }
        for (const auto& ruby : fRubyAnnotations) {
            if (range.start < ruby.baseRange.end && ruby.baseRange.start < range.end) {
                throw std::invalid_argument("tate-chu-yoko range intersects a ruby base range");
            }
        }

        bool sidewaysOverride = false;
        for (TextIndex start = range.start; start < range.end;) {
            TextIndex end = start + 1;
            while (end < range.end && !isGraphemeStart(end)) {
                ++end;
            }
            if (OrientationOverrideAt(fOrientationOverrides, start) ==
                VerticalTextOrientation::kSideways) {
                sidewaysOverride = true;
                break;
            }
            start = end;
        }
        if (sidewaysOverride) {
            // text-combine-upright is ignored when text-orientation resolves to
            // the explicit sideways value; mixed keeps its ICU run classification
            // and remains combinable.
            continue;
        }

        auto contentStyle = *firstStyle;
        contentStyle.setBaselineShift(0);
        contentStyle.setDecoration(TextDecoration::kNoDecoration);
        contentStyle.setHeightOverride(false);
        contentStyle.setHeight(0);
        auto contentParagraphStyle = fParagraphStyle;
        contentParagraphStyle.setWritingMode(WritingMode::kHorizontalTb);
        contentParagraphStyle.setTextStyle(contentStyle);
        contentParagraphStyle.setTextDirection(TextDirection::kLtr);
        contentParagraphStyle.setTextAlign(TextAlign::kStart);
        contentParagraphStyle.setTextIndent(0, false);
        contentParagraphStyle.setMaxLines(std::numeric_limits<size_t>::max());
        contentParagraphStyle.setEllipsis(SkString());
        contentParagraphStyle.setEllipsis(std::u16string());
        ParagraphBuilderImpl builder(contentParagraphStyle, fFontCollection, fUnicode);
        builder.addText(original.data() + range.start, range.width());
        auto content = builder.Build();
        content->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());

        const SkScalar naturalWidth = std::max(SkScalar(0), content->getMaxIntrinsicWidth());
        if (!SkIsFinite(naturalWidth)) {
            throw std::runtime_error("tate-chu-yoko content has a non-finite natural width");
        }
        const SkScalar fontSize = firstStyle->getFontSize();
        if (!SkIsFinite(fontSize) || fontSize <= 0) {
            throw std::invalid_argument("tate-chu-yoko requires a finite positive font size");
        }
        SkScalar lineAscent = fontSize * 0.5f;
        SkScalar lineDescent = fontSize * 0.5f;
        std::vector<LineMetrics> lineMetrics;
        content->getLineMetrics(lineMetrics);
        if (!lineMetrics.empty()) {
            const auto& metrics = lineMetrics.front();
            if (std::isfinite(metrics.fAscent) && std::isfinite(metrics.fDescent)) {
                lineAscent = SkDoubleToScalar(metrics.fAscent);
                lineDescent = SkDoubleToScalar(metrics.fDescent);
            }
        }
        if (!SkIsFinite(lineAscent) || !SkIsFinite(lineDescent)) {
            lineAscent = fontSize * 0.5f;
            lineDescent = fontSize * 0.5f;
        }

        VerticalTateChuYokoSpan span;
        span.range = range;
        span.clusters = ClusterRange(first, last);
        span.content = std::move(content);
        span.style = contentStyle;
        span.naturalWidth = naturalWidth;
        span.lineAscent = lineAscent;
        span.lineDescent = lineDescent;
        span.inlineExtent = fontSize;
        span.sizing = annotation.sizing;
        if (annotation.sizing == TateChuYokoSizing::kGrow) {
            span.cellWidth = naturalWidth;
            span.squeeze = 1.0f;
        } else {
            span.cellWidth = std::min(naturalWidth, fontSize);
            span.squeeze = naturalWidth > fontSize ? fontSize / naturalWidth : 1.0f;
        }
        span.blockOffset = -fClusters[first].run().baselineShift();
        span.styleIsDefault = firstStyle->equals(fParagraphStyle.getTextStyle());
        fVerticalTateChuYoko.push_back(std::move(span));
    }

    auto boundaryAdvance = [this](ClusterIndex index) {
        if (index < fClusters.size()) {
            const Cluster& cluster = fClusters[index];
            if (cluster.textRange().width() > 0) {
                if (const Run* run = cluster.runOrNull()) {
                    return run->offset().y() + run->inlinePosition(cluster.startPos());
                }
            }
        }
        if (index > 0 && index <= fClusters.size()) {
            const Cluster& previous = fClusters[index - 1];
            if (previous.textRange().width() > 0) {
                if (const Run* run = previous.runOrNull()) {
                    return run->offset().y() + run->inlinePosition(previous.endPos());
                }
            }
        }
        if (fRuns.empty()) {
            return SkScalar(0);
        }
        const Run& run = fRuns.back();
        return run.offset().y() + run.inlinePosition(run.size());
    };
    std::vector<SkScalar> underlyingExtents(fVerticalTateChuYoko.size(), 0);
    for (size_t index = 0; index < fVerticalTateChuYoko.size(); ++index) {
        const auto& span = fVerticalTateChuYoko[index];
        const SkScalar extent = boundaryAdvance(span.clusters.end) -
                                boundaryAdvance(span.clusters.start);
        if (!SkIsFinite(extent) || extent < 0) {
            throw std::runtime_error("tate-chu-yoko shaped extent is invalid");
        }
        underlyingExtents[index] = extent;
    }

    SkScalar cumulative = 0;
    for (auto& run : fRuns) {
        if (cumulative != 0) {
            run.fOffset.fY += cumulative;
        }
        SkScalar shift = 0;
        run.iterateThroughClusters([&](Cluster* cluster) {
            const ClusterIndex index = SkToSizeT(cluster - fClusters.begin());
            for (size_t glyph = cluster->startPos(); glyph < cluster->endPos(); ++glyph) {
                run.fGlyphData->positions()[glyph].fY += shift;
            }
            for (size_t spanIndex = 0; spanIndex < fVerticalTateChuYoko.size(); ++spanIndex) {
                auto& span = fVerticalTateChuYoko[spanIndex];
                if (span.clusters.start == index) {
                    span.inlineStart = run.offset().y() + run.inlinePosition(cluster->startPos());
                }
                if (span.clusters.end - 1 == index) {
                    shift += span.inlineExtent - underlyingExtents[spanIndex];
                }
            }
        });
        run.fGlyphData->positions()[run.size()].fY += shift;
        run.fAdvance.fY += shift;
        cumulative += shift;
    }
    for (const auto& span : fVerticalTateChuYoko) {
        if (!SkIsFinite(span.inlineStart)) {
            throw std::runtime_error("tate-chu-yoko inline placement is invalid");
        }
    }
    fTateChuYokoPrepared = true;
}

const VerticalTateChuYokoSpan*
ParagraphImpl::verticalTateChuYokoAtCluster(ClusterIndex cluster) const {
    for (const auto& span : fVerticalTateChuYoko) {
        if (cluster >= span.clusters.start && cluster < span.clusters.end) {
            return &span;
        }
    }
    return nullptr;
}

const VerticalTateChuYokoSpan*
ParagraphImpl::verticalTateChuYokoStartingAt(ClusterIndex cluster) const {
    for (const auto& span : fVerticalTateChuYoko) {
        if (span.clusters.start == cluster) {
            return &span;
        }
    }
    return nullptr;
}

std::vector<std::pair<const VerticalTateChuYokoSpan*, SkPoint>>
ParagraphImpl::verticalTateChuYokoPlacements(size_t columnIndex) const {
    std::vector<std::pair<const VerticalTateChuYokoSpan*, SkPoint>> result;
    if (columnIndex >= fVerticalColumns.size() || columnIndex >= fLines.size()) {
        return result;
    }
    const auto& column = fVerticalColumns[columnIndex];
    const auto& line = fLines[columnIndex];
    for (const auto& span : fVerticalTateChuYoko) {
        if (span.clusters.start < column.clusters.start || span.clusters.end > column.clusters.end) {
            continue;
        }
        const SkScalar centerX = line.offset().x() + column.blockDescent + span.blockOffset;
        const SkScalar originX = centerX - span.cellWidth * 0.5f;
        const SkScalar originY = line.offset().y() - column.inlineSourceStart + span.inlineStart +
                                 VerticalClusterShift(column, span.clusters.start) +
                                 (span.inlineExtent - span.lineAscent - span.lineDescent) * 0.5f;
        result.emplace_back(&span, SkPoint::Make(originX, originY));
    }
    return result;
}

std::vector<std::pair<const VerticalRubySpan*, SkPoint>>
ParagraphImpl::verticalRubyPlacements(size_t columnIndex) const {
    std::vector<std::pair<const VerticalRubySpan*, SkPoint>> result;
    const auto& column = fVerticalColumns[columnIndex];
    const auto& line = fLines[columnIndex];
    for (const auto& span : fVerticalRuby) {
        if (span.clusters.start < column.clusters.start || span.clusters.end > column.clusters.end) {
            continue;
        }
        result.emplace_back(&span, SkPoint::Make(line.offset().x() + column.blockDescent + column.rubyBaseAscent +
                span.input.style.gap - span.bounds.left(), line.offset().y() - column.inlineSourceStart +
                span.inlineStart + span.annotationOffset + span.overflowShift +
                VerticalClusterShift(column, span.clusters.start)));
    }
    return result;
}

SkScalar ParagraphImpl::verticalRubyShift(ClusterIndex cluster) const {
    for (const auto& span : fVerticalRuby) {
        if (cluster >= span.clusters.start && cluster < span.clusters.end) {
            return span.overflowShift;
        }
    }
    return 0;
}

void ParagraphImpl::layoutWithInlineExtent(SkScalar inlineExtent) {
    if (!this->isVertical()) {
        this->layout(inlineExtent);
        return;
    }
    try {
        this->layoutVertical(inlineExtent);
    } catch (...) {
        this->invalidateVerticalLayout();
        throw;
    }
}

const ParagraphLayoutMetrics& ParagraphImpl::getLayoutMetrics() const {
    if (this->isVertical()) {
        this->ensureVerticalLayoutReady();
    }
    return fLayoutMetrics;
}

void ParagraphImpl::ensureVerticalLayoutReady() const {
    if (!this->isVertical() || fState < kFormatted || fLayoutMetrics.writingMode != WritingMode::kVerticalRl) {
        throw std::logic_error("vertical-rl paragraph has no valid layout; call layoutWithInlineExtent()");
    }
}

void ParagraphImpl::invalidateVerticalLayout() {
    fLines.clear();
    fVerticalColumns.clear();
    fVerticalRuby.clear();
    fVerticalTateChuYoko.clear();
    fVerticalDecorations.clear();
    fVerticalBackgrounds.clear();
    fLayoutMetrics = ParagraphLayoutMetrics{};
    fWidth = 0;
    fHeight = 0;
    fLongestLine = 0;
    fMaxWidthWithTrailingSpaces = 0;
    fMinIntrinsicWidth = 0;
    fMaxIntrinsicWidth = 0;
    fExceededMaxLines = false;
    if (fState >= kShaped) {
        fRuns.clear();
        fClusters.clear();
        fClustersIndexFromCodeUnit.clear();
    }
    if (fState > kIndexed) {
        fState = kIndexed;
    }
    fOldWidth = 0;
    fOldHeight = 0;
    fOldInlineExtent = 0;
    fTateChuYokoPrepared = false;
}

void ParagraphImpl::layoutVertical(SkScalar inlineExtent) {
    if (!this->validateVerticalConfiguration(inlineExtent)) {
        throw std::invalid_argument("vertical-rl configuration is not supported");
    }
    this->validateVerticalOrientationOverrides();

    const bool sameExtent = fOldInlineExtent == inlineExtent;
    if (fState == kFormatted && sameExtent) {
        // Reuse the existing formatted columns.
    } else if (fState >= kLineBroken) {
        fState = kShaped;
    }

    if (fState < kShaped) {
        fRubyPrepared = false;
        fTateChuYokoPrepared = false;
        if (!fFontCollection->getParagraphCache()->findParagraph(this)) {
            if (fState < kIndexed) {
                if (!this->computeCodeUnitProperties()) {
                    throw std::runtime_error("vertical-rl failed to compute Unicode properties");
                }
                fState = kIndexed;
            }
            fRuns.clear();
            fClusters.clear();
            fClustersIndexFromCodeUnit.clear();
            fClustersIndexFromCodeUnit.push_back_n(fText.size() + 1, EMPTY_INDEX);
            if (!this->shapeTextIntoEndlessLine()) {
                this->resetContext();
                this->computeEmptyMetrics();
                this->fLines.clear();
                this->fVerticalColumns.clear();
                this->fVerticalDecorations.clear();
                this->breakShapedTextIntoVerticalColumns(inlineExtent);
                fState = kFormatted;
                fOldWidth = inlineExtent;
                fOldInlineExtent = inlineExtent;
                fOldHeight = fHeight;
                return;
            }
            fFontCollection->getParagraphCache()->updateParagraph(this);
        }
        fState = kShaped;
    }

    if (fState == kShaped) {
        if (hasRuby() && !fRubyPrepared) {
            this->prepareVerticalRuby();
        }
        if (hasTateChuYoko() && !fTateChuYokoPrepared) {
            this->prepareVerticalTateChuYoko();
        }
        this->resetContext();
        this->computeEmptyMetrics();
        this->fLines.clear();
        this->fVerticalColumns.clear();
        this->fVerticalDecorations.clear();
        this->breakShapedTextIntoVerticalColumns(inlineExtent);
        fState = kLineBroken;
    }

    if (fState == kLineBroken) {
        fState = kFormatted;
    }

    fOldWidth = inlineExtent;
    fOldInlineExtent = inlineExtent;
    fOldHeight = fHeight;
}

void ParagraphImpl::breakShapedTextIntoVerticalColumns(SkScalar maxInlineExtent) {
    fExceededMaxLines = false;
    for (auto& span : fVerticalRuby) {
        span.overflowShift = 0;
    }
    struct Unit {
        ClusterIndex cluster;
        ClusterIndex endCluster;
        SkScalar inlineStart;
        SkScalar inlineEnd;
        bool hardBreak;
        bool softBreak;
        bool whitespace;
    };
    struct TempColumn {
        size_t firstUnit;
        size_t endUnit;
        ClusterRange clusters;
        TextRange text;
        TextRange trimmed;
        bool hardBreak;
        bool softWrapped;
        SkScalar inlineStart;
        SkScalar inlineExtent;
        SkScalar blockAscent;
        SkScalar blockDescent;
        SkScalar blockThickness;
        SkScalar inlineLineExtent = 0;
        bool justified = false;
        size_t justificationOpportunityCount = 0;
        SkScalar justificationStep = 0;
        std::vector<SkScalar> justificationShifts;
        bool ellipsized = false;
        VerticalAffix affix;
        SkScalar rubyGrowth = 0;
        SkScalar rubyBaseAscent = 0;
    };

    std::vector<Unit> units;
    if (fClusters.size() > 1) {
        for (size_t index = 0; index + 1 < fClusters.size(); ++index) {
            Cluster& cluster = fClusters[index];
            Run* run = cluster.runOrNull();
            if (run == nullptr || cluster.textRange().empty()) {
                continue;
            }
            const SkScalar inlineStart = run->offset().fY + run->inlinePosition(cluster.startPos());
            SkScalar inlineEnd = inlineStart + cluster.width();
            size_t endCluster = index + 1;
            const VerticalTateChuYokoSpan* tateChuYoko = verticalTateChuYokoStartingAt(index);
            if (tateChuYoko != nullptr) {
                if (!units.empty()) {
                    units.back().softBreak = true;
                }
                units.push_back({index,
                                 tateChuYoko->clusters.end,
                                 inlineStart,
                                 inlineStart + tateChuYoko->inlineExtent,
                                 false,
                                 true,
                                 false});
                index = tateChuYoko->clusters.end - 1;
                continue;
            }
            for (const auto& span : fVerticalRuby) {
                if (span.clusters.start == index) {
                    endCluster = span.clusters.end;
                    inlineEnd = inlineStart + span.inlineExtent;
                    break;
                }
            }
            if (run->isPlaceholder() && cluster.canBreakBefore() && !units.empty()) {
                units.back().softBreak = true;
            }
            units.push_back({index,
                             endCluster,
                             inlineStart,
                             inlineEnd,
                             cluster.isHardBreak(),
                             fClusters[endCluster - 1].isSoftBreak(),
                             cluster.isWhitespaceBreak()});
            index = endCluster - 1;
        }
    }

    std::vector<TempColumn> columns;
    const bool finiteInline = SkIsFinite(maxInlineExtent);
    const SkScalar firstColumnIndent = ResolveVerticalTextIndent(
            fParagraphStyle, maxInlineExtent);
    const SkScalar firstColumnLimit = finiteInline
            ? std::max(SkScalar(0), maxInlineExtent - firstColumnIndent)
            : maxInlineExtent;
    if (!SkIsFinite(firstColumnIndent) ||
        (finiteInline && !SkIsFinite(firstColumnLimit))) {
        throw std::invalid_argument("vertical-rl text indent produced an invalid first-column extent");
    }
    const TextStyle& emptyTextStyle = fRuns.empty() && !fTextStyles.empty()
            ? fTextStyles.back().fStyle
            : fParagraphStyle.getTextStyle();
    auto emptyColumnMetrics = [this, &emptyTextStyle]() {
        if (emptyTextStyle.getHeightOverride()) {
            const SkScalar thickness = emptyTextStyle.getHeight() * emptyTextStyle.getFontSize();
            if (!SkIsFinite(thickness) || thickness <= 0) {
                throw std::invalid_argument(
                        "vertical-rl empty column line height must produce a finite positive thickness");
            }
            return VerticalBlockMetrics{thickness * 0.5f, thickness * 0.5f, thickness};
        }
        const SkScalar fallback = std::max(fEmptyMetrics.height(), SkScalar(1));
        return VerticalBlockMetrics{fallback * 0.5f, fallback * 0.5f, fallback};
    };
    size_t unitIndex = 0;
    while (unitIndex < units.size()) {
        const size_t start = unitIndex;
        const SkScalar columnLimit = start == 0 ? firstColumnLimit : maxInlineExtent;
        const SkScalar inlineStart = units[start].inlineStart;
        size_t end = start;
        size_t lastSoft = kNoBreak;
        bool hardBreak = false;

        while (end < units.size()) {
            const bool placeholder = fClusters[units[end].cluster].runOrNull()->isPlaceholder();
            if (placeholder && end > start) {
                lastSoft = end - 1;
            }
            const SkScalar extent = units[end].inlineEnd - inlineStart;
            if (units[end].hardBreak) {
                ++end;
                hardBreak = true;
                break;
            }
            if (finiteInline && end > start && extent > columnLimit) {
                if (lastSoft != kNoBreak) {
                    end = lastSoft + 1;
                } else {
                    // No legal break precedes this overflow. Keep the whole
                    // unbreakable segment in one column and let it overflow
                    // inline instead of splitting a normal word at an
                    // arbitrary grapheme/cluster boundary.
                    do {
                        hardBreak = units[end].hardBreak;
                        ++end;
                    } while (end < units.size() && !hardBreak && !units[end - 1].softBreak);
                }
                break;
            }
            if (units[end].softBreak) {
                lastSoft = end;
            }
            ++end;
            if (placeholder && finiteInline && extent > columnLimit) {
                break;
            }
        }

        if (end == start) {
            end = start + 1;
            hardBreak = units[start].hardBreak;
        }

        ClusterRange clusterRange(units[start].cluster, units[end - 1].endCluster);
        TextRange text(fClusters[clusterRange.start].textRange().start,
                       fClusters[clusterRange.end - 1].textRange().end);
        size_t trimmedEnd = clusterRange.end;
        while (trimmedEnd > clusterRange.start && fClusters[trimmedEnd - 1].isWhitespaceBreak()) {
            if (verticalTateChuYokoAtCluster(trimmedEnd - 1) != nullptr) {
                break;
            }
            --trimmedEnd;
        }
        TextRange trimmed = trimmedEnd == clusterRange.start
                                    ? TextRange(text.start, text.start)
                                    : TextRange(text.start, fClusters[trimmedEnd - 1].textRange().end);

        VerticalBlockMetrics blockMetrics{0, 0, 0};
        bool hasBlockMetrics = false;
        bool mergedBlockMetrics = false;
        double largestRunThickness = 0;
        const Run* previousRun = nullptr;
        for (size_t index = clusterRange.start; index < clusterRange.end; ++index) {
            if (fClusters[index].isHardBreak()) {
                continue;
            }
            Run* run = fClusters[index].runOrNull();
            if (run == nullptr || run->isPlaceholder() || run->size() == 0) {
                continue;
            }
            // Multiple clusters from one run share the same metrics. Do not
            // recompute the thickness from the rounded sides for those repeats:
            // at very small HalfLeading targets the two float sides can cancel
            // back to zero even though the run has an explicit positive target.
            if (run == previousRun) {
                continue;
            }
            previousRun = run;
            const VerticalBlockMetrics runMetrics = VerticalBlockMetricsForRun(*run);
            largestRunThickness = std::max(largestRunThickness, runMetrics.thickness);
            if (!hasBlockMetrics) {
                blockMetrics = runMetrics;
                hasBlockMetrics = true;
            } else {
                blockMetrics.ascent = std::max(blockMetrics.ascent, runMetrics.ascent);
                blockMetrics.descent = std::max(blockMetrics.descent, runMetrics.descent);
                mergedBlockMetrics = true;
            }
        }
        if (!hasBlockMetrics) {
            SkScalar width = 0;
            for (size_t index = clusterRange.start; index < clusterRange.end; ++index) {
                const Run* run = fClusters[index].runOrNull();
                if (run != nullptr && run->isPlaceholder()) {
                    width = std::max(width, run->placeholderStyle()->fWidth);
                }
            }
            blockMetrics = width > 0 ? VerticalBlockMetrics{width * 0.5, width * 0.5, width}
                                     : emptyColumnMetrics();
        } else if (mergedBlockMetrics) {
            double thickness = blockMetrics.ascent + blockMetrics.descent;
            if (!std::isfinite(thickness) ||
                thickness > std::numeric_limits<SkScalar>::max() ||
                thickness < -std::numeric_limits<SkScalar>::max()) {
                throw std::invalid_argument("vertical-rl merged column block thickness overflowed");
            }
            if (thickness < largestRunThickness) {
                // max(R) + max(L) is mathematically at least every run's own
                // R + L. A smaller finite result is double-rounding loss, so
                // retain the positive target instead of shrinking the column.
                thickness = largestRunThickness;
            }
            blockMetrics.thickness = thickness;
        }
        const SkScalar blockAscent = SkDoubleToScalar(blockMetrics.ascent);
        const SkScalar blockDescent = SkDoubleToScalar(blockMetrics.descent);
        const SkScalar blockThickness = SkDoubleToScalar(blockMetrics.thickness);
        if (!SkIsFinite(blockAscent) || !SkIsFinite(blockDescent) ||
            !SkIsFinite(blockThickness) || blockThickness <= 0) {
            throw std::invalid_argument("vertical-rl column has invalid block metrics");
        }

        const SkScalar inlineExtent = units[end - 1].inlineEnd - inlineStart;
        const bool softWrapped = !hardBreak && end < units.size();
        columns.push_back({start,
                           end,
                           clusterRange,
                           text,
                           trimmed,
                           hardBreak,
                           softWrapped,
                           inlineStart,
                           inlineExtent,
                           blockAscent,
                           blockDescent,
                           blockThickness});
        for (const auto& span : fVerticalRuby) {
            if (span.clusters.start >= clusterRange.start && span.clusters.end <= clusterRange.end) {
                columns.back().rubyGrowth = std::max(columns.back().rubyGrowth,
                        span.input.style.gap + span.bounds.width());
            }
        }
        unitIndex = end;
    }

    if (units.empty()) {
        const VerticalBlockMetrics emptyMetrics = emptyColumnMetrics();
        columns.push_back({0,
                           0,
                           ClusterRange(0, 0),
                           TextRange(0, 0),
                           TextRange(0, 0),
                           false,
                           false,
                           0,
                           0,
                           SkDoubleToScalar(emptyMetrics.ascent),
                           SkDoubleToScalar(emptyMetrics.descent),
                           SkDoubleToScalar(emptyMetrics.thickness)});
    } else if (units.back().hardBreak) {
        const TextIndex end = fText.size();
        const VerticalBlockMetrics emptyMetrics = emptyColumnMetrics();
        columns.push_back({units.size(),
                           units.size(),
                           ClusterRange(fClusters.size() - 1, fClusters.size() - 1),
                           TextRange(end, end),
                           TextRange(end, end),
                           false,
                           false,
                           units.back().inlineEnd,
                           0,
                           SkDoubleToScalar(emptyMetrics.ascent),
                           SkDoubleToScalar(emptyMetrics.descent),
                           SkDoubleToScalar(emptyMetrics.thickness)});
    }

    const size_t maxColumns = fParagraphStyle.getMaxLines();
    const bool hiddenByMaxLines = columns.size() > maxColumns;
    if (hiddenByMaxLines) {
        fExceededMaxLines = true;
        columns.resize(maxColumns);
    }

    const bool finiteMaxLines = maxColumns != std::numeric_limits<size_t>::max();
    const SkString ellipsisText = this->getEllipsis();
    if (finiteInline && finiteMaxLines && !ellipsisText.isEmpty() && !columns.empty()) {
        TempColumn& column = columns.back();
        const SkScalar columnLimit = columns.size() == 1 ? firstColumnLimit : maxInlineExtent;
        const bool inlineOverflow = VerticalExtentExceeds(column.inlineExtent, columnLimit);
        if (hiddenByMaxLines || inlineOverflow) {
            const ClusterIndex originalClusterEnd = column.clusters.end;
            ClusterIndex insertionCluster = column.clusters.end;
            while (insertionCluster > column.clusters.start &&
                   (fClusters[insertionCluster - 1].isWhitespaceBreak() ||
                    fClusters[insertionCluster - 1].isHardBreak())) {
                --insertionCluster;
            }
            for (const auto& span : fVerticalRuby) {
                if (span.clusters.start < insertionCluster && insertionCluster < span.clusters.end) {
                    insertionCluster = span.clusters.start;
                    break;
                }
            }
            for (const auto& span : fVerticalTateChuYoko) {
                if (span.clusters.start < insertionCluster && insertionCluster < span.clusters.end) {
                    insertionCluster = span.clusters.start;
                    break;
                }
            }
            auto boundaryAdvance = [&](ClusterIndex firstExcluded) {
                if (firstExcluded >= column.clusters.end) {
                    return column.inlineExtent;
                }
                const Cluster& cluster = fClusters[firstExcluded];
                const Run* run = cluster.runOrNull();
                if (run == nullptr) {
                    return column.inlineExtent;
                }
                return run->offset().y() + run->inlinePosition(cluster.startPos()) -
                       column.inlineStart;
            };
            SkScalar retainedAdvance = boundaryAdvance(insertionCluster);
            auto resolveAffixStyle = [&](ClusterIndex styleCluster) {
                const TextStyle* style = nullptr;
                if (column.firstUnit != column.endUnit) {
                    const TextIndex styleIndex = styleCluster < column.clusters.end
                            ? fClusters[styleCluster].textRange().start
                            : column.text.end;
                    style = StyleAt(fTextStyles, styleIndex);
                }
                if (style == nullptr) {
                    style = &fParagraphStyle.getTextStyle();
                }
                return style;
            };
            auto resolveAffixTypeface = [&](ClusterIndex styleCluster) {
                sk_sp<SkTypeface> typeface;
                if (column.firstUnit != column.endUnit) {
                    const ClusterIndex styledCluster = styleCluster < column.clusters.end
                            ? styleCluster
                            : column.clusters.end - 1;
                    const Run* styledRun = fClusters[styledCluster].runOrNull();
                    if (styledRun != nullptr) {
                        typeface = styledRun->font().refTypeface();
                    }
                }
                return typeface;
            };
            VerticalAffix affix;
            for (;;) {
                const ClusterIndex styledCluster = insertionCluster;
                affix = ShapeVerticalAffix(fFontCollection.get(),
                                           fUnicode.get(),
                                           *resolveAffixStyle(styledCluster),
                                           ellipsisText,
                                           styledCluster < column.clusters.end
                                                   ? fClusters[styledCluster].textRange().start
                                                   : column.text.end,
                                           resolveAffixTypeface(styledCluster));
                while (insertionCluster > column.clusters.start &&
                       VerticalExtentExceeds(retainedAdvance + affix.advance, columnLimit)) {
                    --insertionCluster;
                    for (const auto& span : fVerticalRuby) {
                        if (span.clusters.start < insertionCluster && insertionCluster < span.clusters.end) {
                            insertionCluster = span.clusters.start;
                            break;
                        }
                    }
                    for (const auto& span : fVerticalTateChuYoko) {
                        if (span.clusters.start < insertionCluster && insertionCluster < span.clusters.end) {
                            insertionCluster = span.clusters.start;
                            break;
                        }
                    }
                    retainedAdvance = boundaryAdvance(insertionCluster);
                }
                if (insertionCluster == styledCluster) {
                    break;
                }
            }
            const bool affixFits = !VerticalExtentExceeds(affix.advance, columnLimit);
            if (!affixFits) {
                insertionCluster = column.clusters.start;
                retainedAdvance = 0;
                affix.advance = 0;
                affix.runs.clear();
            }
            const TextIndex insertion = insertionCluster < column.clusters.end
                    ? fClusters[insertionCluster].textRange().start
                    : column.text.end;
            affix.inlineStart = column.inlineStart + retainedAdvance;
            affix.insertion = insertion;
            column.clusters = ClusterRange(column.clusters.start, insertionCluster);
            column.text = TextRange(column.text.start, insertion);
            column.trimmed = column.text;
            column.inlineExtent = retainedAdvance;
            column.inlineLineExtent = retainedAdvance + affix.advance;
            column.ellipsized = true;
            column.affix = std::move(affix);
            if (insertionCluster < originalClusterEnd || hiddenByMaxLines) {
                fExceededMaxLines = true;
            }
        }
    }

    auto classifyCluster = [this](ClusterIndex clusterIndex) {
        if (verticalTateChuYokoStartingAt(clusterIndex) != nullptr) {
            return VerticalOpportunityKind::Asian;
        }
        const Cluster& cluster = fClusters[clusterIndex];
        if (cluster.isWhitespaceBreak() || cluster.isHardBreak() || cluster.textRange().empty()) {
            return VerticalOpportunityKind::Neutral;
        }
        const char* current = fText.c_str() + cluster.textRange().start;
        const char* end = fText.c_str() + cluster.textRange().end;
        return ClassifyVerticalOpportunity(SkUTF::NextUTF8WithReplacement(&current, end));
    };
    const bool justify = fParagraphStyle.getTextAlign() == TextAlign::kJustify;
    for (size_t index = 0; index < columns.size(); ++index) {
        TempColumn& column = columns[index];
        if (column.ellipsized) {
            continue;
        }
        column.inlineLineExtent = column.inlineExtent;
        if (!justify || !finiteInline || !column.softWrapped) {
            continue;
        }

        const SkScalar columnLimit = index == 0 ? firstColumnLimit : maxInlineExtent;
        const SkScalar freeSpace = columnLimit - column.inlineExtent;
        if (!(freeSpace > 0)) {
            continue;
        }

        ClusterIndex trimmedEnd = column.clusters.end;
        while (trimmedEnd > column.clusters.start &&
               fClusters[trimmedEnd - 1].isWhitespaceBreak()) {
            --trimmedEnd;
        }
        if (trimmedEnd <= column.clusters.start) {
            continue;
        }

        // Opportunities are recorded at the last cluster before the space is
        // inserted. A whitespace run therefore expands its own cell, while an
        // Asian/Latin boundary expands the preceding unit cell. Both keep the
        // next cluster contiguous with the expanded end.
        std::vector<ClusterIndex> opportunities;
        bool hasPrevious = false;
        VerticalOpportunityKind previous = VerticalOpportunityKind::Neutral;
        for (ClusterIndex clusterIndex = column.clusters.start;
             clusterIndex < trimmedEnd;) {
            const Cluster& cluster = fClusters[clusterIndex];
            if (cluster.isHardBreak()) {
                hasPrevious = false;
                ++clusterIndex;
                continue;
            }
            if (cluster.isWhitespaceBreak()) {
                ClusterIndex whitespaceEnd = clusterIndex;
                while (whitespaceEnd < trimmedEnd &&
                       fClusters[whitespaceEnd].isWhitespaceBreak() &&
                       !fClusters[whitespaceEnd].isHardBreak()) {
                    ++whitespaceEnd;
                }
                // PRD 6.2: any breakable whitespace run between two
                // non-whitespace content units is one opportunity, regardless
                // of the units' scripts. A space before a hard break is
                // trailing whitespace of this column, not an opportunity.
                if (hasPrevious && whitespaceEnd < trimmedEnd &&
                    !fClusters[whitespaceEnd].isHardBreak()) {
                    opportunities.push_back(whitespaceEnd - 1);
                }
                hasPrevious = false;
                clusterIndex = whitespaceEnd;
                continue;
            }

            const VerticalOpportunityKind kind = classifyCluster(clusterIndex);
            if (hasPrevious && IsWordLike(previous) && IsWordLike(kind) &&
                (previous == VerticalOpportunityKind::Asian ||
                 kind == VerticalOpportunityKind::Asian)) {
                opportunities.push_back(clusterIndex - 1);
            }
            previous = kind;
            hasPrevious = true;
            const auto ruby = std::find_if(fVerticalRuby.begin(), fVerticalRuby.end(),
                    [&](const auto& span) { return span.clusters.start == clusterIndex; });
            const auto tateChuYoko = verticalTateChuYokoStartingAt(clusterIndex);
            clusterIndex = tateChuYoko != nullptr
                    ? tateChuYoko->clusters.end
                    : (ruby == fVerticalRuby.end() ? clusterIndex + 1 : ruby->clusters.end);
        }

        if (opportunities.empty()) {
            continue;
        }
        const double step = static_cast<double>(freeSpace) /
                            static_cast<double>(opportunities.size());
        if (!std::isfinite(step)) {
            throw std::invalid_argument("vertical-rl justification step is not finite");
        }

        const size_t clusterCount = column.clusters.width();
        std::vector<SkScalar> shifts(clusterCount + 1, 0);
        double cumulative = 0;
        size_t nextOpportunity = 0;
        for (size_t offset = 0; offset < clusterCount; ++offset) {
            shifts[offset] = SkDoubleToScalar(cumulative);
            const ClusterIndex clusterIndex = column.clusters.start + offset;
            if (nextOpportunity < opportunities.size() &&
                opportunities[nextOpportunity] == clusterIndex) {
                cumulative += step;
                ++nextOpportunity;
            }
        }
        if (!std::isfinite(cumulative) || nextOpportunity != opportunities.size()) {
            throw std::invalid_argument("vertical-rl justification produced an invalid shift");
        }
        // Use the exact free-space value at the final boundary so accumulated
        // double rounding cannot move the visible column end.
        shifts[clusterCount] = freeSpace;
        column.inlineLineExtent = columnLimit;
        column.justified = true;
        column.justificationOpportunityCount = opportunities.size();
        column.justificationStep = SkDoubleToScalar(step);
        column.justificationShifts = std::move(shifts);
    }

    for (auto& column: columns) {
        double ascent = column.blockAscent;
        double descent = column.blockDescent;
        double naturalAscent = 0;
        double naturalDescent = 0;
        auto mergeNatural = [&](double runAscent, double runDescent, SkScalar shift) {
            naturalAscent = std::max(naturalAscent, runAscent - std::min(shift, SkScalar(0)));
            naturalDescent = std::max(naturalDescent, runDescent + std::max(shift, SkScalar(0)));
        };
        bool shifted = false;
        auto merge = [&](const VerticalBlockMetrics& metrics, SkScalar shift) {
            if (shift == 0) {
                return;
            }
            const double shiftedAscent = metrics.ascent - shift;
            const double shiftedDescent = metrics.descent + shift;
            if (!SkIsFinite(shift) || !std::isfinite(shiftedAscent) ||
                !std::isfinite(shiftedDescent) ||
                std::abs(shiftedAscent) > std::numeric_limits<SkScalar>::max() ||
                std::abs(shiftedDescent) > std::numeric_limits<SkScalar>::max()) {
                throw std::invalid_argument("vertical-rl baseline shift overflowed block metrics");
            }
            ascent = std::max(ascent, shiftedAscent);
            descent = std::max(descent, shiftedDescent);
            shifted = true;
        };
        const Run* previousRun = nullptr;
        for (ClusterIndex index = column.clusters.start; index < column.clusters.end; ++index) {
            const Cluster& cluster = fClusters[index];
            const Run* run = cluster.runOrNull();
            if (cluster.isHardBreak() || cluster.textRange().empty() ||
                run == nullptr || run->isPlaceholder() || run->size() == 0 || run == previousRun) {
                continue;
            }
            previousRun = run;
            mergeNatural(run->blockAscent(), run->blockDescent(), run->baselineShift());
            merge(VerticalBlockMetricsForRun(*run), run->baselineShift());
        }
        if (column.affix.style.getBaselineShift() != 0 || column.rubyGrowth > 0) {
            for (const auto& run: column.affix.runs) {
                double runAscent = run.font.getSize() * 0.5;
                double runDescent = runAscent;
                for (size_t index = 0; index < run.glyphs.size(); ++index) {
                    SkRect bounds = run.font.getBounds(run.glyphs[index], nullptr);
                    if (run.sideways) {
                        bounds = RotateClockwiseBounds(bounds);
                    }
                    const double origin = AffixRunBlockOffset(run) +
                                          run.positions[index].x() + run.offsets[index].x();
                    runAscent = std::max(runAscent, origin + bounds.right());
                    runDescent = std::max(runDescent, -(origin + bounds.left()));
                }
                const auto& style = column.affix.style;
                mergeNatural(runAscent, runDescent, style.getBaselineShift());
                merge(ResolveVerticalBlockMetrics(runAscent, runDescent, run.font.getSize(),
                                                  style.getHeightOverride() ? style.getHeight() : 0,
                                                  style.getHalfLeading()),
                      style.getBaselineShift());
            }
        }
        if (shifted) {
            const double thickness = ascent + descent;
            if (!std::isfinite(thickness) || thickness <= 0 ||
                thickness > std::numeric_limits<SkScalar>::max()) {
                throw std::invalid_argument("vertical-rl baseline shift produced invalid column thickness");
            }
            column.blockAscent = SkDoubleToScalar(ascent);
            column.blockDescent = SkDoubleToScalar(descent);
            column.blockThickness = SkDoubleToScalar(thickness);
        }
        SkScalar placeholderWidth = 0;
        for (ClusterIndex index = column.clusters.start; index < column.clusters.end; ++index) {
            const Run* run = fClusters[index].runOrNull();
            if (run != nullptr && run->isPlaceholder()) {
                placeholderWidth = std::max(placeholderWidth, run->placeholderStyle()->fWidth);
            }
        }
        if (placeholderWidth > column.blockThickness) {
            const SkScalar growth = (placeholderWidth - column.blockThickness) * 0.5f;
            column.blockAscent += growth;
            column.blockDescent += growth;
            column.blockThickness = placeholderWidth;
        }
        SkScalar tateChuYokoWidth = 0;
        for (const auto& span : fVerticalTateChuYoko) {
            if (span.clusters.start >= column.clusters.start && span.clusters.end <= column.clusters.end) {
                tateChuYokoWidth = std::max(tateChuYokoWidth, span.cellWidth);
            }
        }
        if (tateChuYokoWidth > column.blockThickness) {
            const SkScalar growth = (tateChuYokoWidth - column.blockThickness) * 0.5f;
            column.blockAscent += growth;
            column.blockDescent += growth;
            column.blockThickness = tateChuYokoWidth;
        }
        if (column.rubyGrowth > 0) {
            if (placeholderWidth > naturalAscent + naturalDescent) {
                naturalAscent += (placeholderWidth - naturalAscent - naturalDescent) * 0.5;
            }
            column.rubyBaseAscent = SkDoubleToScalar(naturalAscent);
            column.rubyGrowth = std::max(SkScalar(0),
                    column.rubyBaseAscent + column.rubyGrowth - column.blockAscent);
            column.blockAscent += column.rubyGrowth;
            column.blockThickness += column.rubyGrowth;
        }
    }

    SkScalar totalThickness = 0;
    SkScalar longestInline = 0;
    for (size_t index = 0; index < columns.size(); ++index) {
        const auto& column = columns[index];
        const SkScalar thickness = column.blockThickness;
        if (!SkIsFinite(thickness) || thickness <= 0) {
            throw std::invalid_argument("vertical-rl column thickness must be finite and positive");
        }
        totalThickness += thickness;
        if (!SkIsFinite(totalThickness)) {
            throw std::invalid_argument("vertical-rl physical width overflowed");
        }
        SkScalar inlineContribution = column.inlineLineExtent;
        if (index == 0 && fText.size() != 0) {
            inlineContribution += firstColumnIndent;
        }
        if (!SkIsFinite(inlineContribution)) {
            throw std::invalid_argument("vertical-rl inline extent overflowed");
        }
        longestInline = std::max(longestInline, std::max(SkScalar(0), inlineContribution));
    }

    SkScalar left = totalThickness;
    this->ensureUTF16Mapping();
    fLayoutMetrics = ParagraphLayoutMetrics{};
    fLayoutMetrics.writingMode = WritingMode::kVerticalRl;
    fLayoutMetrics.longestInlineExtent = longestInline;
    fLayoutMetrics.physicalSize = SkSize::Make(totalThickness, finiteInline ? maxInlineExtent : longestInline);

    SkRect contentBounds = SkRect::MakeEmpty();
    bool hasContent = false;
    for (size_t index = 0; index < columns.size(); ++index) {
        const TempColumn& column = columns[index];
        const SkScalar thickness = column.blockThickness;
        left -= thickness;
        SkScalar alignmentShift = 0;
        const SkScalar columnIndent = index == 0 ? firstColumnIndent : 0;
        const SkScalar columnLimit = index == 0 ? firstColumnLimit : maxInlineExtent;
        if (column.ellipsized) {
            alignmentShift = columnIndent;
        } else if (finiteInline) {
            const SkScalar extra = std::max(SkScalar(0), columnLimit - column.inlineExtent);
            switch (fParagraphStyle.getTextAlign()) {
                case TextAlign::kStart:
                    alignmentShift = columnIndent;
                    break;
                case TextAlign::kEnd:
                    alignmentShift = columnIndent + extra;
                    break;
                case TextAlign::kCenter:
                    alignmentShift = columnIndent + extra * 0.5f;
                    break;
                case TextAlign::kJustify:
                    alignmentShift = columnIndent;
                    break;
                default:
                    break;
            }
        } else {
            alignmentShift = columnIndent;
        }

        InternalLineMetrics sizes(-column.blockAscent, column.blockDescent, 0);
        this->addLine(SkPoint::Make(left, alignmentShift),
                      SkVector::Make(thickness, column.inlineLineExtent),
                      column.trimmed,
                      column.text,
                      column.text,
                      column.clusters,
                      column.clusters,
                      column.inlineLineExtent,
                      sizes);
        fVerticalColumns.push_back({column.inlineStart,
                                    column.blockAscent,
                                    column.blockDescent,
                                    column.hardBreak,
                                    column.clusters,
                                    column.text});
        VerticalColumnGeometry& geometry = fVerticalColumns.back();
        geometry.inlineContentExtent = column.inlineExtent;
        geometry.inlineExtent = column.inlineLineExtent;
        geometry.justified = column.justified;
        geometry.justificationOpportunityCount = column.justificationOpportunityCount;
        geometry.justificationStep = column.justificationStep;
        geometry.justificationShifts = column.justificationShifts;
        geometry.ellipsized = column.ellipsized;
        geometry.affix = column.affix;
        geometry.rubyGrowth = column.rubyGrowth;
        geometry.rubyBaseAscent = column.rubyBaseAscent;

        ParagraphColumnMetrics metrics;
        metrics.lineNumber = index;
        metrics.hardBreak = column.hardBreak;
        metrics.utf8Range = column.text;
        metrics.utf16Start = fUTF16IndexForUTF8Index[column.text.start];
        metrics.utf16End = fUTF16IndexForUTF8Index[column.text.end];
        metrics.utf16EndExcludingWhitespace = fUTF16IndexForUTF8Index[column.trimmed.end];
        metrics.utf16EndIncludingNewline = metrics.utf16End;
        metrics.inlineExtent = column.inlineLineExtent;
        metrics.blockThickness = thickness;
        metrics.physicalBaseline = left + column.blockDescent;
        metrics.physicalBounds =
                SkRect::MakeLTRB(left, alignmentShift, left + thickness,
                                 alignmentShift + column.inlineLineExtent);
        fLayoutMetrics.columns.push_back(metrics);
        if (column.inlineLineExtent > 0) {
            if (!hasContent) {
                contentBounds = metrics.physicalBounds;
                hasContent = true;
            } else {
                contentBounds.join(metrics.physicalBounds);
            }
        }
    }

    fWidth = totalThickness;
    fHeight = finiteInline ? maxInlineExtent : longestInline;
    fLongestLine = longestInline;
    fMaxWidthWithTrailingSpaces = longestInline;
    fMaxIntrinsicWidth = 0;
    fMinIntrinsicWidth = 0;
    SkScalar hardRun = 0;
    SkScalar unbreakableRun = 0;
    const SkScalar intrinsicIndent = fParagraphStyle.getTextIndentIsPercentage()
            ? 0
            : firstColumnIndent;
    bool firstHardFragment = true;
    for (const auto& unit: units) {
        const SkScalar width = unit.inlineEnd - unit.inlineStart;
        if (firstHardFragment) {
            hardRun += intrinsicIndent;
            unbreakableRun += intrinsicIndent;
            firstHardFragment = false;
        }
        hardRun += width;
        unbreakableRun += width;
        if (unit.softBreak && !unit.hardBreak) {
            fMinIntrinsicWidth = std::max(fMinIntrinsicWidth, std::max(SkScalar(0), unbreakableRun));
            unbreakableRun = 0;
        }
        if (unit.hardBreak) {
            fMinIntrinsicWidth = std::max(fMinIntrinsicWidth, std::max(SkScalar(0), unbreakableRun));
            unbreakableRun = 0;
            fMaxIntrinsicWidth = std::max(fMaxIntrinsicWidth, std::max(SkScalar(0), hardRun));
            hardRun = 0;
        }
    }
    fMaxIntrinsicWidth = std::max(fMaxIntrinsicWidth, std::max(SkScalar(0), hardRun));
    fMinIntrinsicWidth = std::max(fMinIntrinsicWidth, std::max(SkScalar(0), unbreakableRun));
    fMaxIntrinsicWidth = std::max(fMaxIntrinsicWidth, fMinIntrinsicWidth);
    fLayoutMetrics.minIntrinsicInlineExtent = fMinIntrinsicWidth;
    fLayoutMetrics.maxIntrinsicInlineExtent = fMaxIntrinsicWidth;
    fLayoutMetrics.contentBounds = hasContent ? contentBounds : SkRect::MakeEmpty();
    if (finiteInline) {
        for (auto& span : fVerticalRuby) {
            const int columnIndex = getVerticalColumnForCluster(span.clusters.start);
            if (columnIndex < 0 || span.input.style.overflow != RubyOverflow::PreserveBase) {
                continue;
            }
            const SkScalar limit = columnIndex == 0 ? firstColumnLimit : maxInlineExtent;
            if (span.inlineExtent <= limit) {
                continue;
            }
            const auto& column = fVerticalColumns[columnIndex];
            const SkScalar cellStart = span.inlineStart - column.inlineSourceStart +
                                      VerticalClusterShift(column, span.clusters.start);
            span.overflowShift = std::max(cellStart, (limit - span.baseExtent) * 0.5f) -
                                 cellStart - span.baseOffset;
        }
    }
    this->resolveVerticalMetrics();
}

void ParagraphImpl::resolveVerticalDecorations() {
    fVerticalDecorations.clear();
    struct PendingSegment {
        bool active = false;
        VerticalDecorationSegment segment;
    };

    auto flush = [this](PendingSegment* pending) {
        if (!pending->active) {
            return;
        }
        fVerticalDecorations.push_back(pending->segment);
        *pending = PendingSegment{};
    };
    auto append = [&flush](PendingSegment* pending,
                           size_t columnIndex,
                           TextDecoration decoration,
                           const SkRect& rect,
                           SkColor color,
                           SkScalar thickness) {
        const bool samePaint = pending->active &&
                pending->segment.column == columnIndex &&
                pending->segment.decoration == decoration &&
                pending->segment.color == color &&
                pending->segment.thickness == thickness &&
                pending->segment.rect.left() == rect.left() &&
                pending->segment.rect.right() == rect.right();
        if (pending->active && !samePaint) {
            flush(pending);
        }
        if (!(rect.top() < rect.bottom())) {
            return;
        }
        if (!pending->active) {
            pending->active = true;
            pending->segment.column = columnIndex;
            pending->segment.decoration = decoration;
            pending->segment.rect = rect;
            pending->segment.color = color;
            pending->segment.thickness = thickness;
            return;
        }
        if (rect.top() <= pending->segment.rect.bottom() + 0.0001f) {
            pending->segment.rect.setLTRB(pending->segment.rect.left(),
                                          pending->segment.rect.top(),
                                          pending->segment.rect.right(),
                                          std::max(pending->segment.rect.bottom(), rect.bottom()));
            return;
        }
        flush(pending);
        pending->active = true;
        pending->segment.column = columnIndex;
        pending->segment.decoration = decoration;
        pending->segment.rect = rect;
        pending->segment.color = color;
        pending->segment.thickness = thickness;
    };

    for (size_t columnIndex = 0;
         columnIndex < fLines.size() && columnIndex < fVerticalColumns.size();
         ++columnIndex) {
        const TextLine& line = fLines[columnIndex];
        const VerticalColumnGeometry& column = fVerticalColumns[columnIndex];
        const SkScalar blockCenter = line.offset().x() + column.blockDescent;
        const SkScalar inlineOrigin = line.offset().y() - column.inlineSourceStart;
        const TextIndex trimmedEnd = line.trimmedText().end;
        PendingSegment underline;
        PendingSegment lineThrough;
        SkScalar underlineCenter = 0;
        bool hasUnderlineCenter = false;

        for (ClusterIndex index = column.clusters.start; index < column.clusters.end; ++index) {
            const Cluster& cluster = fClusters[index];
            const Run* run = cluster.runOrNull();
            if (cluster.isHardBreak() || (run != nullptr && run->isPlaceholder()) ||
                cluster.textRange().start >= trimmedEnd) {
                flush(&underline);
                flush(&lineThrough);
                continue;
            }
            const auto* tateChuYoko = verticalTateChuYokoAtCluster(index);
            if (tateChuYoko != nullptr && index != tateChuYoko->clusters.start) {
                continue;
            }
            // Directionality/soft-hyphen controls have no glyph or advance.
            // Keep a pending painted segment alive so they cannot manufacture
            // a gap between otherwise contiguous decorated clusters.
            if (run == nullptr || cluster.textRange().empty() || cluster.width() <= 0) {
                continue;
            }
            const TextStyle* style = StyleAt(fTextStyles, cluster.textRange().start);
            if (style == nullptr) {
                style = &fParagraphStyle.getTextStyle();
            }
            const TextDecoration type = style->getDecorationType();
            if (type == TextDecoration::kNoDecoration) {
                flush(&underline);
                flush(&lineThrough);
                continue;
            }

            const SkScalar shift = VerticalClusterShift(column, index);
            const SkScalar shiftAfter = VerticalClusterShiftAfter(column, index);
            const SkScalar top = tateChuYoko != nullptr
                    ? inlineOrigin + tateChuYoko->inlineStart + shift + verticalRubyShift(index)
                    : inlineOrigin + run->offset().y() + run->inlinePosition(cluster.startPos()) +
                              shift + verticalRubyShift(index);
            const SkScalar bottom = tateChuYoko != nullptr
                    ? top + tateChuYoko->inlineExtent
                    : top + std::max(SkScalar(0), cluster.width() + shiftAfter - shift);
            if (!SkIsFinite(top) || !SkIsFinite(bottom)) {
                flush(&underline);
                flush(&lineThrough);
                continue;
            }

            const SkColor color = style->getDecorationColor() == SK_ColorTRANSPARENT
                    ? style->getColor()
                    : style->getDecorationColor();
            const SkScalar multiplier = style->getDecorationThicknessMultiplier();
            // SidewaysBlockOffset only moves a rotated run's baseline origin
            // onto the column's central baseline. Decorations belong to the
            // column, not to that adjusted glyph origin; only baselineShift
            // moves them along the block axis.
            const SkScalar runBlockCenter = blockCenter - run->baselineShift();
            const bool hasUnderline = HasDecoration(type, TextDecoration::kUnderline);
            const bool hasLineThrough = HasDecoration(type, TextDecoration::kLineThrough);

            if (hasUnderline) {
                const SkScalar thickness = ResolveVerticalDecorationThickness(
                        *run, TextDecoration::kUnderline, multiplier);
                if (thickness > 0) {
                    if (!hasUnderlineCenter) {
                        underlineCenter = ResolveVerticalUnderlineCenter(*run, blockCenter, thickness);
                        hasUnderlineCenter = true;
                    }
                    const SkScalar centerX = underlineCenter - run->baselineShift();
                    append(&underline,
                           columnIndex,
                           TextDecoration::kUnderline,
                           SkRect::MakeLTRB(centerX - thickness * 0.5f,
                                            top,
                                            centerX + thickness * 0.5f,
                                            bottom),
                           color,
                           thickness);
                } else {
                    flush(&underline);
                }
            } else {
                flush(&underline);
            }

            if (hasLineThrough) {
                const SkScalar thickness = ResolveVerticalDecorationThickness(
                        *run, TextDecoration::kLineThrough, multiplier);
                if (thickness > 0) {
                    append(&lineThrough,
                           columnIndex,
                           TextDecoration::kLineThrough,
                           SkRect::MakeLTRB(runBlockCenter - thickness * 0.5f,
                                            top,
                                            runBlockCenter + thickness * 0.5f,
                                            bottom),
                           color,
                           thickness);
                } else {
                    flush(&lineThrough);
                }
            } else {
                flush(&lineThrough);
            }
        }
        flush(&underline);
        flush(&lineThrough);
    }
}

void ParagraphImpl::resolveVerticalBackgrounds() {
    fVerticalBackgrounds.clear();
    for (size_t columnIndex = 0;
         columnIndex < fLines.size() && columnIndex < fVerticalColumns.size();
         ++columnIndex) {
        const TextLine& line = fLines[columnIndex];
        const VerticalColumnGeometry& column = fVerticalColumns[columnIndex];
        const SkScalar inlineOrigin = line.offset().y() - column.inlineSourceStart;
        bool adjacent = false;
        for (ClusterIndex index = column.clusters.start; index < column.clusters.end; ++index) {
            const Cluster& cluster = fClusters[index];
            if (cluster.isHardBreak() || cluster.textRange().start >= line.trimmedText().end) {
                adjacent = false;
                continue;
            }
            const Run* run = cluster.runOrNull();
            if (run != nullptr && run->isPlaceholder()) {
                adjacent = false;
                continue;
            }
            if (verticalTateChuYokoAtCluster(index) != nullptr) {
                adjacent = false;
                continue;
            }
            if (run == nullptr || cluster.textRange().empty() || cluster.width() <= 0) {
                continue;
            }
            const TextStyle* style = StyleAt(fTextStyles, cluster.textRange().start);
            if (style == nullptr) {
                style = &fParagraphStyle.getTextStyle();
            }
            if (!style->hasBackground()) {
                adjacent = false;
                continue;
            }
            const auto metrics = VerticalBlockMetricsForRun(*run);
            const SkScalar baseline = line.offset().x() + column.blockDescent +
                                      VerticalRunBlockOffset(*run);
            const SkScalar shift = VerticalClusterShift(column, index);
            const SkScalar top = inlineOrigin + run->offset().y() +
                                 run->inlinePosition(cluster.startPos()) + shift + verticalRubyShift(index);
            const SkScalar bottom = top + std::max(SkScalar(0), cluster.width() +
                    VerticalClusterShiftAfter(column, index) - shift);
            const SkRect rect = SkRect::MakeLTRB(baseline - metrics.descent,
                                                top,
                                                baseline + metrics.ascent,
                                                bottom);
            if (!rect.isFinite() || rect.isEmpty()) {
                adjacent = false;
                continue;
            }
            const auto paint = style->getBackgroundPaintOrID();
            if (adjacent) {
                auto& previous = fVerticalBackgrounds.back();
                if (previous.paint == paint && previous.rect.left() == rect.left() &&
                    previous.rect.right() == rect.right() && rect.top() <= previous.rect.bottom()) {
                    previous.rect.join(rect);
                    continue;
                }
            }
            fVerticalBackgrounds.push_back({columnIndex, rect, paint});
            adjacent = true;
        }
    }
}

void ParagraphImpl::resolveVerticalMetrics() {
    resolveVerticalDecorations();
    resolveVerticalBackgrounds();
    SkRect ink = SkRect::MakeEmpty();
    bool hasInk = false;
    for (size_t columnIndex = 0; columnIndex < fLines.size() && columnIndex < fVerticalColumns.size(); ++columnIndex) {
        const TextLine& line = fLines[columnIndex];
        const VerticalColumnGeometry& column = fVerticalColumns[columnIndex];
        const SkScalar baselineX = line.offset().x() + column.blockDescent;
        const SkScalar inlineOrigin = line.offset().y() - column.inlineSourceStart;
        for (auto& run: fRuns) {
            const bool sideways = run.isSideways();
            const SkScalar runBaselineX = baselineX + VerticalRunBlockOffset(run);
            for (size_t glyphIndex = 0; glyphIndex < run.size(); ++glyphIndex) {
                if (!GlyphInColumn(run, glyphIndex, column)) {
                    continue;
                }
                const TextIndex glyphText = run.textRange().start + run.clusterIndex(glyphIndex);
                const ClusterIndex clusterIndex = fClustersIndexFromCodeUnit[glyphText];
                if (verticalTateChuYokoAtCluster(clusterIndex) != nullptr) {
                    continue;
                }
                const SkScalar shift = VerticalClusterShift(column, clusterIndex);
                SkRect bounds = run.font().getBounds(run.fGlyphData->glyphs()[glyphIndex], nullptr);
                if (bounds.isEmpty()) {
                    continue;
                }
                if (sideways) {
                    bounds = RotateClockwiseBounds(bounds);
                }
                const SkPoint position = run.fGlyphData->positions()[glyphIndex] + run.fGlyphData->offsets()[glyphIndex];
                bounds.offset(runBaselineX + position.x(),
                              inlineOrigin + run.offset().y() + position.y() + shift +
                              verticalRubyShift(clusterIndex));
                if (!hasInk) {
                    ink = bounds;
                    hasInk = true;
                } else {
                    ink.join(bounds);
                }
            }
        }
        ForEachVerticalAffixGlyph(line,
                                  column,
                                  [&](const VerticalAffixRun& run,
                                      size_t glyphIndex,
                                      const SkPoint&,
                                      const SkPoint& origin) {
            SkRect bounds = run.font.getBounds(run.glyphs[glyphIndex], nullptr);
            if (bounds.isEmpty()) {
                return;
            }
            if (run.sideways) {
                bounds = RotateClockwiseBounds(bounds);
            }
            bounds.offset(origin.x(), origin.y());
            if (!hasInk) {
                ink = bounds;
                hasInk = true;
            } else {
                ink.join(bounds);
            }
        });
        for (const auto& [span, origin] : verticalTateChuYokoPlacements(columnIndex)) {
            span->content->extendedVisit([&](int, const ExtendedVisitorInfo* info) {
                if (info == nullptr || info->count == 0) {
                    return;
                }
                SkMatrix transform;
                transform.setScale(span->squeeze, 1);
                transform.postConcat(info->glyphTransform);
                for (int index = 0; index < info->count; ++index) {
                    SkRect bounds = transform.mapRect(info->bounds[index]);
                    if (bounds.isEmpty()) {
                        continue;
                    }
                    const SkPoint position = info->positions[index];
                    bounds.offset(origin.x() + span->squeeze * (info->origin.x() + position.x()),
                                  origin.y() + info->origin.y() + position.y());
                    if (!hasInk) {
                        ink = bounds;
                        hasInk = true;
                    } else {
                        ink.join(bounds);
                    }
                }
            });
        }
    }
    for (const auto& decoration: fVerticalDecorations) {
        if (!hasInk) {
            ink = decoration.rect;
            hasInk = true;
        } else {
            ink.join(decoration.rect);
        }
    }
    for (const auto& background: fVerticalBackgrounds) {
        if (!hasInk) {
            ink = background.rect;
            hasInk = true;
        } else {
            ink.join(background.rect);
        }
    }
    for (size_t column = 0; column < fVerticalColumns.size(); ++column) {
        for (const auto& [span, origin] : verticalRubyPlacements(column)) {
            SkRect bounds = span->annotation->getLayoutMetrics().inkBounds;
            bounds.offset(origin);
            ink.join(bounds);
            hasInk |= !bounds.isEmpty();
        }
    }
    fLayoutMetrics.inkBounds = hasInk ? ink : SkRect::MakeEmpty();
}

int ParagraphImpl::getVerticalColumnForCluster(ClusterIndex clusterIndex) const {
    for (size_t index = 0; index < fVerticalColumns.size(); ++index) {
        const auto& column = fVerticalColumns[index];
        if (column.clusters.start <= clusterIndex && clusterIndex < column.clusters.end) {
            return SkToInt(index);
        }
    }
    return -1;
}

SkScalar ParagraphImpl::getVerticalClusterShift(ClusterIndex clusterIndex) const {
    const int columnIndex = getVerticalColumnForCluster(clusterIndex);
    if (columnIndex < 0) {
        return 0;
    }
    return VerticalClusterShift(fVerticalColumns[columnIndex], clusterIndex);
}

SkScalar ParagraphImpl::getVerticalClusterShiftAfter(ClusterIndex clusterIndex) const {
    const int columnIndex = getVerticalColumnForCluster(clusterIndex);
    if (columnIndex < 0) {
        return 0;
    }
    return VerticalClusterShiftAfter(fVerticalColumns[columnIndex], clusterIndex);
}

bool ParagraphImpl::getVerticalClusterCell(ClusterIndex clusterIndex, SkRect* cell) const {
    if (clusterIndex >= fClusters.size()) {
        return false;
    }
    const int columnIndex = getVerticalColumnForCluster(clusterIndex);
    if (columnIndex < 0) {
        return false;
    }
    const Cluster& cluster = fClusters[clusterIndex];
    const Run* run = cluster.runOrNull();
    if (run == nullptr) {
        return false;
    }
    const TextLine& line = fLines[columnIndex];
    const VerticalColumnGeometry& column = fVerticalColumns[columnIndex];
    if (const auto* span = verticalTateChuYokoAtCluster(clusterIndex)) {
        const SkScalar shift = VerticalClusterShift(column, clusterIndex);
        const SkScalar top = line.offset().y() + span->inlineStart + shift -
                             column.inlineSourceStart;
        const SkScalar bottom = top + span->inlineExtent;
        if (cell != nullptr) {
            const SkScalar centerX = line.offset().x() + column.blockDescent + span->blockOffset;
            const SkScalar left = centerX - span->cellWidth * 0.5f;
            *cell = SkRect::MakeLTRB(left, top, left + span->cellWidth, bottom);
        }
        return true;
    }
    const SkScalar inlineStart = run->offset().y() + run->inlinePosition(cluster.startPos());
    const SkScalar shift = VerticalClusterShift(column, clusterIndex);
    const SkScalar shiftAfter = VerticalClusterShiftAfter(column, clusterIndex);
    const SkScalar top = line.offset().y() + inlineStart + shift - column.inlineSourceStart;
    const SkScalar bottom = std::min(
            line.offset().y() + line.height(),
            top + std::max(SkScalar(0), cluster.width() + shiftAfter - shift));
    if (cell != nullptr) {
        const SkScalar width = run->isPlaceholder() ? run->placeholderStyle()->fWidth : line.width();
        const SkScalar left = run->isPlaceholder()
                ? fLayoutMetrics.columns[columnIndex].physicalBounds.centerX() - width * 0.5f
                : line.offset().x();
        *cell = SkRect::MakeLTRB(left, top, left + width, bottom);
    }
    return true;
}

void ParagraphImpl::paintVertical(ParagraphPainter* painter, SkScalar x, SkScalar y) {
    ensureVerticalLayoutReady();
    if (painter == nullptr) {
        throw std::invalid_argument("vertical-rl paint requires a painter");
    }

    struct GlyphPaint {
        sk_sp<SkTextBlob> blob;
        const TextStyle* style;
        SkPoint origin;
        bool sideways;
    };
    std::vector<GlyphPaint> glyphPaints;
    std::vector<VerticalDecorationSegment> decorations = fVerticalDecorations;

    for (const auto& background: fVerticalBackgrounds) {
        SkRect rect = background.rect;
        rect.offset(x, y);
        painter->drawRect(rect, background.paint);
    }

    for (size_t columnIndex = 0; columnIndex < fLines.size() && columnIndex < fVerticalColumns.size(); ++columnIndex) {
        const TextLine& line = fLines[columnIndex];
        const VerticalColumnGeometry& column = fVerticalColumns[columnIndex];
        const SkScalar baselineX = line.offset().x() + column.blockDescent;
        const SkScalar inlineOrigin = line.offset().y() - column.inlineSourceStart;

        for (auto& run: fRuns) {
            const bool sideways = run.isSideways();
            std::vector<SkGlyphID> glyphs;
            std::vector<SkPoint> points;
            glyphs.reserve(run.size());
            points.reserve(run.size());
            const TextStyle* style = nullptr;
            auto flushGlyphs = [&]() {
                if (glyphs.empty()) {
                    return;
                }
                SkTextBlobBuilder builder;
                auto buffer = builder.allocRunPos(run.font(), SkToInt(glyphs.size()));
                std::copy(glyphs.begin(), glyphs.end(), buffer.glyphs);
                std::copy(points.begin(), points.end(), buffer.points());
                sk_sp<SkTextBlob> blob = builder.make();
                if (blob != nullptr) {
                    const SkScalar originX = x + baselineX + VerticalRunBlockOffset(run);
                    const SkScalar originY = y + inlineOrigin + run.offset().y();
                    glyphPaints.push_back({std::move(blob), style, {originX, originY}, sideways});
                }
                glyphs.clear();
                points.clear();
            };
            for (size_t glyphIndex = 0; glyphIndex < run.size(); ++glyphIndex) {
                if (!GlyphInColumn(run, glyphIndex, column)) {
                    continue;
                }
                const SkPoint position = run.fGlyphData->positions()[glyphIndex] + run.fGlyphData->offsets()[glyphIndex];
                const TextIndex glyphText = run.textRange().start + run.clusterIndex(glyphIndex);
                const ClusterIndex clusterIndex = fClustersIndexFromCodeUnit[glyphText];
                if (verticalTateChuYokoAtCluster(clusterIndex) != nullptr) {
                    continue;
                }
                const TextStyle* glyphStyle = StyleAt(fTextStyles, glyphText);
                if (glyphStyle == nullptr) {
                    throw std::runtime_error("vertical-rl could not resolve the text style for painting");
                }
                if (glyphStyle != style) {
                    flushGlyphs();
                    style = glyphStyle;
                }
                const SkScalar shift = VerticalClusterShift(column, clusterIndex) +
                                       verticalRubyShift(clusterIndex);
                glyphs.push_back(run.fGlyphData->glyphs()[glyphIndex]);
                SkPoint point = sideways ? ReverseClockwise(position) : position;
                if (sideways) {
                    point.fX += shift;
                } else {
                    point.fY += shift;
                }
                points.push_back(point);
            }
            flushGlyphs();
        }
        if (column.ellipsized) {
            for (const auto& run: column.affix.runs) {
                if (run.glyphs.empty()) {
                    continue;
                }
                std::vector<SkPoint> points;
                points.reserve(run.glyphs.size());
                for (size_t index = 0; index < run.glyphs.size(); ++index) {
                    const SkPoint position = run.positions[index] + run.offsets[index];
                    points.push_back(run.sideways ? ReverseClockwise(position) : position);
                }
                SkTextBlobBuilder builder;
                auto buffer = builder.allocRunPos(run.font, SkToInt(run.glyphs.size()));
                std::copy(run.glyphs.begin(), run.glyphs.end(), buffer.glyphs);
                std::copy(points.begin(), points.end(), buffer.points());
                sk_sp<SkTextBlob> blob = builder.make();
                if (blob == nullptr) {
                    continue;
                }
                const SkScalar originX = x + baselineX + AffixRunBlockOffset(run) -
                                        column.affix.style.getBaselineShift();
                const SkScalar originY = y + inlineOrigin + column.affix.inlineStart +
                                         run.inlineOffset;
                glyphPaints.push_back({std::move(blob), &column.affix.style,
                                      {originX, originY}, run.sideways});
            }
        }
    }

    for (size_t column = 0; column < fVerticalColumns.size(); ++column) {
        for (const auto& [span, origin] : verticalRubyPlacements(column)) {
            auto* annotation = static_cast<ParagraphImpl*>(span->annotation.get());
            for (const auto& background : annotation->fVerticalBackgrounds) {
                SkRect rect = background.rect;
                rect.offset(x + origin.x(), y + origin.y());
                painter->drawRect(rect, background.paint);
            }
            for (const auto& segment : annotation->fVerticalDecorations) {
                auto translated = segment;
                translated.rect.offset(origin);
                decorations.push_back(translated);
            }
            annotation->extendedVisit([&](int, const ExtendedVisitorInfo* info) {
                if (info == nullptr || info->count == 0) {
                    return;
                }
                const bool sideways = info->glyphTransform.getSkewX() != 0;
                SkTextBlobBuilder builder;
                auto buffer = builder.allocRunPos(info->font, info->count);
                std::copy(info->glyphs, info->glyphs + info->count, buffer.glyphs);
                for (int index = 0; index < info->count; ++index) {
                    buffer.points()[index] = sideways ? ReverseClockwise(info->positions[index]) :
                                                        info->positions[index];
                }
                auto blob = builder.make();
                if (blob != nullptr) {
                    glyphPaints.push_back({std::move(blob),
                            StyleAt(annotation->fTextStyles, info->utf8Starts[0]),
                            info->origin + origin + SkPoint::Make(x, y), sideways});
                }
            });
        }
    }

    auto drawGlyph = [&](const GlyphPaint& glyph, SkPoint offset, const auto& draw) {
        const SkPoint origin = glyph.origin + offset;
        if (glyph.sideways) {
            painter->save();
            painter->translate(origin.x(), origin.y());
            painter->rotate(90);
            draw(0, 0);
            painter->restore();
        } else {
            draw(origin.x(), origin.y());
        }
    };
    for (const auto& glyph: glyphPaints) {
        for (const TextShadow& shadow: glyph.style->getShadows()) {
            if (!shadow.hasShadow()) {
                continue;
            }
            drawGlyph(glyph, shadow.fOffset, [&](SkScalar originX, SkScalar originY) {
                painter->drawTextShadow(glyph.blob, originX, originY, shadow.fColor,
                                        SkDoubleToScalar(shadow.fBlurSigma));
            });
        }
    }
    for (const auto& glyph: glyphPaints) {
        drawGlyph(glyph, SkPoint::Make(0, 0), [&](SkScalar originX, SkScalar originY) {
            painter->drawTextBlob(glyph.blob, originX, originY, PaintForStyle(*glyph.style));
        });
    }

    for (size_t column = 0; column < fVerticalColumns.size(); ++column) {
        for (const auto& [span, origin] : verticalTateChuYokoPlacements(column)) {
            painter->save();
            painter->translate(x + origin.x(), y + origin.y());
            if (span->squeeze != 1) {
                painter->scale(span->squeeze, 1);
            }
            span->content->paint(painter, 0, 0);
            painter->restore();
        }
    }

    auto paintDecorationType = [&](TextDecoration decoration) {
        for (const auto& segment: decorations) {
            if (segment.decoration != decoration) {
                continue;
            }
            SkRect rect = segment.rect;
            rect.offset(x, y);
            const ParagraphPainter::DecorationStyle style(
                    segment.color, segment.thickness, std::nullopt);
            painter->drawFilledRect(rect, style);
        }
    };
    paintDecorationType(TextDecoration::kUnderline);
    paintDecorationType(TextDecoration::kLineThrough);
}

void ParagraphImpl::extendedVisitVertical(const ExtendedVisitor& visitor) {
    ensureVerticalLayoutReady();
    for (size_t columnIndex = 0; columnIndex < fLines.size() && columnIndex < fVerticalColumns.size(); ++columnIndex) {
        const TextLine& line = fLines[columnIndex];
        const VerticalColumnGeometry& column = fVerticalColumns[columnIndex];
        const SkScalar baselineX = line.offset().x() + column.blockDescent;
        const SkScalar inlineOrigin = line.offset().y() - column.inlineSourceStart;

        for (auto& run: fRuns) {
            std::vector<SkGlyphID> glyphs;
            std::vector<SkPoint> positions;
            std::vector<SkRect> bounds;
            std::vector<uint32_t> starts;
            glyphs.reserve(run.size());
            positions.reserve(run.size());
            bounds.reserve(run.size());
            starts.reserve(run.size() + 1);
            for (size_t glyphIndex = 0; glyphIndex < run.size(); ++glyphIndex) {
                if (!GlyphInColumn(run, glyphIndex, column)) {
                    continue;
                }
                const TextIndex glyphText = run.textRange().start + run.clusterIndex(glyphIndex);
                const ClusterIndex clusterIndex = fClustersIndexFromCodeUnit[glyphText];
                if (verticalTateChuYokoAtCluster(clusterIndex) != nullptr) {
                    continue;
                }
                const SkScalar shift = VerticalClusterShift(column, clusterIndex);
                glyphs.push_back(run.fGlyphData->glyphs()[glyphIndex]);
                SkPoint position = run.fGlyphData->positions()[glyphIndex] + run.fGlyphData->offsets()[glyphIndex];
                position.fY += shift + verticalRubyShift(clusterIndex);
                positions.push_back(position);
                bounds.push_back(run.font().getBounds(run.fGlyphData->glyphs()[glyphIndex], nullptr));
                starts.push_back(SkToU32(run.fClusterStart + run.fGlyphData->clusterIndexes()[glyphIndex]));
            }
            if (glyphs.empty()) {
                continue;
            }
            starts.push_back(SkToU32(run.textRange().end));

            ExtendedVisitorInfo info = {
                    run.font(),
                    SkPoint::Make(baselineX + VerticalRunBlockOffset(run), inlineOrigin + run.offset().y()),
                    SkSize::Make(line.width(), line.height()),
                    SkToInt(glyphs.size()),
                    glyphs.data(),
                    positions.data(),
                    bounds.data(),
                    starts.data(),
                    0,
                    run.isSideways() ? ClockwiseRotation() : SkMatrix::I(),
            };
            visitor(SkToInt(columnIndex), &info);
        }
        if (column.ellipsized) {
            for (const auto& run: column.affix.runs) {
                if (run.glyphs.empty()) {
                    continue;
                }
                std::vector<SkPoint> positions;
                std::vector<SkRect> bounds;
                positions.reserve(run.glyphs.size());
                bounds.reserve(run.glyphs.size());
                for (size_t index = 0; index < run.glyphs.size(); ++index) {
                    positions.push_back(run.positions[index] + run.offsets[index]);
                    bounds.push_back(run.font.getBounds(run.glyphs[index], nullptr));
                }
                std::vector<uint32_t> starts(run.glyphs.size() + 1,
                                             SkToU32(column.affix.insertion));
                ExtendedVisitorInfo info = {
                        run.font,
                        SkPoint::Make(baselineX + AffixRunBlockOffset(run) -
                                      column.affix.style.getBaselineShift(),
                                      inlineOrigin + column.affix.inlineStart +
                                              run.inlineOffset),
                        SkSize::Make(line.width(), line.height()),
                        SkToInt(run.glyphs.size()),
                        run.glyphs.data(),
                        positions.data(),
                        bounds.data(),
                        starts.data(),
                        0,
                        run.sideways ? ClockwiseRotation() : SkMatrix::I(),
                };
                visitor(SkToInt(columnIndex), &info);
            }
        }
        for (const auto& [span, origin] : verticalTateChuYokoPlacements(columnIndex)) {
            span->content->extendedVisit([&](int, const ExtendedVisitorInfo* contentInfo) {
                if (contentInfo == nullptr || contentInfo->count == 0) {
                    return;
                }
                std::vector<SkPoint> positions(contentInfo->count);
                for (int index = 0; index < contentInfo->count; ++index) {
                    positions[index] = SkPoint::Make(
                            span->squeeze * (contentInfo->origin.x() +
                                             contentInfo->positions[index].x()),
                            contentInfo->origin.y() + contentInfo->positions[index].y());
                }
                std::vector<uint32_t> starts(contentInfo->count + 1);
                const uint32_t width = SkToU32(span->range.width());
                for (int index = 0; index < contentInfo->count; ++index) {
                    starts[index] = SkToU32(span->range.start) +
                                    std::min(contentInfo->utf8Starts[index], width);
                }
                starts[contentInfo->count] = SkToU32(span->range.end);
                SkMatrix transform;
                transform.setScale(span->squeeze, 1);
                transform.postConcat(contentInfo->glyphTransform);
                ExtendedVisitorInfo info = {
                        contentInfo->font,
                        origin,
                        SkSize::Make(span->cellWidth, span->inlineExtent),
                        contentInfo->count,
                        contentInfo->glyphs,
                        positions.data(),
                        contentInfo->bounds,
                        starts.data(),
                        contentInfo->flags | Paragraph::kTateChuYoko_VisitorFlag,
                        transform,
                };
                visitor(SkToInt(columnIndex), &info);
            });
        }
        for (const auto& [span, origin] : verticalRubyPlacements(columnIndex)) {
            span->annotation->extendedVisit([&](int, const ExtendedVisitorInfo* annotationInfo) {
                if (annotationInfo == nullptr) {
                    return;
                }
                auto info = *annotationInfo;
                info.origin += origin;
                std::vector<uint32_t> starts(info.count + 1, SkToU32(span->input.baseRange.start));
                starts.back() = SkToU32(span->input.baseRange.end);
                info.utf8Starts = starts.data();
                info.flags |= Paragraph::kRubyAnnotation_VisitorFlag;
                visitor(SkToInt(columnIndex), &info);
            });
        }
        visitor(SkToInt(columnIndex), nullptr);
    }
}

int ParagraphImpl::getVerticalPath(int lineNumber, SkPath* dest) {
    ensureVerticalLayoutReady();
    if (dest == nullptr) {
        throw std::invalid_argument("vertical-rl getPath requires a destination path");
    }
    if (lineNumber < 0 || SkToSizeT(lineNumber) >= fLines.size()) {
        throw std::logic_error("vertical-rl getPath line number is outside the visible columns");
    }
    const TextLine& line = fLines[lineNumber];
    const VerticalColumnGeometry& column = fVerticalColumns[lineNumber];
    const SkScalar baselineX = line.offset().x() + column.blockDescent;
    const SkScalar inlineOrigin = line.offset().y() - column.inlineSourceStart;
    SkPathBuilder builder;
    int missing = 0;
    for (auto& run: fRuns) {
        const bool sideways = run.isSideways();
        for (size_t glyphIndex = 0; glyphIndex < run.size(); ++glyphIndex) {
            if (!GlyphInColumn(run, glyphIndex, column)) {
                continue;
            }
            const TextIndex glyphText = run.textRange().start + run.clusterIndex(glyphIndex);
            const ClusterIndex clusterIndex = fClustersIndexFromCodeUnit[glyphText];
            if (verticalTateChuYokoAtCluster(clusterIndex) != nullptr) {
                continue;
            }
            const SkScalar shift = VerticalClusterShift(column, clusterIndex);
            const auto glyphPath = run.font().getPath(run.fGlyphData->glyphs()[glyphIndex]);
            if (!glyphPath) {
                ++missing;
                continue;
            }
            const SkPoint position = run.fGlyphData->positions()[glyphIndex] + run.fGlyphData->offsets()[glyphIndex];
            const SkScalar originX = baselineX + VerticalRunBlockOffset(run) + position.x();
            const SkScalar originY = inlineOrigin + run.offset().y() + position.y() + shift +
                                    verticalRubyShift(clusterIndex);
            SkMatrix total;
            if (sideways) {
                total.setAll(0, -1, originX, 1, 0, originY, 0, 0, 1);
            } else {
                total.setTranslate(originX, originY);
            }
            builder.addPath(*glyphPath, total);
        }
    }
    if (column.ellipsized) {
        for (const auto& run: column.affix.runs) {
            const SkScalar runBaselineX = baselineX + AffixRunBlockOffset(run) -
                                         column.affix.style.getBaselineShift();
            const SkScalar runInlineStart = inlineOrigin + column.affix.inlineStart +
                                            run.inlineOffset;
            for (size_t index = 0; index < run.glyphs.size(); ++index) {
                const auto glyphPath = run.font.getPath(run.glyphs[index]);
                if (!glyphPath) {
                    ++missing;
                    continue;
                }
                const SkPoint position = run.positions[index] + run.offsets[index];
                const SkScalar originX = runBaselineX + position.x();
                const SkScalar originY = runInlineStart + position.y();
                SkMatrix total;
                if (run.sideways) {
                    total.setAll(0, -1, originX, 1, 0, originY, 0, 0, 1);
                } else {
                    total.setTranslate(originX, originY);
                }
                builder.addPath(*glyphPath, total);
            }
        }
    }
    for (const auto& [span, origin] : verticalTateChuYokoPlacements(SkToSizeT(lineNumber))) {
        SkPath contentPath;
        missing += span->content->getPath(0, &contentPath);
        SkMatrix transform;
        transform.setScale(span->squeeze, 1);
        transform.postTranslate(origin.x(), origin.y());
        builder.addPath(contentPath, transform);
    }
    for (const auto& [span, origin] : verticalRubyPlacements(SkToSizeT(lineNumber))) {
        SkPath annotationPath;
        missing += span->annotation->getPath(0, &annotationPath);
        builder.addPath(annotationPath, SkMatrix::Translate(origin.x(), origin.y()));
    }
    *dest = builder.detach();
    return missing;
}

std::vector<TextBox> ParagraphImpl::getVerticalRectsForRange(unsigned start, unsigned end, bool inlineBox) {
    ensureVerticalLayoutReady();
    if (start >= end || fText.isEmpty()) {
        return {};
    }
    this->ensureUTF16Mapping();
    const size_t utf16Length = fUTF8IndexForUTF16Index.size() - 1;
    if (start >= utf16Length) {
        return {};
    }
    end = std::min<unsigned>(end, static_cast<unsigned>(utf16Length));
    if (start >= end) {
        return {};
    }

    const TextIndex utf8Start = findPreviousGraphemeBoundary(fUTF8IndexForUTF16Index[start]);
    const TextIndex mappedEnd = fUTF8IndexForUTF16Index[end];
    TextIndex utf8End = mappedEnd;
    if (mappedEnd < fText.size()) {
        const TextIndex graphemeStart = findPreviousGraphemeBoundary(mappedEnd);
        const TextIndex graphemeEnd = findNextGraphemeBoundary(graphemeStart + 1);
        const size_t graphemeStartUtf16 = fUTF16IndexForUTF8Index[graphemeStart];
        const size_t graphemeEndUtf16 = fUTF16IndexForUTF8Index[graphemeEnd];
        if (end > graphemeStartUtf16 && end < graphemeEndUtf16) {
            // The requested endpoint is inside a surrogate pair, combining
            // sequence, or other grapheme. Select the complete intersecting unit.
            utf8End = graphemeEnd;
        }
    }
    if (utf8Start >= utf8End) {
        return {};
    }

    std::vector<TextBox> boxes;
    for (size_t columnIndex = 0; columnIndex < fVerticalColumns.size(); ++columnIndex) {
        const auto& column = fVerticalColumns[columnIndex];
        const TextLine& line = fLines[columnIndex];
        SkScalar columnInlineThickness = 0;
        if (inlineBox) {
            for (ClusterIndex index = column.clusters.start; index < column.clusters.end; ++index) {
                const Cluster& cluster = fClusters[index];
                const Run* run = cluster.runOrNull();
                if (verticalTateChuYokoAtCluster(index) == nullptr && run != nullptr && !run->isPlaceholder()) {
                    columnInlineThickness = std::max(columnInlineThickness, run->descent() - run->ascent());
                }
            }
        }
        bool found = false;
        SkScalar left = 0;
        SkScalar right = 0;
        SkScalar top = 0;
        SkScalar bottom = 0;
        auto flush = [&]() {
            if (found && bottom > top) {
                const SkScalar blockLeft = inlineBox ? left : line.offset().x();
                const SkScalar blockRight = inlineBox ? right : line.offset().x() + line.width();
                boxes.emplace_back(SkRect::MakeLTRB(blockLeft, top, blockRight, bottom),
                                   TextDirection::kLtr);
            }
            found = false;
        };
        for (ClusterIndex index = column.clusters.start; index < column.clusters.end; ++index) {
            const Cluster& cluster = fClusters[index];
            const TextRange clusterText = cluster.textRange();
            if (const auto* span = verticalTateChuYokoStartingAt(index)) {
                if (clusterText.end > utf8Start && clusterText.start < utf8End) {
                    flush();
                    SkRect cell;
                    if (getVerticalClusterCell(index, &cell) && !cell.isEmpty()) {
                        if (inlineBox) {
                            const Run* run = cluster.runOrNull();
                            if (run == nullptr) {
                                continue;
                            }
                            const SkScalar fontThickness = columnInlineThickness > 0
                                    ? columnInlineThickness
                                    : run->descent() - run->ascent();
                            const SkScalar thickness = std::max(cell.width(), fontThickness);
                            const SkScalar centerX = cell.centerX();
                            cell.fLeft = centerX - thickness * 0.5f;
                            cell.fRight = centerX + thickness * 0.5f;
                        }
                        boxes.emplace_back(cell, TextDirection::kLtr);
                    }
                }
                index = span->clusters.end - 1;
                continue;
            }
            if (clusterText.end <= utf8Start || clusterText.start >= utf8End) {
                continue;
            }
            if (cluster.isHardBreak() || cluster.width() <= 0) {
                continue;
            }
            const Run* run = cluster.runOrNull();
            if (run == nullptr) {
                continue;
            }
            if (run->isPlaceholder()) {
                flush();
                SkRect cell;
                if (getVerticalClusterCell(index, &cell) && !cell.isEmpty()) {
                    boxes.emplace_back(cell, TextDirection::kLtr);
                }
                continue;
            }
            const SkScalar inlineStart = run->offset().y() + run->inlinePosition(cluster.startPos());
            const SkScalar shift = VerticalClusterShift(column, index);
            const SkScalar shiftAfter = VerticalClusterShiftAfter(column, index);
            const SkScalar clusterTop =
                    line.offset().y() + inlineStart + shift - column.inlineSourceStart;
            const SkScalar clusterBottom = std::min(
                    line.offset().y() + line.height(),
                    clusterTop + std::max(SkScalar(0), cluster.width() + shiftAfter - shift));
            const SkScalar thickness = std::max(SkScalar(0), run->descent() - run->ascent());
            const SkScalar baselineX = line.offset().x() + column.blockDescent - run->baselineShift();
            const SkScalar clusterLeft = inlineBox
                    ? baselineX - thickness * 0.5f
                    : line.offset().x();
            const SkScalar clusterRight = inlineBox
                    ? baselineX + thickness * 0.5f
                    : line.offset().x() + line.width();
            if (inlineBox && found && (left != clusterLeft || right != clusterRight)) {
                flush();
            }
            if (!found) {
                left = clusterLeft;
                right = clusterRight;
                top = clusterTop;
                bottom = clusterBottom;
                found = true;
            } else {
                top = std::min(top, clusterTop);
                bottom = std::max(bottom, clusterBottom);
            }
        }
        flush();
    }
    return boxes;
}

PositionWithAffinity ParagraphImpl::getVerticalGlyphPositionAtCoordinate(SkScalar dx, SkScalar dy) {
    ensureVerticalLayoutReady();
    if (fText.isEmpty() || fLines.empty()) {
        return {0, Affinity::kDownstream};
    }
    this->ensureUTF16Mapping();

    int nearest = -1;
    SkScalar nearestDistance = std::numeric_limits<SkScalar>::max();
    for (size_t index = 0; index < fLines.size(); ++index) {
        const TextLine& line = fLines[index];
        const SkScalar left = line.offset().x();
        const SkScalar right = left + line.width();
        SkScalar distance = 0;
        if (dx < left) {
            distance = left - dx;
        } else if (dx > right) {
            distance = dx - right;
        }
        if (distance < nearestDistance) {
            nearestDistance = distance;
            nearest = SkToInt(index);
        }
    }
    if (nearest < 0) {
        return {0, Affinity::kDownstream};
    }

    const TextLine& line = fLines[nearest];
    const VerticalColumnGeometry& column = fVerticalColumns[nearest];
    if (column.clusters.empty() || line.height() <= 0) {
        const size_t utf16 =
                SkToSizeT(nearest) < fLayoutMetrics.columns.size() ? fLayoutMetrics.columns[nearest].utf16Start : 0;
        return {SkToInt(utf16), Affinity::kDownstream};
    }

    const SkScalar localY = std::clamp(dy - line.offset().y(), SkScalar(0), line.height());
    const SkScalar inlineCoordinate = column.inlineSourceStart + localY;
    if (column.ellipsized && inlineCoordinate >= column.affix.inlineStart &&
        (inlineCoordinate <= column.affix.inlineStart + column.affix.advance ||
         SkScalarNearlyZero(inlineCoordinate - column.affix.inlineStart))) {
        return {SkToInt(fUTF16IndexForUTF8Index[column.affix.insertion]),
                Affinity::kDownstream};
    }

    for (ClusterIndex index = column.clusters.start; index < column.clusters.end; ++index) {
        const Cluster& cluster = fClusters[index];
        if (const auto* span = verticalTateChuYokoStartingAt(index)) {
            const SkScalar shift = VerticalClusterShift(column, index);
            const SkScalar clusterStart = span->inlineStart + shift;
            const SkScalar clusterWidth = span->inlineExtent;
            const SkScalar clusterEnd = clusterStart + clusterWidth;
            const size_t startUtf16 = fUTF16IndexForUTF8Index[span->range.start];
            const size_t endUtf16 = fUTF16IndexForUTF8Index[span->range.end];
            if (inlineCoordinate <= clusterStart) {
                return {SkToInt(startUtf16), Affinity::kDownstream};
            }
            if (inlineCoordinate <= clusterEnd) {
                const SkScalar middle = clusterStart + clusterWidth * 0.5f;
                if (inlineCoordinate < middle) {
                    return {SkToInt(startUtf16), Affinity::kUpstream};
                }
                return {SkToInt(endUtf16), Affinity::kDownstream};
            }
            index = span->clusters.end - 1;
            continue;
        }
        const Run* run = cluster.runOrNull();
        if (run == nullptr || cluster.textRange().empty()) {
            continue;
        }
        const SkScalar shift = VerticalClusterShift(column, index);
        const SkScalar shiftAfter = VerticalClusterShiftAfter(column, index);
        const SkScalar clusterStart =
                run->offset().y() + run->inlinePosition(cluster.startPos()) + shift;
        const SkScalar clusterWidth =
                std::max(SkScalar(0), cluster.width() + shiftAfter - shift);
        const SkScalar clusterEnd = clusterStart + clusterWidth;
        const size_t startUtf16 = fUTF16IndexForUTF8Index[cluster.textRange().start];
        const size_t endUtf16 = fUTF16IndexForUTF8Index[cluster.textRange().end];
        if (inlineCoordinate <= clusterStart) {
            return {SkToInt(startUtf16), Affinity::kDownstream};
        }
        if (clusterWidth <= 0) {
            return {SkToInt(endUtf16), Affinity::kDownstream};
        }
        if (inlineCoordinate <= clusterEnd) {
            const SkScalar middle = clusterStart + clusterWidth * 0.5f;
            if (inlineCoordinate < middle) {
                return {SkToInt(startUtf16), Affinity::kUpstream};
            }
            return {SkToInt(endUtf16), Affinity::kDownstream};
        }
    }

    if (column.clusters.empty()) {
        return {SkToInt(fUTF16IndexForUTF8Index[column.text.end]), Affinity::kDownstream};
    }
    const Cluster& last = fClusters[column.clusters.end - 1];
    return {SkToInt(fUTF16IndexForUTF8Index[last.textRange().end]), Affinity::kDownstream};
}

bool ParagraphImpl::getVerticalGlyphClusterAt(TextIndex codeUnitIndex, GlyphClusterInfo* glyphInfo) {
    ensureVerticalLayoutReady();
    if (codeUnitIndex >= fText.size()) {
        return false;
    }
    const ClusterIndex clusterIndex = fClustersIndexFromCodeUnit[codeUnitIndex];
    if (clusterIndex == EMPTY_INDEX || clusterIndex >= fClusters.size()) {
        return false;
    }
    SkRect cell;
    if (!getVerticalClusterCell(clusterIndex, &cell)) {
        return false;
    }
    if (glyphInfo != nullptr) {
        glyphInfo->fBounds = cell;
        glyphInfo->fClusterTextRange = fClusters[clusterIndex].textRange();
        glyphInfo->fGlyphClusterPosition = TextDirection::kLtr;
    }
    return true;
}

bool ParagraphImpl::getVerticalClosestGlyphClusterAt(SkScalar dx, SkScalar dy, GlyphClusterInfo* glyphInfo) {
    const PositionWithAffinity position = getVerticalGlyphPositionAtCoordinate(dx, dy);
    this->ensureUTF16Mapping();
    size_t utf16 = position.position <= 0 ? 0 : SkToSizeT(position.position);
    if (position.affinity == Affinity::kDownstream && utf16 > 0) {
        --utf16;
    }
    if (fUTF8IndexForUTF16Index.size() > 1 && utf16 + 1 >= fUTF8IndexForUTF16Index.size()) {
        utf16 = fUTF8IndexForUTF16Index.size() - 2;
    }
    const TextIndex utf8 = fUTF8IndexForUTF16Index[utf16];
    return getVerticalGlyphClusterAt(utf8, glyphInfo);
}

bool ParagraphImpl::getVerticalGlyphInfoAtUTF16Offset(size_t codeUnitIndex, GlyphInfo* glyphInfo) {
    ensureVerticalLayoutReady();
    if (fText.isEmpty()) {
        return false;
    }
    this->ensureUTF16Mapping();
    if (codeUnitIndex >= fUTF8IndexForUTF16Index.size()) {
        return false;
    }
    TextIndex utf8 = fUTF8IndexForUTF16Index[codeUnitIndex];
    if (utf8 >= fText.size()) {
        utf8 = findPreviousGraphemeBoundary(fText.size());
    }
    const TextIndex startIndex = findPreviousGraphemeBoundary(utf8);
    const TextIndex endIndex = findNextGraphemeBoundary(std::min(utf8 + 1, fText.size()));
    if (startIndex >= endIndex) {
        return false;
    }

    SkRect bounds = SkRect::MakeEmpty();
    bool found = false;
    for (size_t columnIndex = 0; columnIndex < fVerticalColumns.size(); ++columnIndex) {
        const auto& column = fVerticalColumns[columnIndex];
        for (ClusterIndex index = column.clusters.start; index < column.clusters.end; ++index) {
            const Cluster& cluster = fClusters[index];
            const TextRange range = cluster.textRange();
            if (range.end <= startIndex || range.start >= endIndex) {
                continue;
            }
            SkRect cell;
            if (!getVerticalClusterCell(index, &cell) || cell.height() <= 0) {
                continue;
            }
            if (!found) {
                bounds = cell;
                found = true;
            } else {
                bounds.join(cell);
            }
        }
    }
    if (!found) {
        return false;
    }
    if (glyphInfo != nullptr) {
        glyphInfo->fGraphemeLayoutBounds = bounds;
        glyphInfo->fGraphemeClusterTextRange =
                TextRange(fUTF16IndexForUTF8Index[startIndex], fUTF16IndexForUTF8Index[endIndex]);
        glyphInfo->fDirection = TextDirection::kLtr;
        glyphInfo->fIsEllipsis = false;
    }
    return true;
}

bool ParagraphImpl::getVerticalClosestUTF16GlyphInfoAt(SkScalar dx, SkScalar dy, GlyphInfo* glyphInfo) {
    const PositionWithAffinity position = getVerticalGlyphPositionAtCoordinate(dx, dy);
    size_t utf16 = position.position <= 0 ? 0 : SkToSizeT(position.position);
    if (position.affinity == Affinity::kDownstream && utf16 > 0) {
        --utf16;
    }
    this->ensureUTF16Mapping();
    if (fUTF8IndexForUTF16Index.size() > 1 && utf16 + 1 >= fUTF8IndexForUTF16Index.size()) {
        utf16 = fUTF8IndexForUTF16Index.size() - 2;
    }
    return getVerticalGlyphInfoAtUTF16Offset(utf16, glyphInfo);
}

} // namespace textlayout
} // namespace skia

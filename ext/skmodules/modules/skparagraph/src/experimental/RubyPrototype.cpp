#include "modules/skparagraph/src/experimental/RubyPrototype.h"
#include "include/core/SkCanvas.h"
#include "modules/skparagraph/src/ParagraphBuilderImpl.h"
#include <cmath>
#include <set>
#include <stdexcept>

namespace skia::textlayout {

ParagraphImpl& RubyPrototype::shapedBase() const {
    return static_cast<ParagraphImpl&>(*fBase);
}

bool RubyPrototype::rawBoundary(size_t offset) const {
    return offset < static_cast<size_t>(fRawFlags.size()) &&
           (fRawFlags[offset] & SkUnicode::CodeUnitFlags::kGraphemeStart) != SkUnicode::CodeUnitFlags::kNoCodeUnitFlag;
}

std::vector<SkPoint> RubyPrototype::points(const PrototypeSlice& slice) {
    std::vector<SkPoint> result;
    for (size_t glyph = slice.glyphStart; glyph < slice.glyphEnd; ++glyph) {
        auto point = slice.run->positions()[glyph] + slice.run->offsets()[glyph];
        point.fX -= slice.run->posX(slice.glyphStart);
        result.push_back(point);
    }
    return result;
}

SkRect RubyPrototype::inkBounds(const PrototypeSlice& slice) {
    auto positions = points(slice);
    std::vector<SkRect> bounds(positions.size());
    slice.run->font().getBounds(SkSpan(slice.run->glyphs().data() + slice.glyphStart, positions.size()),
                                bounds,
                                nullptr);
    SkRect ink = SkRect::MakeEmpty();
    for (size_t glyph = 0; glyph < bounds.size(); ++glyph) {
        bounds[glyph].offset(positions[glyph]);
        ink.join(bounds[glyph]);
    }
    return ink;
}

PrototypeShape RubyPrototype::shapeRange(ParagraphImpl& paragraph, TextRange range) {
    PrototypeShape result;
    for (auto& cluster: paragraph.clusters()) {
        if (cluster.textRange().width() == 0 || !cluster.belongs(range))
            continue;
        auto& run = cluster.run();
        if (!run.leftToRight() || run.isPlaceholder()) {
            throw std::runtime_error("prototype supports horizontal LTR glyph runs only");
        }
        PrototypeSlice slice{&run, cluster.textRange(), cluster.startPos(), cluster.endPos(), result.advance};
        auto ink = inkBounds(slice);
        result.inlineStart = std::min(result.inlineStart, result.advance + ink.left());
        result.inlineEnd = std::max(result.inlineEnd, result.advance + ink.right());
        result.blockStart = std::min({result.blockStart, run.ascent(), ink.top()});
        result.blockEnd = std::max({result.blockEnd, run.descent(), ink.bottom()});
        result.slices.push_back(slice);
        result.advance += cluster.width();
    }
    result.inlineEnd = std::max(result.inlineEnd, result.advance);
    return result;
}

RubyPrototype::RubyPrototype(std::unique_ptr<Paragraph> base,
                             std::vector<PrototypeRubyInput> inputs,
                             const ParagraphStyle& style,
                             sk_sp<FontCollection> fonts,
                             sk_sp<SkUnicode> unicode)
    : fBase(std::move(base)) {
    auto& paragraph = shapedBase();
    std::string original(paragraph.text().data(), paragraph.text().size());
    if (original.find_first_of("\n\r\t") != std::string::npos || original.empty()) {
        throw std::runtime_error("prototype requires nonempty single-paragraph input without tabs");
    }
    if (!unicode->computeCodeUnitFlags(original.data(), original.size(), false, &fRawFlags)) {
        throw std::runtime_error("Unicode analysis failed");
    }
    paragraph.layout(std::numeric_limits<SkScalar>::infinity());
    if (paragraph.unresolvedGlyphs() != 0)
        throw std::runtime_error("unresolved base glyphs");
    std::set<size_t> glyphBoundaries{original.size()};
    for (auto& cluster: paragraph.clusters()) glyphBoundaries.insert(cluster.textRange().start);
    size_t previousEnd = 0;
    for (auto& input: inputs) {
        auto range = input.baseRange;
        if (range.start < previousEnd || range.start >= range.end || range.end > original.size()) {
            throw std::runtime_error("invalid or overlapping ruby range");
        }
        if (!rawBoundary(range.start) || !rawBoundary(range.end)) {
            throw std::runtime_error("raw-grapheme boundary rejected");
        }
        if (!glyphBoundaries.contains(range.start) || !glyphBoundaries.contains(range.end)) {
            throw std::runtime_error("shaped-cluster boundary rejected");
        }
        if (input.annotation.empty())
            throw std::runtime_error("empty annotation unsupported");
        previousEnd = range.end;
    }
    size_t inputIndex = 0;
    auto clusters = paragraph.clusters();
    for (size_t clusterIndex = 0; clusterIndex + 1 < clusters.size();) {
        auto range = clusters[clusterIndex].textRange();
        bool hasRuby = inputIndex < inputs.size() && inputs[inputIndex].baseRange.start == range.start;
        if (hasRuby)
            range = inputs[inputIndex].baseRange;
        PrototypeUnit unit;
        unit.text = range;
        unit.base = shapeRange(paragraph, range);
        unit.hasRuby = hasRuby;
        const auto baseExtent = unit.base.inlineEnd - unit.base.inlineStart;
        unit.inlineExtent = unit.base.advance;
        unit.before = -unit.base.blockStart;
        unit.after = unit.base.blockEnd;
        if (hasRuby) {
            unit.oversizePlacement = inputs[inputIndex].oversizePlacement;
            auto annotationStyle = style;
            auto textStyle = style.getTextStyle();
            textStyle.setFontSize(textStyle.getFontSize() * 0.5f);
            annotationStyle.setTextStyle(textStyle);
            ParagraphBuilderImpl builder(annotationStyle, fonts, unicode);
            const auto& text = inputs[inputIndex++].annotation;
            builder.addText(text.data(), text.size());
            auto annotation = builder.Build();
            annotation->layout(std::numeric_limits<SkScalar>::infinity());
            if (annotation->unresolvedGlyphs() != 0)
                throw std::runtime_error("unresolved annotation glyphs");
            unit.annotation = shapeRange(static_cast<ParagraphImpl&>(*annotation), {0, text.size()});
            unit.inlineExtent = std::max(baseExtent, unit.annotation.inlineEnd - unit.annotation.inlineStart);
            unit.before += 2 + unit.annotation.blockEnd - unit.annotation.blockStart;
            fAnnotations.push_back(std::move(annotation));
        }
        auto flags = fRawFlags[range.end];
        unit.breakAfter = range.end == original.size() || SkUnicode::hasSoftLineBreakFlag(flags);
        fUnits.push_back(std::move(unit));
        while (clusterIndex + 1 < clusters.size() && clusters[clusterIndex].textRange().end <= range.end) {
            ++clusterIndex;
        }
    }
}

SkScalar RubyPrototype::maxIntrinsic() const {
    SkScalar result = 0;
    for (const auto& unit: fUnits) result += unit.inlineExtent;
    return result;
}

SkScalar RubyPrototype::minIntrinsic() const {
    SkScalar result = 0;
    SkScalar group = 0;
    for (const auto& unit: fUnits) {
        group += unit.inlineExtent;
        if (unit.breakAfter) {
            result = std::max(result, group);
            group = 0;
        }
    }
    return std::max(result, group);
}

void RubyPrototype::place(const PrototypeShape& shape, bool annotation, SkPoint origin) {
    for (const auto& slice: shape.slices) {
        auto position = origin + SkPoint::Make(slice.inlineOffset, 0);
        auto ink = inkBounds(slice);
        ink.offset(position);
        fPlacements.push_back({slice, annotation, position, ink});
    }
}

void RubyPrototype::layout(SkScalar inlineLimit) {
    if (std::isnan(inlineLimit) || inlineLimit < 0)
        throw std::runtime_error("invalid inline limit");
    fLines.clear();
    fPlacements.clear();
    size_t lineStart = 0;
    SkScalar lineAdvance = 0;
    auto emit = [&](size_t lineEnd) {
        SkScalar before = 0;
        SkScalar after = 0;
        for (size_t index = lineStart; index < lineEnd; ++index) {
            before = std::max(before, fUnits[index].before);
            after = std::max(after, fUnits[index].after);
        }
        SkScalar baseline = height() + before;
        fLines.push_back({lineStart, lineEnd, lineAdvance, baseline, baseline + after});
        SkScalar position = 0;
        for (size_t index = lineStart; index < lineEnd; ++index) {
            const auto& unit = fUnits[index];
            auto shift = unit.hasRuby ? (unit.inlineExtent - unit.base.inlineEnd + unit.base.inlineStart) / 2 -
                                                unit.base.inlineStart
                                      : 0;
            SkScalar oversizeShift = 0;
            if (unit.hasRuby && unit.inlineExtent > inlineLimit &&
                unit.oversizePlacement == PrototypeOversizePlacement::BaseVisible) {
                auto baseExtent = unit.base.inlineEnd - unit.base.inlineStart;
                auto anchor = std::max(0.f, (inlineLimit - baseExtent) / 2);
                oversizeShift = anchor - position - shift - unit.base.inlineStart;
            }
            place(unit.base, false, {position + shift + oversizeShift, baseline});
            if (unit.hasRuby) {
                auto annotationShift =
                        (unit.inlineExtent - unit.annotation.inlineEnd + unit.annotation.inlineStart) / 2 -
                        unit.annotation.inlineStart;
                auto annotationBaseline = baseline + unit.base.blockStart - 2 - unit.annotation.blockEnd;
                place(unit.annotation, true, {position + annotationShift + oversizeShift, annotationBaseline});
            }
            position += unit.inlineExtent;
        }
        lineStart = lineEnd;
        lineAdvance = 0;
    };
    for (size_t groupStart = 0; groupStart < fUnits.size();) {
        size_t groupEnd = groupStart;
        SkScalar advance = 0;
        do {
            advance += fUnits[groupEnd].inlineExtent;
            ++groupEnd;
        } while (groupEnd < fUnits.size() && !fUnits[groupEnd - 1].breakAfter);
        if (groupStart > lineStart && lineAdvance + advance > inlineLimit)
            emit(groupStart);
        lineAdvance += advance;
        groupStart = groupEnd;
    }
    if (lineStart < fUnits.size())
        emit(fUnits.size());
}

void RubyPrototype::paint(SkCanvas* canvas) const {
    SkPaint paint;
    paint.setColor(SK_ColorBLACK);
    paint.setAntiAlias(true);
    for (const auto& placement: fPlacements) {
        auto positions = points(placement.slice);
        auto glyphs = SkSpan(placement.slice.run->glyphs().data() + placement.slice.glyphStart, positions.size());
        canvas->drawGlyphs(glyphs, positions, placement.origin, placement.slice.run->font(), paint);
    }
}

std::vector<SkRect> RubyPrototype::baseRects(TextRange bytes) const {
    std::vector<SkRect> result;
    for (const auto& placement: fPlacements) {
        if (!placement.annotation && placement.slice.text.start < bytes.end && placement.slice.text.end > bytes.start) {
            result.push_back(placement.ink);
        }
    }
    return result;
}

}

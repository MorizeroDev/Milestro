#include "modules/skparagraph/src/experimental/RubyPrototype.h"
#include "include/core/SkCanvas.h"
#include "modules/skparagraph/src/ParagraphBuilderImpl.h"
#include <algorithm>
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

SkUnicode::BidiLevel RubyPrototype::bidiLevelAt(size_t offset) const {
    for (const auto& region: fBaseBidi) {
        if (region.start <= offset && offset < region.end)
            return region.level;
    }
    throw std::runtime_error("missing bidi region");
}

PrototypeShape RubyPrototype::shapeRange(ParagraphImpl& paragraph, TextRange range) const {
    PrototypeShape result;
    std::vector<SkUnicode::BidiRegion> annotationBidi;
    const auto* regions = &fBaseBidi;
    if (&paragraph != &shapedBase()) {
        auto text = paragraph.text();
        auto direction = paragraph.paragraphStyle().getTextDirection() == TextDirection::kLtr
                                 ? SkUnicode::TextDirection::kLTR
                                 : SkUnicode::TextDirection::kRTL;
        if (!fUnicode->getBidiRegions(text.data(), text.size(), direction, &annotationBidi))
            throw std::runtime_error("annotation bidi analysis failed");
        regions = &annotationBidi;
    }
    struct VisualCluster {
        PrototypeSlice slice;
        SkScalar advance;
        SkUnicode::BidiLevel level;
    };
    std::vector<VisualCluster> visualClusters;
    for (auto& cluster: paragraph.clusters()) {
        if (cluster.textRange().width() == 0 || !cluster.belongs(range))
            continue;
        auto& run = cluster.run();
        if (run.isPlaceholder())
            throw std::runtime_error("prototype does not support placeholders");
        auto region = std::find_if(regions->begin(), regions->end(), [&](const auto& candidate) {
            return candidate.start <= cluster.textRange().start && cluster.textRange().start < candidate.end;
        });
        if (region == regions->end())
            throw std::runtime_error("missing cluster bidi region");
        visualClusters.push_back({{&run, cluster.textRange(), cluster.startPos(), cluster.endPos(), 0},
                                  cluster.width(), region->level});
    }
    std::vector<SkUnicode::BidiLevel> levels;
    std::vector<int32_t> visualOrder(visualClusters.size());
    for (const auto& cluster: visualClusters) levels.push_back(cluster.level);
    if (!levels.empty()) fUnicode->reorderVisual(levels.data(), levels.size(), visualOrder.data());
    for (auto visualIndex: visualOrder) {
        auto slice = visualClusters[visualIndex].slice;
        slice.inlineOffset = result.advance;
        auto ink = inkBounds(slice);
        result.inlineStart = std::min(result.inlineStart, result.advance + ink.left());
        result.inlineEnd = std::max(result.inlineEnd, result.advance + ink.right());
        result.blockStart = std::min({result.blockStart, slice.run->ascent(), ink.top()});
        result.blockEnd = std::max({result.blockEnd, slice.run->descent(), ink.bottom()});
        result.slices.push_back(slice);
        result.advance += visualClusters[visualIndex].advance;
    }
    result.inlineEnd = std::max(result.inlineEnd, result.advance);
    return result;
}

RubyPrototype::RubyPrototype(std::unique_ptr<Paragraph> base,
                             std::vector<PrototypeRubyInput> inputs,
                             const ParagraphStyle& style,
                             sk_sp<FontCollection> fonts,
                             sk_sp<SkUnicode> unicode)
    : fBase(std::move(base)), fUnicode(std::move(unicode)) {
    auto& paragraph = shapedBase();
    std::string original(paragraph.text().data(), paragraph.text().size());
    if (original.find('\t') != std::string::npos || original.empty()) {
        throw std::runtime_error("prototype requires nonempty input without tabs");
    }
    if (!fUnicode->computeCodeUnitFlags(original.data(), original.size(), false, &fRawFlags)) {
        throw std::runtime_error("Unicode analysis failed");
    }
    auto direction = style.getTextDirection() == TextDirection::kLtr
                             ? SkUnicode::TextDirection::kLTR
                             : SkUnicode::TextDirection::kRTL;
    if (!fUnicode->getBidiRegions(original.data(), original.size(), direction, &fBaseBidi))
        throw std::runtime_error("bidi analysis failed");
    if (!SkUnicode::extractUtfConversionMapping(
                SkSpan<const char>(original.data(), original.size()),
                [&](size_t offset) { fUtf8ForUtf16.push_back(offset); },
                [&](size_t) {}))
        throw std::runtime_error("UTF16 mapping failed");
    paragraph.layout(std::numeric_limits<SkScalar>::infinity());
    if (paragraph.unresolvedGlyphs() != 0)
        throw std::runtime_error("unresolved base glyphs");
    if (!paragraph.lines().empty()) {
        auto metrics = paragraph.lines().back().sizes();
        fEmptyBefore = -metrics.ascent();
        fEmptyAfter = metrics.descent();
    } else if (!paragraph.runs().empty()) {
        auto& run = paragraph.runs().back();
        fEmptyBefore = -run.ascent();
        fEmptyAfter = run.descent();
    }
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
        for (size_t offset = range.start + 1; offset <= range.end; ++offset) {
            if (SkUnicode::hasHardLineBreakFlag(fRawFlags[offset]))
                throw std::runtime_error("ruby range crosses hard line break");
            if (offset < range.end && bidiLevelAt(offset) != bidiLevelAt(range.start))
                throw std::runtime_error("ruby range crosses bidi levels");
        }
        if (input.annotation.empty() || input.annotation.find('\t') != std::string::npos)
            throw std::runtime_error("annotation must be nonempty single-line text without tabs");
        skia_private::TArray<SkUnicode::CodeUnitFlags, true> annotationFlags;
        if (!fUnicode->computeCodeUnitFlags(input.annotation.data(), input.annotation.size(), false,
                                            &annotationFlags))
            throw std::runtime_error("annotation Unicode analysis failed");
        for (auto flags: annotationFlags) {
            if (SkUnicode::hasHardLineBreakFlag(flags))
                throw std::runtime_error("annotation must be nonempty single-line text without tabs");
        }
        previousEnd = range.end;
    }
    size_t inputIndex = 0;
    auto clusters = paragraph.clusters();
    for (size_t clusterIndex = 0; clusterIndex + 1 < clusters.size();) {
        auto range = clusters[clusterIndex].textRange();
        bool hasRuby = inputIndex < inputs.size() && inputs[inputIndex].baseRange.start == range.start;
        if (hasRuby) {
            range = inputs[inputIndex].baseRange;
        } else {
            size_t nextCluster = clusterIndex;
            while (range.end < original.size() && !rawBoundary(range.end)) {
                ++nextCluster;
                if (nextCluster + 1 >= clusters.size())
                    throw std::runtime_error("incomplete shaped grapheme");
                range.end = clusters[nextCluster].textRange().end;
            }
        }
        PrototypeUnit unit;
        unit.text = range;
        unit.base = shapeRange(paragraph, range);
        unit.hasRuby = hasRuby;
        unit.bidiLevel = bidiLevelAt(range.start);
        unit.whitespace = !hasRuby;
        for (size_t index = clusterIndex;
             index + 1 < clusters.size() && clusters[index].textRange().end <= range.end;
             ++index)
            unit.whitespace &= clusters[index].isWhitespaceBreak();
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
            ParagraphBuilderImpl builder(annotationStyle, fonts, fUnicode);
            const auto& text = inputs[inputIndex++].annotation;
            builder.addText(text.data(), text.size());
            auto annotation = builder.Build();
            annotation->layout(std::numeric_limits<SkScalar>::infinity());
            if (annotation->unresolvedGlyphs() != 0)
                throw std::runtime_error("unresolved annotation glyphs");
            unit.annotation = shapeRange(static_cast<ParagraphImpl&>(*annotation), {0, text.size()});
            unit.inlineExtent = std::max({unit.base.advance, baseExtent,
                                          unit.annotation.inlineEnd - unit.annotation.inlineStart});
            unit.before += 2 + unit.annotation.blockEnd - unit.annotation.blockStart;
            fAnnotations.push_back(std::move(annotation));
        }
        auto flags = fRawFlags[range.end];
        unit.hardBreakAfter = SkUnicode::hasHardLineBreakFlag(flags);
        unit.breakAfter = range.end == original.size() ||
                          unit.hardBreakAfter || SkUnicode::hasSoftLineBreakFlag(flags);
        fUnits.push_back(std::move(unit));
        while (clusterIndex + 1 < clusters.size() && clusters[clusterIndex].textRange().end <= range.end) {
            ++clusterIndex;
        }
    }
}

SkScalar RubyPrototype::maxIntrinsic() const {
    SkScalar result = 0;
    SkScalar line = 0;
    for (const auto& unit: fUnits) {
        line += unit.inlineExtent;
        if (unit.hardBreakAfter) {
            result = std::max(result, line);
            line = 0;
        }
    }
    result = std::max(result, line);
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
    auto emit = [&](size_t lineEnd, SkScalar lineAdvance, bool hardBreak) {
        SkScalar trailingAdvance = 0;
        for (size_t index = lineEnd; index > lineStart && fUnits[index - 1].whitespace; --index)
            trailingAdvance += fUnits[index - 1].inlineExtent;
        lineAdvance -= trailingAdvance;
        lineAdvance = std::max(0.f, lineAdvance);
        SkScalar before = lineStart == lineEnd ? fEmptyBefore : 0;
        SkScalar after = lineStart == lineEnd ? fEmptyAfter : 0;
        for (size_t index = lineStart; index < lineEnd; ++index) {
            before = std::max(before, fUnits[index].before);
            after = std::max(after, fUnits[index].after);
        }
        SkScalar baseline = height() + before;
        fLines.push_back({lineStart, lineEnd, lineAdvance, baseline, baseline + after, hardBreak});
        std::vector<SkUnicode::BidiLevel> levels;
        std::vector<int32_t> visualOrder(lineEnd - lineStart);
        for (size_t index = lineStart; index < lineEnd; ++index)
            levels.push_back(fUnits[index].bidiLevel);
        auto baseLevel = shapedBase().paragraphStyle().getTextDirection() == TextDirection::kLtr ? 0 : 1;
        for (size_t index = lineEnd; index > lineStart && fUnits[index - 1].whitespace; --index)
            levels[index - lineStart - 1] = baseLevel;
        if (!levels.empty()) fUnicode->reorderVisual(levels.data(), levels.size(), visualOrder.data());
        SkScalar position = baseLevel == 1 ? -trailingAdvance : 0;
        for (auto visualIndex: visualOrder) {
            size_t index = lineStart + visualIndex;
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
    };
    size_t cursor = 0;
    size_t lastBreak = 0;
    SkScalar lineAdvance = 0;
    SkScalar lastBreakAdvance = 0;
    if (fUnits.empty()) {
        emit(0, 0, false);
        return;
    }
    while (cursor < fUnits.size()) {
        const auto& unit = fUnits[cursor];
        if (cursor > lineStart && unit.inlineExtent > 0 && !unit.whitespace &&
            lineAdvance + unit.inlineExtent > inlineLimit) {
            if (lastBreak > lineStart) {
                cursor = lastBreak;
                emit(lastBreak, lastBreakAdvance, false);
            } else {
                emit(cursor, lineAdvance, false);
            }
            lineAdvance = 0;
            lastBreak = lineStart;
            lastBreakAdvance = 0;
            continue;
        }
        lineAdvance += unit.inlineExtent;
        ++cursor;
        if (unit.hardBreakAfter) {
            emit(cursor, lineAdvance, true);
            lineAdvance = 0;
            lastBreak = lineStart;
            lastBreakAdvance = 0;
        } else if (unit.breakAfter) {
            lastBreak = cursor;
            lastBreakAdvance = lineAdvance;
        }
    }
    if (lineStart < fUnits.size() || fUnits.back().hardBreakAfter)
        emit(fUnits.size(), lineAdvance, false);
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

std::vector<SkRect> RubyPrototype::baseRectsUtf16(TextRange codeUnits) const {
    if (codeUnits.start >= codeUnits.end || codeUnits.end >= fUtf8ForUtf16.size())
        return {};
    size_t start = fUtf8ForUtf16[codeUnits.start];
    size_t end = fUtf8ForUtf16[codeUnits.end];
    if (codeUnits.start > 0 && fUtf8ForUtf16[codeUnits.start - 1] == start)
        start = fUtf8ForUtf16[codeUnits.start + 1];
    while (start < end && !rawBoundary(start)) ++start;
    while (end > start && !rawBoundary(end)) --end;
    return baseRects({start, end});
}

bool RubyPrototype::hitTestRangeUtf16(TextRange codeUnits, SkPoint point) const {
    if (!std::isfinite(point.x()) || !std::isfinite(point.y())) return false;
    for (const auto& rect: baseRectsUtf16(codeUnits)) {
        if (rect.contains(point.x(), point.y())) return true;
    }
    return false;
}

}

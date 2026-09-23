#include "modules/skparagraph/src/ParagraphImpl.h"
#include "modules/skparagraph/src/ParagraphBuilderImpl.h"
#include "modules/skparagraph/src/TextWrapper.h"
#include <algorithm>
#include <cmath>
#include <stdexcept>
#include <unicode/uchar.h>

namespace skia::textlayout {

namespace {

SkRect paragraphInk(Paragraph& paragraph) {
    SkRect result = SkRect::MakeEmpty();
    paragraph.visit([&](int, const Paragraph::VisitorInfo* info) {
        if (!info) return;
        std::vector<SkRect> bounds(info->count);
        info->font.getBounds({info->glyphs, static_cast<size_t>(info->count)}, bounds, nullptr);
        for (int index = 0; index < info->count; ++index) {
            bounds[index].offset(info->origin + info->positions[index]);
            result.join(bounds[index]);
        }
    });
    return result;
}

bool emergencyBoundary(int before, int after) {
    return before != U_LB_OPEN_PUNCTUATION && before != U_LB_QUOTATION &&
           before != U_LB_GLUE && before != U_LB_WORD_JOINER &&
           after != U_LB_CLOSE_PUNCTUATION && after != U_LB_CLOSE_PARENTHESIS &&
           after != U_LB_EXCLAMATION && after != U_LB_INFIX_NUMERIC &&
           after != U_LB_NONSTARTER && after != U_LB_BREAK_SYMBOLS &&
           after != U_LB_QUOTATION && after != U_LB_GLUE && after != U_LB_WORD_JOINER &&
           after != U_LB_COMBINING_MARK && after != U_LB_ZWJ;
}

SkScalar inlineBefore(RubyAlignment alignment, SkScalar extra) {
    switch (alignment) {
        case RubyAlignment::Start:
            return 0;
        case RubyAlignment::Center:
            return extra / 2;
        case RubyAlignment::End:
            return extra;
        case RubyAlignment::SpaceAround:
        case RubyAlignment::SpaceBetween:
            return extra / 2;
    }
    return extra / 2;
}

bool validRubyStyle(const RubyStyle& style) {
    if (!SkIsFinite(style.fontSizeMultiplier) || style.fontSizeMultiplier <= 0 || !SkIsFinite(style.gap) ||
        style.gap < 0)
        return false;
    switch (style.position) {
        case RubyPosition::Above:
        case RubyPosition::Below:
            break;
        default:
            return false;
    }
    switch (style.alignment) {
        case RubyAlignment::Start:
        case RubyAlignment::Center:
        case RubyAlignment::End:
        case RubyAlignment::SpaceAround:
        case RubyAlignment::SpaceBetween:
            break;
        default:
            return false;
    }
    switch (style.overhang) {
        case RubyOverhang::None:
        case RubyOverhang::Allow:
            break;
        default:
            return false;
    }
    switch (style.overflow) {
        case RubyOverflow::PreserveBase:
        case RubyOverflow::Inline:
            return true;
        default:
            return false;
    }
}

}

void ParagraphImpl::setRubyAnnotations(std::vector<RubyAnnotation> annotations) {
    std::sort(annotations.begin(), annotations.end(), [](const auto& first, const auto& second) {
        return first.baseRange.start < second.baseRange.start;
    });
    size_t previousEnd = 0;
    for (const auto& annotation : annotations) {
        const auto range = annotation.baseRange;
        if (range.start < previousEnd || range.start >= range.end || range.end > fText.size())
            throw std::invalid_argument("invalid or overlapping ruby range");
        if (annotation.text.empty() || annotation.text.find('\t') != std::string::npos)
            throw std::invalid_argument("ruby annotation must be nonempty single-line text without tabs");
        if (!validRubyStyle(annotation.style))
            throw std::invalid_argument("invalid ruby style");
        previousEnd = range.end;
    }
    fRubyAnnotations = std::move(annotations);
    fRubyPrepared = false;
    this->markDirty();
}

void ParagraphImpl::prepareRuby() {
    auto original = std::string(fText.c_str(), fText.size());
    skia_private::TArray<SkUnicode::CodeUnitFlags, true> rawFlags;
    if (!fUnicode->computeCodeUnitFlags(original.data(), original.size(), false, &rawFlags))
        throw std::runtime_error("ruby Unicode analysis failed");
    fRubySpans.clear();
    fRubyUnits.clear();
    std::vector<SkScalar> leading(fClusters.size(), 0);
    std::vector<SkScalar> trailing(fClusters.size(), 0);
    for (const auto& input : fRubyAnnotations) {
        const auto range = input.baseRange;
        if (!SkUnicode::hasGraphemeStartFlag(rawFlags[range.start]) ||
            !SkUnicode::hasGraphemeStartFlag(rawFlags[range.end]))
            throw std::invalid_argument("ruby raw-grapheme boundary rejected");
        auto first = clusterIndex(range.start);
        auto last = clusterIndex(range.end - 1) + 1;
        if (fClusters[first].textRange().start != range.start ||
            fClusters[last - 1].textRange().end != range.end)
            throw std::invalid_argument("ruby shaped-cluster boundary rejected");
        auto level = fClusters[first].run().fBidiLevel;
        bool ideographic = false;
        for (size_t index = first; index < last; ++index) {
            if (fClusters[index].isHardBreak())
                throw std::invalid_argument("ruby range crosses hard line break");
            if (fClusters[index].run().isPlaceholder())
                throw std::invalid_argument("ruby range contains placeholder");
            if (fClusters[index].run().fBidiLevel != level)
                throw std::invalid_argument("ruby range crosses bidi levels");
            ideographic |= fClusters[index].isIdeographic();
        }
        fUnicode->forEachCodepoint(original.data() + range.start, range.width(),
            [&](SkUnichar codepoint, int, int, int) {
                if (u_hasBinaryProperty(codepoint, UCHAR_BIDI_CONTROL))
                    throw std::invalid_argument("ruby range contains bidi controls");
            });
        auto annotationText = input.text;
        skia_private::TArray<SkUnicode::CodeUnitFlags, true> annotationFlags;
        if (!fUnicode->computeCodeUnitFlags(annotationText.data(), annotationText.size(), false,
                                           &annotationFlags))
            throw std::runtime_error("ruby annotation Unicode analysis failed");
        for (auto flags : annotationFlags) {
            if (SkUnicode::hasHardLineBreakFlag(flags))
                throw std::invalid_argument("ruby annotation must be single-line");
        }
        auto textStyle = input.style.textStyle.value_or(fTextStyles[findAllBlocks(range).start].fStyle);
        textStyle.setFontSize(textStyle.getFontSize() * input.style.fontSizeMultiplier);
        auto buildAnnotation = [&](const TextStyle& annotationTextStyle) {
            auto annotationStyle = fParagraphStyle;
            annotationStyle.setTextStyle(annotationTextStyle);
            annotationStyle.setTextAlign(TextAlign::kLeft);
            annotationStyle.setMaxLines(std::numeric_limits<size_t>::max());
            annotationStyle.setEllipsis(SkString());
            annotationStyle.setEllipsis(std::u16string());
            annotationStyle.setStrutStyle(StrutStyle());
            ParagraphBuilderImpl builder(annotationStyle, fFontCollection, fUnicode);
            builder.addText(input.text.data(), input.text.size());
            auto paragraph = builder.Build();
            paragraph->layout(std::numeric_limits<SkScalar>::infinity());
            auto bounds = paragraphInk(*paragraph);
            bounds.fLeft = std::min(0.f, bounds.left());
            bounds.fRight = std::max(paragraph->getMaxIntrinsicWidth(), bounds.right());
            bounds.fTop = std::min(0.f, bounds.top());
            bounds.fBottom = std::max(paragraph->getHeight(), bounds.bottom());
            return std::make_pair(std::move(paragraph), bounds);
        };
        auto [annotation, annotationBounds] = buildAnnotation(textStyle);
        SkScalar advance = 0;
        SkScalar baseTop = 0;
        SkScalar baseBottom = 0;
        SkRect baseInk = SkRect::MakeEmpty();
        for (size_t visual = 0; visual < last - first; ++visual) {
            auto index = level % 2 == 0 ? first + visual : last - visual - 1;
            const auto& cluster = fClusters[index];
            const auto& run = cluster.run();
            baseTop = std::min(baseTop, run.correctAscent());
            baseBottom = std::max(baseBottom, run.correctDescent());
            std::vector<SkRect> bounds(cluster.size());
            run.font().getBounds(run.glyphs().subspan(cluster.startPos(), cluster.size()), bounds, nullptr);
            for (size_t glyph = 0; glyph < cluster.size(); ++glyph) {
                auto position = run.positions()[cluster.startPos() + glyph] + run.offsets()[cluster.startPos() + glyph];
                position.fX += advance - run.posX(cluster.startPos());
                bounds[glyph].offset(position);
                baseInk.join(bounds[glyph]);
            }
            advance += cluster.width();
        }
        baseTop = std::min(baseTop, baseInk.top());
        baseBottom = std::max(baseBottom, baseInk.bottom());
        auto baseLeft = std::min(0.f, baseInk.left());
        auto baseWidth = std::max(advance, baseInk.right()) - baseLeft;
        auto distributed = input.style.alignment == RubyAlignment::SpaceAround ||
                           input.style.alignment == RubyAlignment::SpaceBetween;
        size_t annotationGraphemes = 0;
        for (size_t index = 0; index < input.text.size(); ++index) {
            if (SkUnicode::hasGraphemeStartFlag(annotationFlags[index]))
                ++annotationGraphemes;
        }
        auto canDistribute =
                input.style.alignment == RubyAlignment::SpaceAround ? annotationGraphemes > 0 : annotationGraphemes > 1;
        if (distributed && canDistribute && annotationBounds.width() < baseWidth) {
            auto trialStyle = textStyle;
            trialStyle.setLetterSpacing(textStyle.getLetterSpacing() + 1);
            auto [trial, trialBounds] = buildAnnotation(trialStyle);
            auto unitWidth = trialBounds.width() - annotationBounds.width();
            if (unitWidth > 0) {
                textStyle.setLetterSpacing(textStyle.getLetterSpacing() +
                                           (baseWidth - annotationBounds.width()) / unitWidth);
                std::tie(annotation, annotationBounds) = buildAnnotation(textStyle);
            }
        }
        auto width =
                input.style.overhang == RubyOverhang::Allow ? baseWidth : std::max(baseWidth, annotationBounds.width());
        auto baseExtra = width - baseWidth;
        auto leftToRight = level % 2 == 0;
        auto visualIndex = [&](size_t index) {
            return leftToRight ? first + index : last - index - 1;
        };
        auto safetyLeft = -baseLeft;
        auto safetyRight = baseWidth - advance - safetyLeft;
        auto safetyBefore = leftToRight ? safetyLeft : safetyRight;
        auto safetyAfter = leftToRight ? safetyRight : safetyLeft;
        auto baseBefore = inlineBefore(input.style.alignment, baseExtra);
        auto leftPad = (leftToRight ? baseBefore : baseExtra - baseBefore) + safetyLeft;
        if (input.style.alignment == RubyAlignment::SpaceBetween && last - first > 1) {
            leading[visualIndex(0)] += safetyBefore;
            trailing[visualIndex(last - first - 1)] += safetyAfter;
            auto spacing = baseExtra / static_cast<SkScalar>(last - first - 1);
            for (size_t index = 0; index + 1 < last - first; ++index) trailing[visualIndex(index)] += spacing;
            leftPad = safetyLeft;
        } else if (input.style.alignment == RubyAlignment::SpaceAround) {
            auto spacing = baseExtra / static_cast<SkScalar>(last - first);
            leading[visualIndex(0)] += safetyBefore;
            trailing[visualIndex(last - first - 1)] += safetyAfter;
            for (size_t index = 0; index < last - first; ++index) {
                leading[visualIndex(index)] += spacing / 2;
                trailing[visualIndex(index)] += spacing / 2;
            }
            leftPad = safetyLeft + spacing / 2;
        } else {
            auto rightPad = width - advance - leftPad;
            leading[visualIndex(0)] += leftToRight ? leftPad : rightPad;
            trailing[visualIndex(last - first - 1)] += leftToRight ? rightPad : leftPad;
        }
        auto annotationExtra = width - annotationBounds.width();
        auto annotationBefore = inlineBefore(input.style.alignment, annotationExtra);
        auto annotationLeft = leftToRight ? annotationBefore : annotationExtra - annotationBefore;
        fRubySpans.push_back({input,
                              {first, last},
                              std::move(annotation),
                              annotationBounds,
                              baseTop,
                              baseBottom,
                              baseLeft + leftPad,
                              baseWidth,
                              width,
                              annotationLeft,
                              ideographic});
    }
    skia_private::TArray<Run, false> detached;
    for (const auto& run : fRuns) detached.emplace_back(run, true);
    fRuns = std::move(detached);
    for (auto& run : fRuns) {
        SkScalar shift = 0;
        run.iterateThroughClusters([&](Cluster* cluster) {
            auto index = static_cast<size_t>(cluster - fClusters.begin());
            for (size_t glyph = cluster->startPos(); glyph < cluster->endPos(); ++glyph) {
                run.fPositions[glyph].fX += shift;
                run.fOffsets[glyph].fX += leading[index];
            }
            shift += leading[index] + trailing[index];
            cluster->space(leading[index] + trailing[index]);
        });
        run.fPositions[run.size()].fX += shift;
        run.fAdvance.fX += shift;
    }
    fRubyOffsets.clear();
    for (const auto& run : fRuns)
        fRubyOffsets.emplace_back(run.offsets().begin(), run.offsets().end());
    std::vector<int> before(original.size() + 1, U_LB_UNKNOWN);
    std::vector<int> after(original.size() + 1, U_LB_UNKNOWN);
    fUnicode->forEachCodepoint(original.data(), original.size(), [&](SkUnichar codepoint, int start, int end, int) {
        auto property = u_getIntPropertyValue(codepoint, UCHAR_LINE_BREAK);
        before[end] = property;
        after[start] = property;
    });
    size_t spanIndex = 0;
    for (size_t first = 0; first + 1 < static_cast<size_t>(fClusters.size());) {
        size_t last = first + 1;
        bool ruby = spanIndex < fRubySpans.size() && fRubySpans[spanIndex].clusters.start == first;
        if (ruby) {
            last = fRubySpans[spanIndex++].clusters.end;
        } else {
            while (last + 1 < static_cast<size_t>(fClusters.size()) &&
                   !SkUnicode::hasGraphemeStartFlag(rawFlags[fClusters[last - 1].textRange().end])) ++last;
        }
        SkScalar width = 0;
        bool whitespace = !ruby;
        for (size_t index = first; index < last; ++index) {
            width += fClusters[index].width();
            whitespace &= fClusters[index].isWhitespaceBreak();
        }
        auto end = fClusters[last - 1].textRange().end;
        fRubyUnits.push_back({{first, last}, width, whitespace,
                             SkUnicode::hasSoftLineBreakFlag(rawFlags[end]),
                             SkUnicode::hasHardLineBreakFlag(rawFlags[end]),
                             emergencyBoundary(before[end], after[end])});
        first = last;
    }
    fRubyPrepared = true;
}

const RubySpan* ParagraphImpl::rubySpanAt(TextIndex index) const {
    auto span = std::upper_bound(fRubySpans.begin(), fRubySpans.end(), index,
        [](TextIndex position, const RubySpan& value) { return position < value.input.baseRange.start; });
    if (span == fRubySpans.begin()) return nullptr;
    --span;
    return index < span->input.baseRange.end ? &*span : nullptr;
}

bool ParagraphImpl::rubyBoundary(ClusterIndex index) const {
    auto unit = std::lower_bound(fRubyUnits.begin(), fRubyUnits.end(), index,
        [](const RubyLayoutUnit& value, ClusterIndex position) { return value.clusters.end < position; });
    return index == 0 || (unit != fRubyUnits.end() && unit->clusters.end == index);
}

void ParagraphImpl::addRubyMetrics(ClusterRange clusters, InternalLineMetrics& metrics) const {
    for (const auto& span : fRubySpans) {
        if (span.clusters.start >= clusters.end || span.clusters.end <= clusters.start)
            continue;
        if (span.input.style.position == RubyPosition::Above) {
            auto ascent = span.baseTop - span.input.style.gap - span.annotationBounds.height() - 1;
            metrics.fAscent = std::min(metrics.fAscent, ascent);
            metrics.fRawAscent = std::min(metrics.fRawAscent, ascent);
        } else {
            auto descent = span.baseBottom + span.input.style.gap + span.annotationBounds.height() + 1;
            metrics.fDescent = std::max(metrics.fDescent, descent);
            metrics.fRawDescent = std::max(metrics.fRawDescent, descent);
        }
    }
}

std::vector<RubyPlacement> ParagraphImpl::rubyPlacements(const TextLine& line) const {
    std::vector<RubyPlacement> result;
    for (const auto& span : fRubySpans) {
        if (span.input.baseRange.start < line.trimmedText().start ||
            span.input.baseRange.end > line.trimmedText().end) continue;
        SkRect cell = SkRect::MakeEmpty();
        line.iterateThroughVisualRuns(false, [&](const Run* run, SkScalar offset, TextRange text, SkScalar* width) {
            auto full = line.measureTextInsideOneRun(text, run, offset, 0, true, TextLine::GlyphCluster);
            *width = full.clip.width();
            auto start = std::max(text.start, span.input.baseRange.start);
            auto end = std::min(text.end, span.input.baseRange.end);
            if (start < end) {
                auto part = line.measureTextInsideOneRun({start, end}, run, offset, 0, true, TextLine::GlyphCluster);
                part.clip.offset(line.offset().x() + full.fTextShift - part.fTextShift, 0);
                cell.join(part.clip);
            }
            return true;
        });
        auto visualFirst = fClusters[span.clusters.start].run().leftToRight()
            ? span.clusters.start : span.clusters.end - 1;
        const auto& firstCluster = fClusters[visualFirst];
        const auto& firstRun = firstCluster.run();
        if (!firstRun.fJustificationShifts.empty()) {
            auto shift = firstRun.fJustificationShifts[firstCluster.startPos()];
            cell.fLeft += shift.fX - shift.fY;
        }
        auto baseline = line.offset().y() + std::floor(line.baseline() + 0.5f);
        auto annotationTop = span.input.style.position == RubyPosition::Above
                                     ? baseline + span.baseTop - span.input.style.gap - span.annotationBounds.bottom()
                                     : baseline + span.baseBottom + span.input.style.gap - span.annotationBounds.top();
        auto origin =
                SkPoint::Make(cell.left() + span.annotationLeft - span.annotationBounds.left() + span.overflowShift,
                              annotationTop);
        result.push_back({&span, origin});
    }
    return result;
}

void ParagraphImpl::positionRuby() {
    for (auto& span : fRubySpans) span.overflowShift = 0;
    if (!SkIsFinite(fWidth)) return;
    for (const auto& line : fLines) {
        for (const auto& placement : rubyPlacements(line)) {
            auto& span = fRubySpans[placement.span - fRubySpans.data()];
            if (span.input.style.overflow != RubyOverflow::PreserveBase || span.width <= fWidth)
                continue;
            auto cellLeft = placement.origin.x() - span.annotationLeft + span.annotationBounds.left();
            auto anchor = std::max(cellLeft, (fWidth - span.baseWidth) / 2);
            span.overflowShift = anchor - cellLeft - span.baseLeft;
            for (size_t index = span.clusters.start; index < span.clusters.end; ++index) {
                const auto& cluster = fClusters[index];
                auto& run = cluster.run();
                for (size_t glyph = cluster.startPos(); glyph < cluster.endPos(); ++glyph)
                    run.fOffsets[glyph].fX += span.overflowShift;
            }
        }
    }
}

void TextWrapper::breakRubyTextIntoLines(ParagraphImpl* parent, SkScalar maxWidth,
                                        const AddLineToParagraph& addLine) {
    const auto& units = parent->rubyUnits();
    fHeight = 0;
    fMinIntrinsicWidth = 0;
    fMaxIntrinsicWidth = 0;
    fExceededMaxLines = false;
    SkScalar hardLineWidth = 0;
    SkScalar wordWidth = 0;
    for (const auto& unit : units) {
        hardLineWidth += unit.width;
        wordWidth += unit.width;
        if (unit.softBreak || unit.hardBreak) {
            fMinIntrinsicWidth = std::max(fMinIntrinsicWidth, wordWidth - (unit.whitespace ? unit.width : 0));
            wordWidth = 0;
        }
        if (unit.hardBreak) {
            fMaxIntrinsicWidth = std::max(fMaxIntrinsicWidth, hardLineWidth);
            hardLineWidth = 0;
        }
    }
    fMinIntrinsicWidth = std::max(fMinIntrinsicWidth, wordWidth);
    fMaxIntrinsicWidth = std::max(fMaxIntrinsicWidth, hardLineWidth);
    auto maxLines = parent->paragraphStyle().getMaxLines();
    if (maxLines == 0) {
        fExceededMaxLines = !units.empty();
        return;
    }
    auto unlimitedEllipsis = parent->paragraphStyle().unlimited_lines() &&
                             parent->paragraphStyle().ellipsized();
    auto heightBehavior = parent->paragraphStyle().getTextHeightBehavior();
    auto emit = [&](size_t first, size_t last, bool hardBreak) {
        auto clusterStart = first == units.size() ? parent->clusters().size() - 1 : units[first].clusters.start;
        auto clusterEnd = first == last ? clusterStart : units[last - 1].clusters.end;
        auto trimmedEnd = clusterEnd;
        SkScalar widthWithSpaces = 0;
        SkScalar width = 0;
        InternalLineMetrics metrics(parent->strutForceHeight());
        for (size_t index = clusterStart; index < clusterEnd; ++index) {
            const auto& cluster = parent->cluster(index);
            widthWithSpaces += cluster.width();
            if (!cluster.isHardBreak() && !cluster.run().isPlaceholder()) metrics.add(&cluster.run());
        }
        auto trimmedUnitEnd = last;
        while (trimmedUnitEnd > first && units[trimmedUnitEnd - 1].whitespace) --trimmedUnitEnd;
        trimmedEnd = trimmedUnitEnd == first ? clusterStart : units[trimmedUnitEnd - 1].clusters.end;
        for (size_t index = clusterStart; index < trimmedEnd; ++index) width += parent->cluster(index).width();
        if (metrics.isClean()) metrics = parent->getEmptyMetrics();
        parent->addRubyMetrics({clusterStart, clusterEnd}, metrics);
        for (size_t index = clusterStart; index < clusterEnd; ++index) {
            auto& run = parent->cluster(index).run();
            if (run.isPlaceholder()) run.updateMetrics(&metrics);
        }
        auto maxRunMetrics = metrics;
        maxRunMetrics.setForceStrut(false);
        auto lastLine = parent->lines().size() + 1 >= maxLines || (unlimitedEllipsis && !hardBreak);
        if ((heightBehavior & TextHeightBehavior::kDisableFirstAscent) && parent->lines().empty())
            metrics.fAscent = metrics.fRawAscent;
        if ((heightBehavior & TextHeightBehavior::kDisableLastDescent) &&
            (lastLine || (last == units.size() && !hardBreak)))
            metrics.fDescent = metrics.fRawDescent;
        if (parent->strutEnabled()) parent->strutMetrics().updateLineMetrics(metrics);
        parent->addRubyMetrics({clusterStart, clusterEnd}, metrics);
        auto textStart = parent->cluster(clusterStart).textRange().start;
        auto textEnd = clusterEnd == clusterStart ? textStart : parent->cluster(clusterEnd - 1).textRange().end;
        auto trimmedTextEnd = trimmedEnd == clusterStart ? textStart : parent->cluster(trimmedEnd - 1).textRange().end;
        auto ellipsis = lastLine && last < units.size() && parent->paragraphStyle().ellipsized() &&
                        SkIsFinite(maxWidth) && !hardBreak;
        auto textWithoutNewlineEnd = hardBreak
            ? parent->cluster(units[last - 1].clusters.start).textRange().start : textEnd;
        addLine({textStart, trimmedTextEnd}, {textStart, textWithoutNewlineEnd}, {textStart, textEnd},
                {clusterStart, trimmedEnd}, {clusterStart, clusterEnd}, widthWithSpaces,
                0, 0, {0, fHeight}, {width, metrics.height()}, metrics, ellipsis);
        parent->lines().back().setMaxRunMetrics(maxRunMetrics);
        fHeight += metrics.height();
        parent->fMaxWidthWithTrailingSpaces = std::max(parent->fMaxWidthWithTrailingSpaces, widthWithSpaces);
    };
    size_t first = 0;
    while (first < units.size()) {
        size_t cursor = first;
        size_t softBreak = first;
        size_t emergencyBreak = first;
        SkScalar width = 0;
        bool hardBreak = false;
        while (cursor < units.size()) {
            const auto& unit = units[cursor];
            if (cursor > first && !unit.whitespace && width + unit.width > maxWidth) {
                auto boundary = softBreak > first ? softBreak : emergencyBreak;
                if (boundary > first) { cursor = boundary; break; }
            }
            width += unit.width;
            ++cursor;
            if (unit.hardBreak) { hardBreak = true; break; }
            if (unit.softBreak) softBreak = cursor;
            if (unit.emergencyBreak) emergencyBreak = cursor;
        }
        emit(first, cursor, hardBreak);
        first = cursor;
        if (parent->lines().size() >= maxLines || (unlimitedEllipsis && !hardBreak)) {
            fExceededMaxLines = first < units.size() || hardBreak;
            break;
        }
    }
    if (first == units.size() && !units.empty() && units.back().hardBreak && parent->lines().size() < maxLines)
        emit(units.size(), units.size(), false);
    if (!parent->lines().empty()) {
        if (heightBehavior & TextHeightBehavior::kDisableFirstAscent)
            parent->lines().front().setAscentStyle(LineMetricStyle::Typographic);
        if (heightBehavior & TextHeightBehavior::kDisableLastDescent)
            parent->lines().back().setDescentStyle(LineMetricStyle::Typographic);
    }
}

}

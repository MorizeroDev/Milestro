#pragma once

#include "modules/skparagraph/src/ParagraphImpl.h"
#include <string>

namespace skia::textlayout {

enum class PrototypeOversizePlacement { InlineStart, BaseVisible };

struct PrototypeRubyInput {
    TextRange baseRange;
    std::string annotation;
    PrototypeOversizePlacement oversizePlacement = PrototypeOversizePlacement::InlineStart;
};

struct PrototypeSlice {
    Run* run;
    TextRange text;
    size_t glyphStart;
    size_t glyphEnd;
    SkScalar inlineOffset;
};

struct PrototypeShape {
    std::vector<PrototypeSlice> slices;
    SkScalar advance = 0;
    SkScalar inlineStart = 0;
    SkScalar inlineEnd = 0;
    SkScalar blockStart = 0;
    SkScalar blockEnd = 0;
};

struct PrototypeUnit {
    TextRange text;
    PrototypeShape base;
    PrototypeShape annotation;
    bool hasRuby = false;
    PrototypeOversizePlacement oversizePlacement = PrototypeOversizePlacement::InlineStart;
    bool breakAfter = false;
    SkScalar inlineExtent = 0;
    SkScalar before = 0;
    SkScalar after = 0;
};

struct PrototypeLine {
    size_t start;
    size_t end;
    SkScalar inlineExtent;
    SkScalar baseline;
    SkScalar blockEnd;
};

struct PrototypePlacement {
    PrototypeSlice slice;
    bool annotation;
    SkPoint origin;
    SkRect ink;
};

class RubyPrototype {
public:
    RubyPrototype(std::unique_ptr<Paragraph> base,
                  std::vector<PrototypeRubyInput> inputs,
                  const ParagraphStyle& style,
                  sk_sp<FontCollection> fonts,
                  sk_sp<SkUnicode> unicode);
    void layout(SkScalar inlineLimit);
    void paint(SkCanvas* canvas) const;
    std::vector<SkRect> baseRects(TextRange bytes) const;
    ParagraphImpl& shapedBase() const;
    bool rawBoundary(size_t offset) const;
    const std::vector<PrototypeUnit>& units() const {
        return fUnits;
    }
    const std::vector<PrototypeLine>& lines() const {
        return fLines;
    }
    const std::vector<PrototypePlacement>& placements() const {
        return fPlacements;
    }
    SkScalar height() const {
        return fLines.empty() ? 0 : fLines.back().blockEnd;
    }
    SkScalar minIntrinsic() const;
    SkScalar maxIntrinsic() const;

private:
    static PrototypeShape shapeRange(ParagraphImpl& paragraph, TextRange range);
    static std::vector<SkPoint> points(const PrototypeSlice& slice);
    static SkRect inkBounds(const PrototypeSlice& slice);
    void place(const PrototypeShape& shape, bool annotation, SkPoint origin);
    std::unique_ptr<Paragraph> fBase;
    std::vector<std::unique_ptr<Paragraph>> fAnnotations;
    skia_private::TArray<SkUnicode::CodeUnitFlags, true> fRawFlags;
    std::vector<PrototypeUnit> fUnits;
    std::vector<PrototypeLine> fLines;
    std::vector<PrototypePlacement> fPlacements;
};

}

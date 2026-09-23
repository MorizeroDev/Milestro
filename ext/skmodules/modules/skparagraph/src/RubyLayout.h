#pragma once

#include "modules/skparagraph/include/Paragraph.h"
#include "modules/skparagraph/include/Ruby.h"
#include "modules/skparagraph/src/Run.h"

namespace skia::textlayout {

struct RubySpan {
    RubyAnnotation input;
    ClusterRange clusters;
    std::unique_ptr<Paragraph> annotation;
    SkRect annotationBounds;
    SkScalar baseTop;
    SkScalar baseBottom;
    SkScalar baseLeft;
    SkScalar baseWidth;
    SkScalar width;
    SkScalar annotationLeft;
    bool ideographic;
    SkScalar overflowShift = 0;
};

struct RubyLayoutUnit {
    ClusterRange clusters;
    SkScalar width;
    bool whitespace;
    bool softBreak;
    bool hardBreak;
    bool emergencyBreak;
};

struct RubyPlacement {
    const RubySpan* span;
    SkPoint origin;
};

}

#include "Milestro/skia/textlayout/ParagraphBuilder.h"

using namespace milestro::skia::textlayout;

SplittedGlyphInfo Paragraph::splitGlyph(SkScalar x, SkScalar y) {
    SkPoint textRenderLeftTop(x, y);
    std::vector<SkRect> boundList;
    paragraph->extendedVisit([&](int lineNumber, const ::skia::textlayout::Paragraph::ExtendedVisitorInfo *info) {
        if (info == nullptr) {
//            std::cout << "Line Number: " << lineNumber << " end" << std::endl;
            return;
        }
//        std::cout << "Line Number: " << lineNumber << std::endl;
        SkPoint origin = info->origin;
//        std::cout << "origin: (" << origin.x() << ", " << origin.y() << ")" << std::endl;

        for (int i = 0; i < info->count; ++i) {
            SkPoint position = info->positions[i];
            auto glyphPosition = position + origin + textRenderLeftTop;
            SkRect bounds = info->bounds[i];
            bounds = bounds.makeOffset(glyphPosition);
            boundList.emplace_back(bounds);
        }
    });

    SplittedGlyphInfo ret;
    ret.bounds = boundList;
    return ret;
}

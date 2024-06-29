#include <src/gpu/ganesh/GrDistanceFieldGenFromVector.h>
#include "Milestro/skia/textlayout/Paragraph.h"
#include "Milestro/skia/Font.h"
#include "Milestro/skia/Path.h"

using namespace milestro::skia::textlayout;
using namespace milestro::skia;

uint64_t
Paragraph::splitGlyph(SkScalar x, SkScalar y,
                      void *context, MilestroSkiaTextlayoutParagraphSplitGlyphCallback callback) {
    if (callback == nullptr) {
        return -1;
    }

    SkPoint textRenderLeftTop = SkPoint::Make(x, y);
    std::vector<SkRect> boundList;
    paragraph->extendedVisit([&](int lineNumber, const ::skia::textlayout::Paragraph::ExtendedVisitorInfo *info) {
        if (info == nullptr) {
            return;
        }
        SkPoint origin = info->origin;
        milestro::skia::Font font(info->font);
        auto advance = info->advance;

        for (int i = 0; i < info->count; ++i) {
            SkGlyphID glyphId = info->glyphs[i];
            SkPoint position = info->positions[i];
            auto glyphPosition = position + origin + textRenderLeftTop;
            SkRect bound = info->bounds[i];
            bound = bound.makeOffset(glyphPosition);
            boundList.emplace_back(bound);

            callback(context, glyphId, &font,
                     bound.left(), bound.top(),
                     bound.right(), bound.bottom(),
                     advance.width(), advance.height());
        }
    });

    return 0;
}

uint64_t Paragraph::toSDF(int width, int height, SkScalar x, SkScalar y, uint8_t *distanceField) {
    auto fullPath = generateToSkPath(x, y);
    SkMatrix drawMatrix;
    return GrGenerateDistanceFieldFromPath(distanceField, fullPath, drawMatrix, width, height, width) ? 0 : -1;
}

Path *Paragraph::toPath(SkScalar x, SkScalar y) {
    auto fullPath = generateToSkPath(x, y);
    return new Path(std::move(fullPath));
}

SkPath Paragraph::generateToSkPath(SkScalar x, SkScalar y) {
    SkPath fullPath;
    SkPoint textRenderLeftTop = SkPoint::Make(x, y);
    std::vector<SkRect> boundList;
    paragraph->extendedVisit([&](int lineNumber, const ::skia::textlayout::Paragraph::ExtendedVisitorInfo *info) {
        if (info == nullptr) {
            return;
        }
        SkPoint origin = info->origin;
        auto font = info->font;

        for (int i = 0; i < info->count; ++i) {
            SkGlyphID glyphId = info->glyphs[i];
            SkPoint position = info->positions[i];
            auto glyphPosition = position + origin + textRenderLeftTop;
            SkRect bound = info->bounds[i];
            bound = bound.makeOffset(glyphPosition);
            boundList.emplace_back(bound);

            SkPath glyphPath;
            if (font.getPath(glyphId, &glyphPath)) {
                fullPath.addPath(glyphPath, glyphPosition.x(), glyphPosition.y());
            }
        }
    });
    return fullPath;
}

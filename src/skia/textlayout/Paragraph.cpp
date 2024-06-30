#include <src/gpu/ganesh/GrDistanceFieldGenFromVector.h>
#include <src/core/SkDistanceFieldGen.h>
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

            callback(context, glyphId, &font,
                     bound.left(), bound.top(),
                     bound.right(), bound.bottom(),
                     advance.width(), advance.height());
        }
    });

    return 0;
}

int64_t Paragraph::toSDF(
        int sdfWidth, int sdfHeight, SkScalar sdfScale,
        SkScalar x, SkScalar y, uint8_t *distanceField) {
    auto fullPath = generateToSkPath(x, y);
    SkPaint paint;

    SkMatrix dfMatrix = SkMatrix::Scale(sdfScale, sdfScale);
    SkPath xformPath;
    fullPath.transform(dfMatrix, &xformPath);

    Canvas canvas(sdfWidth, sdfHeight, nullptr, true);
    SkCanvas *skCanvas = canvas.unwrap();
    skCanvas->drawPath(xformPath, paint);
    SkPixmap pixmap;
    if (!skCanvas->peekPixels(&pixmap)) {
        return false;
    }
//#ifdef MILESTRO_USE_CLI
//    canvas.SaveToPng("toSDF.png");
//#endif

    return SkGenerateDistanceFieldFromA8Image(distanceField, pixmap.addr8(), sdfWidth, sdfHeight, sdfWidth) ? 0 : -1;
}

Path *Paragraph::toPath(SkScalar x, SkScalar y) {
    auto fullPath = generateToSkPath(x, y);
    return new Path(std::move(fullPath));
}

SkPath Paragraph::generateToSkPath(SkScalar x, SkScalar y) {
    SkPath fullPath;
    SkPoint textRenderLeftTop = SkPoint::Make(x, y);
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

            SkPath glyphPath;
            if (font.getPath(glyphId, &glyphPath)) {
                fullPath.addPath(glyphPath, glyphPosition.x(), glyphPosition.y());
            }
        }
    });
    return fullPath;
}

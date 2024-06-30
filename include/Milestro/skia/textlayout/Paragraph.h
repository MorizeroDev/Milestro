#ifndef MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPH_H
#define MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPH_H

#include "modules/skparagraph/include/ParagraphBuilder.h"
#include "ParagraphStyle.h"
#include "FontCollection.h"
#include "Milestro/skia/Unicode.h"
#include "TextStyle.h"
#include "Milestro/skia/Path.h"
#include "Milestro/skia/Canvas.h"
#include "Milestro/util/milestro_serializerable.h"
#include "Milestro/game/milestro_game_types.h"

namespace milestro::skia::textlayout {

class MILESTRO_API Paragraph {
public :
    explicit Paragraph(std::unique_ptr<::skia::textlayout::Paragraph> paragraph) {
        this->paragraph = std::move(paragraph);
    }

    void layout(SkScalar width) {
        paragraph->layout(width);
    }

    uint64_t splitGlyph(SkScalar x, SkScalar y, void* context,
                                 MilestroSkiaTextlayoutParagraphSplitGlyphCallback callback = nullptr);

    int64_t toSDF(int sdfWidth, int sdfHeight, SkScalar sdfScale,
                  SkScalar x, SkScalar y, uint8_t *distanceField);

    milestro::skia::Path* toPath(SkScalar x, SkScalar y);

    void paint(milestro::skia::Canvas *canvas, SkScalar x, SkScalar y) {
        paragraph->paint(canvas->unwrap(), x, y);
    }

    MILESTRO_DECLARE_NON_COPYABLE(Paragraph)

private:
    std::unique_ptr<::skia::textlayout::Paragraph> paragraph;

    SkPath generateToSkPath(SkScalar x, SkScalar y);
};
}
#endif //MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPH_H

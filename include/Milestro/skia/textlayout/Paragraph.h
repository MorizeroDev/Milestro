#ifndef MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPH_H
#define MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPH_H

#include "../unicode/Unicode.h"
#include "FontCollection.h"
#include "Milestro/game/milestro_game_types.h"
#include "Milestro/skia/Canvas.h"
#include "Milestro/skia/Path.h"
#include "Milestro/util/milestro_serializerable.h"
#include "ParagraphStyle.h"
#include "TextStyle.h"
#include "modules/skparagraph/include/ParagraphBuilder.h"

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
        paint(canvas->unwrap(), x, y);
    }

    void paint(SkCanvas *canvas, SkScalar x, SkScalar y) {
        paragraph->paint(canvas, x, y);
    }

    ::skia::textlayout::Paragraph *unwrap() {
        return paragraph.get();
    }

    MILESTRO_DECLARE_NON_COPYABLE(Paragraph)

private:
    std::unique_ptr<::skia::textlayout::Paragraph> paragraph;

    SkPath generateToSkPath(SkScalar x, SkScalar y);
};
}
#endif //MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPH_H

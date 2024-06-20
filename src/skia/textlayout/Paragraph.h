#ifndef MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPH_H
#define MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPH_H

#include "modules/skparagraph/include/ParagraphBuilder.h"
#include "ParagraphStyle.h"
#include "FontCollection.h"
#include "skia/Unicode.h"
#include "TextStyle.h"
#include "skia/Canvas.h"

namespace milestro::skia::textlayout {
class Paragraph {
public :
    Paragraph(std::unique_ptr<::skia::textlayout::Paragraph> &&paragraph) {
        this->paragraph = std::move(paragraph);
    }

    void layout(SkScalar width) {
        paragraph->layout(width);
    }

    void splitGlyph() {
        // TODO 整理结果，传回给 Unity
        paragraph->visit([](int lineNumber, const ::skia::textlayout::Paragraph::VisitorInfo *info) {
            if (info == nullptr) {
                std::cout << "Line Number: " << lineNumber << " end" << std::endl;
                return;
            }
            std::cout << "Line Number: " << lineNumber << std::endl;
            SkPoint origin = info->origin;
            std::cout << "origin: (" << origin.x() << ", " << origin.y() << ")" << std::endl;

            for (int i = 0; i < info->count; ++i) {
                uint16_t glyph = info->glyphs[i];
                SkPoint position = info->positions[i];

                std::cout << "Glyph ID: " << glyph
                          << " at position (" << position.x() << ", " << position.y() << ")" << std::endl;
            }
        });
    }

    void paint(milestro::skia::Canvas *canvas, SkScalar x, SkScalar y) {
        paragraph->paint(canvas->unwrap(), x, y);
    }

    MILESTRO_DECLARE_NON_COPYABLE(Paragraph)

private:
    std::unique_ptr<::skia::textlayout::Paragraph> paragraph;
};
}
#endif //MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPH_H

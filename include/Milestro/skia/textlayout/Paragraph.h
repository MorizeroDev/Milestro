#ifndef MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPH_H
#define MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPH_H

#include "modules/skparagraph/include/ParagraphBuilder.h"
#include "ParagraphStyle.h"
#include "FontCollection.h"
#include "Milestro/skia/Unicode.h"
#include "TextStyle.h"
#include "Milestro/skia/Canvas.h"
#include "Milestro/util/milestro_serializerable.h"

namespace milestro::skia::textlayout {

class MILESTRO_API SplittedGlyphInfo : public milestro::util::serialization::serializable {
public:
    std::vector<SkRect> bounds;
    nlohmann::json toJson() override {
        std::vector<nlohmann::json> jsonBounds;
        for (const auto &rect : bounds) {
            jsonBounds.push_back({
                                     {"left", rect.left()},
                                     {"top", rect.top()},
                                     {"right", rect.right()},
                                     {"bottom", rect.bottom()}
                                 });
        }
        return {{"bounds", jsonBounds}};
    }
};

class MILESTRO_API Paragraph {
public :
    explicit Paragraph(std::unique_ptr<::skia::textlayout::Paragraph> paragraph) {
        this->paragraph = std::move(paragraph);
    }

    void layout(SkScalar width) {
        paragraph->layout(width);
    }

    SplittedGlyphInfo splitGlyph(SkScalar x, SkScalar y);

    void paint(milestro::skia::Canvas *canvas, SkScalar x, SkScalar y) {
        paragraph->paint(canvas->unwrap(), x, y);
    }

    MILESTRO_DECLARE_NON_COPYABLE(Paragraph)

private:
    std::unique_ptr<::skia::textlayout::Paragraph> paragraph;
};
}
#endif //MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPH_H

#ifndef MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPHBUILDER_H
#define MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPHBUILDER_H

#include "modules/skparagraph/include/ParagraphBuilder.h"
#include "ParagraphStyle.h"
#include "FontCollection.h"
#include "skia/Unicode.h"
#include "TextStyle.h"
#include "Paragraph.h"

namespace milestro::skia::textlayout {
class ParagraphBuilder {
public:
    explicit ParagraphBuilder(ParagraphStyle *style) {
        auto fontCollection = GetFontCollection();
        auto unicodeProvider = GetUnicodeProvider();
        builder = ::skia::textlayout::ParagraphBuilder::make(style->unwrap(),
                                                             fontCollection->unwrap(),
                                                             unicodeProvider->unwrap());
    }

    MILESTRO_DECLARE_NON_COPYABLE(ParagraphBuilder)

    void pushStyle(TextStyle *&style) {
        builder->pushStyle(style->spawn());
    };

    void pop() {
        builder->pop();
    };

    void addText(const char *text, size_t len) {
        builder->addText(text, len);
    }

    Paragraph *build() {
        auto paragraph = builder->Build();
        auto ret = new Paragraph(std::move(paragraph));
        return ret;
    }

private:
    std::unique_ptr<::skia::textlayout::ParagraphBuilder> builder;
};

}

#endif //MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPHBUILDER_H

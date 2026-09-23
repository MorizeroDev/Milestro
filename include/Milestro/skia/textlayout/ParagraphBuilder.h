#ifndef MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPHBUILDER_H
#define MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPHBUILDER_H

#include "../unicode/Unicode.h"
#include "FontCollection.h"
#include "Paragraph.h"
#include "ParagraphStyle.h"
#include "RubyStyle.h"
#include "TextStyle.h"
#include "modules/skparagraph/include/ParagraphBuilder.h"

namespace milestro::skia::textlayout {

class MILESTRO_API ParagraphBuilder {
public:
    explicit ParagraphBuilder(ParagraphStyle *style) {
        auto fontCollection = GetFontCollection();
        auto unicodeProvider = GetUnicodeProvider();
        builder = fontCollection->MakeParagraphBuilder(*style, unicodeProvider->unwrap());
    }

    MILESTRO_DECLARE_NON_COPYABLE(ParagraphBuilder)

    void pushStyle(TextStyle *style) {
        GetFontCollection()->PushStyle(builder.get(), *style);
    };

    void pop() {
        builder->pop();
    };

    void addText(const char *text, size_t len) {
        builder->addText(text, len);
    }

    void addRuby(size_t startUtf8, size_t endUtf8, const char* text, size_t length) {
        builder->addRuby({{startUtf8, endUtf8}, std::string(text, length)});
    }

    void addRuby(size_t startUtf8, size_t endUtf8, const char* text, size_t length, const RubyStyle& style) {
        builder->addRuby({{startUtf8, endUtf8}, std::string(text, length), style.spawn()});
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

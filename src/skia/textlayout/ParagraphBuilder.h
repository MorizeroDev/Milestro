#ifndef MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPHBUILDER_H
#define MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPHBUILDER_H

#include "modules/skparagraph/include/ParagraphBuilder.h"
#include "ParagraphStyle.h"
#include "FontCollection.h"
#include "skia/Unicode.h"
#include "TextStyle.h"

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

  MILESTRO_DECLARE_NON_COPYABLE(Paragraph)

private:
  std::unique_ptr<::skia::textlayout::Paragraph> paragraph;
};

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
    builder->pushStyle(style->unwrap());
  };

  void pop() {
    builder->pop();
  };

  void addText(const char *text, size_t len) {
    builder->addText(text, len);
  }

  Paragraph build() {
    auto paragraph = builder->Build();
    return Paragraph(std::move(paragraph));
  }

private:
  std::unique_ptr<::skia::textlayout::ParagraphBuilder> builder;
};

}

#endif //MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPHBUILDER_H

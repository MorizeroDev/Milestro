#ifndef MILESTRO_TEXTLAYOUT_FONTCOLLECTION
#define MILESTRO_TEXTLAYOUT_FONTCOLLECTION
#include "modules/skparagraph/include/FontCollection.h"
#include "Milestro/util/milestro_class.h"

namespace milestro::skia::textlayout {

class FontCollection {

public:
  explicit FontCollection(sk_sp<::skia::textlayout::FontCollection> fontCollection) {
    this->fontCollection = std::move(fontCollection);
  }

  MILESTRO_DECLARE_NON_COPYABLE(FontCollection)

  sk_sp<::skia::textlayout::FontCollection> unwrap() {
    return fontCollection;
  }

private:
  sk_sp<::skia::textlayout::FontCollection> fontCollection;
};

FontCollection *GetFontCollection();

}

#endif //MILESTRO_SRC_SKIA_TEXT LAYOUT_FONTCOLLECTION_H
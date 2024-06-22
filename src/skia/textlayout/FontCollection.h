#ifndef MILESTRO_SKIA_TEXTLAYOUT_FONTCOLLECTION
#define MILESTRO_SKIA_TEXTLAYOUT_FONTCOLLECTION

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

    void foo() {
    }

private:
    sk_sp<::skia::textlayout::FontCollection> fontCollection;
};

FontCollection *GetFontCollection();

}

#endif //MILESTRO_SKIA_TEXTLAYOUT_FONTCOLLECTION

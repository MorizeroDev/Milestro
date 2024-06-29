#ifndef MILESTRO_SKIA_TEXTLAYOUT_FONTCOLLECTION
#define MILESTRO_SKIA_TEXTLAYOUT_FONTCOLLECTION

#include "modules/skparagraph/include/FontCollection.h"
#include "Milestro/util/milestro_class.h"
#include "Milestro/common/milestro_export_macros.h"

namespace milestro::skia::textlayout {

class MILESTRO_API FontCollection {
public:
    explicit FontCollection(sk_sp<::skia::textlayout::FontCollection> fontCollection) {
        this->fontCollection = std::move(fontCollection);
    }

    MILESTRO_DECLARE_NON_COPYABLE(FontCollection)

    sk_sp<::skia::textlayout::FontCollection> unwrap() {
        return fontCollection;
    }

    void clearCaches() {
        fontCollection->clearCaches();
    }

    bool fontFallbackEnabled() {
        return fontCollection->fontFallbackEnabled();
    }

    void setFontFallbackEnabled(bool enabled) {
        if (enabled) {
            fontCollection->enableFontFallback();
        } else {
            fontCollection->disableFontFallback();

        }
    };

private:
    sk_sp<::skia::textlayout::FontCollection> fontCollection;
};

MILESTRO_API FontCollection *GetFontCollection();

}

#endif //MILESTRO_SKIA_TEXTLAYOUT_FONTCOLLECTION

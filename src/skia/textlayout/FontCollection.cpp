#include <memory>
#include <string>
#include "FontCollection.h"
#include "Milestro/common/milestro_result.h"
#include "Milestro/log/log.h"
#include "skia/FontManager.h"

namespace milestro::skia::textlayout {

std::unique_ptr<FontCollection> FontCollectionInstance = nullptr;

Result<void, std::string> InitialFontCollection() {
    auto fontCollection = sk_make_sp<::skia::textlayout::FontCollection>();
    auto fontMgr = milestro::skia::GetFontManager();
    fontCollection->setDefaultFontManager(fontMgr->unwrap());

    if (fontCollection == nullptr) {
        return Err(std::string("fail to create ::skia::textlayout::FontCollection"));
    }
    FontCollectionInstance = std::make_unique<FontCollection>(std::move(fontCollection));
    return Ok();
}

FontCollection *GetFontCollection() {
    if (FontCollectionInstance == nullptr) {
        auto result = InitialFontCollection();
        if (result.isErr()) {
            MILESTROLOG_ERROR("{}", result.unwrapErr());
        }
    }

    return FontCollectionInstance.get();
}

}
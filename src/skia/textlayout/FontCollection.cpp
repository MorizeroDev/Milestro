#include <memory>
#include <string>
#include "Milestro/skia/textlayout/FontCollection.h"
#include "Milestro/common/milestro_result.h"
#include "Milestro/log/log.h"
#include "Milestro/skia/FontRegistry.h"

namespace milestro::skia::textlayout {

static std::unique_ptr<FontCollection> FontCollectionInstance = nullptr;

Result<void, std::string> InitialFontCollection() {
    auto fontCollection = sk_make_sp<::skia::textlayout::FontCollection>();
    if (fontCollection == nullptr) {
        return Err(std::string("fail to create ::skia::textlayout::FontCollection"));
    }

    auto fontRegistry = milestro::skia::GetFontRegistry();
    fontCollection->setAssetFontManager(fontRegistry->GetRegisteredFontMgr());

    if (fontRegistry->IsSystemFontMgrAvailable()) {
        fontCollection->setDefaultFontManager(fontRegistry->GetSystemFontMgr());
    } else {
        fontCollection->setDefaultFontManager(SkFontMgr::RefEmpty());
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

    assert(FontCollectionInstance != nullptr && "FontCollectionInstance is null");
    return FontCollectionInstance.get();
}

}

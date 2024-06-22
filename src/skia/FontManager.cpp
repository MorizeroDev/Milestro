#include <src/ports/SkFontMgr_custom.h>
#include "Milestro/skia/FontManager.h"
#include "Milestro/log/log.h"

namespace milestro::skia {

inline sk_sp<MilestroFontManager> MakeSkFontMgr() {
    sk_sp<MilestroFontManager> result = sk_make_sp<MilestroFontManager>();
    return result;
}

std::unique_ptr<FontManager> FontManagerInstance = nullptr;

Result<void, std::string> InitialFontManager() {
    auto skFontMgr = MakeSkFontMgr();
    if (skFontMgr == nullptr) {
        return Err(std::string("fail to createSkFontMgr"));
    }
    FontManagerInstance = std::make_unique<FontManager>(std::move(skFontMgr));
    return Ok();
}

FontManager *GetFontManager() {
    if (FontManagerInstance == nullptr) {
        auto result = InitialFontManager();
        if (result.isErr()) {
            MILESTROLOG_ERROR("{}", result.unwrapErr());
        }
    }

    return FontManagerInstance.get();
}

}

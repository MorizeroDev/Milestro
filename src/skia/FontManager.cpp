#include "Milestro/skia/FontManager.h"
#include "Milestro/log/log.h"
#include <src/ports/SkFontMgr_custom.h>

namespace fs = std::filesystem;

namespace milestro::skia {

inline sk_sp<MilestroFontManager> MakeSkFontMgr() {
    sk_sp<MilestroFontManager> result = sk_make_sp<MilestroFontManager>();
    return result;
}

static std::unique_ptr<FontManager> FontManagerInstance = nullptr;

Result<void, std::string> InitialFontManager() {
    auto skFontMgr = MakeSkFontMgr();
    if (skFontMgr == nullptr) {
        return Err(std::string("fail to createSkFontMgr"));
    }
    FontManagerInstance = std::make_unique<FontManager>(std::move(skFontMgr));
    return Ok();
}

FontManager* GetFontManager() {
    if (FontManagerInstance == nullptr) {
        auto result = InitialFontManager();
        if (result.isErr()) {
            MILESTROLOG_ERROR("{}", result.unwrapErr());
        }
    }

    return FontManagerInstance.get();
}

MilestroFontManager::RegisterResult FontManager::RegisterFontFromFile(const char* path) {
    MILESTROLOG_DEBUG("try to register font from file, path: {}", path);
    auto stream = SkStream::MakeFromFile(path);
    if (!stream) {
        MILESTROLOG_DEBUG("failed to open: {}", path);
        return MilestroFontManager::RegisterResult::Failed;
    }
    return fontMgr->registerFont(std::move(stream), SkString(path));
}

} // namespace milestro::skia

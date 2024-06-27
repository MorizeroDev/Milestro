#include "Milestro/skia/FontManager.h"
#include "Milestro/log/log.h"
#include "Milestro/util/milestro_encoding.h"
#include "Milestro/skia/MilestroEmptyFontManager.h"
#include <src/ports/SkFontMgr_custom.h>
#include "Milestro/common/milestro_platform.h"

namespace fs = std::filesystem;

namespace milestro::skia {

static std::unique_ptr<FontManager> FontManagerInstance = nullptr;

Result<void, std::string> InitialFontManager() {
    auto milestroFontManager = sk_make_sp<MilestroFontManager>();
    if (milestroFontManager == nullptr) {
        return Err(std::string("fail to create MilestroFontManager"));
    }

    auto milestroEmptyFontManager = sk_make_sp<MilestroEmptyFontManager>();
    if (milestroEmptyFontManager == nullptr) {
        MILESTROLOG_ERROR(std::string("fail to create MilestroEmptyFontManager, skipped"));
    }

    FontManagerInstance = std::make_unique<FontManager>(
        std::move(milestroFontManager),
        std::move(milestroEmptyFontManager)
    );
    return Ok();
}

FontManager *GetFontManager() {
    if (FontManagerInstance == nullptr) {
        auto result = InitialFontManager();
        if (result.isErr()) {
            MILESTROLOG_CRITICAL("{}", result.unwrapErr());
        }
    }

    assert(FontManagerInstance != nullptr && "FontManagerInstance is null");
    return FontManagerInstance.get();
}

MilestroFontManager::RegisterResult FontManager::RegisterFontFromFile(const char *path) {
#if MILESTRO_PLATFORM_IOS
    fs::path homeDir(milestro::util::env::getenv("HOME"));
    fs::path filename((std::string(path)));
    fs::path filePath = homeDir / filename;
#else
    fs::path filePath((std::string(path)));
#endif

#if _WIN32
    auto filePathString = milestro::util::encoding::WStringToString(filePath.wstring());
#else
    auto filePathString = filePath.string();
#endif
    MILESTROLOG_DEBUG("try to register font from file, path: {}", filePathString);

    auto stream = SkStream::MakeFromFile(filePathString.c_str());
    if (!stream) {
        MILESTROLOG_DEBUG("failed to open: {}", filePathString);
        return MilestroFontManager::RegisterResult::Failed;
    }
    return fontMgr->registerFont(std::move(stream), SkString(filePathString.c_str()));
}
} // namespace milestro::skia

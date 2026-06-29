#include "Milestro/skia/FontRegistry.h"
#include "Milestro/log/log.h"
#include "Milestro/common/milestro_platform.h"
#include "Milestro/util/milestro_encoding.h"
#include "Milestro/util/milestro_env.h"
#include <cassert>
#include <filesystem>
#include <memory>
#include <string>

#if MILESTRO_PLATFORM_MAC || MILESTRO_PLATFORM_IOS
#include "include/ports/SkFontMgr_mac_ct.h"
#elif MILESTRO_PLATFORM_WINDOWS
#include "include/ports/SkTypeface_win.h"
#endif

namespace fs = std::filesystem;

namespace milestro::skia {

namespace {

sk_sp<SkFontMgr> MakePlatformSystemFontMgr() {
#if MILESTRO_PLATFORM_MAC || MILESTRO_PLATFORM_IOS
    return SkFontMgr_New_CoreText(nullptr);
#elif MILESTRO_PLATFORM_WINDOWS
    return SkFontMgr_New_DirectWrite();
#else
    return nullptr;
#endif
}

} // namespace

static std::unique_ptr<FontRegistry> FontRegistryInstance = nullptr;

Result<void, std::string> InitialFontRegistry() {
    auto registeredFontMgr = sk_make_sp<MilestroRegisteredFontMgr>();
    if (registeredFontMgr == nullptr) {
        return Err(std::string("fail to create MilestroRegisteredFontMgr"));
    }

    auto systemFontMgr = MakePlatformSystemFontMgr();
    if (systemFontMgr) {
        MILESTROLOG_INFO("Milestro system FontMgr ready, families: {}", systemFontMgr->countFamilies());
    } else {
        MILESTROLOG_WARN("Milestro system FontMgr unavailable on this platform/build; registered asset fonts remain available.");
    }

    FontRegistryInstance = std::make_unique<FontRegistry>(
        std::move(registeredFontMgr),
        std::move(systemFontMgr)
    );
    return Ok();
}

FontRegistry *GetFontRegistry() {
    if (FontRegistryInstance == nullptr) {
        auto result = InitialFontRegistry();
        if (result.isErr()) {
            MILESTROLOG_CRITICAL("{}", result.unwrapErr());
        }
    }

    assert(FontRegistryInstance != nullptr && "FontRegistryInstance is null");
    return FontRegistryInstance.get();
}

MilestroRegisteredFontMgr::RegisterResult FontRegistry::RegisterFontFromFile(const char *path) {
#if MILESTRO_PLATFORM_IOS
    fs::path homeDir(milestro::util::env::getenv("HOME"));
    fs::path filename((std::string(path)));
    fs::path filePath = homeDir / filename;
#else
    fs::path filePath((std::string(path)));
#endif

#if MILESTRO_PLATFORM_WINDOWS
    auto filePathString = milestro::util::encoding::WStringToString(filePath.wstring());
#else
    auto filePathString = filePath.string();
#endif
    MILESTROLOG_DEBUG("try to register font from file, path: {}", filePathString);

    auto stream = SkStream::MakeFromFile(filePathString.c_str());
    if (!stream) {
        MILESTROLOG_DEBUG("failed to open: {}", filePathString);
        return MilestroRegisteredFontMgr::RegisterResult::Failed;
    }
    return registeredFontMgr->registerFont(std::move(stream), SkString(filePathString.c_str()));
}
} // namespace milestro::skia

#include "Milestro/skia/unicode/Unicode.h"
#include "Milestro/log/log.h"
#include "Milestro/unicode/milestro_icu.h"
#include "SkUnicodeNop.h"
#include <modules/skunicode/include/SkUnicode_icu.h>

#include <stdexcept>
#include <string>

namespace milestro::skia {

inline sk_sp<SkUnicode> MakeSkUnicode() {
    sk_sp<SkUnicode> result;
    result = SkUnicodes::ICU::Make();
    return result;
}

static std::unique_ptr<UnicodeProvider> UnicodeProviderInstance = nullptr;


UnicodeProvider* GetFallbackUnicodeProvider() {
    static UnicodeProvider fallback(MakeNopSkUnicode());
    return &fallback;
}

Result<void, std::string> InitialUnicodeProvider() {
    try {
        milestro::unicode::EnsureLoadICU();
    } catch (const std::exception& e) {
        return Err(std::string("fail to load ICU for SkUnicode: ") + e.what());
    }

    auto skFontMgr = MakeSkUnicode();
    if (skFontMgr == nullptr) {
        return Err(std::string("fail to create SkUnicode"));
    }
    UnicodeProviderInstance = std::make_unique<UnicodeProvider>(std::move(skFontMgr));
    return Ok();
}

UnicodeProvider *GetUnicodeProvider() {
    if (UnicodeProviderInstance == nullptr) {
        auto result = InitialUnicodeProvider();
        if (result.isErr()) {
            MILESTROLOG_ERROR("{}; using nop SkUnicode fallback", result.unwrapErr());
            return GetFallbackUnicodeProvider();
        }
    }
    if (UnicodeProviderInstance == nullptr) {
        MILESTROLOG_ERROR("fail to initialize SkUnicode provider; using nop SkUnicode fallback");
        return GetFallbackUnicodeProvider();
    }

    return UnicodeProviderInstance.get();
}

}

#include "Unicode.h"
#include "Milestro/log/log.h"

#if WIN32

#include <modules/skunicode/include/SkUnicode_icu.h>

#endif

namespace milestro::skia {

inline sk_sp<SkUnicode> MakeSkUnicode() {
    sk_sp<SkUnicode> result;
#if WIN32
    result = SkUnicodes::ICU::Make();
#else
#error No SkUnicode Provider
#endif
    return result;
}

std::unique_ptr<UnicodeProvider> UnicodeProviderInstance = nullptr;

Result<void, std::string> InitialUnicodeProvider() {
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
            MILESTROLOG_ERROR("{}", result.unwrapErr());
        }
    }

    return UnicodeProviderInstance.get();
}

}

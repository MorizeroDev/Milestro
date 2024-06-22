#include "Milestro/skia/Unicode.h"
#include "Milestro/log/log.h"
#include <modules/skunicode/include/SkUnicode_icu.h>

namespace milestro::skia {

inline sk_sp<SkUnicode> MakeSkUnicode() {
    sk_sp<SkUnicode> result;
    result = SkUnicodes::ICU::Make();
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

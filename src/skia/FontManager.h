#ifndef MILESTRO_FONTMANAGER_H
#define MILESTRO_FONTMANAGER_H

#include <include/core/SkRefCnt.h>
#include <include/core/SkFontMgr.h>
#include <Milestro/util/milestro_class.h>
#include "Milestro/common/milestro_result.h"
#include <string>
#include <utility>

#if WIN32

#include "include/ports/SkTypeface_win.h"

#endif

namespace milestro::skia {
    inline sk_sp<SkFontMgr> MakeSkFontMgr() {
        sk_sp<SkFontMgr> result;
#if WIN32
        result = SkFontMgr_New_DirectWrite();
#else
#error No SkFontMgr Provider
#endif
        return result;
    }

    class FontManager {
    public:
        explicit FontManager(sk_sp<SkFontMgr> fontMgr) {
            this->fontMgr = std::move(fontMgr);
        }

        MILESTRO_DECLARE_NON_COPYABLE(FontManager)
    private:
        sk_sp<SkFontMgr> fontMgr;
    };

    extern std::unique_ptr<FontManager> FontManagerInstance = nullptr;

    Result<void, std::string> InitialFontManager() {
        auto skFontMgr = MakeSkFontMgr();
        if (skFontMgr == nullptr) {
            return Err(std::string("fail to createSkFontMgr"));
        }
        FontManagerInstance = std::make_unique<FontManager>(std::move(skFontMgr));
        return Ok();
    }
}

#endif //MILESTRO_FONTMANAGER_H

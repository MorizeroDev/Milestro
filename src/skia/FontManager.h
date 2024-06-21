#ifndef MILESTRO_FONTMANAGER_H
#define MILESTRO_FONTMANAGER_H

#include <include/core/SkRefCnt.h>
#include <include/core/SkFontMgr.h>
#include <include/core/SkTypeface.h>
#include <Milestro/util/milestro_class.h>
#include "Milestro/common/milestro_result.h"
#include <string>
#include <utility>

namespace milestro::skia {

class TypeFace {
public:
    explicit TypeFace(sk_sp<SkTypeface> typeFace) {
        this->typeFace = std::move(typeFace);
    }

    sk_sp<SkTypeface> unwrap() {
        return typeFace;
    }

    MILESTRO_DECLARE_NON_COPYABLE(TypeFace)
private:
    sk_sp<SkTypeface> typeFace;
};

class FontManager {
public:
    explicit FontManager(sk_sp<SkFontMgr> fontMgr) {
        this->fontMgr = std::move(fontMgr);
    }

    MILESTRO_DECLARE_NON_COPYABLE(FontManager)

    TypeFace *RegisterFont(char *path) {
        auto typeFace = fontMgr->makeFromFile(path);
        auto ret = new TypeFace(std::move(typeFace));
        return ret;
    }

    sk_sp<SkFontMgr> unwrap() {
        return fontMgr;
    }

private:
    sk_sp<SkFontMgr> fontMgr;
};

FontManager *GetFontManager();
}

#endif //MILESTRO_FONTMANAGER_H

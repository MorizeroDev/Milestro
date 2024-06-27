#ifndef MILESTRO_FONTMANAGER_H
#define MILESTRO_FONTMANAGER_H

#include <include/core/SkRefCnt.h>
#include <include/core/SkStream.h>
#include <include/core/SkFontMgr.h>
#include <include/core/SkTypeface.h>
#include <Milestro/util/milestro_class.h>
#include "Milestro/common/milestro_result.h"
#include "Milestro/log/log.h"
#include "Typeface.h"
#include "MilestroFontManager.h"
#include "MilestroEmptyFontManager.h"
#include <string>
#include <utility>

namespace milestro::skia {

class MILESTRO_API FontManager {
public:
    explicit FontManager(sk_sp<MilestroFontManager> fontMgr,
                         sk_sp<MilestroEmptyFontManager> emptyFontMgr) {
        this->fontMgr = std::move(fontMgr);
        if (emptyFontMgr) {
            this->emptyFontMgr = std::move(emptyFontMgr);
        }
    }

    MILESTRO_DECLARE_NON_COPYABLE(FontManager)

    MilestroFontManager::RegisterResult RegisterFontFromFile(const char *path);

    std::vector<std::string> GetFamiliesNames() {
        std::vector<std::string> ret;
        for (int i = 0; i < fontMgr->countFamilies(); i++) {
            SkString famName(" ");
            fontMgr->getFamilyName(i, &famName);
            ret.emplace_back(famName.c_str());
        }
        return ret;
    }

    sk_sp<SkFontMgr> GetFontMgr() {
        return fontMgr;
    }

    bool IsEmptyFontMgrAvailable() {
        return emptyFontMgr != nullptr;
    }

    sk_sp<SkFontMgr> GetEmptyFontMgr() {
        return emptyFontMgr;
    }

private:
    sk_sp<MilestroFontManager> fontMgr;
    sk_sp<MilestroEmptyFontManager> emptyFontMgr;
};

MILESTRO_API FontManager *GetFontManager();
}

#endif //MILESTRO_FONTMANAGER_H

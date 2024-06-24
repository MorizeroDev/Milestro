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
#include <string>
#include <utility>

namespace milestro::skia {

class MILESTRO_API FontManager {
public:
    explicit FontManager(sk_sp<MilestroFontManager> fontMgr) {
        this->fontMgr = std::move(fontMgr);
    }

    MILESTRO_DECLARE_NON_COPYABLE(FontManager)

    MilestroFontManager::RegisterResult RegisterFontFromFile(const char *path) {
        SkString filename(path);
        auto stream = SkStream::MakeFromFile(filename.c_str());
        return fontMgr->registerFont(std::move(stream), SkString(path));
    }

    std::vector<std::string> GetFamiliesNames() {
        std::vector<std::string> ret;
        for (int i = 0; i < fontMgr->countFamilies(); i++) {
            SkString famName(" ");
            fontMgr->getFamilyName(i, &famName);
            ret.emplace_back(famName.c_str());
        }
        return ret;
    }

    sk_sp<SkFontMgr> unwrap() {
        return fontMgr;
    }

private:
    sk_sp<MilestroFontManager> fontMgr;
};

MILESTRO_API FontManager *GetFontManager();
}

#endif //MILESTRO_FONTMANAGER_H

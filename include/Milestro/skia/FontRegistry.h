#ifndef MILESTRO_FONTREGISTRY_H
#define MILESTRO_FONTREGISTRY_H

#include <Milestro/util/milestro_class.h>
#include <include/core/SkRefCnt.h>
#include <include/core/SkStream.h>
#include <include/core/SkFontMgr.h>
#include <include/core/SkTypeface.h>
#include "Milestro/common/milestro_result.h"
#include "Milestro/log/log.h"
#include "Typeface.h"
#include "MilestroRegisteredFontMgr.h"
#include <string>
#include <utility>
#include <vector>

namespace milestro::skia {

class MILESTRO_API FontRegistry {
public:
    explicit FontRegistry(sk_sp<MilestroRegisteredFontMgr> registeredFontMgr,
                          sk_sp<SkFontMgr> systemFontMgr)
        : registeredFontMgr(std::move(registeredFontMgr)),
          systemFontMgr(std::move(systemFontMgr)) {
    }

    MILESTRO_DECLARE_NON_COPYABLE(FontRegistry)

    MilestroRegisteredFontMgr::RegisterResult RegisterFontFromFile(const char *path);

    std::vector<std::string> GetRegisteredFontFamilyNames() const {
        std::vector<std::string> ret;
        for (int i = 0; i < registeredFontMgr->countFamilies(); i++) {
            SkString famName(" ");
            registeredFontMgr->getFamilyName(i, &famName);
            ret.emplace_back(famName.c_str());
        }
        return ret;
    }

    std::vector<MilestroFontFamilyInfo> GetRegisteredFontFamilies() const {
        std::vector<MilestroFontFamilyInfo> ret;
        for (auto &name: GetRegisteredFontFamilyNames()) {
            MilestroFontFamilyInfo item;
            item.name = std::move(name);
            ret.emplace_back(std::move(item));
        }
        return ret;
    }

    std::vector<MilestroFontFaceInfo> GetRegisteredFontFaces() const {
        return registeredFontMgr->getFontFaces();
    }

    sk_sp<SkFontMgr> GetRegisteredFontMgr() const {
        return registeredFontMgr;
    }

    bool IsSystemFontMgrAvailable() const {
        return systemFontMgr != nullptr;
    }

    sk_sp<SkFontMgr> GetSystemFontMgr() const {
        return systemFontMgr;
    }

private:
    sk_sp<MilestroRegisteredFontMgr> registeredFontMgr;
    sk_sp<SkFontMgr> systemFontMgr;
};

MILESTRO_API FontRegistry *GetFontRegistry();
}

#endif //MILESTRO_FONTREGISTRY_H

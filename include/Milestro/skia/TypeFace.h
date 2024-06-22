#ifndef MILESTRO_SKIA_TYPEFACE_H
#define MILESTRO_SKIA_TYPEFACE_H

#include <include/core/SkTypeface.h>
#include "Milestro/util/milestro_class.h"
#include "Milestro/log/log.h"
#include "Milestro/util/milestro_serializerable.h"

namespace milestro::skia {

class MILESTRO_API FontFamilyName : public milestro::util::serialization::serializable {
public:
    std::string name;
    std::string language;

    nlohmann::json toJson() override {
        return {
            {"name", name},
            {"language", language},
        };
    }
};

class MILESTRO_API TypeFace {
public:
    explicit TypeFace(sk_sp<SkTypeface> typeFace) {
        this->typeFace = std::move(typeFace);
    }

    sk_sp<SkTypeface> unwrap() {
        return typeFace;
    }

    std::vector<FontFamilyName> GetFamilyNames() {
        std::vector<FontFamilyName> ret;

        auto familyNames = typeFace->createFamilyNameIterator();
        SkTypeface::LocalizedString famName;
        famName.fString = " ";
        famName.fLanguage = " ";
        while (familyNames->next(&famName)) {
            FontFamilyName item;
            item.name = std::string(famName.fString.c_str());
            item.language = std::string(famName.fLanguage.c_str());
            ret.emplace_back(item);
        }
        familyNames->unref();

        return std::move(ret);
    }

    MILESTRO_DECLARE_NON_COPYABLE(TypeFace)
private:
    sk_sp<SkTypeface> typeFace;
};

}

#endif //MILESTRO_SKIA_TYPEFACE_H

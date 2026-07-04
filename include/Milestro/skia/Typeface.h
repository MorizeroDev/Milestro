#ifndef MILESTRO_SKIA_TYPEFACE_H
#define MILESTRO_SKIA_TYPEFACE_H

#include <include/core/SkTypeface.h>
#include "Milestro/util/milestro_class.h"
#include "Milestro/log/log.h"
#include <cstddef>
#include <string>
#include <utility>
#include <vector>

namespace milestro::skia {

class MILESTRO_API FontFamilyName {
public:
    std::string name;
    std::string language;
};

class MILESTRO_API MilestroTypefaceFamilyNameList {
public:
    explicit MilestroTypefaceFamilyNameList(std::vector<FontFamilyName> data)
        : data(std::move(data)) {
    }

    FontFamilyName *At(size_t position) {
        return &data[position];
    }

    FontFamilyName Get(size_t position) const {
        return data[position];
    }

    size_t Size() const {
        return data.size();
    }

private:
    std::vector<FontFamilyName> data;
};

class MILESTRO_API Typeface {
public:
    explicit Typeface(sk_sp<SkTypeface> typeFace) {
        this->typeFace = std::move(typeFace);
    }

    MILESTRO_DECLARE_NON_COPYABLE(Typeface)

    sk_sp<SkTypeface> unwrap() {
        return typeFace;
    }

    std::vector<FontFamilyName> GetFamilyNames() {
        std::vector<FontFamilyName> ret;

        auto familyNames = typeFace->createFamilyNameIterator();
        SkTypeface::LocalizedString famName;
        while (familyNames->next(&famName)) {
            FontFamilyName item;
            item.name = std::string(famName.fString.c_str());
            item.language = std::string(famName.fLanguage.c_str());
            ret.emplace_back(item);
        }
        familyNames->unref();

        return std::move(ret);
    }

private:
    sk_sp<SkTypeface> typeFace;
};

}

#endif //MILESTRO_SKIA_TYPEFACE_H

#ifndef MILESTRO_ICU_ICUUCOLLATOR_H
#define MILESTRO_ICU_ICUUCOLLATOR_H

#include "Milestro/common/milestro_result.h"
#include "Milestro/log/log.h"
#include "Milestro/util/milestro_class.h"
#include <iostream>
#include <sstream>
#include <string>
#include <unicode/ucol.h>
#include <unicode/unistr.h>
#include <unicode/ustring.h>

namespace milestro::icu {

class IcuUCollator {

public:
    IcuUCollator(std::string collation) {
        UErrorCode status = U_ZERO_ERROR;

        collator = ucol_open(collation.c_str(), &status);
        if (U_FAILURE(status)) {
            std::stringstream ss;
            ss << "Failed to create UCollator: " << u_errorName(status) << std::endl;
            MILESTROLOG_ERROR("{}", ss.str());
            throw std::runtime_error(ss.str());
        }
    }

    ~IcuUCollator() {
        if (collator != nullptr) {
            ucol_close(collator);
            collator = nullptr;
        }
    }

    MILESTRO_DECLARE_NON_COPYABLE(IcuUCollator);

    int compare(const char *a, const char *b) {
        ::icu::UnicodeString ustr_a = ::icu::UnicodeString::fromUTF8(a);
        ::icu::UnicodeString ustr_b = ::icu::UnicodeString::fromUTF8(b);
        return ucol_strcoll(collator, ustr_a.getBuffer(), ustr_a.length(), ustr_b.getBuffer(), ustr_b.length());
    }

private:
    UCollator *collator = nullptr;
};

} // namespace milestro::icu

#endif //MILESTRO_ICU_ICUUCOLLATOR_H

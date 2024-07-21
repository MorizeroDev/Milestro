#ifndef MILESTRO_ICU_ICUUCOLLATOR_H
#define MILESTRO_ICU_ICUUCOLLATOR_H

#include "Milestro/common/milestro_result.h"
#include "Milestro/log/log.h"
#include "Milestro/util/milestro_class.h"
#include "Milestro/util/milestro_function.h"
#include <iostream>
#include <sstream>
#include <string>
#include <unicode/ucol.h>
#include <unicode/unistr.h>
#include <unicode/ustring.h>

namespace milestro::icu {

class IcuUCollator {

public:
    IcuUCollator(std::string collation);

    ~IcuUCollator() {
        if (collator != nullptr) {
            ucol_close(collator);
            collator = nullptr;
        }
    }

    MILESTRO_DECLARE_NON_COPYABLE(IcuUCollator);

    int compare(const char *a, const char *b) ;

    void setAttribute(UColAttribute attr, UColAttributeValue value) {
        UErrorCode status = U_ZERO_ERROR;
        ucol_setAttribute(collator, attr, value, &status);
        if (U_FAILURE(status)) {
            auto msg =  std::format("{}: Failed to set attribute: {}",
                                    MILESTRO_FUNCTIONNAME, u_errorName(status));
            MILESTROLOG_ERROR("{}", msg);
            throw std::runtime_error(msg);
        }
    }

private:
    UCollator *collator = nullptr;
};

} // namespace milestro::icu
#endif //MILESTRO_ICU_ICUUCOLLATOR_H

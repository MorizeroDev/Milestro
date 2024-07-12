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

    Result<std::vector<UChar>, std::string> convertToUChar(const char *src) {
        UErrorCode status = U_ZERO_ERROR;

        std::vector<UChar> dest;
        int32_t dest_len;

        // First, determine the necessary size for the UChar buffer
        u_strFromUTF8(nullptr, 0, &dest_len, src, -1, &status);
        if (status != U_BUFFER_OVERFLOW_ERROR && U_FAILURE(status)) {
            std::stringstream ss;
            ss << "fail to determine the necessary size for string: " << u_errorName(status);
            return Err(ss.str());
        }
        status = U_ZERO_ERROR; // Reset status

        // Allocate buffer with the correct size
        dest.resize(dest_len + 1);

        // Perform the conversion
        u_strFromUTF8(dest.data(), dest.size(), nullptr, src, -1, &status);
        if (U_FAILURE(status)) {
            std::stringstream ss;
            ss << "fail to convert string: " << u_errorName(status);
            return Err(ss.str());
        }

        return Ok(dest);
    }

    int compare(const char *a, const char *b) {
        auto ustr_a_result = convertToUChar(a);
        if (ustr_a_result.isErr()) {
            throw std::runtime_error(ustr_a_result.unwrapErr());
        }
        auto ustr_b_result = convertToUChar(b);
        if (ustr_b_result.isErr()) {
            throw std::runtime_error(ustr_b_result.unwrapErr());
        }

        auto ustr_a = ustr_a_result.unwrap();
        auto ustr_b = ustr_b_result.unwrap();
        return ucol_strcoll(collator,
                            ustr_a.data(), static_cast<int32_t>(ustr_a.size()),
                            ustr_b.data(), static_cast<int32_t>(ustr_b.size())
        );
    }

private:
    UCollator *collator = nullptr;
};

} // namespace milestro::icu
#endif //MILESTRO_ICU_ICUUCOLLATOR_H

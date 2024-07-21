#include "Milestro/icu/IcuUCollator.h"
#include "Milestro/skia/Unicode.h"

namespace milestro::icu {

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

IcuUCollator::IcuUCollator(std::string collation)  {
    (void) milestro::skia::GetUnicodeProvider();

    UErrorCode status = U_ZERO_ERROR;
    collator = ucol_open(collation.c_str(), &status);
    if (U_FAILURE(status)) {
        auto msg =  std::format("{}: Failed to create UCollator: {}",
                                MILESTRO_FUNCTIONNAME, u_errorName(status));
        MILESTROLOG_ERROR("{}", msg);
        throw std::runtime_error(msg);
    }
}

int IcuUCollator::compare(const char *a, const char *b) {
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

}

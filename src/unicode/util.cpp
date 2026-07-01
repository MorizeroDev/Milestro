#include <Milestro/unicode/milestro_unicode_util.h>
#include <Milestro/unicode/milestro_icu.h>

namespace milestro::unicode {

std::string toLower(const std::string &locale, const std::string &data) {
    EnsureLoadICU();

    UErrorCode status = U_ZERO_ERROR;
    icu::Locale icuLocale(locale.c_str());

    std::string result;
    icu::StringByteSink sink(&result, static_cast<int32_t>( data.length()));
    icu::CaseMap::utf8ToLower(locale.c_str(), 0, data.c_str(), sink, nullptr, status);

    if (U_FAILURE(status)) {
        std::stringstream ss;
        ss << "CaseMap toLower failed: " << u_errorName(status) << std::endl;
        MILESTROLOG_ERROR("{}", ss.str());
        throw std::runtime_error(ss.str());
    }

    return result;
}

std::string toUpper(const std::string &locale, const std::string &data) {
    EnsureLoadICU();

    UErrorCode status = U_ZERO_ERROR;
    icu::Locale icuLocale(locale.c_str());

    std::string result;
    icu::StringByteSink sink(&result, static_cast<int32_t>( data.length()));
    icu::CaseMap::utf8ToUpper(locale.c_str(), 0, data.c_str(), sink, nullptr, status);

    if (U_FAILURE(status)) {
        std::stringstream ss;
        ss << "CaseMap toLower failed: " << u_errorName(status) << std::endl;
        MILESTROLOG_ERROR("{}", ss.str());
        throw std::runtime_error(ss.str());
    }

    return result;
}

}
#include <Milestro/unicode/milestro_unicode_normalize.h>
#include <Milestro/unicode/milestro_icu.h>

namespace milestro::unicode {

Normalizer::Normalizer(const std::string &name, int mode) {
    EnsureLoadICU();

    UErrorCode status = U_ZERO_ERROR;

    normalizer = icu::Normalizer2::getInstance(
            nullptr, name.c_str(), static_cast<UNormalization2Mode>(mode), status
    );

    if (U_FAILURE(status)) {
        std::stringstream ss;
        ss << "Failed to create Normalizer: " << u_errorName(status) << std::endl;
        MILESTROLOG_ERROR("{}", ss.str());
        throw std::runtime_error(ss.str());
    }
}

std::string Normalizer::normalize(const std::string &text) {
    UErrorCode status = U_ZERO_ERROR;
    icu::UnicodeString inputUStr = icu::UnicodeString::fromUTF8(text);

    // Normalize the input string using Normalizer2
    icu::UnicodeString normalizedText;
    normalizer->normalize(inputUStr, normalizedText, status);

    if (U_FAILURE(status)) {
        std::stringstream ss;
        ss << "Normalization failed: " << u_errorName(status) << std::endl;
        MILESTROLOG_ERROR("{}", ss.str());
        throw std::runtime_error(ss.str());
    }

    std::string result;
    normalizedText.toUTF8String(result);
    return result;
}

} // namespace milestro::unicode

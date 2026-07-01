#include <Milestro/unicode/milestro_unicode_segments.h>
#include <Milestro/unicode/milestro_icu.h>

namespace milestro::unicode {

Segmenter::Segmenter(const std::string &locale, const std::string &text) {
    EnsureLoadICU();

    UErrorCode status = U_ZERO_ERROR;

    inputUStr = icu::UnicodeString::fromUTF8(text);
    icu::Locale icuLocale(locale.c_str());

    wordIterator = icu::BreakIterator::createWordInstance(icuLocale, status);

    if (U_FAILURE(status)) {
        std::stringstream ss;
        ss << "Failed to create Segmenter: " << u_errorName(status) << std::endl;
        MILESTROLOG_ERROR("{}", ss.str());
        throw std::runtime_error(ss.str());
    }

    wordIterator->setText(inputUStr);
}

Segmenter::~Segmenter() {
    delete wordIterator;
}

std::string Segmenter::subString(int32_t start, int32_t len) {
    icu::UnicodeString word = inputUStr.tempSubString(start, len);
    std::string result;
    word.toUTF8String(result);
    return result;
}

} // namespace milestro::unicode

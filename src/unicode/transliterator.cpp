#include <Milestro/unicode/milestro_unicode_transliterator.h>
#include <stdexcept>
#include <sstream>
#include <Milestro/unicode/milestro_icu.h>

namespace milestro::unicode {

void PrintAllTransliterator() {
    EnsureLoadICU();

    UErrorCode status = U_ZERO_ERROR;
    auto iter = icu::Transliterator::getAvailableIDs(status);
    if (U_FAILURE(status)) {
        throw std::runtime_error("");
    }
    int *length;
    while (true) {
        auto ptr = iter->next(length, status);
        if (*length == 0 || ptr == nullptr) {
            break;
        }
        std::string data(ptr, *length);
        MILESTROLOG_INFO("{}", data);
    }
}

Transliterator::Transliterator(const std::string id, int direction) {
    EnsureLoadICU();

    icu::UnicodeString tId = icu::UnicodeString::fromUTF8(id);
    UParseError err;
    UErrorCode status = U_ZERO_ERROR;

    transliterator = icu::Transliterator::createInstance(
            tId, (UTransDirection)direction,
            err, status);

    if (U_FAILURE(status) || transliterator == nullptr) {
        std::stringstream ss;
        ss << "Failed to create Transliterator: " << u_errorName(status) << "\n"
           << "Error at line " << err.line << ", offset " << err.offset;
        MILESTROLOG_ERROR("{}", ss.str());
        throw std::runtime_error(ss.str());
    }
}

Transliterator::~Transliterator() {
    delete transliterator;
}

std::string Transliterator::transliterate(const std::string &input) {
    icu::UnicodeString ustr = icu::UnicodeString::fromUTF8(input);

    transliterator->transliterate(ustr);

    std::string output;
    ustr.toUTF8String(output);
    return output;
}

}

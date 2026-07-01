#include <Milestro/unicode/milestro_unicode_comparison.h>
#include <Milestro/unicode/milestro_icu.h>

namespace milestro::unicode {

StringComparator::StringComparator(std::string collation) {
    EnsureLoadICU();

    UErrorCode status = U_ZERO_ERROR;

    collator = ucol_open(collation.c_str(), &status);
    if (U_FAILURE(status)) {
        std::stringstream ss;
        ss << "Failed to create UCollator: " << u_errorName(status) << std::endl;
        MILESTROLOG_ERROR("{}", ss.str());
        throw std::runtime_error(ss.str());
    }
}

StringComparator::~StringComparator() {
    if (collator != nullptr) {
        ucol_close(collator);
        collator = nullptr;
    }
}

int StringComparator::compare(const char *a, const char *b) {
    icu::UnicodeString ustr_a = icu::UnicodeString::fromUTF8(a);
    icu::UnicodeString ustr_b = icu::UnicodeString::fromUTF8(b);
    return ucol_strcoll(collator, ustr_a.getBuffer(), ustr_a.length(), ustr_b.getBuffer(), ustr_b.length());
}

} // namespace milestro::unicode

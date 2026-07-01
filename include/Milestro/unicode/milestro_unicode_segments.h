#ifndef MILESTRO_UNICODE_SEGMENT_H
#define MILESTRO_UNICODE_SEGMENT_H

#include <iostream>
#include <Milestro/common/milestro_result.h>
#include <Milestro/log/log.h>
#include <Milestro/util/milestro_class.h>
#include <sstream>
#include <string>
#include <unicode/brkiter.h>
#include <unicode/casemap.h>
#include <unicode/locid.h>
#include <unicode/normalizer2.h>
#include <unicode/ustring.h>
#include <unicode/utypes.h>

namespace milestro::unicode {

class Segmenter {

public:
    Segmenter(const std::string& locale, const std::string& text);

    ~Segmenter();

    MILESTRO_DECLARE_NON_COPYABLE(Segmenter);

    int32_t first() {
        return wordIterator->first();
    }

    int32_t next() {
        return wordIterator->next();
    }

    int32_t current() {
        return wordIterator->current();
    }

    int32_t previous() {
        return wordIterator->previous();
    }

    std::string subString(int32_t start, int32_t len);

private:
    icu::BreakIterator* wordIterator = nullptr;
    icu::UnicodeString inputUStr;
};


} // namespace milestro::unicode

#endif //MILESTRO_UNICODE_COMPARISON_H

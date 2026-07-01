#ifndef MILESTRO_UNICODE_NORMALIZE_H
#define MILESTRO_UNICODE_NORMALIZE_H

#include <Milestro/common/milestro_result.h>
#include <Milestro/log/log.h>
#include <Milestro/util/milestro_class.h>
#include <iostream>
#include <sstream>
#include <string>
#include <unicode/brkiter.h>
#include <unicode/casemap.h>
#include <unicode/locid.h>
#include <unicode/normalizer2.h>
#include <unicode/ustring.h>
#include <unicode/utypes.h>

namespace milestro::unicode {

class Normalizer {
public:
    Normalizer(const std::string& name, int mode);

    MILESTRO_DECLARE_NON_COPYABLE(Normalizer);

    std::string normalize(const std::string& text);

private:
    const icu::Normalizer2* normalizer = nullptr;
};

} // namespace milestro::unicode

#endif //MILESTRO_UNICODE_COMPARISON_H

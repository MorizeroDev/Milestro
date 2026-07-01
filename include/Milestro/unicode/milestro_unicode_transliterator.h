#ifndef MILESTRO_UNICODE_TRANSLITERATOR_H
#define MILESTRO_UNICODE_TRANSLITERATOR_H

#include <iostream>
#include <Milestro/common/milestro_result.h>
#include <Milestro/log/log.h>
#include <Milestro/util/milestro_class.h>
#include <sstream>
#include <string>
#include <unicode/translit.h>

namespace milestro::unicode {


class MILESTRO_API Transliterator {
public:
    MILESTRO_DECLARE_NON_COPYABLE(Transliterator)

    Transliterator(const std::string id, int direction);

    ~Transliterator();

    std::string transliterate(const std::string& input);

private:
    icu::Transliterator* transliterator;
};


} // namespace milestro::unicode

#endif //MILESTRO_UNICODE_TRANSLITERATOR_H

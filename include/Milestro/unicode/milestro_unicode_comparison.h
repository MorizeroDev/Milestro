#ifndef MILESTRO_UNICODE_COMPARISON_H
#define MILESTRO_UNICODE_COMPARISON_H

#include <Milestro/common/milestro_result.h>
#include <Milestro/log/log.h>
#include <Milestro/util/milestro_class.h>
#include <iostream>
#include <sstream>
#include <string>
#include <unicode/ucol.h>
#include <unicode/unistr.h>
#include <unicode/ustring.h>

namespace milestro::unicode {

class StringComparator {

public:
    StringComparator(std::string collation);

    ~StringComparator();

    MILESTRO_DECLARE_NON_COPYABLE(StringComparator);

    int compare(const char* a, const char* b);

private:
    UCollator* collator = nullptr;
};

} // namespace milestro::unicode

#endif //MILESTRO_UNICODE_COMPARISON_H

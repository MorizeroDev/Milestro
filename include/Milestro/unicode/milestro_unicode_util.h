#ifndef MILESTRO_UNICODE_UTIL_H
#define MILESTRO_UNICODE_UTIL_H

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

std::string toLower(const std::string& locale, const std::string& data);

std::string toUpper(const std::string& locale, const std::string& data);

} // namespace milestro::unicode

#endif //MILESTRO_UNICODE_COMPARISON_H

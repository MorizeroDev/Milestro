#ifndef MILESTRO_MILESTRO_ENV_H
#define MILESTRO_MILESTRO_ENV_H

#include <string>

namespace milestro::util::env {

#ifdef _MSC_VER
inline std::string getenv(std::string key) {
    char* home = nullptr;
    size_t size = 0;
    if (_dupenv_s(&home, &size, key.c_str()) == 0 && home != nullptr) {
        std::string homeStr(home);
        free(home);
        return homeStr;
    }
    return "";
}
#else

inline std::string getenv(std::string key) {
    char* home = ::getenv(key.c_str());
    if (home != nullptr) {
        return std::string(home);
    }
    return "";
}
#endif

template <typename R>
inline R getenv(std::string key, R defaultValue);

template <>
inline bool getenv(std::string key, bool defaultValue) {
    std::string s = getenv(std::move(key));
    if (s.empty()) {
        return defaultValue;
    }
    std::transform(s.begin(), s.end(), s.begin(), [](unsigned char c) {
        return std::tolower(c);
    });
    return s == "on" || s == "true" || s == "1";
}

} // namespace milestro::util::env

#endif

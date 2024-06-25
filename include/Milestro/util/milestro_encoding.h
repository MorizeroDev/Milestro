#ifndef MILESTRO_ENCODING_H
#define MILESTRO_ENCODING_H

#if defined(WIN32) || defined(_WIN32) || defined(__WIN32__) || defined(__NT__)
#include <string>
#include <windows.h>
#endif

namespace milestro::util::encoding {

#if defined(WIN32) || defined(_WIN32) || defined(__WIN32__) || defined(__NT__)
inline std::string WStringToString(const std::wstring& wstr) {
    if (wstr.empty())
        return {};
    int size_needed = WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int) wstr.size(), nullptr, 0, nullptr, nullptr);
    std::string strTo(size_needed, 0);
    WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int) wstr.size(), &strTo[0], size_needed, nullptr, nullptr);
    return strTo;
}

inline std::wstring StringToWString(const std::string& str) {
    if (str.empty())
        return {};
    int size_needed = MultiByteToWideChar(CP_UTF8, 0, &str[0], (int) str.size(), nullptr, 0);
    std::wstring wstrTo(size_needed, 0);
    MultiByteToWideChar(CP_UTF8, 0, &str[0], (int) str.size(), &wstrTo[0], size_needed);
    return wstrTo;
}

#endif
} // namespace milestro::util::encoding

#endif

#ifndef MILESTRO_FONTFAMILYDECLARATION_H
#define MILESTRO_FONTFAMILYDECLARATION_H

#include "Milestro/common/milestro_export_macros.h"
#include <cstdint>
#include <string>
#include <utility>

namespace milestro::skia {

enum class FontFamilyTokenKind : int32_t {
    Bare = 0,
    Exact = 1,
};

class MILESTRO_API FontFamilyToken {
public:
    FontFamilyToken() = default;

    FontFamilyToken(FontFamilyTokenKind kind, std::string value)
        : kind(kind), value(std::move(value)) {
    }

    static FontFamilyToken Bare(std::string value) {
        return FontFamilyToken(FontFamilyTokenKind::Bare, std::move(value));
    }

    static FontFamilyToken Exact(std::string value) {
        return FontFamilyToken(FontFamilyTokenKind::Exact, std::move(value));
    }

    FontFamilyTokenKind kind = FontFamilyTokenKind::Bare;
    std::string value;
};

enum class FontFamilyCandidateKind : int32_t {
    Named = 0,
    SystemDefault = 1,
};

class MILESTRO_API FontFamilyCandidate {
public:
    FontFamilyCandidate() = default;

    FontFamilyCandidate(FontFamilyCandidateKind kind, std::string value)
        : kind(kind), value(std::move(value)) {
    }

    static FontFamilyCandidate Named(std::string value) {
        return FontFamilyCandidate(FontFamilyCandidateKind::Named, std::move(value));
    }

    static FontFamilyCandidate SystemDefault() {
        return FontFamilyCandidate(FontFamilyCandidateKind::SystemDefault, "");
    }

    FontFamilyCandidateKind kind = FontFamilyCandidateKind::Named;
    std::string value;
};

} // namespace milestro::skia

#endif // MILESTRO_FONTFAMILYDECLARATION_H

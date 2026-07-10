#ifndef MILESTRO_GAME_SKIA_FONTFAMILYTOKENS_H
#define MILESTRO_GAME_SKIA_FONTFAMILYTOKENS_H

#include "Milestro/skia/FontFamilyDeclaration.h"
#include <cstddef>
#include <cstdint>
#include <string>
#include <utility>
#include <vector>

inline std::vector<milestro::skia::FontFamilyToken> ReadFamilyTokens(
        uint8_t **families,
        const int32_t *kinds,
        uint32_t familyCount) {
    std::vector<milestro::skia::FontFamilyToken> ret;
    if (families == nullptr || familyCount == 0) {
        return ret;
    }

    ret.reserve(static_cast<size_t>(familyCount));
    for (uint32_t i = 0; i < familyCount; ++i) {
        if (families[i] == nullptr) {
            continue;
        }

        std::string family(reinterpret_cast<const char *>(families[i]));
        if (family.empty()) {
            continue;
        }

        const auto kind = kinds == nullptr
                ? milestro::skia::FontFamilyTokenKind::Bare
                : static_cast<milestro::skia::FontFamilyTokenKind>(kinds[i]);
        if (kind == milestro::skia::FontFamilyTokenKind::Exact) {
            ret.emplace_back(milestro::skia::FontFamilyToken::Exact(std::move(family)));
        } else {
            ret.emplace_back(milestro::skia::FontFamilyToken::Bare(std::move(family)));
        }
    }

    return ret;
}

inline std::vector<milestro::skia::FontFamilyToken> ReadBareFamilyTokens(
        uint8_t **families,
        uint32_t familyCount) {
    return ReadFamilyTokens(families, nullptr, familyCount);
}

#endif // MILESTRO_GAME_SKIA_FONTFAMILYTOKENS_H

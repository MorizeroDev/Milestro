#include "Milestro/skia/FontFamilyKeywordMap.h"
#include "Milestro/common/milestro_platform.h"
#include <algorithm>
#include <cctype>
#include <initializer_list>
#include <utility>

namespace milestro::skia {

namespace {

std::string Trim(const std::string &value) {
    auto begin = value.begin();
    while (begin != value.end() && std::isspace(static_cast<unsigned char>(*begin))) {
        ++begin;
    }

    auto end = value.end();
    while (end != begin && std::isspace(static_cast<unsigned char>(*(end - 1)))) {
        --end;
    }

    return std::string(begin, end);
}

std::string LowerAscii(std::string value) {
    std::transform(value.begin(),
                   value.end(),
                   value.begin(),
                   [](unsigned char c) { return static_cast<char>(std::tolower(c)); });
    return value;
}

std::string CandidateKey(const FontFamilyCandidate &candidate) {
    if (candidate.kind == FontFamilyCandidateKind::SystemDefault) {
        return "\x1Fmilestro-system-default";
    }

    return LowerAscii(candidate.value);
}

void AddCandidate(std::vector<FontFamilyCandidate> &candidates,
                  std::unordered_set<std::string> &seenCandidates,
                  FontFamilyCandidate candidate) {
    if (candidate.kind == FontFamilyCandidateKind::Named && candidate.value.empty()) {
        return;
    }

    auto key = CandidateKey(candidate);
    if (!seenCandidates.insert(std::move(key)).second) {
        return;
    }

    candidates.emplace_back(std::move(candidate));
}

std::vector<FontFamilyToken> ExactTokens(std::initializer_list<const char *> families) {
    std::vector<FontFamilyToken> tokens;
    tokens.reserve(families.size() + 1);
    for (auto family: families) {
        tokens.emplace_back(FontFamilyToken::Exact(family));
    }

    tokens.emplace_back(FontFamilyToken::Bare("system-ui"));
    return tokens;
}

} // namespace

std::string NormalizeFontFamilyKeyword(const std::string &keyword) {
    return LowerAscii(Trim(keyword));
}

void FontFamilyKeywordMap::ClearUserMappings() {
    userMappings.clear();
}

void FontFamilyKeywordMap::SetUserMapping(std::string keyword, std::vector<FontFamilyToken> mapping) {
    auto normalizedKeyword = NormalizeFontFamilyKeyword(keyword);
    if (normalizedKeyword.empty()) {
        return;
    }

    userMappings[std::move(normalizedKeyword)] = std::move(mapping);
}

std::vector<FontFamilyCandidate> FontFamilyKeywordMap::Resolve(const std::vector<FontFamilyToken> &tokens) const {
    std::vector<FontFamilyCandidate> candidates;
    std::unordered_set<std::string> seenCandidates;
    std::unordered_set<std::string> visitingKeywords;

    for (const auto &token: tokens) {
        ResolveToken(token, candidates, seenCandidates, visitingKeywords);
    }

    return candidates;
}

bool FontFamilyKeywordMap::TryGetKeywordMapping(const std::string &keyword, std::vector<FontFamilyToken> &mapping) const {
    auto normalizedKeyword = NormalizeFontFamilyKeyword(keyword);
    if (normalizedKeyword.empty()) {
        return false;
    }

    auto configured = userMappings.find(normalizedKeyword);
    if (configured != userMappings.end()) {
        mapping = configured->second;
        return true;
    }

    return TryGetDefaultKeywordMapping(normalizedKeyword, mapping);
}

bool FontFamilyKeywordMap::TryGetDefaultKeywordMapping(const std::string &keyword,
                                                       std::vector<FontFamilyToken> &mapping) const {
    auto normalizedKeyword = NormalizeFontFamilyKeyword(keyword);
    if (normalizedKeyword == "serif") {
#if MILESTRO_PLATFORM_MAC || MILESTRO_PLATFORM_IOS
        mapping = ExactTokens({"Times New Roman", "Georgia", "New York", "Noto Serif"});
#elif MILESTRO_PLATFORM_WINDOWS
        mapping = ExactTokens({"Times New Roman", "Georgia", "Noto Serif"});
#elif MILESTRO_PLATFORM_ANDROID
        mapping = ExactTokens({"Noto Serif", "Noto Serif CJK SC", "Droid Serif"});
#else
        mapping = ExactTokens({"Noto Serif", "DejaVu Serif", "Times New Roman", "Georgia"});
#endif
        return true;
    }

    if (normalizedKeyword == "sans-serif") {
#if MILESTRO_PLATFORM_MAC || MILESTRO_PLATFORM_IOS
        mapping = {
            FontFamilyToken::Exact("Source Han Sans VF"),
            FontFamilyToken::Bare("system-ui"),
            FontFamilyToken::Exact("Helvetica Neue"),
            FontFamilyToken::Exact("Helvetica"),
            FontFamilyToken::Exact("Arial"),
            FontFamilyToken::Exact("Noto Sans"),
        };
#elif MILESTRO_PLATFORM_WINDOWS
        mapping = {
            FontFamilyToken::Exact("Source Han Sans VF"),
            FontFamilyToken::Bare("system-ui"),
            FontFamilyToken::Exact("Arial"),
            FontFamilyToken::Exact("Noto Sans"),
        };
#elif MILESTRO_PLATFORM_ANDROID
        mapping = {
            FontFamilyToken::Exact("Source Han Sans VF"),
            FontFamilyToken::Bare("system-ui"),
            FontFamilyToken::Exact("Noto Sans CJK SC"),
            FontFamilyToken::Exact("Noto Sans"),
            FontFamilyToken::Exact("Droid Sans"),
        };
#else
        mapping = {
            FontFamilyToken::Exact("Source Han Sans VF"),
            FontFamilyToken::Exact("Noto Sans"),
            FontFamilyToken::Exact("DejaVu Sans"),
            FontFamilyToken::Exact("Arial"),
            FontFamilyToken::Bare("system-ui"),
        };
#endif
        return true;
    }

    if (normalizedKeyword == "monospace") {
#if MILESTRO_PLATFORM_MAC || MILESTRO_PLATFORM_IOS
        mapping = ExactTokens({"Menlo", "Monaco", "SF Mono", "Courier New", "Noto Sans Mono"});
#elif MILESTRO_PLATFORM_WINDOWS
        mapping = ExactTokens({"Consolas", "Cascadia Mono", "Courier New", "Noto Sans Mono"});
#elif MILESTRO_PLATFORM_ANDROID
        mapping = ExactTokens({"Roboto Mono", "Droid Sans Mono", "Noto Sans Mono"});
#else
        mapping = ExactTokens({"Noto Sans Mono", "DejaVu Sans Mono", "Liberation Mono", "Courier New"});
#endif
        return true;
    }

    return false;
}

void FontFamilyKeywordMap::ResolveToken(const FontFamilyToken &token,
                                        std::vector<FontFamilyCandidate> &candidates,
                                        std::unordered_set<std::string> &seenCandidates,
                                        std::unordered_set<std::string> &visitingKeywords) const {
    switch (token.kind) {
        case FontFamilyTokenKind::Exact:
            AddCandidate(candidates, seenCandidates, FontFamilyCandidate::Named(token.value));
            return;
        case FontFamilyTokenKind::Bare:
            ResolveBareToken(token.value, candidates, seenCandidates, visitingKeywords);
            return;
    }
}

void FontFamilyKeywordMap::ResolveBareToken(const std::string &value,
                                            std::vector<FontFamilyCandidate> &candidates,
                                            std::unordered_set<std::string> &seenCandidates,
                                            std::unordered_set<std::string> &visitingKeywords) const {
    auto family = Trim(value);
    auto normalizedKeyword = NormalizeFontFamilyKeyword(family);
    if (normalizedKeyword.empty()) {
        return;
    }

    std::vector<FontFamilyToken> mapping;
    if (TryGetKeywordMapping(normalizedKeyword, mapping)) {
        if (!visitingKeywords.insert(normalizedKeyword).second) {
            return;
        }

        for (const auto &mappedToken: mapping) {
            ResolveToken(mappedToken, candidates, seenCandidates, visitingKeywords);
        }

        visitingKeywords.erase(normalizedKeyword);
        return;
    }

    if (normalizedKeyword == "system-ui") {
        if (!visitingKeywords.insert(normalizedKeyword).second) {
            return;
        }

        ResolveSystemUi(candidates, seenCandidates);
        visitingKeywords.erase(normalizedKeyword);
        return;
    }

    AddCandidate(candidates, seenCandidates, FontFamilyCandidate::Named(family));
}

void FontFamilyKeywordMap::ResolveSystemUi(std::vector<FontFamilyCandidate> &candidates,
                                           std::unordered_set<std::string> &seenCandidates) const {
#if MILESTRO_PLATFORM_MAC || MILESTRO_PLATFORM_IOS
    AddCandidate(candidates, seenCandidates, FontFamilyCandidate::Named(".AppleSystemUIFont"));
    AddCandidate(candidates, seenCandidates, FontFamilyCandidate::Named("SF Pro Text"));
    AddCandidate(candidates, seenCandidates, FontFamilyCandidate::Named("Helvetica Neue"));
    AddCandidate(candidates, seenCandidates, FontFamilyCandidate::Named("Helvetica"));
    AddCandidate(candidates, seenCandidates, FontFamilyCandidate::Named("Arial"));
#elif MILESTRO_PLATFORM_WINDOWS
    AddCandidate(candidates, seenCandidates, FontFamilyCandidate::Named("Segoe UI"));
    AddCandidate(candidates, seenCandidates, FontFamilyCandidate::Named("Arial"));
#elif MILESTRO_PLATFORM_ANDROID
    AddCandidate(candidates, seenCandidates, FontFamilyCandidate::Named("Roboto"));
    AddCandidate(candidates, seenCandidates, FontFamilyCandidate::Named("Noto Sans"));
    AddCandidate(candidates, seenCandidates, FontFamilyCandidate::Named("Droid Sans"));
#else
    AddCandidate(candidates, seenCandidates, FontFamilyCandidate::Named("Noto Sans"));
    AddCandidate(candidates, seenCandidates, FontFamilyCandidate::Named("DejaVu Sans"));
    AddCandidate(candidates, seenCandidates, FontFamilyCandidate::Named("Arial"));
#endif
    AddCandidate(candidates, seenCandidates, FontFamilyCandidate::SystemDefault());
}

} // namespace milestro::skia

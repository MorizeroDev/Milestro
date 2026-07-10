#ifndef MILESTRO_FONTFAMILYKEYWORDMAP_H
#define MILESTRO_FONTFAMILYKEYWORDMAP_H

#include "FontFamilyDeclaration.h"
#include "Milestro/common/milestro_export_macros.h"
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

namespace milestro::skia {

class MILESTRO_API FontFamilyKeywordMap {
public:
    void ClearUserMappings();
    void SetUserMapping(std::string keyword, std::vector<FontFamilyToken> mapping);

    std::vector<FontFamilyCandidate> Resolve(const std::vector<FontFamilyToken> &tokens) const;

private:
    bool TryGetKeywordMapping(const std::string &keyword, std::vector<FontFamilyToken> &mapping) const;
    bool TryGetDefaultKeywordMapping(const std::string &keyword, std::vector<FontFamilyToken> &mapping) const;

    void ResolveToken(const FontFamilyToken &token,
                      std::vector<FontFamilyCandidate> &candidates,
                      std::unordered_set<std::string> &seenCandidates,
                      std::unordered_set<std::string> &visitingKeywords) const;
    void ResolveBareToken(const std::string &value,
                          std::vector<FontFamilyCandidate> &candidates,
                          std::unordered_set<std::string> &seenCandidates,
                          std::unordered_set<std::string> &visitingKeywords) const;
    void ResolveSystemUi(std::vector<FontFamilyCandidate> &candidates,
                         std::unordered_set<std::string> &seenCandidates) const;

    std::unordered_map<std::string, std::vector<FontFamilyToken>> userMappings;
};

MILESTRO_API std::string NormalizeFontFamilyKeyword(const std::string &keyword);

} // namespace milestro::skia

#endif // MILESTRO_FONTFAMILYKEYWORDMAP_H

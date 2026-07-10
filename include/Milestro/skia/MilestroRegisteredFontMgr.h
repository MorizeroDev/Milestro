#ifndef MILESTRO_MILESTROREGISTEREDFONTMGR_H
#define MILESTRO_MILESTROREGISTEREDFONTMGR_H

#include "include/core/SkFontMgr.h"
#include "include/core/SkFontScanner.h"
#include "include/core/SkFontStyle.h"
#include "include/core/SkRefCnt.h"
#include "include/core/SkString.h"
#include "include/core/SkTypes.h"
#include "Milestro/common/milestro_export_macros.h"
#include "FontRegistryTypes.h"
#include <cstdint>
#include <string>
#include <vector>
#include <src/ports/SkTypeface_FreeType.h>

class SkData;
class SkStreamAsset;
class SkTypeface;

namespace milestro::skia {

class MILESTRO_API MilestroRegisteredFontStyleSet : public SkFontStyleSet {
public:
    explicit MilestroRegisteredFontStyleSet(SkString familyName);

    /** Should only be called during the initial build phase. */
    void appendTypeface(sk_sp<SkTypeface> typeface);
    int count() override;
    void getStyle(int index, SkFontStyle *style, SkString *name) override;
    sk_sp<SkTypeface> createTypeface(int index) override;
    sk_sp<SkTypeface> matchStyle(const SkFontStyle &pattern) override;
    SkString getFamilyName();

private:
    skia_private::TArray<sk_sp<SkTypeface>> fStyles;
    SkString fFamilyName;

    friend class MilestroRegisteredFontMgr;
};

// SkFontMgr for fonts explicitly registered from Milestro asset files.
// It intentionally keeps the FreeType scanner/typeface path because it preserves
// TTC face indices and named variable-font instances across platforms.
class MILESTRO_API MilestroRegisteredFontMgr : public SkFontMgr {
public:
    enum class RegisterResult : int32_t {
        Succeed = 0,
        Duplicated = 1,
        Failed = -1,
    };

    explicit MilestroRegisteredFontMgr();

    MilestroRegisteredFontMgr::RegisterResult registerFont(std::unique_ptr<SkStreamAsset> stream, const SkString &filename);
    std::vector<MilestroFontFaceInfo> getFontFaces() const;

protected:
    int onCountFamilies() const override;
    void onGetFamilyName(int index, SkString *familyName) const override;
    sk_sp<SkFontStyleSet> onCreateStyleSet(int index) const override;
    sk_sp<SkFontStyleSet> onMatchFamily(const char familyName[]) const override;
    sk_sp<SkTypeface> onMatchFamilyStyle(const char familyName[],
                                         const SkFontStyle &fontStyle) const override;
    sk_sp<SkTypeface> onMatchFamilyStyleCharacter(const char familyName[], const SkFontStyle &,
                                                  const char *bcp47[], int bcp47Count,
                                                  SkUnichar character) const override;
    sk_sp<SkTypeface> onMakeFromData(sk_sp<SkData> data, int ttcIndex) const override;
    sk_sp<SkTypeface> onMakeFromStreamIndex(std::unique_ptr<SkStreamAsset>, int ttcIndex) const override;
    sk_sp<SkTypeface> onMakeFromStreamArgs(std::unique_ptr<SkStreamAsset>, const SkFontArguments &) const override;
    sk_sp<SkTypeface> onMakeFromFile(const char path[], int ttcIndex) const override;
    sk_sp<SkTypeface> onLegacyMakeTypeface(const char familyName[], SkFontStyle style) const override;

private:
    std::unique_ptr<SkFontScanner> fScanner;
    std::vector<sk_sp<MilestroRegisteredFontStyleSet>> fFamilies;
    std::vector<MilestroFontFaceInfo> fFaces;
    std::vector<SkString> fFontRegistered;
//    std::vector<std::unique_ptr<SkStreamAsset>> fStreamHolder;
};

}

#endif //MILESTRO_MILESTROREGISTEREDFONTMGR_H

#ifndef MILESTRO_MILESTROFONTMANAGER_H
#define MILESTRO_MILESTROFONTMANAGER_H

#include "include/core/SkFontMgr.h"
#include "include/core/SkFontStyle.h"
#include "include/core/SkRefCnt.h"
#include "include/core/SkString.h"
#include "include/core/SkTypes.h"
#include "include/private/base/SkTArray.h"
#include <vector>

class SkData;
class SkStreamAsset;
class SkTypeface;

namespace milestro::skia {

class MilestroFontStyleSet : public SkFontStyleSet {
public:
    explicit MilestroFontStyleSet(SkString familyName);

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

    friend class MilestroFontManager;
};

class MilestroFontManager : public SkFontMgr {
public:
    explicit MilestroFontManager();
    void registerTypeFace(sk_sp<SkTypeface> typeFace);

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
    bool isValidAndUniqueFontName(SkString name);

    std::vector<sk_sp<MilestroFontStyleSet>> fFamilies;
    sk_sp<SkFontStyleSet> fDefaultFamily;

    sk_sp<SkFontMgr> backend;
};

}

#endif

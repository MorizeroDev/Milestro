#ifndef MILESTRO_MILESTROFONTMANAGER_H
#define MILESTRO_MILESTROFONTMANAGER_H

#include "include/core/SkFontMgr.h"
#include "include/core/SkFontScanner.h"
#include "include/core/SkFontStyle.h"
#include "include/core/SkRefCnt.h"
#include "include/core/SkString.h"
#include "include/core/SkTypes.h"
#include "Milestro/common/milestro_export_macros.h"
#include <cstdint>
#include <string>
#include <vector>
#include <src/ports/SkTypeface_FreeType.h>

class SkData;
class SkStreamAsset;
class SkTypeface;

namespace milestro::skia {

class MILESTRO_API MilestroFontFamilyInfo {
public:
    std::string name;
};

class MILESTRO_API MilestroFontFamilyList {
public:
    explicit MilestroFontFamilyList(std::vector<MilestroFontFamilyInfo> data);

    MilestroFontFamilyInfo *At(size_t position);
    MilestroFontFamilyInfo Get(size_t position) const;
    size_t Size() const;

private:
    std::vector<MilestroFontFamilyInfo> data;
};

class MILESTRO_API MilestroFontFaceInfo {
public:
    std::string sourcePath;
    std::string familyName;
    int32_t faceIndex = 0;
    int32_t instanceIndex = 0;
    int32_t packedIndex = 0;
    int32_t weight = 0;
    int32_t width = 0;
    int32_t slant = 0;
    bool fixedPitch = false;
};

class MILESTRO_API MilestroFontFaceList {
public:
    explicit MilestroFontFaceList(std::vector<MilestroFontFaceInfo> data);

    MilestroFontFaceInfo *At(size_t position);
    MilestroFontFaceInfo Get(size_t position) const;
    size_t Size() const;

private:
    std::vector<MilestroFontFaceInfo> data;
};

class MILESTRO_API MilestroFontStyleSet : public SkFontStyleSet {
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

class MILESTRO_API MilestroFontManager : public SkFontMgr {
public:
    enum class RegisterResult : int32_t {
        Succeed = 0,
        Duplicated = 1,
        Failed = -1,
    };

    explicit MilestroFontManager();

    MilestroFontManager::RegisterResult registerFont(std::unique_ptr<SkStreamAsset> stream, const SkString &filename);
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
    std::vector<sk_sp<MilestroFontStyleSet>> fFamilies;
    std::vector<MilestroFontFaceInfo> fFaces;
    std::vector<SkString> fFontRegistered;
//    std::vector<std::unique_ptr<SkStreamAsset>> fStreamHolder;
};

}

#endif

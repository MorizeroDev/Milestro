#ifndef MILESTRO_FONTREGISTRYTYPES_H
#define MILESTRO_FONTREGISTRYTYPES_H

#include "Milestro/common/milestro_export_macros.h"
#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

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

} // namespace milestro::skia

#endif // MILESTRO_FONTREGISTRYTYPES_H

#ifndef MILESTRO_ICU_H
#define MILESTRO_ICU_H

#include <string>

namespace milestro::unicode {

bool CopyAndLoadICU(uint8_t* dataPtr, size_t size, std::string path);

bool LoadICU(void* dataPtr, std::string path);

void EnsureLoadICU();

} // namespace milestro::unicode

#endif //MILESTRO_MILESTRO_ICU_H

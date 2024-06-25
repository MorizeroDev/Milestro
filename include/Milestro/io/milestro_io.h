#ifndef MILESTRO_IO_H
#define MILESTRO_IO_H

#include <Milestro/common/milestro_export_macros.h>
#include <cstdint>
#include <string>
#include <vector>

namespace milestro::io {
enum class FileStatus {
    FileSystemError,
    Exists,
    NotExists,
    Directory,
};

MILESTRO_API FileStatus isFileExists(const std::string &path);

MILESTRO_API std::vector<uint8_t> readFile(const std::string &path);

MILESTRO_API void writeFile(const std::string &path, const std::vector<uint8_t> &data);

MILESTRO_API void writeFile(const std::string &path, const uint8_t *data, size_t size);
} // namespace Milestro::io


#endif //MILESTRO_UTILS_IO_H

#include <Milestro/io/milestro_io.h>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <vector>

namespace milestro::io {
inline std::u8string toU8String(const std::string& s) {
    return std::u8string(reinterpret_cast<const char8_t*>(s.data()), s.size());
}

FileStatus isFileExists(const std::string& path) {
    if (path.length() == 0) {
        return FileStatus::NotExists;
    }

    std::error_code ec;
#ifdef _WIN32
    auto stat = std::filesystem::status(toU8String(path), ec);
#else
    auto stat = std::filesystem::status(path, ec);
#endif
    if (ec.value() == 2) {
        return FileStatus::NotExists;
    }
    if (ec) {
        return FileStatus::FileSystemError;
    }
    switch (stat.type()) {
        case std::filesystem::file_type::none: // LCOV_EXCL_LINE
        case std::filesystem::file_type::not_found:
            return FileStatus::NotExists;
        case std::filesystem::file_type::directory:
            return FileStatus::Directory;
        case std::filesystem::file_type::symlink:
        case std::filesystem::file_type::block:
        case std::filesystem::file_type::character:
        case std::filesystem::file_type::fifo:
        case std::filesystem::file_type::socket:
        case std::filesystem::file_type::regular:
        case std::filesystem::file_type::unknown:
        default:
            return FileStatus::Exists;
    }
}

std::vector<uint8_t> readFile(const std::string& path) {
    // c++20风格，解决windows中文路径问题
#ifdef _WIN32
    std::filesystem::path filePath(toU8String(path));
    std::ifstream input(filePath, std::ios::binary);
#else
    std::ifstream input(path, std::ios::binary);
#endif
    if (!input) {
        throw std::runtime_error("Unable to open file: " + path);
    }

    std::vector<uint8_t> bytes((std::istreambuf_iterator<char>(input)), (std::istreambuf_iterator<char>()));

    // 虽然它是RAII会自己释放，但手动释放也行
    input.close();
    return bytes;
}

void writeFile(const std::string& path, const std::vector<uint8_t>& data) {
    std::ofstream target;
#ifdef _WIN32
    std::filesystem::path filePath(toU8String(path));
    target.open(filePath, std::ios::binary | std::ios::out | std::ios::trunc);
#else
    target.open(path, std::ios::binary | std::ios::out | std::ios::trunc);
#endif

    target.write(reinterpret_cast<const char*>(data.data()), data.size());
    target.close();
}

void writeFile(const std::string& path, const uint8_t* data, size_t size) {
    std::ofstream target;
#ifdef _WIN32
    std::filesystem::path filePath(toU8String(path));
    target.open(filePath, std::ios::binary | std::ios::out | std::ios::trunc);
#else
    target.open(path, std::ios::binary | std::ios::out | std::ios::trunc);
#endif

    target.write(reinterpret_cast<const char*>(data), size);
    target.close();
}
} // namespace milestro::io

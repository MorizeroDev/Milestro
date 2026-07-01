#include <Milestro/common/milestro_platform.h>

#if MILIZE_PLATFORM_WINDOWS

#include <Milestro/util/milestro_encoding.h>

#else

#include <cerrno>
#include <fcntl.h>
#include <filesystem>
#include <sys/mman.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <unistd.h>

#endif

#include <Milestro/io/milestro_io.h>
#include <Milestro/log/log.h>
#include <Milestro/unicode/milestro_icu.h>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>

#include "unicode/udata.h"

#if MILIZE_PLATFORM_WINDOWS

#include <io.h>
#include <windows.h>

#endif

namespace milestro::unicode {
static bool init_icu(void* addr) {
    if (addr == nullptr) {
        return false;
    }

    UErrorCode err = U_ZERO_ERROR;
    udata_setCommonData(addr, &err);
    if (err != U_ZERO_ERROR) {
        MILESTROLOG_CRITICAL("udata_setCommonData() returned {}.", (int) err);
        return false;
    }
    udata_setFileAccess(UDATA_ONLY_PACKAGES, &err);
    if (err != U_ZERO_ERROR) {
        MILESTROLOG_CRITICAL("udata_setFileAccess() returned {}.", (int) err);
        return false;
    }
    return true;
}

#if MILIZE_PLATFORM_WINDOWS

static void* win_mmap(const wchar_t* dataFile) {
    if (!dataFile) {
        return nullptr;
    }
    struct FCloseWrapper {
        void operator()(FILE* f) {
            fclose(f);
        }
    };
    std::unique_ptr<FILE, FCloseWrapper> stream(_wfopen(dataFile, L"rb"));
    if (!stream) {
        MILESTROLOG_CRITICAL("win_mmap: datafile missing: {}.", milestro::util::encoding::WStringToString(dataFile));
        return nullptr;
    }
    int fileno = _fileno(stream.get());
    if (fileno < 0) {
        MILESTROLOG_CRITICAL("win_mmap: datafile fileno error.");
        return nullptr;
    }
    HANDLE file = (HANDLE) _get_osfhandle(fileno);
    if ((HANDLE) INVALID_HANDLE_VALUE == file) {
        MILESTROLOG_CRITICAL("win_mmap: datafile handle error.");
        return nullptr;
    }
    struct CloseHandleWrapper {
        void operator()(HANDLE h) {
            CloseHandle(h);
        }
    };
    std::unique_ptr<void, CloseHandleWrapper> mmapHandle(
            CreateFileMapping(file, nullptr, PAGE_READONLY, 0, 0, nullptr));
    if (!mmapHandle) {
        MILESTROLOG_CRITICAL("win_mmap: datafile mmap error.");
        return nullptr;
    }
    void* addr = MapViewOfFile(mmapHandle.get(), FILE_MAP_READ, 0, 0, 0);
    if (nullptr == addr) {
        MILESTROLOG_CRITICAL("win_mmap: datafile view error.");
        return nullptr;
    }
    return addr;
}

static std::wstring get_module_path(HMODULE module) {
    DWORD len;
    std::wstring path;
    path.resize(MAX_PATH);

    len = GetModuleFileNameW(module, (LPWSTR) path.data(), (DWORD) path.size());
    if (len > path.size()) {
        path.resize(len);
        len = GetModuleFileNameW(module, (LPWSTR) path.data(), (DWORD) path.size());
    }
    path.resize(len);
    std::size_t end = path.rfind('\\');
    if (end == std::wstring::npos) {
        return std::wstring();
    }
    path.resize(end);
    return path;
}

static std::wstring library_directory() {
    HMODULE hModule = NULL;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS, reinterpret_cast<LPCSTR>(&library_directory), &hModule);
    return get_module_path(hModule);
}

static std::wstring executable_directory() {
    HMODULE hModule = GetModuleHandleA(NULL);
    return get_module_path(hModule);
}

static bool load_from(const std::wstring& path) {
    if (path.empty()) {
        return false;
    }

    auto status = milestro::io::isFileExists(path);
    auto sPath = path;
    if (status == milestro::io::FileStatus::Directory) {
        sPath = sPath + L"\\icudtl.dat";
    }
    if (void* addr = win_mmap(sPath.c_str())) {
        if (init_icu(addr)) {
            return true;
        }
    }
    return false;
}

bool LoadIcuImpl(void* dataPtr, std::string& dir) {
    std::wstring wDir = milestro::util::encoding::StringToWString(dir);

    static bool good = false;
    static std::once_flag flag;
    std::call_once(flag, [&]() {
        good = init_icu(dataPtr) || load_from(wDir) || load_from(executable_directory()) ||
               load_from(library_directory());
    });
    return good;
}

#else

// POSIX 版本的 mmap，仿照 Windows 版风格：传入 UTF-8 路径，返回映射地址。
// 如果需要返回大小，可传入 outSize 非 nullptr，函数会写入文件大小。
// prot/flags 可按需修改，默认 PROT_READ + MAP_PRIVATE（只读私有映射）。
//
// 返回值：映射地址，失败返回 nullptr。调用者负责在不再使用时调用 munmap(addr, size).
//
// 注意：在 POSIX 上，close(fd) 在 mmap 之后即可执行，映射依然有效直到 munmap。
//       path 必须是以 UTF-8 编码的 C 字符串。在 iOS/macOS/Android/Linux 本地层通常如此。
//       如果是 Android APK 内部 asset，需要先拷贝到可读写路径后再映射。
void* posix_mmap(const char* dataFile, size_t* outSize = nullptr, int prot = PROT_READ, int flags = MAP_PRIVATE) {
    if (!dataFile) {
        return nullptr;
    }
    // 打开文件，仅只读示例。如需可写映射，可改 O_RDWR，并修改 prot/flags。
    int fd = open(dataFile, O_RDONLY);
    if (fd < 0) {
        MILESTROLOG_CRITICAL("posix_mmap: open(\"%s\") failed: %s", dataFile, strerror(errno));
        return nullptr;
    }
    // 获取文件大小
    struct stat st{};
    if (fstat(fd, &st) < 0) {
        MILESTROLOG_CRITICAL("posix_mmap: fstat(\"%s\") failed: %s", dataFile, strerror(errno));
        close(fd);
        return nullptr;
    }
    if (st.st_size == 0) {
        MILESTROLOG_CRITICAL("posix_mmap: file \"%s\" is empty", dataFile);
        close(fd);
        return nullptr;
    }
    auto length = static_cast<size_t>(st.st_size);
    // 执行 mmap
    void* addr = mmap(nullptr, length, prot, flags, fd, 0);
    if (addr == MAP_FAILED) {
        MILESTROLOG_CRITICAL("posix_mmap: mmap(\"%s\") failed: %s", dataFile, strerror(errno));
        close(fd);
        return nullptr;
    }
    // 关闭 fd，映射依然有效
    close(fd);
    if (outSize) {
        *outSize = length;
    }
    return addr;
}

// 对应的解除映射函数，调用者在不再访问映射内存时使用：
void posix_munmap(void* addr, size_t size) {
    if (addr && size > 0) {
        if (munmap(addr, size) < 0) {
            MILESTROLOG_CRITICAL("posix_munmap: munmap failed: %s", strerror(errno));
        }
    }
}

static bool load_from(const std::string& path) {
    if (path.empty()) {
        return false;
    }

    auto status = milestro::io::isFileExists(path);
    auto sPath = path;
    if (status == milestro::io::FileStatus::Directory) {
        sPath = sPath + "/icudtl.dat";
    }
    if (void* addr = posix_mmap(sPath.c_str())) {
        if (init_icu(addr)) {
            return true;
        }
    }
    return false;
}


bool LoadIcuImpl(void* dataPtr, std::string& path) {
    static bool good = false;
    static std::once_flag flag;
    std::call_once(flag, [&]() {
        good = init_icu(dataPtr) || load_from(path) || load_from(std::filesystem::current_path());
    });
    return good;
}

#endif

std::unique_ptr<std::vector<uint8_t>> icudtl = nullptr;

bool LoadICU(void* dataPtr, std::string path) {
    return LoadIcuImpl(dataPtr, path);
};

bool CopyAndLoadICU(uint8_t* dataPtr, size_t size, std::string path) {
    static bool good = false;
    static std::once_flag flag;
    std::call_once(flag, [&]() {
        icudtl = std::make_unique<std::vector<uint8_t>>(dataPtr, dataPtr + size);
        good = LoadICU(icudtl->data(), path);
    });
    return good;
};

void EnsureLoadICU() {
    auto ret = LoadICU(nullptr, "");
    if (!ret) {
        throw std::runtime_error("failed to load icu");
    };
}
} // namespace milestro::unicode

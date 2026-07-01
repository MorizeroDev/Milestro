#if defined(__linux__) && !defined(_GNU_SOURCE)
#define _GNU_SOURCE
#endif

#include <Milestro/common/milestro_platform.h>

#if MILESTRO_PLATFORM_WINDOWS

#include <Milestro/util/milestro_encoding.h>

#else

#include <cerrno>
#include <fcntl.h>
#include <filesystem>
#include <sys/mman.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <unistd.h>
#if !MILESTRO_PLATFORM_IOS
#include <dlfcn.h>
#endif

#endif

#include <Milestro/io/milestro_io.h>
#include <Milestro/log/log.h>
#include <Milestro/unicode/milestro_icu.h>
#include <Milestro/util/milestro_env.h>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#include "unicode/udata.h"

#if MILESTRO_PLATFORM_WINDOWS

#include <io.h>
#include <windows.h>

#endif

namespace milestro::unicode {
namespace {

std::mutex IcuLoadMutex;
bool IcuLoaded = false;

} // namespace

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

#if MILESTRO_PLATFORM_WINDOWS

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

    auto status = milestro::io::isFileExists(milestro::util::encoding::WStringToString(path));
    auto sPath = path;
    if (status == milestro::io::FileStatus::Directory) {
        sPath = sPath + L"\\icudtl.dat";
    }
    auto sPathString = milestro::util::encoding::WStringToString(sPath);
    if (milestro::io::isFileExists(sPathString) != milestro::io::FileStatus::Exists) {
        MILESTROLOG_INFO("ICU data file does not exist: {}", sPathString);
        return false;
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

    std::lock_guard lock(IcuLoadMutex);
    if (!IcuLoaded) {
        IcuLoaded = init_icu(dataPtr) || load_from(wDir) || load_from(executable_directory()) ||
                    load_from(library_directory()) ||
                    load_from(milestro::util::encoding::StringToWString(
                            milestro::util::env::getenv("MILESTRO_UNICODE_ICUDAL_PATH")));
        if (IcuLoaded) {
            MILESTROLOG_INFO("ICU loaded successfully.");
        }
    }
    return IcuLoaded;
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
        MILESTROLOG_CRITICAL("posix_mmap: open(\"{}\") failed: {}", dataFile, strerror(errno));
        return nullptr;
    }
    // 获取文件大小
    struct stat st{};
    if (fstat(fd, &st) < 0) {
        MILESTROLOG_CRITICAL("posix_mmap: fstat(\"{}\") failed: {}", dataFile, strerror(errno));
        close(fd);
        return nullptr;
    }
    if (st.st_size == 0) {
        MILESTROLOG_CRITICAL("posix_mmap: file \"{}\" is empty", dataFile);
        close(fd);
        return nullptr;
    }
    auto length = static_cast<size_t>(st.st_size);
    // 执行 mmap
    void* addr = mmap(nullptr, length, prot, flags, fd, 0);
    if (addr == MAP_FAILED) {
        MILESTROLOG_CRITICAL("posix_mmap: mmap(\"{}\") failed: {}", dataFile, strerror(errno));
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
            MILESTROLOG_CRITICAL("posix_munmap: munmap failed: {}", strerror(errno));
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
    if (milestro::io::isFileExists(sPath) != milestro::io::FileStatus::Exists) {
        MILESTROLOG_INFO("ICU data file does not exist: {}", sPath);
        return false;
    }
    if (void* addr = posix_mmap(sPath.c_str())) {
        if (init_icu(addr)) {
            return true;
        }
    }
    return false;
}

#if !MILESTRO_PLATFORM_IOS
static std::string library_icudtl_path() {
    Dl_info info{};
    if (dladdr(reinterpret_cast<const void*>(&library_icudtl_path), &info) == 0 || info.dli_fname == nullptr) {
        return {};
    }

    std::error_code ec;
    auto path = std::filesystem::path(info.dli_fname);
    if (path.is_relative()) {
        path = std::filesystem::absolute(path, ec);
        if (ec) {
            return {};
        }
    }

    if (!path.has_parent_path()) {
        return {};
    }
    path.replace_filename("icudtl.dat");
    return path.string();
}
#endif

bool LoadIcuImpl(void* dataPtr, std::string& path) {
    std::lock_guard lock(IcuLoadMutex);
    if (!IcuLoaded) {
#if MILESTRO_PLATFORM_IOS
        IcuLoaded = init_icu(dataPtr) || load_from(path);
#else
        IcuLoaded = init_icu(dataPtr) || load_from(path) || load_from(library_icudtl_path()) ||
                    load_from(std::filesystem::current_path().string()) ||
                    load_from(milestro::util::env::getenv("MILESTRO_UNICODE_ICUDAL_PATH"));
#endif
        if (IcuLoaded) {
            MILESTROLOG_INFO("ICU loaded successfully.");
        }
    }
    return IcuLoaded;
}

#endif

std::unique_ptr<std::vector<uint8_t>> icudtl = nullptr;

bool LoadICU(void* dataPtr, std::string path) {
    return LoadIcuImpl(dataPtr, path);
};

bool IsICULoaded() {
    std::lock_guard lock(IcuLoadMutex);
    return IcuLoaded;
}

bool CopyAndLoadICU(uint8_t* dataPtr, size_t size, std::string path) {
    static bool good = false;
    static std::mutex mutex;
    std::lock_guard lock(mutex);
    if (!good && !IsICULoaded()) {
        if (dataPtr == nullptr || size == 0) {
            return false;
        }
        icudtl = std::make_unique<std::vector<uint8_t>>(dataPtr, dataPtr + size);
        good = LoadICU(icudtl->data(), path);
    }
    return good || IsICULoaded();
};

void EnsureLoadICU() {
    auto ret = LoadICU(nullptr, "");
    if (!ret) {
        throw std::runtime_error("failed to load icu");
    };
}
} // namespace milestro::unicode

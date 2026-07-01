# Build on Windows

This document covers the Windows build flow only.

Milestro's native library is built with CMake. Skia is not vendored in this repository, so prepare a separate Skia checkout and build output first. The current Windows workflow uses `clang-cl`, the MSVC runtime, and Ninja.

The examples assume:

- Milestro repo: `C:/work/Milestro`
- Skia source: `C:/work/skia`
- Visual Studio LLVM: `C:/Program Files/Microsoft Visual Studio/2022/Community/VC/Tools/Llvm/x64`

Adjust the paths for your local machine.

## Build

Run commands from a Visual Studio x64 Developer PowerShell, or make sure `clang-cl`, `lld-link`, CMake, Ninja, and the Windows SDK tools are on `PATH`.

### Debug

Build Skia from the Skia source root:

```powershell
$gnArgs = @'
target_os="win"
target_cpu="x64"
is_official_build=false
is_debug=true

skia_enable_fontmgr_custom_empty=true
skia_use_freetype=true
skia_use_system_freetype2=false
skia_use_freetype_woff2=true
skia_use_libavif=true

skia_use_metal=false
skia_use_gl=false

skia_use_direct3d=true
skia_use_vulkan=true

skia_use_icu=false
skia_use_client_icu=true

extra_cflags=["/D_ITERATOR_DEBUG_LEVEL=2", "/MDd"]

clang_win="C:/Program Files/Microsoft Visual Studio/2022/Community/VC/Tools/Llvm/x64"
cc="clang-cl"
cxx="clang-cl"
'@

.\bin\gn.exe gen out\MSVC-CL-MDd "--args=$gnArgs"
ninja -C out\MSVC-CL-MDd
```

Build Milestro from the repo root:

```powershell
cmake -S . -B cmake-build-nocli-debug -G Ninja `
  -DCMAKE_BUILD_TYPE=Debug `
  -DCMAKE_C_COMPILER=clang-cl `
  -DCMAKE_CXX_COMPILER=clang-cl `
  -DMILESTRO_SKIA_LIB_PATH=C:/work/skia/out/MSVC-CL-MDd `
  -DMILESTRO_SKIA_INCLUDE_PATH=C:/work/skia `
  -DMILESTRO_ENABLE_CLI=OFF

cmake --build cmake-build-nocli-debug --target Milestro
```

### RelWithDebInfo

Build Skia from the Skia source root:

```powershell
$gnArgs = @'
target_os="win"
target_cpu="x64"
is_official_build=false
is_debug=false

skia_enable_fontmgr_custom_empty=true
skia_use_freetype=true
skia_use_system_freetype2=false
skia_use_freetype_woff2=true
skia_use_libavif=true

skia_use_metal=false
skia_use_gl=false

skia_use_direct3d=true
skia_use_vulkan=true

skia_use_icu=false
skia_use_client_icu=true

extra_cflags=["/MD"]

clang_win="C:/Program Files/Microsoft Visual Studio/2022/Community/VC/Tools/Llvm/x64"
cc="clang-cl"
cxx="clang-cl"
'@

.\bin\gn.exe gen out\MSVC-CL-MD "--args=$gnArgs"
ninja -C out\MSVC-CL-MD
```

Build Milestro from the repo root:

```powershell
cmake -S . -B cmake-build-nocli-relwithdebinfo -G Ninja `
  -DCMAKE_BUILD_TYPE=RelWithDebInfo `
  -DCMAKE_C_COMPILER=clang-cl `
  -DCMAKE_CXX_COMPILER=clang-cl `
  -DMILESTRO_SKIA_LIB_PATH=C:/work/skia/out/MSVC-CL-MD `
  -DMILESTRO_SKIA_INCLUDE_PATH=C:/work/skia `
  -DMILESTRO_ENABLE_CLI=OFF

cmake --build cmake-build-nocli-relwithdebinfo --target Milestro
```

## CMake Settings

Skia inputs:

| Option | Value |
| --- | --- |
| `MILESTRO_SKIA_INCLUDE_PATH` | Skia source root. |
| `MILESTRO_SKIA_LIB_PATH` | Skia output directory containing `.lib` files. |
| `MILESTRO_SKIA_CMAKE` | Optional Skia CMake directory. Use instead of `MILESTRO_SKIA_LIB_PATH` when consuming Skia CMake targets. |

Common project options:

| Option | Default | Notes |
| --- | --- | --- |
| `MILESTRO_BUILD_SHARED_LIBS` | `ON` | Builds `Milestro` as a shared library. |
| `MILESTRO_ENABLE_CLI` | `ON` | Builds `MilestroCli`. Turn off for Unity plugin-only builds. |
| `MILESTRO_WITH_ADDRESS_SANITIZER` | `OFF` | Adds ASan flags for Clang builds. |
| `MILESTRO_ENABLE_RELEASE_SYMBOLS` | `ON` | Keeps debug info in release-style builds for split symbol packages. |
| `MILESTRO_REMAP_SOURCE_PATHS` | `ON` | Remaps absolute source/build paths in debug info. |

The Skia build must match Milestro's runtime mode:

- Debug: Skia `/MDd`, Milestro `CMAKE_BUILD_TYPE=Debug`
- Release-like: Skia `/MD`, Milestro `CMAKE_BUILD_TYPE=RelWithDebInfo` or `Release`

Do not set CMake's global `BUILD_SHARED_LIBS`; the project intentionally forces third-party dependencies to static libraries.

## Codegen

When `include/Milestro/game/milestro_game_interface.h` changes, regenerate the Unity binding files:

```powershell
.\gradlew.bat h2cs
```

or through a configured CMake build:

```powershell
cmake --build cmake-build-nocli-debug --target H2CS
```

This updates:

- `apps/unity-plugins/Milestro/Binding/BindingC.cs`
- `apps/unity-plugins/Milestro/Plugins/iOS/FrameworkBinding.cpp`

`clang-format` is optional; the Gradle task skips formatting if it is not installed.

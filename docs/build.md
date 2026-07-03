# Build

Milestro's native library is built with CMake. Binding generation and C# source
formatting are handled by Gradle.

Skia is not vendored in this repository. Build Skia separately, then point
Milestro at the Skia source root and either the Skia library output directory or
the Skia CMake directory.

## Requirements

- CMake 3.26 or newer.
- A C++20 compiler. The external Skia library path flow requires Clang,
  AppleClang, or `clang-cl`.
- Ninja, Xcode, or another CMake generator suitable for the target platform.
- A separate Skia checkout and build output.
- Java/Gradle for `h2cs` binding generation.
- Optional: `clang-format` for generated binding formatting.
- Optional: `dotnet format` for C# formatting.

Apple builds also enable Swift, Objective-C, and Objective-C++. Swift 5.9 or
newer is required because the CMake project checks for Swift C++ interop support.

## Targets

- `Milestro`: native library used by Unity.
- `MilestroCli`: CLI executable, enabled by default with `MILESTRO_ENABLE_CLI=ON`.
- `H2CS`: regenerates C# and iOS bridge bindings from the exported game API.
- Native tests: `MilestroTest_ReadFont`, `MilestroTest_ReadImage`,
  `MilestroTest_Icu`, `MilestroTest_SkiaUnicodeFallback`,
  `MilestroTest_InputBoxApiSpike`, and `MilestroTest_InputBox`.

## Common CMake Configuration

Using prebuilt Skia static libraries:

```sh
cmake -S . -B cmake-build-relwithdebinfo -G Ninja \
  -DCMAKE_BUILD_TYPE=RelWithDebInfo \
  -DMILESTRO_SKIA_LIB_PATH=/path/to/skia/out/build \
  -DMILESTRO_SKIA_INCLUDE_PATH=/path/to/skia

cmake --build cmake-build-relwithdebinfo --target Milestro
```

Using Skia CMake targets instead:

```sh
cmake -S . -B cmake-build-relwithdebinfo -G Ninja \
  -DCMAKE_BUILD_TYPE=RelWithDebInfo \
  -DMILESTRO_SKIA_CMAKE=/path/to/skia/cmake-or-build-dir \
  -DMILESTRO_SKIA_INCLUDE_PATH=/path/to/skia
```

Do not set CMake's global `BUILD_SHARED_LIBS`; the project intentionally forces
third-party dependencies to static libraries.

## CMake Options

| Option | Default | Notes |
| --- | --- | --- |
| `MILESTRO_SKIA_INCLUDE_PATH` | unset | Skia source root. Required. |
| `MILESTRO_SKIA_LIB_PATH` | unset | Skia output directory containing `.a` or `.lib` files. |
| `MILESTRO_SKIA_CMAKE` | unset | Optional Skia CMake directory. Use instead of `MILESTRO_SKIA_LIB_PATH` when consuming Skia CMake targets. |
| `MILESTRO_BUILD_SHARED_LIBS` | `ON` | Builds `Milestro` as a shared library. |
| `MILESTRO_BUILD_FRAMEWORK_LIBS` | `OFF` | Builds `Milestro` as an Apple framework when building for iOS. |
| `MILESTRO_ENABLE_CLI` | `ON` | Builds `MilestroCli`. Turn off for Unity plugin-only builds. |
| `MILESTRO_ENABLE_TESTS` | `ON` | Builds GoogleTest-based native tests. |
| `MILESTRO_WITH_ADDRESS_SANITIZER` | `OFF` | Adds ASan flags for Clang builds. |
| `MILESTRO_ENABLE_RELEASE_SYMBOLS` | `ON` | Keeps debug info in release-style builds for split symbol packages. |
| `MILESTRO_REMAP_SOURCE_PATHS` | `ON` | Remaps absolute source/build paths in debug info. |
| `MILESTRO_ENABLE_DESKTOP_OPENGL_RENDER` | `OFF` | Enables the experimental Unity OpenGLCore RenderTexture backend on desktop Linux. |
| `MILESTRO_ENABLE_DESKTOP_VULKAN_RENDER` | `OFF` | Enables the experimental Unity Vulkan RenderTexture backend on desktop. |

## Tests

Build and run tests from a configured tree:

```sh
cmake --build cmake-build-relwithdebinfo
ctest --test-dir cmake-build-relwithdebinfo --output-on-failure
```

The non-Android tests copy `tests/data` into the runtime output directory.
ICU-related tests use `ext/icu-cmake/common/icudtl.dat`.

Android test execution is currently left as a manual workflow in the CMake test
files.

## Binding Generation

When `include/Milestro/game/milestro_game_interface.h` changes, regenerate the
Unity binding files:

```sh
./gradlew h2cs
```

or through a configured CMake build:

```sh
cmake --build cmake-build-relwithdebinfo --target H2CS
```

This updates:

- `apps/unity-plugins/Milestro/Binding/BindingC.cs`
- `apps/unity-plugins/Milestro/Plugins/iOS/FrameworkBinding.cpp`

`clang-format` is optional. The Gradle task skips generated binding formatting if
it is not installed.

Format C# files under `apps/unity-plugins`:

```sh
./gradlew format
```

This uses `dotnet format` when available.

## Unity Runtime Files

For Unity integration, place the built native binary under the Unity plugin
assets. In this repository those paths are under:

```text
apps/unity-plugins/Milestro/Plugins/
```

That directory is ignored for generated platform binaries, except for the iOS
`FrameworkBinding.cpp` source file and its `.meta` file.

The managed bindings expect:

- Non-iOS: `DllImport("libMilestro")`
- iOS player builds: `DllImport("__Internal")` with the `FrameworkBinding`
  entry-point prefix

The current Unity bootstrap also expects ICU data at:

```text
apps/unity-plugins/Resources/Milestro/icudtl.dat.bytes
```

Copy it from:

```text
ext/icu-cmake/common/icudtl.dat
```

The copied `.bytes` asset is ignored by git.

## Build on Windows

This section covers the current Windows build flow.

The examples assume:

- Milestro repo: `C:/work/Milestro`
- Skia source: `C:/work/skia`
- Visual Studio LLVM: `C:/Program Files/Microsoft Visual Studio/2022/Community/VC/Tools/Llvm/x64`

Adjust the paths for your local machine.

Run commands from a Visual Studio x64 Developer PowerShell, or make sure
`clang-cl`, `lld-link`, CMake, Ninja, and the Windows SDK tools are on `PATH`.

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

skia_enable_tools=false

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

skia_enable_tools=false

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

The Skia build must match Milestro's runtime mode:

- Debug: Skia `/MDd`, Milestro `CMAKE_BUILD_TYPE=Debug`
- Release-like: Skia `/MD`, Milestro `CMAKE_BUILD_TYPE=RelWithDebInfo` or `Release`

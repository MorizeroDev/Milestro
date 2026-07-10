# Build

Milestro's native library is built with CMake. Binding generation, Unity plugin
packaging, and C# source formatting are handled by Gradle.

The normal CMake flow also prepares the native third-party dependencies needed
by Milestro. No separate Skia build step is required for a standard local build.

## Requirements

- CMake 3.26 or newer.
- A C++20 compiler.
- Ninja, Python 3, and Git for third-party dependency setup, plus a CMake
  generator suitable for the target platform.
- Java/Gradle for `h2cs` binding generation and Unity plugin packaging.
- Optional: `clang-format` for generated binding formatting.
- Optional: `dotnet format` for C# formatting.

Apple builds also enable Swift, Objective-C, and Objective-C++. Swift 5.9 or
newer is required because the CMake project checks for Swift C++ interop support.

## Targets

- `Milestro`: native library used by Unity.
- `MilestroCli`: CLI executable, enabled by default with `MILESTRO_ENABLE_CLI=ON`.
- `H2CS`: regenerates C# and iOS bridge bindings from the exported game API.
- `milestro_unity_plugin`: runs the Gradle Unity plugin packaging task.
- Native tests: `MilestroTest_ReadFont`, `MilestroTest_ReadImage`,
  `MilestroTest_Icu`, `MilestroTest_SkiaUnicodeFallback`,
  `MilestroTest_InputBoxApiSpike`, and `MilestroTest_InputBox`.

## Common CMake Configuration

Configure and build from the repository root:

```sh
cmake -S . -B cmake-build-relwithdebinfo -G Ninja \
  -DCMAKE_BUILD_TYPE=RelWithDebInfo

cmake --build cmake-build-relwithdebinfo --target Milestro
```

On the first configure, CMake may download native dependency sources into
ignored paths under `ext/`. Generated dependency build output stays under the
chosen CMake build directory.

Do not set CMake's global `BUILD_SHARED_LIBS`; the project intentionally forces
third-party dependencies to static libraries.

## CMake Options

| Option | Default | Notes |
| --- | --- | --- |
| `MILESTRO_BUILD_SHARED_LIBS` | `ON` | Builds `Milestro` as a shared library. |
| `MILESTRO_BUILD_FRAMEWORK_LIBS` | `OFF` | Builds `Milestro` as an Apple framework when building for iOS. |
| `MILESTRO_ENABLE_CLI` | `ON` | Builds `MilestroCli`. Turn off for Unity plugin-only builds. |
| `MILESTRO_ENABLE_TESTS` | `ON` | Builds GoogleTest-based native tests. |
| `MILESTRO_WITH_ADDRESS_SANITIZER` | `OFF` | Adds ASan flags for Clang builds. |
| `MILESTRO_ENABLE_RELEASE_SYMBOLS` | `ON` | Keeps debug info in release-style builds for split symbol packages. |
| `MILESTRO_REMAP_SOURCE_PATHS` | `ON` | Remaps absolute source/build paths in debug info. |
| `MILESTRO_ENABLE_DESKTOP_OPENGL_RENDER` | `OFF` | Enables the experimental Unity OpenGLCore RenderTexture backend on desktop Linux. |
| `MILESTRO_ENABLE_DESKTOP_VULKAN_RENDER` | `OFF` | Enables the experimental Unity Vulkan RenderTexture backend on desktop. |
| `MILESTRO_UNITY_PLUGIN_OUTPUT_DIR` | unset | Optional output directory for the `milestro_unity_plugin` target. Defaults to `<build-dir>/unity-plugin`. |

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

The generated iOS bridge is written under the Unity plugin tree. That plugin
tree is used as generated/local build output; generated native plugin content is
not tracked by git in the current repository state.

`clang-format` is optional. The Gradle task skips generated binding formatting if
it is not installed.

Format C# files under `apps/unity-plugins`:

```sh
./gradlew format
```

This uses `dotnet format` when available.

## Unity Plugin Packaging

Package the Unity asset tree with Gradle:

```sh
./gradlew packageUnityPlugin
```

The default output is:

```text
build/unity-plugin/
```

Override it with:

```sh
./gradlew packageUnityPlugin -PmilestroUnityPluginOutputDir=/path/to/output
```

or through CMake:

```sh
cmake --build cmake-build-relwithdebinfo --target milestro_unity_plugin
```

Set `MILESTRO_UNITY_PLUGIN_OUTPUT_DIR` at configure time to choose the CMake
target's output directory.

The package task copies `Milestro`, `Milestro.Editor`, `Milestro.Experimental`,
and `Resources` from `apps/unity-plugins/`. It also generates:

```text
Resources/Milestro/icudtl.dat.bytes
```

from:

```text
ext/icu-cmake/common/icudtl.dat
```

The task does not compile native platform binaries. Any native binaries or
generated iOS bridge files that already exist under
`apps/unity-plugins/Milestro/Plugins/` are copied as part of the `Milestro`
asset tree.

## Unity Runtime Files

For Unity integration, place the built native binary under the Unity plugin
assets. In this repository those paths are under:

```text
apps/unity-plugins/Milestro/Plugins/
```

That directory is treated as generated/local plugin output. Generated platform
binaries and generated iOS bridge files under it are ignored by git.

The managed bindings expect:

- Non-iOS: `DllImport("libMilestro")`
- iOS player builds: `DllImport("__Internal")` with the `FrameworkBinding`
  entry-point prefix

If you use `packageUnityPlugin`, the ICU Unity `TextAsset` is generated into the
package output automatically. If you use `apps/unity-plugins` directly, create
the file at:

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

Run commands from a Visual Studio x64 Developer PowerShell, or make sure
`clang-cl`, `lld-link`, CMake, Ninja, and the Windows SDK tools are on `PATH`.

### Debug

Build Milestro from the repo root:

```powershell
cmake -S . -B cmake-build-nocli-debug -G Ninja `
  -DCMAKE_BUILD_TYPE=Debug `
  -DCMAKE_C_COMPILER=clang-cl `
  -DCMAKE_CXX_COMPILER=clang-cl `
  -DMILESTRO_ENABLE_CLI=OFF

cmake --build cmake-build-nocli-debug --target Milestro
```

### RelWithDebInfo

Build Milestro from the repo root:

```powershell
cmake -S . -B cmake-build-nocli-relwithdebinfo -G Ninja `
  -DCMAKE_BUILD_TYPE=RelWithDebInfo `
  -DCMAKE_C_COMPILER=clang-cl `
  -DCMAKE_CXX_COMPILER=clang-cl `
  -DMILESTRO_ENABLE_CLI=OFF

cmake --build cmake-build-nocli-relwithdebinfo --target Milestro
```

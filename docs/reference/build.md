# Build and package from source

Most Unity users should install the prebuilt package described in
[Install into a Unity project](../getting-started/install.md). This page is for
contributors who need to change the native library, bindings, or Unity package.

## Where the build definition lives

The build scripts are the source of truth. Do not copy a separate build matrix
into this page and expect it to stay current.

- Root build definitions: `CMakeLists.txt`, `cmake/`, `build.gradle.kts`.
- Release packaging: [`.github/workflows/workflow-formal.yml`](../../.github/workflows/workflow-formal.yml).
- Platform builds: [Windows](../../.github/workflows/build-windows.yml),
  [Linux](../../.github/workflows/build-linux.yml),
  [macOS](../../.github/workflows/build-macos.yml),
  [iOS](../../.github/workflows/build-ios.yml), and
  [Android](../../.github/workflows/build-android.yml).

The platform workflows show the compiler, SDK/NDK, CMake arguments, install
layout, and symbol packaging used for releases. Start there when a local build
differs from CI.

## Quick local build

Requirements are CMake 3.26 or newer, a C++20 compiler, Ninja, Python 3, Git,
and a Java/Gradle toolchain for binding generation and Unity packaging. Apple
targets also need Swift 5.9 or newer.

Initialize submodules before configuring:

```sh
git submodule update --init --recursive
```

The normal CMake flow downloads the native dependencies it needs; a separate
Skia build step is not required.

```sh
cmake -S . -B cmake-build-debug -G Ninja \
  -DCMAKE_BUILD_TYPE=Debug \
  -DMILESTRO_ENABLE_CLI=OFF

cmake --build cmake-build-debug --target Milestro
```

On Windows, run this from a Visual Studio x64 developer environment with
`clang-cl`, `lld-link`, CMake, Ninja, and the Windows SDK available. The
[Windows workflow](../../.github/workflows/build-windows.yml) is the reference
configuration.

Useful targets:

- `Milestro`: native library used by Unity.
- `MilestroCli`: command-line tool when `MILESTRO_ENABLE_CLI=ON`.
- `H2CS`: regenerate C# and iOS bridge bindings.
- `milestro_unity_plugin`: run Unity package assembly.
- Native tests when `MILESTRO_ENABLE_TESTS=ON`.

The available CMake options and defaults are declared in `CMakeLists.txt`.

## Tests

```sh
cmake --build cmake-build-debug
ctest --test-dir cmake-build-debug --output-on-failure
```

Android device validation is separate from the host test run. See
[`tests/android_unity/README.md`](../../tests/android_unity/README.md).

## Binding generation

Do not edit the generated binding files by hand. After changing
`include/Milestro/game/milestro_game_interface.h`, run:

```sh
./gradlew h2cs
```

or:

```sh
cmake --build <build-dir> --target H2CS
```

This updates:

- `apps/unity-plugins/Milestro/Binding/BindingC.cs`
- `apps/unity-plugins/Milestro/Plugins/iOS/FrameworkBinding.cpp`

Format Unity C# sources with `./gradlew format`.

## Package the Unity plugin

```sh
./gradlew packageUnityPlugin
```

The default output is `build/unity-plugin/`. Override it with:

```sh
./gradlew packageUnityPlugin -PmilestroUnityPluginOutputDir=<output-dir>
```

The package task rebuilds a dedicated output directory, copies the allowlisted
Unity asset roots, generates `Resources/Milestro/icudtl.dat.bytes`, and verifies
the file set and `.meta` GUIDs. It does not compile native binaries: place them
under the matching `apps/unity-plugins/Milestro/Plugins/` paths first.

The complete cross-platform archive is assembled by
[`.github/workflows/workflow-formal.yml`](../../.github/workflows/workflow-formal.yml):
it collects each platform artifact, writes the iOS bridge, packages
`milestro-unity-plugins.tar.gz`, and publishes it from tag builds.

## Platform matrix

Instead of repeating per-platform commands here, use the workflow that matches
your target:

| Target | Reference |
| --- | --- |
| Windows x86-64 | [`build-windows.yml`](../../.github/workflows/build-windows.yml) |
| Linux x86-64 | [`build-linux.yml`](../../.github/workflows/build-linux.yml) |
| macOS arm64 | [`build-macos.yml`](../../.github/workflows/build-macos.yml) |
| iOS arm64 | [`build-ios.yml`](../../.github/workflows/build-ios.yml) |
| Android arm64-v8a | [`build-android.yml`](../../.github/workflows/build-android.yml) |

Do not set CMake's global `BUILD_SHARED_LIBS`; the project controls shared and
static libraries explicitly.

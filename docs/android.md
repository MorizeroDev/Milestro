# Android implementation and acceptance

## Status

The integration branch contains GLES3 rendering, Vulkan Direct and StagingCopy,
Android XML/system-font fallback with FreeType, and mobile ICU data loading. The
release workflow produces arm64-v8a; armeabi-v7a is supported by native configuration
and ABI compilation checks but is not a release artifact or a verified device target.

The former `feat/android-vulkan` branch is not a drop-in replacement for current
managed code. Vulkan target payload ABI 2, its C# layout, and native binaries must
be deployed together. Do not combine an older `.so` with the new bindings.

**Status is implementation plus contract tests, not Android rendering acceptance.**
No Adreno/Mali device, Unity player, GPU validation-layer, or performance result is
implied by a host unit test or a successful native build. See the matrix below.

## Native builds

Initialize dependencies with `git submodule update --init --recursive`. Use a
fresh build directory when changing NDK, ABI, minimum API, or backend options.
The following commands are POSIX shell examples; on PowerShell use the equivalent
NDK environment variable and command-line continuation syntax.

Vulkan plus GLES3 (the arm64 release configuration):

```sh
cmake -S . -B build/android-vulkan -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake" \
  -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-24 \
  -DMILESTRO_ENABLE_ANDROID_VULKAN_RENDER=ON \
  -DMILESTRO_ENABLE_CLI=OFF -DMILESTRO_ENABLE_TESTS=OFF \
  -DMILESTRO_BUILD_SHARED_LIBS=ON -DCMAKE_BUILD_TYPE=Release
cmake --build build/android-vulkan --target Milestro
```

GLES-only, retaining an API 23 native target:

```sh
cmake -S . -B build/android-gles -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake" \
  -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-23 \
  -DMILESTRO_ENABLE_ANDROID_VULKAN_RENDER=OFF \
  -DMILESTRO_ENABLE_CLI=OFF -DMILESTRO_ENABLE_TESTS=OFF \
  -DMILESTRO_BUILD_SHARED_LIBS=ON -DCMAKE_BUILD_TYPE=Release
cmake --build build/android-gles --target Milestro
```

Configuration prints `Milestro Android backends: GLES3=ON, Vulkan=ON/OFF, API=...`.
GLES/EGL/log/android/jnigraphics are required (the last two carry Skia's
system dependencies when targeting higher APIs). Enabling Vulkan on an API below 24 or without its NDK
library is a fatal error, before third-party setup. Skia's Vulkan/EGL/VMA options
follow this selection; GLES-only builds do not accidentally retain Skia Vulkan.
The legacy NDK toolchain's `CMAKE_SYSTEM_VERSION=1` is not treated as the API level.

The API 24 release library is not an API 23 compatibility artifact just because
Unity is using GLES. Match the Unity player's minimum API to the native build.

## Unity selection and limitations

For an isolated run, disable automatic graphics API selection in Player Settings
and test one active Unity API at a time. A GLES-only native build must be paired
with Unity GLES3. Milestro does not switch Unity's API when initialization fails.

On Unity Vulkan, the default is Direct. To test StagingCopy for component surfaces,
set this before their first creation (for example before activating their objects):

```csharp
using Milestro.Configuration;
using Milestro.Skia;

MilestroConfiguration.Configuration.RenderSurface.VulkanBackend =
    UnitySkiaVulkanBackend.StagingCopy;
```

For explicitly created surfaces, set the same enum on
`UnitySkiaRenderTextureDescriptor.VulkanBackend`. Existing targets do not switch
in place. StagingCopy is CPU rasterization plus a full-surface upload; it must not
be reported as a GPU rasterization performance result. Direct's no-wait shutdown
and paired event scheduling still need real-device lifecycle validation. The
integration does not silently change the default to a purportedly safer backend.

GLES supports Auto/RGBA32, not explicit BGRA32. All current backends reject MSAA
render targets. The GLES guard restores blend factors/equations/color, separate
front/back stencil parameters, depth/culling state, pixel-store/PBO state, and
GLES3 samplers in addition to the prior viewport/FBO/program/buffer/texture state.
This is a state-boundary fix, not proof that every extension or Unity pipeline is
isolated; mixed Unity/Skia draws still need a visual regression run.

## Fonts, ICU, and input

- Register bundled fonts for predictable coverage. Android additionally uses
  Skia's XML/system-font manager and FreeType scanner, not the API 30+ NDK font
  manager. Failure to initialize system fonts leaves registered fonts available.
  OEM font coverage and emoji fallback still require device checks.
- Keep `Resources/Milestro/icudtl.dat.bytes` in the Unity package. Mobile bootstrap
  writes it to the configured persistent path before native loading. An inaccessible
  process working directory no longer interrupts later native path fallbacks.
- The general input/session-isolation implementation is not a guarantee of Android
  soft-keyboard behavior. Strict served-editor composition cancellation remains
  unsupported; see [limitations](limitations.md#android-strict-composition-control-is-unsupported).

## Repeatable checks without Unity or a GPU

CMake backend policy checks, plus real NDK configuration and compilation of the
native payload ABI and GLES guard on arm64/ARMv7, API 23/24 with Vulkan on/off,
and API 26/30 with Vulkan on, with both legacy and non-legacy NDK toolchains:

```sh
python3 tests/android_build/test_android_graphics_config.py --ndk "$ANDROID_NDK_HOME"
```

Omit `--ndk` to run synthetic library-availability/policy tests only. Expected
failures (API 23 with Vulkan enabled, or a missing required library) are asserted,
not counted as successful Vulkan configurations.

Run host payload, scale-transform, Vulkan ring-lifecycle, and fake-GL state tests
without downloading/building Skia or ICU:

```sh
git submodule update --init ext/googletest
cmake -S tests/platform_contracts -B build/platform-contracts -DCMAKE_BUILD_TYPE=Release
cmake --build build/platform-contracts --config Release
ctest --test-dir build/platform-contracts -C Release --output-on-failure
```

The fake-GL tests check normal exit, early return, exceptions, nested guards, and
state restoration. They do not create a GL context. The separate
`MilestroTest_UnityRenderVulkanProduction` suite requires the full project and
uses fake Unity/Vulkan host callbacks; even that suite is not a GPU acceptance test.

## Outstanding device acceptance matrix

Run every row on GLES3, Vulkan Direct, and Vulkan StagingCopy, on at least one
Adreno and one Mali device. Record device/OS/driver, Unity version and render
pipeline, ABI, native/managed revision, color space, logs, and screenshots.

| Area | Required scenarios | Status |
| --- | --- | --- |
| Basic drawing | Paragraph, SlimText, image, TextInput; clear and incremental draws | Pending device run |
| Fonts | Latin/CJK, mixed fallback, missing glyphs, emoji, registered vs system fonts | Pending device run |
| Pixels | Gamma/Linear, transparent edges, clipping, orientation, HiDPI | Pending device run |
| Interop | Unity draws before/after Milestro with blend, stencil masks, different samplers | Pending device run |
| Lifecycle | Resize churn, scene unload, repeated create/destroy, pause/resume, rotation | Pending device run |
| Vulkan | Validation layers, delayed/missing render events, memory after retirement | Pending device run |
| Input | Soft keyboard show/hide, composition, owner switching, selection/clipboard | Pending device run |
| Performance | Many targets, large targets, CPU/GPU frame time, upload cost, memory plateau | Pending device run |

Do not mark the platform release-ready until the selected default backend passes
this matrix. Do not change Direct/StagingCopy defaults based only on mocked tests.

## Local verification snapshot (2026-09-06)

Repair work is on `fix/android-platform-readiness`, based on
`origin/luvia/task230-vulkan-dual-backend-review-repair` (`6df358d`), not the older
`feat/android-vulkan` branch. Local checks use Windows, NDK
`30.0.14904198` (r30 beta1), and MSVC 2022 for host tests. The release workflow
still uses NDK r29; a local r30 result is not a recorded r29 CI result.

- Host contracts: **21/21 passed**, including three new fake-GL restoration tests.
- Android policy tests: **4/4 passed**. The real-NDK test covers **24 combinations**
  across two ABIs and two toolchains: API 23/24 with Vulkan on/off, and API 26/30
  with Vulkan on. Four API 23 + Vulkan combinations are expected rejections;
  the other twenty compile the payload ABI and GLES guard with warnings as errors.
- Full-project CMake/GN generation passed for arm64 API 24 with Vulkan enabled
  and arm64 API 23 GLES-only. The latter emits both `skia_use_vulkan=false` and
  `skia_use_vma=false`.
- Full arm64 API 24 Vulkan + GLES plugin compilation and linking **passed**:
  `build/android-vulkan/lib/libMilestro.so` (unstripped, with debug symbols).
  ELF inspection confirms AArch64, the expected GLES/EGL/Vulkan system dependencies,
  and exported `UnityPluginLoad`, `UnityPluginUnload`, and
  `MilestroUnityRenderGetRenderEventAndDataFunc`. This is not a device-load test.
  The API 23 GLES-only tree was configured but not fully built in this snapshot.
- NDK syntax checks passed for FontRegistry, ICU loading, the GLES backend,
  Vulkan backend, Direct/StagingCopy adapters, and dispatcher on both arm64 and
  ARMv7 at API 24. Syntax checks alone do not verify linking or GPU behavior.
- No ADB device was connected. Unity player execution, the full Vulkan production
  fake-host suite, and real GPU/IME acceptance were not run in this snapshot.

## Unity 6000.3.7f1 bundled toolchain

On Windows, use the version directory (not `Unity.exe`) with the helper:

```powershell
./scripts/build-android-unity.ps1 `
  -UnityRoot 'D:\UnityHub\Editor\6000.3.7f1' -RunContractTests -PackageUnity
```

This uses `Editor/Data/PlaybackEngines/AndroidPlayer/{SDK,NDK,OpenJDK}` and
restores the caller's tool environment afterwards. Defaults match the reference
`MilestroDemo`: arm64-v8a and native API 26, with both Vulkan and GLES enabled.
`-GlesOnly -ApiLevel 23` selects the separate native GLES-only configuration;
`-ConfigureOnly` skips compilation and cannot be combined with `-PackageUnity`.
CMake, Ninja and Python (for optional contract tests) must also be installed, and
native submodules/Skia dependencies initialized as described above.

Build directories include NDK revision, ABI, API and backend to avoid accidentally
reusing a different toolchain's CMake cache. The script enables
`ANDROID_SUPPORT_FLEXIBLE_PAGE_SIZES=ON`; do not assume NDK r27 has the same
linker defaults as newer NDKs. `-PackageUnity` uses the bundled JDK with Gradle,
packages the matching C#/ICU roots, and adds `libMilestro.so` under the selected
Android ABI. It disables the persistent Gradle daemon for this invocation to
avoid inherited Ninja output handles keeping Windows batch packaging alive.

The r27 libc++ build exposed missing `std::atomic_ref` support in the dispatcher.
`MilestroUnityRenderAtomic.h` now retains the plain, aligned `int32_t` managed
ABI and uses compiler atomics when the standard-library feature is unavailable.
The release stores and strong acquire/release compare-exchanges retain their
original ordering; no `std::atomic` objects are overlaid on C#-owned storage.
The Android contract matrix compiles this helper too; host tests cover stores,
CAS success/failure and contended updates.

A cross-build-only package need not contain a Windows editor native binary.
Batch editor import now warns and defers ICU bootstrap on `DllNotFoundException`,
without swallowing player runtime failures. Failed bootstrap attempts reset the
initialization flag rather than incorrectly marking ICU initialized.

For reproducible APK validation, use the isolated Demo procedure in
`tests/android_unity/README.md`. The older Demo bindings/producer API must be
updated together with the native library, not by replacing `.so` alone.

### Bundled-toolchain verification snapshot (2026-09-06)

The follow-up run used the user's `D:\UnityHub\Editor\6000.3.7f1` installation:
NDK **27.2.12479018 (r27c)**, bundled **Temurin JDK 17.0.9**, SDK platform 35 and
build-tools 36.0.0 for the Unity APKs. `E:\Code\MilestroDemo` at `1822274` was
used as a read-only reference. Validation ran in an ignored clone at
`build/unity6000-demo-validation`; the original Demo remained Git-clean.

- Host platform suite: **24/24 passed**, including the three new atomic tests.
- r27c policy/ABI/GLES/atomic compilation suite: **4/4 passed**, with all 24
  ABI/toolchain/API/backend combinations exercised (four expected rejections).
  This is cross-compilation, not execution of ARM tests on a host machine.
- Full ARM64 API 26 Vulkan + GLES native compilation and linking **passed**.
  The first r27c build failed on `std::atomic_ref`; the compatibility fix was
  then rebuilt and verified with this same bundled NDK, not a substitute NDK.
- Matching Unity package generation with bundled JDK **passed**, verifying
  426 allowlisted managed/resource/meta files and 233 unique GUIDs before
  adding the native library. Output:
  `build/android-unity-27.2.12479018-arm64-v8a-api26-vulkan/unity-plugin`.
- All **282** Android entrypoints declared by current `BindingC.cs` exist in
  the native dynamic export table, as do Unity plugin load/unload hooks.
  Native ELF build-id: `ed1c3ed15182acad21215ba3ffbd0b29f3ac421a`.
- Bundled r27c API 23 GLES-only CMake/GN **configuration passed**, with Vulkan
  and VMA disabled. This separate native tree was **not fully compiled**.
- Complete native/package script execution also verified environment restoration
  under Windows PowerShell with redirected logs. CMake warnings do not falsely
  terminate successful native commands; nonzero exit codes remain errors.
- The workflow now defines a separate r27c contract gate while retaining r29
  release artifacts. **No remote CI run is claimed** by these local results.

All three ARM64 / IL2CPP players successfully built with Unity 6000.3.7f1:

| APK under `build/unity6000-demo-validation/Builds/` | File size (bytes) | Build result |
| --- | ---: | --- |
| `MilestroDemo-VulkanDirect.apk` | 55,793,990 | Passed |
| `MilestroDemo-VulkanStagingCopy.apk` | 55,793,994 | Passed |
| `MilestroDemo-GLES3.apk` | 55,014,924 | Passed |

APK inspection confirmed min SDK 26, target SDK 35, ARM64 only, the selected
Resources mode, and a matching Milestro native build-id in every variant.
The packaged Milestro library is stripped (13,311,328 bytes) and retains
`0x4000` alignment on all `PT_LOAD` segments. All three APKs passed the bundled
SDK's `zipalign -c -P 16 4` check; this does **not** prove execution on a 16 KB
page-size device. The GLES3 player uses the dual-backend API 26 library.

Logs are in `build/android-unity-toolchain-verified.log` and
`build/unity6000-{VulkanDirect,VulkanStagingCopy,GLES3}.log`; package ELF identity,
size and SHA-256 records are in `build/android-unity-apk-verification.jsonl`.
The initial pre-bootstrap-fix import/build log was retained separately as
`build/unity6000-VulkanDirect-first.log`.

**Still pending:** no ADB device was connected, so none of these APKs was
installed or run. Pixel correctness, real GPU synchronization/lifetimes,
Adreno/Mali behavior, font/ICU/IME behavior, and performance remain unaccepted.
The full Vulkan production fake-host suite was also not run. Do not interpret
successful APK packaging as completion of the device matrix above.

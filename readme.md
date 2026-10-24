# Milestro

Milestro is a native Skia text rendering stack for Unity.

The project bypasses Unity's built-in text rendering path and lets Unity call a
native C++ layer for paragraph layout, glyph processing, rasterization, Unicode
utilities, and render-target output.

Milestro is still a proof-of-concept. The APIs and Unity component surfaces are
usable, but the repository is organized for active development rather than as a
finished UPM package.

## What It Provides

- A native C++ library built around Skia, SkParagraph, SkSVG, and ICU.
- A C-compatible game/plugin API in `include/Milestro/game/milestro_game_interface.h`.
- Generated Unity C# bindings in `apps/unity-plugins/Milestro/Binding/BindingC.cs`.
- An iOS framework/static-link bridge in `apps/unity-plugins/Milestro/Plugins/iOS/FrameworkBinding.cpp`.
- Managed Unity wrappers for Skia canvas, images, paths, SVG, fonts, text layout, render textures, and Unicode helpers.
- Unity components for UI text, world-space text, and editable text input.
- Hybrid legacy/Input System routing for keyboard, text, composition, and IME.
- Rich text parsing helpers for a small XML-like tag set.
- Native Unity render-event integration for selected graphics backends.

## Unity Installation

Download the prebuilt `milestro-unity-plugins.tar.gz` from a GitHub Release, or
download the `milestro-unity-plugins` artifact from the **Formal Build and
Release** workflow and extract its `.tar.gz` contents into the consuming Unity
project's `Assets/` directory. Include `Resources/` and the `.meta` files.

See [Install into a Unity project](docs/getting-started/install.md) for the
complete download and extraction steps. When working from this repository
instead, `apps/unity-plugins/` maps to the same `Assets/` layout; native plugin
binaries must be built separately.

Before importing Milestro, add the external ParaParty packages to the Unity
project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "party.para.util.colors": "https://github.com/ParaParty/ParaPartyUtil.git?path=Colors",
    "party.para.util.unitynative": "https://github.com/ParaParty/ParaPartyUtil.git?path=UnityNative"
  }
}
```

These are required by `Milestro.asmdef`: `party.para.util.colors` is used by the
rich text color parser, and `party.para.util.unitynative` is used by managed
native-object wrappers and native callback helpers.

`com.unity.inputsystem` is optional for static text. Editable `TextInput`
requires a version in `[1.16.0,2.0.0-0)` and an `InputSystemUIInputModule`. See
[Add your first input field](docs/getting-started/first-input.md).

If a consuming Unity project defines `MILEASE_HAS_NEWTONSOFT_JSON` and provides
`Newtonsoft.Json`, Milestro model DTOs include optional `JsonProperty` metadata.
Defining `MILEASE_HAS_NEWTONSOFT` additionally annotates font-metadata types.
Newtonsoft remains optional and is not required by default.

See [docs/README.md](docs/README.md) for the Unity user manual and
[docs/reference/hybrid-input.md](docs/reference/hybrid-input.md) for provider
selection, capabilities, and migration from the removed legacy input
components.

## Main Unity APIs

Stable component entry points:

- `Milestro.Components.TextBox`: UI `Graphic` that renders text into a
  render texture through Skia.
- `Milestro.Components.WorldSpaceTextBox`: world-space quad presenter backed by
  the same text render-target pipeline.
- `Milestro.Components.TextInput`: editable UI text input backed by the native
  `InputBox` text model.

Lower-level managed wrappers:

- `Milestro.Skia`: canvas, image, font registry, typeface, path, SVG, vertex data,
  Unity render texture surfaces, and render command lists.
- `Milestro.Skia.TextLayout`: paragraph style, text style, strut style,
  paragraph builder, paragraph, font collection, and input box wrappers.
- `Milestro.Unicode`: ICU loading, normalization, segmentation, transliteration,
  case mapping, and collation helpers.
- `Milestro.RichTextParser`: markup parsing into paragraph payloads.

`apps/unity-plugins/Milestro.Experimental/` contains older experimental bitmap,
mesh, and SDF text components. Keep it optional unless you are working on those
paths directly.

## Native Architecture

Milestro is split into a native layer and a Unity layer.

Native layer:

- `src/skia/` and `include/Milestro/skia/`: C++ wrappers for Skia canvas,
  images, fonts, typefaces, paths, SVG, vertex data, and text layout.
- `src/skia/unicode/`: Skia Unicode bridge code.
- `src/unicode/` and `include/Milestro/unicode/`: ICU-backed Unicode helpers.
- `src/io/` and `include/Milestro/io/`: native IO helpers.
- `src/game/` and `include/Milestro/game/`: exported plugin API used by Unity.
- `src/unity_render/`: Unity native render-event integration.

Unity layer:

- `apps/unity-plugins/Milestro/Binding/`: generated C# P/Invoke bindings.
- `apps/unity-plugins/Milestro/Skia/`: managed Skia and render-target wrappers.
- `apps/unity-plugins/Milestro/Skia/TextLayout/`: managed paragraph and input-box APIs.
- `apps/unity-plugins/Milestro/Unicode/`: managed ICU and Unicode helpers.
- `apps/unity-plugins/Milestro/Components/`: Unity components.
- `apps/unity-plugins/Milestro/RichTextParser/`: XML-like rich text parsing.
- `apps/unity-plugins/Milestro.Editor/`: editor menu utilities.

## Build And Tooling

Milestro uses CMake for native code and Gradle for C header to C# binding
generation and allowlisted Unity release packaging.

The normal CMake build prepares the native dependencies it needs. A standard
local build does not require a separate Skia build step:

```sh
cmake -S . -B cmake-build-relwithdebinfo -G Ninja \
  -DCMAKE_BUILD_TYPE=RelWithDebInfo

cmake --build cmake-build-relwithdebinfo --target Milestro
```

Useful targets:

- `Milestro`: native shared or static library.
- `MilestroCli`: optional CLI target, enabled by default.
- `H2CS`: regenerates Unity binding files from the public C++ API.
- Native tests under `tests/`, enabled by default with `MILESTRO_ENABLE_TESTS=ON`.

Regenerate bindings after changing `include/Milestro/game/milestro_game_interface.h`:

```sh
./gradlew h2cs
```

Format C# Unity sources:

```sh
./gradlew format
```

See [docs/reference/build.md](docs/reference/build.md) for local build commands,
CI packaging, and the platform workflow reference.

## Render Backend Status

The render-target path is platform-dependent:

- Apple builds compile the Metal backend.
- Windows builds compile the Direct3D 12 backend.
- Android builds OpenGLES3 and, by default, Vulkan Direct/StagingCopy backends.
  Vulkan requires an API 24+ NDK target; missing required libraries fail configuration.
  API 23 GLES-only builds explicitly set `MILESTRO_ENABLE_ANDROID_VULKAN_RENDER=OFF`.
- Desktop OpenGL and desktop Vulkan are experimental CMake options:
  `MILESTRO_ENABLE_DESKTOP_OPENGL_RENDER` and
  `MILESTRO_ENABLE_DESKTOP_VULKAN_RENDER`.

`UnityAutoRenderTextureSurface` currently auto-selects Metal, Direct3D 12,
Vulkan, OpenGLES3, or OpenGLCore from Unity's active graphics device. Vulkan defaults
to Direct; StagingCopy uses CPU rasterization followed by a Vulkan upload. MSAA
render targets are not supported yet. See [docs/reference/build.md](docs/reference/build.md)
for build commands and [tests/android_unity/README.md](tests/android_unity/README.md)
for device validation.

## Repository Layout

```text
apps/
  cmd/                  Native CLI entry point
  unity-plugins/        Unity C# plugin code and Unity resources
cmake/                  CMake helper modules
docs/                   Project documentation
ext/                    Vendored third-party dependencies except Skia
include/Milestro/       Public native headers
scripts/                Release and symbol-management scripts
src/                    Native implementation
tests/                  Native tests and test data
```

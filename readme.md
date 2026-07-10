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
- Rich text parsing helpers for a small XML-like tag set.
- Native Unity render-event integration for selected graphics backends.

## Unity Installation

The Unity assets live under `apps/unity-plugins/`. Treat this directory as the
contents of a Unity `Assets/` folder, or copy/link the directories you need into
your project.

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

If a consuming Unity project defines `MILEASE_HAS_NEWTONSOFT` and provides
`Newtonsoft.Json`, Milestro model DTOs include optional `JsonProperty` metadata.
Newtonsoft remains optional and is not required by default.

Runtime assets needed by the current code:

- Native plugin binary named for `DllImport("libMilestro")` on non-iOS targets.
- iOS builds use `DllImport("__Internal")` with the `FrameworkBinding` entry-point prefix.
- ICU data copied to `Assets/Resources/Milestro/icudtl.dat.bytes`. In this repo
  the source data is `ext/icu-cmake/common/icudtl.dat`, and the Unity target path
  maps to `apps/unity-plugins/Resources/Milestro/icudtl.dat.bytes`.

See [docs/unity.md](docs/unity.md) for the full Unity integration notes.

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
generation.

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

See [docs/build.md](docs/build.md) for CMake options, test commands, and the
current Windows build flow.

## Render Backend Status

The render-target path is platform-dependent:

- Apple builds compile the Metal backend.
- Windows builds compile the Direct3D 12 backend.
- Android enables OpenGLES3 and Vulkan backends when the platform libraries are found.
- Desktop OpenGL and desktop Vulkan are experimental CMake options:
  `MILESTRO_ENABLE_DESKTOP_OPENGL_RENDER` and
  `MILESTRO_ENABLE_DESKTOP_VULKAN_RENDER`.

`UnityAutoRenderTextureSurface` currently auto-selects Metal, Direct3D 12,
OpenGLES3, or OpenGLCore from Unity's active graphics device. MSAA render targets
are not supported yet.

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

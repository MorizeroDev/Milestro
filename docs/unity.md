# Unity Integration

The Unity side of Milestro lives under `apps/unity-plugins/`. The repository
does not currently define a complete UPM package. Use the Gradle
`packageUnityPlugin` task to produce a Unity asset-tree directory, or copy/link
the relevant directories into a Unity project's `Assets/` folder during local
development.

## Required Packages

Add these dependencies to the Unity project's `Packages/manifest.json` before
compiling Milestro:

```json
{
  "dependencies": {
    "com.morizero.milease": "https://github.com/MorizeroDev/Milease.git",
    "party.para.util.colors": "https://github.com/ParaParty/ParaPartyUtil.git?path=Colors",
    "party.para.util.unitynative": "https://github.com/ParaParty/ParaPartyUtil.git?path=UnityNative"
  }
}
```

Why they are required:

- `com.morizero.milease`: used by scroll tweening helpers for smooth text and
  input scrolling.
- `party.para.util.colors`: used by `Milestro.RichTextParser` for color parsing
  and serialization.
- `party.para.util.unitynative`: used by managed native-object wrappers,
  disposable native resources, and callback helpers.

Optional: if the consuming Unity project defines `MILEASE_HAS_NEWTONSOFT` and
provides `Newtonsoft.Json`, Milestro model DTOs include `JsonProperty` metadata
for compatibility with Newtonsoft-based serialization. Milestro does not require
that symbol or package by default.

## Asset Layout

When mapped into a Unity project, the important directories are:

```text
Assets/
  Milestro/                 Core runtime assembly
  Milestro/Plugins/         Native plugin binaries and generated iOS bridge files
  Milestro.Editor/          Editor-only utilities
  Milestro.Experimental/    Optional experimental components
  Resources/Milestro/       Runtime resources
```

`Milestro.Experimental` is optional. It contains older bitmap, mesh, and SDF text
experiments that are useful for development but are not the primary component
entry points.

## Native Plugin Binary

The generated C# binding uses:

- Non-iOS: `DllImport("libMilestro")`
- iOS player builds: `DllImport("__Internal")` with generated `FrameworkBinding`
  forwarding functions

For non-iOS platforms, place the built native library in Unity's plugin assets,
for example under:

```text
Assets/Milestro/Plugins/<platform>/
```

In this repository that maps to:

```text
apps/unity-plugins/Milestro/Plugins/<platform>/
```

Generated plugin binaries and generated iOS bridge files under
`apps/unity-plugins/Milestro/Plugins/` are ignored by git in the current
repository state. `./gradlew h2cs` writes the iOS bridge source to:

```text
apps/unity-plugins/Milestro/Plugins/iOS/FrameworkBinding.cpp
```

For iOS, generate `FrameworkBinding.cpp` before packaging, and keep it compiled
into the Unity Xcode project alongside the Milestro native library or framework.

## ICU Data

`MilestroBootstrap` initializes ICU automatically on editor and player startup
unless the C# scripting define symbol `MILESTRO_NO_ICU_INIT` is set in Unity.

The default bootstrap loads this Unity resource:

```text
Resources/Milestro/icudtl.dat.bytes
```

`./gradlew packageUnityPlugin` creates it from the vendored ICU data file:

```text
ext/icu-cmake/common/icudtl.dat
```

When using the repository layout directly, the target path is:

```text
apps/unity-plugins/Resources/Milestro/icudtl.dat.bytes
```

The `.bytes` extension is important: Unity imports it as a `TextAsset`, while
`Resources.Load<TextAsset>("Milestro/icudtl.dat")` resolves it without the final
`.bytes` suffix.

ICU loading is configurable through `MilestroConfiguration.Configuration.Icu`:

- `IcudtlResourcePath` defaults to `Milestro/icudtl.dat`.
- `IcudtlPersistentFileName` defaults to `icudtl.dat`.
- `IcudtlPathOverride` can point mobile builds at a specific extracted ICU data
  path.

Editor and standalone builds load ICU from memory. Mobile builds write the
resource bytes to the configured persistent path, then load ICU from that file.

## Components

Primary runtime components:

- `Milestro.Components.TextBox`: UI `Graphic` for rendered text. It owns or
  discovers a `TextBoxRenderTextureProducer` on the same GameObject, supports
  wheel scrolling in both axes, and passes unused scroll delta to parent scroll
  handlers.
- `Milestro.Components.WorldSpaceTextBox`: world-space quad backed by a Milestro
  render texture. It can create its mesh, renderer, and default material.
- `Milestro.Components.TextInput`: editable UI input with caret, selection,
  composition text, keyboard handling, pointer selection, single-line or
  multi-line modes, optional wrapping, masking, read-only mode, and scrollable
  overflow.

Internal component building blocks:

- `TextBoxRenderTextureProducer`: builds text render-target output from content,
  font family list, alignment, direction, wrap mode, size, weight, color, locale,
  margin, and scroll offset.
- `TextBoxRenderTarget`: builds Skia paragraph payloads and submits render
  commands to a Unity render texture surface.
- `RenderTextureGraphic` and `RenderTextureMeshPresenter`: present render texture
  output in UI and world-space contexts.

Current default text components use `Source Han Sans VF` as the initial font
family. Register that font, or change the component's font family list before
rendering.

## Configuration

Global runtime defaults are exposed through:

```csharp
Milestro.Configuration.MilestroConfiguration.Configuration
```

Current configuration groups:

- `InputBoxShortcut`: undo/redo and clipboard shortcut variants.
- `ScrollAxisLock`: scroll gesture deadzone, dominance ratio, and timeout values.
- `TextInput`: scroll-wheel step, key repeat timing, keyboard scroll interlock,
  and surrogate-pair timeout.
- `WorldSpaceTextBox`: default material resource path.
- `Icu`: ICU resource path and extracted-file path settings.

Set these values before Milestro components are created when you need project-wide
defaults.

## Native Wrappers

Low-level managed wrappers such as `Canvas`, `Paragraph`, `ParagraphBuilder`,
`TextStyle`, `InputBox`, `TypeFace`, `MilestroImage`, `Svg`, and Unicode helper
objects inherit `DisposableNativeObject`. Dispose them when you create them
directly, for example with `using` or an explicit `Dispose()`. Component-owned
objects are released by their Unity component lifecycle.

## Fonts

Register fonts through:

```csharp
Milestro.Skia.FontRegistry.RegisterFontFromFile(path);
```

Editor helpers are available under:

```text
Milestro/Font Registry/List Registered Font Families
Milestro/Font Registry/List Registered Font Faces
```

These menu items log the current native font registry state as JSON.

## Rich Text

`Milestro.RichTextParser.RichTextParser` parses a small XML-like subset and
converts it to `ParagraphPayload`.

Supported tags in the current parser:

- `<b>`: font weight 700
- `<i>`: italic
- `<u>`: underline
- `<s>`: strikethrough
- `<font color="..." size="..." weight="...">`: inline style values
- `<p align="...">`: paragraph alignment
- `<br>`: line break

Unknown tags throw during conversion. In the Unity editor, parser errors are
returned as visible error text; in player builds the fallback text is
`Render Error`.

## Render Texture Backends

`UnityAutoRenderTextureSurface` selects a backend from Unity's active graphics
device:

- Metal
- Direct3D12
- OpenGLES3
- OpenGLCore

`UnitySkiaRenderTextureSurface` also has a Vulkan backend enum path, but native
Vulkan support depends on the platform build:

- Android enables Vulkan when the NDK Vulkan library is found.
- Desktop Vulkan is behind `MILESTRO_ENABLE_DESKTOP_VULKAN_RENDER`.

Desktop OpenGL support is behind `MILESTRO_ENABLE_DESKTOP_OPENGL_RENDER`.

MSAA render textures are rejected by the current descriptor normalization code.

## Troubleshooting

If Unity reports missing assemblies for `Paraparty.Colors` or
`Paraparty.UnityNative`, verify the two ParaParty Git dependencies in
`Packages/manifest.json`.

If Unity throws `DllNotFoundException: libMilestro`, verify the native plugin was
built for the active Unity platform and is imported under `Assets/Milestro/Plugins`.

If startup logs `Failed to load icudtl.dat from Resources`, verify
`Resources/Milestro/icudtl.dat.bytes` exists in the Unity asset tree.

If text renders blank, first check that the requested font family has been
registered and that the render texture size is non-zero.

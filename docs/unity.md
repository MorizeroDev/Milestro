# Unity Integration

The Unity side of Milestro lives under `apps/unity-plugins/`. The repository does
not currently define a complete UPM package; use this folder as Unity asset
content, or copy/link the relevant directories into a Unity project's `Assets/`
folder.

## Required Packages

Add these dependencies to the Unity project's `Packages/manifest.json` before
compiling Milestro:

```json
{
  "dependencies": {
    "party.para.util.colors": "https://github.com/ParaParty/ParaPartyUtil.git?path=Colors",
    "party.para.util.unitynative": "https://github.com/ParaParty/ParaPartyUtil.git?path=UnityNative"
  }
}
```

Why they are required:

- `party.para.util.colors`: used by `Milestro.RichTextParser` for color parsing
  and serialization.
- `party.para.util.unitynative`: used by managed native-object wrappers,
  disposable native resources, and callback helpers.

Milestro also uses `Newtonsoft.Json` in several managed models and editor tools.
If your Unity project does not already provide that assembly, install Unity's
Newtonsoft Json package through Package Manager.

## Asset Layout

When mapped into a Unity project, the important directories are:

```text
Assets/
  Milestro/                 Core runtime assembly
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

Generated plugin binaries under `apps/unity-plugins/Milestro/Plugins/` are
ignored by git. The checked-in exception is the generated iOS bridge source:

```text
apps/unity-plugins/Milestro/Plugins/iOS/FrameworkBinding.cpp
```

For iOS, keep `FrameworkBinding.cpp` compiled into the Unity Xcode project
alongside the Milestro native library or framework.

## ICU Data

`MilestroBootstrap` initializes ICU automatically on editor and player startup
unless the C# scripting define symbol `MILESTRO_NO_ICU_INIT` is set in Unity.

The bootstrap loads this Unity resource:

```text
Resources/Milestro/icudtl.dat.bytes
```

Create it from the vendored ICU data file:

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

## Components

Primary runtime components:

- `Milestro.Components.TextBox`: UI `Graphic` for rendered text. It owns or
  discovers a `TextBoxRenderTextureProducer` on the same GameObject.
- `Milestro.Components.WorldSpaceTextBox`: world-space quad backed by a Milestro
  render texture. It can create its mesh, renderer, and default material.
- `Milestro.Components.TextInput`: editable UI input with caret, selection,
  composition text, keyboard handling, and pointer selection.

Internal component building blocks:

- `TextBoxRenderTextureProducer`: builds text render-target output from content,
  font family list, alignment, direction, size, weight, color, locale, and margin.
- `TextBoxRenderTarget`: builds Skia paragraph payloads and submits render
  commands to a Unity render texture surface.
- `RenderTextureGraphic` and `RenderTextureMeshPresenter`: present render texture
  output in UI and world-space contexts.

Current default text components use `Source Han Sans VF` as the initial font
family. Register that font, or change the component's font family list before
rendering.

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

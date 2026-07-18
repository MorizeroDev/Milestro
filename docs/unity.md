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

Optional: if the consuming Unity project defines `MILEASE_HAS_NEWTONSOFT` and
provides `Newtonsoft.Json`, Milestro model DTOs include `JsonProperty` metadata
for compatibility with Newtonsoft-based serialization. Milestro does not require
that symbol or package by default.

Optional: install `com.unity.inputsystem` version `1.18.0` or later, below `2.0`,
to use Milestro with an active `InputSystemUIInputModule`. The packaged
`Milestro.InputSystem` assembly is constrained to `[1.18.0,2.0.0)` and is not
compiled when that package is absent. The base `Milestro` assembly has no Input
System package reference and continues to support the legacy
`StandaloneInputModule` when Unity enables the Legacy Input Manager.

## Asset Layout

When mapped into a Unity project, the important directories are:

```text
Assets/
  Milestro/                 Core runtime assembly
  Milestro/Plugins/         Native plugin binaries and generated iOS bridge files
  Milestro.InputSystem/     Optional Input System runtime provider
  Milestro.Editor/          Editor-only utilities
  Milestro.Experimental/    Optional experimental components
  Resources/Milestro/       Runtime resources
```

The Gradle package recursively includes those five release roots and their
existing top-level metadata. `Milestro.Experimental` contains older bitmap,
mesh, and SDF text experiments and remains optional at the component level. The
package excludes `Milestro.Tests`, `Milestro.InputSystem.Tests`, formatting
projects, and unknown top-level source trees.

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
  handlers. Use the `ScrollPercent*` properties to link its normalized `0..1`
  scroll position to other scrollbars.
- `Milestro.Components.TextBoxScrollbar`: optional connector for Unity UI
  `Scrollbar` components. Assign exactly one TextBox or TextInput target plus
  horizontal and/or vertical scrollbars, and it hides each scrollbar when that
  axis has no overflow.
- `Milestro.Components.WorldSpaceTextBox`: world-space quad backed by a Milestro
  render texture. It can create its mesh, renderer, and default material.
- `Milestro.Components.TextInput`: editable UI input with caret, selection,
  composition text, keyboard handling, pointer selection, single-line or
  multi-line modes, optional wrapping, masking, read-only mode, and scrollable
  overflow. Use the `ScrollPercent*` properties to link its normalized `0..1`
  scroll position to other scrollbars. Keyboard, committed text, composition,
  and IME ownership are supplied by the process-wide HybridInput dispatcher.
  Runtime `Text` assignments canonicalize line breaks and invalid UTF-16 and
  invoke `onValueChanged` once when the canonical value changes;
  use `SetTextWithoutNotify` for an explicit silent assignment.
- `Milestro.Components.MilestroScrollRect`: Unity `ScrollRect` extension with
  Milestro wheel tweening and optional presentation-only Elastic scrolling. The
  stock `ScrollRect` component does not receive Milestro Elastic behavior.

Internal component building blocks:

- `TextBoxRenderTextureProducer`: builds text render-target output from content,
  font family list, alignment, direction, wrap mode, size, weight, color, locale,
  margin, and scroll offset.
- `TextBoxRenderTarget`: builds Skia paragraph payloads and submits render
  commands to a Unity render texture surface.
- `RenderTextureGraphic` and `RenderTextureMeshPresenter`: present render texture
  output in UI and world-space contexts.

Current default text components use `system-ui` as the initial font family.
Font family lists accept keyword mappings. Milestro predefines `serif`,
`sans-serif`, `monospace`, and `system-ui`, and applications can add or override
their own keywords such as `heading`, `body`, or `code`. Unquoted rich text
values first check keyword mappings; quoted rich-text values such as `"serif"`
are treated as literal named families. The TextLayout `FontCollection` expands
these declarations when a paragraph is built, then delegates concrete family
matching to SkParagraph's registered and system font managers.

## TextInput Lifecycle Events

`TextInput` exposes four getter-only Unity events:

- `onValueChanged` and `onEndEdit` are `UnityEvent<string>` values. In the
  Inspector, bind methods that take the event's Dynamic String payload.
- `onFocusGained` and `onFocusLost` are parameterless `UnityEvent` values.

Use `AddListener` and `RemoveListener` for runtime subscriptions. There is no
second public C# event channel. Persistent Inspector listeners are serialized
with their scene or prefab. Runtime listeners are not serialized, so their
owner must register them again after a domain reload and remove them according
to its own lifetime.

Callbacks observe committed state. `onValueChanged` runs after `Text` contains
the canonical payload. A normal focus release invokes `onEndEdit` with the
final canonical text and then invokes `onFocusLost`. If a listener throws,
Unity stops the remaining listeners in that Unity event and the HybridInput
dispatcher reports `ListenerException`, stops the current transaction, and
recovers for a later transaction or focus session. In particular, an
`onEndEdit` exception can prevent `onFocusLost`; it is not retried later.

Assigning a different canonical value through `Text` invokes
`onValueChanged` once, including while the component is disabled or inactive.
Assigning the same value does not invoke it. `SetTextWithoutNotify`, Inspector
validation, deserialization, and scene loading are silent.

## Elastic Scrolling

`TextBox`, `TextInput`, and `MilestroScrollRect` each own a serialized
`scrollElastic` setting group. Elastic is enabled by default, but only takes
effect when the selected HybridInput provider reports at least delta-only
scroll capability. The legacy provider reports `Unsupported`, so scrolling
remains clamped and the Elastic settings have no visual effect in that mode.

Overscroll is presentation-only. Logical offsets, normalized positions,
scrollbars, selection, composition, caret geometry, and hit testing remain
clamped. Return motion uses a monotonic exponential curve; the default
coefficient brings the maximum 96-pixel offset to the 0.1-pixel visual epsilon
in about 0.24 seconds, while smaller offsets settle earlier. An active parent
scroll handler disables child Elastic; nested edge handoff is not part of this
phase.

For `MilestroScrollRect`, use `SetLogicalNormalizedPosition` and the matching
logical get/set helpers for programmatic movement while Elastic may be active.
The component applies its visual offset only during the Canvas render window
and removes the exact tracked delta before normal lifecycle work. Unity's
`ScrollRect.normalizedPosition`, `horizontalNormalizedPosition`,
`verticalNormalizedPosition`, and `content` members are not virtual. Code that
holds the component as a base `ScrollRect` can therefore read or write during
that narrow render-applied window; an already-performed base write cannot be
made fully transparent and is settled on the next guard pass. Use the
Milestro-specific logical helpers and `MilestroScrollRect.content` when this
distinction matters.

## Hybrid Input

Milestro selects one input provider from the active Unity event-system module:

- `StandaloneInputModule` selects the built-in `legacy` provider when the
  Legacy Input Manager is enabled.
- `InputSystemUIInputModule` selects the optional `input-system` provider when
  `com.unity.inputsystem` is `[1.18.0,2.0.0)` and `Milestro.InputSystem` is
  present.

Selection requires exactly one active `EventSystem`. No match, multiple active
event systems, or equally ranked providers fail closed and are reported through
`Milestro.Input.HybridInputRuntime.Diagnostics`. Applications may request a
registered provider explicitly with:

```csharp
HybridInputRuntime.SetProviderOverride("input-system");
```

Pass `null` to restore automatic selection. An override is still rejected when
that provider does not match the current environment. The Input System provider
reports keyboard state, committed text, composition, IME control, and
delta-only scroll capability; gesture phase, momentum phase, device
classification, and scroll capture are not claimed yet. See
[hybrid-input.md](hybrid-input.md) for the provider contract, diagnostics,
package-optional behavior, and migration notes.

Strict `TextInput` focus additionally requires
`IHybridInputFocusSessionProvider`. The Input System adapter binds a fresh
session sink into each text, composition, and key-edge source callback closure.
The legacy polled source cannot
yet prove capture-time event ownership and therefore fails `TextInput` focus
admission with `SessionIsolationUnsupported`; it does not fall back to an
unscoped mode.

## Configuration

Global runtime defaults are exposed through:

```csharp
Milestro.Configuration.MilestroConfiguration.Configuration
```

Current configuration groups:

- `FontFamily`: override built-in font-family keywords such as `sans-serif` or
  `system-ui`, or define additional keyword mappings. Mapping values may refer
  to other keywords, and quoted values stay literal named families. Resetting a
  built-in keyword mapping restores its Milestro default.
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

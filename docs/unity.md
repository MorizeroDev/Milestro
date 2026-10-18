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

Optional: install `com.unity.inputsystem` in the compatibility range
`[1.16.0,2.0.0-0)` to use Milestro with an active
`InputSystemUIInputModule`. The packaged
`Milestro.InputSystem` assembly uses that same range and is not
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
- `Milestro.Components.WorldSpaceTextBatch`: optional common-parent component for
  world-space text. It combines labels that share a material and output texture
  into one mesh renderer, with atlas UVs stored in the combined mesh. Enable the
  render-texture atlas for the largest draw-call reduction. Distinct atlas pages
  or materials remain distinct renderers, while singleton outputs continue to
  use their original renderer. Disabling the batch component restores them.
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

`WorldSpaceTextBatch` tracks output texture, UV, mesh, and transform changes and
normally rebuilds only when one of those changes. Pixel-only text updates do not
rebuild the combined mesh. Per-label custom `MaterialPropertyBlock` values are
not merged; keep shared visual settings in the source material.

## TextInput Form Options

`placeholder` accepts a separate uGUI `Graphic` (including `TextBox` or `Text`).
Assign a child graphic, lay it out inside the input, and disable its raycast
target. The input enables it only while both the value and IME preedit are empty;
the placeholder never becomes part of `Text` or a value event.

`characterLimit` defaults to `0` (unlimited). It counts Unicode scalar values:
an emoji surrogate pair counts as one character; combining marks and multi-code-
point emoji count separately. Limits apply to typing, paste, IME commits,
`Text`, and `SetTextWithoutNotify`. Replacing a selection frees its budget before
the new text is checked. Rejected input leaves the selection intact.

`contentType` supplies `Standard`, `Autocorrected`, `IntegerNumber`,
`DecimalNumber`, `Alphanumeric`, `Name`, `EmailAddress`, `Password`, `Pin`, and
`Custom` presets. Numeric validation uses ASCII digits, an optional leading
minus, and `.` for decimals; PIN accepts only digits. Name validation accepts
Unicode letters and combining marks and capitalizes word starts. Email
validation filters characters and duplicate `@`/adjacent dots; applications
must still validate the complete address on submission. Password and PIN
presets enable masking and disable copying. Presets other than Standard,
Autocorrected, and Custom select single-line input.

`characterValidation` and `keyboardType` can be overridden independently;
changing either selects `Custom`. `onValidateInput` adds a runtime callback
after built-in validation. Its arguments are the proposed text with the
selection removed and accepted input inserted, the insertion index in UTF-16
code units, and one Unicode code point. Return the accepted/replacement code
point, or `0` to reject it. The callback should only validate its arguments;
avoid changing the input or its settings from inside it. Invalid scalar return
values are rejected. The callback is scalar-based, not grapheme-cluster-based:
`👨‍👩‍👧‍👦` produces seven callbacks, including three for U+200D ZWJ, so it is
not delivered or rejected atomically. Use `onValidateEdit` when the complete
sequence matters. `onValueChanged` is a post-commit notification and is not a
substitute for pre-edit validation.

`onValidateEdit` runs after built-in rules, `onValidateInput`, and the character
limit have produced a candidate, but before that candidate changes the value.
Its `TextInputEditContext` contains `CurrentText`, `ProposedText`, the filtered
`InsertedText`, UTF-16 selection offsets, and a `Source` of `Typing`, `Paste`,
`ImeCommit`, or `Programmatic`. Return `TextInputEditResult.Accept()` to accept
the candidate or `Reject()` to leave the value and selection unchanged.
`Replace(text)` replaces the entire candidate value; line canonicalization,
the character limit, built-in rules, and `onValidateInput` still apply to the
replacement, but `onValidateEdit` is not called recursively. Interactive
replacement retains undo history. Assigning `onValidateEdit` alone does not
revalidate the current value.

```csharp
input.onValidateEdit = edit =>
    edit.InsertedText == "👨‍👩‍👧‍👦"
        ? TextInputEditResult.Reject()
        : TextInputEditResult.Accept();
```

Changing limits, content rules, or `onValidateInput` revalidates the value and
clears undo history so old edits cannot restore disallowed text. IME preedit
remains unrestricted until committed. Character limits continue to count
Unicode scalars rather than complete ZWJ sequences.

```csharp
input.contentType = TextInputContentType.EmailAddress;
input.characterLimit = 254;
input.onSubmit.AddListener(address => SaveAddress(address));
```

`keyboardType` uses Unity's `TouchScreenKeyboardType`. The dispatcher sends
keyboard type, autocorrection, multiline, masking, and read-only settings to
providers implementing `IHybridInputKeyboardProvider`, before starting their
focus session and when settings change. On mobile, `TextInput` itself opens a
`TouchScreenKeyboard` session with these settings (see below). Setting a
keyboard type does not validate text.

## TextInput Touch Keyboard

On platforms where `TouchScreenKeyboard.isSupported` is true, focusing a
`TextInput` opens a `TouchScreenKeyboard` session with the current
`keyboardType`, autocorrection, multiline, masking, and `characterLimit`
settings. Read-only inputs do not open one. Following TMP's rule, Android
hardware keyboards (`TouchScreenKeyboard.isInPlaceEditingAllowed`) suppress
the soft keyboard. Blurring the input closes the session; Done submits and
blurs, and Cancel or LostFocus blurs without submitting.

`shouldHideMobileInput` defaults to `true`, matching TMP's default: the
native input field above the keyboard is hidden, so text is edited directly
in the Unity-rendered field through the normal committed/composition events,
and the input's text and selection stay mirrored into the keyboard for
autocorrect context. Set it to `false` to show the system-native input field;
in that mode the keyboard's text is polled back into the input through the
same canonicalization and validation pipeline as typing, and corrected text
and selection are written back to the keyboard. In hidden mode the system
keyboard itself still appears; only the extra native input box is hidden.

`touchScreenKeyboard` exposes the active `TouchScreenKeyboard`, or `null`
when no session is open. `onTouchScreenKeyboardStatusChanged` is a
`UnityEvent<TouchScreenKeyboard.Status>` that fires once per status
transition (`Visible`, `Done`, `Canceled`, `LostFocus`).

The soft keyboard session is ordinary platform text input. Strict native IME
ownership transfer and composition cancellation remain limited as described
in `docs/limitations.md`.

## TextInput Lifecycle Events

`TextInput` exposes six getter-only Unity events:

- `onValueChanged`, `onEndEdit`, and `onSubmit` are `UnityEvent<string>` values. In the
  Inspector, bind methods that take the event's Dynamic String payload.
- `onFocusGained` and `onFocusLost` are parameterless `UnityEvent` values.
- `onTouchScreenKeyboardStatusChanged` is a `UnityEvent<TouchScreenKeyboard.Status>`
  value for soft keyboard status transitions.

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

Single-line Enter/Keypad Enter invokes `onSubmit` after any value change in the
same input frame. Multiline Enter inserts a newline by default; set
`submitOnEnter` to submit on Enter and insert a newline with Shift+Enter.
Enter used to confirm IME composition does not submit. `Submit()` also submits
the currently focused, enabled, editable input when no composition is active.
Submission retains focus; an application's listener can move focus or close
the form, which then produces the normal `onEndEdit`/`onFocusLost` sequence.
Submit callbacks use the same bounded dispatcher and exception handling as
the other events.

## Elastic Scrolling

`TextBox`, `TextInput`, and `MilestroScrollRect` each own a serialized
`scrollElastic` setting group. Elastic is enabled by default, but only takes
effect when the selected HybridInput provider reports at least delta-only
scroll capability. The ordinary legacy provider reports `Unsupported`, so
scrolling remains clamped and the Elastic settings have no visual effect for a
`StandaloneInputModule`. In a Both-mode project whose Input System package is
outside Milestro's supported range, the core compatibility route reports
`DeltaOnly` only while it exactly matches `InputSystemUIInputModule`; this keeps
Elastic available without changing Legacy-only behavior.

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
  `com.unity.inputsystem` is `[1.16.0,2.0.0-0)` and `Milestro.InputSystem` is
  present.
- With Active Input Handling set to Both, an unsupported Input System package
  selects the core `legacy` provider for an exact `InputSystemUIInputModule`
  delta-only scroll fallback. This route does not claim strict TextInput focus.
- With Input System Package (New) only, missing, below-minimum, unparseable,
  or `2.0.0-0` and newer package states are Editor errors and fail
  the player build instead of silently producing `NoMatch`.

The compatibility range follows Unity's package-version comparison, so a valid
1.x prerelease above the `1.16.0` floor has the same assembly/provider owner as
the corresponding 1.x range. Prerelease contents are not separately
runtime-certified; the runtime matrix uses official stable package releases.

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
- `<a href="target" id="optional-id">...</a>`: a clickable TextBox range.
  `href` is required and non-empty. `id` may be omitted, but an explicitly
  empty `id`, unknown or duplicate attributes, nested anchors, and malformed
  XML are rejected. Link targets are returned to the application unchanged;
  Milestro never opens or interprets them.
- `<ruby>base<rt>annotation</rt></ruby>`: an HTML-shaped Ruby annotation.
  One non-empty, plain-text `<rt>` is supported per `<ruby>`. Existing inline
  tags may style the base. Optional plain-text `<rp>` children are accepted as
  HTML fallback content and are not rendered. Nested Ruby and multiple `<rt>`
  elements are rejected.

The `style` attribute on `<font>`, `<span>`, and `<p>` accepts
`font-kerning: auto | normal | none` and `font-feature-settings`. Feature
settings accept `normal` or a comma-separated list such as `'liga' 0`,
`'tnum' on`, or `'kern' 1`. Use `&quot;` when a double-quoted feature tag
appears inside a double-quoted XML attribute.

`<p>` also accepts `text-indent` with a `px` length or a percentage, for
example `<p style="text-indent: 24px">` or `<p style="text-indent: 10%">`.
Unitless zero and negative finite values are accepted. Indentation applies
only to the first formatted line and follows the paragraph's inline-start
direction. The CSS `hanging` and `each-line` modifiers are not supported.

Ruby layout styles use the element's standard HTML `style` attribute. The
supported standard CSS Ruby properties are:

- `ruby-position: over | under`
- `ruby-align: start | center | space-around | space-between`
- `ruby-overhang: auto | none`

Milestro-specific controls use CSS custom properties:

- `--milestro-ruby-size`: positive annotation font-size multiplier
- `--milestro-ruby-gap`: non-negative gap in paragraph layout units
- `--milestro-ruby-overflow: preserve-base | inline`

For example:

```xml
<ruby style="ruby-position: under; ruby-align: space-around;
             ruby-overhang: auto; --milestro-ruby-size: 0.5;
             --milestro-ruby-gap: 2">
  漢字<rt>かんじ</rt>
</ruby>
```

The parser implements a deliberate single-base/single-annotation subset of
HTML Ruby. Complex Ruby with multiple `<rt>` annotations, nested annotations,
`ruby-position: inter-character`, and styled markup inside `<rt>` is not
supported. When omitted, the standard properties use their CSS initial values:
`ruby-position: over`, `ruby-align: space-around`, and `ruby-overhang: auto`.

Read-only `TextBox` exposes the serialized, getter-only
`onLinkClicked` event. Its `LinkClickedEventArgs` payload contains `Href` and
`Id`; omitted `id` is reported as an empty string. A click is emitted only when
the primary mouse button or a touch press and release hit the same visible link
range without becoming a drag.

Add `Milestro.Components.TextBoxAccessibility` to the same GameObject to enable
keyboard, controller, and screen-reader interaction. The component is a uGUI
`Selectable`, so it uses the project's ordinary `EventSystem` and input module:

- Left/Up selects the previous link and Right/Down selects the next link. At
  either end, navigation returns to uGUI so focus can move to another
  `Selectable`; links do not wrap around.
- Submit invokes the active link through the same `onLinkClicked` event as a
  pointer click. Cancel clears the component's EventSystem selection.
- Keyboard or controller focus draws a configurable rectangle around each line
  of the active link. Pointer selection does not display this indicator.
  Fixed-viewport text scrolls an off-screen active link into view; flow-mode
  visibility remains controlled by the external flow viewport.

The inspector exposes `Focus Color`, `Focus Thickness`, `Link Hint`, and
`Screen Reader Enabled`. Screen-reader support requires Unity 6000.3 or newer
and the built-in Accessibility module (`com.unity.modules.accessibility`).
Enable that module in Package Manager or the project's `Packages/manifest.json`.
Without it, navigation still works and the component logs a warning instead of
publishing accessibility nodes.

With the module available, the component publishes a container with static-text
and link nodes to `AssistiveSupport.activeHierarchy`. Link nodes use the visible
link text as their label, `Link Hint` as additional guidance, a button role, and
screen-space frames derived from the laid-out link rectangles. Screen-reader
activation invokes `onLinkClicked`; moving screen-reader focus also shows the
focus indicator and scrolls the link into view. Disabling or destroying the
component removes its nodes. Milestro still returns link targets unchanged and
never opens or interprets them.

Task 201 persistent-listener and stripping checks are available from the Unity
menus under `Milestro/Task 201`. Generate/open the fixture to verify the saved
prefab and scene in Play Mode, or build the ordinary macOS IL2CPP player with
Medium managed stripping. After the
`TASK201_HYPERLINK_INTEGRATION_READY` log appears, physically click the visible
link once with the primary mouse button and require a
`TASK201_HYPERLINK_INTEGRATION_RESULT` log whose status is `PASS`.
Before launching Unity, set `MILESTRO_TASK201_HEAD` and
`MILESTRO_TASK201_TREE` to the exact 40-character candidate commit and tree
IDs; both values are embedded in the result log.

Unknown tags throw during conversion. In the Unity editor, parser errors are
returned as visible error text; in player builds the fallback text is
`Render Error`.

## Draw Call Batching

TextBox, TextInput, and ordinary SlimText render targets share render-texture
atlas pages by default. Their public output remains a texture plus a UV rectangle;
custom presenters must use both rather than sampling the entire texture.
Explicit `UnityAutoRenderTextureSurface` / `UnitySkiaRenderTextureSurface` instances
and SlimText's zero-allocation submission path remain standalone.

Configure the atlas before creating text:

```csharp
var surfaces = MilestroConfiguration.Configuration.RenderSurface;
surfaces.EnableAtlas = true;
surfaces.AtlasPageSize = 2048;
```

Each RGBA page costs `AtlasPageSize * AtlasPageSize * 4` bytes in the render-surface
budget (16 MiB at the default size, 4 MiB at 1024). Smaller pages reduce the
minimum allocation for sparse interfaces; larger pages fit more labels into a
shared texture. Padding and fragmentation consume additional page space, and
live regions are never repacked. Empty pages are released after pending draws.
Oversized regions, invalid page sizes, or rejected page allocations fall back to
standalone textures under the existing raster policy. Set `EnableAtlas = false`
to disable sharing. Color spaces, device epochs, and configuration changes never
share a page.

There are two separate reductions:

- Compatible UGUI graphics can batch because they sample the same texture.
  Canvas boundaries, materials, masks, clipping, and transparent ordering can
  still split batches. Sharing a texture does not guarantee one draw call for
  an arbitrary scene, and world-space mesh presenters are not GPU-instanced.
- Dirty regions are collected and sent as one native submission per dirty page
  per flush, with one submission in flight per page. Unchanged regions retain
  their pixels; a static page produces no new native submissions. This reduces
  render-target switches and submission overhead, not necessarily every Skia
  glyph draw. Rejected updates retain the producer's dirty state for retry.

Region updates clear one pixel of transparent padding, clip to the content, and
apply that target's raster scale. Draw snapshots and retired regions remain alive
until their pending submission completes. The atlas uses the existing surface
budget and device-epoch recovery rather than a separate texture budget.

## Render Texture Backends

`UnityAutoRenderTextureSurface` selects Metal, Direct3D12, Direct3D11, Vulkan,
OpenGLES3, or OpenGLCore from Unity's active graphics device. It does not change Unity's graphics
API or fall back from Vulkan to GLES when a native backend is unavailable.

Direct3D11 rasterizes on the CPU into a persistent per-target Skia surface and uploads
the pixels to Unity's `RenderTexture` with `UpdateSubresource`. The cached surface
preserves incremental atlas updates, and is released with its Unity target or on a
device reset. This backend includes full-surface CPU uploads; it is not GPU text
rasterization.

Vulkan has two per-target implementations:

- `UnitySkiaVulkanBackend.Direct` (default): Skia Vulkan draws directly into the
  Unity target using paired Prepare/Submit events. Queue and shutdown lifetime
  risks still require device validation; the implementation abandons its Skia
  context without a CPU wait during shutdown.
- `UnitySkiaVulkanBackend.StagingCopy`: Skia rasterizes on the CPU and records a
  buffer-to-image upload in Unity's command buffer. A bounded three-slot ring
  uses Unity safe-frame progress to retire storage. This is not GPU text
  rasterization and includes full-surface CPU copies/uploads.

Set `MilestroConfiguration.Configuration.RenderSurface.VulkanBackend` before
creating component render surfaces, or set `VulkanBackend` on an explicit
`UnitySkiaRenderTextureDescriptor`. Selection is fixed for each target's lifetime;
changing the configuration is not a live switch for existing surfaces.

Android Vulkan requires `MILESTRO_ENABLE_ANDROID_VULKAN_RENDER=ON` (the default),
an API 24+ target, and the NDK Vulkan library. An API 23 GLES-only build explicitly
disables that option. Desktop Vulkan and desktop OpenGL remain behind
`MILESTRO_ENABLE_DESKTOP_VULKAN_RENDER` and `MILESTRO_ENABLE_DESKTOP_OPENGL_RENDER`.
See [the build guide](build.md) for Android build details.

MSAA render targets are rejected by descriptor normalization. The GLES backend
also rejects BGRA32; use Auto/RGBA32. Deploy managed bindings and the native
plugin from the same build: atlas region commands require render payload ABI 3.
Older native binaries are rejected even when atlas sharing is disabled. Rebuild
the native plugin for every shipping platform when updating these bindings.

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

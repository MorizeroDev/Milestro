# Troubleshoot a missing or blank result

Start with the symptom. Each section points to the smallest check that rules out
the common cause first.

## Nothing renders

1. Confirm the GameObject has an active `Milestro` → **Text Box** component.
2. Confirm the `RectTransform` has a non-zero width and height.
3. Confirm the producer has non-empty content:

   ```csharp
   var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();
   Debug.Log(producer != null ? producer.content : "No producer");
   ```

4. Confirm the object is under an enabled Canvas and is not clipped by a parent
   mask.
5. Check the Console for `DllNotFoundException` or ICU errors before changing
   text settings.

## Text is white on a white background

The default `textStyle.textColor` is white. Set it explicitly:

```csharp
producer.textStyle.textColor = Color.black;
```

## `DllNotFoundException: libMilestro`

Milestro found the managed assembly but not the native library for the current
Platform and Architecture.

- Check that the extracted package contains
  `Milestro/Plugins/<platform>/<library>` somewhere under `Assets/`, usually
  `Assets/unity-plugins/Milestro/Plugins/`.
- Confirm the build target matches the binary. The release package includes
  Windows x86-64, Linux x86-64, macOS arm64, Android arm64-v8a, and iOS arm64.
- Do not mix managed files from one build with native binaries from another.
  Rebuild both from the same source revision when you update either side.
- On iOS, make sure `FrameworkBinding.cpp` and the Milestro framework are part
  of the generated Xcode project.

## ICU startup error

The package contains:

```text
Resources/Milestro/icudtl.dat.bytes
```

With the default extraction this is
`Assets/unity-plugins/Resources/Milestro/icudtl.dat.bytes`. Unity recognizes a
`Resources` folder at any depth under `Assets/`, so the intermediate directory
name does not matter.

The prebuilt package includes this file. If you use the repository source tree
directly, create it from
`ext/icu-cmake/common/icudtl.dat` or run `./gradlew packageUnityPlugin`.

The resource is loaded as `Milestro/icudtl.dat` without the `.bytes` suffix.
The `.bytes` extension is what makes Unity import it as a `TextAsset`.

If ICU is initialized by your own bootstrap, the `MILESTRO_NO_ICU_INIT` define
disables Milestro's automatic initialization. See
[Runtime configuration](../reference/configuration.md).

## `TextInput` cannot receive focus or typing

1. Check that the scene contains exactly one active `EventSystem`.
2. Check which input module is active. Use `InputSystemUIInputModule` for
   editable text.
3. Check that `com.unity.inputsystem` is in `[1.16.0,2.0.0-0)`.
4. Check that Active Input Handling is **Input System Package (New)** or
   **Both**.
5. Inspect `HybridInputRuntime.Diagnostics` and read `SelectionStatus`,
   `ProviderId`, and `LastDiagnostic`.

A `StandaloneInputModule` may still provide ordinary UI navigation, but it does
not provide the strict focus session required by `TextInput`. See
[Hybrid input](../reference/hybrid-input.md).

## Input focus works, but the IME behaves differently

Ordinary composition and committed text are supported. Strict cancellation and
owner transfer of an active platform composition are platform-limited. In
particular, macOS can leave a system candidate window visible after a handoff,
and iOS/Android cannot strictly cancel the host editor's composition.

Read the platform notes in
[Known limitations](../reference/limitations.md#hybridinput-ime-composition-and-session-control)
before building a custom focus handoff.

## `Render Error` appears instead of rich text

The editor reports parser details as visible error text; player builds fall back
to `Render Error`. Check the Console, then inspect the markup against the
supported tag and attribute set in [Text, fonts, and rich text](../guides/text.md#rich-text).

Common causes are an unknown tag, malformed XML, nested `<a>`, multiple `<rt>`
elements, or empty rich-text values that the current parser does not accept.

## Text is clipped or unexpectedly small

These settings interact:

- `wrapMode` controls line breaking.
- `paragraphStyle.singleLine` forces a single line.
- `textOverflow` chooses clipping, ellipsis, or overflow.
- `TextBoxLayoutElement` can make the box shrink to content.
- `enableAutoFontSize` can reduce the font to fit a fixed box.

Change one at a time and inspect the inspector. The exact rules are in
[Layout and sizing](../guides/layout.md) and
[Layout engine parameters](../reference/layout-engine.md).

## Missing ParaParty assembly

If Unity reports missing `Paraparty.Colors` or `Paraparty.UnityNative`, verify
both Git dependencies are present in `Packages/manifest.json` exactly as shown
in [Install](install.md). Package resolution requires Git and network access;
wait for Unity's Package Manager to finish resolving before entering Play Mode.

## Rendering works in the editor but fails in a player

- Confirm the native plugin is included for the target architecture.
- Confirm the graphics API is supported by the selected render backend.
- Rebuild the managed package and native binaries from the same revision.
- Check that render targets are not using unsupported MSAA or a backend-specific
  format.

See [Rendering, batching, and backends](../reference/rendering.md) for backend
selection and its constraints.

# Milestro documentation

Milestro renders text in Unity through a native Skia text pipeline. This
documentation is organized around what you want to do, starting with a runnable
project:

1. install a prebuilt Unity package;
2. render a first string;
3. add an editable text field;
4. look up the feature you need next.

You do not need to build Milestro from source to try it.

## First success

Start with steps 1 and 2. Continue with step 3 when you need editable text, and
use step 4 when something does not render or receive focus.

| Step | Page | What you get |
| --- | --- | --- |
| 1 | [Install the Unity package](getting-started/install.md) | A Unity project containing Milestro and the required runtime assets |
| 2 | [Render your first text](getting-started/first-text.md) | A `TextBox` showing text in the current scene |
| 3 | [Add your first input field](getting-started/first-input.md) | An editable `TextInput` with value and submit events |
| 4 | [Troubleshoot a blank or missing result](getting-started/troubleshoot.md) | A symptom-based checklist instead of guessing |

The shortest path is: download `milestro-unity-plugins` from a GitHub Actions
run or a Release, extract it into `Assets/`, add the two required packages, and
run the first-text example.

## Choose the right text component

| Goal | Unity component | Guide |
| --- | --- | --- |
| A paragraph with wrapping, rich text, links, or Ruby | `TextBox` | [Text, fonts, and rich text](guides/text.md) |
| A short single-line UI label | `CanvasSlimText` | [SlimText](guides/slim-text.md) |
| A paragraph in world space | `WorldSpaceTextBox` | [World-space text](guides/world-space.md) |
| A short single-line label in world space | `WorldSpaceSlimText` | [World-space text](guides/world-space.md) |
| An editable UI field | `TextInput` | [Input and IME](guides/input.md) |
| Custom paragraph or editing behaviour | `ParagraphBuilder` / `InputBox` | [Layout engine parameters](reference/layout-engine.md) |

`TextBox` creates its `TextBoxRenderTextureProducer` automatically. Set the
content and styles on the producer, not on `TextBox`.

`InputBox` is not a Unity component. It is the low-level native editing model
owned by `TextInput`; add `TextInput` when you need an editable field.

## Guides

Use these when you already have a scene working:

- [Text, fonts, and rich text](guides/text.md): change content, font family,
  size, weight, spacing, rich-text markup, and Ruby annotations.
- [SlimText](guides/slim-text.md): use the lightweight single-line path for UI
  and world-space labels.
- [Layout and sizing](guides/layout.md): wrapping, overflow, margins, content
  sizing, auto font size, and color gradients.
- [Input and IME](guides/input.md): configure `TextInput`, validate input,
  handle events, and use the mobile keyboard.
- [Scrolling and scrollbars](guides/scrolling.md): wheel scrolling, scrollbar
  binding, and Elastic overscroll.
- [Links and accessibility](guides/links-and-accessibility.md): clickable text,
  keyboard navigation, and screen-reader components.
- [World-space and lightweight text](guides/world-space.md): labels in 3D space,
  single-line UI labels, and batching.
- [Coming from TextMesh Pro](guides/coming-from-tmpro.md): map TMP concepts to
  Milestro components and avoid one-to-one assumptions.

## Reference

Reference pages describe exact behavior and advanced configuration:

- [Unity components and events](reference/components.md)
- [Runtime configuration](reference/configuration.md)
- [Layout engine parameters](reference/layout-engine.md)
- [Rendering, batching, and backends](reference/rendering.md)
- [Hybrid input provider contract](reference/hybrid-input.md)
- [Known limitations](reference/limitations.md)
- [Horizontal Ruby layout](reference/ruby-layout.md)
- [Vertical text research and implementation status](reference/vertical-text-research.md)
- [TextInput accessibility design](reference/accessibility-design.md)
- [Build from source and CI reference](reference/build.md)

## Requirements at a glance

- Unity 6 project. Screen-reader support additionally requires Unity 6000.3 or
  newer and the built-in `com.unity.modules.accessibility` module.
- `party.para.util.colors` and `party.para.util.unitynative` in
  `Packages/manifest.json`.
- `com.unity.inputsystem` in `[1.16.0,2.0.0-0)` for editable `TextInput` focus
  and `InputSystemUIInputModule`.
- A native plugin binary for the platform and architecture you run. The release
  package includes the targets listed in [Install](getting-started/install.md).

## Other entry points

- [Project README](../readme.md) for the architecture overview.
- [Repository layout](../readme.md#repository-layout) for where source lives.
- [Android Unity test notes](../tests/android_unity/README.md) for device-level
  validation.

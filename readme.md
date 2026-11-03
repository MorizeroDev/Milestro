# Milestro

[English](readme.md) | [简体中文](readme.zh-CN.md)

Milestro brings reading-oriented, multilingual typography to Unity, powered by
Skia and ICU.

Text-heavy interfaces need more than labels: they need natural punctuation
spacing, mixed-script shaping, font fallback, annotations, and predictable
layout. Milestro focuses on these needs for reading views, dialogue, localized
interfaces, and editable text.

The project is still a proof of concept under active development. APIs may
change; distribution currently uses an `Assets/` plugin package.

## Text That Feels Good to Read

Balanced punctuation spacing, natural mixed-script text, and a comfortable
paragraph rhythm help readers focus on the content. From a line of dialogue
to a full page, Milestro aims to make text feel ready to read with its default settings.

## What Makes Milestro Different

- **CJK and vertical typography.** Canvas `TextBox` supports `vertical-rl`,
  mixed upright/sideways orientation, and tate-chu-yoko for short horizontal
  runs within a vertical column.
- **Ruby annotations.** Attach readings to base text with controls for size,
  gap, alignment, and overhang.
- **Fonts by family.** Register font files and request ordered family lists,
  with native and system fallback, rather than assigning a `TMP_FontAsset`.
- **Expressive markup.** Milestro Markup combines inline styles, paragraph
  margins, no-wrap ranges, case conversion, backgrounds, links, placeholders,
  and element IDs for geometry queries.
- **Existing content support.** Optional Markdown and TMPro parsers use the
  same paragraph rendering pipeline. The TMPro compatibility layer reuses
  supported TMP tags while applications adopt Milestro components.
- **Text as part of the interface.** Layout sizing, scrolling, scrollbars,
  editable input with IME, and optional accessibility helpers accompany the
  text components.

Vertical writing currently targets fixed-height Canvas `TextBox` components.
Platform and component limits are documented in
[Known limitations](docs/en/reference/limitations.md).

## Start in Unity

Download `milestro-unity-plugins.tar.gz` from GitHub Releases, or the
`milestro-unity-plugins` artifact from **Formal Build and Release**.
Extract the archive into your Unity 6 project's `Assets/` directory, preserving
`Resources/` and `.meta` files. Follow the
[installation guide](docs/en/getting-started/install.md) for required packages
and configuration.

Use `TextBox` for paragraphs, `CanvasSlimText` for short single-line labels,
and `TextInput` for editable fields. World-space text components are also
available.

```csharp
using Milestro.Components.Internal;
using UnityEngine;

var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();
producer.richTextParserId = Milestro.RichTextParser.XmlRichTextParser.Id;
producer.content = "Read <ruby>漢字<rt>かんじ</rt></ruby> with Milestro.";
producer.textStyle.size = 40f;
producer.textStyle.textColor = Color.white;
```

## Explore

- [First text](docs/en/getting-started/first-text.md) and
  [first input field](docs/en/getting-started/first-input.md).
- [Text, fonts, and markup](docs/en/guides/text.md), including typography defaults.
- [Markup showcases](docs/en/examples/README.md) with horizontal and vertical XML examples.
- [Coming from TextMesh Pro](docs/en/guides/coming-from-tmpro.md).
- [English manual](docs/en/README.md) / [简体中文手册](docs/zh/README.md).
- [Build from source](docs/en/reference/build.md) and
  [rendering backends](docs/en/reference/rendering.md) for contributors.

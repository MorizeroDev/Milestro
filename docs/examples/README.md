# Rich text showcases

These XML files are ready-to-paste `TextBoxRenderTextureProducer.content`

| File | Writing mode | Focus |
| --- | --- | --- |
| [Horizontal showcase](horizontal-example.xml) | `WritingMode.HorizontalTb` | Inline styles, bidirectional paragraphs, alignment, margins, OpenType features, links, Ruby, and placeholders |
| [Japanese vertical showcase](vertical-rl-ja-example.xml) | `WritingMode.VerticalRl` | Japanese content, vertical orientation, tate-chu-yoko, column alignment, indent, Ruby, and placeholders |
| [Traditional Chinese vertical showcase](vertical-rl-zh-Hant-example.xml) | `WritingMode.VerticalRl` | Traditional Chinese content with the same feature coverage and reading annotations |

## Display a showcase

1. Create a Canvas `TextBox` as described in [First text](../getting-started/first-text.md).
2. Give its `RectTransform` a fixed, nonzero size, for example `1000 × 800`.
   Use a dark background for the showcase colors.
3. Copy the entire XML file into the producer's Content field, or copy the file
   into the Unity project's `Assets/` directory and assign it as a `TextAsset`
   to a script that sets `producer.content = showcaseAsset.text`.
4. Apply the settings below. The XML does not set the writing mode.

With `textBox` referencing the Canvas `TextBox` and `showcaseAsset` referencing
the imported XML asset:

```csharp
using Milestro.Components.Internal;
using Milestro.Model;
using UnityEngine;

var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();
producer.enableAutoFontSize = false;
producer.textStyle.size = 32f;
producer.textStyle.textColor = Color.white;
producer.paragraphStyle.singleLine = false;
producer.paragraphStyle.textDirection = TextDirection.Ltr;
producer.paragraphStyle.textAlign = TextAlign.Start;
producer.paragraphStyle.paragraphSpacing = 12f;
producer.paragraphStyle.writingMode = WritingMode.HorizontalTb;
// For either vertical showcase, use WritingMode.VerticalRl instead.
producer.wrapMode = TextBoxWrapMode.Wrap;
producer.textOverflow = TextOverflow.Overflow;
producer.content = showcaseAsset.text;
```

The examples are longer than one viewport. Scroll down in the horizontal
showcase and toward later columns on the left in either vertical showcase. Add an
EventSystem for pointer interaction and a `TextBoxScrollbar` for visible scroll
controls; see [Scrolling](../guides/scrolling.md).

Keep auto font size disabled when comparing the labeled sizes and offsets.
The outer `<font size="32">` sets the run size; setting the component size to
32 as well makes the block-margin `em` examples use the same reference size.
Resize the width for horizontal wrapping and the height for vertical wrapping.
The vertical XML files set the inherited locale to `ja` or `zh-Hant`; individual
multilingual samples override it locally. Reading annotations use the same Ruby
layout in both files; the Chinese sample does not imply a separate Bopomofo
layout engine.

Horizontal section headings declare `margin-top: 64px` and
`margin-bottom: 6px`. Vertical section headings declare `margin-right: 64px`
and `margin-left: 6px`, placing the larger margin on the side of the preceding
paragraph. The current parser does not yet apply these left/right paragraph
margin declarations.

## What to expect

- Font families such as Google Sans, Source Han Sans VF, and Noto Color Emoji
  are requests, not bundled fonts. Register fonts as needed; fallback, weight,
  width, slant, locale-sensitive glyphs, and OpenType substitutions depend on
  the available faces. See [Fonts](../guides/text.md#fonts).
- Links supply application data through `TextBox` events. They do not open a
  browser automatically. See [Links](../guides/links-and-accessibility.md).
- `<img/>` leaves an empty inline box. The application draws its own visual
  using `TryGetPlaceholderGeometrySnapshot`; an empty gap is the expected
  result without that integration. See [Inline placeholders](../guides/text.md#inline-placeholders).
- Horizontal paragraph margins collapse between adjacent blocks. In
  `vertical-rl`, top/bottom margins are physical vertical insets for each
  paragraph; `paragraphSpacing` supplies the horizontal gap between blocks.
- Vertical positive baseline shifts move right; negative shifts move left.
  `text-indent` moves the first column down or up. Ruby `over` is on the right;
  `under` falls back to that side. Negative letter spacing and RTL paragraphs
  are excluded from the vertical showcases because they are unsupported.
- `font-feature-settings: normal` clears all inherited feature settings,
  including the default `chws`. It does not restore a preset feature list.

For supported syntax and limits, see [Rich text](../guides/text.md#rich-text),
[Vertical writing](../guides/text.md#vertical-writing), and
[Ruby layout](../reference/ruby-layout.md).

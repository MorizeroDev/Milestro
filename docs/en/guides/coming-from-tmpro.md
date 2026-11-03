# Coming from TextMesh Pro

[English](coming-from-tmpro.md) | [简体中文](../../zh/guides/coming-from-tmpro.md)

Milestro is not a drop-in `TMP_Text` replacement. TextMesh Pro puts layout,
styling, font assets, and presentation behind one component. Milestro separates
those responsibilities:

```text
TextBox                    presents a render texture in uGUI
TextBoxRenderTextureProducer
  content                  the string
  textStyle                font families, size, weight, color, spacing
  paragraphStyle           alignment, direction, single-line, ellipsis spacing
  wrapMode / textOverflow  line breaking and overflow policy
  margin                   insets and CSS-like units
TextBoxLayoutElement       reports intrinsic size to Unity layout
```

`TextBox` creates the producer automatically. Configure the producer; do not
look for one monolithic `fontSize` property on `TextBox`.

## Concept mapping

| TextMesh Pro | Milestro |
| --- | --- |
| `TMP_Text.text` | `TextBoxRenderTextureProducer.content` or `SlimTextRenderTextureProducer.text` |
| `fontSize` | `producer.textStyle.size` for paragraph text; `SlimTextRenderTextureProducer.fontSize` for SlimText |
| `font` / `TMP_FontAsset` | `textStyle.fontFamilies`, a family list resolved through `FontRegistry` and system fallback |
| `color` | `textStyle.textColor` for paragraph text; SlimText has `color` |
| `alignment` | `paragraphStyle.textAlign`; paragraph direction is `paragraphStyle.textDirection` |
| `enableWordWrapping` | `wrapMode` plus `paragraphStyle.singleLine` |
| Overflow mode | `textOverflow` (`Clip`, `Ellipsis`, `Overflow`) |
| Margins | `producer.margin`, using px, container %, `vw`, `vh`, or `em` |
| Auto Size | `enableAutoFontSize`, `fontSizeMin`, `fontSizeMax` |
| Content Size Fitter support | `TextBoxLayoutElement` for paragraph text; SlimText implements `ILayoutElement` directly |
| `<link>` rich text | `<a href="..." id="...">` and `TextBox.onLinkClicked` |
| `<sprite>` / sprite asset | Not part of the current component-level rich-text subset |
| `TMP_InputField` | `Milestro.Components.TextInput`, backed by the process-wide HybridInput dispatcher |
| `characterLimit` | `TextInput.characterLimit` |
| Content type and keyboard | `TextInput.contentType` and `keyboardType` |
| Rich-text tags | Select `richTextParserId`: Milestro Markup, Markdown, or the optional TMPro syntax module; plain text by default |

The biggest practical difference is fonts: Milestro does not bind a text
component to a generated font atlas asset. It resolves font families through its
native font registry and fallback chain, then rasterizes the paragraph into a
render texture.

## Milestro Markup and TMP compatibility

Milestro provides its own **Milestro Markup** for font styling, paragraph
layout, Ruby annotations, inline placeholders, and element position queries.
The comparison below shows how these capabilities relate to TMP rich text.

Milestro also provides a **TMPro rich-text compatibility layer** for existing
content. Use it to retain supported TMP tags during migration; author new
content in Milestro Markup if desired. Each text component selects one syntax
through `richTextParserId`.

Install the `Milestro.RichText.TMPro` module (included in the prebuilt package)
and select **TMPro** in the producer's **Rich Text Parser** field:

```csharp
producer.richTextParserId = Milestro.RichText.TMPro.TMProRichTextParser.Id;
producer.content = "<size=150%><b>Hello</b></size> <link=help>Help</link>";
```

The module has no TextMesh Pro dependency. It accepts common TMP style, font,
color, metric, case-conversion, no-wrap, alignment, link, and `<noparse>` tags.
Unsupported or unmatched tags warn and remain literal. The compatibility layer
uses Milestro layout and rendering: paragraph layout tags create blocks, and
line height uses Milestro's font-size-based metrics. Check wrapping, spacing,
and font appearance after migration. For accepted tags and limitations, see
[supported syntax and error handling](text.md#tmpro) and the
[module README](../../../apps/unity-plugins/Milestro.RichText.TMPro/README.md).

## Migration examples

### Paragraph label

TextMesh Pro style:

```csharp
label.text = "Hello";
label.fontSize = 42;
label.color = Color.black;
label.alignment = TextAlignmentOptions.Center;
label.enableWordWrapping = true;
```

Milestro:

```csharp
using Milestro.Components.Internal;
using Milestro.Model;

var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();
producer.content = "Hello";
producer.textStyle.size = 42f;
producer.textStyle.textColor = Color.black;
producer.paragraphStyle.textAlign = TextAlign.Center;
producer.wrapMode = TextBoxWrapMode.Wrap;
```

### Simple one-line label

For a short label that never wraps and does not need rich text, links, or
scrolling, use `CanvasSlimText` instead of a full `TextBox`:

```csharp
using Milestro.Components;

public CanvasSlimText label;

label.Text = "Score: 1200";
label.TextColor = Color.white;
```

SlimText has a simpler parameter set and a dedicated single-line render path.
It is not a paragraph engine: it has no `paragraphStyle`, rich-text parser,
links, Ruby, or multi-line layout. See [SlimText](../guides/slim-text.md).

### Editable field

TextMesh Pro style:

```csharp
inputField.text = "Name";
inputField.characterLimit = 32;
inputField.onValueChanged.AddListener(OnChanged);
```

Milestro:

```csharp
input.Text = "Name";
input.characterLimit = 32;
input.onValueChanged.AddListener(OnChanged);
```

`TextInput` already owns the native `InputBox`, caret, selection, and
composition state. You do not create a child text component for the value or a
separate TMP input field. See [Input and IME](../guides/input.md).

<a id="rich-text-comparison"></a>
## Rich-text comparison

This table uses the [TMP 4.0 rich-text documentation](https://docs.unity3d.com/Packages/com.unity.textmeshpro@4.0/manual/RichText.html)
and its [supported tag list](https://docs.unity3d.com/Packages/com.unity.textmeshpro@4.0/manual/RichTextSupportedTags.html)
to compare common features and key differences. The Milestro column refers to
the built-in **Milestro Markup** parser.

This table describes **Milestro Markup** features and syntax. For tags accepted
by the compatibility layer, see the [TMPro parser](text.md#tmpro).
Support refers to Milestro Markup capabilities:
✅ Full, 🟡 Partial or requires application-side work, ❌ Unsupported.

| Feature | Milestro Markup support | TMP 4.0 | Milestro Markup |
| --- | --- | --- | --- |
| Markup syntax | 🟡 Partial | Tag values can use `<color=red>`; closing tags are optional for most tags | XML-like attributes and nesting; unknown tags are errors and `&` must be escaped |
| Bold, italic, underline, strikethrough | ✅ Full | `<b>`, `<i>`, `<u>`, `<s>`, `<strikethrough>` | `<b>`, `<i>`, `<u>`, `<s>`, plus `<strong>`, `<em>`, `<del>` |
| Color, size, weight, font | ✅ Full | `<color>`, `<size>`, `<font-weight>`, `<font>`; selects font assets | `<font color="…" size="…" weight="…" face="…">`; ordered font-family fallback |
| Superscript and subscript | ✅ Full | `<sup>`, `<sub>` | `<sup>`, `<sub>` |
| Baseline offset | ✅ Full | `<voffset>` | `<span voffset="…">` or `voffset-em`; wrap one character or a text range |
| Character spacing | ✅ Full | `<cspace>` | `<span cspace="…">` |
| Horizontal positioning | ❌ Unsupported | `<pos>` sets the line's caret position; `<space>` adds a horizontal offset | No equivalent tags; `cspace` adjusts spacing and `text-indent` adjusts paragraph indentation |
| Arbitrary per-character rotation | ❌ Unsupported | `<rotate>` rotates each character around its center | No equivalent tag; vertical `text-orientation` controls upright or sideways text |
| Links | ✅ Full | `<link>` marks a link ID | `<a href="…" id="…">`, with click, hover, and optional accessibility navigation |
| Inline images | 🟡 Partial | `<sprite>` draws from sprite assets | `<img width="…" height="…" key="…"/>` reserves space; the application draws the image |
| Alignment and indentation | 🟡 Partial | `<align>`, `<indent>`, `<line-indent>` | `<p align="…" style="text-indent: …">`; paragraph-block layout |
| Literal markup text | ✅ Full | `<noparse>` disables tag parsing within its scope | Use XML escaping: `&lt;b&gt;` displays `<b>`, and `&amp;` displays `&` |
| Inline no-wrap | ✅ Full | `<nobr>` | `<span style="white-space: nowrap">`; nested `normal` restores soft wrapping and hard breaks remain active |
| Case conversion and simulated small caps | ✅ Full | `<lowercase>`, `<uppercase>`, `<allcaps>`, `<smallcaps>` | `text-transform: lowercase / uppercase / smallcaps`; `none` restores original text; smallcaps uppercases original lowercase letters at 80% size |
| Background highlighting | ✅ Full | `<mark>` | `<mark>` defaults to yellow; `background-color` sets the background without changing foreground color |
| Paragraph margins | 🟡 Partial | `<margin>` and left/right margin tags | `<p style="margin: …">` and four physical longhands, scoped to paragraph blocks |
| Text area width | 🟡 Partial | `<width>` changes the text area width | Paragraph left/right margins control available width; in vertical layout, top/bottom margins control available inline height; scoped to paragraph blocks |
| Opacity | 🟡 Partial | `<alpha>` sets opacity independently | `<font color="#RRGGBBAA">` sets color and opacity together; no independent opacity tag |
| Explicit line breaks | ✅ Full | `<br>` | `<br/>` or newline characters |
| Line height | 🟡 Partial | `<line-height>` adjusts height relative to the font asset's default | `producer.textStyle.lineHeight` sets a multiplier; no inline line-height tag |
| Local gradient presets | ❌ Unsupported | `<gradient>` applies a gradient preset to a text range | Component gradient presentation settings are available; no local gradient tag |
| Forced monospace advance | ❌ Unsupported | `<mspace>` sets uniform character advances | A monospace font can be selected; no tag forces uniform advances |
| Pagination | ❌ Unsupported | `<page>` creates page breaks with Page overflow mode | No page-break tag or Page overflow mode |
| Named styles | ❌ Unsupported | `<style>` applies a named style preset | Inline `style="…"` declarations are supported; no named styles, class selectors, or stylesheets |

## Additional Milestro capabilities

Milestro Markup and its layout APIs provide the following capabilities. This
comparison covers the official built-in TMP 4.0 tags and layout interfaces;
project scripts and third-party extensions can implement additional effects.

| Feature | Milestro support | TMP 4.0 | Milestro |
| --- | --- | --- | --- |
| Ruby annotations | ✅ Supported | No built-in Ruby tag | `<ruby>`, `<rt>`, with position, alignment, overhang, size, and gap controls; see [Ruby layout](../reference/ruby-layout.md) |
| Native vertical writing | ✅ Supported | No native `vertical-rl` layout mode; `<rotate>` rotates characters | `WritingMode.VerticalRl` lays out columns top to bottom and right to left, including column breaks, links, and scrolling geometry; see [vertical writing](text.md#vertical-writing) |
| Vertical text orientation | ✅ Supported | No equivalent `text-orientation` tag | `mixed` follows Unicode orientation properties, `upright` stands characters upright, and `sideways` rotates them |
| Tate-chu-yoko | ✅ Supported | No built-in tate-chu-yoko tag | `text-combine-upright: all` places a short horizontal run within a vertical column, squeezing it to one em or widening the column |
| General OpenType feature control | ✅ Supported | No general rich-text tag for arbitrary feature tags and values | Per-run `font-feature-settings` controls `'liga'`, `'tnum'`, and `'chws'`; `font-kerning` controls kerning; results depend on the font |
| Element IDs and empty anchors | ✅ Supported | `<link>` marks links; no general element ID or empty paragraph anchor tag | `id` and `TryGetElementGeometryById` query text, paragraphs, placeholders, and empty anchors in content and Canvas coordinates; see [element geometry](text.md#element-geometry) |
| Four-sided paragraph margins | ✅ Supported | `<margin>` controls horizontal margins | `<p style="margin: …">` supports four physical sides, shorthand, and collapsing block margins; vertical layout applies them along their physical axes; see [text styles](text.md#rich-text) |

Vertical writing currently requires a fixed-height Canvas `TextBox`; it is
unavailable for `TextInput`, SlimText, and world-space components. Its base
direction is LTR, and `vertical-lr` is unsupported. Ruby does not support
nested or multiple annotation layers; vertical `under` falls back to the right.

## Things that do not translate one-to-one

When writing Milestro Markup, use `<span style="white-space: nowrap">...</span>` where TMP uses
`<nobr>...</nobr>`. The default `white-space: normal` restores automatic
wrapping for nested content; explicit line breaks remain active in both modes.

In Milestro Markup, use `<span style="text-transform: smallcaps">...</span>` for TMP-style small
caps. `lowercase`, `uppercase`, and the inherited default `none` are also
supported.

- A `TMP_FontAsset` is not a Milestro text property. Register font files and
  select a family list such as `{"Inter", "sans-serif"}`.
- TMP material presets and per-character vertex animation do not have a direct
  `TextBox` equivalent. Color gradients and uGUI material tinting are separate
  presentation features.
- Unknown tags are errors in Milestro Markup. The optional `tmpro` parser
  warns and preserves unsupported or unmatched tags as literal text.
- Paragraph spacing is block-based. Milestro builds one Skia paragraph per
  rich-text block, stacks them, and applies `paragraphStyle.paragraphSpacing`
  plus the supported `<p>` margins.
- `TextBoxLayoutElement` and `enableAutoFontSize` solve different problems:
  layout sizing changes the box, while auto font size changes glyph size.

For the exact supported tags and layout values, continue with
[Text, fonts, and rich text](text.md) and
[Layout engine parameters](../reference/layout-engine.md).

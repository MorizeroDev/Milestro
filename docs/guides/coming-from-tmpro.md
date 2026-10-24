# Coming from TextMesh Pro

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
| Rich-text tags | A smaller XML-like subset parsed by `Milestro.RichTextParser` |

The biggest practical difference is fonts: Milestro does not bind a text
component to a generated font atlas asset. It resolves font families through its
native font registry and fallback chain, then rasterizes the paragraph into a
render texture.

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

## Things that do not translate one-to-one

- A `TMP_FontAsset` is not a Milestro text property. Register font files and
  select a family list such as `{"Inter", "sans-serif"}`.
- TMP material presets and per-character vertex animation do not have a direct
  `TextBox` equivalent. Color gradients and uGUI material tinting are separate
  presentation features.
- TMP's rich-text tag set is larger than Milestro's. Unknown tags are parser
  errors, not silently ignored styling.
- Paragraph spacing is block-based. Milestro builds one Skia paragraph per
  rich-text block, stacks them, and applies `paragraphStyle.paragraphSpacing`
  plus the supported `<p>` margins.
- `TextBoxLayoutElement` and `enableAutoFontSize` solve different problems:
  layout sizing changes the box, while auto font size changes glyph size.

For the exact supported tags and layout values, continue with
[Text, fonts, and rich text](text.md) and
[Layout engine parameters](../reference/layout-engine.md).

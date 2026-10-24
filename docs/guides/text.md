# Text, fonts, and rich text

This page is the feature guide for paragraph text. It covers `TextBox`,
`TextBoxRenderTextureProducer`, fonts, rich-text markup, and text spacing.

## Choose the right component

| Need | Component |
| --- | --- |
| Wrapped paragraphs, scrolling, links, Ruby, rich text | `TextBox` + `TextBoxRenderTextureProducer` |
| Cheap single-line UI label | `CanvasSlimText` |
| Cheap single-line world-space label | `WorldSpaceSlimText` |
| Editable field | `TextInput` |
| Low-level paragraph construction | `ParagraphBuilder` + `Paragraph` |

`TextBox` is a uGUI `Graphic` that presents a render texture. The producer
owns the text and its style. `CanvasSlimText` and `WorldSpaceSlimText` are
different components, not modes of `TextBox`. See [SlimText](slim-text.md).

## The three text style layers

Paragraph rendering reads three groups of settings from the producer:

| Layer | Property | Controls |
| --- | --- | --- |
| Text run | `producer.textStyle` | Font families, size, weight, width, slant, color, spacing, locale |
| Paragraph | `producer.paragraphStyle` | Alignment, text direction, single-line mode, ellipsis string, extra block spacing |
| Component layout | `producer.wrapMode`, `producer.textOverflow`, `producer.margin`, `producer.enableAutoFontSize` | Line breaking, overflow, insets, automatic glyph fitting |

This separation is intentional. For example, changing `wrapMode` changes line
breaking, while changing `paragraphStyle.textAlign` changes how the resulting
lines are aligned inside the paragraph.

## Set text and basic style

```csharp
using Milestro.Components.Internal;
using Milestro.Model;
using UnityEngine;

var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();

producer.content = "Milestro renders paragraphs with Skia.";
producer.textStyle.size = 40f;
producer.textStyle.textColor = Color.black;
producer.textStyle.fontFamilies = new[] { "Inter", "sans-serif" };
producer.textStyle.weight = FontWeight.Medium;
producer.paragraphStyle.textAlign = TextAlign.Start;
producer.wrapMode = TextBoxWrapMode.Wrap;
producer.textOverflow = TextOverflow.Clip;
```

`textStyle.weight` uses numeric Skia values. The constants in
`Milestro.Model.FontWeight` are `Thin` (100), `Normal` (400), `Medium` (500),
`SemiBold` (600), `Bold` (700), `Black` (900), and so on.

`textStyle.width` and `textStyle.slant` select the width and slant axes of a
variable or multi-face family:

```csharp
producer.textStyle.width = FontWidth.Condensed;
producer.textStyle.slant = FontSlant.Italic;
```

`textStyle.locale` defaults to `zh-Hans` and affects locale-sensitive shaping,
line breaking, and font fallback. Set it explicitly when the content language
is known:

```csharp
producer.textStyle.locale = "en-US";
```

## Fonts

Milestro does not use a Unity `Font` or a `TMP_FontAsset` reference on the text
component. A text style contains a **font-family list**. Milestro resolves that
list through its native registry and the platform font fallback chain.

### Register font files

Register font files before creating the text that needs them:

```csharp
using Milestro.Skia;

// fontPath must be a file path readable by the current platform.
FontRegistry.RegisterFontFromPath(fontPath);
```

Then request the family name exposed by that font file:

```csharp
producer.textStyle.fontFamilies = new[] { "My Font", "sans-serif" };
```

The editor menu **Milestro** → **Font Registry** can list the registered
families and faces, which is useful when checking the real family name inside a
font file.

### Family keywords and fallback

The parser and text style accept ordered family declarations. The built-in
keywords are:

- `serif`
- `sans-serif`
- `monospace`
- `system-ui`

Applications can add or override keywords such as `heading`, `body`, or `code`
through `MilestroConfiguration.Configuration.FontFamily`. The declarations are
expanded when a paragraph is built. A later family is a fallback, not a style
override. Quoted rich-text family names are treated as literal names rather than
keywords.

The default is `system-ui`. If a requested family is missing, check the
registered family names before assuming that the text has no glyphs.

## Rich text

`TextBoxRenderTextureProducer.content` is parsed as rich text. Plain text is
valid; when the string contains `<` or `&`, the parser treats it as XML-like
markup. The supported subset is smaller than TextMesh Pro's tag set.

### Supported tags

| Tag | Effect |
| --- | --- |
| `<b>`, `<strong>` | Weight 700 |
| `<i>`, `<em>` | Italic slant |
| `<u>` | Underline |
| `<s>`, `<del>` | Strikethrough |
| `<sup>`, `<sub>` | Superscript or subscript run |
| `<font>` | Inline color, size, weight, family, and CSS style |
| `<span>` | Lower-level inline overrides |
| `<p>` | Paragraph block, alignment, direction, CSS block styles |
| `<br>` | Explicit line break |
| `<a href="target" id="optional-id">` | Clickable range |
| `<ruby>base<rt>annotation</rt></ruby>` | Ruby annotation |
| `<rp>` | HTML fallback text inside Ruby; not rendered |

Unknown tags are rejected. In the Unity editor the parser shows the error text;
in a player build the fallback is `Render Error`.

### Inline style

`<font>` accepts inline values:

```xml
<font color="#3366ff" size="48" weight="700" face="Inter">
  Blue and bold
</font>
```

`<span>` accepts the lower-level overrides:

| Attribute | Meaning |
| --- | --- |
| `cspace` | Extra letter spacing in points |
| `voffset` | Absolute baseline shift |
| `voffset-em` | Baseline shift in em units |
| `fscale` | Absolute font-size multiplier; must be positive |
| `lang` | Locale |
| `face` | Font-family list |
| `weight`, `width`, `slant` | Font style axes |

`<font>`, `<span>`, and `<p>` also accept a `style` attribute with a small CSS
subset:

- `font-kerning: auto | normal | none`
- `font-feature-settings: normal` or comma-separated settings such as
  `'liga' 0`, `'tnum' on`, and `'kern' 1`

Use `&quot;` if a double-quoted feature tag appears inside a double-quoted XML
attribute.

### Paragraph blocks

`<p>` starts a new block. It accepts `align`, `dir`, and `style`:

```xml
<p align="center" dir="ltr" style="text-indent: 2em; margin-bottom: 12px">
  A centered paragraph with a first-line indent.
</p>
```

Supported paragraph CSS includes `text-indent` in `px`, `em`, or `%`, plus
`margin-top` and `margin-bottom` in `px`, `em`, or `%`. These margins are
between rich-text blocks. They are not the same as the `TextBox` margin around
the whole viewport.

The `em` unit resolves against the paragraph's inherited font size. A negative
text indent can paint into the horizontal margin, but layout still uses the
width inside the margins. The CSS `hanging` and `each-line` modifiers are not
supported.

### Text spacing

Component-level spacing is available on `producer.textStyle`:

```csharp
producer.textStyle.size = 36f;
producer.textStyle.letterSpacing = 0.5f; // extra points between glyphs
producer.textStyle.wordSpacing = 2f;     // extra points at word boundaries
producer.textStyle.lineHeight = 1.25f;   // line-height multiplier
```

`lineHeight` is a multiplier, not an absolute offset. Values at or below zero
keep the font's natural metrics. Rich-text runs inherit these values; a local
`cspace` style can still override letter spacing.

The producer also has a default gap between paragraph blocks:

```csharp
producer.paragraphStyle.paragraphSpacing = 12f;
```

This is applied between adjacent blocks. Rich-text `<p>` elements can add their
own `margin-top` and `margin-bottom`; adjacent vertical margins collapse, and
the producer spacing is added as the default gap.

### Ruby

HTML-shaped Ruby is supported:

```xml
<ruby style="ruby-position: over; ruby-align: space-around">
  漢字<rt>かんじ</rt>
</ruby>
```

Supported standard Ruby properties are `ruby-position`, `ruby-align`, and
`ruby-overhang`. Milestro also accepts the custom properties
`--milestro-ruby-size`, `--milestro-ruby-gap`, and
`--milestro-ruby-overflow`.

The current parser supports one plain-text `<rt>` per `<ruby>`. Nested Ruby,
multiple annotations, and styled markup inside `<rt>` are rejected. For the
native layout rules and managed `ParagraphBuilder` API, see
[Horizontal Ruby layout](../reference/ruby-layout.md).

## Parser behavior to remember

- Tags are case-sensitive XML element names.
- Plain text containing `&` is parsed as markup; escape it as `&amp;`.
- Anchor targets are application data. Milestro reports them but never opens or
  interprets the target.
- A malformed document does not silently degrade to plain text.
- The parser is not an HTML engine; it implements the documented subset only.

Next: [layout and sizing](layout.md), [links and accessibility](links-and-accessibility.md),
or [TextMesh Pro comparison](coming-from-tmpro.md).

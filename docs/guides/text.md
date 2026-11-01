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
| Paragraph | `producer.paragraphStyle` | Alignment, text direction, writing mode, single-line mode, ellipsis string, extra block spacing |
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
| `<img width=".." height=".." key=".."/>` | Inline placeholder box |
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

`<font>`, `<span>`, and other inline elements also accept a `style` attribute
with a small CSS subset:

- `font-kerning: auto | normal | none`
- `font-feature-settings: normal` or comma-separated settings such as
  `'liga' 0`, `'tnum' on`, and `'kern' 1`

The vertical-only properties (`text-combine-upright`, `text-orientation`, and
`--milestro-text-combine-upright-sizing`) are documented in
[Vertical writing](#vertical-writing).

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

Paragraphs enable the OpenType `chws` feature by default. Fonts that support
`chws` therefore apply Chromium-style CJK punctuation compression between
adjacent full-width punctuation marks (for example `、，。`); a punctuation
mark without an adjacent punctuation mark keeps its normal advance. Calling
`TextStyle.ResetFontFeature()` disables all font features, including this
default. In vertical-rl text, upright runs map that request to `vchw` instead;
fonts without `vchw` use an inline-axis fallback, so punctuation compression
never shifts later glyphs across the column.

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
[Ruby layout](../reference/ruby-layout.md).

### Inline placeholders

`<img/>` reserves an inline box without painting anything, so the application
can position its own visuals there:

```xml
Score: <img width="24" height="24" key="coin-icon"/> 1200
```

`width` and `height` are required positive numbers and `key` is an optional
application string. The box is atomic for wrapping and occupies one character
(U+FFFC) in the source text. It must be an empty element, and it is ignored
inside `<ruby>`.

Milestro only reports geometry; placing and animating the visual is up to the
application. Query the laid-out boxes from the `TextBox`:

```csharp
if (textBox.TryGetPlaceholderGeometrySnapshot(out var snapshot))
{
    foreach (var item in snapshot.Geometry)
    {
        // item.Key, item.StartUtf16, item.Visible
        // item.Rect is in paragraph layout units; convert to render-texture
        // pixels with PaintPosition and the auto font size FitScale:
        Vector2 centerPixels = snapshot.PaintPosition + item.Rect.center * snapshot.FitScale;
    }
}
```

Re-read the snapshot when `ContentVersion` or `Generation` changes.

## Vertical writing

`TextBox` supports vertical right-to-left writing (`vertical-rl`) for CJK
titles, labels, and multi-paragraph bodies:

```csharp
producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
```

Columns run top to bottom and advance from right to left. The same content,
fonts, rich-text markup, margins, scrolling, links, and auto font size apply.
Ruby annotations are placed on the physical right of the base column;
`ruby-position: under` falls back to that side with an Editor diagnostic.
Vertical-specific sizing, indent, alignment, and effect rules are in
[Layout and sizing](layout.md#vertical-writing-vertical-rl).

### Tate-chu-yoko

Mark short runs to typeset them horizontally inside a vertical column (dates,
numbers, short Latin words):

```xml
<span style="text-combine-upright: all">2025</span>年
<span style="text-combine-upright: all; --milestro-text-combine-upright-sizing: grow">netnewswire</span>
```

A combine group is shaped horizontally (not rotated) and occupies a one-em
slot down the column. It is atomic for wrapping, justification, and ellipsis
truncation, and its glyphs keep their real source text for hit testing and
links. With the default `squeeze` sizing, a group wider than one em is
compressed to fit; `--milestro-text-combine-upright-sizing: grow` keeps the
natural width and widens the column instead.

- `text-combine-upright: none | all` works on any inline element; consecutive
  `all` text forms one group.
- A group needs one uniform text style and cannot contain a hard break, an
  `<img/>` placeholder, or a Ruby base range.
- There is no `digits` auto-detection: unmarked digits follow the normal
  mixed-orientation rules.
- Horizontal paragraphs ignore combine marks entirely.

### Text orientation

The default `mixed` orientation stands CJK glyphs upright and rotates Latin
runs sideways, following the Unicode `Vertical_Orientation` property. Override
it per block or per span:

```xml
<span style="text-orientation: upright">ABC123</span>
<span style="text-orientation: sideways">漢字ABC</span>
```

- `text-orientation: mixed | upright | sideways` works on block and inline
  elements and inherits like other text styles.
- `upright` sets every glyph upright without rotation; Latin letters and
  digits stack one by one (it does not merge them — use tate-chu-yoko for
  that).
- `sideways` rotates every glyph 90 degrees clockwise, including CJK.
- Inside a `sideways` range, tate-chu-yoko marks have no effect; the text
  follows the normal sideways rules.
- Horizontal paragraphs ignore orientation marks entirely.

Current limits:

- Use a fixed-size Canvas `TextBox`. `TextInput` editing, SlimText, and the
  world-space components stay horizontal.
- The base direction stays LTR; RTL content is not supported. `textAlign`
  supports `Start`, `End`, `Center`, and `Justify`; `Left` and `Right` fall
  back to `Start`.
- `vertical-lr` is not available yet.
- Unsupported combinations degrade with an Editor diagnostic instead of
  failing silently.

## Parser behavior to remember

- Tags are case-sensitive XML element names.
- Plain text containing `&` is parsed as markup; escape it as `&amp;`.
- Anchor targets are application data. Milestro reports them but never opens or
  interprets the target.
- A malformed document does not silently degrade to plain text.
- The parser is not an HTML engine; it implements the documented subset only.

Next: [layout and sizing](layout.md), [links and accessibility](links-and-accessibility.md),
or [TextMesh Pro comparison](coming-from-tmpro.md).

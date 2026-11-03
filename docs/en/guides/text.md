# Text, fonts, and rich text

[English](text.md) | [简体中文](../../zh/guides/text.md)

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

Select a parser with `producer.richTextParserId`: `null` or `""` renders plain
text, `milestro_rich_text` selects Milestro Markup, and `markdown` selects the
Markdown module; `tmpro` selects the TMPro syntax module.

### Milestro Markup

Rich-text parsing is disabled by default. Pick a parser in the **Rich Text
Parser** dropdown in the Inspector, or set
`producer.richTextParserId = XmlRichTextParser.Id`, to parse
`TextBoxRenderTextureProducer.content` as XML-like markup (the built-in
`milestro_rich_text` parser, shown as "Milestro Rich Text"). When no parser is
selected, `<` and `&` have no special meaning and the content is rendered
literally. For differences in features and syntax, see the
[rich-text comparison](coming-from-tmpro.md#rich-text-comparison). This parser
implements the XML-like markup documented below, rather than a full HTML engine.

Parsers are plugins selected by id per text box. Modules implement
`IRichTextParserProvider` (id, display name, parser factory) and register it
with `RichTextParserService.Register(provider)`. Ids match
`[A-Za-z_][A-Za-z0-9_-]*` and live in serialized data, so never rename a
shipped id — register an alias instead. Display names only appear in the
inspector and may change freely. The dropdown lists every registered parser,
and the field also accepts ids typed by hand.

Use `null` or `""` for plain text, `XmlRichTextParser.Id`
(`milestro_rich_text`) for the built-in XML parser, or `"markdown"` for the
Markdown module described below. These APIs live in `Milestro.RichTextParser`.
An unknown non-empty id is an error; it does not fall back to plain text.

#### Supported tags

The following tags and XML-specific styles apply to **Milestro Rich Text**.

| Tag | Effect |
| --- | --- |
| `<b>`, `<strong>` | Weight 700 |
| `<i>`, `<em>` | Italic slant |
| `<u>` | Underline |
| `<mark>` | Yellow text background by default |
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

#### Inline style

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
| `lheight` | Positive line-height multiplier |
| `lang` | Locale |
| `face` | Font-family list |
| `weight`, `width`, `slant` | Font style axes |

`<font>`, `<span>`, and other inline elements also accept a `style` attribute
with a small CSS subset:

- `font-kerning: auto | normal | none`
- `font-feature-settings: normal` or comma-separated settings such as
  `'liga' 0`, `'tnum' on`, and `'kern' 1`
- `white-space: normal | nowrap`
- `text-transform: none | lowercase | uppercase | smallcaps`
- `background-color: <color>` with basic CSS color names, `transparent`,
  `#RGB[A]`, `#RRGGBB[AA]`, `oklab()`, or `oklch()` colors

`<mark>` uses a yellow background unless its inline `background-color`
overrides it. The property also works on other inline elements and paints the
background of the affected text runs without changing their foreground color:

```xml
<mark>Yellow highlight</mark>
<mark style="background-color: #3366ff80">Custom translucent highlight</mark>
```

`lowercase` and `uppercase` use ICU case mappings with the component locale;
an inline `lang` attribute overrides that locale. `smallcaps` follows TextMesh
Pro: original lowercase letters become uppercase at 80% of the effective font
size, while original uppercase letters and other characters keep their full
size. This is simulated small caps, rather than an OpenType small-caps feature.
Case conversion can change text length, for example `ß` becomes `SS`.
The property inherits through nested markup; use `none` to restore
untransformed text.

`normal` keeps the default automatic wrapping. `nowrap` suppresses soft line
breaks inside the inline range, matching the purpose of TMPro's `<nobr>` tag;
explicit `<br>` elements and newline characters still force breaks. Content
that exceeds the available inline size overflows instead of shrinking.

The vertical-only properties (`text-combine-upright`, `text-orientation`, and
`--milestro-text-combine-upright-sizing`) are documented in
[Vertical writing](#vertical-writing).

Use `&quot;` if a double-quoted feature tag appears inside a double-quoted XML
attribute.

#### Paragraph blocks

`<p>` starts a new block. It accepts `align`, `dir`, and `style`:

```xml
<p align="center" dir="ltr" style="text-indent: 2em; margin-bottom: 12px">
  A centered paragraph with a first-line indent.
</p>
```

Supported paragraph CSS includes `text-indent` in `px`, `em`, or `%`, plus
`margin`, `margin-top`, `margin-right`, `margin-bottom`, and `margin-left`.
Margins accept `px`, `em`, or `%`; the shorthand accepts one through four values
using the usual CSS expansion order. All block margins must be non-negative;
vertical layouts also accept `vw` and `vh`. In horizontal layout, left and right margins
inset every line while top and bottom margins add block spacing. In `vertical-rl`,
top and bottom margins inset the column while right and left margins add spacing
before and after the block. These margins are not the same as the `TextBox` margin
around the whole viewport.

The `em` unit resolves against the paragraph's inherited font size. A negative
text indent can paint into the horizontal margin, but layout still uses the
width inside the margins. The CSS `hanging` and `each-line` modifiers are not
supported.

#### Text spacing

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
keep the font's natural metrics. Rich-text runs inherit these values; local
`cspace` and `lheight` styles can override letter spacing and line height.

The producer also has a default gap between paragraph blocks:

```csharp
producer.paragraphStyle.paragraphSpacing = 12f;
```

This is applied between adjacent blocks. Rich-text `<p>` elements can add their
own block-start and block-end margins (`top`/`bottom` horizontally and
`right`/`left` in `vertical-rl`); adjacent margins collapse, and the producer
spacing is added as the default gap.

#### Ruby

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

#### Inline placeholders

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

<a id="element-geometry"></a>
#### Element IDs and geometry queries

Rendered elements accept a non-empty, case-sensitive `id`. Use it for an
`<img/>` placeholder, paragraph, text range, or empty anchor:

```xml
<p id="chapter"></p>
<p id="paragraph">A <span id="label">text range</span> and
<img id="icon" width="24" height="24" key="coin-icon"/>.</p>
```

After layout, query an element with `TextBox.TryGetElementGeometryById`:

```csharp
if (textBox.TryGetElementGeometryById("icon", out var element))
{
    Debug.Log(element.ContentRect);
    Debug.Log(element.CanvasRect);
}
```

`ContentRect` uses unscrolled content coordinates with a top-left origin and
y pointing down, in the same units as `TextBoxScrollState`. Read its y position
for horizontal text or x position in `vertical-rl`.
`CanvasRect` uses TextBox RectTransform local coordinates with y pointing up;
it includes the current scroll offset and is not clipped to the viewport.
Empty elements return zero-size rects at their text position. Repeated IDs
resolve to the first element in document order. The query returns `false` if
the ID is missing or layout geometry is unavailable.

An `id` identifies geometry, while an `<img>`'s `key` identifies placeholder
content for the application; both can be used together. IDs do not trigger
navigation automatically. See [Jump to an element id](scrolling.md#jump-to-an-element-id)
for scrolling code.

#### XML parser behavior to remember

- Tags are case-sensitive XML element names.
- With rich text enabled, plain text containing `&` is parsed as markup; escape
  it as `&amp;`.
- Anchor targets are application data. Milestro reports them but never opens or
  interprets the target.
- A malformed document does not silently degrade to plain text.
- The parser is not an HTML engine; it implements the documented subset only.


### Markdown

The `Milestro.RichText.Markdown` module registers **Markdown** (`markdown`)
automatically in the Editor and before scene load in Player builds. The
prebuilt package includes the module and `Markdig.dll`. The Unity project must
use the **.NET Standard 2.1** API compatibility level. When working from source,
first [build Markdig](../reference/build.md#quick-local-build).

```csharp
producer.richTextParserId = "markdown";
producer.content = "# Welcome\n\n**Bold**, *italic*, ~~deleted~~ and [help](help).";
```

The parser uses CommonMark with Markdig's emphasis extras. It maps bold,
italic, strikethrough, headings, paragraphs, lists, quotes, links, and autolinks
into paragraph payloads. Headings use weight 700 and size scales from 1.6 to
1.05 for levels 1–6. Lists use text markers and two spaces per nesting level;
quotes add indentation. Soft and hard line breaks both become `\n`.

Code spans and code blocks render as literal text with inherited styling;
they do not select a monospace font or syntax highlighting. Raw HTML is shown
literally, so XML tags such as `<ruby>` do not enable Milestro XML styling in
Markdown. Thematic breaks are omitted. Images reserve a fixed 16 × 16 inline
placeholder keyed by the image URL; alt text is discarded and the application
must draw the image using [placeholder geometry](#inline-placeholders).
Links use the same [link events](links-and-accessibility.md) as XML anchors.
Table and task-list extensions are not enabled; their syntax remains text.

### TMPro

The optional `Milestro.RichText.TMPro` module registers **TMPro** (`tmpro`)
automatically in the Editor and before scene load. It has no dependency on
TextMesh Pro or a third-party managed library.

```csharp
producer.richTextParserId = Milestro.RichText.TMPro.TMProRichTextParser.Id;
producer.content = "<color=#3366ff><b>Hello</b></color> <link=help>Help</link>";
```

Supported tags are `<b>`, `<i>`, `<u>`, `<s>`, `<strikethrough>`, `<sup>`, `<sub>`, `<color>`,
hex color shorthand, `<font>`, `<font-weight>`, `<size>`, `<cspace>`,
`<voffset>`, `<mark>`, `<uppercase>`, `<allcaps>`, `<lowercase>`,
`<smallcaps>`, `<nobr>`, `<align>`, `<br>`, `<link>`, `<noparse>`, `<alpha>`,
`<line-height>`, `<indent>`, `<line-indent>`, `<margin>`, `<margin-left>`,
`<margin-right>`, and `<width>`.
`<font>` selects a family name, not a TMP font asset or material.
`<align>` creates paragraph blocks. `<link=help>` reports `help` as the link
event's `Href`; it does not navigate automatically.

`<alpha=#80>` multiplies foreground opacity by 128/255 while preserving RGB;
it also multiplies any alpha already present in the text color.
`<line-height=85%>` or `<line-height=0.85em>` sets a run-level line-height
multiplier. This uses Milestro's font-size-based height model, rather than TMP
font asset metrics.

Paragraph layout tags accept `px`, `em`, and `%` and create paragraph blocks:
`<indent>` sets the left margin, `<line-indent>` sets first-line indentation,
and `<margin>` sets both horizontal margins. `<width>` limits the available
inline extent after margins, including the column's inline height in
`vertical-rl`. These block boundaries can differ from TMP's tag scope.

```csharp
producer.content = "<margin=1em><width=75%><line-indent=2em>" +
    "<line-height=120%><alpha=#80>Paragraph</alpha></line-height>" +
    "</line-indent></width></margin>";
```

Tag names are case-insensitive, and unclosed opening tags close at the end of
the input. Unsupported, invalid, or mismatched tags log a Unity warning and
remain visible as literal text; supported tags in their contents still parse.
`<noparse>` preserves its contents literally without warnings, including
entity text such as `&amp;`. This differs from Milestro Markup, where unknown
tags are errors and literal markup uses XML escaping. This module supports a
subset of TMP syntax; see the [module README](../../../apps/unity-plugins/Milestro.RichText.TMPro/README.md)
for supported units and limitations, and the [migration guide](coming-from-tmpro.md)
for differences in underlying capabilities.

## Custom parsers and payloads

Implement `IRichTextParser.Parse(string source, RichTextParseContext context)`
to return a `ParagraphPayload`, and expose it through an
`IRichTextParserProvider`. Register the provider before a text box needs its
id; register in the Editor as well if Inspector previews are needed.
The registry shares one parser instance per registration, so parsers must be
stateless across calls. Registering the same id replaces the provider and
discards its cached instance. `Unregister(id)` removes it.

Create a fresh `ParagraphPayloadBuilder` per parse. `AppendText` inherits
component styling when no `TextStyleState` is supplied; `BeginBlock` starts
a stacked paragraph without inserting a separator character. Use `\n` for
line breaks within a block, `BeginLink`/`EndLink` for non-nested clickable
ranges, and `AppendPlaceholder` for a positive, finite inline box. The builder
tracks UTF-16 offsets, counts each placeholder as one character, and closes
an open link on `Build()`. `RichTextParseContext` provides
`AllowViewportBlockMargins`, `Locale`, and an optional `Issues` collection for
non-fatal diagnostics.

Pass `context.Locale` to `new ParagraphPayloadBuilder(context.Locale)` when
case conversion should use the component locale. The builder applies
`TextStyleState.TextTransform`, tracks `NoWrap` ranges across styled text and
placeholders, and measures UTF-16 offsets after transformation.
`BeginBlock(ParagraphStyleState, TextStyleState)` copies paragraph settings and
captures the text style used to resolve indentation; `EndBlock()` ends the
current block. Block boundaries and `Build()` finalize pending no-wrap ranges.

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

- Use a Canvas `TextBox` with fixed height. Fixed rectangles and content-sized
  widths are supported; auto height is not. `TextInput` editing, SlimText, and
  the world-space components stay horizontal.
- The base direction stays LTR; RTL content is not supported. `textAlign`
  supports `Start`, `End`, `Center`, and `Justify`; `Left` and `Right` fall
  back to `Start`.
- `vertical-lr` is not available yet.
- Unsupported combinations degrade with an Editor diagnostic instead of
  failing silently.

Next: [layout and sizing](layout.md), [links and accessibility](links-and-accessibility.md),
or [TextMesh Pro comparison](coming-from-tmpro.md).

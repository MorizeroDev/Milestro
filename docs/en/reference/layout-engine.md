# Layout engine parameters

[English](layout-engine.md) | [简体中文](../../zh/reference/layout-engine.md)

This page describes the Unity-facing paragraph layout model and the parameters
that affect measurement, line breaking, and painting. For task-oriented recipes,
see [Layout and sizing](../guides/layout.md).

## Pipeline

A `TextBox` frame goes through these stages:

```text
content
  -> plain-text payload or optional rich-text parse
  -> paragraph construction and style resolution
  -> intrinsic measurement
  -> sizing (fixed/content/auto font)
  -> line layout at the resolved width
  -> Skia rasterization into a render texture
  -> uGUI mesh presentation (with optional color gradient)
```

The parser produces one or more paragraph blocks. Each block becomes a Skia
paragraph, and the blocks are stacked along the block axis (downward for
horizontal text, right to left for vertical text), with their margins and
`paragraphStyle.paragraphSpacing`. Rich-text `<p>` elements change that block
structure, but they do not create separate Unity objects.

## `TextBoxRenderTextureProducer`

The producer is the configuration surface. `TextBox` creates it automatically
on the same GameObject.

| Member | Type | Default | Meaning |
| --- | --- | --- | --- |
| `content` | `string` | `""` | Plain text or supported rich text |
| `richTextParserId` | `string?` | `null` | Registered parser id (`milestro_rich_text`, module-provided `markdown` or `tmpro`); `null` or empty renders literally |
| `margin` | `Margin` | all zero | Inset around the paragraph viewport |
| `textStyle` | `TextStyleSettings` | see below | Run-level typography |
| `paragraphStyle` | `ParagraphStyleSettings` | see below | Paragraph alignment and block behavior |
| `wrapMode` | `TextBoxWrapMode` | `Wrap` | `Wrap` or `NoWrap` |
| `textOverflow` | `TextOverflow` | `Clip` | `Clip`, `Ellipsis`, or `Overflow` |
| `enableAutoFontSize` | `bool` | `false` | Fit glyph size within fixed/content bounds |
| `fontSizeMin` | `float` | `12` | Minimum fitted size |
| `fontSizeMax` | `float` | `72` | Maximum fitted size |

Read-only runtime measurements include `contentWidth`, `contentHeight`,
`viewportWidth`, `viewportHeight`, `maxScrollX`, `maxScrollY`, and
`effectiveFontSize`.

## `TextStyleSettings`

| Member | Type | Default | Meaning |
| --- | --- | --- | --- |
| `fontFamilies` | family list | `{ "system-ui" }` | Ordered family declarations with fallback |
| `size` | `float` | `36` | Design font size |
| `weight` | `int` | `400` | Skia weight, normally 100..900 |
| `width` | `FontWidth` | `Normal` | Condensed through expanded axis |
| `slant` | `FontSlant` | `Upright` | `Upright`, `Italic`, or `Oblique` |
| `textColor` | `Color` | white | Base text color |
| `letterSpacing` | `float` | `0` | Extra points between glyphs |
| `wordSpacing` | `float` | `0` | Extra points at word-boundary spaces |
| `lineHeight` | `float` | `0` | Horizontal line-height or vertical column-thickness multiplier; `<= 0` uses natural metrics |
| `locale` | `string` | `zh-Hans` | Locale for shaping, breaks, and fallback |

The font-family list is resolved when the paragraph is built. `fontFamilies`
supports Milestro keywords and literal names; fallback happens inside Skia's
font collection, not by swapping a Unity font asset on the component.

## `ParagraphStyleSettings`

| Member | Type | Default | Meaning |
| --- | --- | --- | --- |
| `textAlign` | `TextAlign` | `Start` | `Left`, `Right`, `Center`, `Justify`, `Start`, or `End` |
| `textDirection` | `TextDirection` | `Ltr` | `Ltr` or `Rtl` |
| `writingMode` | `WritingMode` | `HorizontalTb` | `HorizontalTb` or `VerticalRl` |
| `singleLine` | `bool` | `false` | Forces a single-line paragraph |
| `ellipsisString` | `string` | `…` | Text used by ellipsis overflow |
| `paragraphSpacing` | `float` | `0` | Extra gap added between adjacent paragraph blocks |

`Start` and `End` follow the resolved text direction. `Left` and `Right` are
physical edges. `Justify` distributes space on justified lines; the final line
follows normal alignment behavior.

## Units and coordinates

Layout units are local UI points/pixels in the producer's render space. Margin
units are independent:

| `MarginUnit` | `horizontal-tb` | `vertical-rl` |
| --- | --- | --- |
| `Px` | Raw value | Raw value |
| `ContainerPercent` | Width for left/right, height for top/bottom | All four sides use the containing block width |
| `Vw` | 1% of container width | 1% of the root Canvas viewport width |
| `Vh` | 1% of container height | 1% of the root Canvas viewport height |
| `Em` | Current effective font size | Base font size for the paragraph |

`MarginValue.Auto` contributes zero when viewport size is resolved. In the
horizontal Canvas path, Auto sides can absorb positive leftover space during
component-level placement. In a fixed-size `vertical-rl` Canvas `TextBox`, Auto
sides are placed after native layout: X uses the complete physical column-box
width and Y uses the laid-out content bounds, then one common paragraph
translation is applied. Auto does not enlarge the clip or scroll range, and
overflow keeps the existing anchor.

Screen-space rasterization can choose a different texture scale from the UI
layout size. That scale affects raster quality and texture size, not the
logical paragraph geometry.

## Wrapping and line breaking

The hierarchy is:

1. `paragraphStyle.singleLine` can force one line.
2. Otherwise `wrapMode == Wrap` permits soft wrapping at the assigned width.
3. `wrapMode == NoWrap` keeps each paragraph on one line.
4. Explicit newlines and `<br>` always create hard breaks.

Within a wrapping paragraph, `<span style="white-space: nowrap">` suppresses
soft breaks inside that inline range while retaining break opportunities at its
boundaries. `white-space: normal` restores the default, including in nested
spans. Hard breaks still apply inside a `nowrap` range.

Inline `text-transform: none | lowercase | uppercase | smallcaps` applies ICU
case conversion while building the payload, using the component locale or an
inline `lang` override. Payload UTF-16 ranges refer to the transformed text,
whose length may differ from the source markup. Small caps uppercases original
lowercase letters and renders them at 80% of the effective font size.

Skia's paragraph implementation uses ICU line-break and bidi data. It keeps
grapheme clusters, shaped runs, and bidirectional ordering intact. A word or
unbreakable sequence wider than the available line can still overflow rather
than silently shrink the font.

`textOverflow` acts after line layout:

- `Clip` paints only the viewport.
- `Ellipsis` applies the paragraph ellipsis configuration.
- `Overflow` retains paint and hit geometry outside the viewport and enables a
  scrollable range when the component supports scrolling.

`Ellipsis` is not an auto-font-size trigger. If you need fitting, choose a fixed
box and enable auto font size rather than relying on ellipsis to shrink glyphs.

## Vertical writing and line height

`VerticalRl` is supported by native Paragraph and by fixed-size static Canvas
`TextBox`. Columns run top to bottom and advance from right to left. The content
height constrains the inline axis; changing viewport width affects clipping,
scroll range, and horizontal placement, not column wrapping.

`lineHeight` changes axis meaning with writing mode:

- Horizontal text uses it for line-box thickness along physical Y.
- Vertical text uses it for each resolved run's target column-box thickness
  along physical X. The target is `lineHeight * resolvedFontSize`; mixed font
  sizes therefore get different target thicknesses, and the final column takes
  the independent maximum of the two sides around the shared baseline.

`lineHeight == 0` or a finite negative value keeps the natural font/run metrics.
A positive multiplier of `1` means one em-sized target column box; it is not
necessarily identical to the natural column width. Non-finite values are errors.
The low-level `TextStyle.HalfLeading` flag selects equal-extra-space allocation;
the normal `TextStyleSettings` API does not add a separate HalfLeading field.

Changing only `lineHeight` does not resize glyphs or change inline advances,
break opportunities, or column count at the same content height. It changes
column boxes and physical placement. A small multiplier can therefore make
glyph ink extend beyond the column box and into neighboring columns; layout and
scroll geometry are not expanded to match that ink, and glyphs are not clipped
per column. Caller clip/overflow behavior remains separate.

For `VerticalRl`, `TextIndent` moves only the first source column along physical
Y. A positive value shifts it down; a negative value hangs it above inline
start. Soft-wrapped columns and columns after hard breaks are not indented. In
finite wrapping, a percentage resolves against the finite inline extent;
`NoWrap` and `singleLine` pass positive infinity, so the percentage resolves to
0. Intrinsic min/max inline extents exclude percentage indentation while final
layout resolves it, and absolute indentation contributes only to the first hard
fragment. `Start`/`End`/`Center` align the first column within the remaining
`max(0, inlineExtent - I)` area, while later columns use the full extent. When
`I >= inlineExtent`, the first column remains and its first layout unit
overflows atomically; negative indentation is not clamped. Painter, path,
visitor, range, and hit geometry all use the resulting first-column origin.
The native path rejects non-finite indent values and unrepresentable resolved
values. The fixed Canvas boundary additionally treats a finite resolved
indent as invalid when `em × font-size × scale`, `percentage × inlineExtent`,
or `inlineExtent - I` overflows the representable range; it lays out that
revision with indent `0`, keeps the configured value intact, and recovers when
a later height or font size is representable.

In `vertical-rl`, `Justify` consumes positive free space in finite soft-wrapped
columns that still have source units after them. Hard-break columns, the source
end column, `NoWrap`/`singleLine`, zero-height layouts, overflow, and columns
without a frozen whitespace or CJK/Latin opportunity stay at Start. The first
column uses `max(0, inlineExtent - I)`, later columns use the full extent, and
the same per-cluster shift feeds painting, paths, visitors, range queries,
link rectangles, and hit testing.

In `vertical-rl`, `Ellipsis` truncates the last allowed column when `maxLines`
and the inline extent are finite. The default `…` or `ellipsisString` is shaped
and placed as one atomic synthetic affix; complete source grapheme/shaped
clusters are removed from that column first. The affix owns no source range or
link, and hidden source produces no range boxes, link rectangles, or
content-size contribution. Paint, paths, visitors, range queries, hit testing,
metrics, and scrolling consume the final cluster/affix geometry. A legal
`singleLine + Ellipsis` uses the finite content height; `singleLine` without
ellipsis keeps the positive-infinite inline extent.

Tab follows HTML default whitespace handling and advances like one ASCII space
in vertical text while retaining its original source range. Soft hyphen and
explicit bidi controls are not painted; they add no glyph or advance and native
layout logs the skip rather than rejecting the paragraph. Their source bidi
levels are normalized to LTR so they cannot reorder vertical clusters, range
queries, or column breaking. The current text
model supports only `white-space: normal | nowrap`; it has no
preserved-whitespace or `tab-size` mode, so preserved-whitespace tab stops are
not implemented. Other missing glyphs use the normal shaping and fallback
path. Vertical punctuation glyph choice, including locale-specific `vert`
forms, remains font-driven.

Native `vertical-rl` backgrounds use each retained source run's final block
metrics and cluster advances, including alignment, indent and Justify shifts.
All backgrounds paint before glyphs, underline and line-through. They extend
only `inkBounds`; layout, intrinsic sizing, scrolling, source ranges, paths and
link rectangles retain their existing meaning. Hidden source and synthetic
ellipsis affixes produce no background. `SkPaint` and custom painter `PaintID`
are preserved; whole-paragraph background updates require a new vertical layout.
Unity markup exposes text-run backgrounds through `<mark>` and inline
`background-color`; see [text styles](../guides/text.md). This does not add an
Inspector field for per-run backgrounds.

Native `vertical-rl` text shadows reuse the final source/affix glyph blobs and
styles. Offsets remain physical X/Y and are applied before the sideways paint
rotation, so they do not rotate with the glyphs. All backgrounds, shadows,
glyphs, underline and line-through paint in that order. Shadows are paint-only:
they do not change layout, intrinsic sizing, scrolling, source ranges, paths,
link rectangles or `inkBounds`. Synthetic affixes use their own final style.
This extension adds no Unity TextBox shadow input or C ABI.

Native and Unity `vertical-rl` baseline shift supports the existing
`sup`/`sub`/`voffset`/`voffset-em` subset. After ordinary baseline alignment, the
final block offset is `-baselineShift` in physical X: superscript and positive
rich-text offsets move right, while subscript and negative offsets move left.
Upright and sideways runs use the same physical direction. Inline advances and
wrapping do not change; final visible run metrics can only grow the two column
sides. Paint, paths, visitors, ranges, links, sizing, and scrolling consume the
resulting geometry. Synthetic affixes use their own shift but still have no
background or decoration.

Explicit tate-chu-yoko ranges (`ParagraphBuilder::addTateChuYoko`, or
rich-text `text-combine-upright: all`) reshape a short source range as a
horizontal sub-paragraph and place it in the column as one atomic unit. The
inline advance is always one em of the group's font size. With the default
`squeeze` sizing, a group wider than one em is compressed along physical X by
a paint transform; `--milestro-text-combine-upright-sizing: grow` keeps the
natural width and grows the column thickness symmetrically instead. Ranges
must align to grapheme and shaped-cluster boundaries, use one uniform resolved
style, and cannot overlap hard breaks, placeholders, or Ruby base ranges;
horizontal paragraphs ignore the annotation. Group glyphs keep their real
source indices in visitors, range queries, paths, hit testing, and links, and
carry `Paragraph::kTateChuYoko_VisitorFlag`.

Orientation overrides (`ParagraphBuilder::addOrientationOverride`, or rich-text
`text-orientation: mixed | upright | sideways`) replace the ICU
`Vertical_Orientation` classification for a source range: each grapheme uses
the override value when present and falls back to the ICU class otherwise, and
runs split where the effective orientation changes. `upright` shapes every
grapheme upright like an ideograph; `sideways` shapes horizontally and rotates
the run 90 degrees clockwise, including CJK. Overrides must align to grapheme
boundaries and cannot overlap each other; they have no effect on placeholders
or zero-width controls, and horizontal paragraphs ignore them. Inside a
`sideways` range, tate-chu-yoko marks are ignored. Ruby annotations always use
`mixed` classification regardless of overrides on the base range. Column
thickness, wrapping, alignment, indent, justification, ellipsis, effects,
links, and scrolling follow the same rules as the equivalent mixed runs, and
overrides add no visitor flag.

## Content sizing

For `vertical-rl` with a fixed height, `TextBoxLayoutElement` can publish
`Fixed`, `MaxContent`, or `FitContent` width from the complete physical column
box while keeping a non-flow viewport. Unbreakable Latin segments overflow
their single column instead of breaking at arbitrary clusters. The sizing
rules below describe the existing horizontal path, which remains unchanged.

`TextBoxLayoutElement` implements `ILayoutElement` and reports intrinsic values
from the parsed paragraph and font metrics, before rasterization.

### Width modes

- `Fixed`: report no preferred width. The `RectTransform` or parent layout
  controls the width.
- `MaxContent`: request the full unwrapped content width.
- `FitContent`: request
  `max(min-content, min(max-content, parent available width))`.

`FitContent` does not itself choose line breaks. `wrapMode` remains the line
breaking control. With `NoWrap`, `FitContent` and `MaxContent` have the same
single-line result.

### Height modes

- `Fixed`: report no preferred height.
- `Auto`: report the full content height after wrapping at the width Unity
  assigns, including margins and rich-text block spacing.

For `vertical-rl`, only `heightMode = Fixed` participates in content-width
sizing. `heightMode = Auto` does not publish vertical content preferences and
falls back to the existing assigned-rectangle path.

Fixed axes report `-1` for the corresponding preferred value so a parent
`LayoutGroup` keeps control. Content-sized axes report `minWidth` and
`preferredWidth`; auto height reports `preferredHeight`.

The layout element skips content-sized ancestors when resolving the external
width for `FitContent`, so a self-sizing hierarchy does not consume its own
output as a constraint.

`TextBoxLayoutElement.forwardPointerScrollToParent` defaults to `true`. When a
layout element manages a text item inside a scroll view, unused wheel input can
continue to the parent scroll handler.

## Auto font size

Auto font size changes glyph size; content sizing changes box size. They can be
combined, but the interaction is asymmetric:

| Box configuration | Fitting behavior |
| --- | --- |
| Fully fixed width and height | Grow and shrink within the configured range |
| Any content-sized axis | At most the design size; content sizing can only shrink |
| `MaxContent`/`FitContent` width with auto height and no external cap | No effective fitting |
| Wrapped fixed width | Reflow first; shrink only when a fixed height cannot contain the result |
| `NoWrap` | Shrink against a fixed width and/or fixed height as needed |
| `Ellipsis` | No auto fitting; preserve ellipsis behavior |

The resolver measures at the effective font size, resolves width and height,
then computes auto height. It does not feed the final content-sized dimensions
back into font fitting, which prevents cross-frame growth loops. Fitting stops
at `fontSizeMin`; remaining overflow uses `Clip`, `Ellipsis`, or `Overflow`.

For SlimText's UTF-8 no-allocation path, fitting is intentionally not applied
per update; the configured size is retained.

## Rich-text block spacing

`paragraphStyle.paragraphSpacing` is the default extra gap between adjacent
paragraph blocks. Rich-text `<p>` blocks support `margin` shorthand and all four
physical longhands. Horizontal `margin-left` and `margin-right` reduce each
block's available line width and offset its content, links, and placeholders.
Vertical-rl `margin-top` and `margin-bottom` similarly reduce the inline extent.
Horizontal top/bottom and vertical-rl right/left are the block-start/end margins;
adjacent pairs collapse and the default spacing is added. Margins are included in
intrinsic measurement and content scrolling. The component-level `TextBox`
margin is separate and surrounds the whole viewport.

## SlimText layout

`SlimTextRenderTextureProducer` is a different, single-line layout path. Its
parameters are:

| Member | Type | Default |
| --- | --- | --- |
| `text` | `string` | `""` |
| `fontFamily` | `string` | platform-dependent; see below |
| `fontWeight` | `int` | `400` |
| `fontSize` | `float` | `24` |
| `color` | `Color` | white |
| `rectOffset` | `RectOffset` | zero |
| `horizontalAlign` | `SlimTextHorizontalAlign` | `Left` |
| `verticalAlign` | `SlimTextVerticalAlign` | `Top` |
| `fallbackToSystemFont` | `bool` | `true` |
| `widthMode` | `TextWidthMode` | `Fixed` |
| `heightMode` | `TextHeightMode` | `Fixed` |
| `layoutPriority` | `int` | `1` |
| `enableAutoFontSize`, `fontSizeMin`, `fontSizeMax` | auto-size group | disabled, 12..72 |

SlimText has no wrapping, rich text, links, Ruby, or paragraph direction. Its
natural min-content and max-content widths are equal because it is always one
line. See [SlimText](../guides/slim-text.md) for component usage.

New components use `Microsoft YaHei UI` in the Windows Editor, `PingFang SC`
in the macOS Editor, and `Noto Sans CJK SC` in the Linux Editor. Other compile
targets use `system-ui`. Serialized family values are retained; the requested
family still depends on registration and system fallback.

## Low-level paragraph API

The component layer wraps the lower-level `Milestro.Skia.TextLayout` API. A
direct paragraph can be built for custom rendering:

```csharp
using System.Collections.Generic;
using Milestro.Skia.TextLayout;
using Milestro.Model;
using UnityEngine;

using var textStyle = new TextStyle();
textStyle.FontSize = 32f;
textStyle.Color = Color.black;
textStyle.SetFontFamilies(new List<string> { "sans-serif" });

using var paragraphStyle = new ParagraphStyle();
paragraphStyle.TextAlign = TextAlign.Start;
paragraphStyle.SetTextStyle(textStyle);

using var builder = new ParagraphBuilder(paragraphStyle);
builder.PushStyle(textStyle);
builder.AddText("A paragraph built directly with Skia.");
builder.Pop();

using var paragraph = builder.Build();
paragraph.Layout(600f);

// Draw paragraph with a Canvas, or query Height / LongestLine /
// MinIntrinsicWidth / MaxIntrinsicWidth.
```

The wrapper objects own native memory. Dispose them with `using` or an explicit
`Dispose()` unless a component owns them.

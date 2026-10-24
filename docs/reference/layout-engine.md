# Layout engine parameters

This page describes the Unity-facing paragraph layout model and the parameters
that affect measurement, line breaking, and painting. For task-oriented recipes,
see [Layout and sizing](../guides/layout.md).

## Pipeline

A `TextBox` frame goes through these stages:

```text
content
  -> rich-text parse
  -> paragraph construction and style resolution
  -> intrinsic measurement
  -> sizing (fixed/content/auto font)
  -> line layout at the resolved width
  -> Skia rasterization into a render texture
  -> uGUI mesh presentation (with optional color gradient)
```

The parser produces one or more paragraph blocks. Each block becomes a Skia
paragraph, and the blocks are stacked vertically with their margins and
`paragraphStyle.paragraphSpacing`. Rich-text `<p>` elements change that block
structure, but they do not create separate Unity objects.

## `TextBoxRenderTextureProducer`

The producer is the configuration surface. `TextBox` creates it automatically
on the same GameObject.

| Member | Type | Default | Meaning |
| --- | --- | --- | --- |
| `content` | `string` | `""` | Plain text or supported rich text |
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
| `lineHeight` | `float` | `0` | Line-height multiplier; `<= 0` uses natural metrics |
| `locale` | `string` | `zh-Hans` | Locale for shaping, breaks, and fallback |

The font-family list is resolved when the paragraph is built. `fontFamilies`
supports Milestro keywords and literal names; fallback happens inside Skia's
font collection, not by swapping a Unity font asset on the component.

## `ParagraphStyleSettings`

| Member | Type | Default | Meaning |
| --- | --- | --- | --- |
| `textAlign` | `TextAlign` | `Start` | `Left`, `Right`, `Center`, `Justify`, `Start`, or `End` |
| `textDirection` | `TextDirection` | `Ltr` | `Ltr` or `Rtl` |
| `singleLine` | `bool` | `false` | Forces a single-line paragraph |
| `ellipsisString` | `string` | `…` | Text used by ellipsis overflow |
| `paragraphSpacing` | `float` | `0` | Extra gap added between adjacent paragraph blocks |

`Start` and `End` follow the resolved text direction. `Left` and `Right` are
physical edges. `Justify` distributes space on justified lines; the final line
follows normal alignment behavior.

## Units and coordinates

Layout units are local UI points/pixels in the producer's render space. Margin
units are independent:

| `MarginUnit` | Resolves against |
| --- | --- |
| `Px` | Raw value |
| `ContainerPercent` | Container width for horizontal sides, height for vertical sides |
| `Vw` | 1% of container width |
| `Vh` | 1% of container height |
| `Em` | Current effective font size |

`MarginValue.Auto` is serialized but currently resolves to zero.

Screen-space rasterization can choose a different texture scale from the UI
layout size. That scale affects raster quality and texture size, not the
logical paragraph geometry.

## Wrapping and line breaking

The hierarchy is:

1. `paragraphStyle.singleLine` can force one line.
2. Otherwise `wrapMode == Wrap` permits soft wrapping at the assigned width.
3. `wrapMode == NoWrap` keeps each paragraph on one line.
4. Explicit newlines and `<br>` always create hard breaks.

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

## Content sizing

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
paragraph blocks. Rich-text `<p>` blocks can add `margin-top` and
`margin-bottom`; collapsed adjacent margins and the default spacing are applied
together. It is included in intrinsic measurement, so it affects auto height.
The component-level `TextBox` margin is separate and surrounds the whole
viewport.

## SlimText layout

`SlimTextRenderTextureProducer` is a different, single-line layout path. Its
parameters are:

| Member | Type | Default |
| --- | --- | --- |
| `text` | `string` | `""` |
| `fontFamily` | `string` | `system-ui` |
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

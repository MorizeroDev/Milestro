# Layout and sizing

Milestro's paragraph layout is closer to CSS/Skia line layout than to a
TextMesh Pro component with one shared "auto size" switch. The useful mental
model is:

```text
margins -> available content box -> wrap -> alignment -> overflow/scroll
                                      \-> optional auto font size
```

The `RectTransform` supplies the outer size. The producer measures the text and
decides how large the rendered content is. An optional `TextBoxLayoutElement`
tells Unity's layout system how large the box should be.

## Choose a sizing recipe

### Fixed box

Use this for dialogue boxes, HUD panels, and anything with a designed size.

1. Set the `RectTransform` size.
2. Leave `TextBoxLayoutElement` off.
3. Set `wrapMode` to `Wrap` for paragraph text.
4. Choose `textOverflow`:
   - `Clip`: clip at the box.
   - `Ellipsis`: add the configured ellipsis string.
   - `Overflow`: draw outside the box; use this when the producer should remain
     scrollable.

```csharp
using Milestro.Components.Internal;
using Milestro.Model;

var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();
producer.wrapMode = TextBoxWrapMode.Wrap;
producer.textOverflow = TextOverflow.Clip;
producer.paragraphStyle.textAlign = TextAlign.Start;
```

### Box that grows with its content

Add `TextBoxLayoutElement`, choose content-sized axes, and let Unity's
`ContentSizeFitter` or a parent `LayoutGroup` apply the preferred size.

```csharp
using Milestro.Components;
using Milestro.Model;
using UnityEngine.UI;

var layout = textBox.gameObject.AddComponent<TextBoxLayoutElement>();
layout.widthMode = TextWidthMode.FitContent;
layout.heightMode = TextHeightMode.Auto;

var fitter = textBox.gameObject.AddComponent<ContentSizeFitter>();
fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
```

The width modes are:

| Mode | Preferred width |
| --- | --- |
| `Fixed` | No preference; the assigned width controls. |
| `MaxContent` | The complete unwrapped content width. |
| `FitContent` | `max(min-content, min(max-content, parent width))`. |

The height modes are:

| Mode | Preferred height |
| --- | --- |
| `Fixed` | No preference; the assigned height controls. |
| `Auto` | Full wrapped content height, including margins and block spacing. |

With `NoWrap`, `FitContent` and `MaxContent` produce the same single-line width.
With `Wrap`, `FitContent` can become narrower than `MaxContent` when the parent
is narrow enough.

For a content-sized box, leave `ContentSizeFitter.verticalFit` and
`horizontalFit` as `Unconstrained` for axes that should stay fixed. A parent
`LayoutGroup` can consume the `ILayoutElement` values without a fitter.

## Wrapping, single-line, and overflow

`TextBox` has two independent controls that are easy to confuse:

- `producer.wrapMode` chooses whether line breaking is allowed.
- `producer.paragraphStyle.singleLine` forces a single-line paragraph.

If `singleLine` is true, it overrides wrapping. `textOverflow` then decides how
the resulting content is presented:

- `Clip` hides pixels outside the viewport.
- `Ellipsis` asks the paragraph to ellipsize. It uses
  `paragraphStyle.ellipsisString` and does not enable auto font size.
- `Overflow` lets content paint outside the viewport and gives `TextBox` a
  scrollable content range.

`TextBox` can scroll a fixed viewport with the mouse wheel. Use
`TextBoxScrollbar` to bind a Unity `Scrollbar` to the normalized position, or
read `GetScrollPercentX/Y()` yourself. See [Scrolling](scrolling.md).

## Margins

`producer.margin` is an inset around the paragraph. Each side is a `MarginValue`
with a numeric value and a unit:

- `Px`
- `ContainerPercent`
- `Vw`
- `Vh`
- `Em`

```csharp
using Milestro.Model;

var margin = producer.margin;
margin.Left.Value = 24f;
margin.Left.Unit = MarginUnit.Px;
margin.Right.Value = 24f;
margin.Right.Unit = MarginUnit.Px;
margin.Top.Value = 1.5f;
margin.Top.Unit = MarginUnit.Em;
margin.Bottom.Value = 12f;
margin.Bottom.Unit = MarginUnit.Px;
```

`em` resolves against the effective font size. Percentage and viewport units
resolve against the current container/available size. `Auto` is accepted in the
serialized model but currently resolves to zero; do not use it as a centering
mechanism.

Rich-text `<p>` blocks can also add `margin-top` and `margin-bottom`. Those are
between blocks, not around the whole `TextBox`.

## Auto font size

Auto font size changes the glyph size to fit the box. It is not the same as
content sizing:

- Content sizing changes the box.
- Auto font size changes the glyphs inside the box.

```csharp
producer.enableAutoFontSize = true;
producer.fontSizeMin = 12f;
producer.fontSizeMax = 48f;
```

The configured `textStyle.size` is the design size. With a fully fixed width
and height, fitting may grow or shrink within the configured min/max range. If
either axis is content-sized, fitting is capped at the design size, so content
sizing can only shrink the glyphs. A wrapped fixed-width box reflows first and
shrinks only when the fixed height cannot contain the result. The detailed
rules are in
[Layout engine parameters](../reference/layout-engine.md#auto-font-size).

Common recipe for a fixed panel title:

```csharp
producer.textStyle.size = 48f;
producer.enableAutoFontSize = true;
producer.fontSizeMin = 24f;
producer.fontSizeMax = 48f;
producer.wrapMode = TextBoxWrapMode.Wrap;
producer.textOverflow = TextOverflow.Clip;
```

Do not use `Ellipsis` and auto font size together when you expect fitting; the
ellipsis path keeps its existing behavior instead of enabling fitting.

## Color gradients

`TextBox` and `TextInput` can tint the rendered texture with a `TextGradient`.
The gradient is baked into the display mesh, so changing it does not rerun
paragraph layout or rerasterize the text.

```csharp
using Milestro.Model;
using UnityEngine;

var stops = new Gradient();
stops.SetKeys(
    new[]
    {
        new GradientColorKey(Color.cyan, 0f),
        new GradientColorKey(Color.magenta, 1f),
    },
    new[]
    {
        new GradientAlphaKey(1f, 0f),
        new GradientAlphaKey(1f, 1f),
    });

textBox.enableColorGradient = true;
textBox.colorGradient = TextGradient.Linear(stops, 90f);
```

Modes are `FourCorners`, `Linear`, and `Radial`. `Linear` and `Radial` use
`MeshResolution` (default 16, clamped to 2..64). Gradients map to the display
quad, not to the ink bounds of the glyphs. The gradient multiplies with
`Graphic.color`.

In the editor, create reusable presets from the context menu under
**Create** → **Milestro** → **Text Gradient**, then assign the preset.

## What to change when text looks wrong

| Symptom | First settings to inspect |
| --- | --- |
| Text too small | `textStyle.size`, `enableAutoFontSize`, layout modes |
| Text clipped | `textOverflow`, box size, margins |
| Text does not wrap | `wrapMode`, `paragraphStyle.singleLine`, available width |
| Text overflows the design | `textOverflow`, `enableAutoFontSize`, parent layout |
| Layout group collapses the box | `TextBoxLayoutElement` modes and fitter settings |

For the parameter-by-parameter rules, including multi-line measurement and
auto-size edge cases, see
[Layout engine parameters](../reference/layout-engine.md).

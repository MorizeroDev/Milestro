# SlimText

SlimText is Milestro's lightweight, **always single-line** text path. It is
useful for names, counters, button captions, damage numbers, and other short
labels that do not need paragraph layout.

It is not a `TextBox` mode. It is a separate producer/render path with a smaller
parameter set:

| Feature | `TextBox` | SlimText |
| --- | --- | --- |
| Multi-line wrapping | Yes | No |
| Rich-text parsing | Yes | No |
| Links and Ruby | Yes | No |
| Paragraph alignment/direction | Yes | Horizontal and vertical alignment only |
| Font family list | Yes | One `fontFamily` string |
| Auto font size | Yes | Yes |
| Intrinsic layout sizing | `TextBoxLayoutElement` | Implements `ILayoutElement` directly |
| Cheap UTF-8 update path | No | Yes, on `WorldSpaceSlimText` and the producer |

Use [TextBox](text.md) when the content is a sentence, a paragraph, or needs
semantic formatting. Use SlimText when one line is enough.

## Canvas SlimText

`Milestro.Components.CanvasSlimText` is the UI version. It uses a `RawImage` and
is driven by a `SlimTextRenderTextureProducer`; the required components are
added for you when you add **Milestro** → **Canvas Slim Text**.

### Create one in the scene

1. Create a UI GameObject under a Canvas.
2. Add **Milestro** → **Canvas Slim Text**.
3. Give its `RectTransform` a non-zero size.
4. Set the text and font fields in the inspector.

### Change it from code

```csharp
using Milestro.Components;
using UnityEngine;

public sealed class ScoreLabel : MonoBehaviour
{
    [SerializeField] private CanvasSlimText label = null;

    private void UpdateScore(int score)
    {
        label.Text = $"Score: {score}";
        label.TextColor = Color.white;
    }
}
```

`CanvasSlimText.Text` and `TextColor` are convenience wrappers over the
producer. For more control, access the producer with
`GetComponent<SlimTextRenderTextureProducer>()`:

```csharp
using Milestro.Components.Internal;
using Milestro.Model;
using UnityEngine;

var producer = label.GetComponent<SlimTextRenderTextureProducer>();
producer.text = "Wave 3";
producer.fontFamily = "Inter";
producer.fontSize = 32f;
producer.fontWeight = FontWeight.SemiBold;
producer.color = Color.yellow;
producer.horizontalAlign = SlimTextHorizontalAlign.Center;
producer.verticalAlign = SlimTextVerticalAlign.Center;
producer.rectOffset = new RectOffset(8, 8, 2, 2);
producer.fallbackToSystemFont = true;
```

`SlimTextRenderTextureProducer.fallbackToSystemFont` controls system fallback
for a missing family. `fontFamily` is a single family declaration, not the
ordered list used by `TextStyleSettings.fontFamilies`.

### Sizing and auto font size

SlimText never wraps. Its `widthMode` and `heightMode` have these effects:

- `Fixed`: use the assigned `RectTransform` width or height.
- `MaxContent` / `FitContent` width: request the natural single-line width.
- `Auto` height: request the full line height, including `rectOffset`.

Because SlimText is single-line, `MaxContent` and `FitContent` produce the same
natural width. Auto font size can still shrink against fixed bounds or a
FitContent parent constraint:

```csharp
producer.widthMode = TextWidthMode.FitContent;
producer.heightMode = TextHeightMode.Auto;
producer.enableAutoFontSize = true;
producer.fontSizeMin = 12f;
producer.fontSizeMax = 32f;
```

The producer implements `ILayoutElement`, so a parent `LayoutGroup` can consume
the reported size directly. `layoutPriority` defaults to `1`.

## WorldSpace SlimText

`Milestro.Components.WorldSpaceSlimText` presents the same single-line text as a
world-space mesh. Add **Milestro** → **World Space Slim Text** to a GameObject
with a `RectTransform`.

The world-space wrapper owns the producer, mesh presenter, mesh filter, and
renderer. Its important settings are:

| Setting | Meaning |
| --- | --- |
| `text` | The string to render |
| `fontFamily`, `fontSize`, `fontWeight`, `color` | Basic text style |
| `textureSizePixels` | Render-target resolution |
| `pixelsPerUnit` | World-space scale |
| `rectOffset` | Padding inside the generated quad |
| `horizontalAlign`, `verticalAlign` | Position inside the quad |
| `widthMode`, `heightMode` | Fixed or content-driven size |
| `enableAutoFontSize`, `fontSizeMin`, `fontSizeMax` | Fit glyphs to bounds |
| `material`, `texturePropertyName` | Material override and texture property |
| `disableRendererWhenNoOutput` | Hide the object when no texture is available |

```csharp
using Milestro.Components;
using UnityEngine;

public sealed class WorldLabel : MonoBehaviour
{
    [SerializeField] private WorldSpaceSlimText label = null;

    private void Start()
    {
        label.text = "World label";
        label.fontSize = 24f;
        label.color = Color.white;
        label.textureSizePixels = new Vector2Int(512, 64);
        label.pixelsPerUnit = 100f;
    }
}
```

`WorldSpaceSlimText` is intended for labels. For a multi-line world-space
paragraph, use `WorldSpaceTextBox` instead; see [World-space text](world-space.md).

## UTF-8 buffer updates

For rapidly changing labels, `WorldSpaceSlimText` can accept UTF-8 bytes
directly. The producer copies them into its reusable storage; it does not
retain the array or allocate an intermediate managed string. The caller still
supplies and formats the bytes, so reuse both the buffer and any formatter on
hot paths.

```csharp
using System.Text;
using UnityEngine;

public sealed class CountLabel : MonoBehaviour
{
    [SerializeField] private WorldSpaceSlimText label = null;

    private readonly byte[] buffer = new byte[128];

    private void SetCount(int count)
    {
        var text = $"Count: {count}";
        var length = Encoding.UTF8.GetBytes(text, 0, text.Length, buffer, 0);
        label.SetTextUtf8NoAlloc(buffer, 0, length);
    }
}
```

`PrepareTextUtf8NoAlloc(capacity)` can be called once to reserve capacity. The
buffer overload validates the range and copies the bytes into the producer's
reusable storage. String interpolation still allocates in the example above; if
that matters, write the number into the byte buffer with an allocation-free
formatter such as `Utf8Formatter`.

## What SlimText does not do

- It does not parse `<b>`, `<a>`, Ruby, or other rich-text tags.
- It does not wrap or scroll.
- It does not expose paragraph direction or `TextAlign.Justify`.
- It does not use a family list; use one family plus system fallback.

If you need any of those features, switch to `TextBox`.

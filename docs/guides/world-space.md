# World-space text

Milestro has two world-space presentation paths:

- `WorldSpaceTextBox` for paragraph text, rich text, links, sizing, and the
  same producer settings as UI `TextBox`.
- `WorldSpaceSlimText` for a single-line, lightweight label.

Both render into a texture and present it with a generated mesh. They do not
use a Canvas.

## WorldSpaceTextBox

Add **Milestro** → **World Space Text Box** to a GameObject. The component
creates the required `RectTransform`, producer, mesh presenter, mesh filter, and
mesh renderer.

The producer is the same `TextBoxRenderTextureProducer` used by UI text:

```csharp
using Milestro.Components.Internal;
using UnityEngine;

public sealed class WorldParagraph : MonoBehaviour
{
    [SerializeField] private WorldSpaceTextBox label = null;

    private void Start()
    {
        var producer = label.GetComponent<TextBoxRenderTextureProducer>();
        producer.content = "A paragraph rendered in world space.";
        producer.textStyle.size = 32f;
        producer.textStyle.textColor = Color.white;
        producer.wrapMode = Milestro.Model.TextBoxWrapMode.Wrap;
    }
}
```

The world-space wrapper adds these presentation settings:

| Setting | Default | Meaning |
| --- | --- | --- |
| `textureSizePixels` | `1024 × 256` | Render-target resolution |
| `pixelsPerUnit` | `100` | World-space scale of the generated quad |
| `material` | default resource | Material override |
| `texturePropertyName` | `_MainTex` | Texture property sampled by the material |
| `disableRendererWhenNoOutput` | `false` | Hide the renderer when there is no output |

The wrapper keeps the `RectTransform` size synchronized with
`textureSizePixels`, so changing the texture size changes the generated quad's
layout size. Use `pixelsPerUnit` to fit it into the scene's scale.

The world-space wrapper sizes its quad from `textureSizePixels` and
`pixelsPerUnit`; it does not use the UI-only `TextBoxLayoutElement`. Choose a
texture size and world scale explicitly when the label must stay stable in a
3D layout.

## Batching world-space paragraphs

Add `Milestro.Components.WorldSpaceTextBatch` to a common parent of multiple
`WorldSpaceTextBox` objects. The batch component:

- combines labels that share a material and output atlas page;
- stores each label's UV rectangle in the combined mesh;
- rebuilds the combined mesh when texture, UV, mesh, or transform changes;
- leaves singleton output and labels on distinct pages as separate renderers.

Enable the render-surface atlas before creating the text for the largest
reduction:

```csharp
var surfaces = Milestro.Configuration.MilestroConfiguration.Configuration.RenderSurface;
surfaces.EnableAtlas = true;
surfaces.AtlasPageSize = 2048;
```

Pixel-only text updates reuse the combined mesh. Per-label
`MaterialPropertyBlock` values are not merged; put shared visual settings in the
source material. Disabling the batch component restores the original
renderers.

## WorldSpaceSlimText

For a single-line label, `WorldSpaceSlimText` avoids the paragraph producer and
uses the SlimText path. It has no wrapping, rich text, links, or Ruby.

```csharp
using Milestro.Components;
using UnityEngine;

public sealed class WorldName : MonoBehaviour
{
    [SerializeField] private WorldSpaceSlimText label = null;

    private void Start()
    {
        label.text = "Unit 04";
        label.fontSize = 24f;
        label.color = Color.white;
        label.textureSizePixels = new Vector2Int(512, 64);
        label.pixelsPerUnit = 100f;
    }
}
```

Its single-line size and alignment settings behave like `CanvasSlimText`. See
[SlimText](slim-text.md) for the full parameter list.

## Which one to use

| Content | Component |
| --- | --- |
| A paragraph, wrapped sentence, rich text, links, Ruby | `WorldSpaceTextBox` |
| Many world labels that can share an atlas | `WorldSpaceTextBox` + `WorldSpaceTextBatch` |
| A short single-line name or counter | `WorldSpaceSlimText` |
| UI text on a Canvas | `TextBox` |
| A short single-line UI label | `CanvasSlimText` |

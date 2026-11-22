# Rendering, batching, and backends

[English](rendering.md) | [简体中文](../../zh/reference/rendering.md)

By default, Milestro renders text into a `RenderTexture` and displays it in uGUI
or a world-space mesh. Some Canvas text can also use the glyph mesh option below.
This page covers rendering modes, batching, and their configuration.

## Experimental glyph meshes

Texture rendering remains the default. To try shared glyph meshes for Canvas
`TextBox` and string-based `CanvasSlimText`, set this before creating text:

```csharp
Milestro.Configuration.MilestroConfiguration.Configuration.RenderSurface
    .EnableTextGlyphMesh = true;
```

The current path requires Gamma color space and white source text. You can
color that output with a Graphic RGB tint, but setting a nonwhite source text
color uses texture rendering instead.

### When does it use textures instead?

Fallback applies to the whole component, including when only one text run
needs an unsupported feature.

| Condition | Result |
| --- | --- |
| The option is off, or the component is world-space text, `TextInput`, or uses SlimText's UTF-8 update path | Keeps texture rendering |
| Linear color space, nonwhite source text, or a color font such as color emoji | Uses texture rendering |
| Vertical text, inline placeholders, backgrounds, shadows, underline, or strikethrough | Uses texture rendering |
| A vertex gradient or custom presentation material | Uses texture rendering |
| Graphic alpha, CanvasRenderer alpha, or inherited CanvasGroup alpha is below one | Uses texture rendering |
| An ancestor `RectMask2D` masks the text and has nonzero softness | Uses texture rendering |
| `CanvasSlimText` has Raycast Target enabled, or has a `Mask` or `RectMask2D` on its own object | Uses texture rendering |
| The output exceeds the glyph limit, cannot fit one glyph atlas page, or the device lacks the required shader or Alpha8 support | Uses texture rendering |

A hard-edged ancestor `RectMask2D` does not itself force fallback. Source text
opacity and Graphic opacity are different: white source text can have alpha,
while fading the whole Graphic or CanvasGroup selects texture rendering.

An output must fit one page. By default, the atlas allows two 2048 × 2048 pages
and up to 2048 glyphs per component. Capacity limits also select texture rendering.
See [configuration](configuration.md#render-surface) for the settings. Keep this
option experimental and check appearance and performance on your target platform.
Implementation and test details are in the [developer documentation](../development/text-pipeline.md).

### Does fallback create a separate texture?

Usually, compatible components still share the render-texture atlas when
`EnableAtlas` is enabled. They use separate textures when that atlas is disabled,
the requested region is too large, or atlas allocation is rejected. Falling
back from glyph meshes therefore does not by itself mean a separate texture
or Canvas for each label.

## Draw call batching

`TextBox`, `TextInput`, and ordinary SlimText render targets share render-texture
atlas pages by default. Their public output is a texture plus a UV rectangle;
custom presenters must use both rather than sampling the whole texture.
Explicit `UnityAutoRenderTextureSurface` / `UnitySkiaRenderTextureSurface`
instances and SlimText's zero-allocation submission path remain standalone.

Configure the atlas before creating text:

```csharp
var surfaces = Milestro.Configuration.MilestroConfiguration.Configuration.RenderSurface;
surfaces.EnableAtlas = true;
surfaces.AtlasPageSize = 2048;
```

Each RGBA page costs approximately `AtlasPageSize * AtlasPageSize * 4` bytes
(16 MiB at the default size, 4 MiB at 1024). Smaller pages reduce the minimum
allocation for sparse interfaces; larger pages fit more labels into a shared
texture. Padding and fragmentation consume additional space.

Oversized regions, invalid page sizes, or rejected allocations fall back to
standalone textures under the existing raster policy. Set `EnableAtlas = false`
to disable sharing.

Compatible uGUI graphics can batch because they sample the same texture.
Canvas boundaries, materials, masks, clipping, and transparent ordering can
still split batches. The atlas also combines page updates and skips unchanged
regions to reduce submission overhead. Sharing a texture does not guarantee one
draw call for an arbitrary scene. World-space mesh presenters are not GPU-instanced.

When many standalone textures update in one frame, some updates can take later
frames to complete.

## Render texture backends

`UnityAutoRenderTextureSurface` selects a backend from Unity's active graphics
device: Metal, Direct3D 12, Direct3D 11, Vulkan, OpenGLES3, or OpenGLCore. It
does not change Unity's graphics API and does not fall back from Vulkan to GLES
when a native backend is unavailable.

### Direct3D 11

The Direct3D 11 backend rasterizes text on the CPU, then uploads the full texture
to the GPU.

### Vulkan

Vulkan has two per-target implementations:

- `UnitySkiaVulkanBackend.Direct` (default): Skia Vulkan draws directly into the
  Unity target.
- `UnitySkiaVulkanBackend.StagingCopy`: Skia rasterizes on the CPU, then copies
  and uploads the full texture to the GPU.

Set `RenderSurface.VulkanBackend` before creating component render surfaces, or
set `VulkanBackend` on an explicit `UnitySkiaRenderTextureDescriptor`. The
selection is fixed for each target's lifetime; changing the configuration is not
a live switch for existing surfaces.

Android Vulkan requires API 24 or newer. Desktop Vulkan and desktop OpenGL are
experimental build options; see the [build guide](../development/build.md).

## Compatibility checks

- MSAA render textures are not supported.
- The GLES backend rejects BGRA32; use Auto/RGBA32.
- Update managed code and the native plugin together from the same revision.
  See the [build guide](../development/build.md) for source builds.

## Surface budgets

`RenderSurface` configuration controls raster scale, texture edge limits, and
per-surface/global byte budgets. The defaults are listed in
[Runtime configuration](configuration.md#render-surface). The producer keeps
standalone surfaces within those budgets and uses the shared atlas budget rather
than a second texture pool.

## Choosing a performance strategy

| Situation | Recommendation |
| --- | --- |
| Many UI labels sharing a material | Enable the render atlas |
| Static text | Leave it unchanged; atlas pages skip redundant submissions |
| Frequently changing single-line labels | Use `CanvasSlimText` or `WorldSpaceSlimText` |
| Many world-space labels | Combine `WorldSpaceTextBox` with `WorldSpaceTextBatch` |
| Memory-constrained UI | Lower `AtlasPageSize` or disable the atlas |
| Custom rendering path | Use the low-level `UnitySkia*` surfaces and dispose them explicitly |

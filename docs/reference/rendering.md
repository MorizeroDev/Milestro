# Rendering, batching, and backends

Milestro text components render into a `RenderTexture` through a native Skia
surface, then present that texture in uGUI or a world-space mesh. This page
documents the render-surface controls that are separate from paragraph layout.

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
texture. Padding and fragmentation consume additional space, and live regions
are never repacked. Empty pages are released after pending draws.

Oversized regions, invalid page sizes, or rejected allocations fall back to
standalone textures under the existing raster policy. Set `EnableAtlas = false`
to disable sharing. Color spaces, device epochs, and configuration changes never
share a page.

There are two separate reductions:

- Compatible uGUI graphics can batch because they sample the same texture.
  Canvas boundaries, materials, masks, clipping, and transparent ordering can
  still split batches. Sharing a texture does not guarantee one draw call for an
  arbitrary scene, and world-space mesh presenters are not GPU-instanced.
- Dirty regions are collected and sent as one native submission per dirty page
  per flush, with one submission in flight per page. Unchanged regions retain
  their pixels; a static page produces no new native submissions. This reduces
  render-target switches and submission overhead, not necessarily every Skia
  glyph draw.

Native submission dispatch uses a bounded backlog per render route (currently
256 submissions). This limit is backpressure against a stalled render drain,
not a guarantee that every independent target can enter the queue in one frame.
When a frame submits more targets than a route can accept, excess enqueues
return a deferred, retryable status instead of growing the backlog or dropping
the update. The managed layer leaves the producer dirty and retries after the
route drains, so a large standalone burst can spread across frames. Atlas
batching reduces the number of submissions by sharing one submission per dirty
page; it does not change the per-route limit.

Region updates clear one pixel of transparent padding, clip to the content, and
apply that target's raster scale. Draw snapshots and retired regions remain alive
until their pending submission completes. The atlas uses the existing surface
budget and device-epoch recovery rather than a separate texture budget.

## Render texture backends

`UnityAutoRenderTextureSurface` selects a backend from Unity's active graphics
device: Metal, Direct3D 12, Direct3D 11, Vulkan, OpenGLES3, or OpenGLCore. It
does not change Unity's graphics API and does not fall back from Vulkan to GLES
when a native backend is unavailable.

### Direct3D 11

The Direct3D 11 backend rasterizes on the CPU into a persistent per-target Skia
surface and uploads pixels with `UpdateSubresource`. The cached surface preserves
incremental atlas updates and is released with its Unity target or on a device
reset. It performs full-surface CPU uploads; it is not GPU text rasterization.

### Vulkan

Vulkan has two per-target implementations:

- `UnitySkiaVulkanBackend.Direct` (default): Skia Vulkan draws directly into the
  Unity target using paired prepare/submit events. Queue and shutdown lifetime
  risks still require device validation.
- `UnitySkiaVulkanBackend.StagingCopy`: Skia rasterizes on the CPU and records a
  buffer-to-image upload. A bounded three-slot ring retires storage using Unity
  safe-frame progress. This path performs full-surface CPU copies/uploads.

Set `RenderSurface.VulkanBackend` before creating component render surfaces, or
set `VulkanBackend` on an explicit `UnitySkiaRenderTextureDescriptor`. The
selection is fixed for each target's lifetime; changing the configuration is not
a live switch for existing surfaces.

Android Vulkan requires `MILESTRO_ENABLE_ANDROID_VULKAN_RENDER=ON` (the default),
an API 24+ target, and the NDK Vulkan library. An API 23 GLES-only build
explicitly disables that option. Desktop Vulkan and desktop OpenGL remain behind
`MILESTRO_ENABLE_DESKTOP_VULKAN_RENDER` and
`MILESTRO_ENABLE_DESKTOP_OPENGL_RENDER`.

## Compatibility checks

- MSAA render targets are rejected by descriptor normalization.
- The GLES backend rejects BGRA32; use Auto/RGBA32.
- Deploy managed bindings and the native plugin from the same build. Atlas
  region commands require render payload ABI 4. Native binaries with a
  different payload ABI are rejected even when atlas sharing is disabled.
- Rebuild every shipping platform's native plugin when updating managed
  bindings.
- Enable `MILESTRO_RENDER_DEBUG_LOG` at configure time for verbose native and
  managed render-flow logging.

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

# Text pipeline implementation

[English](text-pipeline.md) | [简体中文](../../zh/development/text-pipeline.md)

This page describes the changes to preparation, caches, and glyph output.
For component settings and fallback conditions, use the
[rendering reference](../reference/rendering.md#experimental-glyph-meshes).

## Preparation and layout

`TextPreparationScheduler` collects dirty Canvas producers and processes them
at uGUI `PostLayout`. Repeated setters share one pending preparation. Explicit
refreshes consume pending work, while changes during preparation stay queued.
Disabling or destroying a producer cancels its pending work. World-space and
standalone producers retain their synchronous update paths.

SlimText retains native glyph positions and metrics so measurement and texture
drawing can consume the same layout. Its compact atlas view is owner-versioned
and consumed synchronously; published mesh outputs own their buffers.
Paragraph retains an owned export captured from actual paragraph painting.
Export does not perform an additional shaping or layout pass.

Dynamic Paragraph content still builds and lays out. The no-wrap natural-width
probe and final layout use different inputs and both remain necessary.
Width queries use a UTF-16 range extent; native UTF-8 inputs count with the same
SkUnicode conversion semantics as editing. Preserve supplementary characters,
malformed input, trailing whitespace, and placeholder semantics when changing this code.

Base text-style projection copies only the fields the rich-text path previously
preserved. It is not a complete style clone. Block and payload overrides still
apply afterward, including indentation, ellipsis, and vertical settings.

## Font configuration and invalidation

Managed synchronization reads the actual font configuration on each call.
An empty configuration resets user mappings. A nonempty configuration is sent
through `MilestroSkiaFontRegistryReplaceFontFamilyKeywordMappings` as a complete
table; native code compares it with the current table before replacing it.

Normalized keyword insertion order does not matter. Token kind, value, and
sequence do matter, and an empty override differs from a missing entry.
Equivalent tables preserve caches. Changed tables, individual native setters,
explicit cache clears, and successful font registration invalidate them.
Resetting an already empty table preserves caches too.

Native owns the effective table. Direct native changes and a new font collection
are therefore corrected by the next managed synchronization without a managed
configuration mirror. The existing mutable dictionary and returned lists do
not support mutation racing synchronization. Marshaling and comparison still
have a cost even when the table is unchanged.

Retaining caches trades memory for CPU work. Paragraph cache entry count is
bounded, but FontCollection face and variation caches have no explicit capacity.
Entry limits do not establish a byte bound, and Unity or managed memory counters
do not measure the full native cache footprint.

## Glyph meshes and texture presentation

`TextGlyphAtlas` shares Alpha8 pages between supported CanvasSlimText and TextBox
outputs. SkParagraph remains responsible for shaping, fallback, and line breaks.
Glyph keys include font identity, raster settings, scale, and quantized subpixel
phase. Depending on the font flags, phases use quarter-pixel steps; baseline
snapping is retained. Warming characters alone does not warm every glyph/phase pair.

Pages retain stable UVs while referenced. An output must fit one page, and pages
with live output references are not recycled. Mesh buffers are reused where
possible; this does not make dynamic layout or Canvas updates allocation-free.

Default Canvas texture presentation uses `Resources/Milestro/TextPremultiplied.shader`
to avoid applying edge coverage twice. This can change text-edge appearance
relative to older versions. Custom materials keep their own behavior.
The glyph shader is `Resources/Milestro/TextGlyphMesh.shader`.

## Validation status

Controlled short-text comparisons show lower CPU frame cost. Overall performance
acceptance remains unsuccessful because a small texture case exceeded its
completion-rate regression budget. Final convergence and sampled CPU latency
passed; the cause of that completion-rate difference and long-term native memory
bounds remain unresolved. These results do not establish GPU or screen-display gains.

Further optimization is paused, and glyph meshes remain off by default.
The [validation guide](text-pipeline-validation.md) describes the retained tools;
external focus observers and analysis scripts are not included in the repository.

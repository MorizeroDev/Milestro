# Vertical text: research and implementation status

Status: the Phase 1 native feasibility experiment and the Milestro-owned native
TTB shaping API are implemented and tested. Vertical paragraph layout, C ABI
exposure, generated Unity bindings for vertical text, and Unity vertical text
are not implemented. Paragraph and managed-surface API names and phase
boundaries below remain proposals, not available features or a committed
release schedule.

Research date: 2026-10-25. This document records source inspection and review of
the official references listed at the end. A native CPU prototype has been
verified, including the native TTB shaping boundary; cross-platform verification
and performance measurement have not been completed.

## Findings

Milestro's dependencies provide the essential building blocks for East Asian
vertical text: HarfBuzz can shape top-to-bottom runs and obtain vertical font
metrics, ICU exposes Unicode character orientation, and Skia can paint positioned
and transformed glyphs. The main integration work is at the boundary between
SkShaper, SkParagraph, and the managed paragraph sizing and query contracts.

There is already a general OpenType feature path, including managed
`TextStyle.SetFontFeature()` and rich-text `font-feature-settings`. Passing
`vert=1` can request vertical alternate glyphs from a suitable font. This does
not change the shaping direction, metrics, wrapping axis, or paragraph geometry.
The existing feature path therefore reduces implementation work but does not
establish vertical text support.

The recommended direction is to introduce logical inline/block coordinates in
the existing paragraph pipeline, shape upright and sideways runs separately,
and map their geometry to physical coordinates consistently. The run-shaping
part of that boundary is now implemented; paragraph integration remains
unimplemented. The Skia-to-RenderTexture-to-Unity presentation pipeline can
continue to be used.

## Phase 1 native experiment

[`tests/skia/src/test_vertical_text.cpp`](../../tests/skia/src/test_vertical_text.cpp)
implements the Phase 1 experiment as the
`MilestroTest_SkiaVerticalTextFeasibility` native test target. The test calls the
HarfBuzz library already vendored and built for Skia, then uses Skia for font
loading, glyph outlines, text blobs, and CPU rasterization. It also exercises the
Milestro-owned `ShapeTtbRun()` boundary. It does not change the paragraph
pipeline or export the API through the C ABI or a generated Unity binding.

The controlled fixtures are
[`SourceHanSans-VF.otf.woff2.bytes`](../../tests/data/font/SourceHanSans-VF.otf.woff2.bytes),
which contains `vhea`, `vmtx`, `VORG`, `vert`, and `vrt2`, and
[`LiberationSans-Bold.ttf.bytes`](../../tests/data/font/LiberationSans-Bold.ttf.bytes),
which contains no `vhea` or `vmtx` table. The image output is written to the
test's configured working directory.

### Measured behavior

- Shaping `「漢字」、。ー` as TTB automatically applies the `vert` substitutions.
  The resulting glyphs match horizontal shaping with an explicit `vert=1`
  feature, while horizontal layout geometry is left unchanged.
- At a 48 px font size, an upright glyph from the font with vertical metrics
  advances 48 px in the Skia downward-positive Y direction. Extracted vertical
  origins convert to `(24, -42.234)`, and the corresponding baselines are
  `(-24, 42.234)` before the per-glyph pen advance.
- HarfBuzz applies vertical glyph origins to the shaped `x_offset` and
  `y_offset` values. The adapter converts the Y sign once from HarfBuzz's
  upward-positive coordinates and does not subtract the vertical origin a second
  time when constructing a Skia baseline.
- Input UTF-8 byte offsets remain stable as shaping clusters. The cluster fixture
  covers both three-byte CJK scalars and a four-byte scalar, and those byte
  offsets convert to the corresponding UTF-16 source positions without altering
  the text. Orientation changes do not require rewriting source text or using
  Unicode presentation forms.
- A font without `vhea`/`vmtx` receives a synthesized vertical advance of
  53.625 px at 48 px and a nonzero synthesized origin. Layout must consume the
  shaper's metrics instead of assuming a vertical advance of exactly one em.
- TTB shaping does not rotate Latin text. Sideways content is shaped
  horizontally to retain its script behavior and then receives a clockwise
  paint transform, which is distinct from the upright TTB path.
- The current public SkShaper API exposes only even/odd bidi levels, and its
  HarfBuzz adapter maps those to horizontal LTR/RTL. A probe test confirms that
  SkShaper shapes the controlled input horizontally and cannot request TTB.
- A text blob and transformed glyph paths built from the same baselines and
  placement matrix have matching raster ink bounds within 2 px. The remaining
  pixel differences, about 2% of the CPU image, occur at antialiased glyph
  edges. The test emits `vertical-text-feasibility.png` and
  `vertical-text-paths-feasibility.png` as visual evidence.

### Integration decision

Keep the pinned Skia and SkShaper sources unchanged. The repository now
implements a Milestro-owned vertical shaping adapter in
`include/Milestro/skia/textlayout/TtbShaper.h` and
`src/skia/textlayout/TtbShaper.cpp`. It requests TTB shaping for upright runs
and horizontal shaping for sideways runs. It returns run-local glyph records,
not a paint transform; future Paragraph and painter integration must apply the
sideways placement transform. The adapter retains the shaper's offsets,
advances, clusters, and language/script/feature inputs rather than
reconstructing glyph positions. If upstream SkShaper later exposes the needed
direction and placement contract, the Milestro adapter can delegate to it
instead of carrying its own HarfBuzz call.

This experiment establishes a viable shaping and transformed-painting boundary,
and the native adapter tests cover the run-shaping contract. It does not show
how SkParagraph line storage, wrapping, or source queries carry those oriented
runs; those remain Phase 2 work. Unity has no vertical-text API or behavior yet.

### Reproduction and verification record

The recorded run used a Windows x64 host, a Debug Ninja build, Ninja 1.13.2,
clang-cl 19.1.5, CMake 4.4.2, and `MILESTRO_ENABLE_TESTS=ON`. Skia and
HarfBuzz came from the pinned `ext/skia` checkout; no external checkout or
generated binding was modified. Run these commands from the repository root in
a Visual Studio x64 developer environment:

```sh
cmake -S . -B cmake-build-cli-debug -G Ninja \
  -DCMAKE_BUILD_TYPE=Debug \
  -DMILESTRO_ENABLE_CLI=ON \
  -DMILESTRO_ENABLE_TESTS=ON
cmake --build cmake-build-cli-debug \
  --target MilestroTest_SkiaVerticalTextFeasibility
ctest --test-dir cmake-build-cli-debug \
  -R '^MilestroTest_SkiaVerticalTextFeasibility$' --output-on-failure
```

These commands require a Visual Studio x64 developer environment. A shell
without that environment may not provide `clang-cl` or the Windows SDK `rc.exe`
during CMake compiler detection.

Recorded result: CMake completed and reported matching Skia base/source
revisions. Reconfiguration regenerated the dependency graph, so the build
rebuilt the ICU and Skia dependencies before compiling and linking the target.
The latest focused CTest run passed 1/1. The GoogleTest binary covers the
feasibility probes, SourceHan upright shaping through `ShapeTtbRun()` with
global UTF-8 clusters, scoped and overridden `vrt2` feature ranges,
fallback-font upright and sideways shaping, Hebrew RTL visual positioning,
missing-glyph accounting, invalid scale cases, and invalid input or UTF-8
boundary cases. The HarfBuzz item-length guard is verified by source inspection
rather than by shaping a giant input run. The probe loads no system font:
`SourceHanSans-VF.otf.woff2.bytes` and
`LiberationSans-Bold.ttf.bytes` are resolved from `tests/data/font/` through
compile definitions.

The HarfBuzz table callback keeps an explicit `SkTypeface` reference for the
lifetime of the `hb_face_t` and transfers each copied `SkData` to the created
`hb_blob_t`; the blob destroy callback releases that `SkData`. HarfBuzz consumes
that ownership on its empty-blob and allocation-failure paths as well as on
success, so the callback must not release it separately. The test run exercises
normal table loading and blob destruction, including the fallback font without
vertical metric tables; it does not inject a HarfBuzz allocation failure.

When `icudtl.dat` is not next to the test runtime, Milestro logs that it is
using its Nop SkUnicode fallback. That does not affect the HarfBuzz shaping,
cluster, or advance assertions in this probe. It also means the run makes no
claim about ICU line breaking or full bidi resolution; the SkShaper capability
test deliberately accepts the provider that Milestro exposes.

Remaining limits of the recorded experiment:

- The run covers one Windows x64 Debug configuration. Android, iOS, macOS,
  Linux, release optimization, and renderer backends were not exercised.
- The public SkShaper API still cannot request TTB. `TtbShaper` is the
  implemented Milestro-owned native boundary, but SkParagraph does not consume
  it yet.
- SkParagraph wrapping, column placement, clipping, source queries, and cache
  isolation are not implemented or covered.
- Cross-font fallback within one vertical line, variable-axis selection,
  combining marks, grapheme clusters, emoji, Ruby, mixed-direction paragraph
  bidi, and paragraph-level RTL placement are not covered by this phase.
- The CPU text-blob/path comparison uses a 5% changed-pixel ceiling to tolerate
  antialiasing. It is not a cross-machine golden-image test.
- No performance, allocation, draw-call, or Unity presentation measurement was
  performed.

The next step is Phase 2: integrate `TtbShapedRun` into SkParagraph so it can
store and query vertical lines without changing the existing horizontal path.
Unity integration follows only after native paragraph geometry and queries are
reliable.

## Intended scope

The initial target is Chinese and Japanese static text with top-to-bottom
content and columns progressing from right to left (`vertical-rl`). Mixed Latin
text, punctuation, font fallback, and grapheme integrity are part of that target.

Later increments can add `vertical-lr` placement, vertical Ruby, and
tate-chu-yoko (a short horizontal group inside a vertical line); the native
shaping API already exposes explicit upright and sideways orientation. Editable
`TextInput` support requires its own increment after paragraph geometry is
reliable.

Full browser CSS layout, arbitrary nested writing modes, Mongolian and other
script-specific vertical conventions, and publication-quality implementation of
all Chinese/Japanese layout requirements are outside the initial target. A
supported subset must be documented at each implementation stage.

## Terminology

| Term | Meaning in this document |
| --- | --- |
| Writing mode | Orientation of typographic lines and the direction in which successive lines are placed |
| Inline axis | Axis along which content advances within a typographic line |
| Block axis | Axis along which successive typographic lines or paragraph blocks are placed |
| Vertical line / column | One typographic line in vertical writing |
| Text direction | Resolved LTR/RTL direction used for bidirectional layout; distinct from writing mode |
| Text orientation | Whether content in a vertical line is upright, sideways, or mixed |
| Vertical alternate | A font-provided replacement glyph appropriate for vertical presentation |
| Vertical metrics | Advance and origin information used to place upright glyphs in vertical runs |
| Grapheme cluster | A user-perceived text unit whose combining marks and other components stay together |
| Shaped cluster | A shaping unit connecting source text to one or more output glyphs |

An upright glyph may have a rotated-looking design: brackets and long vowel
marks can acquire that appearance through the font's vertical substitution.
This is separate from rotating an entire sideways run during painting.

## Current implementation evidence

The configured Skia revision and vendored module source revision are both
`a625b5349f7048005a4ce9612b74ffcce458982e`, recorded in
[`ext/skmodules/vendor-meta`](../../ext/skmodules/vendor-meta) and
[`ext/skia-cmake/CMakeLists.txt`](../../ext/skia-cmake/CMakeLists.txt).
The Skia source observations below describe the inspected implementation; they
must be checked again when the dependency revision changes.

| Area | Evidence | Consequence |
| --- | --- | --- |
| Public configuration | [`ParagraphStyleSettings`](../../apps/unity-plugins/Milestro/Model/ParagraphStyleSettings.cs) exposes `textDirection` as LTR/RTL, with no writing mode or orientation | A distinct configuration model is needed |
| OpenType features | [`TextStyle`](../../include/Milestro/skia/textlayout/TextStyle.h), its [managed wrapper](../../apps/unity-plugins/Milestro/Skia/TextLayout/TextStyle.cs), and [`OneLineShaper.cpp`](../../ext/skmodules/modules/skparagraph/src/OneLineShaper.cpp) forward feature tags to shaping | The generic feature channel can be reused |
| Shaping direction | [SkShaper's HarfBuzz adapter][skshaper-source] chooses `HB_DIRECTION_LTR` or `HB_DIRECTION_RTL` from the bidi level | TTB cannot currently be requested through this adapter |
| Shaper options | [`SkShaper.h`][skshaper-header] has width/tracking options and no vertical direction option | Changing paragraph style alone cannot enable vertical shaping |
| Run geometry | [`Run.h`](../../ext/skmodules/modules/skparagraph/src/Run.h) uses X positions and horizontal font ascent/descent for advance and line geometry | Representing a two-dimensional point does not make its consumers axis-independent |
| Wrapping | [`TextWrapper.cpp`](../../ext/skmodules/modules/skparagraph/src/TextWrapper.cpp) fits accumulated cluster widths into a maximum width | Vertical wrapping needs inline extents based on vertical advances |
| Glyph painting | [`Run::copyTo()`](../../ext/skmodules/modules/skparagraph/src/Run.cpp) builds positioned glyph blobs; [`ParagraphPainter`](../../ext/skmodules/modules/skparagraph/include/ParagraphPainter.h) offers translation but no general transform operation | Run rotation needs an explicit representation and painting strategy |
| Paths and visitors | [`Paragraph.cpp`](../../src/skia/textlayout/Paragraph.cpp) builds paths and glyph bounds from glyph positions plus translation | Sideways transforms must also reach paths, visitors, and glyph extraction |
| Unicode orientation | [`uchar.h`](../../ext/icu/icu4c/source/common/unicode/uchar.h) defines `UCHAR_VERTICAL_ORIENTATION` and the four orientation values | ICU can supply orientation data without a separate handwritten Unicode table |
| Ruby | [`RubyLayout.cpp`](../../ext/skmodules/modules/skparagraph/src/RubyLayout.cpp) adjusts X positions and expands ascent/descent for above/below annotations | Vertical Ruby requires changes to placement and line thickness |
| Editing | [`InputBox.cpp`](../../src/skia/textlayout/InputBox.cpp) builds a vertical caret rectangle and uses preferred X for movement between horizontal lines | Vertical editing needs caret and navigation semantics beyond paragraph painting |
| Unity measurement | [`TextBoxIntrinsicMeasurement`](../../apps/unity-plugins/Milestro/Components/Internal/TextBoxIntrinsicMeasurement.cs) measures at a supplied content width and accumulates heights | Height must become an available inline constraint in vertical writing |
| Rich-text blocks | [`TextBoxBlockStack`](../../apps/unity-plugins/Milestro/Components/Internal/TextBoxBlockStack.cs) stacks blocks along Y | Paragraph block placement must follow writing mode |
| SlimText | [`TextDrawSnapshot`](../../include/Milestro/skia/TextDrawSnapshot.h) uses `drawSimpleText()` directly | Paragraph implementation work does not automatically add vertical SlimText support |

The existing [Ruby reference](ruby-layout.md) explicitly records vertical
text/shaping as unimplemented. The current [layout reference](layout-engine.md)
describes the horizontal behavior that must remain covered by regression tests.

## Standards and dependency behavior

### Writing mode, direction, and orientation

[CSS Writing Modes][css-writing-modes] separates three independent concerns:

- `writing-mode`: `horizontal-tb`, `vertical-rl`, or `vertical-lr`.
- `direction`: bidi paragraph direction, with additional bidi processing.
- `text-orientation`: `mixed`, `upright`, or `sideways` inside vertical lines.

This separation fits Milestro better than extending `TextDirection` with a TTB
member. HarfBuzz's TTB is a shaping direction for an individual run, while the
paragraph still needs bidi analysis and a column progression policy.

For mixed orientation, horizontal-script text is normally shaped as horizontal
runs and then rotated clockwise. Preserve joining, ligatures, kerning, and
source-cluster mappings inside each run. Rotating isolated code points would
lose those properties. Bidi content within sideways runs needs separate
verification; TTB must not replace the bidi algorithm.

The CSS `upright` value also changes bidi treatment. Supporting that value is a
separate behavior decision, especially for Arabic and Hebrew; it must not be
introduced merely as a glyph rotation flag.

### Unicode orientation and cluster boundaries

[Unicode UAX #50][uax50] supplies the `Vertical_Orientation` property:

| Value | General interpretation | Missing-alternate convention in UAX #50 |
| --- | --- | --- |
| `U` | Upright | Retain upright appearance |
| `R` | Rotate clockwise | Use sideways appearance |
| `Tu` | Use a transformed vertical form | Upright fallback |
| `Tr` | Use a transformed vertical form | Rotated fallback |

CSS mixed layout initially typesets `U`, `Tu`, and `Tr` upright with vertical
features, and `R` sideways. Its handling of missing alternates for `Tr` is not
a requirement to rotate every such character automatically. The implementation
must choose and test a missing-alternate policy against the actual shaper and
font; the orientation property alone does not reveal whether substitution
succeeded.

Apply orientation to whole grapheme clusters, respecting UAX #50's cluster
rules, including the enclosing-mark exception. Do not divide combining
sequences, variation-selector sequences, emoji ZWJ sequences, or shaped clusters
when segmenting, rotating, or wrapping. Reconcile orientation boundaries with
script, language, bidi, style, and font-fallback boundaries before shaping.

Use the bundled ICU's Unicode data as the implementation data source. Record
its Unicode version in future test expectations; the latest online UAX does
not imply that an older runtime contains the latest property data.

### OpenType features: `vert`, `vrt2`, and `vrtr`

The [OpenType registry][opentype-vertical] distinguishes these mechanisms:

| Feature | Responsibility |
| --- | --- |
| `vert` | Substitute forms suitable for upright vertical presentation |
| `vrt2` | Supply vertical alternates and pre-rotated forms, including forms covered by `vert` |
| `vrtr` | Supply alternates intended to be rotated sideways by the layout engine |
| `vkrn` | Vertical kerning, distinct from horizontal `kern` |
| `vpal`, `vhal`, `valt`, `vchw` | Optional vertical spacing/alignment policies with documented interactions |

`vert` and `vrt2` must not be enabled together for the same glyph. CSS does not
use `vrt2`: it controls orientation using Unicode data and engine-side
transforms. For Milestro, that is the recommended initial policy: `vert` for
upright runs, horizontal shaping and a rotation for sideways runs, and `vrtr`
where supported and applicable to sideways runs.

The inspected [HarfBuzz feature planner][hb-shape-source] enables `vert` for
vertical shaping. It does not automatically choose `vrt2`. Explicit user
feature settings still require a precedence policy: user overrides, automatic
vertical features, and `font-feature-settings: normal` must have documented
interactions. The spacing features above must not be enabled indiscriminately;
some are mutually exclusive or duplicate layout-engine adjustments.

Vertical substitution selects glyphs while preserving the original source
text. Application code should not rewrite brackets into Unicode presentation
forms or insert newlines to manufacture a vertical layout.

### Vertical metrics and font fallback

Upright runs need vertical advances and origins, using the relevant OpenType
vertical metrics tables (`vhea`/`vmtx`) and `VORG` where applicable. Horizontal
advance and horizontal ascent/descent are not interchangeable with those data.

The inspected [HarfBuzz font implementation][hb-font-source] reads vertical
metrics and origins, accounts for variable-font data, and synthesizes missing
metrics. SkShaper already creates an OpenType-backed HarfBuzz parent font and
a Skia-backed sub-font. A prototype must verify the inherited vertical
callbacks, scaling, synthetic styles, and origin conventions in that actual
configuration. Existing dependency capability is not proof of correct output
through Milestro.

Retain HarfBuzz's offsets and advances. Its Y axis points upward; Skia's native
paragraph coordinates use downward-positive Y. Convert the sign once at the
adapter boundary, then keep placement and queries consistent.

Fallback fonts may lack vertical alternates or have different origins and
advances. Test mixed-font lines, punctuation, emoji, variable fonts, and fonts
without vertical tables. Locale-dependent forms should follow language and
font shaping rather than a hardcoded punctuation replacement list.

### Line breaking and advanced typography

ICU break opportunities, grapheme boundaries, and HarfBuzz shaping constraints
remain useful. Fit clusters against the vertical inline extent while retaining
hard breaks and punctuation restrictions. An oversized unbreakable group needs
an explicit overflow policy; individual glyph spacing must not silently split
the group.

Orientation data alone does not implement all Japanese/Chinese typesetting.
Punctuation compression, hanging punctuation, strict/loose break tailoring,
annotation collision handling, and tate-chu-yoko need their own policies and
fixtures. [JLREQ][jlreq] and [CLREQ][clreq] are follow-up references for those
requirements, not claims of current conformance.

## Architecture options

| Approach | What it provides | Assessment |
| --- | --- | --- |
| Rotate the whole horizontal paragraph | Entirely sideways text | Useful for a sideways-only effect; does not meet the target of upright CJK with mixed content |
| Place characters individually or insert line breaks | A simple stacked label | Does not preserve shaping, metrics, or normal source/query semantics for general text |
| Extend the current paragraph pipeline with logical axes and oriented runs | Shared parsing, shaping/fallback infrastructure, breaks, and queries | Recommended direction, subject to the native experiment |
| Build an independent vertical paragraph engine | Freedom to design vertical behavior separately | Duplicates substantial fallback, cache, Ruby, and query behavior; reconsider only if the experiment shows integration is impractical |

The recommended approach still requires a broad audit. Existing width/height
and X/Y names often encode real horizontal assumptions. Mapping coordinates
only at final painting would leave measurement, hit testing, and path output
inconsistent.

### Logical geometry

Use inline advance and block thickness internally. Keep public physical bounds
in ordinary X/Y coordinates, with one shared mapping used by painting and
queries.

| Mode | Inline progression for the target layout | Progression of successive lines |
| --- | --- | --- |
| `horizontal-tb` | Horizontal, with resolved bidi ordering | Downward |
| `vertical-rl` | Downward, with separately resolved oriented runs | Leftward |
| `vertical-lr` | Downward, with separately resolved oriented runs | Rightward |

Column progression does not reverse the source string. Define the physical
content origin, right-edge anchoring, empty-line geometry, baseline placement,
and overflow bounds explicitly. Unity's existing conversion from native
coordinates to UI coordinates remains a separate boundary.

The layout model needs at least:

- Available inline extent, independent of viewport width/height naming.
- Logical line metrics and a physical content size.
- Run orientation, shaping direction, and a paint transform.
- Original source ranges and cluster mappings unaffected by orientation.
- A forward geometry mapping and an inverse mapping for pointer queries.

### Shaping and rendering integration

Audit the existing shaping segmentation before adding orientation boundaries.
Upright runs use TTB and vertical metrics; sideways runs retain horizontal
shaping with their resolved bidi direction and then receive a paint transform.
Letter spacing, word spacing, justification, and baseline shifts must follow
their appropriate logical axes.

Decide how transforms are carried before making vertical paragraphs public.
Possible mechanisms include transformed glyph blobs or an extended painter
contract. The current `ParagraphPainter` cannot express arbitrary rotation,
and the current visitors/glyph extraction callbacks do not carry a general
transform. Painting, shadows, decorations, range backgrounds, paths, and
experimental glyph/SDF consumers must either consume the same geometry or
explicitly report that a vertical operation is unsupported.

The tracked vendor tree currently covers SkParagraph and SkUnicode, while
SkShaper is built from the Skia dependency checkout. An adapter change must
have a reproducible, tracked source/patch integration and matching build
wiring. Editing only the dependency checkout would leave fresh builds without
the change. Avoid compiling duplicate SkShaper implementations or depending on
unexported HarfBuzz internals.

### Milestro-owned TTB shaping API

The Milestro-owned native boundary is implemented in
`include/Milestro/skia/textlayout/TtbShaper.h` and
`src/skia/textlayout/TtbShaper.cpp` rather than by changing the shaping
interface of the pinned Skia checkout. The API shapes exactly one already
segmented, font-resolved UTF-8 run. It does not perform orientation
segmentation, bidi analysis, font fallback, line breaking, alignment, or
painting, and its header does not depend on SkParagraph.

#### Why not change SkShaper directly

The SkShaper implementation used by this repository is
`ext/skia/modules/skshaper`, not a file in the tracked `ext/skmodules` vendor
overlay. It is pinned with the Skia revision. Changing only that checkout would
be an untracked dependency patch that a fresh build would lose; copying the
implementation into Milestro would duplicate a maintained shaper and its
HarfBuzz adapter.

Adding a direction flag to the current public interface would not be enough.
`SkShaper::BiDiRunIterator` exposes only a bidi embedding level, the HarfBuzz
adapter maps even/odd levels to `HB_DIRECTION_LTR`/`HB_DIRECTION_RTL`, and
`RunHandler::RunInfo` plus its width/tracking options describe horizontal line
layout. A complete change would also need to alter run metadata, line-breaking
helpers, and the contract consumed by SkParagraph. That is a larger upstream
API change than the paragraph pipeline needs. The independent adapter keeps the
pinned sources unchanged and confines HarfBuzz-specific direction, vertical
origin, feature normalization, and coordinate conversion to one small
Milestro-owned implementation. If SkShaper later exposes an equivalent
contract, the adapter internals can delegate to it without changing the
Paragraph-facing data.

#### Input and output contract

The implemented header contract deliberately excludes `width`, line count,
alignment, fallback candidates, and paragraph style:

```cpp
enum class TtbOrientation { kUpright, kSideways };
enum class TtbHorizontalDirection { kLeftToRight, kRightToLeft };

struct TtbFeature {
    SkFourByteTag tag;
    uint32_t value;
    size_t start;  // global UTF-8 byte offset
    size_t end;
};

struct TtbShapeRequest {
    std::string_view utf8;   // caller-owned paragraph text
    size_t runStart;         // global UTF-8 byte offset
    size_t runLength;
    SkFont font;             // copy owns a typeface reference
    SkFourByteTag script;
    std::string_view language;
    TtbOrientation orientation;
    TtbHorizontalDirection sidewaysDirection;
    SkSpan<const TtbFeature> features;
};

struct TtbGlyph {
    SkGlyphID glyph;
    uint32_t clusterUtf8;    // global UTF-8 byte offset
    SkPoint position;        // run-local visual pen origin; +Y down
    SkPoint offset;          // shaped offset added to position; +Y down
    SkVector advance;        // +Y down
};

struct TtbShapedRun {
    TtbOrientation orientation;
    TtbHorizontalDirection sidewaysDirection;
    SkVector advance;        // inline run advance; +Y down for upright
    uint32_t unresolvedGlyphs;
    std::vector<TtbGlyph> glyphs;  // logical cluster order
};

MILESTRO_API bool ShapeTtbRun(const TtbShapeRequest& request, TtbShapedRun* result);
```

The input fields are the minimum needed to reproduce shaping: source bytes and
their global range, one resolved `SkFont`, script, language, orientation,
sideways bidi direction, and explicit feature overrides. `runStart` exists so
the caller can keep paragraph-global byte clusters even when HarfBuzz receives
only a slice. The full `utf8` view may also supply shaping context around that
slice. `sidewaysDirection` is ignored for an upright run. The adapter validates
that the range is in bounds and that `runStart` and `runStart + runLength` are
UTF-8 scalar boundaries; it rejects a run that starts or ends inside a
multibyte scalar instead of shaping replacement text. It also rejects a run
that exceeds HarfBuzz's per-call item-length limit and returns false when
buffer allocation or HarfBuzz shaping fails. The caller remains responsible for
supplying valid UTF-8 inside the run. The item-length limit follows HarfBuzz's
`INT_MAX / 8` guard and is verified by source inspection, not by a giant input
run in the focused tests.

The output is a shaped run, not final paragraph geometry. `advance` is the sum
of signed advances in run-local coordinates. For upright output the run
advances in positive Y. For sideways output the total advance is horizontal and
positive; `sidewaysDirection` records the resolved bidi direction. In both
modes `glyphs` is emitted in logical cluster order. Positions are computed from
a run-local left origin in visual order: LTR logical order has increasing X,
while RTL logical order has decreasing X. For RTL, the adapter consumes raw
HarfBuzz visual-order output to compute positions and then reverses only the
completed glyph records for logical-order output. Consequently
`position + offset` is directly paintable without another RTL reversal, and a
caller must not recompute a forward pen over the reversed records.

`clusterUtf8` remains a source byte offset even when substitution changes the
glyph count. The API deliberately does not return a separate vertical origin,
placement transform, or final column geometry. The caller applies the future
sideways placement rotation consistently to painting, paths, queries, and
wrapping. Consecutive clusters may map to multiple glyphs and several source
bytes may map to one cluster; callers must not reconstruct text or positions
from glyph indexes.

#### HarfBuzz responsibility boundary

- **Direction:** the adapter maps `kUpright` to `HB_DIRECTION_TTB`. It maps
  `kSideways` to `HB_DIRECTION_LTR` or `HB_DIRECTION_RTL` from the already
  resolved bidi direction. It sets script and language explicitly and does not
  call `hb_buffer_guess_segment_properties()` to make a paragraph-level bidi
  decision. HarfBuzz returns sideways RTL glyphs in visual left-to-right order;
  the adapter accumulates those positions before reversing the completed
  records into logical cluster order.
- **Features:** HarfBuzz automatically enables `vert` for a vertical buffer.
  The adapter does not append a second automatic `vert=1`. The default upright
  policy is therefore HarfBuzz's built-in `vert` substitution only. HarfBuzz
  does not choose `vrt2` automatically. Caller feature tags, values, and global
  UTF-8 byte ranges are passed through in their original order, including
  later-entry overrides. For upright runs the adapter computes the effective
  `vrt2` enabled intervals with last-entry-wins semantics, then appends
  `vert=0` only over those intervals intersected with the run. Caller `vert`
  behavior and HarfBuzz's automatic `vert` remain in effect outside them.
  `vrtr` is only an explicit sideways request and is never enabled from table
  presence. The effective feature list, including appended suppression
  intervals, not just the caller's original list, becomes part of the shaping
  cache key.
- **Vertical metrics and origin:** when an `SkTypeface`-backed HarfBuzz font is
  built with `hb_ot_font_set_funcs()`, HarfBuzz reads `vhea`/`vmtx`, `VORG`,
  and variation data and synthesizes missing advances/origins. During
  `hb_shape()`, HarfBuzz folds the vertical glyph origin into the returned
  `x_offset`/`y_offset`. The adapter converts HarfBuzz's upward-positive Y to
  Skia's downward-positive Y exactly once, and does not call
  `hb_font_get_glyph_v_origin()` again or subtract an origin from `position`.
  It also uses the shaped `x_advance`/`y_advance` rather than recomputing a
  per-glyph `hb_font_get_glyph_v_advance()`. `unresolvedGlyphs` is derived from
  HarfBuzz output for the caller's fallback retry; it is not a fallback
  decision made inside this API.
- **Clusters:** the adapter owns the buffer's source text, item offset/length,
  and `HB_BUFFER_CLUSTER_LEVEL_MONOTONE_CHARACTERS`. HarfBuzz owns cluster
  merging and reordering and may return multiple glyphs per source cluster.
  The adapter returns those values unchanged as global UTF-8 byte offsets.
  Paragraph code later converts them to UTF-16 indexes without rewriting the
  text. Orientation and shaping must never change the source index domain.

Line thickness is intentionally outside the glyph output. Upright paragraph
layout must query vertical font metrics separately, include them in the
Paragraph cache/run record, and must not substitute horizontal
`SkFontMetrics` ascent/descent. The shaped glyph advances remain authoritative
for inline occupancy.

#### Upright and sideways routing

The caller determines mixed orientation from UAX #50 and shapes runs, not
individual code points. Segmentation boundaries must be grapheme-safe and must
be reconciled with style, script, language, bidi, and fallback boundaries
before calling `ShapeTtbRun()`. The native API routes both orientation modes,
but Paragraph and painter integration remain future work.

Upright runs use `kUpright`, TTB shaping, vertical alternates, vertical
advances, and vertical origins folded into offsets. They are painted without a
run rotation; glyphs that look rotated, such as brackets or long vowel marks,
acquire that form through the font's vertical substitution. Sideways runs use
`kSideways`, horizontal shaping with their resolved LTR/RTL direction, no TTB
or automatic `vert`, and retain the script's joining, ligatures, and kerning.
Their glyphs are emitted horizontally. Paragraph/painter integration must apply
one clockwise 90-degree placement transform for the initial `vertical-rl`
target. A future `vertical-lr`/`sideways-lr` policy must choose its rotation
direction explicitly rather than infer it from `kSideways`. The shaper does not
rotate individual glyph outlines. Keeping orientation data in the output lets
wrapping, hit testing, paths, and visitors agree with the same placement once
that integration exists.

#### Font, typeface, and cache lifetime

`SkFont` owns a reference to its `SkTypeface`, so taking it by value in the
request keeps the shaping and painting state alive for the call. The current
implementation creates a per-call `hb_face_t` and keeps an explicit
`sk_sp<SkTypeface>` in the table callback owner. It copies variation coordinates
from the typeface into `hb_font_set_variations()` and sets the HarfBuzz scale
from the `SkFont` size and shaping-relevant scale. Synthetic embolden/skew,
hinting, edging, and subpixel settings remain Skia paint properties; the
adapter does not mutate a shared `SkTypeface` or reconstruct a new one to express
them.

If an `hb_face_t` cache is added later, it must hold a strong
`sk_sp<SkTypeface>` reference (or use a table callback with a matching
ref/unref) for as long as the face lives. Never retain a raw `SkTypeface*` after
the `SkFont` copy is gone. Cache the expensive face by `SkTypefaceID` plus a
strong typeface reference, face index, and variation coordinates; cache the
HarfBuzz font only by the additional shaping scale/font properties. Use a
per-call or per-thread `hb_buffer`; HarfBuzz buffers must not be shared while
shaping.

Font fallback remains the caller's responsibility. The request contains one
resolved `SkFont`, so the existing font collection can choose a candidate span,
and a nonzero `unresolvedGlyphs` result can trigger a retry with the next
candidate. A font without `vhea`/`vmtx` is not by itself a fallback reason,
because HarfBuzz synthesizes metrics. A missing `vert` alternate is governed by
the documented orientation policy, not an arbitrary font switch.

At minimum, caches and invalidation must distinguish:

- HarfBuzz face: `SkTypefaceID` plus the owned typeface reference, face index,
  and variation coordinates.
- HarfBuzz font: the face key plus size, scale, and any shaping-affecting
  synthetic properties.
- Shaped run: a hash of the UTF-8 bytes and length, global byte range, font
  key, script, language bytes, orientation, sideways direction, effective
  feature tags/values/ranges including appended `vrt2` suppression intervals,
  and cluster level. Width, wrapping, alignment, and raster settings are not
  shaping keys.
- Paragraph cache: writing mode, effective per-run orientation, resolved
  direction, and normalized vertical features in addition to the existing
  text, style, font, locale, feature, and variation inputs.

Horizontal and vertical runs must not share cached glyph placement. Preserve
the existing Ruby cache-isolation rule as well.

#### Paragraph integration path

Paragraph and Unity vertical support are not implemented. The planned first
consumer is the single-line shaping stage in
`ext/skmodules/modules/skparagraph/src/OneLineShaper.cpp`, through a thin
Milestro adapter that converts `TtbShapedRun` into the `Run` glyph, position,
offset, cluster, and advance vectors. This changes SkParagraph-side data, not
SkShaper. Do not overload `Run::fBidiLevel` to mean TTB: orientation and
sideways direction need explicit run metadata. The existing bidi level remains
the bidi result for sideways text.

When this integration is implemented:

- `Run` and `Cluster` need a logical inline extent; vertical wrapping consumes
  the Y advance while sideways wrapping consumes its horizontal advance. The
  current `Run::advance()` formula combines an X width with horizontal font
  metrics and cannot be reused unchanged for a vertical run.
- Paragraph line metrics and baselines must use vertical font metrics, while
  every glyph position stays in the shared physical coordinate model.
- Painting, paths, shadows, range rectangles, links, and visitor callbacks must
  apply the same sideways rotation. Upright runs already contain the font's
  vertical alternates and need no extra glyph rotation.
- The paragraph cache must include writing mode and effective orientation, and
  source queries must continue to map through the unchanged UTF-8/UTF-16
  cluster mapping.
- No public C API or managed binding change is needed to validate this native
  boundary. If it is later exported to Unity, the native and managed sides must
  ship from the same revision and generated bindings must be regenerated with
  `./gradlew h2cs`.

The implemented boundary lets tests cover TTB shaping independently of line
layout. Phase 2 must still integrate the same output into Paragraph without
forking SkShaper or mixing horizontal and vertical run semantics.

### Cache and source-index invariants

Writing mode, effective orientation, and shaping direction must participate in
the relevant cache keys and invalidation alongside existing font, feature,
locale, and variation inputs. Translation-only placement should remain separate
from reusable shaping where possible.

Preserve the existing distinction between native UTF-8 byte ranges and managed
UTF-16 offsets. Glyph substitution and rotation must not change the text index
domain. Measurement, rendering, and differently oriented paragraphs must not
mutate each other's cached glyph storage. Retain the Ruby cache-isolation
invariants described in [Horizontal Ruby layout](ruby-layout.md).

## Public contract decisions

The native TTB shaping API above is implemented. The following paragraph and
managed-surface requirements are design proposals, not finalized APIs:

| Concern | Proposed direction | Decision still needed |
| --- | --- | --- |
| Paragraph mode | A separate `WritingMode`, defaulting to `HorizontalTb` | Exact native/C#/serialized representation |
| Run orientation | An inherited `TextOrientation`, initially `Mixed` | When span-level overrides become supported |
| Layout input | An explicit inline constraint or a width/height constraint structure | Replacement/extension of `Layout(float width)` across the matching native/managed pair |
| Measurement | Logical intrinsic extents plus physical content size | Names and behavior of existing `MinIntrinsicWidth`, `Height`, and line metrics |
| Alignment | Define start/end along the inline axis and preserve bidi distinctions | Meaning/support of physical `Left` and `Right` in vertical modes |
| Line count | A typographic line count includes vertical columns | Consistency of `maxLines`, single-line mode, and ellipsis |
| Spacing | Paragraph spacing follows block progression | Physical block margins versus future logical margins |
| Baseline and line height | Separate line thickness from horizontal font ascent/descent | Sign and side of baseline shifts and decorations |
| Ruby side | A logical annotation side mapped by writing mode | Replacement or interpretation of `Above`/`Below` |
| Feature overrides | Automatic mode features are explicit paragraph policy | Paragraph-level precedence and reset behavior beyond the implemented native `vrt2` interval rule |

Physical names such as `contentWidth`, `contentHeight`, and rectangle edges
should continue to describe physical geometry. Do not silently return an
inline height from a property named width. Existing width-mode/height-mode
asymmetry also needs a design pass: `TextWidthMode` offers content sizing while
`TextHeightMode` currently offers only fixed/auto height.

Start Unity integration with a fixed viewport. Auto sizing, available-height
resolution through layout parents, and auto font fitting can follow after
their constraints are defined. Preserve the current protection against
measurement/layout feedback loops.

For rich text, a single writing mode per `TextBox` is a reasonable first
restriction. Blocks then share one coordinate model. Existing CSS
`margin-top`/`margin-bottom` are physical properties and must not silently be
renamed into logical margins. Their vertical behavior must be specified before
multi-block support is enabled.

Native API and Unity managed changes must ship together from the same revision.
Do not introduce mixed-version fallback paths. Change exported declarations in
`include/Milestro/game/milestro_game_interface.h`, update wrappers, and
regenerate both binding outputs with `./gradlew h2cs`; generated binding files
must not be edited manually.

## Ruby, interaction, and related components

### Ruby

Vertical Ruby needs upright annotation shaping, side placement, inline
allocation/alignment, and contribution to column thickness. Horizontal width
padding becomes a logical inline concern. Overhang, overflow, atomic wrapping,
ellipsis removal, and source-range queries still need to agree.

The current implementation positions annotations above/below and expands
ascent/descent. Reuse its group and source-index invariants, then revise the
placement model. Existing Ruby-specific query limitations remain relevant;
vertical support should not imply independent annotation editing or hit testing.

### Static queries and scrolling

Range rectangles, links, hover, accessibility frames, and glyph positions must
match painted physical bounds. Inverse pointer mapping must include the same
viewport offset, scale, clipping, and scroll translation used for painting.
Sideways glyph ink can differ from the rectangular layout cell; document which
queries return each representation.

For `vertical-rl`, initial content anchoring and movement toward later columns
must be defined together. Preserve the physical meaning of X/Y scroll values
and scrollbar ranges. Existing horizontal overflow helpers and scroll-to-link
behavior need review rather than a mechanical width/height swap.

### Editing and IME

Vertical `TextInput` requires horizontal insertion carets in upright columns,
appropriate geometry inside sideways runs, selection dragging, and consistent
movement between columns. Define physical arrow-key actions separately from
logical previous/next movement, word movement, and line start/end. Preserve the
preferred inline coordinate when moving across columns.

Composition underlines, replacement ranges, candidate-window anchor rectangles,
mobile selection synchronization, and accessibility geometry need device
verification. Existing platform IME limitations documented in
[Known limitations](limitations.md) remain independent of writing mode.

### Other renderers

`WorldSpaceTextBox` can reuse the paragraph/render-target work once geometry is
correct. SlimText uses a different direct-text path; decide its vertical scope
separately. Experimental mesh/SDF consumers depend on glyph/path extraction and
must receive transforms or explicitly reject unsupported vertical operations.

## Incremental implementation sequence

Each phase should produce a reviewable result and evidence before the next
phase expands the public surface. The Phase 1 native experiment, including the
Milestro-owned TTB shaping API, is completed by this research document.

| Phase | Deliverable | Exit criteria |
| --- | --- | --- |
| 1. Native feasibility experiment | Controlled-font samples and the implemented Milestro-owned TTB shaping API for upright TTB, sideways runs, and shared transformed geometry (`tests/skia/src/test_vertical_text.cpp`) | Verified substitutions, advances/origins, cluster mappings, painting and path strategy; implemented and tested the maintained native shaping boundary |
| 2. Native paragraph layout | `vertical-rl` mixed layout with wrapping, hard breaks, clipping, line metrics, source queries, and cache isolation | Deterministic tests show layout/query agreement and no horizontal regression; finalize the native contract |
| 3. Unity static text | Matching managed/C API and `TextBox` support with a fixed viewport | Rendering, physical measurement, pointer/link geometry, and basic scrolling agree; supported rich-text subset is documented |
| 4. Complete static behavior | Explicit multi-block policy, content sizing, auto fitting, ellipsis, decorations, and world-space coverage | Each enabled capability has geometry and visual coverage; unsupported combinations are explicit |
| 5. Typography extensions | Vertical Ruby, additional modes/orientations, and explicit tate-chu-yoko in separately reviewable increments | Each extension preserves clusters, wrapping, source queries, and cache isolation |
| 6. Editing | Vertical `InputBox`/`TextInput` navigation, composition, selection, and accessibility integration | Native/managed tests and platform IME/device validation cover the declared support matrix |

Phase 1 was scoped as a small native experiment using existing Skia test
infrastructure and CPU images. It avoided Unity and renderer-backend setup while
resolving the largest shaping uncertainty: how oriented runs, transforms, and
existing SkParagraph geometry fit together.

The Phase 1 CPU experiment above covers the shaping and painting portion of that
boundary, and the focused tests cover `ShapeTtbRun()`. Phases 2 through 6 remain
proposals; Paragraph and Unity vertical support are not implemented.

Before enabling any public mode, define unsupported combinations explicitly.
A prototype may restrict Ruby, placeholders, paragraph-level bidi or mixed RTL
runs, or experimental exports; such restrictions must be observable and
documented rather than silently producing horizontal or incomplete output.

## Validation plan

### Controlled corpus

Use licensed, redistributable fixture fonts with recorded versions and known
features. Tests must not depend on whichever system font is installed. Include
both a font with vertical substitutions/metrics and a font without them. Font
substitution and positioning assertions should use the known fixture data.

| Case | What to establish |
| --- | --- |
| `天地玄黃`, Chinese/Japanese punctuation, brackets, and `ー` | Upright CJK, locale-sensitive alternate selection, and vertical origin/advance |
| `漢字ABC2026かな` and Latin ligature/kerning pairs | Stable orientation segmentation and intact sideways-run shaping |
| Combining marks, enclosing marks, variation selectors, and emoji ZWJ sequences | Orientation and wrapping preserve complete clusters |
| Font fallback within a line, missing vertical tables, variable fonts | Predictable fallback metrics and mixed-font column thickness |
| Narrow, zero, and oversized inline constraints | Defined overflow, preserved atomic units, and no invalid geometry |
| Hard breaks, empty paragraphs, final newline, trailing spaces | Consistent empty columns, source positions, and line metrics |
| Arabic/Hebrew with numbers and isolates inside mixed text | Verified bidi behavior or an explicit unsupported boundary |
| Clip, ellipsis, justification, backgrounds, shadows, and decorations | Paint and query geometry share placement and transforms |
| Ruby groups and tate-chu-yoko, when introduced | Atomic wrapping, side placement, and stable source-range ownership |
| Repeated layout at different heights and shared-font/style variations | Correct cache invalidation and independent paragraph storage |

### Oracles and automated checks

Compare controlled upright runs with HarfBuzz output for the same font,
language, features, and direction. The Phase 1 test performs this comparison in
process; the equivalent diagnostic commands are:

```sh
hb-shape "<font-file>" --direction=ttb --script=hani --language=ja '「漢字」、。ー'
hb-shape "<font-file>" --direction=ltr --script=hani --language=ja --features=vert=1 '「漢字」、。ー'
```

The second command helps expose the difference between alternate selection and
vertical metrics. HarfBuzz output alone does not verify paragraph wrapping or
painting.

Use geometry assertions for source ranges, column order, advances, bounds,
caret/range positions, and paint/query round trips. Add CPU-rendered image
comparisons with pinned fonts for appearance. Browser samples using the same
font and explicit writing-mode settings provide an additional visual reference,
with implementation differences investigated rather than treated as universal
pixel-level requirements.

Extend the existing `tests/skia/`, `tests/ruby/`, and `tests/inputbox/` coverage
where the behavior belongs. Existing regression targets include
`MilestroTest_SkiaParagraphHitTest`, `MilestroTest_Ruby`, and
`MilestroTest_InputBox`. The Phase 1 test, including `ShapeTtbRun()` coverage, is
registered as `MilestroTest_SkiaVerticalTextFeasibility`; future paragraph tests
should extend that target family. Managed coverage should extend the sizing,
paragraph-style, parser, and scrolling tests under
`apps/unity-plugins/Milestro.Tests/` as each feature is introduced.

### Platform and performance verification

Verify native CPU output first, then Unity rendering and interaction on the
supported desktop/mobile backends. Include color emoji and font-manager
differences. Unity Editor validation requires a host run because its license
service is unavailable in the sandbox.

Measure shaping, layout, raster time, allocations, cache behavior, and glyph
blob/draw-call counts against horizontal baselines. Orientation segmentation
can increase run count; no performance target or overhead estimate is
established by this research. Record reproducible fixtures and build settings
without workstation paths or device/account identifiers.

## Open decisions and next checkpoint

Resolve these in the follow-up design review for native paragraph layout and
later surfaces:

1. Can logical advances and explicit run transforms integrate cleanly with the
   current line representation, or does a smaller vertical-specific line
   placement module provide a better boundary?
2. Should the implemented HarfBuzz-backed TTB adapter remain the production
   boundary, or should direction and placement hooks be proposed upstream to
   SkShaper later?
3. What transform representation covers painters, visitors, paths, and glyph
   extraction without duplicating placement rules?
4. What is the missing-vertical-alternate policy, and which remaining feature
   conflicts need explicit precedence?
5. What layout/measurement contract expresses available inline extent while
   preserving the physical meaning of existing dimensions?
6. Which rich-text, bidi, placeholder, alignment, and export combinations form
   the first supported static subset?

The next useful increment is Phase 2 native paragraph layout using the measured
shaping and painting contract above. Keep future completed work separate from
the proposals recorded here.

## References

Reviewed primary references:

- [CSS Writing Modes Level 3][css-writing-modes], particularly text orientation,
  vertical font features, and mixed vertical orientations.
- [Unicode UAX #50: Unicode Vertical Text Layout][uax50], particularly property
  values, grapheme clusters, and transformed-character handling.
- [OpenType registered features, `u` through `z`][opentype-vertical], including
  `vert`, `vrt2`, `vrtr`, and vertical spacing features.
- [HarfBuzz buffer properties][hb-buffer-properties], for shaping direction,
  script, language, and cluster behavior.
- [SkShaper HarfBuzz adapter][skshaper-source] and [SkShaper API][skshaper-header]
  at the configured Skia revision.
- The inspected HarfBuzz [feature planner][hb-shape-source] and
  [OpenType font callbacks][hb-font-source]. The source links use the HarfBuzz
  revision listed in the inspected Skia `DEPS`; recheck dependency pins when
  reproducing the experiment.

Further requirements research, not a completed conformance audit:

- [Requirements for Japanese Text Layout (JLREQ)][jlreq].
- [Requirements for Chinese Text Layout (CLREQ)][clreq].

Related Milestro documentation:

- [Layout engine parameters](layout-engine.md).
- [Horizontal Ruby layout](ruby-layout.md).
- [Text, fonts, and rich text](../guides/text.md).
- [Rendering, batching, and backends](rendering.md).
- [Input and IME](../guides/input.md).

[css-writing-modes]: https://www.w3.org/TR/css-writing-modes-3/
[uax50]: https://www.unicode.org/reports/tr50/
[opentype-vertical]: https://learn.microsoft.com/en-us/typography/opentype/spec/features_uz
[hb-buffer-properties]: https://harfbuzz.github.io/setting-buffer-properties.html
[skshaper-source]: https://github.com/google/skia/blob/a625b5349f7048005a4ce9612b74ffcce458982e/modules/skshaper/src/SkShaper_harfbuzz.cpp
[skshaper-header]: https://github.com/google/skia/blob/a625b5349f7048005a4ce9612b74ffcce458982e/modules/skshaper/include/SkShaper.h
[hb-shape-source]: https://github.com/harfbuzz/harfbuzz/blob/9cb1fee51069b206effb4736e443b038d230789d/src/hb-ot-shape.cc
[hb-font-source]: https://github.com/harfbuzz/harfbuzz/blob/9cb1fee51069b206effb4736e443b038d230789d/src/hb-ot-font.cc
[jlreq]: https://www.w3.org/TR/jlreq/
[clreq]: https://www.w3.org/TR/clreq/

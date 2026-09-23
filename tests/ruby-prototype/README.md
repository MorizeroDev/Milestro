# Throwaway native ruby experiment

This is task249's bounded experiment on Milestro `e21137b6`, not a production ruby implementation.
It calls the real vendored builder, ICU and HarfBuzz, retains original text/run/cluster identity,
and runs a separate experimental unit/line/CPU-paint path. It does **not** modify or validate the
existing TextWrapper/TextLine implementation for ruby. Do not wire this API into the product.

## Run

Dependencies: GCC13/C++20, CMake>=3.25, Ninja, Skia's GN executable, fixed Skia source/dependencies,
Milestro's ICU source and its matching static archives. There is no Unity dependency.

1. Initialize the repo's `ext/icu` submodule (`457157a92aa053e632cc7fcfd0e12f8a943b2d11`).
2. Fetch upstream `google/skia` at `7184e167115bf8b7000ba1582f57709aaa4b565f` into `ext/skia`.
   Supply GN and sync the pinned DEPS. CPU targets require freetype, harfbuzz, brotli, libpng, zlib
   and Skia's ICU checkout. Matching clean local clones may be reused, not older revisions.
3. Build Milestro's `ext/icu-cmake` or supply matching archives from an existing build.
   The test explicitly loads the repo's `icudtl.dat`; the dummy data archive is not a data substitute.
4. From this checkout, run:

```sh
ICU_SOURCE="$PWD/ext/icu" ICU_ARCHIVES=/absolute/matching/icu/lib \
  bash tests/ruby-prototype/run.sh
```

`CMAKE`, `NINJA`, `BUILD_DIR`, `OUTPUT_DIR` and `JOBS` can override tool/output locations.
Output is numeric diagnostics and PPM CPU raster images. A successful exit is only the explicitly
tested experimental contract; assertion counts include loops, not hundreds of independent tests.

## Fixed fonts and Unicode

|File|SHA256|
|---|---|
|`tests/data/font/SourceHanSans-VF.otf.woff2.bytes`|`bd1cd6bd0993a96c799ba765d8171368ff69cba3253e6bbcb96dedfb9152498a`|
|`tests/data/font/NotoColorEmoji.ttf.bytes`|`c2f19f6a404baa7da7a710b018c2892d7b51386983ddca146811f76aea0b6861`|
|`ext/skia/resources/fonts/Roboto-Regular.ttf`|`466989fd178ca6ed13641893b7003e5d6ec36e42c2a816dee71f87b775ea097f`|
|`ext/icu-cmake/common/icudtl.dat`|`310d9b2cb42947fad7f388b09d2ae574e2adeb60ca76abf280438908f20b2d7b`|

The selected backend is the locally vendored SkUnicode ICU implementation (ICU77.1), not system
fonts or guessed cluster arrays. Roboto's actual `ffi` cluster is asserted before a boundary test.

## Experimental policies / deliberate gaps

- Horizontal LTR, one upper annotation layer, explicit UTF8 byte ranges, centered, no overhang.
- Internal pair breaks forbidden; group endings come from original Unicode soft-break flags.
  Oversized nonbreaking groups overflow rather than taking the original emergency split path.
  At zero width the algorithm still consumes at least one such group, not necessarily one pair.
- The extreme case uses 56 kana over one base character: the pair is 893.441 wide in a 120-wide
  container. Centering puts the base ink at 431.721, entirely outside a 120-wide clip. Eric accepted
  overflow without silent shrinking; preserve this visible cost, do not interpret progress as readability.
- Whole-input shaping and original glyph offsets are reused. A separate raw Unicode pass precedes
  shaping/cache lookup, so run-added grapheme flags cannot legalize an invalid boundary.
- Ordinary units preserve original advance (do not pad every non-ruby cluster to its ink width).
  Ruby units use ink-safe widths; before/after metrics are real font extents without old rounding.
- Original builder/layout/paint remain available. Their before/after raster hash is checked.
  The experimental no-ruby path intentionally differs from TextWrapper's emergency wrapping,
  trimming and rounded height policy; it is not a proposed replacement for all ordinary text.
- `baseRects` returns the same native ink geometry consumed by experimental paint, in UTF8 bytes.
  It is **not** proof of the existing UTF16 range/glyph-query APIs, extendedVisit or hyperlink support.
- Solid paint only. No XML, managed ABI, Unity snapshots, strut, justification, ellipsis, editing,
  multiline annotation, complex Bidi, vertical shaping or production ruby cache is implemented.
- Object ownership is local parent plus annotation paragraphs; it does not test two independent
  measurement/render parents or asynchronous retirement. The annotation input is immutable.

## Upstream and vertical seams

Only ParagraphBuilderImpl's header and implementation have a macro-gated experimental factory.
All other new code is in `src/experimental` or this test directory. No original wrapping code was
copied. The short new legal-break-only policy answers the experiment, not full TextWrapper parity.
Future integration still touches TextWrapper/TextStretch, TextLine visual slices and both visitors,
ParagraphImpl fast paths/queries and ParagraphCache. These are upstream synchronization hotspots.

Units/lines use inline extent, before/after and block progression, not a permanent top/left model.
The glyph extraction and CPU painter remain explicitly horizontal. SkShaper_harfbuzz currently
chooses LTR/RTL, not TTB; Run assumes horizontal advance and font ascent/descent. Vertical work
needs real shaping direction/features/metrics and punctuation/orientation policies, then an
axis-aware placement/painter boundary. Rotating this output would not implement vertical layout.

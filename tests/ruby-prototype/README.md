# Throwaway native ruby experiment

This retains the earlier experiment as an independent regression control. Production Ruby now
uses the normal builder, TextWrapper and TextLine pipeline; its API and policies are described
in [horizontal Ruby layout](../../docs/ruby-layout.md), and tested by `tests/ruby`.
The prototype calls the real vendored builder, ICU and HarfBuzz and retains original
text/run/cluster identity, but uses its own experimental unit/line/CPU-paint path.

## Run

The repository CMake test tree reuses the existing Skia and ICU targets, including on Windows.
From a Visual Studio developer shell:

```powershell
cmake --build cmake-build-nocli-debug --target ruby-prototype MilestroTest_Ruby
ctest --test-dir cmake-build-nocli-debug -R Ruby --output-on-failure
```

CTest writes prototype samples under `tests/ruby-prototype/results` in the build tree.
No separate Windows Skia build is required. The standalone Linux runner remains available:

Dependencies: GCC13/C++20, CMake>=3.25, Ninja, Skia's GN executable, fixed Skia source/dependencies,
Milestro's ICU source and its matching static archives. There is no Unity dependency.

1. Initialize the repo's `ext/icu` submodule (`457157a92aa053e632cc7fcfd0e12f8a943b2d11`).
2. Fetch upstream `google/skia` at `b23ddbff12658185f0d506befec22a84f8b8c03b` into `ext/skia`.
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
|`tests/data/font/LiberationSans-Bold.ttf.bytes`|`e8e7047c5217563154dc59667124a9735a17680aafdf4762ee5b3f2b5099bfed`|
|`ext/icu-cmake/common/icudtl.dat`|`310d9b2cb42947fad7f388b09d2ae574e2adeb60ca76abf280438908f20b2d7b`|

The selected backend is the locally vendored SkUnicode ICU implementation and pinned ICU data.
The fixture explicitly loads its fonts, including the OFL Liberation Sans fixture for RTL
regressions. Roboto's actual `ffi` cluster is asserted before a boundary test.

## Experimental policies / deliberate gaps

- Horizontal LTR and RTL, one upper annotation layer, explicit UTF8 byte ranges, centered, no overhang.
  Original ICU bidi levels order visual units and glyph slices. A ruby range crossing bidi levels
  is rejected because the pair must remain one visual unit.
- Internal pair and grapheme breaks are forbidden. Original Unicode soft breaks are preferred;
  hard breaks create lines, including an empty final line. A word too wide for its line uses an
  emergency break between units. Trailing spaces remain attached to the prior line but are
  excluded from its reported visible width. A single oversized pair still overflows.
- The extreme case uses 56 kana over one base character: the pair is 893.441 wide in a 120-wide
  container. Ordinary inline positioning puts the base ink at 431.721, outside a 120-wide clip.
  Eric accepted no silent shrinking, not automatically the disappearance of the base.
- A temporary per-pair `PrototypeOversizePlacement` enum compares `InlineStart` and `BaseVisible`
  on the same line-breaking path. The latter moves only an oversized pair's visual placement so
  its base safe box is centered inside the inline limit, with annotation centered around that base.
  It does not change allocated width, glyph size or line membership. Both modes match when the
  pair fits. At zero width, or when the base itself cannot fit, anchoring cannot make it all visible.
  These are experimental names; CSS naming/default policy is still under discussion. Interactions
  with adjacent units, especially leading punctuation, need further work.
- Whole-input shaping and original glyph offsets are reused. A separate raw Unicode pass precedes
  shaping/cache lookup, so run-added grapheme flags cannot legalize an invalid boundary.
- Ordinary units preserve original advance (do not pad every non-ruby cluster to its ink width).
  Ruby units cover advance and ink-safe widths; before/after metrics are real font extents without old rounding.
- Original builder/layout/paint remain available. Their before/after raster hash is checked.
  The experimental no-ruby path still differs from TextWrapper's line metrics and rounding policy;
  it is not a proposed replacement for all ordinary text.
- `baseRects` returns the native ink geometry consumed by experimental paint, in UTF8 bytes.
  `baseRectsUtf16` and `hitTestRangeUtf16` map original UTF16 ranges to that same geometry.
  Partial surrogate pairs and graphemes are rounded inward, so incomplete characters return no ink.
  They do not validate the existing Paragraph range/glyph-query APIs, extendedVisit or hyperlinks.
- Solid paint only. No XML, managed ABI, Unity snapshots, strut, justification, ellipsis, editing,
  multiline annotation, ruby spanning bidi levels, vertical shaping or production ruby cache is implemented.
- Object ownership is local parent plus annotation paragraphs; it does not test two independent
  measurement/render parents or asynchronous retirement. The annotation input is immutable.

## Upstream and vertical seams

Only ParagraphBuilderImpl's header and implementation have a macro-gated experimental factory.
All other new code is in `src/experimental` or this test directory. No original wrapping code was
copied. The experimental breaker uses Unicode breaks and unit-level emergency wrapping, not full
TextWrapper parity.
Production integration now touches TextWrapper, TextLine and both visitors, ParagraphImpl
fast paths/queries and shared Run glyph storage. These are upstream synchronization hotspots
preserved by the vendor script.

Units/lines use inline extent, before/after and block progression, not a permanent top/left model.
The glyph extraction and CPU painter remain explicitly horizontal. SkShaper_harfbuzz currently
chooses LTR/RTL, not TTB; Run assumes horizontal advance and font ascent/descent. Vertical work
needs real shaping direction/features/metrics and punctuation/orientation policies, then an
axis-aware placement/painter boundary. Rotating this output would not implement vertical layout.

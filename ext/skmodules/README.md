# Skia module vendor

`vendor-meta` records the upstream source and compatible Skia core revision. The current
module source is from `e43478a2f9f8c7f68d6c7e2ca79441dac7df9b75`.

This revision reserves the rendered soft hyphen width during justification and
keeps invisible soft hyphens out of justification patch counting. Hits on a
rendered soft hyphen or ellipsis use upstream affinity so glyph lookup stays
on the current line. Cluster iteration also handles trailing whitespace runs
and runs removed by ellipsis without forming invalid ranges.

Skia no longer provides the libavif decoder or its GN flag. AVIF decoding is
unsupported in this standalone build; CrabbyAvif currently requires Android
framework integration. PNG and JPEG decoding remain enabled.

This update resets typeface, fallback, and paragraph resolution caches when font
managers, default families, or fallback settings change. Long unbreakable words
reuse width measurements across lines, and style block lookup uses a binary search.
Shaping also clears resolved blocks between styles. SkUnicode computes character
properties once per code point and caches ASCII flags in the ICU backend.

This revision includes upstream fixes for BiDi cluster coverage and ellipsis shaping,
zero-width grapheme hit bounds, combining-mark and ligature styling, justified hit
testing, and soft hyphen rendering (including configurable replacement strings).
Soft hyphen breaks reserve the rendered hyphen's width and expose its source range
to selection and RTL hit testing. Font cache keys cover all shaping inputs, cache
hits restore unresolved glyph/font diagnostics, baseline shifts apply once with
height overrides, and empty-line/font metrics honor TextStyle half-leading.
SkUnicode includes the native Apple backend, Unicode property fixes, and
`isDefaultIgnorable`. Milestro still selects the ICU backend; its no-data fallback
uses the upstream hardcoded default-ignorable property table. Skia core and headers
must match this revision because the new virtual method changes the SkUnicode ABI.

Milestro keeps horizontal Ruby integration in the paragraph/builder public headers,
`Ruby.h`, `ParagraphBuilderImpl.cpp/.h`, `ParagraphImpl.cpp/.h`, `Run.cpp/.h`,
`RubyLayout.cpp/.h`, `TextLine.cpp`, and `TextWrapper.cpp/.h`. Visitor safety changes
also remain in `SkParagraphTest.cpp`.
First-line indentation also modifies `ParagraphStyle.h` and the paragraph layout files.
Cached long-word measurements update intrinsic width only on the first scan so
reused measurements preserve negative first-line indentation.
These overlays are preserved by `scripts/vendor-skia-modules.sh`; its `overlay_paths`
array supplies both upstream conflict detection and synchronization exclusions,
including the vertical layout, metrics, painter, shaping, style, and cache overlays.

See [horizontal Ruby](../../docs/reference/ruby-layout.md) for the API and layout policy.

When updating Skia, compare the old and new upstream revisions for the preserved paths,
merge those changes into the vendor tree, then run the vendor script with the new revision
as `--base`. If those paths changed upstream, add `--merged-overlays` after reviewing and
merging them. Update the Skia pin in `ext/skia-cmake/CMakeLists.txt` and any experiment
revision checks at the same time.

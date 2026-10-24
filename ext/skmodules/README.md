# Skia module vendor

`vendor-meta` records the upstream source and compatible Skia core revision. The current
module source is from `a625b5349f7048005a4ce9612b74ffcce458982e`.

This update synchronizes FontCollection's font caches and caches system fallback
results across paragraphs. It also corrects underline gaps with shifted baselines and
soft-hyphen intrinsic widths, including fallback glyphs and truncated paragraphs.

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
These overlays are preserved by `scripts/vendor-skia-modules.sh`; its `overlay_paths`
array supplies both upstream conflict detection and synchronization exclusions.

See [horizontal Ruby](../../docs/ruby-layout.md) for the API and layout policy.

When updating Skia, compare the old and new upstream revisions for the preserved paths,
merge those changes into the vendor tree, then run the vendor script with the new revision
as `--base`. If those paths changed upstream, add `--merged-overlays` after reviewing and
merging them. Update the Skia pin in `ext/skia-cmake/CMakeLists.txt` and any experiment
revision checks at the same time.

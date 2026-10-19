# Skia module vendor

`vendor-meta` records the upstream source and compatible Skia core revision. The current
module source is from `a3cc6e57c0379322e3507839372ba4cfc382cfd6`.

This revision includes upstream fixes for BiDi cluster coverage and ellipsis shaping,
zero-width grapheme hit bounds, combining-mark and ligature styling, justified hit
testing, and soft hyphen rendering (including configurable replacement strings).
SkUnicode includes the native Apple backend, Unicode property fixes, and
`isDefaultIgnorable`. Milestro still selects the ICU backend; its no-data fallback
uses the upstream hardcoded default-ignorable property table. Skia core and headers
must match this revision because the new virtual method changes the SkUnicode ABI.

Milestro keeps horizontal Ruby integration in the paragraph/builder public headers,
`Ruby.h`, `ParagraphBuilderImpl.cpp/.h`, `ParagraphImpl.cpp/.h`, `Run.cpp/.h`,
`RubyLayout.cpp/.h`, `TextLine.cpp`, and `TextWrapper.cpp/.h`. Visitor safety changes
also remain in `SkParagraphTest.cpp`. The earlier prototype lives in `src/experimental/`.
First-line indentation also modifies `ParagraphStyle.h` and the paragraph layout files.
These overlays are preserved by `scripts/vendor-skia-modules.sh`; its `overlay_paths`
array supplies both upstream conflict detection and synchronization exclusions.

See [horizontal Ruby](../../docs/ruby-layout.md) for the API and layout policy.

When updating Skia, compare the old and new upstream revisions for the preserved paths,
merge those changes into the vendor tree, then run the vendor script with the new revision
as `--base`. If those paths changed upstream, add `--merged-overlays` after reviewing and
merging them. Update the Skia pin in `ext/skia-cmake/CMakeLists.txt` and any experiment
revision checks at the same time.

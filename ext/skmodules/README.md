# Skia module vendor

`vendor-meta` records the upstream source and compatible Skia core revision. The current
module source is from `b23ddbff12658185f0d506befec22a84f8b8c03b`.

Milestro keeps horizontal Ruby integration in the paragraph/builder public headers,
`Ruby.h`, `ParagraphBuilderImpl.cpp/.h`, `ParagraphImpl.cpp/.h`, `Run.cpp/.h`,
`RubyLayout.cpp/.h`, `TextLine.cpp`, and `TextWrapper.cpp/.h`. Visitor safety changes
also remain in `SkParagraphTest.cpp`. The earlier prototype lives in `src/experimental/`.
These overlays are preserved by `scripts/vendor-skia-modules.sh`; its `overlay_paths`
array supplies both upstream conflict detection and synchronization exclusions.

See [horizontal Ruby](../../docs/ruby-layout.md) for the API and layout policy.

When updating Skia, compare the old and new upstream revisions for the preserved paths,
merge those changes into the vendor tree, then run the vendor script with the new revision
as `--base`. If those paths changed upstream, add `--merged-overlays` after reviewing and
merging them. Update the Skia pin in `ext/skia-cmake/CMakeLists.txt` and any experiment
revision checks at the same time.

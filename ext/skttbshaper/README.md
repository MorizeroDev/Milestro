# skttbshaper

`ext/skttbshaper` is a project-maintained Milestro extension for shaping one
already segmented and font-resolved UTF-8 run as upright TTB or sideways text.
It is not a copy of Skia upstream source and is separate from the pinned
`ext/skia` checkout, so Skia vendor updates do not overwrite it.

The module builds the static target `milestro_skttbshaper`, also available as
`Milestro::SkTtbShaper`. Its public header is `skttbshaper/TtbShaper.h` in the
`skia::textlayout` namespace. The module has no Milestro, SkParagraph, ICU, or
Unity dependency. It reuses the HarfBuzz library already built for the `skia`
target and does not compile a second HarfBuzz or SkShaper.

See [the TTB target contract](../../docs/reference/ttb-shaper-target.md) for the
dependency contract and [the vertical text research](../../docs/reference/vertical-text-research.md)
for the shaping behavior and future paragraph integration boundary.

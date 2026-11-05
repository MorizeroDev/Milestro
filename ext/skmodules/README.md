# Vendored Skia text modules

Milestro builds its own SkParagraph and SkUnicode libraries alongside Skia core.
This directory contains upstream code plus Milestro's layout extensions. Skia's
GN build disables SkParagraph and ICU; this directory's `CMakeLists.txt` builds
the text modules against Milestro's ICU target instead.

## Source and version contract

The current upstream revision is
`443876974ffca0982aa10ab21d6e7f4b9f5fd66e`.

`vendor-meta` records two revisions:

| Key | Meaning |
| --- | --- |
| `skia-base` | Compatible Skia core revision, also pinned in `ext/skia-cmake/CMakeLists.txt` |
| `source-commit` | Commit supplying the vendored module sources |

Both currently identify the same commit. A later source commit may differ only
inside `modules/skparagraph` and `modules/skunicode`; the vendor script verifies
that restriction. CMake accepts a Skia checkout at either recorded revision and
rejects other revisions. Changing metadata alone does not migrate the code.

| Path | Contents |
| --- | --- |
| `modules/skparagraph/` | Upstream paragraph module with Milestro layout extensions |
| `modules/skunicode/` | Upstream Unicode implementations, without the upstream `include/` tree |
| `LICENSE` | Skia's license |
| `vendor-meta` | Source and compatible core revisions |

SkUnicode headers come from `ext/skia/modules/skunicode/include/`. These headers,
the vendored implementations, and Skia core must agree on the API and ABI.
Milestro selects the ICU Unicode backend; its no-data fallback lives in
`src/skia/unicode/` and uses Skia's hardcoded default-ignorable property table.

## Milestro extensions

Local changes support horizontal Ruby, vertical text, text orientation overrides,
tate-chu-yoko, first-line indentation, no-wrap ranges, and CJK punctuation spacing.
They also extend metrics, painting, visitors, and cache keys for those features.

The complete list of preserved files is the `overlay_paths` array in
`scripts/vendor-skia-modules.sh`. It is used both to detect upstream changes that
need review and to exclude local overlays from automatic replacement. Treat
`OneLineShaper.cpp` and `OneLineShaper.h` as one overlay when updating shaping.

The current upstream Run representation stores glyphs, positions, and cluster
indexes in one exact-size allocation. Horizontal shaping stores offsets only
when nonzero; positions and cluster indexes have a sentinel entry, offsets do
not. Milestro explicitly allocates vertical run data. Ruby detaches shared glyph
data and materializes writable offsets before changing padding, so cached runs
and other paragraphs retain their original geometry. Unresolved codepoint sets
are shared with paragraph cache entries and remain immutable after shaping.

See [Ruby layout](../../docs/en/reference/ruby-layout.md) and
[the layout engine](../../docs/en/reference/layout-engine.md) for public behavior.

## Updating Skia

Run commands from the repository root. The Skia module source must be committed;
the vendor script reads Git objects and rejects uncommitted module changes.

1. Record the old revisions from `vendor-meta`, check out the new Skia revision,
   and synchronize its dependencies:

   ```sh
   git -C ext/skia checkout <new-skia-commit>
   (cd ext/skia && python3 tools/git-sync-deps)
   ```

2. Compare the old source commit with the new commit, including every preserved
   overlay. Merge upstream changes into the corresponding vendored files while
   retaining Milestro's extensions. Check core and header changes against callers
   in `src/`, `include/`, and `ext/skttbshaper/` as well.

   ```sh
   git -C ext/skia diff <old-source-commit> HEAD -- modules/skparagraph modules/skunicode
   ```

3. Preview synchronization. If upstream changed an overlay, the script stops
   until the merge has been reviewed. Only then use `--merged-overlays`:

   ```sh
   scripts/vendor-skia-modules.sh --base <new-skia-commit> --dry-run --merged-overlays
   scripts/vendor-skia-modules.sh --base <new-skia-commit> --merged-overlays
   ```

   The script replaces non-overlay files, removes obsolete non-overlay files,
   omits SkUnicode headers, and updates the license and metadata. The confirmation
   flag does not merge overlays; that work must already be complete. If no overlay
   changed upstream, omit the flag.

4. Update the Skia pin in `ext/skia-cmake/CMakeLists.txt`, this document's source
   revision and implementation notes, and any experiment-specific revision checks.
   Reconfigure the build so GN and CMake use the new sources.

## Validation

Build the native library and tests, then run the registered suites:

```sh
cmake -S . -B <build-dir> -G Ninja -DCMAKE_BUILD_TYPE=Debug -DMILESTRO_ENABLE_TESTS=ON
cmake --build <build-dir>
ctest --test-dir <build-dir> --output-on-failure
```

Pay particular attention to Ruby and vertical paragraph suites, cache isolation
across paragraphs and relayouts, missing glyph diagnostics, hit testing, spacing,
and InputBox caret geometry. Upstream tests under `modules/skparagraph/tests/`
remain vendored references; Milestro's CMake regression tests live under `tests/`.

Verify Unity with managed sources and the native library from the same Milestro
revision. Exercise EditMode and PlayMode, including rendering with a graphics
context. See [build and packaging](../../docs/en/reference/build.md).

Finally, inspect the diff and run the vendor script in dry-run mode again. Local
overlays must remain intact and non-overlay module contents must match upstream.
Check that the patch contains no workstation paths or other device details.

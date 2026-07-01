# InputBox Phase 0 API Spike

Task: #39

Base/head at spike start: `origin/master` / `ac9c9b0570a8697153f68a77ab101381626e2dcc` (`chore: formatter rules`).

Scope: verify whether current Milestro, SkParagraph, and SkUnicode can support a later horizontal pure-text InputBox. This is not an editor model, IME implementation, rich-text editor, vertical input, worldspace renderer, or batching task.

## Conclusion

Feasible, with a required offset-conversion layer.

SkParagraph exposes enough primitive data for a horizontal InputBox: hit-test with affinity, selection/range rects, glyph/grapheme cluster info, line metrics, word boundaries, paragraph measured sizes, and nearest glyph lookup. SkUnicode exposes ICU-backed code-unit flags including grapheme starts and glyph-cluster starts.

The main caveat is offset units:

- Milestro should store canonical editor state as UTF-8 byte offsets.
- SkParagraph editing-facing APIs are mostly UTF-16-facing: `getGlyphPositionAtCoordinate`, `getRectsForRange`, `getWordBoundary`, `getGlyphInfoAtUTF16Offset`, `getClosestUTF16GlyphInfoAt`, and `getLineNumberAtUTF16Offset`.
- SkParagraph also has UTF-8-facing cluster APIs such as `getGlyphClusterAt(TextIndex codeUnitIndex)` and internal UTF-8/UTF-16 maps, but those maps are not exposed through the public API.

Therefore Phase 1 needs a Milestro-owned conversion table built from the source UTF-8 string after each text mutation:

- UTF-8 byte offset -> UTF-16 code-unit offset.
- UTF-16 code-unit offset -> UTF-8 byte offset.
- valid caret boundaries snapped to grapheme/shaping cluster starts/ends.

## Verified API Surface

Upstream SkParagraph primary APIs used by the spike:

- `Paragraph::getGlyphPositionAtCoordinate(dx, dy)` returns `PositionWithAffinity` for hit-test.
- `Paragraph::getRectsForRange(start, end, RectHeightStyle, RectWidthStyle)` returns visual `TextBox` segments for a range.
- `Paragraph::getGlyphInfoAtUTF16Offset(offset, &info)` returns glyph/grapheme cluster geometry and UTF-16 text range.
- `Paragraph::getClosestUTF16GlyphInfoAt(dx, dy, &info)` maps point to nearby glyph info.
- `Paragraph::getGlyphClusterAt(utf8Offset, &info)` and `getClosestGlyphClusterAt(dx, dy, &info)` expose UTF-8 cluster info.
- `Paragraph::getLineMetrics`, `getLineMetricsAt`, `getLineNumberAtUTF16Offset`, `getHeight`, `getLongestLine`, `getMinIntrinsicWidth`, `getMaxIntrinsicWidth` cover layout and scroll-range needs.
- `Paragraph::getWordBoundary(offset)` covers word movement/selection.

Upstream SkUnicode primary APIs used by the spike:

- `SkUnicode::computeCodeUnitFlags(utf8, len, replaceTabs, &flags)`.
- `SkUnicode::CodeUnitFlags::kGraphemeStart`.
- `SkUnicode::CodeUnitFlags::kGlyphClusterStart`.
- `SkUnicode::convertUtf8ToUtf16` / `convertUtf16ToUtf8` for offset mapping support.

Primary source references:

- SkParagraph header: <https://github.com/google/skia/blob/main/modules/skparagraph/include/Paragraph.h>
- SkParagraph implementation: <https://github.com/google/skia/blob/main/modules/skparagraph/src/ParagraphImpl.cpp>
- SkUnicode header: <https://github.com/google/skia/blob/main/modules/skunicode/include/SkUnicode.h>

## Answers Required By #39

### Hit-Test Unit

`getGlyphPositionAtCoordinate` returns `PositionWithAffinity`; upstream implementation calls `ensureUTF16Mapping()` before returning line hit-test results. The adjacent closest-cluster implementation treats `result.position` as a UTF-16 offset and converts it through `fUTF8IndexForUTF16Index`.

Conclusion: treat hit-test result position as UTF-16. Convert it to Milestro's canonical UTF-8 byte offset through a Milestro-owned table. Preserve affinity.

### Grapheme / Shaping Cluster Boundaries

SkUnicode has `computeCodeUnitFlags` and flags for `kGraphemeStart` and `kGlyphClusterStart`.

SkParagraph also provides cluster APIs:

- UTF-8: `getGlyphClusterAt`.
- Point-based: `getClosestGlyphClusterAt`.
- UTF-16: `getGlyphInfoAtUTF16Offset`.

Conclusion: use SkUnicode flags to build the editor boundary table, then use SkParagraph cluster info for geometry. Do not allow caret movement/delete/selection cuts inside a grapheme or shaping cluster. The spike sample covers ASCII, CJK/Japanese, combining mark, precomposed accent, emoji skin-tone/ZWJ/family, Thai, and RTL/LTR mixed text.

### Range Rect / Selection Boxes

`getRectsForRange` returns `std::vector<TextBox>`, not one rectangle. Upstream implementation snaps partial grapheme selections to grapheme edges before collecting boxes per line.

Conclusion: mixed bidi selection can be represented as multiple visual boxes. Phase 1/3 should keep selection drawing as a list of rects with direction, not a single logical rectangle.

### Caret Rect + Affinity

SkParagraph does not expose a single "caret rect" function, but the ingredients exist:

- Hit-test: `PositionWithAffinity`.
- Glyph/grapheme geometry: `getGlyphInfoAtUTF16Offset` or `getClosestUTF16GlyphInfoAt`.
- Line metrics: `getLineMetricsAt`, `getLineNumberAtUTF16Offset`.
- Selection/collapsed geometry can be derived from nearby glyph cluster boxes plus affinity and line metrics.

Conclusion: caret rect is computable, but Milestro should wrap it as a native helper instead of duplicating the heuristics in C#.

### Word Boundary / Line Metrics / Measured Size

Available from SkParagraph:

- `getWordBoundary`.
- `getLineMetrics`.
- `getLineMetricsAt`.
- `getLineNumberAtUTF16Offset`.
- `getHeight`.
- `getLongestLine`.
- `getMinIntrinsicWidth`.
- `getMaxIntrinsicWidth`.

Conclusion: enough for selection movement, layout bridge measurement, and scroll range.

### Single-Line Horizontal Auto-Scroll

Available primitives:

- viewport width from Unity `RectTransform`.
- content width from `getLongestLine` or `getMaxIntrinsicWidth`.
- caret x from native-computed caret rect.
- scroll offset maintained by InputBox model.

Conclusion: enough. Phase 1 should use `ensureCaretVisible(scrollX, caretRect, viewportWidth, contentWidth)` and clamp to `[0, max(0, contentWidth - viewportWidth)]`.

## Current Milestro Gaps

Current Milestro wrappers expose only:

- `Paragraph::layout`.
- `Paragraph::paint`.
- `Paragraph::splitGlyph`.
- `Paragraph::toSDF`.
- `Paragraph::toPath`.

The spike adds a native-only `Paragraph::unwrap()` helper so tests can compile against SkParagraph directly. It deliberately does not add C ABI or C# APIs yet.

## Suggested Minimum Native Wrapper List

Add these before Phase 1 editor model work:

- `ParagraphHitTest(x, y) -> { utf16Offset, utf8Offset, affinity }`.
- `ParagraphGetRangeRects(utf8Start, utf8End, heightStyle, widthStyle) -> RectList`.
- `ParagraphGetGlyphInfoAtUtf8Offset(utf8Offset) -> { clusterUtf8Start, clusterUtf8End, clusterUtf16Start, clusterUtf16End, rect, direction, isEllipsis }`.
- `ParagraphGetClosestGlyphInfo(x, y) -> same`.
- `ParagraphGetLineMetrics(line)`, `ParagraphGetAllLineMetrics`.
- `ParagraphGetMetrics() -> { height, longestLine, minIntrinsicWidth, maxIntrinsicWidth }`.
- `UnicodeBuildTextBoundaryMap(utf8Text) -> immutable boundary/map handle`.
- `BoundaryMapUtf8ToUtf16`, `BoundaryMapUtf16ToUtf8`.
- `BoundaryMapSnapUtf8(offset, mode)` for previous/next/nearest grapheme and glyph-cluster boundary.

The C# wrapper should consume these as data handles/lists in the existing Milestro style, not JSON.

## Risks

- Public SkParagraph APIs expose mixed offset units. Milestro must not expose raw SkParagraph offsets as editor positions.
- `getRectsForRange` is UTF-16-facing; feeding UTF-8 offsets will corrupt selection geometry for non-ASCII.
- Caret geometry at bidi run boundaries needs affinity; storing only a logical offset is insufficient.
- SkUnicode grapheme boundaries and SkParagraph glyph clusters are related but not identical. Use a boundary table that prevents both user-perceived character splits and shaping-cluster splits.
- The local agent machine has no configured Skia include/lib inputs and no `cmake`, so this spike source was not built here.

## Phase 1 Minimum Interface Boundary

Phase 1 should avoid full editor complexity and expose only:

- one UTF-8 text buffer;
- one logical caret `{utf8Offset, affinity}`;
- optional selection disabled or collapsed only;
- boundary-map snap/move/delete helpers;
- paragraph layout cache;
- hit-test -> snapped caret;
- native caret rect helper;
- horizontal scroll offset and `ensureCaretVisible`.

IME, selection UI, clipboard, mixed-bidi visual movement, multi-line editing, layout bridge, read-only scroll productization, worldspace, and batching remain later phases.

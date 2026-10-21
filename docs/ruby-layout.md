# Horizontal Ruby layout

Ruby uses the normal `ParagraphBuilder::Build()`, `Paragraph::layout()` and
`Paragraph::paint()` pipeline. It also participates in line metrics, visitors,
paths, original-text range queries and hit testing. Paragraphs without Ruby retain
the upstream layout path.

## Input

Add original base text, then register a half-open range of **UTF-8 byte offsets**:

```cpp
builder.addText("漢字後");
builder.addRuby({{0, 6}, "かんじ"});
auto paragraph = builder.Build();
paragraph->layout(200);
paragraph->paint(canvas, 0, 0);
```

Milestro's C++ wrapper exposes `addRuby(startUtf8, endUtf8, text, length)` and a
`RubyStyle` overload; the C ABI exposes both the original entrypoint and
`MilestroSkiaTextlayoutParagraphBuilderAddRubyWithStyle`. Unity exposes explicit
`AddRubyUtf8` and `AddRubyUtf16` range methods. `AddRubyText(baseText, annotation,
style)` is the preferred managed API when adding new text because callers do not
need to calculate either form of offset.
For example, `AddText("漢字後"); AddRuby(0, 6, "かんじ");` annotates the first two characters.
Ruby does not insert characters into the base text. Native APIs that accept UTF-16
offsets continue to refer to the original base text; byte and UTF-16 offsets are
different for non-ASCII input.

```csharp
var rubyStyle = new RubyStyle
{
    Position = RubyPosition.Above,
    Alignment = RubyAlignment.SpaceAround,
    FontSizeMultiplier = 0.5f,
    Gap = 2,
};
builder.AddRubyText("漢字", "かんじ", rubyStyle);
builder.AddText("後");
```

Annotations can be registered out of order, but cannot overlap. Range validity is
checked at `Build()`; Unicode, shaped-cluster and bidi validity are checked at
`layout()`. C ABI calls return `MILESTRO_API_RET_FAILED` when validation throws.
`AddRuby` therefore succeeding does not guarantee a later build/layout succeeds.
`Reset()` clears annotations.

## Width and breaks

- Each registered range forms one atomic Ruby group. Its base and annotation stay
  on the same line. Separate adjacent groups can wrap independently.
- By default, the allocated width is the larger of the base advance/ink extent and
  annotation advance/ink extent. `RubyOverhang::Allow` instead allocates the base
  width and permits annotation ink outside that cell; it does not perform collision
  avoidance with neighboring annotations.
- `RubyAlignment` supports visual `Start`, `Center`, `End`, `SpaceAround` and
  `SpaceBetween`. Start/end follow the resolved bidi direction. Distributed modes
  add spacing to whichever side is shorter.
- An annotation occupies one line above or below the base. It inherits the first
  base style unless `RubyStyle::textStyle` is supplied, then applies
  `fontSizeMultiplier`. The default multiplier is one half and the default gap is
  two units. Actual font metrics contribute to line height.
- ICU soft and hard breaks remain authoritative. Emergency wrapping preserves raw
  graphemes and shaped clusters, opening/closing punctuation restrictions, NBSP,
  word joiners and combining sequences. An unbreakable group or sequence wider than
  the available width overflows; fonts are not silently reduced.
- `RubyOverflow::PreserveBase` is the default. For an oversized group it moves base
  and annotation together toward the available width while keeping preceding visual
  cells clear. The allocated/intrinsic width stays unchanged. A zero-width container,
  a base wider than the container, or surrounding unbreakable punctuation may still
  prevent the base from fitting. `RubyOverflow::Inline` retains inline centering.
- Ellipsis removes complete Ruby groups. Hidden groups emit no annotation glyphs
  or base selection boxes. Justification distributes space around groups and keeps
  internal base spacing unchanged.
- Hard breaks preserve empty lines, including the final empty line. Maximum line
  count, trailing spaces, text-height behavior and surrounding placeholders use
  the normal paragraph line representation.

## Bidirectional text

Base glyph order comes from the normal ICU bidi analysis and HarfBuzz shaping.
LTR/RTL paragraphs, mixed scripts, numbers, explicit isolates/overrides and wrapped
RTL lines retain native visual order. Annotations are independently shaped in their
annotation paragraph, with the parent's paragraph direction.

A base range must stay within one resolved bidi level. Ranges crossing levels or
containing directional controls are rejected. Controls may surround a valid range.
Ruby ranges must also coincide with raw grapheme and shaped-cluster boundaries;
they cannot contain placeholders or hard breaks. Annotation text must be nonempty,
single-line and contain no tabs.

## Queries and cache

`visit()` and `extendedVisit()` include annotation glyphs on their base line, marked
with `Paragraph::kRubyAnnotation_VisitorFlag`. Each annotation glyph maps to its base
range; annotation text does not gain independent positions in the source text.
`getPath()` includes base and annotation outlines.

Normal range/glyph/caret queries return base-text layout cells, including Ruby width
padding. They do not expose independent annotation selection, editing or ink boxes.
Partial-range cells can differ from visible base ink when an oversized group is moved;
precise Ruby-specific ink queries and annotation hit testing remain follow-up work.

Only unchanged base shaping is shared through `ParagraphCache`. Ruby detaches glyph
storage before adding padding, and restores placement offsets on width changes.
Different annotations and independent measurement/render paragraphs cannot modify
one another's cached base shaping.

## Deferred work

Multi-layer annotations and dedicated measurement APIs remain separate work.
The Unity rich-text parser supports the single-base HTML subset
`<ruby>base<rt>annotation</rt></ruby>`, optional `<rp>` fallback text, standard
CSS `ruby-position`, `ruby-align` and `ruby-overhang`, plus documented
`--milestro-ruby-*` custom properties. InputBox editing intentionally remains
plain-text-only. Vertical text/shaping is not implemented.

## Validation

The normal CMake test tree registers `MilestroTest_Ruby` and
reuses the existing Skia/ICU targets. It also covers the exported API in
`MilestroTest_SkiaParagraphHitTest`. Use the existing Visual Studio developer shell:

```powershell
cmake --build cmake-build-nocli-debug
ctest --test-dir cmake-build-nocli-debug --output-on-failure
```

Ruby tests generate CPU-rendered PPM samples under the build tree's
`tests/ruby/results` directory.

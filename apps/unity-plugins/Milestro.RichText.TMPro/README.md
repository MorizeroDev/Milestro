# Milestro.RichText.TMPro

TextMesh Pro rich-text support for Milestro, registered as `tmpro`.

The parser has no TextMesh Pro dependency. Set the text component's rich-text
parser to **TMPro**, or assign `TMProRichTextParser.Id` to `richTextParserId`.
It builds paragraph segments directly without generating intermediate Milestro
markup.

## Supported syntax

- Styles: `<b>`, `<i>`, `<u>`, `<s>`, `<strikethrough>`, `<sup>`, and `<sub>`.
- Color and fonts: `<color>`, `<#rgb>`, `<#rrggbb>`, `<font>`, and
  `<font-weight>`. `<alpha=#aa>` adjusts foreground opacity without replacing
  its RGB color.
- Metrics: `<size>`, `<cspace>`, `<voffset>`, and `<line-height>`. Absolute
  values and `px` are supported where meaningful; `<size>` also accepts `%` and
  `em`, `<voffset>` also accepts `%` and `em`, and `<line-height>` accepts `%`
  and `em` as a run-level line-height multiplier.
- Presentation: `<mark>`, `<uppercase>`, `<allcaps>`, `<lowercase>`,
  `<smallcaps>`, and `<nobr>`.
- Paragraph layout: `<indent>`, `<line-indent>`, `<margin>`, `<margin-left>`,
  `<margin-right>`, and `<width>`, with `px`, `em`, and `%` values. These tags
  create Milestro paragraph blocks. `line-indent` controls first-line indent;
  `width` limits the available inline extent after margins.
- Structure and interaction: `<align>`, `<br>`, `<link>`, and `<noparse>`.

Tag names are case-insensitive. Unclosed opening tags close at the end of the
input. An unsupported or mismatched tag logs a Unity warning and is emitted as
literal text; its contents continue to parse normally. Tags inside `<noparse>`
are always literal and do not produce warnings.

Resource-dependent and layout-specific TMPro tags such as `<sprite>`,
`<gradient>`, `<style>`, `<page>`, `<pos>`, `<rotate>`, and `<flush>` are
currently unsupported.

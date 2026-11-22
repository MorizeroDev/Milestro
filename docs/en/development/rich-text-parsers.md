# Custom rich-text parsers

[English](rich-text-parsers.md) | [简体中文](../../zh/development/rich-text-parsers.md)

To add your own markup format, implement `IRichTextParser.Parse(string source,
RichTextParseContext context)` in `Milestro.RichTextParser` and return a
`ParagraphPayload`.

## Registration

Provide an `IRichTextParserProvider` with `Id`, `DisplayName`, and
`CreateParser()`. Call `RichTextParserService.Register(provider)` before a text
box uses it. Register in the Editor too if you need Inspector previews.

IDs are case-sensitive and must match `[A-Za-z_][A-Za-z0-9_-]*`. They are stored
in serialized data, so keep published IDs stable. Registering the same ID
replaces the provider and cached parser; `Unregister(id)` removes it.
The service shares one parser instance per registration. Keep it stateless
between calls, and create a new `ParagraphPayloadBuilder` for each parse.

## Payloads

Use `new ParagraphPayloadBuilder(context.Locale)` for locale-aware case
conversion. The context also supplies `AllowViewportBlockMargins` and an
optional `Issues` collection for non-fatal diagnostics.

| Operation | Behavior |
| --- | --- |
| `AppendText` | Inherits component styling when no `TextStyleState` is supplied |
| `BeginBlock` / `EndBlock` | Creates paragraph blocks without separator characters; use `\n` within a block |
| `BeginLink` / `EndLink` | Marks a link range; links cannot nest |
| `AppendPlaceholder` | Adds a positive, finite box that counts as one UTF-16 unit |
| `Build` | Finalizes the payload, closes an open link, and commits pending no-wrap ranges |

The builder applies `TextStyleState.TextTransform` and computes UTF-16 offsets
after transformation. `NoWrap` ranges can span styled text and placeholders.
`BeginBlock(ParagraphStyleState, TextStyleState)` copies paragraph settings and
captures the text style for indentation. Block boundaries finalize pending
no-wrap ranges too.

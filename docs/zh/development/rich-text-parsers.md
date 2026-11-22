# 自定义富文本解析器

[English](../../en/development/rich-text-parsers.md) | [简体中文](rich-text-parsers.md)

要接入自己的标记格式，在 `Milestro.RichTextParser` 命名空间下实现
`IRichTextParser.Parse(string source, RichTextParseContext context)`，返回 `ParagraphPayload`。

## 注册

实现 `IRichTextParserProvider` 的 `Id`、`DisplayName` 和 `CreateParser()`，
然后在文本框使用前调用 `RichTextParserService.Register(provider)`。
需要 Inspector 预览时，也要在 Editor 中注册。

ID 区分大小写，必须匹配 `[A-Za-z_][A-Za-z0-9_-]*`。它会存入序列化数据，发布后应保持稳定。
同 ID 注册会替换提供者和已缓存的解析器；`Unregister(id)` 移除注册。
一次注册会共享一个解析器实例，因此调用之间不能保留解析状态，每次解析应新建 builder。

## 载荷

使用 `new ParagraphPayloadBuilder(context.Locale)`，让大小写转换遵循组件语言。
context 还提供 `AllowViewportBlockMargins` 和可选的非致命诊断集合 `Issues`。

| 操作 | 行为 |
| --- | --- |
| `AppendText` | 不传 `TextStyleState` 时继承组件样式 |
| `BeginBlock` / `EndBlock` | 创建段落块，不插入分隔字符；块内换行用 `\n` |
| `BeginLink` / `EndLink` | 标记链接范围，链接不能嵌套 |
| `AppendPlaceholder` | 添加正数、有限尺寸的占位框，占一个 UTF-16 单元 |
| `Build` | 生成载荷，关闭未结束的链接，并提交待处理的禁止换行范围 |

builder 应用 `TextStyleState.TextTransform`，并按转换后的文本计算 UTF-16 偏移。
`NoWrap` 范围可以跨样式片段和占位符。
`BeginBlock(ParagraphStyleState, TextStyleState)` 复制段落设置，并记录解析缩进所需的文字样式。
块边界也会提交待处理的禁止换行范围。

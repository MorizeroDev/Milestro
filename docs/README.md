# Milestro documentation / Milestro 文档

Choose a language / 选择语言：

- [English manual](en/README.md)
- [简体中文手册](zh/README.md)
- [Project README](../readme.md) / [项目说明](../readme.zh-CN.md)

The manuals cover installing and using Milestro in Unity. Source builds,
implementation details, and validation tools have separate developer entry points:

- [Developer documentation](en/development/README.md)
- [开发文档](zh/development/README.md)

用户手册介绍如何在 Unity 中安装和使用 Milestro。源码构建、实现细节和测试工具
放在独立的开发文档中。两种语言共用 [examples/](examples/) 下的 XML 示例。

## Maintaining documentation / 文档维护

Keep the same page paths under `en/` and `zh/`. When behavior changes, update
both pages, their language-switch links, and runnable examples together.
Preserve API identifiers, package names, and resource paths in their original
form. Use repository-relative links and portable placeholders for external paths.

Keep `getting-started/` and `guides/` focused on user tasks. Put exact settings
in `reference/`, and implementation, build, and test details in `development/`.
Remove outdated experiments and speculative designs from user guides.

`en/` 与 `zh/` 使用相同的页面路径。行为变更时，同步更新两种语言、语言切换链接和
可运行示例。API 标识符、包名、资源路径保持原样；仓库内使用相对链接，仓库外路径使用
可移植占位符。组件/API 的事实依据是源码，平台构建参数以 CI 工作流为准。

`getting-started/` 和 `guides/` 只保留完成用户操作所需的说明，准确参数放在
`reference/`，实现、构建和测试细节放在 `development/`。用户指南应使用简短、
连贯的语言，不堆叠术语；过时实验和未实现功能的设想可以直接删除。

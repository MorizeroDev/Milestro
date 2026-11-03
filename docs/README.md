# Milestro documentation / Milestro 文档

Choose a language / 选择语言：

- [English manual](en/README.md)
- [简体中文手册](zh/README.md)
- [Project README](../readme.md) / [项目说明](../readme.zh-CN.md)

Both manuals cover installation, Unity components, typography, input, rendering,
and building from source. XML showcase assets are shared in [examples/](examples/).

两种语言的手册均包含安装、Unity 组件、排版、输入、渲染和源码构建说明。
XML 展示素材共用 [examples/](examples/) 下的文件。

## Maintaining documentation / 文档维护

Keep the same page paths under `en/` and `zh/`. When behavior changes, update
both pages, their language-switch links, and runnable examples together.
Preserve API identifiers, package names, and resource paths in their original
form. Use repository-relative links and portable placeholders for external paths.

`en/` 与 `zh/` 使用相同的页面路径。行为变更时，同步更新两种语言、语言切换链接和
可运行示例。API 标识符、包名、资源路径保持原样；仓库内使用相对链接，仓库外路径使用
可移植占位符。组件/API 的事实依据是源码，平台构建参数以 CI 工作流为准。

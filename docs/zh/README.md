# Milestro 简体中文手册

[English](../en/README.md) | [简体中文](README.md)

Milestro 通过原生 Skia 管线在 Unity 中渲染文本。无需从源码构建即可尝试预编译包。
这份手册按“先运行，再选择功能，最后查阅参数”的顺序组织。

## 开始使用

| 顺序 | 页面 | 目标 |
| --- | --- | --- |
| 1 | [安装 Unity 插件](getting-started/install.md) | 导入完整插件与资源 |
| 2 | [显示第一段文本](getting-started/first-text.md) | 在场景里运行 `TextBox` |
| 3 | [创建第一个输入框](getting-started/first-input.md) | 编辑、提交并监听事件 |
| 4 | [排查问题](getting-started/troubleshoot.md) | 按症状检查空白输出、字体和焦点 |

最短流程：下载 `milestro-unity-plugins`，将归档解压进 `Assets/`，添加两个必需的 ParaParty 包，
然后运行第一段文本示例。

## 选择组件

| 需求 | 组件 | 说明 |
| --- | --- | --- |
| 多行、富文本、链接、Ruby 或竖排 | `TextBox` | [文本与字体](guides/text.md) |
| 单行 UI 标签 | `CanvasSlimText` | [SlimText](guides/slim-text.md) |
| 世界空间段落 | `WorldSpaceTextBox` | [世界空间文本](guides/world-space.md) |
| 世界空间单行标签 | `WorldSpaceSlimText` | [世界空间文本](guides/world-space.md) |
| 可编辑输入 | `TextInput` | [输入与 IME](guides/input.md) |
| 单选设置项 | `MilestroDropdown` | [下拉框](guides/dropdown.md) |
| 自定义排版或编辑器 | `ParagraphBuilder` / `InputBox` | [排版引擎](reference/layout-engine.md) |

`TextBox` 自动创建 `TextBoxRenderTextureProducer`，内容与样式设置在 producer 上。
`InputBox` 是 `TextInput` 拥有的底层原生编辑模型，不是可添加到场景的组件。

## 功能指南

- [文本、字体与富文本](guides/text.md)：字体族、字重、间距、标签、Ruby、占位符、竖排。
- [SlimText](guides/slim-text.md)：单行轻量文本及 UTF-8 更新。
- [布局与尺寸](guides/layout.md)：换行、溢出、边距、内容尺寸、自适应字号、渐变。
- [输入与 IME](guides/input.md)：验证、事件、焦点、移动软键盘。
- [下拉框](guides/dropdown.md)：初始化、候选与提交、翻译刷新、弹出列表布局。
- [滚动与滚动条](guides/scrolling.md)：视口滚动、滚动条连接、Elastic。
- [链接与无障碍](guides/links-and-accessibility.md)：点击、导航、屏幕阅读器。
- [世界空间文本](guides/world-space.md)：3D 标签、纹理分辨率与合批。
- [从 TextMesh Pro 迁移](guides/coming-from-tmpro.md)：概念与 API 对照。
- [富文本展示示例](examples/README.md)：横排、日文竖排、繁体中文竖排 XML。

## 参考资料

- [Unity 组件与事件](reference/components.md)
- [运行时配置](reference/configuration.md)
- [排版引擎参数](reference/layout-engine.md)
- [渲染、合批与后端](reference/rendering.md)
- [HybridInput 提供者契约](reference/hybrid-input.md)
- [已知限制](reference/limitations.md)
- [Ruby 排版](reference/ruby-layout.md)
- [TextInput 无障碍设计](reference/accessibility-design.md)
- [源码构建与 CI](reference/build.md)

## 基本要求

使用 Unity 6。屏幕阅读器支持还需要 Unity 6000.3+ 及内置 `com.unity.modules.accessibility`。
核心程序集依赖 `party.para.util.colors` 和 `party.para.util.unitynative`。
可编辑 `TextInput` 需要 `[1.16.0,2.0.0-0)` 的 `com.unity.inputsystem` 与
`InputSystemUIInputModule`。运行平台和架构必须有配套原生插件；托管与原生文件必须来自同一修订。

其他入口：[项目 README](../../readme.zh-CN.md)、[仓库目录](../../readme.zh-CN.md#仓库目录)、
[Android Unity 设备验证](../../tests/android_unity/README.md)。

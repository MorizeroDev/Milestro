# Milestro

[English](readme.md) | [简体中文](readme.zh-CN.md)

Milestro 基于 Skia 与 ICU，为 Unity 提供面向阅读的多语言文本排版与渲染。

阅读界面需要的不只是把字显示出来，还包括自然的标点间距、多语言混排、字体回退、
注音和稳定的段落布局。Milestro 致力于让阅读视图、对话、本地化界面和可编辑文本
更容易获得合适的排版效果。

项目仍处于概念验证和持续开发阶段，API 可能变化；当前以 `Assets/` 插件形式分发。

## 让文字读起来更舒服

恰到好处的标点间距、自然的中外文混排、顺畅的段落节奏，让读者把注意力留给内容。
从一句对话到一整页文章，Milestro 希望让文字在默认设置下就有适合阅读的样子。

## 我们的特色

- **中文与竖排排版。** Canvas `TextBox` 支持 `vertical-rl`、直立与侧转文字混排，
  以及将短横排片段放入竖列的纵中横。
- **Ruby 注音。** 为基文添加读音，控制注音字号、间距、对齐和外伸。
- **按字体族选择字体。** 注册字体文件，设置有序 family 列表，结合原生与系统字体回退，
  无需为组件指定 `TMP_FontAsset`。
- **丰富的标记能力。** Milestro Markup 提供行内样式、段落边距、局部禁止换行、
  大小写转换、背景、链接、占位符，以及按元素 ID 查询几何位置。
- **复用已有内容。** 可选 Markdown 与 TMPro 解析器共用段落渲染管线；
  TMPro 兼容层让迁移到 Milestro 组件的项目继续使用受支持的 TMP 标记。
- **与界面配套的文本能力。** 提供布局尺寸报告、滚动、滚动条、带 IME 的可编辑输入，
  以及可选无障碍辅助组件。

竖排目前面向固定高度的 Canvas `TextBox`。各平台和组件的适用范围见
[已知限制](docs/zh/reference/limitations.md)。

## 在 Unity 中开始

从 GitHub Releases 下载 `milestro-unity-plugins.tar.gz`，或从
**Formal Build and Release** 工作流下载 `milestro-unity-plugins` artifact。
将归档解压到 Unity 6 项目的 `Assets/`，保留 `Resources/` 和 `.meta` 文件。
必需依赖与配置步骤见[安装指南](docs/zh/getting-started/install.md)。

段落使用 `TextBox`，短单行标签使用 `CanvasSlimText`，可编辑字段使用 `TextInput`；
也提供世界空间文本组件。

```csharp
using Milestro.Components.Internal;
using UnityEngine;

var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();
producer.richTextParserId = Milestro.RichTextParser.XmlRichTextParser.Id;
producer.content = "用 Milestro 阅读<ruby>漢字<rt>かんじ</rt></ruby>。";
producer.textStyle.size = 40f;
producer.textStyle.textColor = Color.white;
```

## 继续了解

- [第一段文本](docs/zh/getting-started/first-text.md)与[第一个输入框](docs/zh/getting-started/first-input.md)。
- [文本、字体与标记](docs/zh/guides/text.md)，包括默认排版行为。
- [标记展示示例](docs/zh/examples/README.md)，包含横排与竖排 XML。
- [从 TextMesh Pro 迁移](docs/zh/guides/coming-from-tmpro.md)。
- [简体中文手册](docs/zh/README.md) / [English manual](docs/en/README.md)。
- 贡献者可查阅[源码构建](docs/zh/reference/build.md)与[渲染后端](docs/zh/reference/rendering.md)。

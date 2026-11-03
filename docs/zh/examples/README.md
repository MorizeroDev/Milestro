# 富文本展示示例

[English](../../en/examples/README.md) | [简体中文](README.md)

以下 XML 文件可直接作为 TextBoxRenderTextureProducer.content，两种手册共用同一份素材。
请选择 **Milestro Rich Text**（`milestro_rich_text`）解析器。

| 文件 | 书写模式 | 内容 |
| --- | --- | --- |
| [横排展示](../../examples/horizontal-example.xml) | HorizontalTb | 行内样式、双向段落、对齐、边距、OpenType、链接、Ruby、占位符 |
| [日文竖排](../../examples/vertical-rl-ja-example.xml) | VerticalRl | 方向、纵中横、列对齐、缩进、Ruby、占位符 |
| [繁体中文竖排](../../examples/vertical-rl-zh-Hant-example.xml) | VerticalRl | 同类排版能力与读音注释 |

## 显示素材

按 [入门](../getting-started/first-text.md) 创建 Canvas TextBox，固定非零尺寸，例如 1000 × 800，使用深色背景。
将 XML 全文复制到 Content，或将 XML 放入 Assets，导入为 TextAsset 后赋给脚本的 showcaseAsset。
XML 不设置书写模式，组件必须手动选择：

```csharp
using Milestro.Components.Internal;
using Milestro.Model;
using UnityEngine;

var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();
producer.richTextParserId = Milestro.RichTextParser.XmlRichTextParser.Id;
producer.enableAutoFontSize = false;
producer.textStyle.size = 32f;
producer.textStyle.textColor = Color.white;
producer.paragraphStyle.singleLine = false;
producer.paragraphStyle.textDirection = TextDirection.Ltr;
producer.paragraphStyle.textAlign = TextAlign.Start;
producer.paragraphStyle.paragraphSpacing = 12f;
producer.paragraphStyle.writingMode = WritingMode.HorizontalTb;
producer.wrapMode = TextBoxWrapMode.Wrap;
producer.textOverflow = TextOverflow.Overflow;
producer.content = showcaseAsset.text;
```

显示任一竖排示例时，将 writingMode 改为 WritingMode.VerticalRl。
内容超过一屏：横排向下滚，竖排向左看后续列。指针交互需 EventSystem，可用 TextBoxScrollbar，见
[滚动](../guides/scrolling.md)。比较字号和偏移时保持 auto font size 关闭。
外层 font size=32 与组件字号一致，使块边距 em 采用相同参考。
横排改宽度观察换行，竖排改高度观察换列。

## 预期结果与限制

- 请求的 Google Sans、Source Han Sans VF、Noto Color Emoji 等不随包附带；字形、字重、地区替换取决于实际字体。
- 链接只返回应用数据，不自动打开浏览器。
- img 仅占位，应用需通过 TryGetPlaceholderGeometrySnapshot 放置视觉对象；未接入时留白正常。
- 横排相邻段落上下边距折叠。竖排上下边距是物理垂直 inset，右左边距折叠后与 paragraphSpacing 一起控制段间水平距离。
- 竖排示例标题的 margin-right/left 分别设置块前、块后间距。
- 竖排正基线偏移向右、负偏移向左；首列缩进沿 Y。Ruby over 在右侧，under 降级到同侧。
- 竖排示例排除尚不支持的负字距与 RTL；中文注释使用 Ruby，不意味着另有独立注音符号排版引擎。
- font-feature-settings: normal 清空包含默认 chws 的全部继承特性，不恢复预设列表。

继续阅读 [富文本](../guides/text.md#rich-text)、[竖排](../guides/text.md#vertical-writing)、
[Ruby](../reference/ruby-layout.md)。

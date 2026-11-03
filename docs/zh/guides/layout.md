# 布局与尺寸

[English](../../en/guides/layout.md) | [简体中文](layout.md)

布局顺序是：整体边距 → 可用内容框 → 换行 → 对齐 → 溢出/滚动，另可启用字号拟合。
`RectTransform` 提供外部尺寸，producer 测量内容；`TextBoxLayoutElement` 可向 Unity 布局系统报告所需尺寸。

## 固定文本框

适合对话框、HUD 等设计尺寸固定的区域。设置非零 RectTransform，不添加布局元素，按需启用换行：

```csharp
using Milestro.Components.Internal;
using Milestro.Model;

var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();
producer.wrapMode = TextBoxWrapMode.Wrap;
producer.textOverflow = TextOverflow.Clip;
producer.paragraphStyle.textAlign = TextAlign.Start;
```

`Clip` 裁剪；`Ellipsis` 省略；`Overflow` 保留溢出内容及可滚动范围。
`paragraphStyle.singleLine = true` 优先于 `wrapMode`，强制单行。
省略使用 `ellipsisString`，不会自动缩小字体。

## 随内容改变尺寸

```csharp
using Milestro.Components;
using Milestro.Model;
using UnityEngine.UI;

var layout = textBox.gameObject.AddComponent<TextBoxLayoutElement>();
layout.widthMode = TextWidthMode.FitContent;
layout.heightMode = TextHeightMode.Auto;
var fitter = textBox.gameObject.AddComponent<ContentSizeFitter>();
fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
```

| 模式 | 首选尺寸 |
| --- | --- |
| 宽 `Fixed` | 不报告，外部分配宽度 |
| 宽 `MaxContent` | 完整不换行宽度 |
| 宽 `FitContent` | `max(min-content, min(max-content, parent width))` |
| 高 `Fixed` | 不报告，外部分配高度 |
| 高 `Auto` | 按最终宽度换行后的完整高度，含边距和块间距 |

`NoWrap` 时 `FitContent` 与 `MaxContent` 结果相同。固定轴将 fitter 对应轴设为 `Unconstrained`。
父 `LayoutGroup` 可直接读取 `ILayoutElement`，无需 fitter。

<a id="vertical-writing-vertical-rl"></a>
## 竖排尺寸与对齐

Canvas `TextBox` 设为 `WritingMode.VerticalRl` 后，高度约束列内换行，新增列消耗物理宽度。
可固定整个框，或保持 `heightMode = Fixed`、垂直 fitter 为 `Unconstrained`，
横向选择 `MaxContent` / `FitContent` 并使用水平 fitter。长且不可断开的词留在一列内溢出，不任意拆字。

`text-indent` 支持 `px`、`em`、`%`、零和负值，只影响源段落首列；正值向下，负值悬挂到上方。
软换列与硬换行后的列无首行缩进。有限 Wrap 的百分比基于减去上下边距的内容高度；
NoWrap/单行的无限行内长度使百分比解析为零。
首列 `Start`/`End`/`Center` 在缩进后剩余空间对齐，后续列使用完整高度。
`Justify` 只分配有限软换列中的剩余空间；硬换行列、末列、单行、不换行和溢出内容保留普通对齐。

下划线和删除线沿竖排片段绘制，链接悬停照常工作。原生背景和阴影使用最终竖排几何，
但 Unity `TextBox` 尚未暴露其设置。`sup`、`sub`、`voffset`、`voffset-em` 不改行内推进：
正偏移/上标向右，负偏移/下标向左，可扩大列框、内容宽度和滚动范围。

## 边距

`producer.margin` 的四边均为 `MarginValue`，支持 `Px`、`ContainerPercent`、`Vw`、`Vh`、`Em`。

```csharp
using Milestro.Model;

var margin = producer.margin;
margin.Left.Value = 24f;
margin.Left.Unit = MarginUnit.Px;
margin.Right.Value = 24f;
margin.Right.Unit = MarginUnit.Px;
margin.Top.Value = 1.5f;
margin.Top.Unit = MarginUnit.Em;
```

`Auto` 在求视口大小时为零，之后可吸收正的剩余空间。竖排 X 按完整列框宽度放置，Y 按实际内容边界放置，
不改变裁剪或滚动范围。横竖排的百分比和视口单位参考不同，精确规则见
[单位与坐标](../reference/layout-engine.md#units-and-coordinates)。
富文本 `<p>` 的上下边距用于块布局，不等于整个文本框的边距。

<a id="auto-font-size"></a>
## 自适应字号

内容尺寸改变框，自适应字号改变框内字形：

```csharp
producer.textStyle.size = 48f;
producer.enableAutoFontSize = true;
producer.fontSizeMin = 24f;
producer.fontSizeMax = 48f;
```

宽高完全固定时可在范围内放大或缩小；任一轴随内容时最多到设计字号，只能缩小。
固定宽且换行时先重排，仅固定高度仍不足时才缩小。`Ellipsis` 路径不执行字号拟合。
更多边界条件见 [排版参考](../reference/layout-engine.md#auto-font-size)。

## 颜色渐变

`TextBox` 和 `TextInput` 支持 `TextGradient`，渐变在呈现网格上生成，改变渐变不触发段落重排或光栅化。

```csharp
using Milestro.Model;
using UnityEngine;

var stops = new Gradient();
stops.SetKeys(
    new[] { new GradientColorKey(Color.cyan, 0f), new GradientColorKey(Color.magenta, 1f) },
    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
textBox.enableColorGradient = true;
textBox.colorGradient = TextGradient.Linear(stops, 90f);
```

模式为 `FourCorners`、`Linear`、`Radial`。后两者使用 `MeshResolution`，默认 16，限制 2..64。
渐变映射到显示四边形而非字形墨迹边界，并与 `Graphic.color` 相乘。
可通过 **Create → Milestro → Text Gradient** 创建预设。

## 常见问题

| 症状 | 优先检查 |
| --- | --- |
| 太小 | 设计字号、自适应字号、布局模式 |
| 裁切 | 溢出模式、框大小、边距 |
| 不换行 | Wrap、singleLine、可用宽/高 |
| 超出设计范围 | Overflow、字号拟合、父级布局 |
| 布局组将框压扁 | 布局元素模式、fitter 配置 |

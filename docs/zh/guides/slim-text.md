# SlimText 单行文本

[English](../../en/guides/slim-text.md) | [简体中文](slim-text.md)

SlimText 是独立的轻量单行生产与渲染路径，适合名称、计数器、按钮文字和伤害数字。
它不是 `TextBox` 的模式，不支持换行、富文本、链接、Ruby、滚动或段落方向。

| 能力 | TextBox | SlimText |
| --- | --- | --- |
| 多行与语义标记 | 支持 | 不支持 |
| 字体 | 有序 family 列表 | 单个 `fontFamily` 加系统回退 |
| 自适应字号 | 支持 | 支持 |
| 内容尺寸 | 通过 `TextBoxLayoutElement` | producer 直接实现 `ILayoutElement` |
| UTF-8 低分配更新 | 无专用路径 | producer 与世界空间组件提供 |

## CanvasSlimText

在 Canvas 下创建 UI 对象，添加 **Milestro → Canvas Slim Text**，设非零 RectTransform。
组件自动添加 `RawImage` 和 `SlimTextRenderTextureProducer`。

```csharp
label.Text = "得分：1200";
label.TextColor = Color.white;
```

其中 `label` 为 `Milestro.Components.CanvasSlimText`。
更多设置通过 producer：

```csharp
using Milestro.Components.Internal;
using Milestro.Model;
using UnityEngine;

var producer = label.GetComponent<SlimTextRenderTextureProducer>();
producer.text = "第三波";
producer.fontFamily = "Inter";
producer.fontSize = 32f;
producer.fontWeight = FontWeight.SemiBold;
producer.color = Color.yellow;
producer.horizontalAlign = SlimTextHorizontalAlign.Center;
producer.verticalAlign = SlimTextVerticalAlign.Center;
producer.rectOffset = new RectOffset(8, 8, 2, 2);
producer.fallbackToSystemFont = true;
```

`fontFamily` 是单个 family 声明，不是段落的有序列表。Editor 新建组件的默认 family 随平台变化：
Windows 为 `Microsoft YaHei UI`，macOS 为 `PingFang SC`，Linux 为 `Noto Sans CJK SC`；
其他编译环境为 `system-ui`。已序列化的配置保持原值，实际字形取决于可用字体与回退。

## 尺寸

`Fixed` 使用指定宽/高。`MaxContent` 和 `FitContent` 宽度均为自然单行宽度，
`Auto` 高度包含完整行高和 `rectOffset`。父 LayoutGroup 可直接读取尺寸，`layoutPriority` 默认 1。

```csharp
producer.widthMode = TextWidthMode.FitContent;
producer.heightMode = TextHeightMode.Auto;
producer.enableAutoFontSize = true;
producer.fontSizeMin = 12f;
producer.fontSizeMax = 32f;
```

即使不换行，常规路径仍可按固定边界或 FitContent 的父级约束缩小字号。

## WorldSpaceSlimText

对带 RectTransform 的对象添加 **Milestro → World Space Slim Text**，自动管理 producer、网格呈现器、
MeshFilter 和 MeshRenderer。

| 设置 | 用途 |
| --- | --- |
| `text`、`fontFamily`、`fontSize`、`fontWeight`、`color` | 内容与基本样式 |
| `textureSizePixels`、`pixelsPerUnit` | 分辨率与世界尺寸 |
| `rectOffset`、`horizontalAlign`、`verticalAlign` | 内边距与对齐 |
| `widthMode`、`heightMode`、自适应字号组 | 尺寸策略 |
| `material`、`texturePropertyName` | 材质与采样属性 |
| `disableRendererWhenNoOutput` | 无输出时隐藏 |

多行世界空间文本使用 [WorldSpaceTextBox](world-space.md)。

## UTF-8 缓冲区更新

```csharp
using System.Text;

var buffer = new byte[128];
label.PrepareTextUtf8NoAlloc(buffer.Length);
var value = "Count: 1200";
var length = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);
label.SetTextUtf8NoAlloc(buffer, 0, length);
```

此处 `label` 是 `WorldSpaceSlimText`。producer 校验范围并将字节复制进可复用存储，
不保留调用方数组，也不生成中间托管字符串。热路径复用缓冲区与格式化器；字符串插值仍会分配，
需要时可用 `Utf8Formatter` 直接写字节。该专用路径不在每次更新时做字号拟合，保持配置字号。

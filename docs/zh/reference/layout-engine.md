# 排版引擎参数

[English](../../en/reference/layout-engine.md) | [简体中文](layout-engine.md)

本页描述 Unity 侧段落的测量、断行与绘制；操作配方见 [布局指南](../guides/layout.md)。

## 管线

```text
内容 → 纯文本或可选富文本解析 → 段落构建/样式 → 固有尺寸测量 → 尺寸求解
     → 行布局 → Skia 光栅化 → 可选渐变的 uGUI 网格呈现
```

解析器可生成多个段落块，每块构造 Skia paragraph。水平文本按向下的块轴堆叠，竖排按从右向左的块轴堆叠，
并应用边距和 `paragraphStyle.paragraphSpacing`。`<p>` 改变块结构但不创建额外 Unity 对象。

## TextBoxRenderTextureProducer

TextBox 在同一 GameObject 自动创建此配置对象。

| 成员 | 类型/默认值 | 含义 |
| --- | --- | --- |
| content | string / 空 | 纯文本或富文本 |
| richTextParserId | string? / null | 注册的富文本解析器 id：milestro_rich_text、模块提供的 markdown 或 tmpro；null 或空串按纯文本显示 |
| margin | Margin / 全零 | 段落视口内边距 |
| textStyle | TextStyleSettings | 文本片段样式 |
| paragraphStyle | ParagraphStyleSettings | 段落与块行为 |
| wrapMode | Wrap | Wrap 或 NoWrap |
| textOverflow | Clip | Clip、Ellipsis、Overflow |
| enableAutoFontSize | false | 自适应字形尺寸 |
| fontSizeMin / fontSizeMax | 12 / 72 | 拟合范围 |

运行时只读量包括 contentWidth、contentHeight、viewportWidth/Height、maxScrollX/Y、effectiveFontSize。

## TextStyleSettings

| 成员 | 默认 | 含义 |
| --- | --- | --- |
| fontFamilies | system-ui | 有序 family 和回退 |
| size | 36 | 设计字号 |
| weight | 400 | Skia 字重，通常 100..900 |
| width | Normal | 字宽轴 |
| slant | Upright | Upright、Italic、Oblique |
| textColor | white | 基础颜色 |
| letterSpacing / wordSpacing | 0 / 0 | 字符/词间附加点数 |
| lineHeight | 0 | 水平行框/竖直列框倍率，<=0 用自然度量 |
| locale | zh-Hans | 字形、断行与回退地区 |

字体在段落构建时解析；回退由 Skia 字体集合处理，不切换 Unity Font 资源。

## ParagraphStyleSettings

| 成员 | 默认 | 含义 |
| --- | --- | --- |
| textAlign | Start | Left、Right、Center、Justify、Start、End |
| textDirection | Ltr | Ltr/Rtl |
| writingMode | HorizontalTb | HorizontalTb/VerticalRl |
| singleLine | false | 强制单行 |
| ellipsisString | … | 省略字符 |
| paragraphSpacing | 0 | 相邻块额外间距 |

Start/End 跟随文字方向，Left/Right 为物理方向。Justify 分配可对齐行的间距，末行回到普通对齐。

<a id="units-and-coordinates"></a>
## 单位与坐标

布局单位是 producer 渲染空间的本地 UI 点/像素；margin 单位独立：

| MarginUnit | 横排 | vertical-rl |
| --- | --- | --- |
| Px | 原值 | 原值 |
| ContainerPercent | 左右按宽、上下按高 | 四边均按包含块宽度 |
| Vw / Vh | 容器宽/高的 1% | root Canvas 视口宽/高的 1% |
| Em | 当前有效字号 | 段落基础字号 |

Auto 在视口求解时为零。横排组件布局可将 Auto 边距用于吸收正的剩余空间。
固定尺寸竖排 Canvas TextBox 在原生布局后放置 Auto：X 使用完整物理列框宽度，Y 使用已排布内容范围，
并做统一段落平移；它不扩大裁剪或滚动范围，溢出保留现有锚点。
屏幕空间光栅比例影响纹理尺寸与品质，不影响逻辑段落几何。

## 换行与溢出

优先规则：`singleLine` 强制单行；否则 Wrap 按分配的宽度软换行；NoWrap 每个段落保持一行；
显式换行和 `<br>` 总会创建硬换行。Skia 使用 ICU 断行与双向文本数据，保留字素簇、塑形片段与双向顺序。
大于可用宽度且不可断开的序列仍会溢出，不会自动缩字号。

行内 `white-space: normal | nowrap` 控制局部软换行，也作用于竖排换列。
`nowrap` 保留硬换行，嵌套 `normal` 可恢复软换行；不支持 `pre` 或 `tab-size` 等保留空白模型。
`text-transform: none | lowercase | uppercase | smallcaps` 在载荷构建时使用 ICU 转换文本，
遵循组件 locale 和行内 `lang`，因此载荷 UTF-16 索引对应转换后的文本，不能直接用作原始标记字符串的偏移。
smallcaps 将原小写字母转为大写并使用有效字号的 80%。

`<mark>` 与 `background-color` 为文字片段添加背景，不改变前景色；详见
[文本样式](../guides/text.md)。背景绘制不扩大布局、固有尺寸或滚动范围。

Overflow 在布局后决定呈现：Clip 限制到视口；Ellipsis 按段落设置省略；Overflow 保留视口外绘制和命中几何，
组件可据此滚动。省略不是自适应字号触发器。

## 竖排与 lineHeight

VerticalRl 支持原生 Paragraph 和固定高度 Canvas TextBox，列从上到下、从右向左。视口高度约束行内轴；
改变宽度影响裁剪、滚动和水平位置，不改变列换行。

水平 `lineHeight` 调整物理 Y 行框厚度；竖排调整物理 X 列框厚度，目标为倍率乘有效字号，混合字号片段分别求值。
零或有限负数使用字体自然度量，正数 1 代表一 em 目标框，不一定等同自然列宽。
较小倍率可能使墨迹超出列框并覆盖邻列，布局/滚动不会因墨迹扩张，调用方裁剪独立处理。
非有限值是错误。底层 HalfLeading 控制均分额外空间，TextStyleSettings 没有单独开关。

竖排 text-indent 只作用源首列：正值向下，负值向上悬挂；软换和硬换后的列不缩进。
有限 Wrap 百分比按行内高度解析，NoWrap/单行使用无限行内范围，百分比为零。
Intrinsic 宽度不含百分比缩进，最终布局再解析。
首列 Start/End/Center 在去缩进后的空间对齐，之后列用完整高度；缩进超过行内范围时首列仍原子溢出。
justification 仅分配有限软换列中冻结的空白/CJK/Latin 断点自由空间；硬换列、末列、无限范围、零高与溢出保持 Start。

竖排 Ellipsis 在 maxLines 与行内长度均有限时截断最后列。省略字符作为无源范围的合成字形，
先删除完整的源字素簇/塑形簇；隐藏源不贡献选择框、链接、范围查询和内容尺寸。
Tab 按 HTML 默认空白行为近似一个 ASCII 空格，保留源范围；当前没有 white-space/tab-size 模式。
软连字符与显式双向控制不绘制、不占推进并按 LTR 规范源双向级别。竖排标点和 vert 字形由字体决定。

原生竖排背景可使用最终片段度量绘制，位于 glyph 前，且只扩展 inkBounds；Unity TextBox 没有背景设置。
原生阴影使用最终字形位置，偏移仍为物理 X/Y，在侧转绘制前应用；只影响绘制、不影响布局、路径、链接和范围。
基线偏移支持 sup/sub/voffset/voffset-em：普通基线后物理偏移为 `-baselineShift`，正值向右，负值向左；
不改变行内推进，但可扩大列两侧。它们不会使源内容改变。

## 竖排高级片段

`addTateChuYoko` 或 `text-combine-upright: all` 将片段横向重塑为原子单位，行内推进固定一 em。
默认 squeeze 以绘制变换压缩超宽片段；grow 保持自然宽度并对称扩大列厚度。
必须对齐字素簇/塑形簇边界、样式一致，且不交叠硬换行、占位符或 Ruby；Visitor 带对应 flag，命中仍用源索引。

`addOrientationOverride` 或 rich text `text-orientation: mixed | upright | sideways` 覆盖 Unicode 方向分类。
覆盖按字素簇拆片，必须在字素边界且互不交叠；不影响占位符和零宽控制，横排忽略。
sideways 顺时针转 90°，并忽略纵中横。Ruby 注音始终按 mixed；其它列厚、换行、效果、链接与滚动沿用对应方向规则。

## 内容尺寸

TextBoxLayoutElement 在光栅化前测固有尺寸。宽 Fixed 不报告偏好；MaxContent 取完整不换行宽；
FitContent 为 `max(min-content, min(max-content, parent available width))`。
高 Fixed 不报告；Auto 报告按 Unity 分配宽度换行后的完整高度，含边距和块间距。
竖排只支持固定高度下的内容宽度；Auto 高回退到矩形路径。固定轴报告 -1，避免覆盖父布局控制。
FitContent 查找外部宽度时跳过内容尺寸祖先，避免自引用。
forwardPointerScrollToParent 默认 true，可向父滚动视图传递未消费滚轮。

<a id="auto-font-size"></a>
## 自适应字号

| 框设置 | 行为 |
| --- | --- |
| 宽高全固定 | 在配置范围放大/缩小 |
| 任一轴按内容 | 最高到设计字号，只缩小 |
| 内容宽 + 自动高且无外部约束 | 不产生有效拟合 |
| 固定宽并换行 | 先重排，固定高不足再缩小 |
| NoWrap | 按固定宽/高需要缩小 |
| Ellipsis | 不拟合，保留省略 |

求解器按有效字号测量，再求宽高，最后算自动高度，不将内容尺寸回灌字号拟合，避免跨帧增长循环。
达到 fontSizeMin 后仍溢出时依 overflow 处理。SlimText UTF-8 零分配更新路径刻意不逐次拟合，保持配置字号。

## 段落间距与 SlimText

paragraphSpacing 是相邻块的默认间隔。`<p>` 支持 `margin` 一到四值简写和四侧物理 longhand。
横排左右边距缩进每行，上下边距控制块前后间隔；竖排上下边距缩进列内范围，右左边距控制块前后间隔。
块前后边距在相邻块之间折叠，并与默认段间距叠加；这些值也参与固有尺寸计算。
块边距接受非负 `px`、`em`、`%`，竖排还可用 `vw`、`vh`；TextBox 外围 margin 是另一层。

SlimText 是独立单行路径：text、fontFamily、fontWeight、fontSize、color、rectOffset、
horizontalAlign、verticalAlign、fallbackToSystemFont、widthMode、heightMode、layoutPriority。
新建组件的默认字体族依 Editor 平台：Windows Microsoft YaHei UI，macOS PingFang SC，
Linux Noto Sans CJK SC，其他目标 system-ui；已有序列化值保留。
总是单行，min-content 与 max-content 宽度相同，不支持方向、富文本、链接或 Ruby。
见 [SlimText 指南](../guides/slim-text.md)。

<a id="low-level-paragraph-api"></a>
## 底层段落 API

```csharp
using System.Collections.Generic;
using Milestro.Skia.TextLayout;
using Milestro.Model;
using UnityEngine;

using var textStyle = new TextStyle();
textStyle.FontSize = 32f;
textStyle.Color = Color.black;
textStyle.SetFontFamilies(new List<string> { "sans-serif" });
using var paragraphStyle = new ParagraphStyle();
paragraphStyle.TextAlign = TextAlign.Start;
paragraphStyle.SetTextStyle(textStyle);
using var builder = new ParagraphBuilder(paragraphStyle);
builder.PushStyle(textStyle);
builder.AddText("直接使用 Skia 构建段落。");
builder.Pop();
using var paragraph = builder.Build();
paragraph.Layout(600f);
```

随后可用 Canvas 绘制，也可查询 Height、LongestLine、MinIntrinsicWidth、MaxIntrinsicWidth。
直接创建的 wrapper 拥有原生内存，应使用 using 或显式 Dispose。

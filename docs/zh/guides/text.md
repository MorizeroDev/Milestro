# 文本、字体与富文本

[English](../../en/guides/text.md) | [简体中文](text.md)

本页介绍段落文本、字体、标记、间距和竖排。多行、滚动、富文本、链接、Ruby 使用
`TextBox` 与自动创建的 `TextBoxRenderTextureProducer`；短单行标签使用
[SlimText](slim-text.md)，可编辑输入使用 [TextInput](input.md)。

## 样式层次

| 层次 | 属性 | 控制内容 |
| --- | --- | --- |
| 文本片段 | `producer.textStyle` | 字体族、字号、字重、字宽、倾斜、颜色、间距、locale |
| 段落 | `producer.paragraphStyle` | 对齐、方向、书写模式、单行、省略字符、段间距 |
| 组件布局 | `wrapMode`、`textOverflow`、`margin`、`enableAutoFontSize` | 换行、溢出、内边距、字号拟合 |

```csharp
using Milestro.Components.Internal;
using Milestro.Model;
using UnityEngine;

var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();
producer.content = "Milestro 使用 Skia 排版。";
producer.textStyle.size = 40f;
producer.textStyle.textColor = Color.black;
producer.textStyle.fontFamilies = new[] { "Inter", "sans-serif" };
producer.textStyle.weight = FontWeight.Medium;
producer.paragraphStyle.textAlign = TextAlign.Start;
producer.wrapMode = TextBoxWrapMode.Wrap;
producer.textOverflow = TextOverflow.Clip;
```

字重采用 Skia 数值，如 `Thin=100`、`Normal=400`、`Medium=500`、`SemiBold=600`、
`Bold=700`、`Black=900`。`width`、`slant` 分别选择字宽和倾斜，例如
`FontWidth.Condensed`、`FontSlant.Italic`。`locale` 默认 `zh-Hans`，影响地区字形、断行和回退；
已知内容语言时应显式设置，例如 `producer.textStyle.locale = "ja"`。

<a id="fonts"></a>
## 字体

组件不引用 Unity `Font` 或 `TMP_FontAsset`，而是请求有序字体族列表，通过原生注册表及系统字体链回退。
先注册平台可读取的字体文件，再使用文件内部的真实 family 名称：

```csharp
using Milestro.Skia;

FontRegistry.RegisterFontFromPath(fontPath);
producer.textStyle.fontFamilies = new[] { "My Font", "sans-serif" };
```

Editor 的 **Milestro → Font Registry** 可查看已注册 family 与 face。
内置关键字为 `serif`、`sans-serif`、`monospace`、`system-ui`，默认 `system-ui`。
可通过 `MilestroConfiguration.Configuration.FontFamily` 定义 `heading`、`body`、`code` 等映射。
关键字在段落构建时展开；后续 family 是回退项。富文本中带引号的 family 名按字面名称处理。
缺字时先核对 family 名与实际可用字体；示例所请求的字体不等于包内附带字体。

<a id="rich-text"></a>
## 富文本

通过 `producer.richTextParserId` 选择解析器：`null` 或空串按纯文本显示，
`milestro_rich_text` 选择 Milestro Markup，`markdown` 选择 Markdown 模块，`tmpro` 选择 TMPro 语法模块。

### Milestro Markup

默认按纯文本显示。在 Inspector 的 **Rich Text Parser** 中选择
**Milestro Rich Text**，或设置：

```csharp
producer.richTextParserId = Milestro.RichTextParser.XmlRichTextParser.Id;
producer.content = "<b>粗体</b>与普通文字";
```

`richTextParserId` 为 `null` 或空串时，`<`、`&` 仅作为普通字符。
内置 XML 解析器 id 为 `milestro_rich_text`。与 TMP 的功能及语法差异见
[富文本能力对照](coming-from-tmpro.md#rich-text-comparison)。
此解析器只实现本文列出的 XML 风格标记，并非完整 HTML 引擎。
未知的非空解析器 id 会报错，不会回退到纯文本。
未知标签和格式错误会报错，Editor 显示错误详情，Player 显示 `Render Error`。

#### XML 标签

以下标签与 XML 专用样式适用于 **Milestro Rich Text** 解析器。

| 标签 | 含义 |
| --- | --- |
| `<b>`、`<strong>` | 字重 700 |
| `<i>`、`<em>` | 斜体 |
| `<u>` | 下划线 |
| `<mark>` | 默认黄色的文字背景 |
| `<s>`、`<del>` | 删除线 |
| `<sup>`、`<sub>` | 上下标 |
| `<font>` | 颜色、字号、字重、字体族与 CSS |
| `<span>` | 底层片段覆盖 |
| `<p>` | 段落块、对齐、方向与块样式 |
| `<br>` | 强制换行 |
| `<img width=".." height=".." key=".."/>` | 不自行绘图的内联占位框 |
| `<a href="target" id="optional-id">` | 可点击范围 |
| `<ruby>基文<rt>注音</rt></ruby>` | Ruby 注音 |
| `<rp>` | Ruby 中供 HTML 回退使用的文本，不显示 |

#### 行内样式与段落块

```xml
<font color="#3366ff" size="48" weight="700" face="Inter">蓝色粗体</font>
<p align="center" dir="ltr" style="text-indent: 2em; margin-bottom: 12px">段落</p>
```

`<span>` 支持 `cspace`（额外字距）、`voffset`（绝对基线偏移）、`voffset-em`（em 偏移）、
`fscale`（正的绝对字号倍率）、`lang`、`face`、`weight`、`width`、`slant`。
行内 `style` 支持 `font-kerning: auto | normal | none`，以及
`font-feature-settings: normal` 或逗号分隔的 `'liga' 0`、`'tnum' on`、`'kern' 1`。
XML 双引号属性内的双引号使用 `&quot;`。

`<p>` 支持 `align`、`dir`、`style`，以及 `text-indent`、`margin` 和四侧
`margin-top`、`margin-right`、`margin-bottom`、`margin-left`。
`margin` 简写接受一到四个值，遵循 CSS 的上、右、下、左展开规则；边距必须非负。
支持 `px`、`em`、`%`，竖排布局中的块边距还支持 `vw`、`vh`。
横排左右边距缩进每一行，上下边距控制块间隔；`vertical-rl` 上下边距缩进列内文本，
右、左边距分别控制块前、块后间隔。块边距与组件整体 `margin` 相互独立。
`em` 依继承字号计算；负 `text-indent` 可画到水平边距，但可用宽度仍是边距内宽度。
不支持 `hanging`、`each-line`。

#### 局部换行、大小写与背景

```xml
<span style="white-space: nowrap">保持在同一行的内容</span>
<span lang="tr" style="text-transform: uppercase">istanbul</span>
<span style="text-transform: smallcaps">Small Caps</span>
<mark>默认黄色背景</mark>
<mark style="background-color: #3366ff80">自定义半透明背景</mark>
```

`white-space: normal | nowrap` 可继承，嵌套 `normal` 恢复自动换行。
`nowrap` 禁止范围内软换行，也适用于竖排换列；显式换行和 `<br/>` 仍然生效。
过长内容会溢出，不自动缩小；这不是浏览器完整的空白处理模型，不支持 `pre` 等值。

`text-transform: none | lowercase | uppercase | smallcaps` 可继承，`none` 恢复原文。
大小写转换使用 ICU 和组件 locale，行内 `lang` 可覆盖；转换可能改变文本长度，例如 `ß` 变为 `SS`。
`smallcaps` 按 TMP 的方式将原小写字母转换为大写，以有效字号的 80% 显示，
原大写及其他字符保留完整字号；它不是字体的 OpenType 小型大写特性。

`background-color` 可用于其他行内元素，背景继承且不改变前景色。
支持基本 CSS 颜色名、`transparent`、`#RGB[A]`、`#RRGGBB[AA]`、`oklab()`、`oklch()`。
`<mark>` 默认为黄色，可由 `background-color` 覆盖。

#### 间距与 OpenType

```csharp
producer.textStyle.letterSpacing = 0.5f;
producer.textStyle.wordSpacing = 2f;
producer.textStyle.lineHeight = 1.25f;
producer.paragraphStyle.paragraphSpacing = 12f;
```

`lineHeight` 是倍率，非绝对增量；小于等于零保留自然度量。富文本继承这些值，局部 `cspace` 可覆盖字距。
段间距加在相邻块之间，叠加折叠后的块前后边距：横排为上、下，竖排为右、左。

段落默认启用 `chws`，支持该特性的字体会压缩相邻全角标点，单独标点保留正常推进。
`TextStyle.ResetFontFeature()` 或 `font-feature-settings: normal` 会清空包括此默认值在内的特性。
竖排直立片段映射到 `vchw`；字体没有 `vchw` 时采用沿行内轴的回退，避免后续字形跨列偏移。

#### Ruby 注音

```xml
<ruby style="ruby-position: over; ruby-align: space-around">漢字<rt>かんじ</rt></ruby>
```

支持 `ruby-position`、`ruby-align`、`ruby-overhang`，以及
`--milestro-ruby-size`、`--milestro-ruby-gap`、`--milestro-ruby-overflow`。
每个 `<ruby>` 只接受一个纯文本 `<rt>`，不支持嵌套、多层或注音内样式。
底层 API 与断行、索引规则见 [Ruby 排版](../reference/ruby-layout.md)。

<a id="inline-placeholders"></a>
#### 内联占位符

```xml
得分：<img width="24" height="24" key="coin-icon"/> 1200
```

`width`、`height` 必须为正数，`key` 可选。空元素只占位置，不绘制图片，源码占一个 U+FFFC 字符；
Ruby 内会被忽略。应用通过几何快照放置自己的视觉对象：

```csharp
if (textBox.TryGetPlaceholderGeometrySnapshot(out var snapshot))
{
    foreach (var item in snapshot.Geometry)
    {
        Vector2 centerPixels = snapshot.PaintPosition + item.Rect.center * snapshot.FitScale;
    }
}
```

`item.Key`、`StartUtf16`、`Visible` 标识项目。`Rect` 处于段落布局单位，
`PaintPosition` 与 `FitScale` 用于转为渲染纹理像素；`ContentVersion` 或 `Generation` 改变后重新读取。

<a id="element-geometry"></a>
#### 元素 ID 与几何查询

可渲染元素接受非空、区分大小写的 `id`。它既可标识 `<img/>` 占位符，也可标识段落、
文字范围或空锚点：

```xml
<p id="chapter"></p>
<p id="paragraph">这里有<span id="label">一段文字</span>和
<img id="icon" width="24" height="24" key="coin-icon"/>。</p>
```

布局完成后，通过 `TextBox.TryGetElementGeometryById` 查询指定元素：

```csharp
if (textBox.TryGetElementGeometryById("icon", out var element))
{
    Debug.Log(element.ContentRect);
    Debug.Log(element.CanvasRect);
}
```

`ContentRect` 使用未滚动的内容坐标，原点位于左上角、Y 向下，单位与 `TextBoxScrollState` 一致。
横排可读取 y 位置，`vertical-rl` 可读取 x 位置。
`CanvasRect` 使用 TextBox RectTransform 本地坐标、Y 向上，包含当前滚动偏移，不按视口裁剪。
空元素返回文本位置处的零尺寸矩形；重复 ID 返回文档顺序中的第一个元素。
ID 不存在或尚无可用布局几何时返回 `false`。

`id` 用于位置查询，`<img>` 的 `key` 用于应用识别占位内容，两者可同时使用。
ID 不自动触发跳转；滚动示例见[按元素 ID 定位](scrolling.md#jump-to-an-element-id)。

#### XML 解析注意事项

标签名区分大小写；启用富文本后 `&` 写为 `&amp;`。链接目标仅作为应用数据返回，不会自动打开。
错误文档不会静默降级为纯文本。完整示例见 [展示素材](../examples/README.md)。

### Markdown

`Milestro.RichText.Markdown` 模块在 Editor 和 Player 场景加载前自动注册
**Markdown**（`markdown`）。预编译包已包含模块与 `Markdig.dll`，Unity 项目必须使用
**.NET Standard 2.1** API Compatibility Level。源码使用者先按
[构建说明](../reference/build.md#markdig) 编译 Markdig。

```csharp
producer.richTextParserId = "markdown";
producer.content = "# 欢迎\n\n**粗体**、*斜体*、~~删除线~~与[帮助](help)。";
```

解析器使用 CommonMark 与 Markdig emphasis extras，将粗体、斜体、删除线、标题、段落、
列表、引用、链接和自动链接转换为段落载荷。一级到六级标题采用字重 700，字号倍率为
1.6 到 1.05。列表使用文本标记，每层缩进两个空格，引用增加缩进；软换行与硬换行均变为 `\n`。

代码片段与代码块按字面文本显示并继承样式，不自动选择等宽字体或语法高亮。
原始 HTML 按字面显示，因此 Markdown 中的 `<ruby>` 等标签不会启用 Milestro XML 样式。
分隔线不显示。图片仅预留固定 16 × 16 的内联占位框，以图片 URL 为 key，丢弃 alt 文本；
应用需根据[占位符几何](#inline-placeholders)自行绘制。链接复用 XML 锚点的
[链接事件](links-and-accessibility.md)。
未启用表格和任务列表扩展，这些语法保留为文本。

### TMPro

可选的 `Milestro.RichText.TMPro` 模块在 Editor 和 Player 场景加载前自动注册
**TMPro**（`tmpro`）。它不依赖 TextMesh Pro 或第三方托管库。

```csharp
producer.richTextParserId = Milestro.RichText.TMPro.TMProRichTextParser.Id;
producer.content = "<color=#3366ff><b>你好</b></color> <link=help>帮助</link>";
```

支持 `<b>`、`<i>`、`<u>`、`<s>`、`<strikethrough>`、`<sup>`、`<sub>`、`<color>`、十六进制颜色简写、
`<font>`、`<font-weight>`、`<size>`、`<cspace>`、`<voffset>`、`<mark>`、
`<uppercase>`、`<allcaps>`、`<lowercase>`、`<smallcaps>`、`<nobr>`、`<align>`、
`<br>`、`<link>`、`<noparse>`、`<alpha>`、`<line-height>`、`<indent>`、
`<line-indent>`、`<margin>`、`<margin-left>`、`<margin-right>`、`<width>`。
`<font>` 选择字体族名称，不选择 TMP 字体资产或材质；`<align>` 创建段落块。
`<link=help>` 在链接事件的 `Href` 中返回 `help`，不自动跳转。

`<alpha=#80>` 保留前景 RGB，将透明度乘以 128/255；颜色本身带有的 alpha 也参与相乘。
`<line-height=85%>` 或 `<line-height=0.85em>` 设置片段行高倍率，使用 Milestro
基于字号的行高模型，不采用 TMP 字体资产的默认行高度量。

段落布局标签接受 `px`、`em`、`%`，并创建段落块：`<indent>` 设置左边距，
`<line-indent>` 设置首行缩进，`<margin>` 同时设置左右边距。
`<width>` 限制扣除边距后的行内可用范围；在 `vertical-rl` 中控制列内可用高度。
这些块边界可能与 TMP 标签的作用范围不同。

```csharp
producer.content = "<margin=1em><width=75%><line-indent=2em>" +
    "<line-height=120%><alpha=#80>段落</alpha></line-height>" +
    "</line-indent></width></margin>";
```

标签名不区分大小写，未闭合的开始标签在输入末尾关闭。
未支持、无效或不匹配的标签会记录 Unity 警告，并作为字面文本显示；其内部的受支持标签仍会解析。
`<noparse>` 内全部内容按字面显示且不警告，`&amp;` 这样的实体文本也保留原样。
这与 Milestro Markup 不同：后者未知标签报错，显示字面标记需使用 XML 转义。
此模块只支持 TMP 语法的一部分；单位和限制见
[模块 README](../../../apps/unity-plugins/Milestro.RichText.TMPro/README.md)，
底层能力差异见[迁移指南](coming-from-tmpro.md)。

## 自定义解析器与载荷

在 `Milestro.RichTextParser` 命名空间中，实现
`IRichTextParser.Parse(string source, RichTextParseContext context)`，返回 `ParagraphPayload`。
通过 `IRichTextParserProvider` 提供稳定的 `Id`、Inspector 的 `DisplayName` 与 `CreateParser()`，
在文本框使用前调用 `RichTextParserService.Register(provider)`；需要 Editor 预览时也应在 Editor 注册。
Inspector 列出已注册解析器，也接受手动输入 id。

id 区分大小写，必须匹配 `[A-Za-z_][A-Za-z0-9_-]*`，会存入序列化数据；已发布的 id 应保持稳定，
需要新名称时可另注册别名。显示名称可自由修改。同 id 注册会替换 provider 并清除缓存实例，
`Unregister(id)` 移除注册。每次注册只创建并共享一个解析器实例，解析器必须在调用之间保持无状态。

每次解析新建 `ParagraphPayloadBuilder`。`AppendText` 不传 `TextStyleState` 时继承组件样式；
`BeginBlock` 开始新的堆叠段落，不插入分隔字符，块内换行用 `\n`。
`BeginLink`/`EndLink` 定义不可嵌套的链接，`AppendPlaceholder` 添加尺寸为正且有限的占位框。
builder 自动跟踪 UTF-16 偏移，每个占位符占一个字符，`Build()` 自动关闭未结束的链接。
`RichTextParseContext` 提供 `AllowViewportBlockMargins`、`Locale` 和可选的非致命诊断集合 `Issues`。

需要按组件 locale 转换大小写时，使用 `new ParagraphPayloadBuilder(context.Locale)`。
builder 应用 `TextStyleState.TextTransform`，跟踪跨样式片段及占位符的 `NoWrap` 范围，
UTF-16 偏移按转换后的文本计算。
`BeginBlock(ParagraphStyleState, TextStyleState)` 复制段落设置，并记录用于解析缩进的文字样式；
`EndBlock()` 结束当前块。块边界和 `Build()` 会提交待完成的禁止换行范围。

<a id="vertical-writing"></a>
## 竖排

```csharp
producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
```

列内从上到下，列从右到左。Canvas `TextBox` 支持固定矩形，以及固定高度、内容决定宽度的布局；
不支持自动高度。字体、富文本、链接、滚动、边距、自适应字号复用同一管线。
Ruby `over` 在物理右侧，`under` 降级到右侧并在 Editor 提示。
详细轴向规则见 [布局](layout.md#vertical-writing-vertical-rl)。

### 纵中横

```xml
<span style="text-combine-upright: all">2025</span>年
<span style="text-combine-upright: all; --milestro-text-combine-upright-sizing: grow">netnewswire</span>
```

组合片段按横向塑形，不旋转，在列内占一 em；换列、两端对齐和省略时作为整体，命中测试保留真实源索引。
默认 `squeeze` 将过宽片段横向压到一 em；`grow` 保持自然宽度并扩大列宽。
支持 `none | all`，连续 `all` 文本组成一组。组内必须采用一致样式，不能跨硬换行、占位符或 Ruby 基文。
不支持 `digits` 自动识别，横排忽略组合标记。

### 文字方向

```xml
<span style="text-orientation: upright">ABC123</span>
<span style="text-orientation: sideways">漢字ABC</span>
```

`mixed` 默认遵循 Unicode `Vertical_Orientation`，CJK 直立、拉丁侧转。
`upright` 逐字直立，不会合并数字；`sideways` 将整个片段顺时针旋转 90°，包括 CJK。
块和行内元素均可继承覆盖；`sideways` 范围内忽略纵中横标记，横排忽略所有方向标记。

当前 `TextInput`、SlimText 和世界空间组件只支持横排；竖排基础方向固定 LTR，不支持 RTL 和
`vertical-lr`。对齐支持 `Start`、`End`、`Center`、`Justify`，`Left`/`Right` 回退为 `Start`。
不支持的组合会给出 Editor 诊断。

# 从 TextMesh Pro 迁移

[English](../../en/guides/coming-from-tmpro.md) | [简体中文](coming-from-tmpro.md)

Milestro 将显示、内容样式和尺寸报告分开：TextBox 呈现纹理，TextBoxRenderTextureProducer
拥有内容与样式，TextBoxLayoutElement 向 Unity 布局报告固有尺寸。它不能直接替换 TMP_Text。

## 概念对照

| TMP | Milestro |
| --- | --- |
| TMP_Text.text | producer.content；SlimText 为 producer.text |
| fontSize | producer.textStyle.size；SlimText 为 fontSize |
| font / TMP_FontAsset | textStyle.fontFamilies，原生字体族列表 |
| color | textStyle.textColor；SlimText 为 color |
| alignment | paragraphStyle.textAlign；方向另设 textDirection |
| enableWordWrapping | wrapMode 与 paragraphStyle.singleLine |
| Overflow | textOverflow：Clip、Ellipsis、Overflow |
| Margins | producer.margin，支持 px、%、vw、vh、em |
| Auto Size | enableAutoFontSize、fontSizeMin、fontSizeMax |
| ContentSizeFitter | TextBoxLayoutElement；SlimText 直接实现 ILayoutElement |
| link | `<a href="..." id="...">` 与 onLinkClicked |
| sprite | 无对应精灵资产语法；img 仅预留位置 |
| TMP_InputField | TextInput，进程级 HybridInput 分发 |
| characterLimit | TextInput.characterLimit，按 Unicode 标量 |
| 内容类型/键盘 | contentType、keyboardType |
| 富文本 | richTextParserId 选择 Milestro Markup、Markdown 或可选的 TMPro 语法模块，默认纯文本 |

## 富文本与 TMP 兼容层

Milestro 提供自己的 **Milestro Markup**，支持字体样式、段落布局、Ruby 注音、
内联占位符和元素位置查询。下方对照表列出它与 TMP 富文本的对应能力。

为便于迁移已有内容，Milestro 也提供 **TMPro 富文本兼容层**。
已有 TMP 标记可交给这个解析器处理，无需先全部改写为 Milestro Markup；
新内容可直接使用 Milestro Markup。每个文本组件通过 `richTextParserId` 选择一种语法。

安装 `Milestro.RichText.TMPro` 模块（预编译包已包含），在 producer 的 **Rich Text Parser**
中选择 **TMPro**，或设置：

```csharp
producer.richTextParserId = Milestro.RichText.TMPro.TMProRichTextParser.Id;
producer.content = "<size=150%><b>你好</b></size> <link=help>帮助</link>";
```

该模块不依赖 TextMesh Pro，接受常用 TMP 样式、字体、颜色、度量、大小写转换、禁止换行、
对齐、链接与 `<noparse>` 标记。未支持或不匹配的标签会警告并按字面显示。
兼容层使用 Milestro 的布局与渲染能力。段落布局标签会创建段落块，行高使用 Milestro 的字号度量；
迁移后应核对换行、间距与字体效果。具体标签及限制见
[支持语法与错误处理](text.md#tmpro)及
[模块 README](../../../apps/unity-plugins/Milestro.RichText.TMPro/README.md)。

## 段落迁移

```csharp
using Milestro.Components.Internal;
using Milestro.Model;
using UnityEngine;

var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();
producer.content = "你好";
producer.textStyle.size = 42f;
producer.textStyle.textColor = Color.black;
producer.paragraphStyle.textAlign = TextAlign.Center;
producer.wrapMode = TextBoxWrapMode.Wrap;
```

不需换行、标记、链接或滚动的标签可用 CanvasSlimText，直接设置 Text 和 TextColor。
输入框改用 `input.Text`、characterLimit 和 onValueChanged；无需为值创建子文本组件。

<a id="rich-text-comparison"></a>
## 富文本能力对照

下表对照 [TMP 4.0 富文本文档](https://docs.unity3d.com/Packages/com.unity.textmeshpro@4.0/manual/RichText.html)
及其[支持标签列表](https://docs.unity3d.com/Packages/com.unity.textmeshpro@4.0/manual/RichTextSupportedTags.html)，
列出常用能力和主要差异。Milestro 一栏指内置 **Milestro Markup** 解析器。

此表说明 **Milestro Markup** 提供的功能及写法；TMPro 兼容层接受的标签见
[TMPro 解析器](text.md#tmpro)。支持情况指 Milestro Markup 的能力支持程度：
✅ 完全支持，🟡 部分支持或需应用补充，❌ 不支持。

| 能力 | Milestro Markup 支持情况 | TMP 4.0 | Milestro Markup |
| --- | --- | --- | --- |
| 标记语法 | 🟡 部分支持 | 标签值可写为 `<color=red>`；多数标签可省略结束标签 | XML 风格属性与嵌套；未知标签报错，`&` 需转义 |
| 粗体、斜体、下划线、删除线 | ✅ 完全支持 | `<b>`、`<i>`、`<u>`、`<s>`、`<strikethrough>` | `<b>`、`<i>`、`<u>`、`<s>`，另有 `<strong>`、`<em>`、`<del>` |
| 颜色、字号、字重、字体 | ✅ 完全支持 | `<color>`、`<size>`、`<font-weight>`、`<font>`；选择字体资产 | `<font color="…" size="…" weight="…" face="…">`；按字体族列表回退 |
| 上下标 | ✅ 完全支持 | `<sup>`、`<sub>` | `<sup>`、`<sub>` |
| 基线偏移 | ✅ 完全支持 | `<voffset>` | `<span voffset="…">` 或 `voffset-em`；可包住单字或一段文字 |
| 字距 | ✅ 完全支持 | `<cspace>` | `<span cspace="…">` |
| 水平排版位置 | ❌ 不支持 | `<pos>` 设置行内光标位置，`<space>` 添加水平偏移 | 无对应标记；`cspace` 调整字距，`text-indent` 调整段首缩进 |
| 逐字任意角度旋转 | ❌ 不支持 | `<rotate>` 绕各字符中心旋转 | 无对应标记；竖排的 `text-orientation` 控制直立或侧转 |
| 链接 | ✅ 完全支持 | `<link>` 标记链接 ID | `<a href="…" id="…">`，提供点击、悬停及可选无障碍导航 |
| 内联图片 | 🟡 部分支持 | `<sprite>` 从精灵资产绘制 | `<img width="…" height="…" key="…"/>` 仅预留位置，应用自行绘制 |
| 对齐与缩进 | 🟡 部分支持 | `<align>`、`<indent>`、`<line-indent>` | `<p align="…" style="text-indent: …">`；按段落块排版 |
| 按字面显示标记 | ✅ 完全支持 | `<noparse>` 禁止解析范围内的标记 | 使用 XML 转义，如 `&lt;b&gt;` 显示为 `<b>`，`&amp;` 显示为 `&` |
| 局部禁止换行 | ✅ 完全支持 | `<nobr>` | `<span style="white-space: nowrap">`；嵌套 `normal` 恢复软换行，硬换行仍生效 |
| 大小写与模拟小型大写 | ✅ 完全支持 | `<lowercase>`、`<uppercase>`、`<allcaps>`、`<smallcaps>` | `text-transform: lowercase / uppercase / smallcaps`；`none` 恢复原文，smallcaps 将原小写字母大写后以 80% 字号显示 |
| 背景高亮 | ✅ 完全支持 | `<mark>` | `<mark>` 默认黄色，`background-color` 可设置背景色，且不改变前景色 |
| 段落边距 | 🟡 部分支持 | `<margin>` 及左右边距标记 | `<p style="margin: …">` 与四侧 longhand；支持四侧物理边距，作用范围按段落块确定 |
| 文本区域宽度 | 🟡 部分支持 | `<width>` 调整文本区域宽度 | 通过段落左右边距控制可用宽度；竖排用上下边距控制行内可用高度，作用范围按段落块确定 |
| 透明度 | 🟡 部分支持 | `<alpha>` 独立设置透明度 | `<font color="#RRGGBBAA">` 同时设置颜色与透明度，无独立透明度标记 |
| 显式换行 | ✅ 完全支持 | `<br>` | `<br/>` 或换行字符 |
| 行高 | 🟡 部分支持 | `<line-height>` 相对字体资产默认行高调整 | `producer.textStyle.lineHeight` 设置倍率；无行内行高标记 |
| 局部渐变预设 | ❌ 不支持 | `<gradient>` 对文本范围应用渐变预设 | 组件支持渐变显示设置，无局部渐变标记 |
| 强制等宽字距 | ❌ 不支持 | `<mspace>` 设置统一字符推进宽度 | 可选择等宽字体，无强制统一推进宽度的标记 |
| 分页 | ❌ 不支持 | `<page>` 配合 Page 溢出模式分页 | 无分页标记或 Page 溢出模式 |
| 命名样式 | ❌ 不支持 | `<style>` 应用命名样式预设 | 支持行内 `style="…"` 声明，尚无命名样式、class selector 或样式表 |

## Milestro 扩展能力

以下能力由 Milestro Markup 和对应的布局 API 提供。对比范围为上述 TMP 4.0 官方内置标签与排版接口；
项目自行编写的脚本或第三方扩展可以实现其他效果。

| 能力 | Milestro 支持情况 | TMP 4.0 | Milestro |
| --- | --- | --- | --- |
| Ruby 注音 | ✅ 支持 | 无内置 Ruby 标签 | `<ruby>`、`<rt>`，支持注音位置、对齐、外伸、字号与间距；见 [Ruby 排版](../reference/ruby-layout.md) |
| 原生竖排 | ✅ 支持 | 无原生 `vertical-rl` 排版模式；`<rotate>` 旋转字符 | `WritingMode.VerticalRl` 按列从上到下、从右到左排版，并提供换列、链接与滚动几何；见[竖排](text.md#vertical-writing) |
| 竖排文字方向 | ✅ 支持 | 无 `text-orientation` 对应标记 | `mixed` 按 Unicode 方向属性混排，`upright` 逐字直立，`sideways` 侧转 |
| 纵中横 | ✅ 支持 | 无内置纵中横标记 | `text-combine-upright: all` 将短片段横向排入竖列，支持压缩至一 em 或扩大列宽 |
| 通用 OpenType 特性控制 | ✅ 支持 | 无按特性标签及数值设置的通用富文本标记 | `font-feature-settings` 可逐片段设置 `'liga'`、`'tnum'`、`'chws'`，并通过 `font-kerning` 控制字偶距；效果取决于字体 |
| 元素 ID 与空锚点 | ✅ 支持 | `<link>` 标记链接；无通用元素 ID 或空段落锚点标记 | `id` 与 `TryGetElementGeometryById` 查询文字、段落、占位符及空锚点，返回内容与 Canvas 坐标；见[元素几何](text.md#element-geometry) |
| 四侧段落边距 | ✅ 支持 | `<margin>` 控制水平边距 | `<p style="margin: …">` 支持四侧物理边距、简写和块间边距折叠；竖排沿对应物理轴应用，见[文本样式](text.md#rich-text) |

竖排当前面向固定高度的 Canvas `TextBox`，不适用于 `TextInput`、SlimText 或世界空间组件；
基础方向为 LTR，不支持 `vertical-lr`。Ruby 不支持嵌套或多层注音，竖排 `under` 回退到右侧。

## 需要重新设计的部分

字体文件先注册，再选择 family，不绑定预生成字形图集资产。
TMP 材质预设、逐字符顶点动画没有直接 TextBox 等价物，渐变与 uGUI tint 是独立显示能力。
使用 Milestro Markup 时，未知标签会报错；使用 TMPro 兼容层时，
未支持或不匹配的标签会发出警告，并保留为字面文本。
段落按块构建 Skia Paragraph，
paragraphSpacing 与受支持的 p 边距一起参与布局。
内容尺寸改变框，自适应字号改变字形，不能当作一个开关。

详见 [文本语法](text.md)、[排版参数](../reference/layout-engine.md)、[输入](input.md)。

# Unity 组件与事件索引

[English](../../en/reference/components.md) | [简体中文](components.md)

本页提供组件与 API 索引，具体使用步骤见相应指南。

## 组件选择

除两个 producer 位于 Milestro.Components.Internal 外，以下组件均位于 Milestro.Components。

| 组件 | 用途 |
| --- | --- |
| TextBox | UI 段落、富文本、链接、Ruby、滚动 |
| TextBoxRenderTextureProducer | TextBox 内容和样式 |
| CanvasSlimText / SlimTextRenderTextureProducer | 单行 UI 与轻量 producer |
| WorldSpaceTextBox / WorldSpaceSlimText | 世界空间段落/单行 |
| WorldSpaceTextBatch | 合并兼容世界空间呈现器 |
| TextInput | 可编辑 UI 文本 |
| MilestroDropdown | 带滚动弹出的单选控件 |
| TextBoxLayoutElement | 固有尺寸报告 |
| TextBoxScrollbar | 连接 TextBox/TextInput 与滚动条 |
| MilestroScrollRect | 带滚轮动画和 Elastic 的 ScrollRect |
| TextBoxAccessibility | 链接导航及屏幕阅读器节点 |
| TextInputAccessibility | 值与编辑公告 |

## TextBox

继承 uGUI Graphic，自动创建/发现同对象 producer；content 和样式设置在 producer。

| 成员 | 用途 |
| --- | --- |
| onLinkClicked / onLinkHoverChanged | 链接激活/进入离开 |
| linkHoverColor / linkHoverUnderlineColor / linkHoverUnderlineThickness | 悬停效果 |
| linkHoverCursorEnabled / linkHoverCursor / linkHoverCursorHotspot / linkHoverCursorMode | 光标反馈 |
| scrollElastic | 视觉越界设置 |
| GetScrollPercent() / GetScrollPercentX/Y() | 规范位置 |
| ScrollToPercent(Vector2, bool) / ScrollToPercentX/Y(float, bool) | 滚动 |
| TryGetScrollState(out TextBoxScrollState) | 逻辑偏移、视口与内容尺寸 |
| TryGetPlaceholderGeometrySnapshot | 内联占位符几何 |

参见 [文本指南](../guides/text.md)、[排版参数](layout-engine.md)。

## TextInput

继承 RenderTextureGraphic，拥有内部原生 InputBox，不添加 TMP_InputField 或手动 InputBox 组件。

| 事件 | 参数 |
| --- | --- |
| onValueChanged / onEndEdit / onSubmit | 已提交、结束、提交的 string |
| onFocusGained / onFocusLost | 无 |
| onTouchScreenKeyboardStatusChanged | TouchScreenKeyboard.Status |

属性分组如下：

- 值：Text、SetTextWithoutNotify(string)、Submit()。
- 外观：size、fontFamilies、textColor、caretColor、caretWidth、selectionColor、textAlignment、lineMode、wrapMode、textOverflow、ellipsisString、locale、srgb。
- 表单：placeholder、characterLimit、contentType、characterValidation、keyboardType、readOnly、maskInput、maskChar、isPassword、allowCopy、selectionEnabled、submitOnEnter。
- 滚动：smoothScroll、scrollTweenDurationSeconds、scrollElastic、GetScrollPercent*、ScrollToPercent*、TryGetScrollState。
- 移动：shouldHideMobileInput、touchScreenKeyboard。
- 验证：onValidateInput、onValidateEdit。

事件与规范化规则见 [输入指南](../guides/input.md)。

## MilestroDropdown

继承 Selectable，纯文字选项、索引值。列表 API 为 options、optionCount、GetOptionText、SetOptions、
ClearOptions、SetOptionText；值 API 为 value、SetValueWithoutNotify、onValueChanged；
弹出 API 为 Show、Hide、isExpanded；呈现引用为 captionText、placeholderText、placeholder、template、maxVisibleItems。
换表静默，实际值变更只通知一次，候选和取消不提交。见 [下拉框](../guides/dropdown.md)。

## SlimText 与世界空间

CanvasSlimText 需要 RectTransform、RawImage、SlimTextRenderTextureProducer。
提供 Text、TextColor、SetContent(string, Color)、Configure(string, float, SlimTextHorizontalAlign, Color)。
其余参数访问 producer。

WorldSpaceSlimText 提供 text、fontFamily、fontSize、fontWeight、color、textureSizePixels、pixelsPerUnit、
rectOffset、对齐、尺寸模式、自动字号、材质、renderer 设置，以及 PrepareTextUtf8NoAlloc(int capacity)、
SetTextUtf8NoAlloc(byte[] buffer, int offset, int length)。见 [SlimText](../guides/slim-text.md)。

WorldSpaceTextBox 自动使用段落 producer，公开 textureSizePixels、pixelsPerUnit、material、
texturePropertyName、disableRendererWhenNoOutput。
WorldSpaceTextBatch 公开 BatchedRendererCount、BatchedTextCount、Rebuild()。
见 [世界空间](../guides/world-space.md)。

## 布局与滚动辅助

TextBoxLayoutElement：widthMode（Fixed/MaxContent/FitContent）、heightMode（Fixed/Auto）、
forwardPointerScrollToParent、layoutPriority。
TextBoxScrollbar：只连接一个 TextBox 或 TextInput，可选水平/垂直 Scrollbar。
MilestroScrollRect：增加 scrollElastic、scrollWheelStepPixels、smoothScroll、scrollTweenDurationSeconds、
逻辑规范位置 Get/Set 方法和返回逻辑 RectTransform 的 content。

## 事件载荷

Milestro.Events.LinkClickedEventArgs 包含 Href、Id；LinkHoverEventArgs 另含 Hovered。
TouchScreenKeyboardEvent 是 UnityEvent&lt;TouchScreenKeyboard.Status&gt;。
事件实例 getter-only，用 AddListener/RemoveListener。

## 底层封装

以下不是场景组件。调用方创建的原生封装应 using/Dispose；组件拥有的对象不另行接管。

| 类型 | 命名空间 | 用途 |
| --- | --- | --- |
| TextStyle、ParagraphStyle、StrutStyle、RubyStyle | Milestro.Skia.TextLayout | 原生样式 |
| ParagraphBuilder、Paragraph | Milestro.Skia.TextLayout | 构建、排版、绘制、命中、轮廓 |
| InputBox | Milestro.Skia.TextLayout | 可编辑模型 |
| FontCollection | Milestro.Skia.TextLayout | 字体回退与缓存 |
| FontRegistry | Milestro.Skia | 注册与检查字体 |
| Canvas | Milestro.Skia | 原生绘图目标 |
| UnityAutoRenderTextureSurface | Milestro.Skia | 自动后端选择 |

Paragraph 的主要接口为 Height、LongestLine、MinIntrinsicWidth、MaxIntrinsicWidth、Layout、Paint、
SplitGlypl、ToPath、ToSDF。InputBox 包含 Text、Cursor、Selection、SetViewport、SetSoftWrap、
MaskInput、Focused、SetTextOverflow、InsertText、组合与提交、撤销重做、选择移动、HitTest、
EnsureCaretVisible、GetCaretRect、GetCompositionRect、GetMetrics、GetLineCount、GetLineMetrics。
原生内存使用示例见 [底层段落 API](layout-engine.md#low-level-paragraph-api)。

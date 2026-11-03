# 滚动与滚动条

[English](../../en/guides/scrolling.md) | [简体中文](scrolling.md)

文本可在自己的渲染纹理视口内滚动，与 ScrollRect 移动 content RectTransform 是两件事。

## TextBox 与 TextInput

TextBox 使用固定 RectTransform，设置 `producer.textOverflow = TextOverflow.Overflow`，按需选择 Wrap。
滚轮和触控板改变视口；Inspector 可调滚轮步长、平滑、动画时长与 Scroll Elastic。

```csharp
Vector2 position = textBox.GetScrollPercent();
textBox.ScrollToPercent(new Vector2(0f, 1f), animated: true);
float vertical = textBox.GetScrollPercentY();
textBox.ScrollToPercentY(vertical, animated: false);
```

`TryGetScrollState(out TextBoxScrollState state)` 返回逻辑偏移、视口与内容尺寸。
TextInput 提供相同的 GetScrollPercentX/Y、ScrollToPercent*、TryGetScrollState，原生编辑框负责光标跟随。
MultiLine + NoWrap 用于水平滚动，MultiLine + Wrap 用于软换行后的垂直滚动；需要可滚动范围时选择 Overflow。

<a id="jump-to-an-element-id"></a>
## 按元素 ID 定位

内置 Milestro Markup 记录可渲染元素的 `id`，包括空段落锚点：

```xml
<p id="chapter"></p>
```

布局完成后查询位置，并按需滚动：

```csharp
if (textBox.TryGetElementGeometryById("chapter", out var element) &&
    textBox.TryGetScrollState(out var state))
{
    float maxScrollY = state.ContentHeight - state.ViewportHeight;
    if (maxScrollY > 0f)
    {
        textBox.ScrollToPercentY(element.ContentRect.y / maxScrollY);
    }
}
```

`ContentRect` 以内容左上角为原点，Y 向下，单位与 `TextBoxScrollState` 一致，不随滚动改变。
`vertical-rl` 使用 `ContentRect.x` 获取水平内容位置。
`CanvasRect` 使用 TextBox 的 RectTransform 本地坐标，Y 向上，包含当前滚动偏移，未经视口裁剪。
空元素返回文本位置处的零尺寸矩形；重复 ID 返回文档顺序中的第一个元素。
ID 区分大小写且不能为空；不存在、尚无可用布局几何时，查询返回 `false`。
这些 ID 用于几何查询，不会自动触发跳转，也不提供 CSS selector 或样式表功能。

## 连接滚动条

添加 **Milestro → Text Box Scrollbar**，只指定一个 TextBox 或 TextInput，
再指定水平和/或垂直 Unity Scrollbar。连接器按视口/内容比例设置滑块大小，双向同步位置，
无溢出时隐藏对应滚动条。垂直滚动条值反转，1 表示顶部，符合 uGUI 惯例。

## MilestroScrollRect

需要整块滚动视图使用 Milestro 滚轮动画和 Elastic 时，用继承自 ScrollRect 的 `MilestroScrollRect`。

```csharp
scrollRect.SetLogicalNormalizedPosition(new Vector2(0f, 1f));
```

提供 Get/SetLogicalNormalizedPosition 及水平/垂直版本，`content` 返回逻辑内容对象。
Elastic 会使原生 ScrollRect 的 normalizedPosition 暂时反映视觉偏移；需要稳定逻辑值时使用上述方法。

## Elastic 越界回弹

TextBox、TextInput、MilestroScrollRect 均有序列化的 `scrollElastic`：

| 设置 | 默认 | 含义 |
| --- | --- | --- |
| Enabled | true | 视觉越界 |
| Resistance | 0.5 | 阻力 |
| MaxOverscroll | 96 | 最大视觉偏移 |
| ReturnDurationSeconds | 0.24 | 回弹时间 |
| ReleaseDelaySeconds | 0.08 | 空闲后释放延迟 |

Elastic 只影响呈现，逻辑滚动、滚动条、选区、光标和命中测试仍被限制。
需要提供者至少支持 DeltaOnly；活动父级滚动处理器会禁用子级 Elastic，不包含嵌套边缘接管。
DeltaOnly 无可靠手势结束信息，释放和正交手势有 [已知限制](../reference/limitations.md#hybridinput-scroll-deltaonly)。

全局轴锁、滚轮动画、步长、键盘重复与互锁配置见 [运行时配置](../reference/configuration.md)。

# 链接与无障碍

[English](../../en/guides/links-and-accessibility.md) | [简体中文](links-and-accessibility.md)

TextBox 提供链接点击与悬停；可选无障碍组件补充导航和屏幕阅读器节点。

## 可点击链接

```xml
阅读<a href="help" id="help-link">使用说明</a>。
```

`href` 必需且非空；`id` 可选，提供时不能空。只接受这两个属性，不能嵌套链接。
目标按原样作为应用数据返回，Milestro 不打开或解释 URL。

```csharp
using Milestro.Components.Internal;
using Milestro.Events;
using UnityEngine;

var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();
producer.richTextParserId = Milestro.RichTextParser.XmlRichTextParser.Id;
producer.content = "阅读<a href=\"help\">帮助</a>。";
textBox.onLinkClicked.AddListener((LinkClickedEventArgs args) => Debug.Log(args.Href));
textBox.onLinkHoverChanged.AddListener((LinkHoverEventArgs args) => Debug.Log(args.Hovered));
```

主鼠标键或触摸按下与释放都命中同一可见链接，且未变成拖动，才产生点击。
Link Hover 组控制颜色、下划线、手型光标；应用自管光标时可关闭 linkHoverCursorEnabled。
不保存 visited 状态，需要时按 Href/Id 自行记录。

## 导航

同对象添加 `Milestro.Components.TextBoxAccessibility`，它是使用场景 EventSystem 的 Selectable。
Left/Up 到前一个链接，Right/Down 到后一个，Submit 触发同一 onLinkClicked，Cancel 清除选择。
到列表边缘后交还 uGUI 导航，不循环。焦点框按链接各行几何绘制，固定视口可自动滚入可见范围。
可配置 Focus Color、Focus Thickness、Link Hint、Screen Reader Enabled。
这些是 uGUI 导航映射；不代表 TextInput 或下拉框已有完整手柄适配。

## 屏幕阅读器

需要 Unity 6000.3+、内置 `com.unity.modules.accessibility`，以及静态文本上的 TextBoxAccessibility
或输入框上的 TextInputAccessibility。缺少模块时保留导航，记录警告，不发布节点。

TextBox 发布静态文本与链接节点，链接使用可见标签、hint、button role 和屏幕空间几何，
激活回到 onLinkClicked，屏幕阅读器聚焦可滚入视图。
TextInput 发布一个 TextField 节点，value 跟随已提交文本。遮罩字段每个字素簇发布一个遮罩字符。
Unity 模块没有结构化光标/选区通道，因此以语音公告反馈编辑变化；密码不公告明文。

```csharp
using Milestro.Components;

textBox.gameObject.AddComponent<TextBoxAccessibility>();
input.gameObject.AddComponent<TextInputAccessibility>();
```

公告模板和回声长度可序列化以便本地化，设计与平台缺口见
[TextInput 无障碍设计](../reference/accessibility-design.md)。

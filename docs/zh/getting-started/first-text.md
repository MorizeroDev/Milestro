# 显示第一段文本

[English](../../en/getting-started/first-text.md) | [简体中文](first-text.md)

本页创建一个 `TextBox`，用 C# 设置文字、字号和颜色。

## 1. 创建 Canvas 和 TextBox

1. 在 Hierarchy 中选择 **GameObject → UI → Canvas**，或使用已有 Canvas。
2. 在 Canvas 下创建空 UI 对象，命名为 `Hello Text`。
3. 添加 **Milestro → Text Box** 组件。
4. 将 `RectTransform` 尺寸设为非零，例如 `800 × 200`。

`TextBox` 是 uGUI `Graphic`，负责显示渲染纹理。它自动在同一对象上创建
`TextBoxRenderTextureProducer`；内容和样式属于 producer。
因此不要在 `TextBox` 上寻找 `text` 字段。

## 2. 添加脚本

创建 `FirstText.cs`，挂在该对象上：

```csharp
using Milestro.Components;
using Milestro.Components.Internal;
using UnityEngine;

public sealed class FirstText : MonoBehaviour
{
    [SerializeField] private TextBox textBox;

    private void Start()
    {
        if (textBox == null)
        {
            Debug.LogError("Assign a TextBox to FirstText.", this);
            return;
        }

        var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();
        if (producer == null)
        {
            Debug.LogError("TextBoxRenderTextureProducer was not created.", this);
            return;
        }

        producer.content = "你好，Milestro！";
        producer.textStyle.size = 48f;
        producer.textStyle.textColor = Color.white;
    }
}
```

将对象拖到脚本的 `Text Box` 字段，进入 Play Mode。白色文字适合深色背景。

## 3. 修改结果

| 需求 | 属性 |
| --- | --- |
| 内容 | `producer.content` |
| 字号、颜色、字重 | `producer.textStyle.size`、`textColor`、`weight` |
| 字体族列表 | `producer.textStyle.fontFamilies` |
| 段落对齐 | `producer.paragraphStyle.textAlign` |
| 换行 | `producer.wrapMode` |
| 裁剪、省略或溢出 | `producer.textOverflow` |
| 边距 | `producer.margin` |

```csharp
producer.content = "这是一段会按文本框宽度自动换行的文字。";
producer.wrapMode = Milestro.Model.TextBoxWrapMode.Wrap;
producer.textOverflow = Milestro.Model.TextOverflow.Clip;
producer.paragraphStyle.textAlign = Milestro.Model.TextAlign.Start;
producer.textStyle.fontFamilies.Clear();
producer.textStyle.fontFamilies.Add("sans-serif");
producer.textStyle.letterSpacing = 0.5f;
producer.textStyle.lineHeight = 1.25f;
```

字体列表通过原生字体注册表与系统回退解析，不引用 Unity `Font` 或 `TMP_FontAsset`。
富文本默认关闭，使用 XML 标签前设置
`producer.richTextParserId = Milestro.RichTextParser.XmlRichTextParser.Id`。

下一步：[文本与字体](../guides/text.md)、[布局与尺寸](../guides/layout.md)、
[单行 SlimText](../guides/slim-text.md)、[输入框](first-input.md)、[TMP 迁移](../guides/coming-from-tmpro.md)。

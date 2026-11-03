# 世界空间文本

[English](../../en/guides/world-space.md) | [简体中文](world-space.md)

`WorldSpaceTextBox` 用于段落和富文本，`WorldSpaceSlimText` 用于短单行标签。
二者均将文本渲染到纹理，再用生成的网格显示，无需 Canvas，目前只支持横排。

## WorldSpaceTextBox

添加 **Milestro → World Space Text Box**，自动创建 RectTransform、producer、网格呈现器、
MeshFilter、MeshRenderer。内容和样式仍在 TextBoxRenderTextureProducer 上：

```csharp
using Milestro.Components;
using Milestro.Components.Internal;
using UnityEngine;

public sealed class WorldParagraph : MonoBehaviour
{
    [SerializeField] private WorldSpaceTextBox label;

    private void Start()
    {
        var producer = label.GetComponent<TextBoxRenderTextureProducer>();
        producer.content = "世界空间中的段落。";
        producer.textStyle.size = 32f;
        producer.textStyle.textColor = Color.white;
        producer.wrapMode = Milestro.Model.TextBoxWrapMode.Wrap;
    }
}
```

| 设置 | 默认 | 含义 |
| --- | --- | --- |
| textureSizePixels | 1024 × 256 | 目标分辨率 |
| pixelsPerUnit | 100 | 世界单位缩放 |
| material | 默认资源 | 材质覆盖 |
| texturePropertyName | _MainTex | 纹理属性 |
| disableRendererWhenNoOutput | false | 无纹理时隐藏 |

组件同步 RectTransform 与 textureSizePixels。纹理尺寸影响布局与四边形大小，pixelsPerUnit 决定世界比例。
不使用仅供 UI 的 TextBoxLayoutElement；3D 布局应明确选择分辨率和比例。

## 世界空间合批

在多个 WorldSpaceTextBox 的共同父级添加 `WorldSpaceTextBatch`。
它合并材质与图集页相同的标签，将各自 UV 区域写入组合网格；纹理、UV、网格或变换变化时重建。
仅像素变化复用网格。单个输出或不同页继续独立显示。

```csharp
var surfaces = Milestro.Configuration.MilestroConfiguration.Configuration.RenderSurface;
surfaces.EnableAtlas = true;
surfaces.AtlasPageSize = 2048;
```

创建文本前启用图集收益最大。不合并各标签 MaterialPropertyBlock，公共视觉设置放在源材质中。
停用 batch 会恢复原始 renderer；此合并不是 GPU instancing。

## WorldSpaceSlimText

```csharp
label.text = "Unit 04";
label.fontSize = 24f;
label.color = Color.white;
label.textureSizePixels = new Vector2Int(512, 64);
label.pixelsPerUnit = 100f;
```

此处 label 为 WorldSpaceSlimText；没有换行、富文本、链接、Ruby。
尺寸与对齐遵循 [SlimText](slim-text.md)。多行选择 WorldSpaceTextBox，UI 单行选择 CanvasSlimText。

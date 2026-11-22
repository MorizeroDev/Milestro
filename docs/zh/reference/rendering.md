# 渲染、合批与后端

[English](../../en/reference/rendering.md) | [简体中文](rendering.md)

默认情况下，文本先绘制到 RenderTexture，再由 uGUI 或世界空间网格显示。
部分 Canvas 文字也可以使用下面介绍的字形网格。本页介绍渲染方式、合批和相关配置。

<a id="experimental-glyph-meshes"></a>
## 实验性字形网格（Mesh Glyph）

默认使用纹理渲染。如果要为 Canvas `TextBox` 和使用字符串更新的 `CanvasSlimText`
尝试共享字形网格，可以在创建文字前设置：

```csharp
Milestro.Configuration.MilestroConfiguration.Configuration.RenderSurface
    .EnableTextGlyphMesh = true;
```

当前仅支持 Gamma 色彩空间和白色源文字。可以用 Graphic 的 RGB tint 为输出着色；
如果直接把源文字设为其他颜色，组件仍会使用纹理渲染。

### 什么时候会回退？

回退针对整个组件。即使只有一小段文字需要不支持的功能，整个文本框也会使用纹理。

| 情况 | 显示方式 |
| --- | --- |
| 开关未启用，或使用世界空间文字、`TextInput`、SlimText 的 UTF-8 更新路径 | 保持纹理渲染 |
| Linear 色彩空间、非白色源文字，或彩色字体（如彩色 emoji） | 回退到纹理 |
| 竖排、内联占位符、背景、阴影、下划线或删除线 | 回退到纹理 |
| 顶点渐变或自定义显示材质 | 回退到纹理 |
| Graphic、CanvasRenderer 或父级 CanvasGroup 的透明度小于 1 | 回退到纹理 |
| 文字受祖先 `RectMask2D` 遮罩影响，且该遮罩启用了非零 softness | 回退到纹理 |
| `CanvasSlimText` 开启了 Raycast Target，或自身对象上有 `Mask`、`RectMask2D` | 回退到纹理 |
| 超出字形数上限、单个输出放不进一页字形图集，或设备缺少所需 shader、Alpha8 支持 | 回退到纹理 |

祖先上的硬边 `RectMask2D` 本身不会触发回退。源文字透明度与整体透明度也不同：
白色源文字可以带 alpha，但通过 Graphic 或 CanvasGroup 淡出整个组件时会改用纹理。

单个输出必须放入一个图集页面。默认最多两个 2048 × 2048 页面，每组件最多 2048 个字形；
容量不足时同样使用纹理。参数见[配置参考](configuration.md#render-surface)。
该功能仍为实验选项，启用前应在目标平台检查画面和性能。
实现与测试方法放在[开发文档](../development/text-pipeline.md)中。

### 回退后会变成独立纹理吗？

通常不会。只要 `EnableAtlas` 开启且分配成功，兼容组件仍会共享渲染纹理图集。
关闭这项配置、文字区域过大，或图集分配被拒绝时，才会改用独立纹理。
因此，从字形网格回退，不等于每个标签都创建一张纹理或一个独立 Canvas。

<a id="draw-call-batching"></a>
## 图集合批

TextBox、TextInput 和普通 SlimText 目标默认共享图集页。公开输出是纹理加 UV 矩形，
自定义呈现器必须同时使用二者，不能采样整页。显式 UnityAutoRenderTextureSurface /
UnitySkiaRenderTextureSurface 与 SlimText 零分配提交路径仍使用独立目标。

```csharp
var surfaces = Milestro.Configuration.MilestroConfiguration.Configuration.RenderSurface;
surfaces.EnableAtlas = true;
surfaces.AtlasPageSize = 2048;
```

在创建文本前配置。RGBA 页约占边长平方乘 4 字节，2048 页约 16 MiB，1024 页约 4 MiB。
小页减少稀疏 UI 的最低分配，大页可容纳更多标签；页面还需要为边缘留白和碎片预留空间。
区域过大、页尺寸非法或分配被拒绝时，会按纹理预算和缩放策略改用独立纹理。

兼容 Graphic 可因共享纹理而合批，但 Canvas、材质、遮罩、裁剪和透明顺序仍会拆批。
图集也会合并页面更新、跳过未变化的区域，从而减少绘制提交开销，但不保证任意场景只有一个 draw call。
世界空间合并网格不是 GPU instancing。

同一帧更新大量独立纹理时，部分更新可能延后到后续帧完成。

## 后端

UnityAutoRenderTextureSurface 按 Unity 当前 graphics device 选择 Metal、D3D12、D3D11、Vulkan、
OpenGLES3、OpenGLCore，不改变 Unity API，也不在 Vulkan 后端缺失时自动切到 GLES。

### Direct3D 11

文字在 CPU 上光栅化，然后整张纹理上传到 GPU。

### Vulkan

Direct 默认通过 Skia Vulkan 直接绘制到 Unity 目标。
StagingCopy 则在 CPU 上光栅化，再将整张纹理复制上传到 GPU。

创建组件表面前设置 RenderSurface.VulkanBackend，或在显式 descriptor 上设置 VulkanBackend。
每个目标创建后固定，修改全局配置不热切换已有表面。
Android Vulkan 要求 API 24 或更新版本。
桌面 Vulkan/OpenGL 是实验性构建选项，见[构建](../development/build.md)。

## 兼容性

- 不支持 MSAA 渲染纹理。
- GLES 拒绝 BGRA32，应使用 Auto/RGBA32。
- 托管代码与原生库必须来自同一修订，一起更新。源码构建见[开发文档](../development/build.md)。

## 预算与策略

RenderSurface 控制 raster scale、边长和单表面/总字节预算，默认值见 [配置](configuration.md#render-surface)。

| 情况 | 建议 |
| --- | --- |
| 大量相同材质 UI 标签 | 使用图集 |
| 静态文本 | 保持内容不变，跳过冗余提交 |
| 高频更新单行 | SlimText |
| 大量世界空间段落 | WorldSpaceTextBox + WorldSpaceTextBatch |
| 内存紧张 | 减小页或关闭图集 |
| 自定义渲染 | 使用低层 UnitySkia 表面并显式 Dispose |

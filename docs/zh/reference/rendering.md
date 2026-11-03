# 渲染、合批与后端

[English](../../en/reference/rendering.md) | [简体中文](rendering.md)

文本先通过原生 Skia 表面绘制到 RenderTexture，再由 uGUI 或世界空间网格呈现。
本页关注渲染表面，与段落布局参数分开。

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
小页减少稀疏 UI 的最低分配，大页可容纳更多标签；padding 和碎片额外占空间，活区域不会重排。
空页在挂起绘制结束后释放。区域过大、页尺寸非法或分配被拒绝时，按现有 raster 策略回退到独立纹理。
色彩空间、设备 epoch、配置不同的目标不共页。

两类成本分别降低：兼容 Graphic 可因共享纹理而合批，但 Canvas、材质、mask、clip、透明顺序仍会拆批；
每次 flush 每个脏页最多一个原生提交，页上未变区域保留像素，静态页无新提交。
这减少目标切换和提交开销，不保证任意场景一个 draw call，也不保证减少所有 Skia 字形绘制。
世界空间合并网格不是 GPU instancing。

每条原生渲染路由最多积压 256 次提交。超出时返回可重试 deferred，托管层保持 dirty 并在 drain 后重试，
大量独立目标的更新可能跨帧完成。图集减少提交数，但不改路由上限。
区域更新清一像素透明 padding、按内容 clip 并应用各目标 raster scale。
快照和退役区域保活到提交完成，复用已有表面预算与设备恢复机制，不建立第二套纹理池。

## 后端

UnityAutoRenderTextureSurface 按 Unity 当前 graphics device 选择 Metal、D3D12、D3D11、Vulkan、
OpenGLES3、OpenGLCore，不改变 Unity API，也不在 Vulkan 后端缺失时自动切到 GLES。

### Direct3D 11

CPU 光栅化到持久化的目标 Skia 表面，再用 UpdateSubresource 上传。
缓存支持图集增量保留，随 Unity 目标或设备重置释放；上传仍是整表面 CPU 上传，不是 GPU 字形光栅化。

### Vulkan

Direct 默认直接在 Unity 目标上使用 Skia Vulkan，采用配对 prepare/submit 事件，队列和退出生命周期仍需设备验证。
StagingCopy 用 CPU 光栅化并记录 buffer-to-image 上传，三槽环按 Unity safe-frame 进度回收存储，仍是整表面复制上传。

创建组件表面前设置 RenderSurface.VulkanBackend，或在显式 descriptor 上设置 VulkanBackend。
每个目标创建后固定，修改全局配置不热切换已有表面。
Android 默认启用 Vulkan，要求 API 24+ 和 NDK Vulkan 库；API 23 GLES-only 必须显式关闭。
桌面 Vulkan/OpenGL 为默认关闭的实验 CMake 选项，见 [构建](build.md)。

## 兼容性

- descriptor 规范化拒绝 MSAA。
- GLES 拒绝 BGRA32，应使用 Auto/RGBA32。
- 托管绑定与原生库必须来自同一构建；图集区域命令要求 render payload ABI 4，ABI 不同即拒绝，即使关闭图集也如此。
- 更新绑定时重新构建所有发布平台的原生插件。
- 配置 MILESTRO_RENDER_DEBUG_LOG 可启用详细原生/托管渲染日志。

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

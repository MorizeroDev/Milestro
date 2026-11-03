# 运行时配置

[English](../../en/reference/configuration.md) | [简体中文](configuration.md)

项目级默认设置位于一个静态配置对象：

```csharp
using Milestro.Configuration;

var configuration = MilestroConfiguration.Configuration;
```

在创建组件或 producer 之前配置。部分值在构建段落时读取，Vulkan 后端等则在表面生命周期内固定。

## 字体关键字

```csharp
configuration.FontFamily.SetKeywordMapping("heading", "Inter", "sans-serif");
configuration.FontFamily.SetKeywordMapping("code", "JetBrains Mono", "monospace");
```

关键字不区分大小写，值可引用其他关键字或字面字体名。
ResetKeywordMapping("heading") 删除覆盖并恢复该关键字内置行为。
内置为 serif、sans-serif、monospace、system-ui。

## 输入快捷键

| InputBoxShortcut 属性 | 默认 |
| --- | --- |
| AcceptRedoWithControlY | true |
| AcceptRedoWithShiftZ | true |
| AcceptShiftClipboardShortcuts | true |

## 滚动轴锁与动画

| ScrollAxisLock 属性 | 默认 | 含义 |
| --- | --- | --- |
| DefaultGestureTimeoutSeconds | 0.18 | 手势空闲重置 |
| DefaultDeadzone | 0.2 | 初始死区 |
| DefaultDominanceRatio | 2 | 选择主轴的比例 |
| DefaultDecisionDistance | 1 | 决策前距离 |
| DefaultInitialCorrectionSeconds | 0.08 | 初始修正窗口 |

| ScrollTween 属性 | 默认 | 含义 |
| --- | --- | --- |
| PointerScrollTweenMode | Auto | Auto、AlwaysTween、BypassFractional |
| FractionalDeltaTolerance | 0.5 | 小数增量噪声容差 |

## TextInput 默认值

| 属性 | 默认 | 含义 |
| --- | --- | --- |
| ScrollWheelStepPixels | 48 | 滚轮步长 |
| KeyRepeatInitialDelay | 0.42 | 首次重复延迟 |
| KeyRepeatInterval | 0.045 | 重复间隔 |
| KeyboardScrollInterlockTimeoutSeconds | 0.03 | 键盘与滚动互锁 |
| SurrogatePairTimeout | 2.5 | 代理对拼接超时 |

<a id="render-surface"></a>
## 渲染表面

| RenderSurface 属性 | 默认 | 含义 |
| --- | --- | --- |
| MaxScreenSpaceRasterScale | 2 | 屏幕空间最大光栅倍率 |
| MinimumFallbackScale | 0.25 | 最小回退倍率 |
| ScaleQuantum | 0.25 | 倍率量化 |
| ScaleHysteresis | 0.05 | 倍率迟滞 |
| ConservativeMaxTextureEdge | 8192 | 保守纹理边长限制 |
| MaxPixelsPerSurface | 16,777,216 | 单表面像素预算 |
| MaxBytesPerSurface | 67,108,864 | 单表面字节预算 |
| MaxGlobalBytes | 268,435,456 | 总字节预算 |
| MaxTransitionBytes | 134,217,728 | 过渡字节预算 |
| MaxAttemptsPerRequestAndEpoch | 8 | 分配重试次数 |
| VulkanBackend | Direct | Direct / StagingCopy |
| EnableAtlas | true | 共享兼容区域 |
| AtlasPageSize | 2048 | 图集页边长 |

RGBA 页约占 `AtlasPageSize * AtlasPageSize * 4` 字节，2048 页约 16 MiB。
EnableAtlas=false 请求独立纹理；详见 [渲染](rendering.md)。

## 世界空间材质

默认 `WorldSpaceTextBox.DefaultMaterialResourcePath` 为 `Milestro/TextBoxDefaultMeterial`。
资源路径中的 Meterial 拼写属于现有路径，引用时必须原样保留。

## ICU

| Icu 属性 | 默认 | 含义 |
| --- | --- | --- |
| IcudtlResourcePath | Milestro/icudtl.dat | Resources 路径，不含 bytes 后缀 |
| IcudtlPersistentFileName | icudtl.dat | 移动端解压后的文件名 |
| IcudtlPathOverride | 空 | 覆盖提取文件的路径 |

资源文件为 `Resources/Milestro/icudtl.dat.bytes`。
Editor/Standalone 从内存加载；移动端先写持久化路径，再从文件初始化。
MILESTRO_NO_ICU_INIT 禁止自动初始化，由应用自行负责。

## 替换配置对象

Configuration 有 setter，可在启动前替换整个对象；通常保留默认对象并修改各组更容易维护：

```csharp
configuration.TextInput.ScrollWheelStepPixels = 64f;
configuration.RenderSurface.EnableAtlas = false;
```

# Runtime configuration

Project-wide defaults live on a single static object:

```csharp
using Milestro.Configuration;

var configuration = MilestroConfiguration.Configuration;
```

Set values before creating Milestro components or text producers. Some settings
are read when a paragraph is built; others, such as the Vulkan backend, are fixed
for an existing render surface's lifetime.

## Groups

| Group | Controls |
| --- | --- |
| `FontFamily` | Keyword-to-family mappings and literal family declarations |
| `InputBoxShortcut` | Undo/redo and clipboard shortcut variants |
| `ScrollAxisLock` | Scroll gesture deadzone, dominance, and timeout |
| `ScrollTween` | Pointer-wheel tween mode and fractional-delta tolerance |
| `TextInput` | Wheel step, key repeat, and keyboard-scroll timing |
| `RenderSurface` | Raster scale, surface budgets, atlas, and Vulkan backend |
| `WorldSpaceTextBox` | Default world-space material resource path |
| `Icu` | ICU resource and extracted-file paths |

## Font family mappings

```csharp
var fonts = MilestroConfiguration.Configuration.FontFamily;
fonts.SetKeywordMapping("heading", "Inter", "sans-serif");
fonts.SetKeywordMapping("code", "JetBrains Mono", "monospace");
```

Keyword names are case-insensitive. Values may refer to other keywords or to
literal family names. `ResetKeywordMapping("heading")` removes the override and
restores the built-in behavior for that keyword.

The built-in keywords are `serif`, `sans-serif`, `monospace`, and `system-ui`.

## Input shortcuts

`InputBoxShortcut` contains:

| Property | Default | Meaning |
| --- | --- | --- |
| `AcceptRedoWithControlY` | `true` | Accept Ctrl+Y for redo |
| `AcceptRedoWithShiftZ` | `true` | Accept Ctrl+Shift+Z for redo |
| `AcceptShiftClipboardShortcuts` | `true` | Accept Shift-modified clipboard shortcuts |

## Scroll axis lock

`ScrollAxisLock` contains:

| Property | Default | Meaning |
| --- | --- | --- |
| `DefaultGestureTimeoutSeconds` | `0.18` | Gesture reset timeout |
| `DefaultDeadzone` | `0.2` | Initial deadzone |
| `DefaultDominanceRatio` | `2` | Ratio used to choose an axis |
| `DefaultDecisionDistance` | `1` | Distance before a decision |
| `DefaultInitialCorrectionSeconds` | `0.08` | Initial correction interval |

## Scroll tween

`ScrollTween` contains:

| Property | Default | Meaning |
| --- | --- | --- |
| `PointerScrollTweenMode` | `Auto` | `Auto`, `AlwaysTween`, or `BypassFractional` |
| `FractionalDeltaTolerance` | `0.5` | Fractional delta treated as noise |

## TextInput defaults

`TextInput` contains:

| Property | Default | Meaning |
| --- | --- | --- |
| `ScrollWheelStepPixels` | `48` | Wheel step |
| `KeyRepeatInitialDelay` | `0.42` | Initial key-repeat delay |
| `KeyRepeatInterval` | `0.045` | Repeat interval |
| `KeyboardScrollInterlockTimeoutSeconds` | `0.03` | Keyboard/scroll interlock |
| `SurrogatePairTimeout` | `2.5` | Surrogate-pair assembly timeout |

## Render surface

`RenderSurface` contains:

| Property | Default | Meaning |
| --- | --- | --- |
| `MaxScreenSpaceRasterScale` | `2` | Maximum screen-space raster scale |
| `MinimumFallbackScale` | `0.25` | Minimum fallback scale |
| `ScaleQuantum` | `0.25` | Raster-scale quantization |
| `ScaleHysteresis` | `0.05` | Scale hysteresis |
| `ConservativeMaxTextureEdge` | `8192` | Conservative texture edge limit |
| `MaxPixelsPerSurface` | `16,777,216` | Per-surface pixel budget |
| `MaxBytesPerSurface` | `67,108,864` | Per-surface byte budget |
| `MaxGlobalBytes` | `268,435,456` | Global byte budget |
| `MaxTransitionBytes` | `134,217,728` | Transition byte budget |
| `MaxAttemptsPerRequestAndEpoch` | `8` | Allocation retry limit |
| `VulkanBackend` | `Direct` | `Direct` or `StagingCopy` |
| `EnableAtlas` | `true` | Share compatible render-texture regions |
| `AtlasPageSize` | `2048` | Atlas page edge in pixels |

Atlas examples:

```csharp
var surfaces = MilestroConfiguration.Configuration.RenderSurface;
surfaces.EnableAtlas = true;
surfaces.AtlasPageSize = 2048;
```

Each RGBA atlas page costs approximately
`AtlasPageSize * AtlasPageSize * 4` bytes. Set `EnableAtlas = false` to request
standalone textures. See [Rendering, batching, and backends](rendering.md).

## World-space material

```csharp
MilestroConfiguration.Configuration.WorldSpaceTextBox.DefaultMaterialResourcePath =
    "Milestro/TextBoxDefaultMeterial";
```

The misspelling in the default resource name is part of the current resource
path and must be preserved when overriding it.

## ICU

`Icu` contains:

| Property | Default | Meaning |
| --- | --- | --- |
| `IcudtlResourcePath` | `Milestro/icudtl.dat` | Unity `Resources` path without `.bytes` |
| `IcudtlPersistentFileName` | `icudtl.dat` | Extracted file name on mobile |
| `IcudtlPathOverride` | empty | Explicit path replacing the extracted-file path |

The default Unity asset is `Resources/Milestro/icudtl.dat.bytes`. Editor and
standalone builds load the bytes from memory; mobile builds write them to the
configured persistent path, then load ICU from that file.

`MILESTRO_NO_ICU_INIT` disables automatic ICU initialization so an application
can perform its own setup.

## Replacing configuration

`MilestroConfiguration.Configuration` is a static property with a setter. A
project can replace the object before boot, but keeping the default object and
changing its groups is less error-prone:

```csharp
var configuration = MilestroConfiguration.Configuration;
configuration.TextInput.ScrollWheelStepPixels = 64f;
configuration.RenderSurface.EnableAtlas = false;
```

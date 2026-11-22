# Text pipeline validation

[English](text-pipeline-validation.md) | [简体中文](../../zh/development/text-pipeline-validation.md)

This is a developer guide for benchmarking and diagnosing Milestro. For normal
Unity use, start with [Text, fonts, and rich text](../guides/text.md).

Use the text pipeline benchmark to compare the existing render-texture atlas with experimental glyph mesh presentation in a Windows Development Player. It covers both Paragraph/TextBox and CanvasSlimText. Build the managed plugin and native library from the same revision before preparing the Unity validation project.

## Build and run

The Unity project must import `Milestro`, `Milestro.Tests/Support`, `Milestro.Tests/EditorMode`, their dependencies, and the plugin `Resources` directory from `apps/unity-plugins/`. Include the matching native Windows library and ICU data using the normal plugin installation procedure. Use a graphics-capable host; do not pass `-nographics` to the Player.

Run the builder from the repository root, substituting the Unity executable and project directory:

```sh
"<unity-editor>" -batchmode -quit -projectPath "<unity-project>" \
  -executeMethod Milestro.Tests.TextPipelineBenchmarkBuilder.Build \
  -logFile "build/text-pipeline-player-build.log"
```

The builder creates `Assets/MilestroTextPipelineBenchmark.unity`, enables frame timing statistics for the build, and produces a Mono Development Player at `Build/TextPipelineBenchmark/TextPipelineBenchmark.exe` within the Unity project. `MILESTRO_TEXT_BENCHMARK_PLAYER` can override the executable output path. The glyph shader is loaded from `Resources/Milestro/TextGlyphMesh.shader`; the runner rejects a missing or unsupported shader or Alpha8 texture support.

First run a short fixture check:

```sh
"<unity-project>/Build/TextPipelineBenchmark/TextPipelineBenchmark.exe" \
  -force-d3d11 -screen-fullscreen 1 -window-mode borderless \
  -screen-width 1920 -screen-height 1080 \
  --text-counts=9 --text-warmup=16 --text-samples=16 --text-repeats=1 \
  --text-native-config="<native-build-configuration>" \
  --text-native-sha256="<native-library-sha256>" \
  --text-output="build/text-pipeline-smoke.json" \
  -logFile "build/text-pipeline-smoke.log"
```

For the default dynamic matrix, omit the count, warmup, sample, and repeat overrides:

```sh
"<unity-project>/Build/TextPipelineBenchmark/TextPipelineBenchmark.exe" \
  -force-d3d11 -screen-fullscreen 1 -window-mode borderless \
  -screen-width 1920 -screen-height 1080 \
  --text-native-config="<native-build-configuration>" \
  --text-native-sha256="<native-library-sha256>" \
  --text-output="build/text-pipeline-timing.json" \
  -logFile "build/text-pipeline-timing.log"
```

Output paths are relative to the process working directory. Alternatively, set `MILESTRO_TEXT_BENCHMARK_REPORT`; without either option the report goes to Unity's persistent data directory. The native configuration and SHA-256 arguments are provenance supplied by the caller; the runner does not verify the binary hash. Record the actual native build settings, including the Skia configuration, alongside the report. A Development Player does not imply an optimized native library.

Check `screenWidth` and `screenHeight` in every report. Unity or the window manager can clamp a requested window size; command-line dimensions alone do not establish the actual rendering resolution. The 500-label fixture fits the fixed geometry at 1920 by 1080; a smaller actual resolution can fail the grid-size check. Use a suitable display mode, such as borderless fullscreen at the intended resolution, and retain the fixed label geometry when comparing results.

The Player exits nonzero for errors, missing output, mesh fallback, incorrect presented glyph materials, missing texture-atlas submissions in a dynamic baseline, or additional glyph rasterizations/uploads after prewarming. A successful fixture check establishes execution correctness; it does not establish a performance improvement.

## Workload and pairing

Defaults are 9, 100, and 500 visible labels, both `paragraph` and `slim`, 120 warmup frames, 240 measured frames, and two repeats. Each repeat uses ABBA order: optimized texture atlas, glyph mesh, glyph mesh, optimized texture atlas. Both paths keep the existing texture-atlas optimization enabled. Every label uses a fixed 132 by 28 logical-pixel rectangle with a 16-pixel font; grid cells only determine spacing. The runner rejects resolutions too small to show the full fixture and records the actual screen size and requested font families. Resolved system fonts can differ between machines.

The fixed-seed corpus contains precomputed eight-character alphanumeric strings. Each pair consumes the same strings in the same order, and the report includes a corpus checksum. Corpus creation, random number generation, strings, and sample storage are prepared outside the measured interval. Each case first warms the complete character set at the same font and raster scale. Shared glyph atlas pages persist between ABBA cases; each case still repeats prewarming.

The runner uses normal Canvas scheduling and presentation. It does not call `Canvas.ForceUpdateCanvases` or explicitly rebuild each component in the measured loop. All labels update each frame by default. Additional options are:

| Option | Meaning |
| --- | --- |
| `--text-counts=9,100,500` | Label counts, each from 1 through 500. |
| `--text-kinds=paragraph,slim` | Component families. |
| `--text-warmup=120` | Warmup frames, at least 16. |
| `--text-samples=240` | Measured frames, at least 16. |
| `--text-repeats=2` | ABBA repeats. |
| `--text-seed=2463534242` | Nonzero deterministic corpus seed. |
| `--text-digits` | Use only digits. |
| `--text-static` | Keep content unchanged after warmup. |
| `--text-attribution` | Enable detailed pipeline diagnostics for a separate attribution run. |
| `--text-font-mappings` | Configure `body` as exact `Microsoft YaHei UI` followed by `system-ui`, configure `heading` as `body`, and request `body` in Paragraph fixtures. Reports record this option and the requested family. Use the same option for both revisions. |
| `--text-freshness` | Enable content-version and CPU-completion tracking in a separate run. |
| `--text-completion-timing` | Enable producer version counters, observing at the request boundary and during bounded drain rather than every request frame. Run separately from timing and attribution. |
| `--text-focus-diagnostics` | Sample internal focus and its timestamp at each request/drain observation in a separate diagnostic run. |
| `--text-diagnostic-trace` | Record preallocated per-label version/counter and output/presenter observations at every request, waiting and drain frame. Requires a separate dynamic 9-Paragraph freshness run. |
| `--text-request-interval-ms=4` | In a diagnostic trace, request the same corpus at absolute wall-clock deadlines instead of every frame. Zero keeps requests unpaced; finite values up to 100 ms are supported. |
| `--text-measurement-fault=never-completes` | Inject incomplete measurement observations to validate failure reporting and exit status. Requires freshness, excludes attribution and completion-timing; never use for performance results. |
| `--text-screenshot=<prefix>` | Save each case after measurement and drain for resolution/quality inspection. |

## Read the measurements

The default timing run disables detailed pipeline diagnostics and the older SlimText/TextBox profilers. Run the same command again with `--text-attribution` and a different report path to locate costs. Do not mix the attribution timing numbers into the primary A/B result: its Stopwatch scopes and counters add work.

Run `--text-freshness` separately with the same inputs for both modes. It reports requests, preparation attempts, accepted submissions, unique completed content versions, valid outputs, presenter synchronization, and version lag. Completion means a texture `Drawn` callback or synchronous mesh publication, not GPU display. Latency samples include the latest newly completed version per label per observation; carryover events and pending tail requests have separate semantics. Standalone surfaces can have multiple pending submissions without identifiable completion callbacks; these are marked unsupported and fail freshness-case acceptance.

Schema 3 separates `requestPhase` from `drainPhase` in freshness and completion-timing runs. Requests still change every frame. After the fixed request window, setters stop while normal scheduling continues for at least one frame, with a limit of 120 frames or 5000 ms, whichever comes first. A timeout fails the case. Both phases retain wall time, completed counts, preparation/submission counts, frame allocation totals and raw frame samples. `requestFreshness` stops at the request boundary; `freshness` includes drain. GPU timing's separate eight-frame sample collection is outside this CPU completion interval.

The time limit has priority: an observation at or beyond 5000 ms fails even if it reports convergence. Frame 120 can succeed before that time limit; otherwise it fails without attempting frame 121. `completionDrainDecision` records the reason. A later valid snapshot cannot rescue a timed-out case. Case failure propagates to the report's `failed` field and Player exit code 1. The coroutine requires the main thread to keep advancing frames; it cannot terminate a stalled main thread. The external measurement runner must enforce a separate process deadline, retain partial logs and focus observations, and report termination distinctly from a Player acceptance failure.

`TextPipelineCompletionGateTests` exercises incomplete content, stale presenters, missing outputs, unsupported tracking, both limits, normal and boundary convergence, and failure propagation through the shared measurement gate. For an end-to-end failure check, run a short freshness fixture with `--text-measurement-fault=never-completes` and require `failed=true`, failed cases, `completionAccepted=false`, and process exit 1. This changes only measurement observations, including synthetic incomplete final snapshots; producer state and counters remain real. The report marks the injection and its scope. Retain that report separately from valid baseline data.

Inspect every entry in `beginVersions`, `requestEndVersions`, and `finalVersions`. Final acceptance requires a valid output, the latest requested content version completed, and the presenter's observed output revision equal to the producer's output revision. Content versions and output revisions are different sequences and must not be compared numerically to each other. Presenter synchronization is a CPU Graphic observation, not proof of Canvas execution or GPU display. Size queries may synchronously use newer layout while texture output is pending; interactive geometry retains its controlled publication contract.

`completedVersionsPerSecond` divides unique requested versions completed by request plus drain wall time. Drain cannot recover intermediate requests already merged by normal scheduling. Final convergence therefore does not imply 100% historical completion, and normalized throughput does not establish strict equal-completion performance when the counts differ. Keep the actual counts alongside percentiles. The completion-timing mode includes version-event counter overhead and boundary/drain observations; its latency samples cover only the latest completion at those observations and cannot be compared directly with per-frame freshness latency. Original timing-only runs leave tracking disabled and do not establish completion throughput.

Use diagnostic traces to investigate whether a shorter fixed-frame request window changes asynchronous merging. Compare an unpaced trace with separately reported fixed-cadence controls using the same 240 requests and corpus. Waiting advances normal Unity frames and retains their observations; deadlines do not skip requests or batch multiple updates into one frame. Record actual request intervals and deadline lateness before treating the controls as equal-cadence workloads. Paced controls change the offered load and cannot replace the original acceptance run or waive its completion-rate budget.

Trace requests include setter timestamps, Unity frame numbers and target deadlines. Each observation contains its timestamp bracket and one flattened block of label states, indexed by observation then label. These retain content request/preparation/submission/completion versions and counters separately from output/presenter revisions. A counter increase greater than one exposes intermediate events that the latest-state samples cannot identify. Bound CPU completion latency using the request timestamp and adjacent observations; do not describe these bounds as exact callback times. Presenter synchronization is still a CPU Graphic observation. The trace buffer is allocated before measurement; exhaustion fails and retains the partial trace instead of deleting observations. Trace overhead and its preallocated memory make this a separate diagnostic workload, unsuitable for primary CPU timing acceptance.

Compare resolutions in separate groups. Record `screenWidth/Height`, display rendering/system dimensions, render scale factors, Canvas pixel size and scale, and each version snapshot's backing texture and UV region dimensions. For texture output, a 2048-pixel atlas does not mean each label has a 2048-pixel raster: use `outputRegionWidth/Height`, which take the absolute UV extent to account for vertical flipping. For mesh output these fields describe the glyph atlas page, not a single label raster. A low-resolution fullscreen framebuffer scaled to a native 4K display can blur output independently of text rasterization. Use native framebuffer captures to distinguish scaling from text-edge quality; fixed 132 by 28 labels and a 16-pixel font remain the same logical size without a Canvas scaler.

Keep the Player foreground and retain focus-change records without removing slow frames. Phase-end `applicationFocusedAtEnd` fields do not prove uninterrupted focus. If external window observations disagree with the Player's focus state, report that uncertainty and resolve window identity and timing alignment before claiming controlled performance. Frame allocation totals also follow engine frame boundaries rather than exact phase wall-clock boundaries, and can include harness setup allocations in the first sampled frame.

Reports include the target process ID, Stopwatch frequency, internal focus callbacks, and each case's request start/end and drain end timestamps. On Windows, `focusClockCalibration` brackets a native QueryPerformanceCounter reading with Stopwatch readings. Use this anchor, frequency conversion and its uncertainty to correlate external samples; matching frequencies alone do not establish matching origins. Unity Mono can report Stopwatch timestamps relative to its own origin. Observe all target-process windows, root/owner relationships and window replacement rather than selecting one enumerated window indefinitely. Treat failed window enumeration or an unavailable foreground handle as unknown observation. Store only target identities and focus state; omit unrelated process information and window titles. Separate startup/prewarm and report-writing events from measured intervals. Sampling cannot rule out transitions between polls, and phase-end self-reports cannot override conflicting external evidence.

Internal focus callbacks and three phase timestamps are recorded in ordinary runs; they add boundary work and work when focus changes. Per-observation `Application.isFocused` reads and timestamps require `--text-focus-diagnostics` and add work to that diagnostic run. Leave this option off for timing baselines. Avoid repeated foreground requests during measurement; any startup request must be bounded and recorded. Before a baseline sequence, fix its run count, resolution, repeatability thresholds and handling of interference. Preserve every frame and failed sequence; same-product repetitions establish repeatability, not an optimization benefit.

Raster phases are part of the glyph cache key. Warming each character once does not necessarily warm its positions in other strings. The runner replays the complete deterministic corpus before measurement and requires a stable pass without glyph rasterizations or uploads. It reports preparation passes, frames, elapsed time, and cache additions. These results describe a corpus-warmed workload; shared atlas pages persist across cases, so preparation measurements are not independent cold-start measurements.

Reports contain raw completed-frame samples and p50/p95 summaries with availability and sample counts. Frame duration is the unscaled interval between frames. Main-thread and render-thread profiler durations include waits and are not isolated text CPU work. Render-thread timing may be unavailable; its summary and raw values then contain null. Draw calls and per-frame GC allocation describe the complete benchmark scene, including the engine and harness. Collection counts and ending memory are captured before diagnostic snapshot and result allocations.

GPU timing uses unique asynchronous `FrameTimingManager` samples whose CPU timestamps fall within the measurement interval. Samples are deduplicated and drained after measurement. The first boundary frame is excluded; the final interval ends before the next idle/reporting frame. GPU samples are stored separately and are not paired with individual CPU rows. No timing, unsupported timing, or incompatible CPU clocks yields an unavailable metric with null percentiles rather than zero. Do not infer GPU speed from missing samples.

The detailed snapshot reports invocation counts and inclusive elapsed times for preparation, paragraph building/layout, glyph snapshot export, mesh construction/population, rasterization, uploads, and texture submissions. These diagnostics run on the Unity main thread. Nested times overlap and must not be added. Paragraph layout can include shaping, but the counters do not isolate native shaping passes or Skia cache hits. Texture region counts describe submitted regions rather than observed GPU draw calls. Upload bytes describe Alpha8 CPU page bytes passed to `Texture.Apply`, not measured driver transfers.

The dynamic Paragraph fixture uses single-line layout. Its natural-width probe and final layout use different widths: the probe measures content, while the final layout determines alignment and positions consumed by painting, mesh export, and interaction geometry. Two layout calls per update do not establish redundant shaping. Preserve layout work required by changed widths, font sizing, layout iterations, or synchronous queries. Static Paragraph glyph exports are already retained until paragraph replacement or layout invalidation; scrolling alone can reuse them.

No-wrap width queries take a UTF-16 range extent while retaining SkParagraph intrinsic and line metrics, range boxes, and the final cluster query. Managed string inputs use their UTF-16 length, including the units preserved by default UTF-8 surrogate replacement. Native UTF-8 inputs count with the same SkUnicode conversion semantics as the editing boundary map, including malformed input. Validate supplementary characters, isolated and split surrogates, embedded NUL, combining and ZWJ tails, and trailing placeholders. InputBox measures its display text, which can differ from committed text during composition or masking.

Resetting font-family keyword mappings when no user mappings exist preserves SkParagraph and font-resolution caches. Managed synchronization replaces the complete nonempty mapping table through one native call. Native code compares normalized keyword keys and ordered token kinds and values against the current table, preserving caches when they match. Keyword insertion order does not matter; token order and bare versus exact tokens do matter. This conservative comparison can invalidate for different declarations that happen to resolve to the same installed font.

Synchronization reads configuration contents each time, including replacement configuration objects and in-place changes, rather than remembering a managed object reference. Native owns the current table, so direct native mapping changes and a new font collection are detected on the next managed synchronization. Managed configuration remains authoritative at that point. Configuration mutation must not race synchronization; the existing mutable dictionary and returned lists do not provide concurrent mutation guarantees. Nonempty synchronization still marshals and compares the configuration; it is not free.

Removing or changing actual mappings, setting mappings through the individual native setter, explicit cache clears, and successful registration through the font-registration C API still invalidate caches. Empty user overrides remain distinct from missing keywords. Validate both unchanged and changed configuration paths. Retained caches can increase memory compared with clearing them on every style preparation: the paragraph cache limits entry count, while face and variation caches have no explicit capacity limit. Unity and managed memory observations cannot establish a long-term native cache bound.

When evaluating preparation changes, retain a baseline Player with its matching native library and compare it with the candidate under the same workload and actual resolution. Compare both texture and mesh modes across revisions; the texture/mesh ABBA sequence within one Player does not itself measure the effect of a code change. Measure timing, attribution, and freshness separately for both revisions, and check completion rates before interpreting lower allocation or CPU cost as equivalent-throughput improvement.

Slim mesh export reuses the current simple-text layout through an owner-versioned view. It copies one 24-byte font descriptor plus 12 bytes per glyph for identity and position; Paragraph retains its owned layout snapshot. The Slim view does not export ink bounds, and becomes invalid when its owner changes or is disposed. Published mesh outputs own their quad buffers independently. Measurement and atlas rasterization consume the owner's native layout synchronously on the main thread.

Memory fields have distinct scopes. `committedRenderSurfaceBytes` covers the render-surface budget ledger, excluding glyph pages and other graphics allocations. `glyphOutputTextureBytes` counts distinct Alpha8 pages referenced by the current fixture; unreferenced shared pages retained from earlier cases are excluded. Unity allocated memory and managed heap measurements have their own scopes. None is total GPU memory usage or a driver memory budget.

This harness targets a Windows Development Player with the selected graphics API. It does not establish release-build, mobile, other backend, text-quality, or production-scene results. Compare paired repeats, GC behavior, and frame tails before drawing conclusions; keep image comparisons and platform acceptance separate from timing.

For an independently observed startup gate, diagnostic traces accept `--text-start-signal=<signal-file>`. The Player yields normal frames before workload setup until an external observer creates the file; waiting fails after 60 seconds. Use a fresh file for each process, retain the external release timestamp and report's `diagnosticGateReleasedTimestamp`, and enforce a separate process deadline. A startup gate does not replace foreground guards over the actual request and drain intervals.

Current implementation and validation status are summarized in
[Text pipeline implementation](text-pipeline.md#validation-status).

## Image comparisons

Run `TextGlyphMeshImageComparisonTests` in Unity PlayMode on a graphics-capable host. Set `MILESTRO_TEXT_IMAGE_OUTPUT` to an output directory. The fixture locates `tests/data/font/LXGWWenKai-Regular.ttf` through repository ancestors; `MILESTRO_TEXT_IMAGE_FONT` can select the fixture font explicitly. Each case writes `baseline.png`, `candidate.png`, `difference.png`, and `summary.json` before asserting the fixed thresholds. The summary records the font hash, actual coordinates, Canvas scale, thresholds, and measurement semantics.

Cases cover integer placement, small fonts with fractional placement and Canvas scaling, hard RectMask2D edges, and multiline Paragraph family-list fallback. Family-list fallback does not establish per-glyph fallback, BiDi, emoji, or complex-script quality. Integrated coverage and centroid are computed from rendered red intensity on black; they are not direct alpha coverage or metrics independent of antialiasing.

CanvasSlimText and TextBox use a premultiplied-alpha material for their default texture presentation. This corrects the previous double application of edge coverage and can change text-edge appearance. Custom materials retain their existing behavior. Include `Resources/Milestro/TextPremultiplied.shader` when installing the plugin.

Glyph mesh remains opt-in and currently requires Gamma project color space. Linear projects use texture rendering because the canonical mask does not meet the same image thresholds there. Its canonical white mask supports white source RGB and source opacity; nonwhite source text, including a single colored Paragraph run, falls back as a whole to texture rendering. Skia adjusts raster coverage using source-color luminance, so tinting a white mask does not reproduce arbitrary colored source text. Graphic RGB tint remains a presentation operation and can color white-source mesh output. Image reports identify texture fallback explicitly; a passing fallback comparison is not evidence of Linear or colored glyph-mesh equivalence.

For TextBox, set presentation tint through `((UnityEngine.UI.Graphic)textBox).color`; the compatibility property `TextBox.color` changes source text color. For CanvasSlimText, set the RawImage tint after `Configure()`, which resets it to white.

Keep failing images and thresholds intact when investigating remaining differences. Performance acceptance also requires content freshness: identical setter input does not establish identical presented versions or latency. A texture baseline can submit fewer updated regions than requested while a mesh candidate updates every label. Record version/latency evidence before treating those results as equal presentation throughput. Native phase tests cover selected scales and origins; they do not establish arbitrary scaling, platform, or color-space equivalence.

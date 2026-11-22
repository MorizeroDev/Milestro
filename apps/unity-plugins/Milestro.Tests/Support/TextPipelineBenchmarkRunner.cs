using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Configuration;
using Milestro.Model;
using Milestro.Skia;
using Newtonsoft.Json;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Canvas = UnityEngine.Canvas;
using Debug = UnityEngine.Debug;
using Path = System.IO.Path;

namespace Milestro.Tests
{
    public sealed class TextPipelineBenchmarkRunner : MonoBehaviour
    {
        private readonly List<CaseResult> results = new List<CaseResult>();
        private readonly FrameTiming[] frameTimings = new FrameTiming[64];
        private readonly WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
        private Options options;
        private int errorCount;
        private string fatal;
        private Fixture activeFixture;
        private Recorders recorders;
        private readonly List<TextPipelineFocusEvent> focusEvents = new List<TextPipelineFocusEvent>();
        private string focusCaseId;
        private string focusPhase = "startup";
        private TextPipelineClockCalibration clockCalibration;
        private TextPipelineDiagnosticTrace activeTrace;
        private long diagnosticGateReleasedTimestamp;

        private void OnApplicationFocus(bool focused)
        {
            focusEvents.Add(new TextPipelineFocusEvent
            {
                timestamp = Stopwatch.GetTimestamp(),
                applicationFocused = focused,
                caseId = focusCaseId,
                phase = focusPhase
            });
        }

        private void Awake()
        {
            clockCalibration = TextPipelineClockCalibration.Capture();
            Application.logMessageReceived += OnLog;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            recorders?.Dispose();
            TextPipelineDiagnostics.Enabled = false;
            TextPipelineDiagnostics.FreshnessEnabled = false;
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                ++errorCount;
            }
        }

        private IEnumerator Start()
        {
            var execution = Execute();
            while (true)
            {
                object current;
                try
                {
                    if (!execution.MoveNext())
                    {
                        break;
                    }
                    current = execution.Current;
                }
                catch (Exception exception)
                {
                    fatal = exception.ToString();
                    Debug.LogException(exception);
                    break;
                }
                yield return current;
            }
            TextPipelineDiagnostics.Enabled = false;
            TextPipelineDiagnostics.FreshnessEnabled = false;
            if (activeFixture != null)
            {
                Destroy(activeFixture.Root);
            }
            WriteReport();
        }

        private IEnumerator Execute()
        {
            options = Options.Parse(Environment.GetCommandLineArgs());
            if (options.FontMappings)
            {
                MilestroConfiguration.Configuration.FontFamily.SetKeywordMapping("body", "\"Microsoft YaHei UI\"", "system-ui");
                MilestroConfiguration.Configuration.FontFamily.SetKeywordMapping("heading", "body");
            }
            OnApplicationFocus(Application.isFocused);
            if (!string.IsNullOrEmpty(options.StartSignal))
            {
                var gateStarted = Stopwatch.GetTimestamp();
                focusPhase = "waiting-for-external-foreground";
                while (!File.Exists(options.StartSignal))
                {
                    if ((Stopwatch.GetTimestamp() - gateStarted) / (double)Stopwatch.Frequency >= 60)
                    {
                        throw new TimeoutException("External foreground start signal was not received within 60 seconds.");
                    }
                    yield return null;
                }
                diagnosticGateReleasedTimestamp = Stopwatch.GetTimestamp();
            }
            if (Application.isEditor || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                throw new InvalidOperationException("Run the Development Player with a graphics device.");
            }
            var glyphShader = Resources.Load<Shader>("Milestro/TextGlyphMesh");
            if (glyphShader == null || !glyphShader.isSupported || !SystemInfo.SupportsTextureFormat(TextureFormat.Alpha8))
            {
                throw new InvalidOperationException("The Player requires the supported Milestro/TextGlyphMesh resource shader and Alpha8 textures.");
            }
            TextBoxPipelineProfiler.Enabled = false;
            SlimTextPipelineProfiler.Enabled = false;
            TextPipelineDiagnostics.Enabled = options.Attribution;
            TextPipelineDiagnostics.FreshnessEnabled = options.TrackCompletion;
            recorders = new Recorders();
            if (!recorders.Main.Valid || !recorders.DrawCalls.Valid || !recorders.GcAlloc.Valid)
            {
                throw new InvalidOperationException("Required main-thread, draw-call, or GC recorder is unavailable.");
            }
            var cameraObject = new GameObject("Benchmark Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            foreach (var count in options.Counts)
            {
                var data = new TextPipelineBenchmarkData(options.Warmup + options.Samples, count,
                    options.Seed, options.Digits ? TextPipelineBenchmarkData.Digits : TextPipelineBenchmarkData.Alphanumeric);
                foreach (var kind in options.Kinds)
                {
                    for (var repeat = 0; repeat < options.Repeats; ++repeat)
                    {
                        for (var order = 0; order < 4; ++order)
                        {
                            var glyphMesh = order == 1 || order == 2;
                            var run = RunCase(kind, count, repeat, order, glyphMesh, data);
                            while (run.MoveNext())
                            {
                                yield return run.Current;
                            }
                        }
                    }
                }
            }
            Destroy(cameraObject);
        }

        private IEnumerator RunCase(string kind, int count, int repeat, int order, bool glyphMesh,
            TextPipelineBenchmarkData data)
        {
            focusCaseId = kind + "-" + count + "-" + repeat + "-" + order;
            focusPhase = "prewarm";
            MilestroConfiguration.Configuration.RenderSurface = new RenderSurfaceConfiguration
            {
                EnableAtlas = true,
                EnableTextGlyphMesh = glyphMesh,
                AtlasPageSize = 2048,
                GlyphAtlasPageSize = 2048,
                MaxGlyphAtlasPages = 2
            };
            var prewarmStarted = Stopwatch.GetTimestamp();
            var prewarmRasterized = TextGlyphAtlas.RasterizedGlyphs;
            var prewarmUploads = TextGlyphAtlas.Uploads;
            activeFixture = Fixture.Create(kind, count, data.Prewarm[0], options.FontMappings);
            for (var frame = 0; frame < 600 && !activeFixture.Ready(glyphMesh); ++frame)
            {
                yield return null;
            }
            if (!activeFixture.Ready(glyphMesh))
            {
                throw new InvalidOperationException("Fixture did not produce the requested output: " + kind + "/" + glyphMesh);
            }
            foreach (var text in data.Prewarm)
            {
                activeFixture.SetAll(text);
                yield return endOfFrame;
                yield return null;
            }
            var corpusPrewarmPasses = 0;
            var corpusPrewarmFrames = options.Static ? 1 : data.Frames.GetLength(0);
            var corpusStable = false;
            while (corpusPrewarmPasses < 3 && !corpusStable)
            {
                var passRasterized = TextGlyphAtlas.RasterizedGlyphs;
                var passUploads = TextGlyphAtlas.Uploads;
                for (var frame = 0; frame < corpusPrewarmFrames; ++frame)
                {
                    activeFixture.SetFrame(data, frame);
                    yield return endOfFrame;
                    yield return null;
                }
                ++corpusPrewarmPasses;
                activeFixture.Validate(glyphMesh);
                corpusStable = corpusPrewarmPasses >= 2 && (!glyphMesh ||
                    TextGlyphAtlas.RasterizedGlyphs == passRasterized &&
                    TextGlyphAtlas.Uploads == passUploads);
            }
            if (!corpusStable)
            {
                throw new InvalidOperationException("Glyph and phase corpus prewarm did not stabilize within three passes.");
            }
            var corpusPrewarmMs = (Stopwatch.GetTimestamp() - prewarmStarted) * 1000d / Stopwatch.Frequency;
            var corpusPrewarmRasterizations = TextGlyphAtlas.RasterizedGlyphs - prewarmRasterized;
            var corpusPrewarmUploadCount = TextGlyphAtlas.Uploads - prewarmUploads;
            for (var frame = 0; frame < options.Warmup; ++frame)
            {
                activeFixture.SetFrame(data, options.Static ? 0 : frame);
                FrameTimingManager.CaptureFrameTimings();
                yield return endOfFrame;
                yield return null;
            }
            activeFixture.Validate(glyphMesh);
            var frames = new FrameSample[options.Samples];
            var trace = options.DiagnosticTrace
                ? new TextPipelineDiagnosticTrace(options.Samples, count, options.RequestIntervalMs) : null;
            activeTrace = trace;
            var drainFrames = options.TrackCompletion ? new FrameSample[TextPipelineCompletionGate.MaxFrames] : Array.Empty<FrameSample>();
            var requestVersions = options.TrackCompletion ? new TextPipelineFreshness[count] : null;
            var beginVersions = options.TrackCompletion ? new TextPipelineFreshness[count] : null;
            var finalVersions = options.TrackCompletion ? new TextPipelineFreshness[count] : null;
            var requestOutputs = options.TrackCompletion ? Fixture.AllocateVersions(count) : null;
            var beginOutputs = options.TrackCompletion ? Fixture.AllocateVersions(count) : null;
            var finalOutputs = options.TrackCompletion ? Fixture.AllocateVersions(count) : null;
            var gpuSamples = new List<GpuSample>(options.Samples + 64);
            var timingClockCompatible = FrameTimingManager.GetCpuTimerFrequency() == (ulong)Stopwatch.Frequency;
            var beforePipeline = options.Attribution ? TextPipelineDiagnostics.Capture() : null;
            yield return endOfFrame;
            yield return null;
            if (options.TrackCompletion)
            {
                activeFixture.BeginFreshness(options.Samples + drainFrames.Length);
                activeFixture.CaptureVersions(beginVersions, beginOutputs);
            }
            var beforeRasterized = TextGlyphAtlas.RasterizedGlyphs;
            var beforeUploads = TextGlyphAtlas.Uploads;
            var beforeSubmissions = RenderTextureAtlas.SubmissionCount;
            var beforeNative = UnitySkiaRenderTextureSurface.ReadNativeDiagnostics().AcceptedSubmissionCount;
            var beforeCollections = GC.CollectionCount(0);
            var beforeManagedMemory = GC.GetTotalMemory(false);
            var beforeUnityMemory = Profiler.GetTotalAllocatedMemoryLong();
            var beforeErrors = errorCount;
            focusPhase = "request";
            var firstTimestamp = (ulong)Stopwatch.GetTimestamp();
            if (trace != null) activeFixture.CaptureTrace(trace, "start", recorders.Read(-1));
            var lastTimestamp = ulong.MaxValue;
            var lastGpuTimestamp = 0UL;
            for (var frame = 0; frame < frames.Length; ++frame)
            {
                var deadline = trace != null
                    ? TextPipelineDiagnosticTrace.Deadline((long)firstTimestamp, frame, options.RequestIntervalMs, Stopwatch.Frequency) : 0;
                while (trace != null && Stopwatch.GetTimestamp() < deadline)
                {
                    FrameTimingManager.CaptureFrameTimings();
                    yield return endOfFrame;
                    yield return null;
                    activeFixture.CaptureTrace(trace, "wait", recorders.Read(frame));
                }
                var beforeSet = trace != null ? Stopwatch.GetTimestamp() : 0;
                activeFixture.SetFrame(data, options.Static ? 0 : options.Warmup + frame);
                if (trace != null)
                {
                    trace.requests[frame] = new TextPipelineDiagnosticTrace.Request
                    {
                        deadline = deadline, beforeSetTimestamp = beforeSet,
                        afterSetTimestamp = Stopwatch.GetTimestamp(), frame = Time.frameCount
                    };
                }
                FrameTimingManager.CaptureFrameTimings();
                yield return endOfFrame;
                if (frame == frames.Length - 1)
                {
                    lastTimestamp = (ulong)Stopwatch.GetTimestamp();
                }
                yield return null;
                frames[frame] = recorders.Read(frame);
                if (trace != null) activeFixture.CaptureTrace(trace, "request", frames[frame]);
                if (options.FocusDiagnostics)
                {
                    frames[frame].applicationFocused = Application.isFocused;
                    frames[frame].focusTimestamp = Stopwatch.GetTimestamp();
                }
                if (options.Freshness)
                {
                    frames[frame].freshness = activeFixture.ObserveFreshness();
                }
                ReadGpuSamples(gpuSamples, firstTimestamp, lastTimestamp,
                    timingClockCompatible, ref lastGpuTimestamp);
            }
            var requestEndTimestamp = Stopwatch.GetTimestamp();
            focusPhase = "drain";
            var requestEndFocused = Application.isFocused;
            var afterCollections = GC.CollectionCount(0);
            var afterManagedMemory = GC.GetTotalMemory(false);
            var afterUnityMemory = Profiler.GetTotalAllocatedMemoryLong();
            var requestRasterized = TextGlyphAtlas.RasterizedGlyphs;
            var requestUploads = TextGlyphAtlas.Uploads;
            var requestSubmissions = RenderTextureAtlas.SubmissionCount;
            var requestNative = UnitySkiaRenderTextureSurface.ReadNativeDiagnostics().AcceptedSubmissionCount;
            var pipeline = options.Attribution
                ? TextPipelineDiagnostics.Capture().DeltaFrom(beforePipeline) : null;
            var requestLatencyCount = 0;
            var drainCount = 0;
            var drainTimedOut = false;
            var drainDecision = TextPipelineCompletionGate.Decision.Waiting;
            var drainEndTimestamp = requestEndTimestamp;
            if (options.TrackCompletion)
            {
                if (!options.Freshness) activeFixture.ObserveFreshness();
                requestLatencyCount = activeFixture.CompletionLatencyCount;
                activeFixture.CaptureVersions(requestVersions, requestOutputs);
                do
                {
                    FrameTimingManager.CaptureFrameTimings();
                    yield return endOfFrame;
                    yield return null;
                    drainFrames[drainCount] = recorders.Read(drainCount);
                    if (trace != null) activeFixture.CaptureTrace(trace, "drain", drainFrames[drainCount]);
                    drainFrames[drainCount].freshness = activeFixture.ObserveFreshness();
                    if (options.FocusDiagnostics)
                    {
                        drainFrames[drainCount].applicationFocused = Application.isFocused;
                        drainFrames[drainCount].focusTimestamp = Stopwatch.GetTimestamp();
                    }
                    ++drainCount;
                    drainEndTimestamp = Stopwatch.GetTimestamp();
                    ReadGpuSamples(gpuSamples, firstTimestamp, lastTimestamp,
                        timingClockCompatible, ref lastGpuTimestamp);
                    var observed = drainFrames[drainCount - 1].freshness.Value;
                    if (options.MeasurementFault == "never-completes")
                    {
                        observed.currentCompletions = 0;
                        drainFrames[drainCount - 1].freshness = observed;
                    }
                    drainDecision = TextPipelineCompletionGate.Observe(drainCount,
                        (drainEndTimestamp - requestEndTimestamp) * 1000d / Stopwatch.Frequency,
                        count, observed.currentCompletions, observed.validOutputs,
                        observed.presenterSynced, observed.unsupportedLabels);
                } while (drainDecision == TextPipelineCompletionGate.Decision.Waiting);
                drainTimedOut = drainDecision == TextPipelineCompletionGate.Decision.FrameLimit ||
                    drainDecision == TextPipelineCompletionGate.Decision.TimeLimit;
                activeFixture.CaptureVersions(finalVersions, finalOutputs);
                if (options.MeasurementFault == "never-completes")
                {
                    foreach (var state in finalOutputs) state.completed = state.requested - 1;
                }
            }
            var drainCollections = GC.CollectionCount(0) - afterCollections;
            var requestPhase = options.TrackCompletion
                ? activeFixture.CompletionPhase(requestVersions, null, frames.Length,
                    (requestEndTimestamp - (long)firstTimestamp) * 1000d / Stopwatch.Frequency,
                    frames, frames.Length, afterCollections - beforeCollections) : null;
            var drainPhase = options.TrackCompletion
                ? activeFixture.CompletionPhase(finalVersions, requestVersions, drainCount,
                    (drainEndTimestamp - requestEndTimestamp) * 1000d / Stopwatch.Frequency,
                    drainFrames, drainCount, drainCollections) : null;
            if (requestPhase != null) requestPhase.applicationFocusedAtEnd = requestEndFocused;
            activeFixture.GetOutputGlyphMemory(out var glyphOutputTexturePages, out var glyphOutputTextureBytes);
            var result = new CaseResult
            {
                caseId = focusCaseId,
                requestStartTimestamp = (long)firstTimestamp,
                requestEndTimestamp = requestEndTimestamp,
                drainEndTimestamp = drainEndTimestamp,
                kind = kind,
                labels = count,
                repeat = repeat,
                order = order,
                mode = glyphMesh ? "glyphMesh" : "optimizedTextureAtlas",
                corpusChecksum = data.Checksum,
                prewarmPasses = corpusPrewarmPasses,
                prewarmFrames = corpusPrewarmFrames * corpusPrewarmPasses,
                prewarmMs = corpusPrewarmMs,
                prewarmGlyphRasterizations = corpusPrewarmRasterizations,
                prewarmGlyphUploads = corpusPrewarmUploadCount,
                expectedMeshLabels = glyphMesh ? count : 0,
                actualMeshLabels = activeFixture.MeshCount(),
                actualTextureLabels = activeFixture.TextureCount(),
                frames = frames,
                diagnosticTrace = trace,
                diagnostics = pipeline,
                freshness = options.TrackCompletion ? activeFixture.EndFreshness() : null,
                requestFreshness = options.TrackCompletion ? activeFixture.EndFreshness(requestVersions, requestLatencyCount) : null,
                requestPhase = requestPhase,
                drainPhase = drainPhase,
                requestEndVersions = requestOutputs,
                beginVersions = beginOutputs,
                finalVersions = finalOutputs,
                drainTimedOut = drainTimedOut,
                completionAccepted = options.TrackCompletion && TextPipelineCompletionGate.AcceptFinal(drainDecision, finalOutputs),
                completionDrainDecision = options.TrackCompletion ? drainDecision.ToString() : "Disabled",
                drainFrames = new ArraySegment<FrameSample>(drainFrames, 0, drainCount),
                canvasPixelWidth = activeFixture.Root.GetComponent<Canvas>().pixelRect.width,
                canvasPixelHeight = activeFixture.Root.GetComponent<Canvas>().pixelRect.height,
                canvasScaleFactor = activeFixture.Root.GetComponent<Canvas>().scaleFactor,
                warmedGlyphRasterizations = requestRasterized - beforeRasterized,
                warmedGlyphUploads = requestUploads - beforeUploads,
                texturePageSubmissions = requestSubmissions - beforeSubmissions,
                nativeSubmissions = requestNative - beforeNative,
                drainTexturePageSubmissions = RenderTextureAtlas.SubmissionCount - requestSubmissions,
                drainNativeSubmissions = UnitySkiaRenderTextureSurface.ReadNativeDiagnostics().AcceptedSubmissionCount - requestNative,
                drainGlyphRasterizations = TextGlyphAtlas.RasterizedGlyphs - requestRasterized,
                drainGlyphUploads = TextGlyphAtlas.Uploads - requestUploads,
                gen0Collections = afterCollections - beforeCollections,
                managedMemoryBefore = beforeManagedMemory,
                managedMemoryAfter = afterManagedMemory,
                unityMemoryBefore = beforeUnityMemory,
                unityMemoryAfter = afterUnityMemory,
                committedRenderSurfaceBytes = UnitySkiaRenderTextureSurface.BudgetLedger.Snapshot().CommittedBytes,
                glyphOutputTexturePages = glyphOutputTexturePages,
                glyphOutputTextureBytes = glyphOutputTextureBytes,
                textureAtlasPages = RenderTextureAtlas.PageCount,
                errors = errorCount - beforeErrors,
                cellWidth = activeFixture.CellSize.x,
                cellHeight = activeFixture.CellSize.y,
                labelWidth = Fixture.LabelWidth,
                labelHeight = Fixture.LabelHeight,
                fontSize = Fixture.FontSize,
                requestedFontFamilies = activeFixture.RequestedFontFamilies,
                gpuClockCompatible = timingClockCompatible
            };
            activeFixture.Validate(glyphMesh);
            focusPhase = "reporting";
            for (var drain = 0; drain < 8; ++drain)
            {
                FrameTimingManager.CaptureFrameTimings();
                yield return endOfFrame;
                yield return null;
                ReadGpuSamples(gpuSamples, firstTimestamp, lastTimestamp,
                    timingClockCompatible, ref lastGpuTimestamp);
            }
            gpuSamples.RemoveAll(sample => sample.frameStartTimestamp >= lastTimestamp);
            result.gpuFrames = gpuSamples.ToArray();
            result.gpuMs = Metric.FromGpu(gpuSamples);
            result.frameMs = Metric.FromFrames(frames, sample => sample.frameMs);
            result.mainThreadMs = Metric.FromFrames(frames, sample => sample.mainThreadMs);
            result.renderThreadMs = recorders.Render.Valid
                ? Metric.FromFrames(frames, sample => sample.renderThreadMs.GetValueOrDefault()) : Metric.Unavailable();
            result.drawCalls = Metric.FromFrames(frames, sample => sample.drawCalls);
            result.gcAllocBytes = Metric.FromFrames(frames, sample => sample.gcAllocBytes);
            result.drainFrameMs = drainCount > 0
                ? Metric.FromFrames(drainFrames, sample => sample.frameMs, drainCount) : Metric.Unavailable();
            result.drainGcAllocBytes = drainCount > 0
                ? Metric.FromFrames(drainFrames, sample => sample.gcAllocBytes, drainCount) : Metric.Unavailable();
            if (options.TrackCompletion)
            {
                result.totalCompletionWallMs = requestPhase.wallMs + drainPhase.wallMs;
                result.completedVersionsPerSecond = result.freshness.completedRequestedVersions * 1000d /
                    result.totalCompletionWallMs;
            }
            var fixtureAccepted = result.errors == 0 && result.actualMeshLabels == result.expectedMeshLabels &&
                (glyphMesh || result.actualTextureLabels == count) && result.drawCalls.p50 > 0 &&
                (!options.Freshness || result.freshness.unsupportedLabels == 0) &&
                (glyphMesh ? result.warmedGlyphRasterizations == 0 && result.warmedGlyphUploads == 0 :
                    result.textureAtlasPages > 0 && (options.Static || result.texturePageSubmissions > 0));
            result.completed = TextPipelineCompletionGate.CaseCompleted(fixtureAccepted,
                options.TrackCompletion, result.completionAccepted);
            results.Add(result);
            activeTrace = null;
            if (!string.IsNullOrEmpty(options.Screenshot))
            {
                yield return endOfFrame;
                var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                result.screenshotWidth = screenshot.width;
                result.screenshotHeight = screenshot.height;
                var screenshotPath = options.Screenshot + "-" + kind + "-" + count + "-" + repeat + "-" + order + ".png";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(screenshotPath))!);
                File.WriteAllBytes(screenshotPath, screenshot.EncodeToPNG());
                Destroy(screenshot);
            }
            Debug.Log("TEXT_PIPELINE_CASE " + kind + " " + count + " " + result.mode + " " + result.completed);
            Destroy(activeFixture.Root);
            activeFixture = null;
            for (var drain = 0; drain < 16; ++drain)
            {
                yield return null;
            }
            if (!result.completed)
            {
                throw new InvalidOperationException("A/B fixture or warmed glyph reuse validation failed.");
            }
        }

        private void ReadGpuSamples(List<GpuSample> destination, ulong firstTimestamp, ulong lastTimestamp,
            bool compatibleClock, ref ulong lastSeenTimestamp)
        {
            if (!compatibleClock)
            {
                return;
            }
            var count = FrameTimingManager.GetLatestTimings((uint)frameTimings.Length, frameTimings);
            var previousWatermark = lastSeenTimestamp;
            for (var index = 0; index < count; ++index)
            {
                var timing = frameTimings[index];
                lastSeenTimestamp = Math.Max(lastSeenTimestamp, timing.frameStartTimestamp);
                if (timing.frameStartTimestamp <= previousWatermark || timing.frameStartTimestamp <= firstTimestamp ||
                    timing.frameStartTimestamp >= lastTimestamp || timing.gpuFrameTime <= 0 ||
                    double.IsNaN(timing.gpuFrameTime) || double.IsInfinity(timing.gpuFrameTime))
                {
                    continue;
                }
                destination.Add(new GpuSample
                {
                    frameStartTimestamp = timing.frameStartTimestamp,
                    gpuMs = timing.gpuFrameTime
                });
            }
        }

        private void WriteReport()
        {
            var output = options?.Output ?? Environment.GetEnvironmentVariable("MILESTRO_TEXT_BENCHMARK_REPORT");
            if (string.IsNullOrEmpty(output))
            {
                output = Path.Combine(Application.persistentDataPath, "text-pipeline-benchmark.json");
            }
            var exitCode = TextPipelineCompletionGate.ExitCode(fatal != null, errorCount,
                results.Count, results.Exists(result => !result.completed));
            var failed = exitCode != 0;
            var report = new
            {
                schemaVersion = 3,
                processId = Process.GetCurrentProcess().Id,
                stopwatchFrequency = Stopwatch.Frequency,
                focusClockCalibration = clockCalibration,
                focusDiagnosticsEnabled = options?.FocusDiagnostics ?? false,
                diagnosticTraceEnabled = options?.DiagnosticTrace ?? false,
                requestIntervalMs = options?.RequestIntervalMs ?? 0,
                diagnosticGateReleasedTimestamp = diagnosticGateReleasedTimestamp,
                diagnosticTraceSampling = "Independent diagnostic only. Fixed absolute request deadlines allow normal frames while waiting; all wait/request/drain observations retained. Latest per-label CPU version samples bracket events, not exact callback times or GPU display. Counter jumps disclose unobserved intermediate events. Paced frame metrics are not the original acceptance workload.",
                partialDiagnosticTrace = fatal != null ? activeTrace : null,
                focusEvents = focusEvents,
                unityVersion = Application.unityVersion,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDevice = SystemInfo.graphicsDeviceName,
                developmentPlayer = Debug.isDebugBuild,
                nativeConfiguration = options?.NativeConfiguration ?? "unspecified",
                nativeSha256 = options?.NativeHash ?? "unspecified",
                attributionEnabled = options?.Attribution ?? false,
                freshnessEnabled = options?.Freshness ?? false,
                completionTimingEnabled = options?.CompletionTiming ?? false,
                fontMappingsEnabled = options?.FontMappings ?? false,
                measurementFault = options?.MeasurementFault ?? "none",
                measurementFaultSampling = "never-completes replaces only drain currentCompletions and final snapshot completed versions with synthetic incomplete observations. Product state and phase event counters remain real. Fault reports are diagnostics, excluded from performance baselines.",
                completionDrainMaxFrames = TextPipelineCompletionGate.MaxFrames,
                completionDrainTimeoutMs = TextPipelineCompletionGate.TimeoutMs,
                seed = options?.Seed,
                alphabet = options?.Digits == true ? "digits" : "alphanumeric62",
                dynamicUpdates = options?.Static != true,
                warmupFrames = options?.Warmup,
                sampleFrames = options?.Samples,
                repeats = options?.Repeats,
                pairOrder = "ABBA; A=optimizedTextureAtlas, B=glyphMesh",
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                displayRenderingWidth = Display.main.renderingWidth,
                displayRenderingHeight = Display.main.renderingHeight,
                displaySystemWidth = Display.main.systemWidth,
                displaySystemHeight = Display.main.systemHeight,
                renderWidthScaleFactor = ScalableBufferManager.widthScaleFactor,
                renderHeightScaleFactor = ScalableBufferManager.heightScaleFactor,
                vSyncCount = QualitySettings.vSyncCount,
                targetFrameRate = Application.targetFrameRate,
                mainThreadRecorderAvailable = recorders?.Main.Valid ?? false,
                renderThreadRecorderAvailable = recorders?.Render.Valid ?? false,
                drawCallRecorderAvailable = recorders?.DrawCalls.Valid ?? false,
                gcRecorderAvailable = recorders?.GcAlloc.Valid ?? false,
                gpuSampling = "Unique asynchronous FrameTimingManager samples within the measured CPU timer interval; not paired with CPU rows. First boundary frame excluded. Unsupported or incompatible clocks produce null metrics.",
                frameSampling = "Completed previous frame read at next Update after normal Canvas rendering; frameMs is unscaled frame interval. Main/render profiler times include waits.",
                diagnosticsSampling = "Disabled in timing-only runs; attribution scope timings are inclusive and cannot be summed. Upload bytes are CPU page bytes submitted, not driver transfer measurements.",
                freshnessSampling = "--text-freshness tracks per-frame versions. --text-completion-timing enables the same existing producer event counters but observes versions only at the request boundary and during drain. Neither is the original timing-only mode. Completion is CPU Drawn/publication, not GPU display. versionsBehind is requested minus completed, including normally merged requests, not queue depth. Latest completion per label per observation supplies latency; it is not all completion latency. Completion-timing request latency has only the latest boundary observation and cannot be compared directly with per-frame freshness latency. Request and drain phases are separate; drain performs no setters and requires latest request completion plus producer/presenter synchronization within 120 frames and 5000 ms. Final output and presenter versions share one sequence, distinct from content request versions. Unique completed request counts may differ even after successful drain; throughput normalization alone is not strict equal-completion evidence.",
                memorySampling = "Measured ending memory and collection counts precede diagnostics/result allocations. committedRenderSurfaceBytes covers the render-surface ledger only. glyphOutputTextureBytes covers distinct Alpha8 pages referenced by this fixture; unreferenced retained shared glyph pages are excluded. Shared glyph atlas pages persist across ABBA cases and are prewarmed before each case.",
                prewarmSampling = "Corpus-warmed steady state: both modes replay the complete deterministic case corpus twice before ordinary warmup. Glyph mode requires a complete pass with zero glyph rasterizations and uploads, with at most three passes. Static mode replays only frame zero. Prewarm counters and wall time include fixture readiness and alphabet warmup, exclude ordinary warmup and measured frames, and reflect the retained shared atlas state rather than an isolated cold start. Prewarm frame count covers corpus replay only.",
                failed = failed,
                fatal = fatal,
                totalErrors = errorCount,
                results = results
            };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output, JsonConvert.SerializeObject(report, Formatting.Indented));
            Debug.Log("TEXT_PIPELINE_REPORT completed=" + !failed);
            Application.Quit(exitCode);
        }

        private sealed class Fixture
        {
            internal const float FontSize = 16f;
            internal const float LabelWidth = 132f;
            internal const float LabelHeight = 28f;
            internal GameObject Root;
            internal Vector2 CellSize;
            internal string RequestedFontFamilies;
            private TextBox[] paragraph;
            private TextBoxRenderTextureProducer[] paragraphProducers;
            private CanvasSlimText[] slim;
            private SlimTextRenderTextureProducer[] slimProducers;
            private TextPipelineFreshness[] freshnessBaseline;
            private long[] observedCompletionVersions;
            private long[] observedCompletions;
            private double[] completionLatencies;
            private int completionLatencyCount;
            internal int CompletionLatencyCount => completionLatencyCount;

            private TextPipelineFreshness FreshnessAt(int index) => paragraph != null
                ? paragraphProducers[index].Freshness : slimProducers[index].Freshness;

            internal void CaptureTrace(TextPipelineDiagnosticTrace trace, string phase, FrameSample sample)
            {
                var observation = trace.BeginObservation(phase, Time.frameCount, sample.frameMs,
                    sample.mainThreadMs, Application.isFocused);
                for (var index = 0; index < trace.labels; ++index)
                {
                    var output = paragraph != null ? paragraphProducers[index].OutputVersion : slimProducers[index].OutputVersion;
                    var presenter = paragraph != null ? paragraph[index].ObservedOutputVersion : slim[index].ObservedOutputVersion;
                    var hasOutput = paragraph != null ? paragraphProducers[index].HasOutput : slimProducers[index].HasOutput;
                    trace.CaptureLabel(observation, index, FreshnessAt(index), output, presenter, hasOutput);
                }
                trace.EndObservation(observation);
            }

            internal void BeginFreshness(int samples)
            {
                var count = paragraph?.Length ?? slim.Length;
                freshnessBaseline = new TextPipelineFreshness[count];
                observedCompletionVersions = new long[count];
                observedCompletions = new long[count];
                completionLatencies = new double[count * samples];
                for (var index = 0; index < count; ++index)
                {
                    if (paragraph != null)
                    {
                        paragraphProducers[index].BeginFreshnessWindow();
                    }
                    else
                    {
                        slimProducers[index].BeginFreshnessWindow();
                    }
                    var state = FreshnessAt(index);
                    freshnessBaseline[index] = state;
                    observedCompletionVersions[index] = state.CompletedVersion;
                    observedCompletions[index] = state.Completions;
                }
            }

            internal FreshnessFrame ObserveFreshness()
            {
                var sample = new FreshnessFrame();
                for (var index = 0; index < freshnessBaseline.Length; ++index)
                {
                    var state = FreshnessAt(index);
                    if (state.UnsupportedSubmission)
                    {
                        ++sample.unsupportedLabels;
                        continue;
                    }
                    var hasOutput = paragraph != null
                        ? paragraphProducers[index].HasOutput : slimProducers[index].HasOutput;
                    var presenterSynced = paragraph != null
                        ? paragraph[index].ObservedOutputVersion == paragraphProducers[index].OutputVersion
                        : slim[index].ObservedOutputVersion == slimProducers[index].OutputVersion;
                    if (hasOutput) ++sample.validOutputs;
                    if (hasOutput && presenterSynced) ++sample.presenterSynced;
                    var lag = state.RequestedVersion - state.CompletedVersion;
                    sample.versionsBehind += lag;
                    sample.maxVersionsBehind = Math.Max(sample.maxVersionsBehind, lag);
                    if (hasOutput && lag == 0) ++sample.currentCompletions;
                    if (state.Completions != observedCompletions[index])
                    {
                        if (state.CompletedVersion > freshnessBaseline[index].RequestedVersion &&
                            state.CompletedVersion != observedCompletionVersions[index])
                        {
                            if (completionLatencyCount < completionLatencies.Length)
                            {
                                completionLatencies[completionLatencyCount++] =
                                    state.CompletedFrame - state.CompletedRequestFrame;
                            }
                        }
                        observedCompletions[index] = state.Completions;
                        observedCompletionVersions[index] = state.CompletedVersion;
                    }
                    sample.completedRequestedVersions += state.WindowCompletedVersions;
                    sample.carryoverCompletions += state.WindowCarryoverCompletions;
                }
                return sample;
            }

            internal FreshnessResult EndFreshness(TextPipelineFreshness[] states = null, int latencyCount = -1)
            {
                var result = new FreshnessResult
                {
                    requestToCompletionFrames = Metric.FromSamples(completionLatencies,
                        latencyCount < 0 ? completionLatencyCount : latencyCount)
                };
                for (var index = 0; index < freshnessBaseline.Length; ++index)
                {
                    var state = states == null ? FreshnessAt(index) : states[index];
                    var before = freshnessBaseline[index];
                    if (state.UnsupportedSubmission) ++result.unsupportedLabels;
                    result.requestedChanges += state.RequestedVersion - before.RequestedVersion;
                    result.preparationAttempts += state.PreparationAttempts - before.PreparationAttempts;
                    result.acceptedSubmissions += state.AcceptedSubmissions - before.AcceptedSubmissions;
                    result.completedSubmissions += state.Completions - before.Completions;
                    result.deferredFastSubmitAttempts += state.DeferredAttempts - before.DeferredAttempts;
                    result.completedRequestedVersions += state.WindowCompletedVersions;
                    result.carryoverCompletions += state.WindowCarryoverCompletions;
                }
                return result;
            }

            internal static TextPipelineVersionSnapshot[] AllocateVersions(int count)
            {
                var states = new TextPipelineVersionSnapshot[count];
                for (var index = 0; index < count; ++index)
                {
                    states[index] = new TextPipelineVersionSnapshot { label = index };
                }
                return states;
            }

            internal void CaptureVersions(TextPipelineFreshness[] states, TextPipelineVersionSnapshot[] outputs)
            {
                for (var index = 0; index < states.Length; ++index)
                {
                    var state = FreshnessAt(index);
                    states[index] = state;
                    var output = outputs[index];
                    output.requested = state.RequestedVersion;
                    output.prepared = state.PreparedVersion;
                    output.submitted = state.SubmittedVersion;
                    output.completed = state.CompletedVersion;
                    output.output = paragraph != null ? paragraphProducers[index].OutputVersion : slimProducers[index].OutputVersion;
                    output.presenter = paragraph != null ? paragraph[index].ObservedOutputVersion : slim[index].ObservedOutputVersion;
                    output.hasOutput = paragraph != null ? paragraphProducers[index].HasOutput : slimProducers[index].HasOutput;
                    output.unsupported = state.UnsupportedSubmission;
                    var texture = paragraph != null ? paragraphProducers[index].OutputTexture : slimProducers[index].OutputTexture;
                    var region = paragraph != null ? paragraphProducers[index].OutputUvRect : slimProducers[index].OutputUvRect;
                    output.textureWidth = texture != null ? texture.width : 0;
                    output.textureHeight = texture != null ? texture.height : 0;
                    output.outputRegionWidth = output.textureWidth * Mathf.Abs(region.width);
                    output.outputRegionHeight = output.textureHeight * Mathf.Abs(region.height);
                }
            }

            internal TextPipelineCompletionPhase CompletionPhase(TextPipelineFreshness[] states,
                TextPipelineFreshness[] before, int frames, double wallMs, FrameSample[] samples,
                int sampleCount, int collections)
            {
                var result = new TextPipelineCompletionPhase
                {
                    wallMs = wallMs,
                    frames = frames,
                    gen0Collections = collections,
                    applicationFocusedAtEnd = Application.isFocused
                };
                for (var index = 0; index < states.Length; ++index)
                {
                    var state = states[index];
                    var baseline = before == null ? freshnessBaseline[index] : before[index];
                    result.requestedChanges += state.RequestedVersion - baseline.RequestedVersion;
                    result.preparationAttempts += state.PreparationAttempts - baseline.PreparationAttempts;
                    result.acceptedSubmissions += state.AcceptedSubmissions - baseline.AcceptedSubmissions;
                    result.completedSubmissions += state.Completions - baseline.Completions;
                    result.completedRequestedVersions += state.WindowCompletedVersions - baseline.WindowCompletedVersions;
                    result.carryoverCompletions += state.WindowCarryoverCompletions - baseline.WindowCarryoverCompletions;
                    result.deferredFastSubmitAttempts += state.DeferredAttempts - baseline.DeferredAttempts;
                }
                for (var index = 0; index < sampleCount; ++index)
                {
                    result.gcAllocBytes += samples[index].gcAllocBytes;
                }
                return result;
            }

            internal static Fixture Create(string kind, int count, string initial, bool fontMappings)
            {
                var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count * Screen.width / (double)Screen.height / 5d)));
                var rows = (count + columns - 1) / columns;
                var cell = new Vector2(Screen.width / (float)columns, Screen.height / (float)rows);
                if (cell.x < LabelWidth || cell.y < LabelHeight)
                {
                    throw new InvalidOperationException("Resolution cannot display every complete label. Use 1920x1080 or reduce --text-counts.");
                }
                var fixture = new Fixture { Root = new GameObject("Text Pipeline Canvas", typeof(Canvas)), CellSize = cell };
                fixture.Root.SetActive(false);
                fixture.Root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                if (kind == "paragraph")
                {
                    fixture.paragraph = new TextBox[count];
                    fixture.paragraphProducers = new TextBoxRenderTextureProducer[count];
                }
                else
                {
                    fixture.slim = new CanvasSlimText[count];
                    fixture.slimProducers = new SlimTextRenderTextureProducer[count];
                }
                for (var index = 0; index < count; ++index)
                {
                    var labelObject = new GameObject("Label " + index, typeof(RectTransform));
                    labelObject.transform.SetParent(fixture.Root.transform, false);
                    var rect = labelObject.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                    rect.pivot = new Vector2(0, 1);
                    rect.anchoredPosition = new Vector2(index % columns * cell.x,
                        -(index / columns * cell.y));
                    rect.sizeDelta = new Vector2(LabelWidth, LabelHeight);
                    if (fixture.paragraph != null)
                    {
                        var text = labelObject.AddComponent<TextBox>();
                        var producer = labelObject.GetComponent<TextBoxRenderTextureProducer>() ??
                            labelObject.AddComponent<TextBoxRenderTextureProducer>();
                        producer.textStyle.size = FontSize;
                        if (fontMappings)
                        {
                            producer.textStyle.fontFamilies = new List<string> { "body" };
                        }
                        producer.textStyle.textColor = Color.white;
                        producer.paragraphStyle.singleLine = true;
                        producer.content = initial;
                        fixture.paragraphProducers[index] = producer;
                        fixture.paragraph[index] = text;
                        text.raycastTarget = false;
                        fixture.RequestedFontFamilies = string.Join(", ", producer.textStyle.fontFamilies);
                    }
                    else
                    {
                        var text = labelObject.AddComponent<CanvasSlimText>();
                        text.Configure(initial, FontSize, SlimTextHorizontalAlign.Left, Color.white);
                        fixture.slim[index] = text;
                        fixture.slimProducers[index] = labelObject.GetComponent<SlimTextRenderTextureProducer>();
                        fixture.RequestedFontFamilies = SlimTextRenderTextureProducer.DefaultFontFamily;
                    }
                }
                fixture.Root.SetActive(true);
                return fixture;
            }

            internal void SetAll(string text)
            {
                var count = paragraph?.Length ?? slim.Length;
                for (var index = 0; index < count; ++index)
                {
                    Set(index, text);
                }
            }

            internal void SetFrame(TextPipelineBenchmarkData data, int frame)
            {
                var count = paragraph?.Length ?? slim.Length;
                for (var index = 0; index < count; ++index)
                {
                    Set(index, data.Frames[frame, index]);
                }
            }

            private void Set(int index, string text)
            {
                if (paragraph != null)
                {
                    paragraph[index].Text = text;
                }
                else
                {
                    slim[index].Text = text;
                }
            }

            internal int MeshCount()
            {
                var count = 0;
                if (paragraph != null)
                {
                    foreach (var producer in paragraphProducers)
                    {
                        if (producer.HasOutput && producer.GlyphMeshOutput != null)
                        {
                            ++count;
                        }
                    }
                }
                else
                {
                    foreach (var producer in slimProducers)
                    {
                        if (producer.HasOutput && producer.GlyphMeshOutput != null)
                        {
                            ++count;
                        }
                    }
                }
                return count;
            }

            internal int TextureCount()
            {
                var count = 0;
                if (paragraph != null)
                {
                    foreach (var producer in paragraphProducers)
                    {
                        if (producer.HasOutput && producer.OutputTexture is RenderTexture)
                        {
                            ++count;
                        }
                    }
                }
                else
                {
                    foreach (var producer in slimProducers)
                    {
                        if (producer.HasOutput && producer.OutputTexture is RenderTexture)
                        {
                            ++count;
                        }
                    }
                }
                return count;
            }

            internal bool Ready(bool glyphMesh)
            {
                var count = paragraph?.Length ?? slim.Length;
                return glyphMesh ? MeshCount() == count : TextureCount() == count;
            }

            internal void GetOutputGlyphMemory(out int pages, out long bytes)
            {
                var textures = new HashSet<Texture>();
                if (paragraph != null)
                {
                    foreach (var producer in paragraphProducers)
                    {
                        if (producer.GlyphMeshOutput != null && producer.OutputTexture is Texture2D texture)
                        {
                            textures.Add(texture);
                        }
                    }
                }
                else
                {
                    foreach (var producer in slimProducers)
                    {
                        if (producer.GlyphMeshOutput != null && producer.OutputTexture is Texture2D texture)
                        {
                            textures.Add(texture);
                        }
                    }
                }
                pages = textures.Count;
                bytes = 0;
                foreach (var texture in textures)
                {
                    bytes += (long)texture.width * texture.height;
                }
            }

            internal void Validate(bool glyphMesh)
            {
                if (!Ready(glyphMesh))
                {
                    throw new InvalidOperationException("A label fell back or has no output.");
                }
                if (glyphMesh)
                {
                    var graphics = Root.GetComponentsInChildren<Graphic>();
                    var presented = 0;
                    foreach (var graphic in graphics)
                    {
                        if (graphic.canvasRenderer.materialCount > 0 &&
                            graphic.canvasRenderer.GetMaterial(0)?.shader?.name == "Milestro/TextGlyphMesh")
                        {
                            ++presented;
                        }
                    }
                    if (presented != (paragraph?.Length ?? slim.Length))
                    {
                        throw new InvalidOperationException("The requested glyph material is not presented by every label.");
                    }
                }
            }
        }

        private sealed class Recorders : IDisposable
        {
            internal ProfilerRecorder Main = Start(ProfilerCategory.Internal, "Main Thread");
            internal ProfilerRecorder Render = Start(ProfilerCategory.Internal, "Render Thread");
            internal ProfilerRecorder DrawCalls = Start(ProfilerCategory.Render, "Draw Calls Count");
            internal ProfilerRecorder GcAlloc = Start(ProfilerCategory.Memory, "GC Allocated In Frame");

            private static ProfilerRecorder Start(ProfilerCategory category, string name) =>
                ProfilerRecorder.StartNew(category, name, 1,
                    ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame);

            internal FrameSample Read(int index)
            {
                return new FrameSample
                {
                    index = index,
                    frameMs = Time.unscaledDeltaTime * 1000d,
                    mainThreadMs = Main.LastValue / 1000000d,
                    renderThreadMs = Render.Valid ? Render.LastValue / 1000000d : (double?)null,
                    drawCalls = DrawCalls.LastValue,
                    gcAllocBytes = GcAlloc.LastValue
                };
            }

            public void Dispose()
            {
                Main.Dispose();
                Render.Dispose();
                DrawCalls.Dispose();
                GcAlloc.Dispose();
            }
        }

        private struct FrameSample
        {
            public bool? applicationFocused;
            public long? focusTimestamp;
            public int index;
            public double frameMs;
            public double mainThreadMs;
            public double? renderThreadMs;
            public long drawCalls;
            public long gcAllocBytes;
            public FreshnessFrame? freshness;
        }

        private struct FreshnessFrame
        {
            public int unsupportedLabels;
            public int validOutputs;
            public int presenterSynced;
            public int currentCompletions;
            public long versionsBehind;
            public long maxVersionsBehind;
            public long completedRequestedVersions;
            public long carryoverCompletions;
        }

        private sealed class FreshnessResult
        {
            public int unsupportedLabels;
            public long requestedChanges;
            public long preparationAttempts;
            public long acceptedSubmissions;
            public long completedSubmissions;
            public long deferredFastSubmitAttempts;
            public long completedRequestedVersions;
            public long carryoverCompletions;
            public Metric requestToCompletionFrames;
        }

        private struct GpuSample
        {
            public ulong frameStartTimestamp;
            public double gpuMs;
        }

        private sealed class Metric
        {
            public bool available;
            public int validSamples;
            public double? p50;
            public double? p95;

            internal static Metric Unavailable() => new Metric();

            private static Metric From(double[] values)
            {
                return values.Length == 0 ? Unavailable() : new Metric
                {
                    available = true,
                    validSamples = values.Length,
                    p50 = TextPipelineBenchmarkData.Percentile(values, 0.5),
                    p95 = TextPipelineBenchmarkData.Percentile(values, 0.95)
                };
            }

            internal static Metric FromSamples(double[] values, int count)
            {
                var measured = new double[count];
                Array.Copy(values, measured, count);
                return From(measured);
            }

            internal static Metric FromFrames(FrameSample[] frames, Func<FrameSample, double> select, int count = -1)
            {
                var values = new double[count < 0 ? frames.Length : count];
                for (var index = 0; index < values.Length; ++index)
                {
                    values[index] = select(frames[index]);
                }
                return From(values);
            }

            internal static Metric FromGpu(List<GpuSample> frames)
            {
                var values = new double[frames.Count];
                for (var index = 0; index < frames.Count; ++index)
                {
                    values[index] = frames[index].gpuMs;
                }
                return From(values);
            }
        }

        private sealed class CaseResult
        {
            public string caseId;
            public long requestStartTimestamp;
            public long requestEndTimestamp;
            public long drainEndTimestamp;
            public string kind;
            public int labels;
            public int repeat;
            public int order;
            public string mode;
            public string corpusChecksum;
            public int expectedMeshLabels;
            public int actualMeshLabels;
            public int actualTextureLabels;
            public bool completed;
            public int errors;
            public float cellWidth;
            public float cellHeight;
            public float labelWidth;
            public float labelHeight;
            public float fontSize;
            public string requestedFontFamilies;
            public FrameSample[] frames;
            public TextPipelineDiagnosticTrace diagnosticTrace;
            public GpuSample[] gpuFrames;
            public Metric frameMs;
            public Metric mainThreadMs;
            public Metric renderThreadMs;
            public Metric gpuMs;
            public Metric drawCalls;
            public Metric gcAllocBytes;
            public object diagnostics;
            public FreshnessResult freshness;
            public FreshnessResult requestFreshness;
            public TextPipelineCompletionPhase requestPhase;
            public TextPipelineCompletionPhase drainPhase;
            public TextPipelineVersionSnapshot[] requestEndVersions;
            public TextPipelineVersionSnapshot[] beginVersions;
            public TextPipelineVersionSnapshot[] finalVersions;
            public bool drainTimedOut;
            public string completionDrainDecision;
            public bool completionAccepted;
            public ArraySegment<FrameSample> drainFrames;
            public Metric drainFrameMs;
            public Metric drainGcAllocBytes;
            public double totalCompletionWallMs;
            public double completedVersionsPerSecond;
            public float canvasPixelWidth;
            public float canvasPixelHeight;
            public float canvasScaleFactor;
            public int screenshotWidth;
            public int screenshotHeight;
            public long drainTexturePageSubmissions;
            public ulong drainNativeSubmissions;
            public long drainGlyphRasterizations;
            public long drainGlyphUploads;
            public long warmedGlyphRasterizations;
            public int prewarmPasses;
            public int prewarmFrames;
            public double prewarmMs;
            public long prewarmGlyphRasterizations;
            public long prewarmGlyphUploads;
            public long warmedGlyphUploads;
            public long texturePageSubmissions;
            public ulong nativeSubmissions;
            public long managedMemoryBefore;
            public long managedMemoryAfter;
            public long unityMemoryBefore;
            public long unityMemoryAfter;
            public long committedRenderSurfaceBytes;
            public int glyphOutputTexturePages;
            public long glyphOutputTextureBytes;
            public int textureAtlasPages;
            public int gen0Collections;
            public bool gpuClockCompatible;
        }

        private sealed class Options
        {
            internal int[] Counts = { 9, 100, 500 };
            internal string[] Kinds = { "paragraph", "slim" };
            internal int Warmup = 120;
            internal int Samples = 240;
            internal int Repeats = 2;
            internal uint Seed = 2463534242;
            internal bool Digits;
            internal bool Attribution;
            internal bool Freshness;
            internal bool CompletionTiming;
            internal bool FontMappings;
            internal string MeasurementFault = "none";
            internal bool FocusDiagnostics;
            internal bool DiagnosticTrace;
            internal double RequestIntervalMs;
            internal string StartSignal;
            internal bool TrackCompletion => Freshness || CompletionTiming;
            internal string Screenshot;
            internal bool Static;
            internal string Output;
            internal string NativeConfiguration = "unspecified";
            internal string NativeHash = "unspecified";

            internal static Options Parse(string[] arguments)
            {
                var value = new Options();
                foreach (var argument in arguments)
                {
                    if (!argument.StartsWith("--text-", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    var parts = argument.Substring(7).Split(new[] { '=' }, 2);
                    var setting = parts.Length > 1 ? parts[1] : "true";
                    switch (parts[0])
                    {
                        case "counts": value.Counts = Array.ConvertAll(setting.Split(','), item => int.Parse(item, CultureInfo.InvariantCulture)); break;
                        case "kinds": value.Kinds = setting.Split(','); break;
                        case "warmup": value.Warmup = int.Parse(setting, CultureInfo.InvariantCulture); break;
                        case "samples": value.Samples = int.Parse(setting, CultureInfo.InvariantCulture); break;
                        case "repeats": value.Repeats = int.Parse(setting, CultureInfo.InvariantCulture); break;
                        case "seed": value.Seed = uint.Parse(setting, CultureInfo.InvariantCulture); break;
                        case "digits": value.Digits = bool.Parse(setting); break;
                        case "attribution": value.Attribution = bool.Parse(setting); break;
                        case "freshness": value.Freshness = bool.Parse(setting); break;
                        case "completion-timing": value.CompletionTiming = bool.Parse(setting); break;
                        case "font-mappings": value.FontMappings = bool.Parse(setting); break;
                        case "measurement-fault": value.MeasurementFault = setting; break;
                        case "focus-diagnostics": value.FocusDiagnostics = bool.Parse(setting); break;
                        case "diagnostic-trace": value.DiagnosticTrace = bool.Parse(setting); break;
                        case "request-interval-ms": value.RequestIntervalMs = double.Parse(setting, CultureInfo.InvariantCulture); break;
                        case "start-signal": value.StartSignal = setting; break;
                        case "screenshot": value.Screenshot = setting; break;
                        case "static": value.Static = bool.Parse(setting); break;
                        case "output": value.Output = setting; break;
                        case "native-config": value.NativeConfiguration = setting; break;
                        case "native-sha256": value.NativeHash = setting; break;
                        default: throw new ArgumentException("Unknown text benchmark setting: " + parts[0]);
                    }
                }
                if (value.Warmup < 16 || value.Samples < 16 || value.Repeats < 1 || value.Seed == 0 ||
                    value.Counts.Length == 0 || Array.Exists(value.Counts, count => count < 1 || count > 500) ||
                    value.Kinds.Length == 0 || Array.Exists(value.Kinds, kind => kind != "paragraph" && kind != "slim"))
                {
                    throw new ArgumentException("Invalid benchmark settings: warmup/samples >=16, repeats >=1, counts 1..500, kinds paragraph/slim, nonzero seed.");
                }
                if (value.MeasurementFault != "none" &&
                    (value.MeasurementFault != "never-completes" || !value.Freshness || value.CompletionTiming || value.Attribution))
                {
                    throw new ArgumentException("Measurement fault requires a separate freshness-only diagnostic run and supports only never-completes.");
                }
                TextPipelineDiagnosticTrace.Deadline(0, 0, value.RequestIntervalMs, Stopwatch.Frequency);
                if ((!string.IsNullOrEmpty(value.StartSignal) && !value.DiagnosticTrace) ||
                    (value.RequestIntervalMs != 0 && !value.DiagnosticTrace) ||
                    (value.DiagnosticTrace && (!value.Freshness || value.CompletionTiming || value.Attribution || value.Static ||
                        value.Counts.Length != 1 || value.Counts[0] != 9 || value.Kinds.Length != 1 || value.Kinds[0] != "paragraph" ||
                        value.MeasurementFault != "none")))
                {
                    throw new ArgumentException("Diagnostic trace requires a separate dynamic 9 Paragraph freshness run; pacing requires diagnostic trace.");
                }
                return value;
            }
        }
    }
}

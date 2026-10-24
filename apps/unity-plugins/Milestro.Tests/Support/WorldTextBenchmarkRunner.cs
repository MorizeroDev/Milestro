using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Configuration;
using Milestro.Skia;
using Unity.Profiling;
using UnityEngine;
using Path = System.IO.Path;

namespace Milestro.Tests
{
    public sealed class WorldTextBenchmarkRunner : MonoBehaviour
    {
        private const int SampleFrames = 120;
        private readonly List<Result> results = new List<Result>();
        private readonly FrameTiming[] timings = new FrameTiming[1];
        private int errorCount;
        private bool fatal;

        private void Awake()
        {
            Application.logMessageReceived += OnLog;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            {
                ++errorCount;
            }
        }

        private void LateUpdate()
        {
            Debug.developerConsoleVisible = false;
        }

        private IEnumerator Start()
        {
            using var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            using var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            using var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            using var mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            yield return RunCases(drawCalls, batches, setPass, mainThread);
            WriteReport(drawCalls, mainThread);
        }

        private static Case CreateCase(int count, WorldTextMode mode, Material sharedMaterial)
        {
            var benchmark = new Case
            {
                Root = new GameObject("World Text Benchmark Root"),
                Labels = new WorldSpaceSlimText[count],
                Producers = new SlimTextRenderTextureProducer[count],
                Buffers = new byte[count][],
                Count = count,
                Mode = mode
            };
            if (mode == WorldTextMode.ManagedAtlasBatched)
            {
                benchmark.Batch = benchmark.Root.AddComponent<WorldSpaceTextBatch>();
            }
            for (var index = 0; index < count; ++index)
            {
                var label = new GameObject("World Label " + index, typeof(RectTransform));
                label.transform.SetParent(benchmark.Root.transform, false);
                label.transform.localPosition = new Vector3(index % 32 * 0.4f, -(index / 32) * 0.22f, 0f);
                var worldText = label.AddComponent<WorldSpaceSlimText>();
                worldText.material = sharedMaterial;
                worldText.textureSizePixels = new Vector2Int(38, 20);
                worldText.pixelsPerUnit = 100f;
                worldText.fontSize = 12;
                if (mode != WorldTextMode.NoAlloc)
                {
                    worldText.text = "T" + index;
                }
                benchmark.Labels[index] = worldText;
                benchmark.Producers[index] = label.GetComponent<SlimTextRenderTextureProducer>();
                benchmark.Buffers[index] = new byte[16];
                label.SetActive(false);
            }
            return benchmark;
        }

        private IEnumerator ActivateAndWait(Case benchmark)
        {
            for (var start = 0; start < benchmark.Count; start += 64)
            {
                var end = Math.Min(benchmark.Count, start + 64);
                for (var index = start; index < end; ++index)
                {
                    var label = benchmark.Labels[index];
                    label.gameObject.SetActive(true);
                    if (benchmark.Mode == WorldTextMode.NoAlloc)
                    {
                        var buffer = benchmark.Buffers[index];
                        label.PrepareTextUtf8NoAlloc(buffer.Length);
                        label.SetTextUtf8NoAlloc(buffer, 0, WriteText(buffer, index));
                    }
                }
                yield return null;
            }
            for (var frame = 0; frame < 600 && !AllReady(benchmark.Producers); ++frame)
            {
                yield return null;
            }
            if (!AllReady(benchmark.Producers))
            {
                Debug.LogError("World text benchmark failed to produce all labels for " + benchmark.Mode);
                fatal = true;
            }
        }

        private IEnumerator RunCases(ProfilerRecorder drawCalls, ProfilerRecorder batches,
            ProfilerRecorder setPass, ProfilerRecorder mainThread)
        {
            var cameraObject = new GameObject("World Text Benchmark Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(6.2f, -3.41f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 4f;
            camera.backgroundColor = Color.black;
            camera.clearFlags = CameraClearFlags.SolidColor;
            var shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError("World text benchmark could not find a transparent shader.");
                yield break;
            }
            var sharedMaterial = new Material(shader);
            var smoke = Array.IndexOf(Environment.GetCommandLineArgs(), "-world-text-smoke") >= 0;
            SlimTextPipelineProfiler.Enabled =
                Array.IndexOf(Environment.GetCommandLineArgs(), "-world-text-profile") >= 0;
            var batchOnly = Array.IndexOf(Environment.GetCommandLineArgs(), "-world-text-batch-only") >= 0;
            var updateOnly = Array.IndexOf(Environment.GetCommandLineArgs(), "-world-text-update-only") >= 0;
            var counts = smoke ? new[] { 100 } :
                batchOnly || updateOnly ? new[] { 1000 } : new[] { 100, 500, 1000 };
            var updatePercents = smoke ? new[] { 0 } : updateOnly ? new[] { 100 } : new[] { 0, 10, 100 };
            foreach (var count in counts)
            {
                var modes = updateOnly
                    ? new[] { WorldTextMode.ManagedAtlas }
                    : batchOnly
                    ? new[] { WorldTextMode.ManagedAtlas, WorldTextMode.ManagedAtlasBatched }
                    : new[]
                    {
                        WorldTextMode.ManagedStandalone, WorldTextMode.ManagedAtlas,
                        WorldTextMode.ManagedAtlasBatched, WorldTextMode.NoAlloc
                    };
                foreach (var mode in modes)
                {
                    MilestroConfiguration.Configuration.RenderSurface = new RenderSurfaceConfiguration
                    {
                        EnableAtlas = mode == WorldTextMode.ManagedAtlas ||
                                      mode == WorldTextMode.ManagedAtlasBatched,
                        AtlasPageSize = 1024
                    };
                    var benchmark = CreateCase(count, mode, sharedMaterial);
                    yield return ActivateAndWait(benchmark);
                    if (fatal)
                    {
                        yield break;
                    }
                    foreach (var updatePercent in updatePercents)
                    {
                        yield return Measure(benchmark, updatePercent, drawCalls, batches, setPass, mainThread);
                    }
                    Destroy(benchmark.Root);
                    for (var frame = 0; frame < 10; ++frame)
                    {
                        yield return null;
                    }
                }
            }
            Destroy(sharedMaterial);
            SlimTextPipelineProfiler.Enabled = false;
        }

        private IEnumerator Measure(Case benchmark, int updatePercent, ProfilerRecorder drawCalls,
            ProfilerRecorder batches, ProfilerRecorder setPass, ProfilerRecorder mainThread)
        {
            var producerUpdate = StartScriptRecorder("Milestro.SlimText.ProducerUpdate");
            var rebuildFastManaged = StartScriptRecorder("Milestro.SlimText.RebuildFastManaged");
            var ensureFont = StartScriptRecorder("Milestro.SlimText.EnsureFont");
            var ensureSnapshot = StartScriptRecorder("Milestro.SlimText.EnsureSnapshot");
            var encodeUpdateText = StartScriptRecorder("Milestro.SlimText.EncodeUpdateText");
            var measureText = StartScriptRecorder("Milestro.SlimText.MeasureText");
            var submitFast = StartScriptRecorder("Milestro.SlimText.SubmitFast");
            var result = new Result
            {
                labels = benchmark.Count,
                mode = benchmark.Mode.ToString(),
                updatePercent = updatePercent
            };
            var beforeErrors = errorCount;
            var updates = benchmark.Count * updatePercent / 100;
            var profiling = SlimTextPipelineProfiler.Enabled;
            var updateLoopMs = 0d;
            var gcBytesStart = 0L;
            var gen0Start = 0;
            ulong beforeSubmissions = 0;
            long beforeAtlasSubmissions = 0;
            try
            {
                for (var frame = 0; frame < SampleFrames + 30; ++frame)
                {
                    ResetAndStart(producerUpdate);
                    ResetAndStart(rebuildFastManaged);
                    ResetAndStart(ensureFont);
                    ResetAndStart(ensureSnapshot);
                    ResetAndStart(encodeUpdateText);
                    ResetAndStart(measureText);
                    ResetAndStart(submitFast);
                    var updateStart = profiling ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                    try
                    {
                        for (var index = 0; index < updates; ++index)
                        {
                            var label = benchmark.Labels[index];
                            if (benchmark.Mode == WorldTextMode.NoAlloc)
                            {
                                var buffer = benchmark.Buffers[index];
                                label.SetTextUtf8NoAlloc(buffer, 0, WriteText(buffer, frame + index));
                            }
                            else
                            {
                                label.text = "T" + (frame + index);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        result.failure = exception.GetType().Name + ": " + exception.Message;
                        Debug.LogWarning("World text benchmark aborted " + benchmark.Mode + " at " +
                                         benchmark.Count + " labels, " + updatePercent + "% updates: " + result.failure);
                        break;
                    }

                    if (profiling && frame >= 30)
                    {
                        updateLoopMs += (System.Diagnostics.Stopwatch.GetTimestamp() - updateStart) * 1000.0 /
                                        System.Diagnostics.Stopwatch.Frequency;
                    }

                    if (frame == 30)
                    {
                        beforeSubmissions = UnitySkiaRenderTextureSurface.ReadNativeDiagnostics().AcceptedSubmissionCount;
                        beforeAtlasSubmissions = RenderTextureAtlas.SubmissionCount;
                        SlimTextPipelineProfiler.Reset();
                        gcBytesStart = GC.GetAllocatedBytesForCurrentThread();
                        gen0Start = GC.CollectionCount(0);
                    }
                    FrameTimingManager.CaptureFrameTimings();
                    yield return null;
                    if (frame < 30)
                    {
                        continue;
                    }
                    result.drawCalls += drawCalls.LastValue;
                    result.batches += batches.LastValue;
                    result.setPass += setPass.LastValue;
                    result.mainThreadMs += mainThread.LastValue / 1000000d;
                    result.profilerProducerUpdateMs += RecorderMilliseconds(producerUpdate);
                    result.profilerRebuildFastManagedMs += RecorderMilliseconds(rebuildFastManaged);
                    result.profilerEnsureFontMs += RecorderMilliseconds(ensureFont);
                    result.profilerEnsureSnapshotMs += RecorderMilliseconds(ensureSnapshot);
                    result.profilerEncodeUpdateTextMs += RecorderMilliseconds(encodeUpdateText);
                    result.profilerMeasureTextMs += RecorderMilliseconds(measureText);
                    result.profilerSubmitFastMs += RecorderMilliseconds(submitFast);
                    result.frameMs += Time.unscaledDeltaTime * 1000d;
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
                    {
                        result.gpuMs += timings[0].gpuFrameTime;
                        ++result.gpuSamples;
                    }
                    ++result.sampledFrames;
                }
            }
            finally
            {
                producerUpdate.Dispose();
                rebuildFastManaged.Dispose();
                ensureFont.Dispose();
                ensureSnapshot.Dispose();
                encodeUpdateText.Dispose();
                measureText.Dispose();
                submitFast.Dispose();
            }
            var sampledFrames = Math.Max(1, result.sampledFrames);
            result.drawCalls /= sampledFrames;
            result.batches /= sampledFrames;
            result.setPass /= sampledFrames;
            result.mainThreadMs /= sampledFrames;
            result.profilerProducerUpdateMs /= sampledFrames;
            result.profilerRebuildFastManagedMs /= sampledFrames;
            result.profilerEnsureFontMs /= sampledFrames;
            result.profilerEnsureSnapshotMs /= sampledFrames;
            result.profilerEncodeUpdateTextMs /= sampledFrames;
            result.profilerMeasureTextMs /= sampledFrames;
            result.profilerSubmitFastMs /= sampledFrames;
            result.frameMs /= sampledFrames;
            result.fps = result.frameMs > 0 ? 1000d / result.frameMs : 0d;
            result.gpuMs /= Math.Max(1, result.gpuSamples);
            if (result.sampledFrames > 0)
            {
                result.nativeSubmissionsPerFrame =
                    (UnitySkiaRenderTextureSurface.ReadNativeDiagnostics().AcceptedSubmissionCount - beforeSubmissions) /
                    (double)result.sampledFrames;
                result.atlasSubmissionsPerFrame =
                    (RenderTextureAtlas.SubmissionCount - beforeAtlasSubmissions) / (double)result.sampledFrames;
            }
            if (profiling)
            {
                var frames = (double)sampledFrames;
                result.profiled = true;
                result.updateLoopMs = updateLoopMs / frames;
                result.gcAllocBytesPerFrame =
                    (GC.GetAllocatedBytesForCurrentThread() - gcBytesStart) / frames;
                result.gen0CollectionsPerFrame = (GC.CollectionCount(0) - gen0Start) / frames;
                result.rebuildCallsPerFrame = SlimTextPipelineProfiler.RebuildCalls / frames;
                result.rebuildSkippedPerFrame = SlimTextPipelineProfiler.RebuildSkippedNoDraw / frames;
                result.ensureSurfaceMs = SlimTextPipelineProfiler.EnsureSurfaceMs / frames;
                result.ensureFontMs = SlimTextPipelineProfiler.EnsureFontMs / frames;
                result.ensureSnapshotMs = SlimTextPipelineProfiler.EnsureSnapshotMs / frames;
                result.encodeUpdateTextMs = SlimTextPipelineProfiler.EncodeUpdateTextMs / frames;
                result.buildCommandsMs = SlimTextPipelineProfiler.BuildCommandsMs / frames;
                result.measureTextMs = SlimTextPipelineProfiler.MeasureTextMs / frames;
                result.trySubmitMs = SlimTextPipelineProfiler.TrySubmitMs / frames;
                result.submitCallsPerFrame = SlimTextPipelineProfiler.SubmitCalls / frames;
                result.submitPrepareMs = SlimTextPipelineProfiler.SubmitPrepareMs / frames;
                result.submitMarshalMs = SlimTextPipelineProfiler.SubmitMarshalMs / frames;
                result.submitEnqueueMs = SlimTextPipelineProfiler.SubmitEnqueueMs / frames;
                result.producerRebuildCallsPerFrame = SlimTextPipelineProfiler.ProducerRebuildCalls / frames;
                result.producerRebuildMs = SlimTextPipelineProfiler.ProducerRebuildMs / frames;
                result.presenterUpdateCallsPerFrame = SlimTextPipelineProfiler.PresenterUpdateCalls / frames;
                result.presenterUpdateMs = SlimTextPipelineProfiler.PresenterUpdateMs / frames;
                result.worldTextBatchUpdateCallsPerFrame =
                    SlimTextPipelineProfiler.WorldTextBatchUpdateCalls / frames;
                result.worldTextBatchUpdateMs = SlimTextPipelineProfiler.WorldTextBatchUpdateMs / frames;
                result.worldTextBatchMeshCallsPerFrame =
                    SlimTextPipelineProfiler.WorldTextBatchMeshCalls / frames;
                result.worldTextBatchMeshMs = SlimTextPipelineProfiler.WorldTextBatchMeshMs / frames;
                result.worldTextBatchUvCallsPerFrame =
                    SlimTextPipelineProfiler.WorldTextBatchUvCalls / frames;
                result.worldTextBatchUvMs = SlimTextPipelineProfiler.WorldTextBatchUvMs / frames;
                result.worldTextBatchObserveMs = SlimTextPipelineProfiler.WorldTextBatchObserveMs / frames;
                result.worldTextBatchOriginalStateMs =
                    SlimTextPipelineProfiler.WorldTextBatchOriginalStateMs / frames;
                result.completionCallsPerFrame = SlimTextPipelineProfiler.CompletionCalls / frames;
                result.completionMs = SlimTextPipelineProfiler.CompletionMs / frames;
                result.fastPathCallsPerFrame = SlimTextPipelineProfiler.FastPathCalls / frames;
            }

            result.completed = result.sampledFrames == SampleFrames && string.IsNullOrEmpty(result.failure);
            result.committedTextureBytes = UnitySkiaRenderTextureSurface.BudgetLedger.Snapshot().CommittedBytes;
            result.atlasPages = RenderTextureAtlas.PageCount;
            var textures = new HashSet<Texture>();
            foreach (var producer in benchmark.Producers)
            {
                textures.Add(producer.OutputTexture);
            }
            result.outputTextures = textures.Count;
            if (benchmark.Batch != null)
            {
                result.batchedRenderers = benchmark.Batch.BatchedRendererCount;
                result.batchedLabels = benchmark.Batch.BatchedTextCount;
            }
            result.errors = errorCount - beforeErrors;
            results.Add(result);
            Debug.Log("WORLD_TEXT_BENCHMARK " + JsonUtility.ToJson(result));
        }

        private void WriteReport(ProfilerRecorder drawCalls, ProfilerRecorder mainThread)
        {
            var report = new Report
            {
                unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                drawCallRecorderValid = drawCalls.Valid,
                mainThreadRecorderValid = mainThread.Valid,
                fatal = fatal,
                totalErrors = errorCount,
                results = results.ToArray()
            };
            report.failed = fatal || !drawCalls.Valid || !mainThread.Valid || results.Count == 0 ||
                            errorCount > 0 || results.Exists(result => !result.completed || result.drawCalls <= 0);
            var output = Environment.GetEnvironmentVariable("MILESTRO_WORLD_TEXT_REPORT");
            if (string.IsNullOrEmpty(output))
            {
                output = Path.Combine(Application.persistentDataPath, "world-text-benchmark.json");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("WORLD_TEXT_BENCHMARK_REPORT " + output);
            Application.Quit(report.failed ? 1 : 0);
        }

        private static bool AllReady(SlimTextRenderTextureProducer[] producers)
        {
            foreach (var producer in producers)
            {
                if (!producer.HasOutput)
                {
                    return false;
                }
            }
            return true;
        }

        private static ProfilerRecorder StartScriptRecorder(string markerName)
        {
            return ProfilerRecorder.StartNew(ProfilerCategory.Scripts,
                markerName,
                1,
                ProfilerRecorderOptions.SumAllSamplesInFrame);
        }

        private static double RecorderMilliseconds(ProfilerRecorder recorder)
        {
            return recorder.Valid ? recorder.CurrentValue / 1000000d : 0d;
        }

        private static void ResetAndStart(ProfilerRecorder recorder)
        {
            recorder.Reset();
            recorder.Start();
        }

        private static int WriteText(byte[] buffer, int value)
        {
            buffer[0] = (byte)'T';
            var length = 1;
            if (value == 0)
            {
                buffer[length++] = (byte)'0';
                return length;
            }
            var firstDigit = length;
            while (value > 0 && length < buffer.Length)
            {
                buffer[length++] = (byte)('0' + value % 10);
                value /= 10;
            }
            for (int left = firstDigit, right = length - 1; left < right; ++left, --right)
            {
                (buffer[left], buffer[right]) = (buffer[right], buffer[left]);
            }
            return length;
        }

        private enum WorldTextMode
        {
            ManagedStandalone,
            ManagedAtlas,
            ManagedAtlasBatched,
            NoAlloc
        }

        private sealed class Case
        {
            public GameObject Root;
            public WorldSpaceSlimText[] Labels;
            public SlimTextRenderTextureProducer[] Producers;
            public byte[][] Buffers;
            public WorldSpaceTextBatch Batch;
            public int Count;
            public WorldTextMode Mode;
        }

        [Serializable]
        private sealed class Report
        {
            public string unityVersion;
            public string graphicsDevice;
            public string graphicsApi;
            public bool drawCallRecorderValid;
            public bool mainThreadRecorderValid;
            public bool fatal;
            public bool failed;
            public int totalErrors;
            public Result[] results;
        }

        [Serializable]
        private sealed class Result
        {
            public int labels;
            public string mode;
            public int updatePercent;
            public int outputTextures;
            public int atlasPages;
            public int batchedRenderers;
            public int batchedLabels;
            public int errors;
            public bool completed;
            public int sampledFrames;
            public string failure;
            public double drawCalls;
            public double batches;
            public double setPass;
            public double mainThreadMs;
            public double frameMs;
            public double fps;
            public double gpuMs;
            public int gpuSamples;
            public double nativeSubmissionsPerFrame;
            public double atlasSubmissionsPerFrame;
            public long committedTextureBytes;
            public bool profiled;
            public double updateLoopMs;
            public double gcAllocBytesPerFrame;
            public double rebuildCallsPerFrame;
            public double rebuildSkippedPerFrame;
            public double ensureSurfaceMs;
            public double ensureFontMs;
            public double ensureSnapshotMs;
            public double encodeUpdateTextMs;
            public double buildCommandsMs;
            public double measureTextMs;
            public double trySubmitMs;
            public double submitCallsPerFrame;
            public double submitPrepareMs;
            public double submitMarshalMs;
            public double submitEnqueueMs;
            public double gen0CollectionsPerFrame;
            public double producerRebuildCallsPerFrame;
            public double producerRebuildMs;
            public double presenterUpdateCallsPerFrame;
            public double presenterUpdateMs;
            public double worldTextBatchUpdateCallsPerFrame;
            public double worldTextBatchUpdateMs;
            public double worldTextBatchMeshCallsPerFrame;
            public double worldTextBatchMeshMs;
            public double worldTextBatchUvCallsPerFrame;
            public double worldTextBatchUvMs;
            public double worldTextBatchObserveMs;
            public double worldTextBatchOriginalStateMs;
            public double completionCallsPerFrame;
            public double completionMs;
            public double fastPathCallsPerFrame;
            public double profilerProducerUpdateMs;
            public double profilerRebuildFastManagedMs;
            public double profilerEnsureFontMs;
            public double profilerEnsureSnapshotMs;
            public double profilerEncodeUpdateTextMs;
            public double profilerMeasureTextMs;
            public double profilerSubmitFastMs;
        }
    }
}

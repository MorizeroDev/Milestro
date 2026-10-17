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
using Canvas = UnityEngine.Canvas;
using Path = System.IO.Path;

namespace Milestro.Tests
{
    public sealed class RenderAtlasBenchmarkRunner : MonoBehaviour
    {
        private const int SampleFrames = 120;
        private readonly List<Result> results = new List<Result>();
        private readonly FrameTiming[] timings = new FrameTiming[1];
        private int errorCount;

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
            var cameraObject = new GameObject("Benchmark Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.backgroundColor = Color.black;
            camera.clearFlags = CameraClearFlags.SolidColor;
            using var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            using var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            using var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            using var mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            using var renderThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread");
            var smoke = Array.IndexOf(Environment.GetCommandLineArgs(), "-atlas-smoke") >= 0;
            foreach (var count in smoke ? new[] { 100 } : new[] { 100, 500, 1000 })
            {
                foreach (var atlas in new[] { false, true })
                {
                    MilestroConfiguration.Configuration.RenderSurface = new RenderSurfaceConfiguration
                    {
                        EnableAtlas = atlas,
                        AtlasPageSize = 1024
                    };
                    var root = new GameObject("Benchmark Canvas", typeof(Canvas));
                    root.SetActive(false);
                    var canvas = root.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    var labels = new TextBoxRenderTextureProducer[count];
                    for (var index = 0; index < count; ++index)
                    {
                        var label = new GameObject("Label " + index, typeof(RectTransform));
                        label.transform.SetParent(root.transform, false);
                        var rect = label.GetComponent<RectTransform>();
                        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                        rect.pivot = new Vector2(0, 1);
                        rect.anchoredPosition = new Vector2(index % 32 * 40, -(index / 32) * 22);
                        rect.sizeDelta = new Vector2(38, 20);
                        var producer = label.AddComponent<TextBoxRenderTextureProducer>();
                        producer.size = 12;
                        producer.singleLine = true;
                        producer.content = "T" + index;
                        labels[index] = producer;
                        label.AddComponent<TextBox>();
                    }
                    root.SetActive(true);
                    for (var frame = 0; frame < 600 && !AllReady(labels); ++frame)
                    {
                        yield return null;
                    }
                    if (!AllReady(labels))
                    {
                        Debug.LogError("Atlas benchmark failed to produce all labels.");
                        Application.Quit(1);
                        yield break;
                    }
                    foreach (var updatePercent in smoke ? new[] { 0 } : new[] { 0, 10, 100 })
                    {
                        var result = new Result { labels = count, atlas = atlas, updatePercent = updatePercent };
                        var beforeErrors = errorCount;
                        var updates = count * updatePercent / 100;
                        ulong beforeSubmissions = 0;
                        long beforeAtlasSubmissions = 0;
                        for (var frame = 0; frame < SampleFrames + 30; ++frame)
                        {
                            for (var index = 0; index < updates; ++index)
                            {
                                labels[index].content = "T" + (frame + index);
                            }
                            if (frame == 30)
                            {
                                beforeSubmissions = UnitySkiaRenderTextureSurface.ReadNativeDiagnostics().AcceptedSubmissionCount;
                                beforeAtlasSubmissions = RenderTextureAtlas.SubmissionCount;
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
                            result.renderThreadMs += renderThread.LastValue / 1000000d;
                            result.frameMs += Time.unscaledDeltaTime * 1000d;
                            if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
                            {
                                result.gpuMs += timings[0].gpuFrameTime;
                                ++result.gpuSamples;
                            }
                        }
                        result.drawCalls /= SampleFrames;
                        result.batches /= SampleFrames;
                        result.setPass /= SampleFrames;
                        result.mainThreadMs /= SampleFrames;
                        result.renderThreadMs /= SampleFrames;
                        result.frameMs /= SampleFrames;
                        result.gpuMs /= Math.Max(1, result.gpuSamples);
                        result.nativeSubmissionsPerFrame =
                            (UnitySkiaRenderTextureSurface.ReadNativeDiagnostics().AcceptedSubmissionCount - beforeSubmissions) / (double)SampleFrames;
                        result.atlasSubmissionsPerFrame = (RenderTextureAtlas.SubmissionCount - beforeAtlasSubmissions) / (double)SampleFrames;
                        result.committedTextureBytes = UnitySkiaRenderTextureSurface.BudgetLedger.Snapshot().CommittedBytes;
                        result.atlasPages = RenderTextureAtlas.PageCount;
                        var textures = new HashSet<Texture>();
                        foreach (var label in labels)
                        {
                            textures.Add(label.OutputTexture);
                        }
                        result.outputTextures = textures.Count;
                        result.errors = errorCount - beforeErrors;
                        results.Add(result);
                        Debug.Log("ATLAS_BENCHMARK " + JsonUtility.ToJson(result));
                    }
                    Destroy(root);
                    for (var frame = 0; frame < 10; ++frame)
                    {
                        yield return null;
                    }
                }
            }
            var report = new Report
            {
                unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                drawCallRecorderValid = drawCalls.Valid,
                mainThreadRecorderValid = mainThread.Valid,
                renderThreadRecorderValid = renderThread.Valid,
                failed = !drawCalls.Valid || results.Exists(result => result.drawCalls <= 0) ||
                         results.Exists(result => result.atlas && result.errors > 0),
                totalErrors = errorCount,
                results = results.ToArray()
            };
            var output = Environment.GetEnvironmentVariable("MILESTRO_ATLAS_REPORT");
            if (string.IsNullOrEmpty(output))
            {
                output = Path.Combine(Application.persistentDataPath, "atlas-benchmark.json");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Application.Quit(report.failed ? 1 : 0);
        }

        private static bool AllReady(TextBoxRenderTextureProducer[] labels)
        {
            foreach (var label in labels)
            {
                if (!label.HasOutput)
                {
                    return false;
                }
            }
            return true;
        }

        [Serializable]
        private sealed class Report
        {
            public string unityVersion;
            public string graphicsDevice;
            public string graphicsApi;
            public bool drawCallRecorderValid;
            public bool mainThreadRecorderValid;
            public bool renderThreadRecorderValid;
            public bool failed;
            public int totalErrors;
            public Result[] results;
        }

        [Serializable]
        private sealed class Result
        {
            public int labels;
            public bool atlas;
            public int updatePercent;
            public int outputTextures;
            public int atlasPages;
            public int errors;
            public double drawCalls;
            public double batches;
            public double setPass;
            public double mainThreadMs;
            public double renderThreadMs;
            public double frameMs;
            public double gpuMs;
            public int gpuSamples;
            public double nativeSubmissionsPerFrame;
            public double atlasSubmissionsPerFrame;
            public long committedTextureBytes;
        }
    }
}

using System;
using System.Collections;
using System.IO;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Model;
using UnityEngine;
using UnityEngine.UI;
using RenderTextureAtlas = Milestro.Skia.RenderTextureAtlas;
using UnitySkiaRenderTextureSurface = Milestro.Skia.UnitySkiaRenderTextureSurface;

namespace Milestro.Tests.TextBoxVertical
{
    public sealed class TextBoxVerticalOrientationPlayerProbe : MonoBehaviour
    {
        private const string ProbeArgument = "-milestroOrientationProbe";
        private const string ResultArgument = "-milestroOrientationResult";
        private const string UprightContent =
            "<p style=\"text-orientation: upright\"><a href=\"orientation-upright\">ABC</a>漢</p>";
        private const string SidewaysContent =
            "<p style=\"text-orientation: sideways\"><a href=\"orientation-sideways\">ABC</a>漢</p>";
        private const string SidewaysTcyContent =
            "<p style=\"text-orientation: sideways\"><a href=\"orientation-sideways-tcy\">" +
            "<span style=\"text-combine-upright: all\">ABC</span></a>漢</p>";
        private const string MixedTcyContent =
            "<p><a href=\"orientation-mixed-tcy\">" +
            "<span style=\"text-combine-upright: all\">ABC</span></a>漢</p>";

        private string resultPath = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument(ProbeArgument)) return;
            var host = new GameObject("Milestro orientation player probe");
            DontDestroyOnLoad(host);
            host.AddComponent<TextBoxVerticalOrientationPlayerProbe>();
        }

        private void Awake()
        {
            resultPath = ArgumentValue(ResultArgument);
        }

        private IEnumerator Start()
        {
            var result = new ProbeResult();
            GameObject root = null;
            Case upright = null;
            Case sideways = null;
            Case sidewaysTcy = null;
            Case mixedTcy = null;
            try
            {
                root = new GameObject("Orientation probe canvas", typeof(RectTransform), typeof(Canvas));
                root.SetActive(false);
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var camera = root.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
                camera.cullingMask = 0;
                upright = CreateCase(root, "Upright", UprightContent, new Vector2(0f, 0f));
                sideways = CreateCase(root, "Sideways", SidewaysContent, new Vector2(220f, 0f));
                sidewaysTcy = CreateCase(root, "Sideways TCY", SidewaysTcyContent,
                    new Vector2(440f, 0f));
                mixedTcy = CreateCase(root, "Mixed TCY", MixedTcyContent,
                    new Vector2(660f, 0f));
                root.SetActive(true);
            }
            catch (Exception error)
            {
                result.Exception = error.ToString();
            }

            if (result.Exception.Length == 0)
            {
                for (var frame = 0; frame < 240; ++frame)
                {
                    try
                    {
                        RenderTextureAtlas.Flush();
                        UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                        upright?.producer.RebuildOutput(false);
                        sideways?.producer.RebuildOutput(false);
                        sidewaysTcy?.producer.RebuildOutput(false);
                        mixedTcy?.producer.RebuildOutput(false);
                        if (upright != null && sideways != null && sidewaysTcy != null &&
                            mixedTcy != null && upright.Ready() && sideways.Ready() &&
                            sidewaysTcy.Ready() && mixedTcy.Ready())
                        {
                            break;
                        }
                    }
                    catch (Exception error)
                    {
                        result.Exception = error.ToString();
                        break;
                    }

                    yield return null;
                }
            }

            if (result.Exception.Length == 0)
            {
                try
                {
                    result.UprightPassed = Measure(upright, "orientation-upright",
                        out result.UprightInlineExtent, out result.UprightBlockExtent,
                        out result.UprightLinkHit, out result.UprightPixelBounds);
                    result.SidewaysPassed = Measure(sideways, "orientation-sideways",
                        out result.SidewaysInlineExtent, out result.SidewaysBlockExtent,
                        out result.SidewaysLinkHit, out result.SidewaysPixelBounds);
                    result.SidewaysTcyPassed = Measure(sidewaysTcy, "orientation-sideways-tcy",
                        out result.SidewaysTcyInlineExtent, out result.SidewaysTcyBlockExtent,
                        out result.SidewaysTcyLinkHit, out result.SidewaysTcyPixelBounds);
                    result.MixedTcyPassed = Measure(mixedTcy, "orientation-mixed-tcy",
                        out result.MixedTcyInlineExtent, out result.MixedTcyBlockExtent,
                        out result.MixedTcyLinkHit, out result.MixedTcyPixelBounds);

                    result.UprightPassed &= result.UprightInlineExtent >
                                           result.SidewaysInlineExtent + 8f;
                    result.UprightPassed &= result.UprightPixelBounds.height >
                                           result.SidewaysPixelBounds.height + 5;
                    result.SidewaysPassed &= result.SidewaysInlineExtent <
                                            result.UprightInlineExtent - 8f;
                    result.SidewaysTcyPassed &=
                        Mathf.Abs(result.SidewaysTcyInlineExtent -
                                  result.SidewaysInlineExtent) <= 2f;
                    result.SidewaysTcyPassed &= Mathf.Abs(
                        result.SidewaysTcyPixelBounds.height -
                        result.SidewaysPixelBounds.height) <= 2;
                    result.MixedTcyPassed &= result.MixedTcyInlineExtent <
                                             result.SidewaysTcyInlineExtent - 8f;
                    result.MixedTcyPassed &= result.MixedTcyPixelBounds.height <
                                             result.SidewaysTcyPixelBounds.height - 5;
                    result.Passed = result.UprightPassed && result.SidewaysPassed &&
                                    result.SidewaysTcyPassed && result.MixedTcyPassed;
                    result.UprightScreenshot = SaveOutput(upright, "upright");
                    result.SidewaysScreenshot = SaveOutput(sideways, "sideways");
                    result.SidewaysTcyScreenshot = SaveOutput(sidewaysTcy, "sideways-tcy");
                    result.MixedTcyScreenshot = SaveOutput(mixedTcy, "mixed-tcy");
                    result.UprightText = ContentText(upright);
                    result.SidewaysText = ContentText(sideways);
                    result.SidewaysTcyText = ContentText(sidewaysTcy);
                    result.MixedTcyText = ContentText(mixedTcy);
                    result.LastLayoutError = upright.producer.lastLayoutError?.GetType().Name ??
                                             sideways.producer.lastLayoutError?.GetType().Name ??
                                             sidewaysTcy.producer.lastLayoutError?.GetType().Name ??
                                             mixedTcy.producer.lastLayoutError?.GetType().Name ?? "";
                }
                catch (Exception error)
                {
                    result.Exception = error.ToString();
                    result.Passed = false;
                }
            }

            if (resultPath.Length != 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(resultPath) ?? ".");
                File.WriteAllText(resultPath, JsonUtility.ToJson(result, true));
            }

            yield return new WaitForEndOfFrame();
            Application.Quit();
        }

        private static bool Measure(Case probe, string href, out float inlineExtent,
            out float blockExtent, out bool linkHit, out RectInt pixelBounds)
        {
            inlineExtent = 0f;
            blockExtent = 0f;
            linkHit = false;
            pixelBounds = default;
            if (!probe.producer.TryGetLinkGeometrySnapshot(out var geometry, out _) ||
                geometry.Paragraphs.Count != 1)
            {
                return false;
            }

            var rects = geometry.Paragraphs[0].GetRectsForRange(0, 3);
            if (rects.Count == 0) return false;
            var left = rects[0].xMin;
            var right = rects[0].xMax;
            var top = rects[0].yMin;
            var bottom = rects[0].yMax;
            for (var index = 1; index < rects.Count; ++index)
            {
                left = Mathf.Min(left, rects[index].xMin);
                right = Mathf.Max(right, rects[index].xMax);
                top = Mathf.Min(top, rects[index].yMin);
                bottom = Mathf.Max(bottom, rects[index].yMax);
            }
            inlineExtent = bottom - top;
            blockExtent = right - left;
            if (!TryGetVisibleLinkPoint(geometry, out var point) ||
                !probe.producer.TryHitLink(point, out var hit) || hit.Href != href)
            {
                return false;
            }

            linkHit = true;
            var readback = ReadOutput(probe.textBox,
                new Vector2Int(probe.producer.OutputWidth, probe.producer.OutputHeight));
            try
            {
                return TryGetColorBounds(readback.GetPixels32(), readback.width, readback.height,
                    (red, green, blue) => red > 40 || green > 40 || blue > 40, out pixelBounds);
            }
            finally
            {
                Destroy(readback);
            }
        }

        private static Case CreateCase(GameObject root, string name, string content,
            Vector2 position)
        {
            var child = new GameObject("Orientation " + name, typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            var rect = child.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(210f, 220f);
            rect.anchoredPosition = position;
            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            producer.enableRichText = true;
            producer.content = content;
            producer.margin = Margin.FixedZero();
            producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            producer.paragraphStyle.textDirection = TextDirection.Ltr;
            producer.paragraphStyle.textAlign = TextAlign.Start;
            producer.textStyle.size = 40f;
            producer.textStyle.lineHeight = 0f;
            producer.textOverflow = TextOverflow.Clip;
            return new Case
            {
                producer = producer,
                textBox = child.AddComponent<TextBox>(),
            };
        }

        private static string SaveOutput(Case probe, string name)
        {
            var texture = ReadOutput(probe.textBox,
                new Vector2Int(probe.producer.OutputWidth, probe.producer.OutputHeight));
            try
            {
                var path = string.IsNullOrEmpty(ArgumentValue(ResultArgument))
                    ? ""
                    : Path.ChangeExtension(ArgumentValue(ResultArgument), null) + "-" + name + ".png";
                if (path.Length != 0)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                return path;
            }
            finally
            {
                Destroy(texture);
            }
        }

        private static string ContentText(Case probe)
        {
            return probe.producer.TryGetContentSnapshot(out var content) ? content.PlainText : "";
        }

        private static Texture2D ReadOutput(TextBox textBox, Vector2Int size)
        {
            var temporary = RenderTexture.GetTemporary(size.x, size.y, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(textBox.Texture, temporary, textBox.UvRect.size, textBox.UvRect.position);
                RenderTexture.active = temporary;
                var readback = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false, true);
                readback.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                readback.Apply();
                return readback;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static bool TryGetVisibleLinkPoint(TextBoxLinkGeometrySnapshot snapshot,
            out Vector2 normalizedPoint)
        {
            normalizedPoint = default;
            var scale = snapshot.FitScale > 0f && float.IsFinite(snapshot.FitScale)
                ? snapshot.FitScale
                : 1f;
            foreach (var link in snapshot.Geometry)
            {
                foreach (var rect in link.Rects)
                {
                    var outputRect = new Rect(rect.x * scale + snapshot.PaintPosition.x,
                        rect.y * scale + snapshot.PaintPosition.y,
                        rect.width * scale,
                        rect.height * scale);
                    var visibleRect = Rect.MinMaxRect(
                        Mathf.Max(outputRect.xMin, snapshot.ClipRect.xMin),
                        Mathf.Max(outputRect.yMin, snapshot.ClipRect.yMin),
                        Mathf.Min(outputRect.xMax, snapshot.ClipRect.xMax),
                        Mathf.Min(outputRect.yMax, snapshot.ClipRect.yMax));
                    if (visibleRect.width < 1f || visibleRect.height < 1f) continue;
                    normalizedPoint = new Vector2(visibleRect.center.x / snapshot.VisibleOutputSize.x,
                        visibleRect.center.y / snapshot.VisibleOutputSize.y);
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetColorBounds(Color32[] pixels, int width, int height,
            Func<byte, byte, byte, bool> predicate, out RectInt bounds)
        {
            var minX = width;
            var minY = height;
            var maxX = -1;
            var maxY = -1;
            for (var row = 0; row < height; ++row)
            {
                var y = height - 1 - row;
                for (var x = 0; x < width; ++x)
                {
                    var color = pixels[row * width + x];
                    if (!predicate(color.r, color.g, color.b)) continue;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                bounds = default;
                return false;
            }

            bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        private static bool HasArgument(string name)
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, name, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        private static string ArgumentValue(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; ++index)
            {
                if (string.Equals(arguments[index], name, StringComparison.Ordinal))
                {
                    return arguments[index + 1];
                }
            }

            return "";
        }

        private sealed class Case
        {
            public TextBoxRenderTextureProducer producer;
            public TextBox textBox;

            public bool Ready()
            {
                return producer != null && textBox != null && producer.HasOutput &&
                       textBox.Texture != null && producer.TryGetLinkGeometrySnapshot(out _, out _);
            }
        }

        [Serializable]
        private sealed class ProbeResult
        {
            public bool Passed;
            public bool UprightPassed;
            public bool SidewaysPassed;
            public bool SidewaysTcyPassed;
            public bool MixedTcyPassed;
            public float UprightInlineExtent;
            public float SidewaysInlineExtent;
            public float SidewaysTcyInlineExtent;
            public float MixedTcyInlineExtent;
            public float UprightBlockExtent;
            public float SidewaysBlockExtent;
            public float SidewaysTcyBlockExtent;
            public float MixedTcyBlockExtent;
            public bool UprightLinkHit;
            public bool SidewaysLinkHit;
            public bool SidewaysTcyLinkHit;
            public bool MixedTcyLinkHit;
            public RectInt UprightPixelBounds;
            public RectInt SidewaysPixelBounds;
            public RectInt SidewaysTcyPixelBounds;
            public RectInt MixedTcyPixelBounds;
            public string UprightText = "";
            public string SidewaysText = "";
            public string SidewaysTcyText = "";
            public string MixedTcyText = "";
            public string UprightScreenshot = "";
            public string SidewaysScreenshot = "";
            public string SidewaysTcyScreenshot = "";
            public string MixedTcyScreenshot = "";
            public string LastLayoutError = "";
            public string Exception = "";
        }
    }
}

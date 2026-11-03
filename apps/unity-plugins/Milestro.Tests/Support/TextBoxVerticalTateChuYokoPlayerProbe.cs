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
    public sealed class TextBoxVerticalTateChuYokoPlayerProbe : MonoBehaviour
    {
        private const string ProbeArgument = "-milestroTateChuYokoProbe";
        private const string ResultArgument = "-milestroTateChuYokoResult";
        private const string CompressedContent =
            "<a href=\"tcy-compressed\"><span style=\"text-combine-upright: all\">ABC</span></a>天地";
        private const string NaturalContent =
            "<a href=\"tcy-natural\"><span style=\"text-combine-upright: all\">A</span></a>天地";
        private const string GrowContent =
            "<a href=\"tcy-grow\"><span style=\"text-combine-upright: all; --milestro-text-combine-upright-sizing: grow\">ABC</span></a>天地";

        private string resultPath = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument(ProbeArgument)) return;
            var host = new GameObject("Milestro tate-chu-yoko player probe");
            DontDestroyOnLoad(host);
            host.AddComponent<TextBoxVerticalTateChuYokoPlayerProbe>();
        }

        private void Awake()
        {
            resultPath = ArgumentValue(ResultArgument);
        }

        private IEnumerator Start()
        {
            var result = new ProbeResult();
            GameObject root = null;
            Case compressed = null;
            Case natural = null;
            Case grow = null;
            Case compressedReference = null;
            Case naturalReference = null;
            try
            {
                root = new GameObject("Tate-chu-yoko probe canvas", typeof(RectTransform), typeof(Canvas));
                root.SetActive(false);
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var camera = root.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
                camera.cullingMask = 0;
                compressed = CreateCase(root, "Compressed", CompressedContent, new Vector2(0f, 0f));
                natural = CreateCase(root, "Natural", NaturalContent, new Vector2(210f, 0f));
                grow = CreateCase(root, "Grow", GrowContent, new Vector2(840f, 0f));
                compressedReference = CreateCase(root, "Compressed reference",
                    "<a href=\"tcy-reference-compressed\">ABC</a>",
                    new Vector2(420f, 0f), WritingMode.HorizontalTb);
                naturalReference = CreateCase(root, "Natural reference",
                    "<a href=\"tcy-reference-natural\">A</a>",
                    new Vector2(630f, 0f), WritingMode.HorizontalTb);
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
                        compressed?.producer.RebuildOutput(false);
                        natural?.producer.RebuildOutput(false);
                        grow?.producer.RebuildOutput(false);
                        compressedReference?.producer.RebuildOutput(false);
                        naturalReference?.producer.RebuildOutput(false);
                        if (compressed != null && natural != null && grow != null &&
                            compressedReference != null && naturalReference != null &&
                            compressed.Ready() && natural.Ready() && grow.Ready() &&
                            compressedReference.Ready() && naturalReference.Ready())
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
                    result.CompressedPassed = Evaluate(compressed, "tcy-compressed", 3,
                        out result.CompressedCellWidth, out result.CompressedLinkHit,
                        out result.CompressedPixelBounds);
                    result.NaturalPassed = Evaluate(natural, "tcy-natural", 1,
                        out result.NaturalCellWidth, out result.NaturalLinkHit,
                        out result.NaturalPixelBounds);
                    result.GrowPassed = Evaluate(grow, "tcy-grow", 3,
                        out result.GrowCellWidth, out result.GrowLinkHit,
                        out result.GrowPixelBounds);
                    result.CompressedReferenceWidth = ContentRangeWidth(compressedReference, 3);
                    result.NaturalReferenceWidth = ContentRangeWidth(naturalReference, 1);
                    result.GrowReferenceWidth = ContentRangeWidth(compressedReference, 3);
                    result.CompressedPassed &= Mathf.Abs(result.CompressedCellWidth - 30f) <= 0.5f &&
                                              result.CompressedReferenceWidth > result.CompressedCellWidth + 1f;
                    result.NaturalPassed &= result.NaturalCellWidth <= 30f + 0.5f &&
                                            Mathf.Abs(result.NaturalCellWidth -
                                                      result.NaturalReferenceWidth) <= 0.5f;
                    result.GrowPassed &= result.GrowCellWidth > 30f + 1f &&
                                         Mathf.Abs(result.GrowCellWidth -
                                                   result.GrowReferenceWidth) <= 0.5f;
                    result.Passed = result.CompressedPassed && result.NaturalPassed &&
                                    result.GrowPassed;
                    result.CompressedScreenshot = SaveOutput(compressed, "compressed");
                    result.NaturalScreenshot = SaveOutput(natural, "natural");
                    result.GrowScreenshot = SaveOutput(grow, "grow");
                    result.CompressedText = ContentText(compressed);
                    result.NaturalText = ContentText(natural);
                    result.GrowText = ContentText(grow);
                    result.LastLayoutError = compressed.producer.lastLayoutError?.GetType().Name ??
                                             natural.producer.lastLayoutError?.GetType().Name ??
                                             grow.producer.lastLayoutError?.GetType().Name ?? "";
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

        private static float ContentRangeWidth(Case probe, int groupLength)
        {
            if (!probe.producer.TryGetLinkGeometrySnapshot(out var geometry, out _) ||
                geometry.Paragraphs.Count != 1)
            {
                return 0f;
            }

            var rects = geometry.Paragraphs[0].GetRectsForRange(0, groupLength);
            if (rects.Count == 0) return 0f;
            var left = rects[0].xMin;
            var right = rects[0].xMax;
            for (var index = 1; index < rects.Count; ++index)
            {
                left = Mathf.Min(left, rects[index].xMin);
                right = Mathf.Max(right, rects[index].xMax);
            }

            return right - left;
        }

        private static bool Evaluate(Case probe, string href, int groupLength,
            out float cellWidth, out bool linkHit, out RectInt pixelBounds)
        {
            cellWidth = 0f;
            linkHit = false;
            pixelBounds = default;
            if (!probe.producer.TryGetLinkGeometrySnapshot(out var geometry, out _) ||
                geometry.Paragraphs.Count != 1)
            {
                return false;
            }

            var rects = geometry.Paragraphs[0].GetRectsForRange(0, groupLength);
            if (rects.Count != 1) return false;
            cellWidth = rects[0].width;
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
            Vector2 position, WritingMode writingMode = WritingMode.VerticalRl)
        {
            var child = new GameObject("Tate-chu-yoko " + name, typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            var rect = child.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 160f);
            rect.anchoredPosition = position;
            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            producer.enableRichText = true;
            producer.content = content;
            producer.margin = Margin.FixedZero();
            producer.paragraphStyle.writingMode = writingMode;
            producer.paragraphStyle.textDirection = TextDirection.Ltr;
            producer.paragraphStyle.textAlign = TextAlign.Start;
            producer.textStyle.size = 30f;
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
            public bool CompressedPassed;
            public bool NaturalPassed;
            public bool GrowPassed;
            public float CompressedCellWidth;
            public float CompressedReferenceWidth;
            public float NaturalCellWidth;
            public float NaturalReferenceWidth;
            public float GrowCellWidth;
            public float GrowReferenceWidth;
            public bool CompressedLinkHit;
            public bool NaturalLinkHit;
            public bool GrowLinkHit;
            public RectInt CompressedPixelBounds;
            public RectInt NaturalPixelBounds;
            public RectInt GrowPixelBounds;
            public string CompressedText = "";
            public string NaturalText = "";
            public string GrowText = "";
            public string CompressedScreenshot = "";
            public string NaturalScreenshot = "";
            public string GrowScreenshot = "";
            public string LastLayoutError = "";
            public string Exception = "";
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Model;
using Milestro.RichTextParser;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RenderTextureAtlas = Milestro.Skia.RenderTextureAtlas;
using UnitySkiaRenderTextureSurface = Milestro.Skia.UnitySkiaRenderTextureSurface;

namespace Milestro.Tests.TextBoxVertical
{
    public sealed class TextBoxVerticalDegradePlayerProbe : MonoBehaviour
    {
        private const string ProbeArgument = "-milestroVerticalDegradeProbe";
        private const string ResultArgument = "-milestroVerticalProbeResult";
        private const string Content = "<a href=\"player-link\">「漢字ABC2026かな」、。ー</a>";
        private const string IndentContent =
            "<p style=\"text-indent: 40px\"><a href=\"indent-link\">天地玄黃宇宙洪荒日月</a></p>";
        private const string JustifyContent =
            "<a href=\"justify-link\">天地玄黃宇宙洪荒日月</a>";
        private const string EllipsisPlainText = "天地玄黃宇宙日月盈昃辰宿列張";
        private const string EllipsisContent =
            "<a href=\"ellipsis-link\">" + EllipsisPlainText + "</a>";
        private const string DecorationContent =
            "<font color=\"#ff0000\"><u>　</u></font>" +
            "<font color=\"#00ff00\"><s>　</s></font>" +
            "<a href=\"decoration-link\">漢字</a>";

        public const string RubyContent = "<a href=\"ruby-link\"><ruby style=\"ruby-align: center; " +
            "ruby-overhang: none; --milestro-ruby-gap: 4\">漢字<rp>(</rp><rt>かなABC</rt><rp>)</rp></ruby></a>";

        public const string BaselineShiftContent =
            "<a href=\"baseline-link\">" +
            "<font color=\"#ff0000\"><span fscale=\"0.5\">天</span></font>" +
            "<font color=\"#00ff00\"><sup>天</sup></font>" +
            "<font color=\"#0000ff\"><sub>天</sub></font>" +
            "<font color=\"#00ffff\"><span fscale=\"0.5\" voffset=\"9\">天</span></font>" +
            "<font color=\"#ff00ff\"><span fscale=\"0.5\" voffset-em=\"-0.4\">天</span></font></a>";

        private static readonly BindingFlags DeclaredInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        private readonly List<string> logs = new List<string>();
        private string resultPath = "";
        private string screenshotPath = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument(ProbeArgument))
            {
                return;
            }

            var host = new GameObject("Milestro vertical player probe");
            DontDestroyOnLoad(host);
            host.AddComponent<TextBoxVerticalDegradePlayerProbe>();
        }

        private void Awake()
        {
            resultPath = ArgumentValue(ResultArgument);
            screenshotPath = string.IsNullOrWhiteSpace(resultPath)
                ? ""
                : Path.ChangeExtension(resultPath, ".png");
            Application.logMessageReceivedThreaded += HandleLog;
        }

        private IEnumerator Start()
        {
            var result = new ProbeResult();
            GameObject root = null;
            ProbeCase legalAuto = null;
            ProbeCase invalidConfig = null;
            ProbeCase legalContent = null;
            ProbeCase autoHeight = null;
            ProbeCase indent = null;
            ProbeCase justify = null;
            ProbeCase ellipsis = null;
            ProbeCase decoration = null;
            ProbeCase baseline = null;
            ProbeCase multiBlock = null;
            ProbeCase placeholder = null;
            ProbeCase ruby = null;
            try
            {
                root = new GameObject("Vertical TextBox probe canvas",
                    typeof(RectTransform),
                    typeof(Canvas));
                root.SetActive(false);
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var camera = root.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
                camera.cullingMask = 0;
                legalAuto = CreateCase(root, "Legal auto", new Margin
                {
                    Left = new MarginValue { Auto = true },
                    Top = new MarginValue { Auto = true },
                    Right = new MarginValue { Auto = true },
                    Bottom = new MarginValue { Auto = true },
                }, invalid: false);
                invalidConfig = CreateCase(root, "Invalid config", new Margin
                {
                    Left = new MarginValue { Auto = true },
                    Top = new MarginValue(),
                    Right = new MarginValue { Value = 10f },
                    Bottom = new MarginValue { Value = 10f },
                }, invalid: true);
                legalContent = CreateContentSizingCase(root);
                autoHeight = CreateAutoHeightCase(root);
                indent = CreateIndentCase(root);
                justify = CreateJustifyCase(root);
                ellipsis = CreateEllipsisCase(root);
                decoration = CreateDecorationCase(root);
                baseline = CreateDecorationCase(root);
                baseline.producer.content = BaselineShiftContent;
                baseline.rect.anchoredPosition = new Vector2(210f, 0f);
                multiBlock = CreateJustifyCase(root);
                multiBlock.rect.anchoredPosition = new Vector2(450f, -280f);
                multiBlock.rect.sizeDelta = new Vector2(160f, 120f);
                multiBlock.producer.paragraphStyle.textAlign = TextAlign.Start;
                multiBlock.producer.paragraphStyle.paragraphSpacing = 23f;
                multiBlock.producer.content = "<a href=\"multi-block\">" +
                    "<p><font color=\"#ff7777\">天地玄黃宇宙洪荒</font></p>" +
                    "<p><font color=\"#77ff77\">日月盈昃辰宿列張</font></p>" +
                    "<p><font color=\"#7777ff\">寒來暑往秋收冬藏</font></p></a>";
                placeholder = CreateJustifyCase(root);
                placeholder.producer.paragraphStyle.textAlign = TextAlign.Start;
                placeholder.producer.content = "<a href=\"placeholder-link\">天<img width=\"50\" height=\"20\" key=\"player-object\"/>地</a>";
                ruby = CreateJustifyCase(root);
                ruby.rect.anchoredPosition = new Vector2(610f, -280f);
                ruby.rect.sizeDelta = new Vector2(180f, 160f);
                ruby.producer.paragraphStyle.textAlign = TextAlign.Start;
                ruby.producer.content = RubyContent.Replace("ruby-align:", "ruby-position: under; ruby-align:");
                root.SetActive(true);
            }
            catch (Exception error)
            {
                result.Exception = error.ToString();
            }

            if (result.Exception.Length == 0)
            {
                for (var frame = 0; frame < 180; ++frame)
                {
                    var ready = false;
                    try
                    {
                        RenderTextureAtlas.Flush();
                        UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                        legalAuto?.producer.RebuildOutput(false);
                        invalidConfig?.producer.RebuildOutput(false);
                        legalContent?.producer.RebuildOutput(false);
                        autoHeight?.producer.RebuildOutput(false);
                        indent?.producer.RebuildOutput(false);
                        justify?.producer.RebuildOutput(false);
                        ellipsis?.producer.RebuildOutput(false);
                        decoration?.producer.RebuildOutput(false);
                        baseline?.producer.RebuildOutput(false);
                        multiBlock?.producer.RebuildOutput(false);
                        placeholder?.producer.RebuildOutput(false);
                        ruby?.producer.RebuildOutput(false);
                        if (legalContent?.rect != null)
                        {
                            Canvas.ForceUpdateCanvases();
                            LayoutRebuilder.ForceRebuildLayoutImmediate(legalContent.rect);
                        }
                        ready = legalAuto != null && legalAuto.Ready() &&
                                invalidConfig != null && invalidConfig.Ready() &&
                                legalContent != null && legalContent.Ready() &&
                                autoHeight != null && autoHeight.Ready() &&
                                indent != null && indent.Ready() &&
                                justify != null && justify.Ready() &&
                                ellipsis != null && ellipsis.Ready() &&
                                decoration != null && decoration.Ready() &&
                                baseline != null && baseline.Ready() &&
                                multiBlock != null && multiBlock.Ready() &&
                                placeholder != null && placeholder.Ready() && ruby != null && ruby.Ready();
                    }
                    catch (Exception error)
                    {
                        result.Exception = error.ToString();
                        break;
                    }

                    if (ready)
                    {
                        break;
                    }

                    yield return null;
                }
            }

            if (legalAuto != null && invalidConfig != null &&
                legalContent != null && autoHeight != null && indent != null &&
                justify != null && ellipsis != null && decoration != null && baseline != null)
            {
                try
                {
                    result.LegalAutoPassed = EvaluateCase(legalAuto,
                        out var legalPaintX,
                        out var legalPaintY);
                    result.BaselineShiftPassed = EvaluateBaselineShiftOutput(baseline.textBox,
                        baseline.producer, true, out result.BaselineShiftOffsets);
                    result.InvalidConfigPassed = EvaluateCase(invalidConfig, out _, out _);
                    result.LegalContentPassed = EvaluateContentSizingCase(legalContent);
                    result.AutoHeightPassed = EvaluateAutoHeightCase(autoHeight);
                    result.IndentPassed = EvaluateIndentCase(indent,
                        out var indentFirstRectYMin,
                        out var indentSecondRectYMin,
                        out var indentLinkRectCount,
                        out var indentLinkHit);
                    result.JustifyPassed = EvaluateJustifyCase(justify,
                        out var justifyFirstRectYMin,
                        out var justifyFirstRectHeight,
                        out var justifyLastRectHeight,
                        out var justifyLinkRectCount,
                        out var justifyLinkHit);
                    result.EllipsisPassed = EvaluateEllipsisCase(ellipsis,
                        out var ellipsisUtf16End,
                        out var ellipsisInlineExtent,
                        out var ellipsisPhysicalHeight,
                        out var ellipsisLinkRectCount,
                        out var ellipsisHiddenRangeEmpty,
                        out var ellipsisLinkHit);
                    result.DecorationPassed = EvaluateDecorationCase(decoration,
                        out var decorationUnderlineX,
                        out var decorationThroughX,
                        out var decorationThroughOffset,
                        out var decorationLinkHit,
                        out var decorationHoverVertical,
                        out var decorationHoverPassed,
                        out var decorationOutsideSourceRejected,
                        out var decorationHoverDiagnostic);
                    result.LegalPaintX = legalPaintX;
                    result.LegalPaintY = legalPaintY;
                    result.IndentFirstRectYMin = indentFirstRectYMin;
                    result.IndentSecondRectYMin = indentSecondRectYMin;
                    result.IndentLinkRectCount = indentLinkRectCount;
                    result.IndentLinkHit = indentLinkHit;
                    result.JustifyFirstRectYMin = justifyFirstRectYMin;
                    result.JustifyFirstRectHeight = justifyFirstRectHeight;
                    result.JustifyLastRectHeight = justifyLastRectHeight;
                    result.JustifyLinkRectCount = justifyLinkRectCount;
                    result.JustifyLinkHit = justifyLinkHit;
                    result.EllipsisUtf16End = ellipsisUtf16End;
                    result.EllipsisInlineExtent = ellipsisInlineExtent;
                    result.EllipsisPhysicalHeight = ellipsisPhysicalHeight;
                    result.EllipsisLinkRectCount = ellipsisLinkRectCount;
                    result.EllipsisHiddenRangeEmpty = ellipsisHiddenRangeEmpty;
                    result.EllipsisLinkHit = ellipsisLinkHit;
                    result.DecorationUnderlineX = decorationUnderlineX;
                    result.DecorationThroughX = decorationThroughX;
                    result.DecorationThroughOffset = decorationThroughOffset;
                    result.DecorationLinkHit = decorationLinkHit;
                    result.DecorationHoverVertical = decorationHoverVertical;
                    result.DecorationHoverPassed = decorationHoverPassed;
                    result.DecorationOutsideSourceRejected = decorationOutsideSourceRejected;
                    result.DecorationHoverDiagnostic = decorationHoverDiagnostic;
                    result.HasOutput = legalAuto.producer.HasOutput &&
                                       invalidConfig.producer.HasOutput &&
                                       legalContent.producer.HasOutput &&
                                       autoHeight.producer.HasOutput &&
                                       indent.producer.HasOutput &&
                                       justify.producer.HasOutput &&
                                       ellipsis.producer.HasOutput &&
                                       decoration.producer.HasOutput;
                    result.HasTexture = legalAuto.textBox.Texture != null &&
                                        invalidConfig.textBox.Texture != null &&
                                        legalContent.textBox.Texture != null &&
                                        autoHeight.textBox.Texture != null &&
                                        indent.textBox.Texture != null &&
                                        justify.textBox.Texture != null &&
                                        ellipsis.textBox.Texture != null &&
                                        decoration.textBox.Texture != null;
                    result.IsVertical = legalAuto.producer.isVerticalLayout &&
                                        invalidConfig.producer.isVerticalLayout &&
                                        legalContent.producer.isVerticalLayout &&
                                        autoHeight.producer.isVerticalLayout &&
                                        indent.producer.isVerticalLayout &&
                                        justify.producer.isVerticalLayout &&
                                        ellipsis.producer.isVerticalLayout &&
                                        decoration.producer.isVerticalLayout;
                    result.ContentWidth = legalAuto.producer.contentWidth;
                    result.HasContentSnapshot = legalAuto.producer.TryGetContentSnapshot(out var content);
                    result.PlainText = result.HasContentSnapshot ? content.PlainText : "";
                    result.HasLinkGeometry = legalAuto.producer.TryGetLinkGeometrySnapshot(out _, out _) &&
                                             invalidConfig.producer.TryGetLinkGeometrySnapshot(out _, out _) &&
                                             indent.producer.TryGetLinkGeometrySnapshot(out _, out _) &&
                                             justify.producer.TryGetLinkGeometrySnapshot(out _, out _) &&
                                             ellipsis.producer.TryGetLinkGeometrySnapshot(out _, out _) &&
                                             decoration.producer.TryGetLinkGeometrySnapshot(out _, out _);
                    result.LinkHit = result.LegalAutoPassed && result.InvalidConfigPassed;
                    result.LastLayoutError = legalAuto.producer.lastLayoutError?.GetType().Name ?? "";
                    if (result.LastLayoutError.Length == 0)
                    {
                        result.LastLayoutError = invalidConfig.producer.lastLayoutError?.GetType().Name ?? "";
                    }
                    if (result.LastLayoutError.Length == 0)
                    {
                        result.LastLayoutError = ellipsis.producer.lastLayoutError?.GetType().Name ?? "";
                    }
                    result.ConfigLogCount = CountConfigurationLogs();
                    if (placeholder != null && placeholder.textBox.TryGetPlaceholderGeometrySnapshot(out var objects) && objects.Geometry.Count == 1)
                    {
                        var item = objects.Geometry[0];
                        result.PlaceholderRect = item.Rect;
                        result.PlaceholderKey = item.Key;
                        result.PlaceholderStartUtf16 = item.StartUtf16;
                        result.PlaceholderVisible = item.Visible;
                        var point = objects.PaintPosition + item.Rect.center * objects.FitScale;
                        result.PlaceholderPassed = placeholder.producer.HasOutput && placeholder.textBox.Texture != null &&
                            item.Key == "player-object" && item.StartUtf16 == 1 && item.Visible &&
                            Mathf.Abs(item.Rect.width - 50) < 0.01f && Mathf.Abs(item.Rect.height - 20) < 0.01f &&
                            placeholder.producer.TryHitLink(new Vector2(point.x / placeholder.rect.rect.width, point.y / placeholder.rect.rect.height), out var objectHit) &&
                            objectHit.Href == "placeholder-link";
                    }
                    result.RubyPassed = ruby != null && EvaluateRubyOutput(ruby.textBox, ruby.producer,
                        out result.RubyAnnotationPixels, out result.RubyLinkHit);
                    if (result.RubyPassed) SaveOutput(ruby, "ruby");
                    result.Passed = result.RubyPassed && result.PlaceholderPassed && result.LegalAutoPassed &&
                                    result.InvalidConfigPassed &&
                                    result.LegalContentPassed &&
                                    result.AutoHeightPassed &&
                                    result.IndentPassed &&
                                    result.JustifyPassed &&
                                    result.EllipsisPassed &&
                                    result.DecorationPassed &&
                                    result.BaselineShiftPassed &&
                                    result.LastLayoutError.Length == 0 &&
                                    result.ConfigLogCount == 0;
                }
                catch (Exception error)
                {
                    result.Exception = error.ToString();
                }
            }

            if (result.Passed && ruby != null)
            {
                var version = ruby.producer.OutputVersion;
                ruby.producer.enableAutoFontSize = true;
                ruby.producer.fontSizeMin = 15;
                ruby.producer.fontSizeMax = 15;
                for (var frame = 0; frame < 180; ++frame)
                {
                    RenderTextureAtlas.Flush();
                    UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                    ruby.producer.RebuildOutput(false);
                    if (ruby.Ready() && ruby.producer.OutputVersion != version) break;
                    yield return null;
                }
                result.RubyAutoFontPassed = EvaluateRubyOutput(ruby.textBox, ruby.producer,
                    out result.RubyAutoFontPixels, out _) &&
                    ruby.producer.TryGetLinkGeometrySnapshot(out var geometry, out _) &&
                    Mathf.Abs(geometry.FitScale - 0.5f) < 0.001f;
                result.Passed &= result.RubyAutoFontPassed;
                if (result.RubyAutoFontPassed) SaveOutput(ruby, "ruby-auto-font");
            }

            if (result.Passed && baseline != null)
            {
                foreach (var vertical in new[] { false, true })
                {
                    baseline.producer.paragraphStyle.writingMode = vertical
                        ? WritingMode.VerticalRl
                        : WritingMode.HorizontalTb;
                    var previousVersion = baseline.producer.OutputVersion;
                    for (var frame = 0; frame < 180; ++frame)
                    {
                        RenderTextureAtlas.Flush();
                        UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                        baseline.producer.RebuildOutput(false);
                        if (baseline.Ready() && baseline.producer.OutputVersion != previousVersion) break;
                        yield return null;
                    }
                    var passed = EvaluateBaselineShiftOutput(baseline.textBox, baseline.producer,
                        vertical, out var offsets);
                    if (!vertical)
                    {
                        result.HorizontalBaselineShiftPassed = passed;
                        result.HorizontalBaselineShiftOffsets = offsets;
                    }
                    result.Passed = result.Passed && passed;
                }
            }

            if (multiBlock != null && result.Exception.Length == 0)
            {
                foreach (Transform child in root.transform)
                {
                    if (child != multiBlock.rect) child.gameObject.SetActive(false);
                }
                multiBlock.rect.anchoredPosition = new Vector2(320f, -200f);
                result.MultiBlockInitialPassed = EvaluateMultiBlockCase(multiBlock,
                    out result.MultiBlockWidth, out result.MultiBlockFrameX);
                result.MultiBlockInitialScroll = multiBlock.producer.scrollX;
                if (screenshotPath.Length > 0)
                {
                    ScreenCapture.CaptureScreenshot(Path.ChangeExtension(screenshotPath, null) + "-blocks-initial.png");
                    yield return new WaitForEndOfFrame();
                }
                var version = multiBlock.producer.OutputVersion;
                multiBlock.textBox.ScrollToPercentX(1f);
                for (var frame = 0; frame < 180; ++frame)
                {
                    RenderTextureAtlas.Flush();
                    UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                    multiBlock.producer.RebuildOutput(false);
                    if (multiBlock.Ready() && multiBlock.producer.OutputVersion != version) break;
                    yield return null;
                }
                result.MultiBlockScrolledPassed = EvaluateMultiBlockCase(multiBlock, out _, out _);
                result.MultiBlockFinalScroll = multiBlock.producer.scrollX;
                result.MultiBlockPassed = result.MultiBlockInitialPassed && result.MultiBlockScrolledPassed &&
                    result.MultiBlockInitialScroll == 0f && result.MultiBlockFinalScroll > 0f &&
                    Mathf.Abs(result.MultiBlockFinalScroll - multiBlock.producer.maxScrollX) < 0.01f;
                result.Passed = result.Passed && result.MultiBlockPassed;

                version = multiBlock.producer.OutputVersion;
                multiBlock.producer.wrapMode = TextBoxWrapMode.NoWrap;
                multiBlock.producer.content = "<a href=\"multi-block\">" +
                    "<p style=\"margin-top: 150px; margin-bottom: 11px\">天地玄黃宇宙洪荒</p>" +
                    "<p style=\"margin-top: 1em; margin-bottom: 10%\">日月盈昃辰宿列張</p>" +
                    "<p style=\"margin-top: 7px; margin-bottom: 19px\">寒來暑往秋收冬藏</p></a>";
                for (var frame = 0; frame < 180; ++frame)
                {
                    RenderTextureAtlas.Flush();
                    UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                    multiBlock.producer.RebuildOutput(false);
                    if (multiBlock.Ready() && multiBlock.producer.OutputVersion != version) break;
                    yield return null;
                }
                version = multiBlock.producer.OutputVersion;
                multiBlock.textBox.ScrollToPercentY(1f);
                for (var frame = 0; frame < 180; ++frame)
                {
                    RenderTextureAtlas.Flush();
                    UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                    multiBlock.producer.RebuildOutput(false);
                    if (multiBlock.Ready() && multiBlock.producer.OutputVersion != version) break;
                    yield return null;
                }
                result.BlockMarginPassed = EvaluateBlockMarginCase(multiBlock,
                    out result.BlockMarginFrameY, out result.BlockMarginHeight);
                result.BlockMarginScrollY = multiBlock.producer.scrollY;
                result.Passed = result.Passed && result.BlockMarginPassed;
            }

            if (multiBlock != null && result.Exception.Length == 0)
            {
                result.AutoFontSizePassed = true;
                var samples = new[]
                {
                    "天地玄黃宇宙洪荒日月盈昃辰宿列張寒來暑往秋收冬藏",
                    "天",
                    "天地玄黃宇宙洪荒日月盈昃",
                };
                for (var sample = 0; sample < samples.Length; ++sample)
                {
                    var version = multiBlock.producer.OutputVersion;
                    multiBlock.rect.sizeDelta = new Vector2(160f, 120f);
                    multiBlock.producer.content = "<a href=\"auto-font\">" + samples[sample] + "</a>";
                    multiBlock.producer.textStyle.size = 30f;
                    multiBlock.producer.enableAutoFontSize = true;
                    multiBlock.producer.fontSizeMin = 6f;
                    multiBlock.producer.fontSizeMax = 60f;
                    multiBlock.producer.wrapMode = sample == 2 ? TextBoxWrapMode.NoWrap : TextBoxWrapMode.Wrap;
                    for (var frame = 0; frame < 180; ++frame)
                    {
                        RenderTextureAtlas.Flush();
                        UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                        multiBlock.producer.RebuildOutput(false);
                        if (multiBlock.Ready() && multiBlock.producer.OutputVersion != version) break;
                        yield return null;
                    }
                    var passed = multiBlock.producer.TryGetLinkGeometrySnapshot(out var geometry, out _) &&
                                 TryGetVisibleLinkPoint(geometry, out var point) &&
                                 multiBlock.producer.TryHitLink(point, out var hit) && hit.Href == "auto-font";
                    result.AutoFontScales[sample] = geometry.FitScale;
                    var pixels = ReadOutput(multiBlock.textBox, new Vector2Int(160, 120));
                    passed &= TryGetColorBounds(pixels.GetPixels32(), 160, 120,
                        (red, green, blue) => red > 40 || green > 40 || blue > 40,
                        out result.AutoFontPixelBounds[sample]);
                    if (screenshotPath.Length != 0)
                    {
                        File.WriteAllBytes(Path.ChangeExtension(screenshotPath, null) + "-auto-font-" + sample + ".png",
                            pixels.EncodeToPNG());
                    }
                    Destroy(pixels);
                    passed &= sample == 1 ? geometry.FitScale == 2f : sample == 0
                        ? geometry.FitScale > 0.4f && geometry.FitScale < 1f
                        : geometry.FitScale >= 0.2f && geometry.FitScale < 1f;
                    result.AutoFontSizePassed &= passed;
                }
                result.Passed &= result.AutoFontSizePassed;
            }

            if (result.Exception.Length != 0)
            {
                result.Passed = false;
            }

            if (screenshotPath.Length != 0)
            {
                result.ScreenshotPath = screenshotPath;
                Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath) ?? ".");
                if (File.Exists(screenshotPath))
                {
                    File.Delete(screenshotPath);
                }
                ScreenCapture.CaptureScreenshot(screenshotPath);
                yield return new WaitForEndOfFrame();
                for (var frame = 0; frame < 30 && !File.Exists(screenshotPath); ++frame)
                {
                    yield return null;
                }

                result.ScreenshotWritten = File.Exists(screenshotPath) &&
                                           new FileInfo(screenshotPath).Length > 0;
                result.Passed = result.Passed && result.ScreenshotWritten;
            }

            if (root != null)
            {
                Destroy(root);
            }

            var json = JsonUtility.ToJson(result);
            var path = string.IsNullOrWhiteSpace(resultPath)
                ? Path.Combine(Application.persistentDataPath, "milestro-vertical-probe.json")
                : resultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, json);
            Debug.Log("TEXTBOX_VERTICAL_PLAYER_PROBE " + json);
            yield return null;
            Application.Quit(result.Passed ? 0 : 1);
        }

        private void SaveOutput(ProbeCase probe, string suffix)
        {
            if (screenshotPath.Length == 0) return;
            var pixels = ReadOutput(probe.textBox, Vector2Int.RoundToInt(probe.rect.rect.size));
            try
            {
                File.WriteAllBytes(Path.ChangeExtension(screenshotPath, null) + "-" + suffix + ".png",
                    pixels.EncodeToPNG());
            }
            finally { Destroy(pixels); }
        }

        public static bool EvaluateRubyOutput(TextBox textBox, TextBoxRenderTextureProducer producer,
            out int annotationPixels, out bool linkHit)
        {
            annotationPixels = 0;
            linkHit = false;
            if (!producer.HasOutput || !producer.isVerticalLayout || producer.lastLayoutError != null ||
                !producer.TryGetLinkGeometrySnapshot(out var geometry, out _) || geometry.Paragraphs.Count != 1)
                return false;
            var column = geometry.Paragraphs[0].LayoutMetrics.Columns[0];
            var edge = geometry.PaintPosition.x +
                (column.PhysicalBaseline + producer.textStyle.size * 0.5f + 4f) * geometry.FitScale;
            var size = Vector2Int.RoundToInt(textBox.GetComponent<RectTransform>().rect.size);
            var pixels = ReadOutput(textBox, size);
            try
            {
                var data = pixels.GetPixels32();
                for (var pixelY = 0; pixelY < size.y; ++pixelY)
                    for (var pixelX = Mathf.Max(0, Mathf.CeilToInt(edge)); pixelX < size.x; ++pixelX)
                        if (data[pixelY * size.x + pixelX].a > 16) ++annotationPixels;
            }
            finally
            {
                Destroy(pixels);
            }
            var annotationPoint = new Vector2((edge + 3f * geometry.FitScale) / size.x,
                (geometry.PaintPosition.y + 30f * geometry.FitScale) / size.y);
            linkHit = producer.TryHitLink(annotationPoint, out var hit) && hit.Href == "ruby-link";
            return annotationPixels > 10 && linkHit && column.BlockThickness > producer.textStyle.size + 4f;
        }

        private static bool EvaluateMultiBlockCase(ProbeCase probe, out float width,
            out float[] frameX)
        {
            width = probe.producer.contentWidth;
            frameX = Array.Empty<float>();
            if (!probe.Ready() || !probe.producer.isVerticalLayout ||
                !probe.producer.TryGetLinkGeometrySnapshot(out var geometry, out _) ||
                geometry.Paragraphs.Count != 3 || geometry.Links.Count != 1 ||
                geometry.Geometry[0].BlockRanges.Count != 3 ||
                !TryGetVisibleLinkPoint(geometry, out var point) ||
                !probe.producer.TryHitLink(point, out var hit) || hit.Href != "multi-block")
            {
                return false;
            }
            frameX = new float[3];
            var expectedWidth = 46f;
            for (var index = 0; index < 3; ++index)
            {
                var range = geometry.Geometry[0].BlockRanges[index];
                frameX[index] = range.FrameOffset.x;
                expectedWidth += geometry.Paragraphs[index].LayoutMetrics.PhysicalSize.x;
                if (range.BlockIndex != index || range.StartUtf16 != 0 || range.EndUtf16 != 8 ||
                    range.FrameOffset.y != 0f) return false;
                if (index > 0 && Mathf.Abs(frameX[index - 1] - frameX[index] - 23f -
                    geometry.Paragraphs[index].LayoutMetrics.PhysicalSize.x) > 0.01f) return false;
            }
            return frameX[2] == 0f && Mathf.Abs(expectedWidth - width) < 0.01f;
        }

        private static bool EvaluateBlockMarginCase(ProbeCase probe, out float[] frameY,
            out float height)
        {
            frameY = Array.Empty<float>();
            height = probe.producer.contentHeight;
            if (!probe.Ready() || !probe.producer.TryGetLinkGeometrySnapshot(out var geometry, out _) ||
                geometry.Paragraphs.Count != 3 || geometry.Geometry.Count != 1 ||
                geometry.Geometry[0].BlockRanges.Count != 3 ||
                !TryGetVisibleLinkPoint(geometry, out var point) ||
                !probe.producer.TryHitLink(point, out var hit) || hit.Href != "multi-block") return false;
            var tops = new[] { 150f, probe.producer.textStyle.size, 7f };
            var bottoms = new[] { 11f, probe.producer.viewportHeight * 0.1f, 19f };
            frameY = new float[3];
            var expectedHeight = 0f;
            var expectedWidth = 46f;
            for (var index = 0; index < 3; ++index)
            {
                var range = geometry.Geometry[0].BlockRanges[index];
                var metrics = geometry.Paragraphs[index].LayoutMetrics;
                frameY[index] = range.FrameOffset.y;
                expectedHeight = Mathf.Max(expectedHeight, tops[index] + metrics.PhysicalSize.y + bottoms[index]);
                expectedWidth += metrics.PhysicalSize.x;
                if (Mathf.Abs(frameY[index] - tops[index]) > 0.01f) return false;
                if (index > 0 && Mathf.Abs(geometry.Geometry[0].BlockRanges[index - 1].FrameOffset.x -
                    range.FrameOffset.x - metrics.PhysicalSize.x - 23f) > 0.01f) return false;
            }
            return Mathf.Abs(expectedWidth - probe.producer.contentWidth) < 0.01f &&
                   Mathf.Abs(expectedHeight - height) < 0.01f &&
                   probe.producer.scrollY > 0f &&
                   Mathf.Abs(probe.producer.scrollY - probe.producer.maxScrollY) < 0.01f;
        }

        private static ProbeCase CreateCase(GameObject root,
            string name,
            Margin margin,
            bool invalid)
        {
            var child = new GameObject("Vertical TextBox " + name,
                typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            child.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 100f);

            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            producer.richTextParserId = XmlRichTextParser.Id;
            producer.content = Content;
            producer.margin = margin;
            producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            producer.textStyle.size = 30f;
            if (invalid)
            {
                producer.paragraphStyle.textDirection = TextDirection.Rtl;
                producer.paragraphStyle.textAlign = TextAlign.Left;
                producer.textStyle.lineHeight = float.NaN;
                producer.textOverflow = TextOverflow.Clip;
            }
            else
            {
                producer.paragraphStyle.textDirection = TextDirection.Ltr;
                producer.paragraphStyle.textAlign = TextAlign.Start;
                producer.textStyle.lineHeight = 0f;
                producer.textOverflow = TextOverflow.Clip;
            }

            return new ProbeCase
            {
                producer = producer,
                textBox = child.AddComponent<TextBox>(),
                rect = child.GetComponent<RectTransform>(),
            };
        }

        private static ProbeCase CreateContentSizingCase(GameObject root)
        {
            var containingBlock = new GameObject("Content sizing containing block",
                typeof(RectTransform));
            containingBlock.transform.SetParent(root.transform, false);
            var containingBlockRect = containingBlock.GetComponent<RectTransform>();
            containingBlockRect.sizeDelta = new Vector2(380f, 150f);

            var child = new GameObject("Vertical TextBox legal content",
                typeof(RectTransform));
            child.transform.SetParent(containingBlock.transform, false);
            var rect = child.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 120f);

            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            producer.richTextParserId = XmlRichTextParser.Id;
            producer.content = Content;
            producer.margin = new Margin
            {
                Left = new MarginValue { Value = 6f, Unit = MarginUnit.Px },
                Top = new MarginValue { Value = 0.2f, Unit = MarginUnit.Em },
                Right = new MarginValue { Value = 2.5f, Unit = MarginUnit.ContainerPercent },
                Bottom = new MarginValue { Value = 1f, Unit = MarginUnit.Vh },
            };
            producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            producer.paragraphStyle.textDirection = TextDirection.Ltr;
            producer.paragraphStyle.textAlign = TextAlign.Start;
            producer.textStyle.size = 30f;
            producer.textStyle.lineHeight = 0f;
            producer.textOverflow = TextOverflow.Clip;
            var textBox = child.AddComponent<TextBox>();
            var layout = child.AddComponent<TextBoxLayoutElement>();
            layout.widthMode = TextWidthMode.FitContent;
            layout.heightMode = TextHeightMode.Fixed;
            var fitter = child.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            return new ProbeCase
            {
                producer = producer,
                textBox = textBox,
                layout = layout,
                rect = rect,
            };
        }

        private static ProbeCase CreateAutoHeightCase(GameObject root)
        {
            var child = new GameObject("Vertical TextBox auto height",
                typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            var rect = child.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180f, 120f);
            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            producer.richTextParserId = XmlRichTextParser.Id;
            producer.content = Content;
            producer.margin = new Margin();
            producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            producer.paragraphStyle.textDirection = TextDirection.Ltr;
            producer.paragraphStyle.textAlign = TextAlign.Start;
            producer.textStyle.size = 30f;
            producer.textStyle.lineHeight = 0f;
            producer.textOverflow = TextOverflow.Clip;
            var textBox = child.AddComponent<TextBox>();
            var layout = child.AddComponent<TextBoxLayoutElement>();
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Auto;
            return new ProbeCase
            {
                producer = producer,
                textBox = textBox,
                layout = layout,
                rect = rect,
            };
        }

        private static ProbeCase CreateIndentCase(GameObject root)
        {
            var child = new GameObject("Vertical TextBox indent",
                typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            var rect = child.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 100f);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(420f, -140f);

            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            producer.richTextParserId = XmlRichTextParser.Id;
            producer.content = IndentContent;
            producer.margin = new Margin();
            producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            producer.paragraphStyle.textDirection = TextDirection.Ltr;
            producer.paragraphStyle.textAlign = TextAlign.Start;
            producer.textStyle.size = 30f;
            producer.textStyle.lineHeight = 0f;
            producer.textOverflow = TextOverflow.Clip;
            return new ProbeCase
            {
                producer = producer,
                textBox = child.AddComponent<TextBox>(),
                rect = rect,
            };
        }

        private static ProbeCase CreateJustifyCase(GameObject root)
        {
            var child = new GameObject("Vertical TextBox justify",
                typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            var rect = child.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 100f);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -280f);

            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            producer.richTextParserId = XmlRichTextParser.Id;
            producer.content = JustifyContent;
            producer.margin = new Margin();
            producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            producer.paragraphStyle.textDirection = TextDirection.Ltr;
            producer.paragraphStyle.textAlign = TextAlign.Justify;
            producer.textStyle.size = 30f;
            producer.textStyle.lineHeight = 0f;
            producer.textOverflow = TextOverflow.Clip;
            return new ProbeCase
            {
                producer = producer,
                textBox = child.AddComponent<TextBox>(),
                rect = rect,
            };
        }

        private static ProbeCase CreateEllipsisCase(GameObject root)
        {
            var child = new GameObject("Vertical TextBox ellipsis",
                typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            var rect = child.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 100f);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(220f, -420f);

            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            producer.richTextParserId = XmlRichTextParser.Id;
            producer.content = EllipsisContent;
            producer.margin = new Margin
            {
                Left = new MarginValue { Value = 10f },
                Top = new MarginValue { Value = 10f },
                Right = new MarginValue { Value = 10f },
                Bottom = new MarginValue { Value = 10f },
            };
            producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            producer.paragraphStyle.textDirection = TextDirection.Ltr;
            producer.paragraphStyle.textAlign = TextAlign.Start;
            producer.paragraphStyle.singleLine = true;
            producer.textStyle.size = 30f;
            producer.textStyle.lineHeight = 0f;
            producer.textOverflow = TextOverflow.Ellipsis;
            return new ProbeCase
            {
                producer = producer,
                textBox = child.AddComponent<TextBox>(),
                rect = rect,
            };
        }

        private static ProbeCase CreateDecorationCase(GameObject root)
        {
            var child = new GameObject("Vertical TextBox decoration",
                typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            var rect = child.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 200f);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            producer.richTextParserId = XmlRichTextParser.Id;
            producer.content = DecorationContent;
            producer.margin = new Margin
            {
                Left = new MarginValue { Value = 10f },
                Top = new MarginValue { Value = 10f },
                Right = new MarginValue { Value = 10f },
                Bottom = new MarginValue { Value = 10f },
            };
            producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            producer.paragraphStyle.textDirection = TextDirection.Ltr;
            producer.paragraphStyle.textAlign = TextAlign.Start;
            producer.textStyle.size = 30f;
            producer.textStyle.lineHeight = 0f;
            producer.textOverflow = TextOverflow.Clip;
            var textBox = child.AddComponent<TextBox>();
            textBox.linkHoverColor = Color.clear;
            textBox.linkHoverUnderlineColor = Color.red;
            textBox.linkHoverUnderlineThickness = 4f;
            return new ProbeCase
            {
                producer = producer,
                textBox = textBox,
                rect = rect,
            };
        }

        public static bool EvaluateBaselineShiftOutput(TextBox textBox,
            TextBoxRenderTextureProducer producer, bool vertical, out float[] offsets)
        {
            offsets = Array.Empty<float>();
            if (!producer.HasOutput || textBox.Texture == null ||
                producer.isVerticalLayout != vertical || producer.lastLayoutError != null ||
                !producer.TryGetLinkGeometrySnapshot(out var geometry, out _) ||
                !TryGetVisibleLinkPoint(geometry, out var point) ||
                !producer.TryHitLink(point, out var hit) || hit.Href != "baseline-link")
            {
                return false;
            }
            var readback = ReadOutput(textBox, new Vector2Int(producer.OutputWidth, producer.OutputHeight));
            try
            {
                var pixels = readback.GetPixels32();
                var predicates = new Func<byte, byte, byte, bool>[]
                {
                    (red, green, blue) => red > 180 && green < 80 && blue < 80,
                    (red, green, blue) => green > 180 && red < 80 && blue < 80,
                    (red, green, blue) => blue > 180 && red < 80 && green < 80,
                    (red, green, blue) => green > 180 && blue > 180 && red < 80,
                    (red, green, blue) => red > 180 && blue > 180 && green < 80,
                };
                var centers = new float[predicates.Length];
                for (var index = 0; index < predicates.Length; ++index)
                {
                    if (!TryGetColorBounds(pixels, readback.width, readback.height, predicates[index],
                            out var bounds)) return false;
                    centers[index] = vertical ? bounds.center.x : bounds.center.y;
                }
                offsets = new float[centers.Length - 1];
                var expected = new[] { 15f, -7.5f, 9f, -12f };
                for (var index = 0; index < offsets.Length; ++index)
                {
                    offsets[index] = centers[index + 1] - centers[0];
                    if (Mathf.Abs(offsets[index] - expected[index]) > 1.5f) return false;
                }
                return true;
            }
            finally
            {
                UnityEngine.Object.Destroy(readback);
            }
        }

        private static bool EvaluateCase(ProbeCase probeCase,
            out float paintX,
            out float paintY)
        {
            paintX = 0f;
            paintY = 0f;
            var producer = probeCase.producer;
            var hasContent = producer.TryGetContentSnapshot(out var content);
            var hasGeometry = producer.TryGetLinkGeometrySnapshot(out var geometry, out _);
            var linkHit = hasGeometry &&
                          TryGetVisibleLinkPoint(geometry, out var point) &&
                          producer.TryHitLink(point, out var hit) &&
                          hit.Href == "player-link";
            if (hasGeometry)
            {
                paintX = geometry.PaintPosition.x;
                paintY = geometry.PaintPosition.y;
            }

            return producer.HasOutput &&
                   probeCase.textBox.Texture != null &&
                   producer.isVerticalLayout &&
                   producer.contentWidth > 0f &&
                   hasContent &&
                   content.PlainText == "「漢字ABC2026かな」、。ー" &&
                   hasGeometry &&
                   linkHit &&
                   producer.lastLayoutError == null;
        }

        private static bool EvaluateContentSizingCase(ProbeCase probeCase)
        {
            var producer = probeCase.producer;
            var layout = probeCase.layout;
            var rect = probeCase.rect;
            if (layout == null || rect == null || !probeCase.Ready())
            {
                return false;
            }

            var containingBlock = rect.parent as RectTransform;
            var canvas = producer.GetComponentInParent<Canvas>();
            var rootRect = canvas != null && canvas.rootCanvas != null
                ? canvas.rootCanvas.transform as RectTransform
                : null;
            if (containingBlock == null || rootRect == null)
            {
                return false;
            }

            var sizing = producer.ResolveSizing();
            var expectedRight = containingBlock.rect.width * 0.025f;
            var expectedTop = producer.textStyle.size * 0.2f;
            var expectedBottom = rootRect.rect.height * 0.01f;
            var expectedMx = 6f + expectedRight;
            var expectedMy = expectedTop + expectedBottom;
            var expectedContentHeight = Mathf.Max(0f, 120f - expectedMy);
            var initialWidth = rect.rect.width;
            for (var pass = 0; pass < 5; ++pass)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Canvas.ForceUpdateCanvases();
                if (!Mathf.Approximately(rect.rect.width, initialWidth))
                {
                    return false;
                }
            }

            var hasContent = producer.TryGetContentSnapshot(out var content);
            var hasGeometry = producer.TryGetLinkGeometrySnapshot(out var geometry, out _);
            var linkHit = hasGeometry &&
                          TryGetVisibleLinkPoint(geometry, out var point) &&
                          producer.TryHitLink(point, out var hit) &&
                          hit.Href == "player-link";
            return producer.HasOutput &&
                   probeCase.textBox.Texture != null &&
                   producer.isVerticalLayout &&
                   producer.lastLayoutError == null &&
                   layout.preferredHeight == -1f &&
                   Mathf.Abs(layout.preferredWidth - sizing.PreferredWidth) <= 0.5f &&
                   Mathf.Abs(rect.rect.width - sizing.PreferredWidth) <= 0.5f &&
                   Mathf.Abs(rect.rect.height - 120f) <= 0.01f &&
                   Mathf.Abs(sizing.Margin.Left - 6f) <= 0.01f &&
                   Mathf.Abs(sizing.Margin.Right - expectedRight) <= 0.01f &&
                   Mathf.Abs(sizing.Margin.Top - expectedTop) <= 0.01f &&
                   Mathf.Abs(sizing.Margin.Bottom - expectedBottom) <= 0.01f &&
                   Mathf.Abs(sizing.ContentHeight - expectedContentHeight) <= 0.01f &&
                   Mathf.Abs(sizing.PreferredWidth -
                             (sizing.ContentWidth + expectedMx)) <= 0.01f &&
                   Mathf.Abs(producer.contentWidth - sizing.ContentWidth) <= 0.01f &&
                   Mathf.Abs(producer.contentHeight - expectedContentHeight) <= 0.01f &&
                   Mathf.Abs(producer.viewportWidth -
                             (rect.rect.width - expectedMx)) <= 0.5f &&
                   Mathf.Abs(producer.viewportHeight - expectedContentHeight) <= 0.01f &&
                   hasContent &&
                   hasGeometry &&
                   linkHit;
        }

        private static bool EvaluateAutoHeightCase(ProbeCase probeCase)
        {
            var producer = probeCase.producer;
            if (probeCase.layout == null || !probeCase.Ready())
            {
                return false;
            }

            return producer.HasOutput &&
                   probeCase.textBox.Texture != null &&
                   producer.isVerticalLayout &&
                   probeCase.layout.preferredWidth == -1f &&
                   probeCase.layout.preferredHeight == -1f &&
                   producer.lastLayoutError == null;
        }

        private static bool EvaluateIndentCase(ProbeCase probeCase,
            out float firstRectYMin,
            out float secondRectYMin,
            out int linkRectCount,
            out bool linkHit)
        {
            firstRectYMin = 0f;
            secondRectYMin = 0f;
            linkRectCount = 0;
            linkHit = false;
            var producer = probeCase.producer;
            if (!probeCase.Ready() ||
                !producer.TryGetLinkGeometrySnapshot(out var geometry, out _) ||
                geometry.Geometry.Count != 1 ||
                geometry.Geometry[0].Rects.Count < 2)
            {
                return false;
            }

            var rects = geometry.Geometry[0].Rects;
            linkRectCount = rects.Count;
            firstRectYMin = rects[0].yMin;
            secondRectYMin = rects[1].yMin;
            linkHit = TryGetVisibleLinkPoint(geometry, out var point) &&
                      producer.TryHitLink(point, out var hit) &&
                      hit.Href == "indent-link";
            return producer.HasOutput &&
                   probeCase.textBox.Texture != null &&
                   producer.isVerticalLayout &&
                   producer.lastLayoutError == null &&
                   Mathf.Abs(firstRectYMin - 40f) <= 0.5f &&
                   Mathf.Abs(secondRectYMin) <= 0.5f &&
                   linkHit;
        }

        private static bool EvaluateJustifyCase(ProbeCase probeCase,
            out float firstRectYMin,
            out float firstRectHeight,
            out float lastRectHeight,
            out int linkRectCount,
            out bool linkHit)
        {
            firstRectYMin = 0f;
            firstRectHeight = 0f;
            lastRectHeight = 0f;
            linkRectCount = 0;
            linkHit = false;
            var producer = probeCase.producer;
            if (!probeCase.Ready() ||
                !producer.TryGetLinkGeometrySnapshot(out var geometry, out _) ||
                geometry.Geometry.Count != 1 ||
                geometry.Geometry[0].Rects.Count < 4)
            {
                return false;
            }

            var rects = geometry.Geometry[0].Rects;
            linkRectCount = rects.Count;
            firstRectYMin = rects[0].yMin;
            firstRectHeight = rects[0].height;
            lastRectHeight = rects[rects.Count - 1].height;
            linkHit = TryGetVisibleLinkPoint(geometry, out var point) &&
                      producer.TryHitLink(point, out var hit) &&
                      hit.Href == "justify-link";
            return producer.HasOutput &&
                   probeCase.textBox.Texture != null &&
                   producer.isVerticalLayout &&
                   producer.lastLayoutError == null &&
                   Mathf.Abs(firstRectYMin) <= 0.5f &&
                   Mathf.Abs(firstRectHeight - producer.viewportHeight) <= 1f &&
                   lastRectHeight < firstRectHeight - 1f &&
                   linkHit;
        }

        private static bool EvaluateEllipsisCase(ProbeCase probeCase,
            out ulong utf16End,
            out float inlineExtent,
            out float physicalHeight,
            out int linkRectCount,
            out bool hiddenRangeEmpty,
            out bool linkHit)
        {
            utf16End = 0;
            inlineExtent = 0f;
            physicalHeight = 0f;
            linkRectCount = 0;
            hiddenRangeEmpty = false;
            linkHit = false;
            var producer = probeCase.producer;
            if (!probeCase.Ready() ||
                !producer.TryGetLinkGeometrySnapshot(out var geometry, out _) ||
                geometry.Paragraphs.Count != 1 ||
                geometry.Geometry.Count != 1)
            {
                return false;
            }

            var metrics = geometry.Paragraphs[0].LayoutMetrics;
            if (metrics.Columns.Count != 1)
            {
                return false;
            }

            utf16End = metrics.Columns[0].Utf16End;
            inlineExtent = metrics.Columns[0].InlineExtent;
            physicalHeight = metrics.PhysicalSize.y;
            linkRectCount = geometry.Geometry[0].Rects.Count;
            hiddenRangeEmpty = utf16End < (ulong)EllipsisPlainText.Length &&
                               geometry.Paragraphs[0].GetRectsForRange((int)utf16End,
                                   EllipsisPlainText.Length).Count == 0;
            linkHit = TryGetVisibleLinkPoint(geometry, out var point) &&
                      producer.TryHitLink(point, out var hit) &&
                      hit.Href == "ellipsis-link";
            return producer.HasOutput &&
                   probeCase.textBox.Texture != null &&
                   producer.isVerticalLayout &&
                   producer.lastLayoutError == null &&
                   Mathf.Abs(producer.viewportHeight - 80f) <= 0.5f &&
                   Mathf.Abs(producer.contentHeight - 80f) <= 0.5f &&
                   utf16End > 0 &&
                   utf16End < (ulong)EllipsisPlainText.Length &&
                   inlineExtent <= 80.05f &&
                   Mathf.Abs(physicalHeight - 80f) <= 0.05f &&
                   linkRectCount > 0 &&
                   hiddenRangeEmpty &&
                   linkHit;
        }

        private static bool EvaluateDecorationCase(ProbeCase probeCase,
            out float underlineX,
            out float throughX,
            out float throughOffset,
            out bool linkHit,
            out bool hoverVertical,
            out bool hoverPassed,
            out bool outsideSourceRejected,
            out string hoverDiagnostic)
        {
            underlineX = 0f;
            throughX = 0f;
            throughOffset = 0f;
            linkHit = false;
            hoverVertical = false;
            hoverPassed = false;
            outsideSourceRejected = false;
            hoverDiagnostic = "";
            var producer = probeCase.producer;
            if (!probeCase.Ready() ||
                !producer.TryGetLinkGeometrySnapshot(out var geometry, out _) ||
                geometry.Geometry.Count != 1 ||
                geometry.Geometry[0].Rects.Count != 1 ||
                producer.OutputWidth <= 0 || producer.OutputHeight <= 0)
            {
                return false;
            }

            var linkRect = geometry.Geometry[0].Rects[0];
            var scale = geometry.FitScale;
            var linkCenterX = linkRect.center.x * scale + geometry.PaintPosition.x;
            var readback = ReadOutput(probeCase.textBox,
                new Vector2Int(producer.OutputWidth, producer.OutputHeight));
            try
            {
                var pixels = readback.GetPixels32();
                if (!TryGetColorBounds(pixels, readback.width, readback.height,
                        (red, green, blue) => red > 180 && red > green + 80 && red > blue + 80,
                        out var underlineBounds) ||
                    !TryGetColorBounds(pixels, readback.width, readback.height,
                        (red, green, blue) => green > 180 && green > red + 80 && green > blue + 80,
                        out var throughBounds))
                {
                    return false;
                }

                underlineX = (underlineBounds.xMin + underlineBounds.xMax + 1f) * 0.5f;
                throughX = (throughBounds.xMin + throughBounds.xMax + 1f) * 0.5f;
                throughOffset = throughX - linkCenterX;
            }
            finally
            {
                UnityEngine.Object.Destroy(readback);
            }

            linkHit = TryGetVisibleLinkPoint(geometry, out var point) &&
                      producer.TryHitLink(point, out var hit) &&
                      hit.Href == "decoration-link";
            var outsidePoint = new Vector2(
                linkRect.xMin * scale + geometry.PaintPosition.x - 6f,
                linkRect.center.y * scale + geometry.PaintPosition.y);
            var outsideNormalized = new Vector2(
                outsidePoint.x / geometry.VisibleOutputSize.x,
                outsidePoint.y / geometry.VisibleOutputSize.y);
            outsideSourceRejected = geometry.ClipRect.Contains(outsidePoint) &&
                                    outsideNormalized.x >= 0f && outsideNormalized.x <= 1f &&
                                    outsideNormalized.y >= 0f && outsideNormalized.y <= 1f &&
                                    !producer.TryHitLink(outsideNormalized, out _);

            var localRects = new List<Rect>();
            var hoverReady = probeCase.textBox.TryGetContentSnapshot(out var content) &&
                             probeCase.textBox.TryGetLinkLocalRects(content.ContentVersion, 0,
                                 localRects) &&
                             localRects.Count > 0;
            if (hoverReady)
            {
                var pointer = new PointerEventData(null)
                {
                    pointerId = -1,
                    button = PointerEventData.InputButton.Left,
                    eligibleForClick = true,
                    position = RectTransformUtility.WorldToScreenPoint(null,
                        probeCase.textBox.rectTransform.TransformPoint(localRects[0].center)),
                };
                pointer.pointerCurrentRaycast = new RaycastResult
                {
                    gameObject = probeCase.textBox.gameObject,
                };
                probeCase.textBox.OnPointerEnter(pointer);
                var indicator = probeCase.textBox
                    .GetComponentInChildren<TextBoxLinkHoverIndicator>(true);
                if (indicator != null)
                {
                    hoverVertical = (bool)typeof(TextBoxLinkHoverIndicator)
                        .GetField("vertical", DeclaredInstance)!
                        .GetValue(indicator);
                    var helper = new VertexHelper();
                    var populate = typeof(TextBoxLinkHoverIndicator).GetMethod(
                        "OnPopulateMesh", DeclaredInstance);
                    populate!.Invoke(indicator, new object[] { helper });
                    var vertices = new List<UIVertex>();
                    helper.GetUIVertexStream(vertices);
                    var minimum = vertices.Count > 0 ? vertices[0].position : Vector3.zero;
                    var maximum = minimum;
                    for (var index = 1; index < vertices.Count; ++index)
                    {
                        minimum = Vector3.Min(minimum, vertices[index].position);
                        maximum = Vector3.Max(maximum, vertices[index].position);
                    }

                    hoverDiagnostic = string.Format(
                        "enabled={0} count={1} vertex=({2:0.###},{3:0.###})-({4:0.###},{5:0.###}) " +
                        "rect=({6:0.###},{7:0.###})-({8:0.###},{9:0.###})",
                        indicator.enabled, vertices.Count,
                        minimum.x, minimum.y, maximum.x, maximum.y,
                        localRects[0].xMin, localRects[0].yMin,
                        localRects[0].xMax, localRects[0].yMax);
                    if (indicator.enabled && vertices.Count == localRects.Count * 6)
                    {
                        hoverPassed = hoverVertical &&
                                      Mathf.Abs(minimum.x - localRects[0].xMin) <= 0.1f &&
                                      Mathf.Abs(maximum.x -
                                                Mathf.Min(localRects[0].xMax,
                                                    localRects[0].xMin + 4f)) <= 0.1f &&
                                      Mathf.Abs(minimum.y - localRects[0].yMin) <= 0.1f &&
                                      Mathf.Abs(maximum.y - localRects[0].yMax) <= 0.1f;
                    }
                }
            }

            return producer.HasOutput &&
                   probeCase.textBox.Texture != null &&
                   producer.isVerticalLayout &&
                   producer.lastLayoutError == null &&
                   underlineX < throughX - 2f &&
                   Mathf.Abs(throughOffset) <= 1.5f &&
                   linkHit &&
                   outsideSourceRejected &&
                   hoverVertical &&
                   hoverPassed;
        }

        private static bool TryGetColorBounds(Color32[] pixels,
            int width,
            int height,
            Func<byte, byte, byte, bool> predicate,
            out RectInt bounds)
        {
            var minX = width;
            var minY = height;
            var maxX = -1;
            var maxY = -1;
            for (var y = 0; y < height; ++y)
            {
                for (var x = 0; x < width; ++x)
                {
                    var pixel = pixels[y * width + x];
                    if (pixel.a <= 16 || !predicate(pixel.r, pixel.g, pixel.b))
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                bounds = default;
                return false;
            }

            bounds = new RectInt(minX, minY, maxX + 1 - minX, maxY + 1 - minY);
            return true;
        }

        private static Texture2D ReadOutput(TextBox textBox, Vector2Int size)
        {
            var temporary = RenderTexture.GetTemporary(size.x, size.y, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(textBox.Texture, temporary, textBox.UvRect.size,
                    textBox.UvRect.position);
                RenderTexture.active = temporary;
                var readback = new Texture2D(size.x, size.y, TextureFormat.RGBA32,
                    false, true);
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

        private void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= HandleLog;
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            logs.Add(type + ": " + condition);
        }

        private int CountConfigurationLogs()
        {
            var count = 0;
            foreach (var entry in logs)
            {
                if (entry.Contains("Canvas TextBox configuration", StringComparison.Ordinal) ||
                    entry.Contains("textDirection=Rtl defaulted", StringComparison.Ordinal) ||
                    entry.Contains("textAlign=Left defaulted", StringComparison.Ordinal) ||
                    entry.Contains("lineHeight=NaN defaulted", StringComparison.Ordinal) ||
                    entry.Contains("margin.left=auto defaulted", StringComparison.Ordinal) ||
                    entry.Contains("textOverflow=Ellipsis defaulted", StringComparison.Ordinal))
                {
                    ++count;
                }
            }

            return count;
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
                    if (visibleRect.width < 1f || visibleRect.height < 1f)
                    {
                        continue;
                    }

                    normalizedPoint = new Vector2(visibleRect.center.x / snapshot.VisibleOutputSize.x,
                        visibleRect.center.y / snapshot.VisibleOutputSize.y);
                    return true;
                }
            }

            return false;
        }

        private static bool HasArgument(string name)
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, name, StringComparison.Ordinal))
                {
                    return true;
                }
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

        private sealed class ProbeCase
        {
            public TextBoxRenderTextureProducer producer = null;
            public TextBox textBox = null;
            public TextBoxLayoutElement layout = null;
            public RectTransform rect = null;

            public bool Ready()
            {
                return producer != null &&
                       textBox != null &&
                       producer.HasOutput &&
                       textBox.Texture != null &&
                       producer.TryGetLinkGeometrySnapshot(out _, out _);
            }
        }

        [Serializable]
        private sealed class ProbeResult
        {
            public bool RubyPassed;
            public int RubyAnnotationPixels;
            public bool RubyLinkHit;
            public bool RubyAutoFontPassed;
            public int RubyAutoFontPixels;
            public bool AutoFontSizePassed;
            public float[] AutoFontScales = new float[3];
            public RectInt[] AutoFontPixelBounds = new RectInt[3];
            public bool PlaceholderPassed;
            public Rect PlaceholderRect;
            public string PlaceholderKey = "";
            public int PlaceholderStartUtf16;
            public bool PlaceholderVisible;
            public bool Passed;
            public bool LegalAutoPassed;
            public bool InvalidConfigPassed;
            public bool LegalContentPassed;
            public bool AutoHeightPassed;
            public bool IndentPassed;
            public bool JustifyPassed;
            public bool EllipsisPassed;
            public bool DecorationPassed;
            public bool BaselineShiftPassed;
            public bool MultiBlockPassed;
            public bool BlockMarginPassed;
            public float[] BlockMarginFrameY = Array.Empty<float>();
            public float BlockMarginHeight;
            public float BlockMarginScrollY;
            public bool MultiBlockInitialPassed;
            public bool MultiBlockScrolledPassed;
            public float MultiBlockWidth;
            public float[] MultiBlockFrameX = Array.Empty<float>();
            public float MultiBlockInitialScroll;
            public float MultiBlockFinalScroll;
            public float[] BaselineShiftOffsets = Array.Empty<float>();
            public bool HorizontalBaselineShiftPassed;
            public float[] HorizontalBaselineShiftOffsets = Array.Empty<float>();
            public bool HasOutput;
            public bool HasTexture;
            public bool IsVertical;
            public float ContentWidth;
            public float LegalPaintX;
            public float LegalPaintY;
            public float IndentFirstRectYMin;
            public float IndentSecondRectYMin;
            public int IndentLinkRectCount;
            public bool IndentLinkHit;
            public float JustifyFirstRectYMin;
            public float JustifyFirstRectHeight;
            public float JustifyLastRectHeight;
            public int JustifyLinkRectCount;
            public bool JustifyLinkHit;
            public ulong EllipsisUtf16End;
            public float EllipsisInlineExtent;
            public float EllipsisPhysicalHeight;
            public int EllipsisLinkRectCount;
            public bool EllipsisHiddenRangeEmpty;
            public bool EllipsisLinkHit;
            public float DecorationUnderlineX;
            public float DecorationThroughX;
            public float DecorationThroughOffset;
            public bool DecorationLinkHit;
            public bool DecorationHoverVertical;
            public bool DecorationHoverPassed;
            public bool DecorationOutsideSourceRejected;
            public string DecorationHoverDiagnostic = "";
            public bool HasContentSnapshot;
            public string PlainText = "";
            public bool HasLinkGeometry;
            public bool LinkHit;
            public string LastLayoutError = "";
            public int ConfigLogCount;
            public bool ScreenshotWritten;
            public string ScreenshotPath = "";
            public string Exception = "";
        }
    }
}

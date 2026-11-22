using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Configuration;
using Milestro.Model;
using Milestro.Skia;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Canvas = UnityEngine.Canvas;
using IOPath = System.IO.Path;
using Object = UnityEngine.Object;

namespace Milestro.Tests
{
    public class TextGlyphMeshImageComparisonTests
    {
        private const string FixtureFamily = "LXGW WenKai";
        private const int ImageWidth = 384;
        private const int ImageHeight = 192;
        private const int CoverageThreshold = 16;
        private const int MinimumLitPixels = 32;
        private const float MaximumBoundsDifference = 1f;
        private const float MaximumCentroidDifference = 0.5f;
        private const float MaximumCoverageDifference = 0.10f;
        private const float MaximumForegroundError = 0.10f;
        private const float MaximumLowPassError = 0.04f;
        private const float MaximumOutsideMask = 2f / 255f;

        [UnityTest]
        public IEnumerator SlimIntegerPixelsMatchTextureCoverage()
        {
            yield return Compare(new Sample("slim-integer", false, 24f, 1f, Vector2.zero,
                "0123456789 AV office"));
        }

        [UnityTest]
        public IEnumerator SlimSmallFractionalCanvasScaleMatchesTextureCoverage()
        {
            yield return Compare(new Sample("slim-small-fractional", false, 13f, 1.25f,
                new Vector2(0.35f, 0.65f), "0123456789 AV office"));
        }

        [UnityTest]
        public IEnumerator SlimHardRectMaskMatchesTextureCoverageAtEdges()
        {
            yield return Compare(new Sample("slim-hard-mask", false, 24f, 1.25f,
                new Vector2(0.35f, 0.65f), "88888888888888888888", masked: true));
        }

        [UnityTest]
        public IEnumerator ParagraphIntegerPixelsMatchTextureCoverage()
        {
            yield return Compare(new Sample("paragraph-integer", true, 24f, 1f, Vector2.zero,
                "0123456789 AV office"));
        }

        [UnityTest]
        public IEnumerator ParagraphSmallFractionalCanvasScaleMatchesTextureCoverage()
        {
            yield return Compare(new Sample("paragraph-small-fractional", true, 13f, 1.25f,
                new Vector2(0.35f, 0.65f), "0123456789 AV office"));
        }

        [UnityTest]
        public IEnumerator ParagraphMultilineFamilyFallbackMatchesTextureCoverage()
        {
            yield return Compare(new Sample("paragraph-multiline-family-fallback", true, 18f, 1.25f,
                new Vector2(0.35f, 0.65f), "office AV 中文混排\n第二行 0123456789\n第三行，标点。",
                familyFallback: true));
        }

        [UnityTest]
        public IEnumerator ParagraphHardRectMaskMatchesTextureCoverageAtEdges()
        {
            yield return Compare(new Sample("paragraph-hard-mask", true, 24f, 1.25f,
                new Vector2(0.35f, 0.65f), "88888888888888888888\n88888888888888888888", masked: true));
        }

        [UnityTest]
        public IEnumerator SlimColoredSourceFallsBackAndMatchesTextureCoverage()
        {
            yield return Compare(new Sample("slim-colored", false, 24f, 1f, Vector2.zero,
                "0123456789 AV office"), Color.red, expectGlyphMesh: false);
        }

        [UnityTest]
        public IEnumerator ParagraphColoredSourceFallsBackAndMatchesTextureCoverage()
        {
            yield return Compare(new Sample("paragraph-colored", true, 24f, 1f, Vector2.zero,
                "0123456789 AV office"), Color.red, expectGlyphMesh: false);
        }

        [UnityTest]
        public IEnumerator SlimTranslucentColoredSourceFallsBackOverColoredBackground()
        {
            yield return Compare(new Sample("slim-colored-translucent", false, 24f, 1f, Vector2.zero,
                "0123456789 AV office"), new Color(1f, 0f, 0f, 0.5f), new Color(0f, 0.2f, 0.3f, 0.6f), false);
        }

        [UnityTest]
        public IEnumerator ParagraphTranslucentColoredSourceFallsBackOverColoredBackground()
        {
            yield return Compare(new Sample("paragraph-colored-translucent", true, 24f, 1f, Vector2.zero,
                "0123456789 AV office"), new Color(1f, 0f, 0f, 0.5f), new Color(0f, 0.2f, 0.3f, 0.6f), false);
        }

        [UnityTest]
        public IEnumerator SlimTextureOpacityCompositesOnceWithTintAndCanvasGroup()
        {
            yield return CheckOpacity(false, false);
        }

        [UnityTest]
        public IEnumerator ParagraphTextureOpacityCompositesOnceWithTintAndCanvasGroup()
        {
            yield return CheckOpacity(true, false);
        }

        [UnityTest]
        public IEnumerator SlimGlyphOpacityCompositesOnceOverColoredBackground()
        {
            yield return CheckOpacity(false, true);
        }

        [UnityTest]
        public IEnumerator ParagraphGlyphOpacityCompositesOnceOverColoredBackground()
        {
            yield return CheckOpacity(true, true);
        }

        private static IEnumerator CheckOpacity(bool paragraph, bool glyphMesh)
        {
            var expectedMesh = glyphMesh && QualitySettings.activeColorSpace == ColorSpace.Gamma;
            TestContext.Out.WriteLine("Requested glyph mesh=" + glyphMesh + ", actual expected output=" +
                (expectedMesh ? "glyph-mesh" : "texture") + ", color space=" + QualitySettings.activeColorSpace);
            FontRegistry.RegisterFontFromPath(FindFixtureFont());
            var previousConfiguration = MilestroConfiguration.Configuration.RenderSurface;
            MilestroConfiguration.Configuration.RenderSurface = new RenderSurfaceConfiguration
            { EnableTextGlyphMesh = glyphMesh };
            Fixture fixture = null;
            Texture2D reference = null;
            Texture2D actual = null;
            Texture2D background = null;
            try
            {
                fixture = new Fixture(new Sample("alpha-composition", paragraph, 24f, 1f,
                    Vector2.zero, "888888"));
                fixture.SetBackground(Color.clear);
                var foreground = glyphMesh ? Color.white : new Color(0.8f, 0.4f, 0.2f, 1f);
                fixture.SetTextColor(foreground);
                yield return fixture.WaitForOutput(expectedMesh);
                reference = fixture.Readback();
                fixture.SetBackground(new Color(0.1f, 0.2f, 0.3f, 0.6f));
                fixture.SetVisible(false);
                background = fixture.Readback();
                fixture.SetVisible(true);
                foreground.a = 0.5f;
                fixture.SetTextColor(foreground);
                var opacity = 0.5f;
                if (!glyphMesh)
                {
                    fixture.SetPresentationOpacity(0.5f, 0.5f);
                    opacity *= 0.25f;
                }
                yield return fixture.WaitForOutput(expectedMesh);
                actual = fixture.Readback();
                var referencePixels = reference.GetPixels32();
                var actualPixels = actual.GetPixels32();
                var backgroundPixels = background.GetPixels32();
                var compared = 0;
                var maximumError = 0f;
                for (var index = 0; index < referencePixels.Length; ++index)
                {
                    Color source = referencePixels[index];
                    if (source.a < 0.1f) continue;
                    Color backdrop = backgroundPixels[index];
                    Color result = actualPixels[index];
                    var remaining = 1f - source.a * opacity;
                    var expected = source * opacity + backdrop * remaining;
                    maximumError = Mathf.Max(maximumError, Mathf.Abs(expected.r - result.r),
                        Mathf.Abs(expected.g - result.g), Mathf.Abs(expected.b - result.b),
                        Mathf.Abs(expected.a - result.a));
                    ++compared;
                }
                Assert.That(compared, Is.GreaterThanOrEqualTo(MinimumLitPixels));
                Assert.That(maximumError, Is.LessThanOrEqualTo(4f / 255f),
                    "Source opacity, graphic tint and inherited opacity must each apply exactly once to premultiplied RGB and alpha.");
            }
            finally
            {
                if (reference != null) Object.Destroy(reference);
                if (actual != null) Object.Destroy(actual);
                if (background != null) Object.Destroy(background);
                fixture?.Dispose();
                MilestroConfiguration.Configuration.RenderSurface = previousConfiguration;
            }
        }

        private static IEnumerator Compare(Sample sample, Color? textColor = null, Color? backgroundColor = null,
            bool expectGlyphMesh = true)
        {
            var linearFallback = QualitySettings.activeColorSpace != ColorSpace.Gamma;
            expectGlyphMesh &= !linearFallback;
            var fontPath = FindFixtureFont();
            FontRegistry.RegisterFontFromPath(fontPath);
            Assert.That(FontRegistry.GetRegisteredFontFamilyNames(), Does.Contain(FixtureFamily));
            var previousConfiguration = MilestroConfiguration.Configuration.RenderSurface;
            var configuration = new RenderSurfaceConfiguration { EnableTextGlyphMesh = false };
            MilestroConfiguration.Configuration.RenderSurface = configuration;
            Fixture fixture = null;
            Texture2D baseline = null;
            Texture2D candidate = null;
            Texture2D difference = null;
            try
            {
                fixture = new Fixture(sample);
                if (textColor.HasValue) fixture.SetTextColor(textColor.Value);
                if (backgroundColor.HasValue) fixture.SetBackground(backgroundColor.Value);
                yield return fixture.WaitForOutput(false);
                baseline = fixture.Readback();
                var rasterized = TextGlyphAtlas.RasterizedGlyphs;
                var uploads = TextGlyphAtlas.Uploads;
                configuration.EnableTextGlyphMesh = true;
                fixture.Rebuild(true);
                yield return fixture.WaitForOutput(expectGlyphMesh);
                if (!expectGlyphMesh)
                {
                    Assert.That(TextGlyphAtlas.RasterizedGlyphs, Is.EqualTo(rasterized));
                    Assert.That(TextGlyphAtlas.Uploads, Is.EqualTo(uploads));
                }
                candidate = fixture.Readback();
                var summary = Measure(sample, fontPath, baseline.GetPixels32(), candidate.GetPixels32(),
                    fixture.MaskBounds, fixture.AppliedAnchoredPosition, out var differencePixels);
                summary.candidateOutput = expectGlyphMesh ? "glyph-mesh" : "texture-fallback";
                summary.fallbackReason = linearFallback ? "linear-color-space" :
                    expectGlyphMesh ? string.Empty : "colored-source";
                summary.sourceColor = textColor ?? Color.white;
                summary.backgroundColor = backgroundColor ?? Color.black;
                difference = new Texture2D(ImageWidth, ImageHeight, TextureFormat.RGBA32, false, true);
                difference.SetPixels32(differencePixels);
                difference.Apply();
                var directory = ArtifactDirectory(sample.Name);
                File.WriteAllBytes(IOPath.Combine(directory, "baseline.png"), baseline.EncodeToPNG());
                File.WriteAllBytes(IOPath.Combine(directory, "candidate.png"), candidate.EncodeToPNG());
                File.WriteAllBytes(IOPath.Combine(directory, "difference.png"), difference.EncodeToPNG());
                File.WriteAllText(IOPath.Combine(directory, "summary.json"), JsonUtility.ToJson(summary, true));
                TestContext.Out.WriteLine(sample.Name + ": " + JsonUtility.ToJson(summary));
                Assert.That(summary.baseline.litPixels, Is.GreaterThanOrEqualTo(MinimumLitPixels));
                Assert.That(summary.candidate.litPixels, Is.GreaterThanOrEqualTo(MinimumLitPixels));
                Assert.That(summary.boundsDifferencePixels, Is.LessThanOrEqualTo(MaximumBoundsDifference),
                    "Coverage bounds moved; inspect baseline.png, candidate.png, and summary.json.");
                Assert.That(summary.centroidDifferencePixels, Is.LessThanOrEqualTo(MaximumCentroidDifference),
                    "The red-channel intensity-weighted centroid moved; antialiasing and alpha can affect this metric.");
                Assert.That(summary.coverageRelativeDifference, Is.LessThanOrEqualTo(MaximumCoverageDifference),
                    "Integrated foreground red-channel intensity changed.");
                Assert.That(summary.foregroundMeanAbsoluteError, Is.LessThanOrEqualTo(MaximumForegroundError),
                    "Foreground pixel error exceeds the fixed tolerance.");
                Assert.That(summary.lowPassForegroundMeanAbsoluteError, Is.LessThanOrEqualTo(MaximumLowPassError),
                    "Error remains after a fixed 3x3 low-pass filter; inspect geometry and glyph phase.");
                if (sample.Masked)
                {
                    Assert.That(summary.baseline.maximumOutsideMask, Is.LessThanOrEqualTo(MaximumOutsideMask));
                    Assert.That(summary.candidate.maximumOutsideMask, Is.LessThanOrEqualTo(MaximumOutsideMask));
                    Assert.That(summary.baseline.litPixelsNearMaskEdge, Is.GreaterThan(0),
                        "The fixture must exercise a clipped glyph at a hard-mask edge.");
                    Assert.That(summary.candidate.litPixelsNearMaskEdge, Is.GreaterThan(0));
                }
            }
            finally
            {
                if (baseline != null) Object.Destroy(baseline);
                if (candidate != null) Object.Destroy(candidate);
                if (difference != null) Object.Destroy(difference);
                fixture?.Dispose();
                MilestroConfiguration.Configuration.RenderSurface = previousConfiguration;
            }
        }

        private static ComparisonSummary Measure(Sample sample, string fontPath, Color32[] baseline,
            Color32[] candidate, Rect mask, Vector2 appliedAnchoredPosition, out Color32[] difference)
        {
            var baselineCoverage = Coverage(baseline);
            var candidateCoverage = Coverage(candidate);
            var baselineMetrics = MeasureImage(baselineCoverage, mask, sample.Masked);
            var candidateMetrics = MeasureImage(candidateCoverage, mask, sample.Masked);
            var rawError = 0f;
            var filteredError = 0f;
            var foregroundPixels = 0;
            var filteredPixels = 0;
            difference = new Color32[baseline.Length];
            for (var index = 0; index < baseline.Length; ++index)
            {
                var error = Mathf.Abs(baselineCoverage[index] - candidateCoverage[index]);
                var errorByte = (byte)Mathf.RoundToInt(error * 255f);
                difference[index] = new Color32(errorByte, errorByte, errorByte, 255);
                if (baselineCoverage[index] > 1f / 255f || candidateCoverage[index] > 1f / 255f)
                {
                    rawError += error;
                    ++foregroundPixels;
                }

                var horizontal = index % ImageWidth;
                var vertical = index / ImageWidth;
                var baselineFiltered = LowPass(baselineCoverage, horizontal, vertical);
                var candidateFiltered = LowPass(candidateCoverage, horizontal, vertical);
                if (baselineFiltered > 1f / 255f || candidateFiltered > 1f / 255f)
                {
                    filteredError += Mathf.Abs(baselineFiltered - candidateFiltered);
                    ++filteredPixels;
                }
            }

            using var hash = SHA256.Create();
            using var fontStream = File.OpenRead(fontPath);
            return new ComparisonSummary
            {
                sample = sample.Name,
                fontFile = IOPath.GetFileName(fontPath),
                fontFamily = FixtureFamily,
                fontSha256 = BitConverter.ToString(hash.ComputeHash(fontStream)).Replace("-", "").ToLowerInvariant(),
                familyFallback = sample.FamilyFallback,
                familyFallbackSemantics = sample.FamilyFallback
                    ? "Missing family resolves to the explicitly registered family; per-glyph font fallback and complex-script shaping are not verified."
                    : "No missing-family fallback requested.",
                text = sample.Content,
                fontSize = sample.FontSize,
                canvasScale = sample.CanvasScale,
                requestedAnchoredPosition = sample.Position,
                anchoredPosition = appliedAnchoredPosition,
                imageWidth = ImageWidth,
                imageHeight = ImageHeight,
                colorSpace = QualitySettings.activeColorSpace.ToString(),
                graphicsBackend = SystemInfo.graphicsDeviceType.ToString(),
                hardMask = sample.Masked,
                maskBoundsPixels = mask,
                coverageMetricSemantics = "totalCoverage integrates normalized rendered red-channel intensity on black, not source alpha or glyph coverage; centroid is weighted by the same intensity.",
                baseline = baselineMetrics,
                candidate = candidateMetrics,
                boundsDifferencePixels = MaximumEdgeDifference(baselineMetrics.bounds, candidateMetrics.bounds),
                centroidDifferencePixels = Vector2.Distance(baselineMetrics.centroid, candidateMetrics.centroid),
                coverageRelativeDifference = Mathf.Abs(candidateMetrics.totalCoverage - baselineMetrics.totalCoverage) /
                                             Mathf.Max(baselineMetrics.totalCoverage, 1f),
                foregroundMeanAbsoluteError = rawError / Mathf.Max(foregroundPixels, 1),
                lowPassForegroundMeanAbsoluteError = filteredError / Mathf.Max(filteredPixels, 1),
                maximumBoundsDifferencePixels = MaximumBoundsDifference,
                maximumCentroidDifferencePixels = MaximumCentroidDifference,
                maximumCoverageRelativeDifference = MaximumCoverageDifference,
                maximumForegroundMeanAbsoluteError = MaximumForegroundError,
                maximumLowPassForegroundMeanAbsoluteError = MaximumLowPassError,
                maximumOutsideMask = MaximumOutsideMask,
                coverageThreshold = CoverageThreshold,
                minimumLitPixels = MinimumLitPixels
            };
        }

        private static float[] Coverage(Color32[] pixels)
        {
            var result = new float[pixels.Length];
            for (var index = 0; index < pixels.Length; ++index)
            {
                result[index] = pixels[index].r / 255f;
            }
            return result;
        }

        private static ImageMetrics MeasureImage(float[] coverage, Rect mask, bool masked)
        {
            var metrics = new ImageMetrics();
            var minimum = new Vector2(ImageWidth, ImageHeight);
            var maximum = Vector2.zero;
            var weightedPosition = Vector2.zero;
            for (var index = 0; index < coverage.Length; ++index)
            {
                var horizontal = index % ImageWidth;
                var vertical = index / ImageWidth;
                var center = new Vector2(horizontal + 0.5f, vertical + 0.5f);
                var value = coverage[index];
                metrics.totalCoverage += value;
                weightedPosition += center * value;
                if (masked && !mask.Contains(center))
                {
                    metrics.maximumOutsideMask = Mathf.Max(metrics.maximumOutsideMask, value);
                }
                if (value < CoverageThreshold / 255f) continue;
                ++metrics.litPixels;
                minimum = Vector2.Min(minimum, new Vector2(horizontal, vertical));
                maximum = Vector2.Max(maximum, new Vector2(horizontal + 1, vertical + 1));
                if (masked && mask.Contains(center) &&
                    Mathf.Min(center.x - mask.xMin, mask.xMax - center.x,
                        center.y - mask.yMin, mask.yMax - center.y) <= 1.5f)
                {
                    ++metrics.litPixelsNearMaskEdge;
                }
            }
            metrics.bounds = metrics.litPixels > 0 ? Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y) : Rect.zero;
            metrics.centroid = weightedPosition / Mathf.Max(metrics.totalCoverage, 0.0001f);
            return metrics;
        }

        private static float LowPass(float[] coverage, int horizontal, int vertical)
        {
            var sum = 0f;
            for (var offsetY = -1; offsetY <= 1; ++offsetY)
            {
                var sampleY = vertical + offsetY;
                if (sampleY < 0 || sampleY >= ImageHeight) continue;
                for (var offsetX = -1; offsetX <= 1; ++offsetX)
                {
                    var sampleX = horizontal + offsetX;
                    if (sampleX >= 0 && sampleX < ImageWidth)
                    {
                        sum += coverage[sampleY * ImageWidth + sampleX];
                    }
                }
            }
            return sum / 9f;
        }

        private static float MaximumEdgeDifference(Rect first, Rect second)
        {
            return Mathf.Max(Mathf.Abs(first.xMin - second.xMin), Mathf.Abs(first.xMax - second.xMax),
                Mathf.Abs(first.yMin - second.yMin), Mathf.Abs(first.yMax - second.yMax));
        }

        private static string FindFixtureFont()
        {
            var configured = Environment.GetEnvironmentVariable("MILESTRO_TEXT_IMAGE_FONT");
            if (!string.IsNullOrEmpty(configured))
            {
                Assert.That(File.Exists(configured), Is.True, "MILESTRO_TEXT_IMAGE_FONT must reference LXGWWenKai-Regular.ttf.");
                return configured;
            }
            foreach (var start in new[] { Application.dataPath, Directory.GetCurrentDirectory() })
            {
                for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                {
                    var path = IOPath.Combine(directory.FullName, "tests", "data", "font", "LXGWWenKai-Regular.ttf");
                    if (File.Exists(path)) return path;
                }
            }
            Assert.Fail("Fixture font missing: use tests/data/font/LXGWWenKai-Regular.ttf or MILESTRO_TEXT_IMAGE_FONT.");
            return string.Empty;
        }

        private static string ArtifactDirectory(string sample)
        {
            var configured = Environment.GetEnvironmentVariable("MILESTRO_TEXT_IMAGE_OUTPUT");
            var root = string.IsNullOrEmpty(configured)
                ? IOPath.Combine(Application.temporaryCachePath, "Milestro", "text-image-comparison")
                : configured;
            var directory = IOPath.Combine(root, sample);
            Directory.CreateDirectory(directory);
            return directory;
        }

        private sealed class Sample
        {
            internal readonly string Name;
            internal readonly bool Paragraph;
            internal readonly float FontSize;
            internal readonly float CanvasScale;
            internal readonly Vector2 Position;
            internal readonly string Content;
            internal readonly bool Masked;
            internal readonly bool FamilyFallback;

            internal Sample(string name, bool paragraph, float fontSize, float canvasScale, Vector2 position,
                string content, bool masked = false, bool familyFallback = false)
            {
                Name = name;
                Paragraph = paragraph;
                FontSize = fontSize;
                CanvasScale = canvasScale;
                Position = position;
                Content = content;
                Masked = masked;
                FamilyFallback = familyFallback;
            }
        }

        private sealed class Fixture : IDisposable
        {
            private const int Layer = 30;
            private readonly GameObject cameraObject;
            private readonly GameObject canvasObject;
            private readonly Camera camera;
            private readonly RenderTexture target;
            private readonly RenderTextureProducer producer;
            private readonly SlimTextRenderTextureProducer slimProducer;
            private readonly TextBoxRenderTextureProducer paragraphProducer;
            private readonly RectTransform maskTransform;

            internal Fixture(Sample sample)
            {
                target = new RenderTexture(ImageWidth, ImageHeight, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear)
                { antiAliasing = 1 };
                target.Create();
                cameraObject = new GameObject("Text Comparison Camera", typeof(Camera));
                camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = ImageHeight / 2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << Layer;
                camera.targetTexture = target;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                canvasObject = new GameObject("Text Comparison Canvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.layer = Layer;
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                canvas.pixelPerfect = false;
                canvas.scaleFactor = sample.CanvasScale;
                var parent = canvasObject.transform;
                if (sample.Masked)
                {
                    var maskObject = new GameObject("Text Comparison Clip", typeof(RectTransform), typeof(RectMask2D));
                    maskObject.layer = Layer;
                    maskTransform = (RectTransform)maskObject.transform;
                    maskTransform.SetParent(parent, false);
                    maskTransform.sizeDelta = new Vector2(96f, 19f);
                    maskTransform.anchoredPosition = new Vector2(0.2f, 0.3f);
                    maskObject.GetComponent<RectMask2D>().softness = Vector2Int.zero;
                    parent = maskTransform;
                }
                var textObject = new GameObject("Text Comparison Sample", typeof(RectTransform));
                textObject.layer = Layer;
                var rect = (RectTransform)textObject.transform;
                rect.SetParent(parent, false);
                rect.sizeDelta = new Vector2(260f, 120f);
                rect.anchoredPosition = sample.Position;
                if (sample.Paragraph)
                {
                    var textBox = textObject.AddComponent<TextBox>();
                    textBox.raycastTarget = false;
                    paragraphProducer = textObject.GetComponent<TextBoxRenderTextureProducer>();
                    paragraphProducer.textStyle.fontFamilies = sample.FamilyFallback
                        ? new[] { "__MilestroImageMissingFamily__", FixtureFamily }
                        : new[] { FixtureFamily };
                    paragraphProducer.textStyle.size = sample.FontSize;
                    paragraphProducer.textStyle.textColor = Color.white;
                    textBox.Text = sample.Content;
                    producer = paragraphProducer;
                    if (sample.Masked)
                    {
                        rect.anchoredPosition += new Vector2(0f, -50f);
                    }
                }
                else
                {
                    var slim = textObject.AddComponent<CanvasSlimText>();
                    slim.Configure(sample.Content, sample.FontSize, SlimTextHorizontalAlign.Left, Color.white);
                    slimProducer = textObject.GetComponent<SlimTextRenderTextureProducer>();
                    slimProducer.fontFamily = FixtureFamily;
                    slimProducer.fallbackToSystemFont = false;
                    producer = slimProducer;
                }
            }

            internal Rect MaskBounds
            {
                get
                {
                    if (maskTransform == null) return Rect.zero;
                    var corners = new Vector3[4];
                    maskTransform.GetWorldCorners(corners);
                    var minimum = (Vector2)camera.WorldToScreenPoint(corners[0]);
                    var maximum = (Vector2)camera.WorldToScreenPoint(corners[2]);
                    return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
                }
            }

            internal Vector2 AppliedAnchoredPosition => ((RectTransform)producer.transform).anchoredPosition;

            internal void SetBackground(Color value) => camera.backgroundColor = value;

            internal void SetVisible(bool visible) => producer.gameObject.SetActive(visible);

            internal void SetTextColor(Color value)
            {
                if (paragraphProducer != null) paragraphProducer.textStyle.textColor = value;
                else slimProducer.color = value;
                Rebuild(true);
            }

            internal void SetPresentationOpacity(float tint, float inherited)
            {
                var graphic = producer.GetComponent<Graphic>();
                var color = graphic.color;
                color.a = tint;
                graphic.color = color;
                var group = canvasObject.AddComponent<CanvasGroup>();
                group.alpha = inherited;
            }

            internal void Rebuild(bool force)
            {
                if (paragraphProducer != null) paragraphProducer.RebuildOutput(force);
                else slimProducer.RebuildOutput(force);
            }

            internal IEnumerator WaitForOutput(bool glyphMesh)
            {
                for (var frame = 0; frame < 180; ++frame)
                {
                    Canvas.ForceUpdateCanvases();
                    Rebuild(false);
                    RenderTextureAtlas.Flush();
                    TextGlyphAtlas.Flush();
                    camera.Render();
                    UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                    var hasMesh = paragraphProducer != null
                        ? paragraphProducer.GlyphMeshOutput != null
                        : slimProducer.GlyphMeshOutput != null;
                    if (producer.HasOutput && hasMesh == glyphMesh)
                    {
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                        Assert.That(producer.OutputTexture, Is.Not.Null);
                        if (glyphMesh) Assert.That(producer.OutputTexture, Is.TypeOf<Texture2D>());
                        if (!glyphMesh)
                        {
                            Assert.That(TextTextureMaterial.Material, Is.Not.Null,
                                "The premultiplied text shader must be included and supported.");
                            Assert.That(producer.GetComponent<Graphic>().materialForRendering.shader.name,
                                Is.EqualTo("Milestro/TextPremultiplied"));
                        }
                        yield break;
                    }
                    yield return null;
                }
                Assert.Fail("Text comparison output did not complete in the requested mode within 180 frames.");
            }

            internal Texture2D Readback()
            {
                Canvas.ForceUpdateCanvases();
                camera.Render();
                var previous = RenderTexture.active;
                var result = new Texture2D(ImageWidth, ImageHeight, TextureFormat.RGBA32, false, true);
                try
                {
                    RenderTexture.active = target;
                    result.ReadPixels(new Rect(0, 0, ImageWidth, ImageHeight), 0, 0);
                    result.Apply();
                    return result;
                }
                finally
                {
                    RenderTexture.active = previous;
                }
            }

            public void Dispose()
            {
                Object.Destroy(canvasObject);
                Object.Destroy(cameraObject);
                target.Release();
                Object.Destroy(target);
            }
        }

        [Serializable]
        private sealed class ImageMetrics
        {
            public int litPixels;
            public float totalCoverage;
            public Rect bounds;
            public Vector2 centroid;
            public float maximumOutsideMask;
            public int litPixelsNearMaskEdge;
        }

        [Serializable]
        private sealed class ComparisonSummary
        {
            public string sample;
            public string candidateOutput;
            public string fallbackReason;
            public Color sourceColor;
            public Color backgroundColor;
            public string fontFile;
            public string fontFamily;
            public string fontSha256;
            public bool familyFallback;
            public string familyFallbackSemantics;
            public string text;
            public float fontSize;
            public float canvasScale;
            public Vector2 requestedAnchoredPosition;
            public Vector2 anchoredPosition;
            public int imageWidth;
            public int imageHeight;
            public string colorSpace;
            public string graphicsBackend;
            public bool hardMask;
            public Rect maskBoundsPixels;
            public string coverageMetricSemantics;
            public ImageMetrics baseline;
            public ImageMetrics candidate;
            public float boundsDifferencePixels;
            public float centroidDifferencePixels;
            public float coverageRelativeDifference;
            public float foregroundMeanAbsoluteError;
            public float lowPassForegroundMeanAbsoluteError;
            public float maximumBoundsDifferencePixels;
            public float maximumCentroidDifferencePixels;
            public float maximumCoverageRelativeDifference;
            public float maximumForegroundMeanAbsoluteError;
            public float maximumLowPassForegroundMeanAbsoluteError;
            public float maximumOutsideMask;
            public int coverageThreshold;
            public int minimumLitPixels;
        }
    }
}

using Milestro.Components;
using Milestro.Model;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Tests
{
    public sealed class RenderTextureGraphicTests
    {
        private GameObject root;
        private Texture2D texture;
        private TextGradientAsset preset;

        [TearDown]
        public void TearDown()
        {
            if (preset != null)
            {
                Object.DestroyImmediate(preset);
            }

            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }

            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TextBoxWithoutOutputDoesNotBuildFallbackQuad()
        {
            root = new GameObject("Test Canvas", typeof(RectTransform), typeof(Canvas));
            var textObject = new GameObject("Text Box", typeof(RectTransform));
            textObject.transform.SetParent(root.transform, false);
            ((RectTransform)textObject.transform).sizeDelta = new Vector2(400, 100);
            var textBox = textObject.AddComponent<TextBox>();
            textBox.Texture = null;

            textBox.Rebuild(CanvasUpdate.PreRender);

            Assert.That(textBox.canvasRenderer.GetMesh().vertexCount, Is.Zero);
        }

        [Test]
        public void DisabledColorGradientKeepsUniformVertexColors()
        {
            var textBox = CreateTextBox();
            textBox.color = Color.white;

            var (vertices, colors) = RebuildMesh(textBox);

            Assert.That(vertices, Has.Length.EqualTo(4));
            foreach (var vertexColor in colors)
            {
                AssertColorClose(vertexColor, Color.white);
            }
        }

        [Test]
        public void FourCornersGradientAppliesPerCornerColors()
        {
            var textBox = CreateTextBox();
            textBox.color = Color.white;
            textBox.enableColorGradient = true;
            textBox.colorGradient = TextGradient.ForCorners(new TextColorGradient(
                Color.red,
                Color.green,
                Color.blue,
                Color.yellow));

            var (vertices, colors) = RebuildMesh(textBox);

            Assert.That(vertices, Has.Length.EqualTo(4));
            AssertColorClose(CornerColor(vertices, colors, left: true, top: true), Color.red);
            AssertColorClose(CornerColor(vertices, colors, left: false, top: true), Color.green);
            AssertColorClose(CornerColor(vertices, colors, left: true, top: false), Color.blue);
            AssertColorClose(CornerColor(vertices, colors, left: false, top: false), Color.yellow);
        }

        [Test]
        public void FourCornersGradientMultipliesGraphicColor()
        {
            var textBox = CreateTextBox();
            textBox.color = new Color(0.5f, 0.25f, 1f, 1f);
            textBox.enableColorGradient = true;
            textBox.colorGradient = TextGradient.ForCorners(new TextColorGradient(
                Color.white,
                Color.white,
                Color.white,
                Color.white));

            var (_, colors) = RebuildMesh(textBox);

            foreach (var vertexColor in colors)
            {
                AssertColorClose(vertexColor, new Color(0.5f, 0.25f, 1f, 1f));
            }
        }

        [Test]
        public void GradientPresetAssetOverridesInlineGradient()
        {
            var textBox = CreateTextBox();
            textBox.color = Color.white;
            textBox.enableColorGradient = true;
            textBox.colorGradient = TextGradient.ForCorners(new TextColorGradient(
                Color.red,
                Color.red,
                Color.red,
                Color.red));
            preset = ScriptableObject.CreateInstance<TextGradientAsset>();
            preset.Gradient = TextGradient.ForCorners(new TextColorGradient(
                Color.green,
                Color.green,
                Color.green,
                Color.green));
            textBox.colorGradientPreset = preset;

            var (_, colors) = RebuildMesh(textBox);

            foreach (var vertexColor in colors)
            {
                AssertColorClose(vertexColor, Color.green);
            }
        }

        [Test]
        public void LinearGradientZeroAngleFillsLeftToRight()
        {
            var textBox = CreateTextBox();
            textBox.color = Color.white;
            textBox.enableColorGradient = true;
            textBox.colorGradient = TextGradient.Linear(CreateTwoStopGradient(Color.red, Color.blue), 0f);

            var (vertices, colors) = RebuildMesh(textBox);

            AssertColorClose(EdgeColor(vertices, colors, horizontal: true, nearSide: true), Color.red);
            AssertColorClose(EdgeColor(vertices, colors, horizontal: true, nearSide: false), Color.blue);
        }

        [Test]
        public void LinearGradientNinetyDegreesFillsTopToBottom()
        {
            var textBox = CreateTextBox();
            textBox.color = Color.white;
            textBox.enableColorGradient = true;
            textBox.colorGradient = TextGradient.Linear(CreateTwoStopGradient(Color.red, Color.blue), 90f);

            var (vertices, colors) = RebuildMesh(textBox);

            AssertColorClose(EdgeColor(vertices, colors, horizontal: false, nearSide: true), Color.red);
            AssertColorClose(EdgeColor(vertices, colors, horizontal: false, nearSide: false), Color.blue);
        }

        [Test]
        public void RadialGradientReachesLastStopAtCorners()
        {
            var textBox = CreateTextBox();
            textBox.color = Color.white;
            textBox.enableColorGradient = true;
            textBox.colorGradient = TextGradient.Radial(CreateTwoStopGradient(Color.white, Color.black));

            var (vertices, colors) = RebuildMesh(textBox);

            AssertColorClose(InnermostColor(vertices, colors), Color.white);
            AssertColorClose(CornerColor(vertices, colors, left: true, top: true), Color.black);
            AssertColorClose(CornerColor(vertices, colors, left: false, top: false), Color.black);
        }

        [Test]
        public void GradientMeshResolutionControlsVertexCount()
        {
            var textBox = CreateTextBox();
            textBox.color = Color.white;
            textBox.enableColorGradient = true;
            var gradient = TextGradient.Linear(CreateTwoStopGradient(Color.red, Color.blue), 0f);
            gradient.MeshResolution = 4;
            textBox.colorGradient = gradient;

            var (vertices, _) = RebuildMesh(textBox);

            Assert.That(vertices, Has.Length.EqualTo(25));
        }

        private TextBox CreateTextBox()
        {
            root = new GameObject("Test Canvas", typeof(RectTransform), typeof(Canvas));
            var textObject = new GameObject("Text Box", typeof(RectTransform));
            textObject.transform.SetParent(root.transform, false);
            ((RectTransform)textObject.transform).sizeDelta = new Vector2(400, 100);
            var textBox = textObject.AddComponent<TextBox>();
            texture = new Texture2D(4, 4);
            textBox.Texture = texture;
            return textBox;
        }

        private static (Vector3[] Vertices, Color32[] Colors) RebuildMesh(TextBox textBox)
        {
            textBox.Rebuild(CanvasUpdate.PreRender);
            var mesh = textBox.canvasRenderer.GetMesh();
            return (mesh.vertices, mesh.colors32);
        }

        private static Gradient CreateTwoStopGradient(Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        private static Color32 CornerColor(Vector3[] vertices, Color32[] colors, bool left, bool top)
        {
            var bestIndex = 0;
            var bestScore = float.MinValue;
            for (var i = 0; i < vertices.Length; ++i)
            {
                var vertex = vertices[i];
                var score = (left ? -vertex.x : vertex.x) + (top ? vertex.y : -vertex.y);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return colors[bestIndex];
        }

        private static Color32 EdgeColor(Vector3[] vertices, Color32[] colors, bool horizontal, bool nearSide)
        {
            var bestIndex = 0;
            var bestScore = float.MinValue;
            for (var i = 0; i < vertices.Length; ++i)
            {
                var vertex = vertices[i];
                float score;
                if (horizontal)
                {
                    score = nearSide ? -vertex.x : vertex.x;
                }
                else
                {
                    // Office angle 90 fills top to bottom: the near side is the top edge.
                    score = nearSide ? vertex.y : -vertex.y;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return colors[bestIndex];
        }

        private static Color32 InnermostColor(Vector3[] vertices, Color32[] colors)
        {
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            foreach (var vertex in vertices)
            {
                minX = Mathf.Min(minX, vertex.x);
                maxX = Mathf.Max(maxX, vertex.x);
                minY = Mathf.Min(minY, vertex.y);
                maxY = Mathf.Max(maxY, vertex.y);
            }

            var center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            var bestIndex = 0;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < vertices.Length; ++i)
            {
                var distance = Vector2.Distance(vertices[i], center);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return colors[bestIndex];
        }

        private static void AssertColorClose(Color32 actual, Color expected)
        {
            Color32 expected32 = expected;
            Assert.That(Mathf.Abs(actual.r - expected32.r), Is.LessThanOrEqualTo(1), "red channel");
            Assert.That(Mathf.Abs(actual.g - expected32.g), Is.LessThanOrEqualTo(1), "green channel");
            Assert.That(Mathf.Abs(actual.b - expected32.b), Is.LessThanOrEqualTo(1), "blue channel");
            Assert.That(Mathf.Abs(actual.a - expected32.a), Is.LessThanOrEqualTo(1), "alpha channel");
        }
    }
}

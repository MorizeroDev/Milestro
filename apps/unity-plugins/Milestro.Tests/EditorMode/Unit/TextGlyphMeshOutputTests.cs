using Milestro.Skia;
using Milestro.Skia.TextLayout;
using Milestro.Components.Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Tests
{
    public sealed class TextGlyphMeshOutputTests
    {
        [TestCase(0.124f, 4u, 0f, 0f)]
        [TestCase(0.125f, 4u, 0f, 0.25f)]
        [TestCase(0.874f, 4u, 0f, 0.75f)]
        [TestCase(0.875f, 4u, 1f, 0f)]
        [TestCase(-0.126f, 4u, -1f, 0.75f)]
        [TestCase(-0.125f, 4u, 0f, 0f)]
        [TestCase(-0.876f, 4u, -1f, 0f)]
        [TestCase(-0.875f, 4u, -1f, 0.25f)]
        [TestCase(0.499f, 0u, 0f, 0f)]
        [TestCase(0.5f, 0u, 1f, 0f)]
        [TestCase(-0.501f, 0u, -1f, 0f)]
        [TestCase(-0.5f, 0u, 0f, 0f)]
        public void RasterPositionMatchesSkiaPhaseBoundaries(float origin, uint flags,
            float expectedAnchor, float expectedPhase)
        {
            Assert.That(TextGlyphAtlas.TryResolveRasterPosition(new Vector2(origin, origin), flags,
                out var position), Is.True);

            Assert.That(position.Anchor, Is.EqualTo(new Vector2(expectedAnchor, expectedAnchor)));
            Assert.That(position.PhaseX, Is.EqualTo(expectedPhase));
            Assert.That(position.PhaseY, Is.EqualTo(expectedPhase));
        }

        [Test]
        public void BaselineSnapKeepsHorizontalSubpixelsAndRoundsVerticalOrigin()
        {
            Assert.That(TextGlyphAtlas.TryResolveRasterPosition(new Vector2(-0.26f, -0.26f), 4u | 32u,
                out var position), Is.True);

            Assert.That(position.Anchor, Is.EqualTo(new Vector2(-1f, 0f)));
            Assert.That(position.PhaseX, Is.EqualTo(0.75f));
            Assert.That(position.PhaseY, Is.Zero);
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(0f, float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity, 0f)]
        [TestCase(float.MaxValue, float.MaxValue)]
        public void NonRasterizableOriginsRejectGlyphOutput(float horizontal, float vertical)
        {
            Assert.That(TextGlyphAtlas.TryResolveRasterPosition(new Vector2(horizontal, vertical), 4u,
                out _), Is.False);
        }

        [Test]
        public void ClipPreservesGlyphUvMappingAndDisplayCoordinates()
        {
            using var page = new TextGlyphAtlas.Page(32);
            using var output = new TextGlyphMeshOutput(page,
                new[]
                {
                    new TextGlyphQuad(new Rect(10f, 20f, 40f, 20f),
                        new Rect(0.2f, 0.3f, 0.4f, 0.2f), new Color32(255, 128, 64, 255))
                },
                new Vector2(100f, 100f), new Rect(20f, 25f, 20f, 10f));
            using var vertices = new VertexHelper();

            output.Populate(vertices, new Rect(-100f, -50f, 200f, 300f), Color.white);

            Assert.That(vertices.currentVertCount, Is.EqualTo(4));
            AssertVertex(vertices, 0, new Vector3(-60f, 145f), new Vector2(0.3f, 0.35f));
            AssertVertex(vertices, 1, new Vector3(-60f, 175f), new Vector2(0.3f, 0.45f));
            AssertVertex(vertices, 2, new Vector3(-20f, 175f), new Vector2(0.5f, 0.45f));
            AssertVertex(vertices, 3, new Vector3(-20f, 145f), new Vector2(0.5f, 0.35f));
        }

        [Test]
        public void RepeatedPopulateUsesCurrentDisplayAndTintWithoutChangingSourceQuad()
        {
            using var page = new TextGlyphAtlas.Page(32);
            var sourceColor = new Color32(255, 128, 64, 128);
            using var output = new TextGlyphMeshOutput(page,
                new[] { new TextGlyphQuad(new Rect(10f, 20f, 40f, 20f),
                    new Rect(0.2f, 0.3f, 0.4f, 0.2f), sourceColor) },
                new Vector2(100f, 100f), new Rect(0f, 0f, 100f, 100f));
            using var vertices = new VertexHelper();
            output.Populate(vertices, new Rect(0f, 0f, 100f, 100f), Color.white);
            AssertVertex(vertices, 0, new Vector3(10f, 60f), new Vector2(0.2f, 0.3f));
            var vertex = new UIVertex();
            vertices.PopulateUIVertex(ref vertex, 0);
            Assert.That(vertex.color, Is.EqualTo(sourceColor));

            var tint = new Color(0.5f, 0.25f, 0.75f, 0.5f);
            output.Populate(vertices, new Rect(-100f, -50f, 200f, 300f), tint);
            Assert.That(vertices.currentVertCount, Is.EqualTo(4));
            AssertVertex(vertices, 0, new Vector3(-80f, 130f), new Vector2(0.2f, 0.3f));
            AssertVertex(vertices, 2, new Vector3(0f, 190f), new Vector2(0.6f, 0.5f));
            vertices.PopulateUIVertex(ref vertex, 0);
            Assert.That(vertex.color, Is.EqualTo((Color32)((Color)sourceColor * tint)));

            output.Populate(vertices, new Rect(0f, 0f, 100f, 100f), Color.white);
            vertices.PopulateUIVertex(ref vertex, 0);
            Assert.That(vertex.color, Is.EqualTo(sourceColor));
        }

        [Test]
        public void LogicalBoundsClipEvenWhenClipRectExtendsBeyondOutput()
        {
            using var page = new TextGlyphAtlas.Page(32);
            using var output = new TextGlyphMeshOutput(page,
                new[] { new TextGlyphQuad(new Rect(-10f, -10f, 40f, 40f),
                    new Rect(0f, 0f, 1f, 1f), Color.white) },
                new Vector2(20f, 20f), new Rect(-100f, -100f, 200f, 200f));
            using var vertices = new VertexHelper();
            output.Populate(vertices, new Rect(0f, 0f, 20f, 20f), Color.white);
            Assert.That(vertices.currentVertCount, Is.EqualTo(4));
            AssertVertex(vertices, 0, Vector3.zero, new Vector2(0.25f, 0.25f));
            AssertVertex(vertices, 2, new Vector3(20f, 20f), new Vector2(0.75f, 0.75f));
        }

        [Test]
        public void EmptyGlyphDoesNotRasterizeOrUpload()
        {
            using var paragraphStyle = new ParagraphStyle();
            using var textStyle = new TextStyle { FontSize = 20f };
            using var builder = new ParagraphBuilder(paragraphStyle);
            builder.PushStyle(textStyle);
            builder.AddText("a a");
            using var paragraph = builder.Build();
            paragraph.Layout(200f);
            using var snapshot = paragraph.CreateGlyphLayoutSnapshot();
            Assert.That(snapshot, Is.Not.Null);
            using var page = new TextGlyphAtlas.Page(32);
            page.Flush();
            var previousDiagnostics = TextPipelineDiagnostics.Enabled;
            TextPipelineDiagnostics.Enabled = true;
            try
            {
                var before = TextPipelineDiagnostics.Capture();
                var descriptor = snapshot.GetAtlasGlyph(1);
                Assert.That(page.TryGet(snapshot, 1, descriptor, 1f, default, out var first), Is.True);
                Assert.That(first.Bounds.width * first.Bounds.height, Is.Zero);
                Assert.That(page.TryGet(snapshot, 1, descriptor, 1f, default, out var second), Is.True);
                Assert.That(second.Bounds, Is.EqualTo(first.Bounds));
                page.Flush();
                var after = TextPipelineDiagnostics.Capture();
                Assert.That(after.GlyphRasterizations - before.GlyphRasterizations, Is.Zero);
                Assert.That(after.GlyphAtlasUploads - before.GlyphAtlasUploads, Is.Zero);
            }
            finally
            {
                TextPipelineDiagnostics.Enabled = previousDiagnostics;
            }
        }

        [Test]
        public void ClippedAwayGlyphDoesNotEmitVertices()
        {
            using var page = new TextGlyphAtlas.Page(32);
            using var output = new TextGlyphMeshOutput(page,
                new[] { new TextGlyphQuad(new Rect(1f, 1f, 2f, 2f), new Rect(0f, 0f, 1f, 1f), Color.white) },
                new Vector2(100f, 100f), new Rect(10f, 10f, 20f, 20f));
            using var vertices = new VertexHelper();

            output.Populate(vertices, new Rect(0f, 0f, 100f, 100f), Color.white);

            Assert.That(vertices.currentVertCount, Is.Zero);
        }

        [Test]
        public void DisposingOneOutputKeepsOtherOutputAndPageAlive()
        {
            using var page = new TextGlyphAtlas.Page(32);
            using var first = new TextGlyphMeshOutput(page, new TextGlyphQuad[0], Vector2.one,
                new Rect(0f, 0f, 1f, 1f));
            using var second = new TextGlyphMeshOutput(page, new TextGlyphQuad[0], Vector2.one,
                new Rect(0f, 0f, 1f, 1f));
            Assert.That(page.References, Is.EqualTo(2));

            first.Dispose();
            first.Dispose();

            Assert.That(page.References, Is.EqualTo(1));
            Assert.That(first.IsValid, Is.False);
            Assert.That(second.IsValid, Is.True);
            Assert.That(second.Texture, Is.SameAs(page.Texture));
            second.Dispose();
            Assert.That(page.References, Is.Zero);
        }

        [Test]
        public void PageUploadsOnlyWhenDirtyAndStopsAfterDisposal()
        {
            using var page = new TextGlyphAtlas.Page(32);
            var uploads = TextGlyphAtlas.Uploads;

            page.Flush();
            page.Flush();

            Assert.That(TextGlyphAtlas.Uploads, Is.EqualTo(uploads + 1));
            page.Dispose();
            page.Flush();
            Assert.That(TextGlyphAtlas.Uploads, Is.EqualTo(uploads + 1));
        }

        [Test]
        public void DisposedPageInvalidatesOutputAndClearsStaleGeometry()
        {
            using var page = new TextGlyphAtlas.Page(32);
            using var output = new TextGlyphMeshOutput(page,
                new[] { new TextGlyphQuad(new Rect(0f, 0f, 1f, 1f), new Rect(0f, 0f, 1f, 1f), Color.white) },
                Vector2.one, new Rect(0f, 0f, 1f, 1f));
            using var vertices = new VertexHelper();
            output.Populate(vertices, new Rect(0f, 0f, 1f, 1f), Color.white);
            Assert.That(vertices.currentVertCount, Is.EqualTo(4));

            page.Dispose();
            output.Populate(vertices, new Rect(0f, 0f, 1f, 1f), Color.white);

            Assert.That(output.IsValid, Is.False);
            Assert.That(output.Texture, Is.Null);
            Assert.That(vertices.currentVertCount, Is.Zero);
        }

        private static void AssertVertex(VertexHelper vertices, int index, Vector3 position, Vector2 uv)
        {
            var vertex = new UIVertex();
            vertices.PopulateUIVertex(ref vertex, index);
            Assert.That(vertex.position.x, Is.EqualTo(position.x).Within(0.0001f));
            Assert.That(vertex.position.y, Is.EqualTo(position.y).Within(0.0001f));
            Assert.That(vertex.uv0.x, Is.EqualTo(uv.x).Within(0.0001f));
            Assert.That(vertex.uv0.y, Is.EqualTo(uv.y).Within(0.0001f));
        }
    }
}

using Milestro.Skia;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Tests
{
    public sealed class TextGlyphMeshOutputTests
    {
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

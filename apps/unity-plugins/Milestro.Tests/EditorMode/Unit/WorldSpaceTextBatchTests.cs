using System.Collections.Generic;
using Milestro.Components;
using Milestro.Components.Internal;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.Tests
{
    public class WorldSpaceTextBatchTests
    {
        [Test]
        public void SharedTextureLabelsBecomeOneMeshWithAtlasUvs()
        {
            var root = new GameObject("Batch");
            var quad = CreateQuad();
            var material = CreateMaterial();
            var texture = new Texture2D(4, 4);
            try
            {
                var batch = root.AddComponent<WorldSpaceTextBatch>();
                var first = CreateLabel(root.transform, quad, material, texture,
                    new Rect(0f, 0f, 0.5f, 0.5f), -1f);
                batch.Rebuild();
                Assert.That(batch.BatchedRendererCount, Is.Zero);
                Assert.That(first.GetComponent<MeshRenderer>().enabled, Is.True);

                var second = CreateLabel(root.transform, quad, material, texture,
                    new Rect(0.5f, 0.5f, 0.5f, 0.5f), 1f);
                batch.Rebuild();
                Assert.That(batch.BatchedRendererCount, Is.EqualTo(1));
                Assert.That(batch.BatchedTextCount, Is.EqualTo(2));
                Assert.That(first.GetComponent<MeshRenderer>().enabled, Is.False);
                Assert.That(second.GetComponent<MeshRenderer>().enabled, Is.False);

                var enabled = EnabledRenderers(root);
                Assert.That(enabled.Count, Is.EqualTo(1));
                var combinedRenderer = enabled[0];
                var combined = combinedRenderer.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(combined.vertexCount, Is.EqualTo(8));
                Assert.That(combined.triangles.Length, Is.EqualTo(12));
                var uvs = combined.uv;
                Assert.That(uvs[0], Is.EqualTo(new Vector2(0f, 0f)));
                Assert.That(uvs[2], Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(uvs[4], Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(uvs[6], Is.EqualTo(new Vector2(1f, 1f)));
                var block = new MaterialPropertyBlock();
                combinedRenderer.GetPropertyBlock(block);
                Assert.That(block.GetTexture(Shader.PropertyToID("_MainTex")), Is.SameAs(texture));
                Assert.That(block.GetVector(Shader.PropertyToID("_MainTex_ST")),
                    Is.EqualTo(new Vector4(1f, 1f, 0f, 0f)));

                first.GetComponent<MeshRenderer>().enabled = true;
                second.GetComponent<MeshRenderer>().enabled = true;
                batch.UpdateBatches(false);
                Assert.That(first.GetComponent<MeshRenderer>().enabled, Is.False);
                Assert.That(second.GetComponent<MeshRenderer>().enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(quad);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void SeparatePagesUseSeparateRenderersAndDisableRestoresOriginals()
        {
            var root = new GameObject("Batch");
            var quad = CreateQuad();
            var material = CreateMaterial();
            var firstPage = new Texture2D(4, 4);
            var secondPage = new Texture2D(4, 4);
            try
            {
                var batch = root.AddComponent<WorldSpaceTextBatch>();
                var labels = new List<FakeProducer>
                {
                    CreateLabel(root.transform, quad, material, firstPage, DefaultUv, 0f),
                    CreateLabel(root.transform, quad, material, firstPage, DefaultUv, 1f),
                    CreateLabel(root.transform, quad, material, secondPage, DefaultUv, 2f),
                    CreateLabel(root.transform, quad, material, secondPage, DefaultUv, 3f)
                };
                batch.Rebuild();
                Assert.That(batch.BatchedRendererCount, Is.EqualTo(2));
                Assert.That(batch.BatchedTextCount, Is.EqualTo(4));
                Assert.That(EnabledRenderers(root).Count, Is.EqualTo(2));
                foreach (var label in labels)
                {
                    Assert.That(label.GetComponent<MeshRenderer>().enabled, Is.False);
                }

                batch.enabled = false;
                Assert.That(batch.BatchedRendererCount, Is.Zero);
                foreach (var label in labels)
                {
                    Assert.That(label.GetComponent<MeshRenderer>().enabled, Is.True);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(quad);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(firstPage);
                Object.DestroyImmediate(secondPage);
            }
        }

        private static readonly Rect DefaultUv = new Rect(0f, 0f, 1f, 1f);

        private static List<MeshRenderer> EnabledRenderers(GameObject root)
        {
            var result = new List<MeshRenderer>();
            foreach (var targetRenderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (targetRenderer.enabled)
                {
                    result.Add(targetRenderer);
                }
            }
            return result;
        }

        private static FakeProducer CreateLabel(Transform parent,
            Mesh mesh,
            Material material,
            Texture texture,
            Rect uvRect,
            float x)
        {
            var label = new GameObject("Label", typeof(MeshFilter), typeof(MeshRenderer),
                typeof(FakeProducer));
            label.transform.SetParent(parent, false);
            label.transform.localPosition = new Vector3(x, 0f, 0f);
            label.GetComponent<MeshFilter>().sharedMesh = mesh;
            label.GetComponent<MeshRenderer>().sharedMaterial = material;
            var producer = label.GetComponent<FakeProducer>();
            producer.Texture = texture;
            producer.UvRect = uvRect;
            var presenter = label.AddComponent<RenderTextureMeshPresenter>();
            presenter.Producer = producer;
            return producer;
        }

        private static Mesh CreateQuad()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f), new Vector3(-0.5f, 0.5f),
                    new Vector3(0.5f, 0.5f), new Vector3(0.5f, -0.5f)
                },
                uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
                triangles = new[] { 0, 1, 2, 2, 3, 0 }
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateMaterial()
        {
            return new Material(Shader.Find("Unlit/Transparent"));
        }

        private sealed class FakeProducer : RenderTextureProducer
        {
            internal Texture Texture;
            internal Rect UvRect;
            public override Texture OutputTexture => Texture;
            public override Rect OutputUvRect => UvRect;
            public override int OutputWidth => 4;
            public override int OutputHeight => 4;
            public override bool HasOutput => Texture != null;
            public override long OutputVersion => 0;
        }
    }
}

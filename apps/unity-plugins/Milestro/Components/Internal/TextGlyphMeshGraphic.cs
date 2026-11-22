using System;
using Milestro.Skia;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Components.Internal
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class TextGlyphMeshGraphic : MaskableGraphic
    {
        [NonSerialized] private TextGlyphMeshOutput? output;

        internal void SetOutput(TextGlyphMeshOutput? next, Color tint)
        {
            color = tint;
            if (ReferenceEquals(output, next))
            {
                return;
            }
            output = next;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        public override Texture mainTexture => output?.Texture != null ? output.Texture : s_WhiteTexture;
        public override Material material
        {
            get => TextGlyphAtlas.Material!;
            set => base.material = value;
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            if (output == null)
            {
                vertices.Clear();
                return;
            }
            output.Populate(vertices, GetPixelAdjustedRect(), color);
        }
    }
}

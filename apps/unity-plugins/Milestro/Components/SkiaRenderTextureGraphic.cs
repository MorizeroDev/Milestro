using System;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Components
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public abstract class SkiaRenderTextureGraphic : MaskableGraphic
    {
        [NonSerialized] private Texture? texture;
        [NonSerialized] private Rect uvRect = new Rect(0, 0, 1, 1);
        [NonSerialized] private bool warnedSiblingGraphic;

        protected SkiaRenderTextureGraphic()
        {
            useLegacyMeshGeneration = false;
        }

        public Texture? Texture
        {
            get => texture;
            set
            {
                if (texture == value)
                {
                    return;
                }

                texture = value;
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }

        public Rect UvRect
        {
            get => uvRect;
            set
            {
                if (uvRect == value)
                {
                    return;
                }

                uvRect = value;
                SetVerticesDirty();
            }
        }

        public override Texture mainTexture
        {
            get
            {
                if (texture == null)
                {
                    if (material != null && material.mainTexture != null)
                    {
                        return material.mainTexture;
                    }

                    return s_WhiteTexture;
                }

                return texture;
            }
        }

        protected override void OnEnable()
        {
            EnsureInitializedGraphicColor();
            WarnSiblingGraphics();
            base.OnEnable();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var targetTexture = mainTexture;
            if (targetTexture == null)
            {
                return;
            }

            var rect = GetPixelAdjustedRect();
            var bounds = new Vector4(rect.x, rect.y, rect.x + rect.width, rect.y + rect.height);
            var texelSize = targetTexture.texelSize;
            var scaleX = targetTexture.width * texelSize.x;
            var scaleY = targetTexture.height * texelSize.y;
            var color32 = color;

            vh.AddVert(new Vector3(bounds.x, bounds.y), color32,
                new Vector4(uvRect.xMin * scaleX, uvRect.yMin * scaleY));
            vh.AddVert(new Vector3(bounds.x, bounds.w), color32,
                new Vector4(uvRect.xMin * scaleX, uvRect.yMax * scaleY));
            vh.AddVert(new Vector3(bounds.z, bounds.w), color32,
                new Vector4(uvRect.xMax * scaleX, uvRect.yMax * scaleY));
            vh.AddVert(new Vector3(bounds.z, bounds.y), color32,
                new Vector4(uvRect.xMax * scaleX, uvRect.yMin * scaleY));

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        public override void SetNativeSize()
        {
            var targetTexture = mainTexture;
            if (targetTexture == null)
            {
                return;
            }

            var width = Mathf.RoundToInt(targetTexture.width * uvRect.width);
            var height = Mathf.RoundToInt(targetTexture.height * uvRect.height);
            rectTransform.anchorMax = rectTransform.anchorMin;
            rectTransform.sizeDelta = new Vector2(width, height);
        }

        private void EnsureInitializedGraphicColor()
        {
            if (color.a > 0.0f)
            {
                return;
            }

            var restoredColor = color == default ? Color.white : new Color(color.r, color.g, color.b, 1.0f);
            color = restoredColor;
            Debug.LogWarning(
                "Initialized Milestro render texture Graphic alpha to 1. Migrated components can otherwise inherit a transparent tint and hide the rendered texture.",
                this);
        }

        private void WarnSiblingGraphics()
        {
            if (warnedSiblingGraphic)
            {
                return;
            }

            var graphics = GetComponents<Graphic>();
            foreach (var graphic in graphics)
            {
                if (graphic == null || graphic == this || !graphic.enabled)
                {
                    continue;
                }

                warnedSiblingGraphic = true;
                Debug.LogWarning(
                    "Another enabled Graphic component on the same GameObject can overwrite the Milestro render texture: " +
                    graphic.GetType().Name,
                    this);
                return;
            }
        }
    }
}

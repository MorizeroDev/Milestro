using System;
using Milestro.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Components.Internal
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public abstract class RenderTextureGraphic : MaskableGraphic
    {
        [NonSerialized] private Texture? texture;
        [NonSerialized] private Rect uvRect = new Rect(0, 0, 1, 1);
        [NonSerialized] private bool warnedSiblingGraphic;

        [SerializeField]
        private bool m_enableColorGradient;

        [SerializeField]
        private TextGradient m_colorGradient = TextGradient.White;

        [SerializeField]
        private TextGradientAsset? m_colorGradientPreset;

#if !UNITY_6000_6_OR_NEWER
        protected RenderTextureGraphic()
        {
            // Legacy mesh generation was removed in Unity 6.6, where the flag is obsolete and
            // Graphic already defaults it to false.
            useLegacyMeshGeneration = false;
        }
#endif

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

        public bool enableColorGradient
        {
            get => m_enableColorGradient;
            set
            {
                if (m_enableColorGradient == value)
                {
                    return;
                }

                m_enableColorGradient = value;
                SetVerticesDirty();
            }
        }

        public TextGradient colorGradient
        {
            get => m_colorGradient;
            set
            {
                if (m_colorGradient == value)
                {
                    return;
                }

                m_colorGradient = value;
                SetVerticesDirty();
            }
        }

        public TextGradientAsset? colorGradientPreset
        {
            get => m_colorGradientPreset;
            set
            {
                if (ReferenceEquals(m_colorGradientPreset, value))
                {
                    return;
                }

                m_colorGradientPreset = value;
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
            var rect = GetPixelAdjustedRect();
            PopulateDisplayMesh(vh, rect, rect);
        }

        protected void PopulateDisplayMesh(VertexHelper vh, Rect displayRect, Rect gradientRect)
        {
            vh.Clear();

            var targetTexture = Texture;
            if (targetTexture == null)
            {
                return;
            }

            var bounds = new Vector4(displayRect.xMin, displayRect.yMin, displayRect.xMax, displayRect.yMax);
            var texelSize = targetTexture.texelSize;
            var scaleX = targetTexture.width * texelSize.x;
            var scaleY = targetTexture.height * texelSize.y;

            if (!m_enableColorGradient)
            {
                PopulateUniformQuad(vh, bounds, color, scaleX, scaleY);
                return;
            }

            var gradient = m_colorGradientPreset != null
                ? m_colorGradientPreset.Gradient
                : m_colorGradient;
            var gradientBounds = new Vector4(
                Mathf.InverseLerp(gradientRect.xMin, gradientRect.xMax, displayRect.xMin),
                Mathf.InverseLerp(gradientRect.yMin, gradientRect.yMax, displayRect.yMin),
                Mathf.InverseLerp(gradientRect.xMin, gradientRect.xMax, displayRect.xMax),
                Mathf.InverseLerp(gradientRect.yMin, gradientRect.yMax, displayRect.yMax));
            if (gradient.Mode == TextGradientMode.FourCorners)
            {
                PopulateFourCornersQuad(vh, bounds, color, gradient, gradientBounds, scaleX, scaleY);
                return;
            }

            PopulateGradientGrid(vh, bounds, color, gradient, gradientBounds, scaleX, scaleY);
        }

        private void PopulateUniformQuad(VertexHelper vh, Vector4 bounds, Color baseColor, float scaleX,
            float scaleY)
        {
            var color32 = (Color32)baseColor;
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

        private void PopulateFourCornersQuad(VertexHelper vh, Vector4 bounds, Color baseColor,
            in TextGradient gradient, Vector4 gradientBounds, float scaleX, float scaleY)
        {
            vh.AddVert(new Vector3(bounds.x, bounds.y),
                baseColor * SampleGradient(gradient, gradientBounds.x, gradientBounds.y),
                new Vector4(uvRect.xMin * scaleX, uvRect.yMin * scaleY));
            vh.AddVert(new Vector3(bounds.x, bounds.w),
                baseColor * SampleGradient(gradient, gradientBounds.x, gradientBounds.w),
                new Vector4(uvRect.xMin * scaleX, uvRect.yMax * scaleY));
            vh.AddVert(new Vector3(bounds.z, bounds.w),
                baseColor * SampleGradient(gradient, gradientBounds.z, gradientBounds.w),
                new Vector4(uvRect.xMax * scaleX, uvRect.yMax * scaleY));
            vh.AddVert(new Vector3(bounds.z, bounds.y),
                baseColor * SampleGradient(gradient, gradientBounds.z, gradientBounds.y),
                new Vector4(uvRect.xMax * scaleX, uvRect.yMin * scaleY));

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        private void PopulateGradientGrid(VertexHelper vh, Vector4 bounds, Color baseColor,
            in TextGradient gradient, Vector4 gradientBounds, float scaleX, float scaleY)
        {
            var resolution = Mathf.Clamp(gradient.MeshResolution,
                TextGradient.MinMeshResolution,
                TextGradient.MaxMeshResolution);
            var stride = resolution + 1;
            for (var row = 0; row <= resolution; ++row)
            {
                var v = (float)row / resolution;
                var y = Mathf.Lerp(bounds.y, bounds.w, v);
                var uvY = Mathf.Lerp(uvRect.yMin, uvRect.yMax, v) * scaleY;
                var gradientV = Mathf.Lerp(gradientBounds.y, gradientBounds.w, v);
                for (var column = 0; column <= resolution; ++column)
                {
                    var u = (float)column / resolution;
                    var x = Mathf.Lerp(bounds.x, bounds.z, u);
                    var uvX = Mathf.Lerp(uvRect.xMin, uvRect.xMax, u) * scaleX;
                    var gradientU = Mathf.Lerp(gradientBounds.x, gradientBounds.z, u);
                    vh.AddVert(new Vector3(x, y),
                        baseColor * SampleGradient(gradient, gradientU, gradientV),
                        new Vector4(uvX, uvY));
                }
            }

            for (var row = 0; row < resolution; ++row)
            {
                for (var column = 0; column < resolution; ++column)
                {
                    var bottomLeft = row * stride + column;
                    var topLeft = bottomLeft + stride;
                    var topRight = topLeft + 1;
                    var bottomRight = bottomLeft + 1;
                    vh.AddTriangle(bottomLeft, topLeft, topRight);
                    vh.AddTriangle(topRight, bottomRight, bottomLeft);
                }
            }
        }

        internal static Color SampleGradient(in TextGradient gradient, float u, float v)
        {
            switch (gradient.Mode)
            {
                case TextGradientMode.Linear:
                    return EvaluateStops(gradient, LinearGradientParameter(u, v, gradient.AngleDegrees));
                case TextGradientMode.Radial:
                    {
                        var center = gradient.RadialCenter;
                        var dx = u - center.x;
                        var dy = v - center.y;
                        var maxDistance = MaxRadialDistance(center);
                        var t = maxDistance > 1e-6f
                            ? Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / maxDistance)
                            : 0f;
                        return EvaluateStops(gradient, t);
                    }
                default:
                    {
                        var corners = gradient.FourCorners;
                        var top = Color.Lerp(corners.TopLeft, corners.TopRight, u);
                        var bottom = Color.Lerp(corners.BottomLeft, corners.BottomRight, u);
                        return Color.Lerp(bottom, top, v);
                    }
            }
        }

        private static Color EvaluateStops(in TextGradient gradient, float t)
        {
            return gradient.Stops != null ? gradient.Stops.Evaluate(Mathf.Clamp01(t)) : Color.white;
        }

        // Office-style linear gradient: the angle increases clockwise with 0
        // filling left to right and 90 filling top to bottom. The parameter range
        // is scaled so the first and last stops touch the extreme corners of the
        // quad along the gradient axis, matching Office's "scaled" fill.
        internal static float LinearGradientParameter(float u, float v, float angleDegrees)
        {
            var radians = angleDegrees * Mathf.Deg2Rad;
            var directionX = Mathf.Cos(radians);
            var directionY = -Mathf.Sin(radians);
            var min = Mathf.Min(0f, directionX, directionY, directionX + directionY);
            var max = Mathf.Max(0f, directionX, directionY, directionX + directionY);
            if (max - min < 1e-6f)
            {
                return 0f;
            }

            return Mathf.Clamp01((u * directionX + v * directionY - min) / (max - min));
        }

        private static float MaxRadialDistance(Vector2 center)
        {
            var maxX = Mathf.Max(center.x, 1f - center.x);
            var maxY = Mathf.Max(center.y, 1f - center.y);
            return Mathf.Sqrt(maxX * maxX + maxY * maxY);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif

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

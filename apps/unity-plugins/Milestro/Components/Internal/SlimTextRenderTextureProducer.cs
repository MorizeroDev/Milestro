using System;
using Milestro.Model;
using Milestro.Skia;
using Milestro.Util;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Milestro.Components.Internal
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Milestro/Internal/Slim Text Render Texture Producer")]
    public class SlimTextRenderTextureProducer : RenderTextureProducer
    {
        private const string DefaultFontFamily = "Source Han Sans VF";

        [SerializeField] private string m_text = "";
        [SerializeField] private string m_fontFamily = DefaultFontFamily;
        [SerializeField, Range(0, 1000)] private int m_fontWeight = FontWeight.Normal;
        [SerializeField] private float m_fontSize = 24f;
        [SerializeField] private Color m_color = Color.white;
        [SerializeField] private Vector2 m_padding = Vector2.zero;
        [SerializeField] private bool m_fallbackToSystemFont = true;

        [NonSerialized] private RectTransform? rectTransformCache;
        [NonSerialized] private SlimTextRenderTarget? renderTarget;
        [NonSerialized] private ColorSpace? m_colorSpaceOverride;
#if UNITY_EDITOR
        [NonSerialized] private bool m_editorRebuildQueued;
#endif

        public override Texture? OutputTexture => renderTarget?.OutputTexture;
        public override Rect OutputUvRect => renderTarget?.OutputUvRect ?? new Rect(0f, 0f, 1f, 1f);
        public override int OutputWidth => renderTarget?.OutputWidth ?? 0;
        public override int OutputHeight => renderTarget?.OutputHeight ?? 0;
        public override bool HasOutput => renderTarget?.HasOutput ?? false;
        public override long OutputVersion => renderTarget?.OutputVersion ?? 0;

        public string text
        {
            get => m_text;
            set
            {
                SetManagedStringText(value);
            }
        }

        public string fontFamily
        {
            get => m_fontFamily;
            set
            {
                var nextValue = value ?? "";
                if (m_fontFamily == nextValue)
                {
                    return;
                }

                m_fontFamily = nextValue;
                MarkPropertiesChanged();
            }
        }

        public int fontWeight
        {
            get => m_fontWeight;
            set
            {
                var nextValue = NormalizeFontWeight(value);
                if (m_fontWeight == nextValue)
                {
                    return;
                }

                m_fontWeight = nextValue;
                MarkPropertiesChanged();
            }
        }

        public float fontSize
        {
            get => m_fontSize;
            set
            {
                var nextValue = NormalizeFontSize(value);
                if (Mathf.Approximately(m_fontSize, nextValue))
                {
                    return;
                }

                m_fontSize = nextValue;
                MarkPropertiesChanged();
            }
        }

        public Color color
        {
            get => m_color;
            set
            {
                if (m_color == value)
                {
                    return;
                }

                m_color = value;
                MarkPaintChanged();
            }
        }

        public Vector2 padding
        {
            get => m_padding;
            set
            {
                var nextValue = NormalizePadding(value);
                if (m_padding == nextValue)
                {
                    return;
                }

                m_padding = nextValue;
                MarkPaintChanged();
            }
        }

        public bool fallbackToSystemFont
        {
            get => m_fallbackToSystemFont;
            set
            {
                if (m_fallbackToSystemFont == value)
                {
                    return;
                }

                m_fallbackToSystemFont = value;
                MarkPropertiesChanged();
            }
        }

        public bool srgb
        {
            get => SurfaceColorSpace() == ColorSpace.Linear;
            set
            {
                m_colorSpaceOverride = value ? ColorSpace.Linear : ColorSpace.Gamma;
                MarkPaintChanged();
            }
        }

        protected virtual void OnEnable()
        {
            rectTransformCache = GetComponent<RectTransform>();
            MarkPropertiesChanged();
            RebuildResources();
        }

        protected virtual void OnDisable()
        {
            renderTarget?.Dispose();
            renderTarget = null;
        }

        private void Update()
        {
            RebuildResources();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            NormalizeSerializedValues();
            MarkPropertiesChanged();
            if (isActiveAndEnabled)
            {
                QueueEditorRebuild();
            }
        }
#endif

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            MarkPaintChanged();
#if UNITY_EDITOR
            QueueEditorRebuild();
#endif
        }

        public void RebuildOutput(bool forceStyle)
        {
            if (forceStyle)
            {
                MarkPropertiesChanged();
            }

            RebuildResources();
        }

        public void SetManagedStringText(string? value)
        {
            var nextValue = value ?? "";
            var changed = m_text != nextValue;
            m_text = nextValue;
            if (RenderTarget.UseManagedStringText() || changed)
            {
                MarkPaintChanged();
            }
        }

        internal void SyncTextWithoutModeSwitch(string? value)
        {
            var nextValue = value ?? "";
            if (m_text == nextValue)
            {
                return;
            }

            m_text = nextValue;
            MarkPaintChanged();
        }

        public void PrepareTextUtf8NoAlloc(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            NormalizeSerializedValues();
            var target = RenderTarget;
            target.EnsureNoAllocCapacity(capacity);
            target.Rebuild(CurrentSize(), SurfaceColorSpace(), CurrentSettings());
        }

        public void SetTextUtf8NoAlloc(byte[] buffer, int offset, int length)
        {
            NormalizeSerializedValues();
            RenderTarget.SetTextUtf8NoAlloc(CurrentSize(),
                SurfaceColorSpace(),
                CurrentSettings(),
                buffer,
                offset,
                length);
        }

#if UNITY_EDITOR
        private void QueueEditorRebuild()
        {
            if (Application.isPlaying || m_editorRebuildQueued)
            {
                return;
            }

            m_editorRebuildQueued = true;
            EditorApplication.delayCall += RebuildResourcesFromEditorDelayCall;
        }

        private void RebuildResourcesFromEditorDelayCall()
        {
            m_editorRebuildQueued = false;
            if (!this || !isActiveAndEnabled)
            {
                return;
            }

            RebuildResources();
        }
#endif

        private void RebuildResources()
        {
            NormalizeSerializedValues();
            RenderTarget.Rebuild(CurrentSize(),
                SurfaceColorSpace(),
                CurrentSettings());
        }

        private Vector2Int CurrentSize()
        {
            var rectTransform = RectTransformComponent();
            if (rectTransform == null)
            {
                return Vector2Int.one;
            }

            var rect = rectTransform.rect;
            return new Vector2Int(Mathf.Max(1, Mathf.CeilToInt(rect.width)),
                Mathf.Max(1, Mathf.CeilToInt(rect.height)));
        }

        private ColorSpace SurfaceColorSpace()
        {
            return m_colorSpaceOverride ?? UnitySkiaRenderTextureDescriptor.DefaultColorSpace;
        }

        private SlimTextRenderTargetSettings CurrentSettings()
        {
            return new SlimTextRenderTargetSettings(m_text,
                m_fontFamily,
                m_fontWeight,
                m_fontSize,
                m_color,
                m_padding,
                m_fallbackToSystemFont);
        }

        private RectTransform? RectTransformComponent()
        {
            if (rectTransformCache == null)
            {
                rectTransformCache = GetComponent<RectTransform>();
            }

            return rectTransformCache;
        }

        private SlimTextRenderTarget RenderTarget
        {
            get
            {
                if (renderTarget == null)
                {
                    renderTarget = new SlimTextRenderTarget();
                }

                return renderTarget;
            }
        }

        private void MarkPropertiesChanged()
        {
            RenderTarget.MarkPropertiesChanged();
#if UNITY_EDITOR
            if (isActiveAndEnabled)
            {
                QueueEditorRebuild();
            }
#endif
        }

        private void MarkPaintChanged()
        {
            RenderTarget.MarkPaintChanged();
#if UNITY_EDITOR
            if (isActiveAndEnabled)
            {
                QueueEditorRebuild();
            }
#endif
        }

        private void NormalizeSerializedValues()
        {
            if (m_text == null)
            {
                m_text = "";
            }

            if (m_fontFamily == null)
            {
                m_fontFamily = "";
            }

            m_fontWeight = NormalizeFontWeight(m_fontWeight);
            m_fontSize = NormalizeFontSize(m_fontSize);
            m_padding = NormalizePadding(m_padding);
        }

        private static int NormalizeFontWeight(int weight)
        {
            return Mathf.Clamp(weight, FontWeight.Invisible, FontWeight.ExtraBlack);
        }

        private static float NormalizeFontSize(float fontSize)
        {
            return FloatUtil.IsFinite(fontSize) ? Mathf.Max(1f, fontSize) : 1f;
        }

        private static Vector2 NormalizePadding(Vector2 padding)
        {
            return new Vector2(FloatUtil.IsFinite(padding.x) ? Mathf.Max(0f, padding.x) : 0f,
                FloatUtil.IsFinite(padding.y) ? Mathf.Max(0f, padding.y) : 0f);
        }
    }
}

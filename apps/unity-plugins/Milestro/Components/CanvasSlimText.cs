using System;
using Milestro.Components.Internal;
using Milestro.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Components
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(RawImage))]
    [RequireComponent(typeof(SlimTextRenderTextureProducer))]
    [AddComponentMenu("Milestro/Canvas Slim Text")]
    public sealed class CanvasSlimText : MonoBehaviour, IClipper
    {
        [NonSerialized] private RawImage image;
        [NonSerialized] private SlimTextRenderTextureProducer producer;
        [NonSerialized] private long observedOutputVersion = long.MinValue;
        [NonSerialized] private TextGlyphMeshGraphic? glyphGraphic;
        [NonSerialized] private bool observedGlyphMeshCapability;
        [NonSerialized] private bool outputApplyDeferred;
        [NonSerialized] private bool deferredForceApply;
        [NonSerialized] private bool clipperRegistered;
        [NonSerialized] private bool applyingDeferredOutput;

        internal bool SupportsGlyphMesh => !Image.raycastTarget && GetComponent<Mask>() == null &&
            GetComponent<RectMask2D>() == null &&
            Image.material == Image.defaultMaterial && Image.color.a >= 1f &&
            Image.canvasRenderer.GetAlpha() >= 1f && Image.canvasRenderer.GetInheritedAlpha() >= 1f &&
            !TextGlyphPresentationSupport.HasSoftRectMask(Image);

        public string Text
        {
            get => Producer.text;
            set => SetContent(value, TextColor);
        }

        public Color TextColor
        {
            get => Producer.color;
            set => SetContent(Text, value);
        }

        public void SetContent(string content, Color color)
        {
            var target = Producer;
            var nextContent = content ?? string.Empty;
            var textChanged = target.text != nextContent;
            var colorChanged = target.color != color;
            if (!textChanged && !colorChanged)
            {
                return;
            }

            if (textChanged)
            {
                target.SetManagedStringText(nextContent);
            }

            if (colorChanged)
            {
                target.color = color;
            }

            target.RequestPreparation();
        }

        public void Configure(
            string content,
            float fontSize,
            SlimTextHorizontalAlign horizontalAlignment,
            Color color)
        {
            var target = Producer;
            target.fontFamily = SlimTextRenderTextureProducer.DefaultFontFamily;
            target.fontSize = fontSize;
            target.color = color;
            target.horizontalAlign = horizontalAlignment;
            target.verticalAlign = SlimTextVerticalAlign.Center;
            target.fallbackToSystemFont = true;
            target.SetManagedStringText(content);

            Image.raycastTarget = false;
            Image.color = Color.white;
            target.RequestPreparation();
        }

        private RawImage Image
        {
            get
            {
                if (image == null)
                {
                    image = GetComponent<RawImage>();
                }

                return image;
            }
        }

        private SlimTextRenderTextureProducer Producer
        {
            get
            {
                if (producer == null)
                {
                    producer = GetComponent<SlimTextRenderTextureProducer>();
                }

                return producer;
            }
        }

        private void OnEnable()
        {
            observedOutputVersion = long.MinValue;
            observedGlyphMeshCapability = SupportsGlyphMesh;
            UnityEngine.Canvas.preWillRenderCanvases += SynchronizeGlyphPresentation;
            Producer.OutputChanged += OnProducerOutputChanged;
            Producer.RequestPreparation();
            ApplyProducerOutput(force: true);
        }

        private void OnDisable()
        {
            UnityEngine.Canvas.preWillRenderCanvases -= SynchronizeGlyphPresentation;
            ClipperRegistry.Unregister(this);
            clipperRegistered = false;
            outputApplyDeferred = false;
            deferredForceApply = false;
            if (glyphGraphic != null)
            {
                glyphGraphic.SetOutput(null, Color.white);
                glyphGraphic.enabled = false;
            }
            if (producer != null)
            {
                producer.OutputChanged -= OnProducerOutputChanged;
            }
            if (image != null)
            {
                image.enabled = false;
                image.texture = null;
                image.uvRect = new Rect(0, 0, 1, 1);
            }

            observedOutputVersion = long.MinValue;
        }

        private void OnProducerOutputChanged()
        {
            if (isActiveAndEnabled)
            {
                ApplyProducerOutput(force: false);
            }
        }

        private void SynchronizeGlyphPresentation()
        {
            if (clipperRegistered && !outputApplyDeferred && !applyingDeferredOutput)
            {
                ClipperRegistry.Unregister(this);
                clipperRegistered = false;
            }
            var capability = SupportsGlyphMesh;
            if (observedGlyphMeshCapability != capability)
            {
                observedGlyphMeshCapability = capability;
                Producer.RequestPreparation();
            }
            if (glyphGraphic == null || !glyphGraphic.enabled)
            {
                return;
            }
            var outputImage = Image;
            glyphGraphic.color = outputImage.color;
            glyphGraphic.maskable = outputImage.maskable;
            glyphGraphic.canvasRenderer.SetColor(outputImage.canvasRenderer.GetColor());
            glyphGraphic.gameObject.layer = gameObject.layer;
        }

        private void OnDestroy()
        {
            if (glyphGraphic == null)
            {
                return;
            }
            glyphGraphic.SetOutput(null, Color.white);
            if (Application.isPlaying)
            {
                Destroy(glyphGraphic.gameObject);
            }
            else
            {
                DestroyImmediate(glyphGraphic.gameObject);
            }
        }

        private void ApplyProducerOutput(bool force)
        {
            if (CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics())
            {
                outputApplyDeferred = true;
                deferredForceApply |= force;
                if (!clipperRegistered)
                {
                    ClipperRegistry.Register(this);
                    clipperRegistered = true;
                }
                return;
            }
            force |= deferredForceApply;
            outputApplyDeferred = false;
            deferredForceApply = false;
            var target = Producer;
            var outputImage = Image;
            var glyphOutput = target.GlyphMeshOutput;
            if (glyphOutput?.IsValid == true && target.HasOutput)
            {
                if (glyphGraphic == null)
                {
                    var child = new GameObject("Milestro Glyph Mesh", typeof(RectTransform),
                        typeof(CanvasRenderer), typeof(LayoutElement), typeof(TextGlyphMeshGraphic));
                    child.hideFlags = HideFlags.DontSave;
                    child.layer = gameObject.layer;
                    child.transform.SetParent(transform, false);
                    child.transform.SetAsFirstSibling();
                    child.GetComponent<LayoutElement>().ignoreLayout = true;
                    var childRect = (RectTransform)child.transform;
                    childRect.anchorMin = Vector2.zero;
                    childRect.anchorMax = Vector2.one;
                    childRect.offsetMin = Vector2.zero;
                    childRect.offsetMax = Vector2.zero;
                    glyphGraphic = child.GetComponent<TextGlyphMeshGraphic>();
                    glyphGraphic.raycastTarget = false;
                }
                glyphGraphic.enabled = true;
                glyphGraphic.SetOutput(glyphOutput.Output, outputImage.color);
                SynchronizeGlyphPresentation();
                outputImage.enabled = false;
                outputImage.texture = null;
                observedOutputVersion = target.OutputVersion;
                return;
            }
            if (glyphGraphic != null)
            {
                glyphGraphic.SetOutput(null, Color.white);
                glyphGraphic.enabled = false;
            }
            if (!target.HasOutput || target.OutputTexture == null)
            {
                outputImage.enabled = false;
                if (force || outputImage.texture != null)
                {
                    outputImage.texture = null;
                    outputImage.uvRect = new Rect(0, 0, 1, 1);
                }

                observedOutputVersion = long.MinValue;
                return;
            }

            outputImage.enabled = true;
            if (!force &&
                observedOutputVersion == target.OutputVersion &&
                outputImage.texture == target.OutputTexture &&
                outputImage.uvRect == target.OutputUvRect)
            {
                return;
            }

            outputImage.texture = target.OutputTexture;
            outputImage.uvRect = target.OutputUvRect;
            observedOutputVersion = target.OutputVersion;
        }

        public void PerformClipping()
        {
            if (!outputApplyDeferred || !isActiveAndEnabled)
            {
                return;
            }
            var force = deferredForceApply;
            outputApplyDeferred = false;
            deferredForceApply = false;
            applyingDeferredOutput = true;
            try
            {
                ApplyProducerOutput(force);
                RefreshParentRectMasks();
            }
            finally
            {
                applyingDeferredOutput = false;
            }
        }

        private void RefreshParentRectMasks()
        {
            var ancestor = glyphGraphic != null && glyphGraphic.enabled ? transform : transform.parent;
            while (ancestor != null)
            {
                var mask = ancestor.GetComponent<RectMask2D>();
                if (mask != null && mask.isActiveAndEnabled)
                {
                    mask.PerformClipping();
                }
                ancestor = ancestor.parent;
            }
        }
    }
}

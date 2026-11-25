using System.Collections.Generic;
using Milestro.Components.Internal;
using UnityEngine;

namespace Milestro.Components
{
    public partial class TextBox
    {
        private readonly List<InlineImageGraphic> inlineImageViews = new List<InlineImageGraphic>();
        private InlineImageLayout? inlineImageLayout;

        public void RefreshInlineImages()
        {
            ClearInlineImageLayout();
            ProducerComponent().RefreshInlineImages();
        }

        private void LateUpdate()
        {
            // Flow mode is driven by TextBoxLayoutElement, which presents images
            // after publishing the visible range for the current frame.
            if (flowModeActive)
            {
                return;
            }

            RefreshInlineImagePresentation();
        }

        internal void RefreshInlineImagePresentation()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            var producer = ProducerComponent();
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: false);
            if (producer.TryGetContentSnapshot(out var currentContent) &&
                producer.TryGetInlineImageGeometrySnapshot(out var snapshot) &&
                currentContent.ContentVersion == snapshot.ContentVersion)
            {
                var layout = InlineImageLayout.From(snapshot);
                inlineImageLayout = layout;
                PresentInlineImages(layout);
                return;
            }

            if (inlineImageLayout is { } cached)
            {
                if (!ProducerComponent().TryGetContentSnapshot(out var content) ||
                    content.ContentVersion == cached.ContentVersion)
                {
                    PresentInlineImages(cached);
                    return;
                }
            }

            HideInlineImages();
        }

        private void PresentInlineImages(InlineImageLayout layout)
        {
            var used = 0;
            foreach (var item in layout.Placeholders)
            {
                var style = item.Presentation;
                var sprite = style?.Binding?.Sprite;
                if (style == null || !sprite || !item.Visible || !style.Visible) continue;
                if (used == inlineImageViews.Count)
                {
                    var viewObject = new GameObject("Milestro Inline Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(InlineImageGraphic));
                    viewObject.hideFlags = HideFlags.HideAndDontSave;
                    viewObject.transform.SetParent(transform, false);
                    var view = viewObject.GetComponent<InlineImageGraphic>();
                    view.raycastTarget = false;
                    view.rectTransform.anchorMin = Vector2.zero;
                    view.rectTransform.anchorMax = Vector2.one;
                    view.rectTransform.offsetMin = Vector2.zero;
                    view.rectTransform.offsetMax = Vector2.zero;
                    view.rectTransform.pivot = rectTransform.pivot;
                    inlineImageViews.Add(view);
                }
                var graphic = inlineImageViews[used++];
                var outputRect = OffsetRect(ScaleRect(item.Rect, layout.FitScale), layout.PaintPosition);
                var box = OutputRectToLocal(outputRect, layout.VisibleOutputSize);
                var clip = OutputRectToLocal(layout.ClipRect, layout.VisibleOutputSize);
                var pixelsToLocal = layout.FitScale * GetPixelAdjustedRect().width / Mathf.Max(1, layout.VisibleOutputSize.x);
                graphic.Present(sprite!, box, clip, style, pixelsToLocal);
                graphic.gameObject.SetActive(true);
            }
            for (var index = used; index < inlineImageViews.Count; ++index)
            {
                inlineImageViews[index].Clear();
                inlineImageViews[index].gameObject.SetActive(false);
            }
        }

        private void HideInlineImages()
        {
            ClearInlineImageLayout();
            foreach (var view in inlineImageViews)
            {
                if (!view) continue;
                view.Clear();
                view.gameObject.SetActive(false);
            }
        }

        private void DestroyInlineImages()
        {
            ClearInlineImageLayout();
            foreach (var view in inlineImageViews)
            {
                if (!view) continue;
                view.Clear();
                if (Application.isPlaying) Destroy(view.gameObject);
                else DestroyImmediate(view.gameObject);
            }
            inlineImageViews.Clear();
        }

        private void ClearInlineImageLayout()
        {
            inlineImageLayout = null;
        }

        private readonly struct InlineImageLayout
        {
            private InlineImageLayout(long contentVersion,
                IReadOnlyList<TextBoxPlaceholderGeometry> placeholders,
                Vector2 paintPosition,
                Rect clipRect,
                Vector2Int visibleOutputSize,
                float fitScale)
            {
                ContentVersion = contentVersion;
                Placeholders = placeholders;
                PaintPosition = paintPosition;
                ClipRect = clipRect;
                VisibleOutputSize = visibleOutputSize;
                FitScale = fitScale > 0f && float.IsFinite(fitScale) ? fitScale : 1f;
            }

            internal long ContentVersion { get; }
            internal IReadOnlyList<TextBoxPlaceholderGeometry> Placeholders { get; }
            internal Vector2 PaintPosition { get; }
            internal Rect ClipRect { get; }
            internal Vector2Int VisibleOutputSize { get; }
            internal float FitScale { get; }

            internal static InlineImageLayout From(TextBoxLinkGeometrySnapshot snapshot)
            {
                return new InlineImageLayout(snapshot.ContentVersion,
                    snapshot.Placeholders,
                    snapshot.PaintPosition,
                    snapshot.ClipRect,
                    snapshot.VisibleOutputSize,
                    snapshot.FitScale);
            }
        }
    }
}

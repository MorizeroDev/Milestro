using Milestro.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Components.Internal
{
    internal static class MilestroDropdownDefaults
    {
        private const float ControlWidth = 240f;
        private const float ControlHeight = 40f;
        private const float ItemHeight = 32f;

        internal static MilestroDropdown Create(Transform? parent, bool activate = true)
        {
            var controlObject = CreateRect("Milestro Dropdown", parent);
            controlObject.gameObject.SetActive(false);
            controlObject.sizeDelta = new Vector2(ControlWidth, ControlHeight);
            var background = controlObject.gameObject.AddComponent<Image>();
            background.color = Color.white;

            var caption = CreateText("Caption", controlObject, string.Empty, 18f,
                SlimTextHorizontalAlign.Left, new Color(0.1f, 0.1f, 0.1f));
            SetInsets((RectTransform)caption.transform, new Vector2(12f, 0f), new Vector2(-36f, 0f));
            var placeholder = CreateText("Placeholder", controlObject, string.Empty, 18f,
                SlimTextHorizontalAlign.Left, new Color(0.4f, 0.4f, 0.4f));
            SetInsets((RectTransform)placeholder.transform, new Vector2(12f, 0f), new Vector2(-36f, 0f));
            var arrow = CreateText("Arrow", controlObject, "▼", 14f,
                SlimTextHorizontalAlign.Center, new Color(0.1f, 0.1f, 0.1f));
            var arrowRect = (RectTransform)arrow.transform;
            arrowRect.anchorMin = new Vector2(1f, 0f);
            arrowRect.anchorMax = Vector2.one;
            arrowRect.sizeDelta = new Vector2(28f, 0f);
            arrowRect.anchoredPosition = new Vector2(-18f, 0f);

            var template = CreateRect("Dropdown List Template", controlObject);
            template.sizeDelta = new Vector2(ControlWidth, 8f * ItemHeight + 4f);
            var templateBackground = template.gameObject.AddComponent<Image>();
            templateBackground.color = new Color(0.96f, 0.96f, 0.96f);
            var scrollRect = template.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = ItemHeight;

            var viewport = CreateRect("Viewport", template);
            SetInsets(viewport, new Vector2(2f, 2f), new Vector2(-22f, -2f));
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0.96f, 0.96f, 0.96f);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;

            var item = CreateRect("Item Template", content);
            item.anchorMin = new Vector2(0f, 1f);
            item.anchorMax = new Vector2(1f, 1f);
            item.pivot = new Vector2(0.5f, 1f);
            item.sizeDelta = new Vector2(0f, ItemHeight);
            var itemBackground = item.gameObject.AddComponent<Image>();
            itemBackground.color = Color.clear;
            itemBackground.raycastTarget = true;
            var itemText = CreateText("Item Text", item, string.Empty, 18f,
                SlimTextHorizontalAlign.Left, new Color(0.1f, 0.1f, 0.1f));
            SetInsets((RectTransform)itemText.transform, new Vector2(12f, 0f), new Vector2(-34f, 0f));
            var checkmark = CreateText("Checkmark", item, "✓", 18f,
                SlimTextHorizontalAlign.Center, new Color(0.1f, 0.1f, 0.1f));
            var checkmarkRect = (RectTransform)checkmark.transform;
            checkmarkRect.anchorMin = new Vector2(1f, 0f);
            checkmarkRect.anchorMax = Vector2.one;
            checkmarkRect.pivot = new Vector2(1f, 0.5f);
            checkmarkRect.sizeDelta = new Vector2(28f, 0f);
            checkmarkRect.anchoredPosition = new Vector2(-6f, 0f);
            var itemBehaviour = item.gameObject.AddComponent<MilestroDropdownItem>();
            itemBehaviour.ConfigureReferences(itemText, checkmark, itemBackground);
            item.gameObject.SetActive(false);

            var scrollbarRect = CreateRect("Scrollbar", template);
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = Vector2.one;
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.sizeDelta = new Vector2(16f, -4f);
            scrollbarRect.anchoredPosition = new Vector2(-2f, 0f);
            var scrollbarBackground = scrollbarRect.gameObject.AddComponent<Image>();
            scrollbarBackground.color = new Color(0.82f, 0.82f, 0.82f);
            var scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
            var slidingArea = CreateRect("Sliding Area", scrollbarRect);
            SetFullStretch(slidingArea);
            var handle = CreateRect("Handle", slidingArea);
            handle.sizeDelta = Vector2.zero;
            handle.anchoredPosition = Vector2.zero;
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.45f, 0.45f, 0.45f);
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.size = 1f;

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scrollRect.verticalScrollbarSpacing = 2f;

            var dropdown = controlObject.gameObject.AddComponent<MilestroDropdown>();
            dropdown.targetGraphic = background;
            dropdown.captionText = caption;
            dropdown.placeholderText = placeholder;
            dropdown.template = template;
            template.gameObject.SetActive(false);
            controlObject.gameObject.SetActive(activate);
            return dropdown;
        }

        private static CanvasSlimText CreateText(
            string name,
            Transform parent,
            string content,
            float fontSize,
            SlimTextHorizontalAlign alignment,
            Color color)
        {
            var rect = CreateRect(name, parent);
            rect.gameObject.AddComponent<RawImage>();
            rect.gameObject.AddComponent<SlimTextRenderTextureProducer>();
            var text = rect.gameObject.AddComponent<CanvasSlimText>();
            text.Configure(content, fontSize, alignment, color);
            return text;
        }

        private static RectTransform CreateRect(string name, Transform? parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = parent != null ? parent.gameObject.layer : 0;
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static void SetFullStretch(RectTransform target)
        {
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        private static void SetInsets(RectTransform target, Vector2 minimum, Vector2 maximum)
        {
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = minimum;
            target.offsetMax = maximum;
        }
    }
}

using System;
using Milestro.Components.Internal;
using Milestro.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Components
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(RawImage))]
    [RequireComponent(typeof(SlimTextRenderTextureProducer))]
    [AddComponentMenu("Milestro/Canvas Slim Text")]
    public sealed class CanvasSlimText : MonoBehaviour
    {
        [NonSerialized] private RawImage image;
        [NonSerialized] private SlimTextRenderTextureProducer producer;
        [NonSerialized] private long observedOutputVersion = long.MinValue;

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

            Refresh(force: true);
        }

        public void Configure(
            string content,
            float fontSize,
            SlimTextHorizontalAlign horizontalAlignment,
            Color color)
        {
            var target = Producer;
            target.fontFamily = "system-ui";
            target.fontSize = fontSize;
            target.color = color;
            target.horizontalAlign = horizontalAlignment;
            target.verticalAlign = SlimTextVerticalAlign.Center;
            target.fallbackToSystemFont = true;
            target.SetManagedStringText(content);

            Image.raycastTarget = false;
            Image.color = Color.white;
            Refresh(force: true);
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
            Refresh(force: true);
        }

        private void OnDisable()
        {
            if (image != null)
            {
                image.enabled = false;
                image.texture = null;
                image.uvRect = new Rect(0, 0, 1, 1);
            }

            observedOutputVersion = long.MinValue;
        }

        private void Update()
        {
            ApplyProducerOutput(force: false);
        }

        private void Refresh(bool force)
        {
            Producer.RebuildOutput(forceStyle: false);
            ApplyProducerOutput(force);
        }

        private void ApplyProducerOutput(bool force)
        {
            var target = Producer;
            var outputImage = Image;
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
    }
}

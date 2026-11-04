using System;
using Milestro.Model;
using UnityEngine;

namespace Milestro.Components
{
    public partial class TextBox
    {
        [Obsolete("Use Text instead.")]
        public string text
        {
            get => Text;
            set => Text = value;
        }

        [Obsolete("Use TextBoxRenderTextureProducer.textStyle.size instead.")]
        public float fontSize
        {
            get => ProducerComponent().textStyle.size;
            set => ProducerComponent().textStyle.size = value;
        }

        [Obsolete("Use TextBoxRenderTextureProducer.textStyle.textColor instead.")]
        public new Color color
        {
            get => ProducerComponent().textStyle.textColor;
            set => ProducerComponent().textStyle.textColor = value;
        }

        [Obsolete("Use TextBoxRenderTextureProducer.paragraphStyle.textAlign instead.")]
        public TextAlign alignment
        {
            get => ProducerComponent().paragraphStyle.textAlign;
            set => ProducerComponent().paragraphStyle.textAlign = value;
        }

        [Obsolete("Use TextBoxRenderTextureProducer.wrapMode instead. paragraphStyle.singleLine overrides wrapping.")]
        public bool enableWordWrapping
        {
            get => ProducerComponent().wrapMode == TextBoxWrapMode.Wrap;
            set => ProducerComponent().wrapMode = value ? TextBoxWrapMode.Wrap : TextBoxWrapMode.NoWrap;
        }

        [Obsolete("Use TextBoxRenderTextureProducer.textOverflow instead.")]
        public TextOverflow overflowMode
        {
            get => ProducerComponent().textOverflow;
            set => ProducerComponent().textOverflow = value;
        }

        [Obsolete("Use TextBoxRenderTextureProducer.margin instead.")]
        public Margin margin
        {
            get => ProducerComponent().margin;
            set => ProducerComponent().margin = value;
        }

        [Obsolete("Use TextBoxRenderTextureProducer.enableAutoFontSize instead.")]
        public bool enableAutoSizing
        {
            get => ProducerComponent().enableAutoFontSize;
            set => ProducerComponent().enableAutoFontSize = value;
        }

        [Obsolete("Use TextBoxRenderTextureProducer.fontSizeMin instead.")]
        public float fontSizeMin
        {
            get => ProducerComponent().fontSizeMin;
            set => ProducerComponent().fontSizeMin = value;
        }

        [Obsolete("Use TextBoxRenderTextureProducer.fontSizeMax instead.")]
        public float fontSizeMax
        {
            get => ProducerComponent().fontSizeMax;
            set => ProducerComponent().fontSizeMax = value;
        }
    }
}

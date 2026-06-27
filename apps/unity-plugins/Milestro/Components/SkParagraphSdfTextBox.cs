using System;
using Milestro.Skia.TextLayout;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Components
{
    public class SkParagraphSdfTextBox : MonoBehaviour
    {
        const int SK_DistanceFieldPad = 4;

        public Paragraph Paragraph { get; set; }

        [SerializeField] public Vector2 offsetPosition;

        [SerializeField] public float scale = 1;

        [SerializeField] public Color color = Color.white;

        [NonSerialized] private RectTransform rect;

        [NonSerialized] private Image img;

        private void OnEnable()
        {
            rect = GetComponent<RectTransform>();
            img = GetComponent<Image>();
        }

        public void RenderParagraph()
        {
            if (Paragraph == null)
            {
                return;
            }

            var payloadWidth = (int)(rect.rect.width * scale);
            var payloadHeight = (int)(rect.rect.height * scale);

            var dfWidth = payloadWidth + 2 * SK_DistanceFieldPad;
            var dfHeight = payloadHeight + 2 * SK_DistanceFieldPad;


            var sdfTex = new Texture2D(dfWidth, dfHeight, TextureFormat.Alpha8, false);
            unsafe
            {
                var ptr = sdfTex.GetPixelData<byte>(0).GetUnsafePtr();
                Paragraph.ToSDF(payloadWidth, payloadHeight, scale, offsetPosition.x, offsetPosition.y, ptr);
            }
        }
    }
}

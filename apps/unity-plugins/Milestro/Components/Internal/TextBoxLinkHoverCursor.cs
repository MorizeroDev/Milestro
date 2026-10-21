using UnityEngine;

namespace Milestro.Components.Internal
{
    internal static class TextBoxLinkHoverCursor
    {
        internal static readonly Vector2 Hotspot = new Vector2(9f, 1f);

        private static readonly string[] Pixels =
        {
            "        ##",
            "       #++#",
            "       #++#",
            "       #++#",
            "       #++#",
            "       #++#",
            "       #++#",
            "       #++###",
            "       #++#++###",
            "       #++#++#++##",
            "       #++#++#++#+#",
            "       #++#++#++#+#",
            "   ##  #++#++#++#+#",
            "  #++# #+++++++++#",
            "  #+++#++++++++++#",
            "   #++#++++++++++#",
            "    #++++++++++++#",
            "    #++++++++++++#",
            "     #+++++++++++#",
            "     #+++++++++++#",
            "      #++++++++++#",
            "      #+++++++++#",
            "       #++++++++#",
            "       #++++++++#",
            "       ##########",
        };

        internal static Texture2D Create()
        {
            const int size = 32;
            var colors = new Color32[size * size];
            for (var row = 0; row < Pixels.Length; ++row)
            {
                var line = Pixels[row];
                for (var column = 0; column < line.Length; ++column)
                {
                    if (line[column] == ' ')
                    {
                        continue;
                    }

                    var intensity = line[column] == '#' ? (byte)0 : (byte)255;
                    colors[(size - row - 2) * size + column] =
                        new Color32(intensity, intensity, intensity, 255);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                name = "Milestro link hand cursor",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(colors);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return texture;
        }
    }
}

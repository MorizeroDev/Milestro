using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Milestro.Skia;
using UnityEngine;

namespace Milestro.ResourceManagement
{
    public sealed class DataUriImageResourceProvider : IImageResourceProvider
    {
        public Task<InlineResourceBorrow?> BorrowAsync(ImageRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var source = request.Source;
            cancellationToken.ThrowIfCancellationRequested();
            var encoded = DecodeDataUri(source);
            using var image = DecodeImage(source, encoded);
            var pixels = image.ReadRgbaPixels();
            var stride = checked(image.Width * 4);
            var row = new byte[stride];
            for (var index = 0; index < image.Height / 2; ++index)
            {
                var opposite = image.Height - index - 1;
                Buffer.BlockCopy(pixels, index * stride, row, 0, stride);
                Buffer.BlockCopy(pixels, opposite * stride, pixels, index * stride, stride);
                Buffer.BlockCopy(row, 0, pixels, opposite * stride, stride);
            }
            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false);
            Sprite? sprite = null;
            try
            {
                texture.LoadRawTextureData(pixels);
                texture.Apply(false, true);
                sprite = Sprite.Create(texture, new Rect(0, 0, image.Width, image.Height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                return Task.FromResult<InlineResourceBorrow?>(InlineResourceBorrow.Create(new ImageResource(sprite), () =>
                {
                    DestroyResource(sprite);
                    DestroyResource(texture);
                }));
            }
            catch
            {
                DestroyResource(sprite);
                DestroyResource(texture);
                throw;
            }
        }

        private static MilestroImage DecodeImage(string source, byte[] encoded)
        {
            var end = source.IndexOfAny(new[] { ';', ',' }, 5);
            var mediaType = end >= 0 ? source.Substring(5, end - 5) : "";
            if (mediaType.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase))
            {
                using var svg = Svg.MakeFromBytes(encoded);
                return svg.Rasterize();
            }
            return MilestroImage.MakeFromBytes(encoded);
        }

        internal static byte[] DecodeDataUri(string source)
        {
            if (source == null || !source.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Expected an image Data URI.", nameof(source));
            var separator = source.IndexOf(',');
            if (separator < 0) throw new ArgumentException("Data URI is missing its comma.", nameof(source));
            var metadata = source.Substring(5, separator - 5);
            var payload = source.Substring(separator + 1);
            var bytes = new List<byte>();
            for (var index = 0; index < payload.Length; ++index)
            {
                if (payload[index] == '%')
                {
                    if (index + 2 >= payload.Length || !byte.TryParse(payload.Substring(index + 1, 2),
                            System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value))
                        throw new ArgumentException("Invalid Data URI percent escape.", nameof(source));
                    bytes.Add(value);
                    index += 2;
                }
                else
                {
                    if (payload[index] > 127) throw new ArgumentException("Data URI bytes must be percent encoded.", nameof(source));
                    bytes.Add((byte)payload[index]);
                }
            }
            return metadata.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)
                ? Convert.FromBase64String(Encoding.ASCII.GetString(bytes.ToArray()))
                : bytes.ToArray();
        }

        private static void DestroyResource(UnityEngine.Object? resource)
        {
            if (!resource) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(resource);
            else UnityEngine.Object.DestroyImmediate(resource);
        }
    }
}

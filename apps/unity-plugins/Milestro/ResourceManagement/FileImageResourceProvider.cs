using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Milestro.ResourceManagement
{
    public sealed class FileImageResourceProvider : IImageResourceProvider
    {
        public async Task<ImageResource?> AcquireAsync(string source, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || !uri.IsFile)
                throw new ArgumentException("File images require an absolute file:// URI.", nameof(source));
            cancellationToken.ThrowIfCancellationRequested();
            using var request = UnityWebRequestTexture.GetTexture(uri.AbsoluteUri, true);
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                await Task.Yield();
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException($"Image load failed: {request.error}");
            var texture = DownloadHandlerTexture.GetContent(request);
            try
            {
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                return new ImageResource(sprite, texture);
            }
            catch
            {
                DestroyResource(texture);
                throw;
            }
        }

        public void Release(ImageResource resource)
        {
            DestroyResource(resource.Sprite);
            if (resource.ReleaseToken is Texture texture) DestroyResource(texture);
        }

        private static void DestroyResource(UnityEngine.Object resource)
        {
            if (!resource) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(resource);
            else UnityEngine.Object.DestroyImmediate(resource);
        }
    }
}

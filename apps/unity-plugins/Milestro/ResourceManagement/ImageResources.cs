using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Milestro.ResourceManagement
{
    public static class ImageResources
    {
        private sealed class MissingProvider : IImageResourceProvider
        {
            public Task<ImageResource?> AcquireAsync(string source, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Debug.LogError($"No default image resource provider is configured for '{source}'.");
                return Task.FromResult<ImageResource?>(null);
            }

            public void Release(ImageResource resource) { }
        }

        public static ResourceProviderRouter<IImageResourceProvider> Providers { get; } = CreateRouter();

        private static ResourceProviderRouter<IImageResourceProvider> CreateRouter()
        {
            var router = new ResourceProviderRouter<IImageResourceProvider>(new MissingProvider());
            router.Register("file", new FileImageResourceProvider());
            router.Register("data", new DataUriImageResourceProvider());
            return router;
        }

        public static async Task<ImageResourceLease?> AcquireAsync(string source,
            CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            cancellationToken.ThrowIfCancellationRequested();
            var provider = Providers.Resolve(source.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? "data://" : source);
            var resource = await provider.AcquireAsync(source, cancellationToken);
            if (!resource.HasValue) return null;
            var lease = new ImageResourceLease(provider, resource.Value);
            if (cancellationToken.IsCancellationRequested)
            {
                lease.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (!resource.Value.Sprite)
            {
                lease.Dispose();
                throw new InvalidOperationException("Image provider returned an invalid Sprite.");
            }
            return lease;
        }
    }
}

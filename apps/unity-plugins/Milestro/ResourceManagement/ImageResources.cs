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
            public Task<InlineResourceBorrow?> BorrowAsync(ImageRequest request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Debug.LogError($"No default image resource provider is configured for '{request.Source}'.");
                return Task.FromResult<InlineResourceBorrow?>(null);
            }
        }

        public static ResourceProviderRouter<IImageResourceProvider> Providers { get; } = CreateRouter();

        private static ResourceProviderRouter<IImageResourceProvider> CreateRouter()
        {
            var router = new ResourceProviderRouter<IImageResourceProvider>(new MissingProvider());
            router.Register("file", new FileImageResourceProvider());
            router.Register("data", new DataUriImageResourceProvider());
            return router;
        }

        public static Task<InlineResourceBorrow?> BorrowAsync(string source,
            CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            cancellationToken.ThrowIfCancellationRequested();
            var provider = Providers.Resolve(source.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? "data://" : source);
            return InlineResourceBorrow.AcceptAsync(provider.BorrowAsync(new ImageRequest(source), cancellationToken), cancellationToken);
        }
    }
}

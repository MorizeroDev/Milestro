using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Milestro.ResourceManagement
{
    public static class SpriteResources
    {
        private sealed class MissingProvider : ISpriteResourceProvider
        {
            public Task<InlineResourceBorrow?> BorrowAsync(SpriteRequest request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Debug.LogError("No sprite resource provider is configured.");
                return Task.FromResult<InlineResourceBorrow?>(null);
            }
        }

        private static ISpriteResourceProvider provider = new MissingProvider();

        public static ISpriteResourceProvider Provider
        {
            get => provider;
            set => provider = value ?? throw new ArgumentNullException(nameof(value));
        }

        public static Task<InlineResourceBorrow?> BorrowAsync(SpriteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            var owner = Provider;
            return InlineResourceBorrow.AcceptAsync(owner.BorrowAsync(request, cancellationToken), cancellationToken);
        }
    }
}

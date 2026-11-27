using System;
using System.Threading;
using System.Threading.Tasks;

namespace Milestro.ResourceManagement
{
    public abstract class InlineResourceBorrow : IDisposable
    {
        private int disposed;

        protected InlineResourceBorrow(ImageResource resource)
        {
            Resource = resource;
        }

        public ImageResource Resource { get; }
        public bool IsDisposed => Volatile.Read(ref disposed) != 0;

        public static InlineResourceBorrow Create(ImageResource resource, Action onReturn) =>
            new SimpleInlineResourceBorrow(resource, onReturn);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0) ReturnCore();
        }

        protected abstract void ReturnCore();

        private sealed class SimpleInlineResourceBorrow : InlineResourceBorrow
        {
            private Action? onReturn;

            internal SimpleInlineResourceBorrow(ImageResource resource, Action onReturn) : base(resource)
            {
                this.onReturn = onReturn ?? throw new ArgumentNullException(nameof(onReturn));
            }

            protected override void ReturnCore()
            {
                var callback = onReturn!;
                onReturn = null;
                callback();
            }
        }

        internal static async Task<InlineResourceBorrow?> AcceptAsync(Task<InlineResourceBorrow?> pending,
            CancellationToken cancellationToken)
        {
            var borrow = await pending;
            if (cancellationToken.IsCancellationRequested)
            {
                borrow?.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (borrow == null) return null;
            if (borrow.IsDisposed || !borrow.Resource.Sprite)
            {
                borrow.Dispose();
                throw new InvalidOperationException("Provider returned an invalid or already returned Sprite borrow.");
            }
            return borrow;
        }
    }
}

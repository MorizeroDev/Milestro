using System;

namespace Milestro.ResourceManagement
{
    public sealed class ImageResourceLease : IDisposable
    {
        private bool disposed;

        internal ImageResourceLease(IImageResourceProvider provider, ImageResource resource)
        {
            Provider = provider;
            Resource = resource;
        }

        public IImageResourceProvider Provider { get; }
        public ImageResource Resource { get; }
        public bool IsDisposed => disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Provider.Release(Resource);
        }
    }
}

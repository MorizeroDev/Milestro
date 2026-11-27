using System;

namespace Milestro.ResourceManagement
{
    public sealed class ImageRequest
    {
        public ImageRequest(string source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public string Source { get; }
    }
}

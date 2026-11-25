using System.Threading;
using System.Threading.Tasks;

namespace Milestro.ResourceManagement
{
    public interface IImageResourceProvider
    {
        Task<ImageResource?> AcquireAsync(string source, CancellationToken cancellationToken);
        void Release(ImageResource resource);
    }
}

using System.Threading;
using System.Threading.Tasks;

namespace Milestro.ResourceManagement
{
    public interface IImageResourceProvider
    {
        Task<InlineResourceBorrow?> BorrowAsync(ImageRequest request, CancellationToken cancellationToken);
    }
}

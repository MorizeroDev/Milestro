using System.Threading;
using System.Threading.Tasks;

namespace Milestro.ResourceManagement
{
    public interface ISpriteResourceProvider
    {
        Task<InlineResourceBorrow?> BorrowAsync(SpriteRequest request, CancellationToken cancellationToken);
    }
}

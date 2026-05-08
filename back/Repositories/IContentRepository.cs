using WebApplication2.Models;

namespace WebApplication2.Repositories;

public interface IContentRepository
{
    Task<ContentItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ContentItem?> GetBySlugAsync(string slug, CancellationToken cancellationToken);

    Task<PagedResult<ContentItem>> ListAsync(ContentListQuery query, CancellationToken cancellationToken);

    Task<ContentItem> CreateAsync(ContentItem item, CancellationToken cancellationToken);

    Task<ContentItem?> UpdateAsync(ContentItem item, long? expectedVersion, CancellationToken cancellationToken);

    Task<ContentItem?> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

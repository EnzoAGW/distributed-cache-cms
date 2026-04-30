using WebApplication2.Models;

namespace WebApplication2.Services;

public interface IContentService
{
    Task<ContentItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ContentItem?> GetBySlugAsync(string slug, CancellationToken cancellationToken);

    Task<PagedResult<ContentItem>> ListAsync(ContentListQuery query, CancellationToken cancellationToken);

    Task<ContentItem> CreateAsync(string slug, string title, string body, string[] tags, CancellationToken cancellationToken);

    Task<ContentItem?> UpdateAsync(Guid id, string slug, string title, string body, string[] tags, long? expectedVersion, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

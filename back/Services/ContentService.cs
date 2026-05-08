using WebApplication2.Exceptions;
using WebApplication2.Models;
using WebApplication2.Repositories;

namespace WebApplication2.Services;

public sealed class ContentService : IContentService
{
    private readonly IContentRepository _repository;
    private readonly ILogger<ContentService> _logger;

    public ContentService(IContentRepository repository, ILogger<ContentService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<ContentItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<ContentItem?> GetBySlugAsync(string slug, CancellationToken cancellationToken)
        => _repository.GetBySlugAsync(NormalizeSlug(slug), cancellationToken);

    public Task<PagedResult<ContentItem>> ListAsync(ContentListQuery query, CancellationToken cancellationToken)
        => _repository.ListAsync(Sanitize(query), cancellationToken);

    public async Task<ContentItem> CreateAsync(string slug, string title, string body, string[] tags, CancellationToken cancellationToken)
    {
        var normalizedSlug = NormalizeSlug(slug);
        var now = DateTimeOffset.UtcNow;
        var item = new ContentItem
        {
            Id = Guid.NewGuid(),
            Slug = normalizedSlug,
            Title = title.Trim(),
            Body = body.Trim(),
            Tags = NormalizeTags(tags),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Version = 1
        };

        try
        {
            var created = await _repository.CreateAsync(item, cancellationToken);
            _logger.LogInformation("Content item created: {Id} (slug: '{Slug}')", created.Id, created.Slug);
            return created;
        }
        catch (InvalidOperationException)
        {
            throw new DuplicateSlugException(normalizedSlug);
        }
    }

    public async Task<ContentItem?> UpdateAsync(
        Guid id, string slug, string title, string body, string[] tags,
        long? expectedVersion, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return null;

        var updated = new ContentItem
        {
            Id = existing.Id,
            Slug = NormalizeSlug(slug),
            Title = title.Trim(),
            Body = body.Trim(),
            Tags = NormalizeTags(tags),
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Version = existing.Version + 1
        };

        try
        {
            var saved = await _repository.UpdateAsync(updated, expectedVersion, cancellationToken);
            if (saved is null) return null;

            _logger.LogInformation("Content item updated: {Id} (slug: '{Slug}', version: {Version})", saved.Id, saved.Slug, saved.Version);
            return saved;
        }
        catch (InvalidOperationException)
        {
            throw new DuplicateSlugException(updated.Slug);
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var removed = await _repository.DeleteAsync(id, cancellationToken);
        if (removed is not null)
            _logger.LogInformation("Content item deleted: {Id} (slug: '{Slug}')", id, removed.Slug);
        return removed is not null;
    }

    private static ContentListQuery Sanitize(ContentListQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var tag = string.IsNullOrWhiteSpace(query.Tag) ? null : query.Tag.Trim();
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        return new ContentListQuery(page, pageSize, tag, search);
    }

    private static string[] NormalizeTags(string[] tags) =>
        tags.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();

    private static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();
}

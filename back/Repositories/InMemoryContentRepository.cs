using System.Collections.Concurrent;
using WebApplication2.Exceptions;
using WebApplication2.Models;

namespace WebApplication2.Repositories;

public sealed class InMemoryContentRepository : IContentRepository
{
    private readonly ConcurrentDictionary<Guid, ContentItem> _items = new();
    private readonly ConcurrentDictionary<string, Guid> _slugIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _writeLock = new();

    public Task<ContentItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        _items.TryGetValue(id, out var item);
        return Task.FromResult(item is null ? null : Clone(item));
    }

    public Task<ContentItem?> GetBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        if (!_slugIndex.TryGetValue(slug, out var id))
        {
            return Task.FromResult<ContentItem?>(null);
        }

        _items.TryGetValue(id, out var item);
        return Task.FromResult(item is null ? null : Clone(item));
    }

    public Task<PagedResult<ContentItem>> ListAsync(ContentListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        IEnumerable<ContentItem> data = _items.Values;

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var normalizedTag = query.Tag.Trim();
            data = data.Where(x => x.Tags.Any(t => t.Equals(normalizedTag, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var normalizedSearch = query.Search.Trim();
            data = data.Where(x =>
                x.Title.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || x.Body.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = data.OrderByDescending(x => x.UpdatedAtUtc);
        var totalCount = ordered.Count();

        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Clone)
            .ToArray();

        return Task.FromResult(new PagedResult<ContentItem>(items, page, pageSize, totalCount));
    }

    public Task<ContentItem> CreateAsync(ContentItem item, CancellationToken cancellationToken)
    {
        lock (_writeLock)
        {
            if (!_slugIndex.TryAdd(item.Slug, item.Id))
            {
                throw new InvalidOperationException("Slug already exists.");
            }

            _items[item.Id] = Clone(item);
            return Task.FromResult(Clone(item));
        }
    }

    public Task<ContentItem?> UpdateAsync(ContentItem item, long? expectedVersion, CancellationToken cancellationToken)
    {
        lock (_writeLock)
        {
            if (!_items.TryGetValue(item.Id, out var current))
            {
                return Task.FromResult<ContentItem?>(null);
            }

            if (expectedVersion.HasValue && current.Version != expectedVersion.Value)
            {
                throw new ConcurrencyConflictException(expectedVersion.Value, current.Version);
            }

            if (!current.Slug.Equals(item.Slug, StringComparison.OrdinalIgnoreCase))
            {
                if (_slugIndex.TryGetValue(item.Slug, out var existingId) && existingId != item.Id)
                {
                    throw new InvalidOperationException("Slug already exists.");
                }

                _slugIndex.TryRemove(current.Slug, out _);
                _slugIndex[item.Slug] = item.Id;
            }

            _items[item.Id] = Clone(item);
            return Task.FromResult<ContentItem?>(Clone(item));
        }
    }

    public void Reset()
    {
        lock (_writeLock)
        {
            _items.Clear();
            _slugIndex.Clear();
        }
    }

    public Task<ContentItem?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_writeLock)
        {
            if (!_items.TryRemove(id, out var removed))
            {
                return Task.FromResult<ContentItem?>(null);
            }

            _slugIndex.TryRemove(removed.Slug, out _);
            return Task.FromResult<ContentItem?>(Clone(removed));
        }
    }

    private static ContentItem Clone(ContentItem item)
    {
        return new ContentItem
        {
            Id = item.Id,
            Slug = item.Slug,
            Title = item.Title,
            Body = item.Body,
            Tags = item.Tags.ToArray(),
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = item.UpdatedAtUtc,
            Version = item.Version
        };
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using WebApplication2.Models;
using WebApplication2.Options;

namespace WebApplication2.Repositories;

/// <summary>
/// Decorator that adds distributed caching to any IContentRepository.
/// Uses a generation counter for list-cache invalidation: any mutation increments
/// the counter, which changes every list cache key without requiring individual tracking.
/// </summary>
public sealed class CachedContentRepository : IContentRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string GenerationKey = "cms:content:list:generation";

    private readonly IContentRepository _inner;
    private readonly IDistributedCache _cache;
    private readonly DistributedCacheEntryOptions _itemOptions;
    private readonly DistributedCacheEntryOptions _listOptions;

    public CachedContentRepository(
        IContentRepository inner,
        IDistributedCache cache,
        IOptions<ContentCacheOptions> cacheOptions)
    {
        _inner = inner;
        _cache = cache;

        var value = cacheOptions.Value;
        _itemOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(10, value.ItemTtlSeconds))
        };
        _listOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(5, value.ListTtlSeconds))
        };
    }

    public async Task<ContentItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var key = ItemKey(id);
        var cached = await GetAsync<ContentItem>(key, cancellationToken);
        if (cached is not null) return cached;

        var item = await _inner.GetByIdAsync(id, cancellationToken);
        if (item is null) return null;

        await SetAsync(key, item, _itemOptions, cancellationToken);
        await SetAsync(SlugKey(item.Slug), item, _itemOptions, cancellationToken);
        return item;
    }

    public async Task<ContentItem?> GetBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var key = SlugKey(slug);
        var cached = await GetAsync<ContentItem>(key, cancellationToken);
        if (cached is not null) return cached;

        var item = await _inner.GetBySlugAsync(slug, cancellationToken);
        if (item is null) return null;

        await SetAsync(key, item, _itemOptions, cancellationToken);
        await SetAsync(ItemKey(item.Id), item, _itemOptions, cancellationToken);
        return item;
    }

    public async Task<PagedResult<ContentItem>> ListAsync(ContentListQuery query, CancellationToken cancellationToken)
    {
        var generation = await GetListGenerationAsync(cancellationToken);
        var key = ListKey(generation, query);

        var cached = await GetAsync<PagedResult<ContentItem>>(key, cancellationToken);
        if (cached is not null) return cached;

        var result = await _inner.ListAsync(query, cancellationToken);
        await SetAsync(key, result, _listOptions, cancellationToken);
        return result;
    }

    public async Task<ContentItem> CreateAsync(ContentItem item, CancellationToken cancellationToken)
    {
        var created = await _inner.CreateAsync(item, cancellationToken);
        await SetAsync(ItemKey(created.Id), created, _itemOptions, cancellationToken);
        await SetAsync(SlugKey(created.Slug), created, _itemOptions, cancellationToken);
        await BumpListGenerationAsync(cancellationToken);
        return created;
    }

    public async Task<ContentItem?> UpdateAsync(ContentItem item, long? expectedVersion, CancellationToken cancellationToken)
    {
        // Read previous state from cache (or inner repo) to know which slug key to evict
        var previous = await GetAsync<ContentItem>(ItemKey(item.Id), cancellationToken)
                       ?? await _inner.GetByIdAsync(item.Id, cancellationToken);

        var updated = await _inner.UpdateAsync(item, expectedVersion, cancellationToken);
        if (updated is null) return null;

        if (previous?.Slug is not null && previous.Slug != updated.Slug)
            await _cache.RemoveAsync(SlugKey(previous.Slug), cancellationToken);

        await SetAsync(ItemKey(updated.Id), updated, _itemOptions, cancellationToken);
        await SetAsync(SlugKey(updated.Slug), updated, _itemOptions, cancellationToken);
        await BumpListGenerationAsync(cancellationToken);
        return updated;
    }

    public async Task<ContentItem?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var removed = await _inner.DeleteAsync(id, cancellationToken);
        if (removed is null) return null;

        await _cache.RemoveAsync(ItemKey(id), cancellationToken);
        await _cache.RemoveAsync(SlugKey(removed.Slug), cancellationToken);
        await BumpListGenerationAsync(cancellationToken);
        return removed;
    }

    private async Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        var bytes = await _cache.GetAsync(key, ct);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes, JsonOptions);
    }

    private Task SetAsync<T>(string key, T value, DistributedCacheEntryOptions options, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        return _cache.SetAsync(key, bytes, options, ct);
    }

    private async Task<string> GetListGenerationAsync(CancellationToken ct)
    {
        var bytes = await _cache.GetAsync(GenerationKey, ct);
        if (bytes is null)
        {
            await _cache.SetStringAsync(GenerationKey, "1", _itemOptions, ct);
            return "1";
        }
        var gen = Encoding.UTF8.GetString(bytes);
        return string.IsNullOrWhiteSpace(gen) ? "1" : gen;
    }

    private async Task BumpListGenerationAsync(CancellationToken ct)
    {
        var current = await GetListGenerationAsync(ct);
        var next = (long.TryParse(current, out var n) ? n : 1L) + 1;
        await _cache.SetStringAsync(GenerationKey, next.ToString(), _itemOptions, ct);
    }

    private static string ItemKey(Guid id) => $"cms:content:item:{id:N}";
    private static string SlugKey(string slug) => $"cms:content:slug:{slug}";

    private static string ListKey(string generation, ContentListQuery query)
    {
        var raw = $"{query.Page}:{query.PageSize}:{query.Tag}:{query.Search}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        return $"cms:content:list:{generation}:{hash}";
    }
}

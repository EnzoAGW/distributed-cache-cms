using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using WebApplication2.Exceptions;
using WebApplication2.Models;
using WebApplication2.Options;
using WebApplication2.Repositories;

namespace WebApplication2.Services;

public sealed class ContentService : IContentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IContentRepository _repository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ContentService> _logger;
    private readonly DistributedCacheEntryOptions _itemCacheEntryOptions;
    private readonly DistributedCacheEntryOptions _listCacheEntryOptions;

    public ContentService(
        IContentRepository repository,
        IDistributedCache cache,
        IOptions<ContentCacheOptions> cacheOptions,
        ILogger<ContentService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;

        var value = cacheOptions.Value;
        _itemCacheEntryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(10, value.ItemTtlSeconds))
        };

        _listCacheEntryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(5, value.ListTtlSeconds))
        };
    }

    public async Task<ContentItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var key = BuildItemKey(id);
        var cached = await GetFromCacheAsync<ContentItem>(key, cancellationToken);

        if (cached is not null)
        {
            return cached;
        }

        _logger.LogDebug("Cache miss for content item {Id}", id);

        var item = await _repository.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        await SetToCacheAsync(key, item, _itemCacheEntryOptions, cancellationToken);
        await SetToCacheAsync(BuildSlugKey(item.Slug), item, _itemCacheEntryOptions, cancellationToken);

        return item;
    }

    public async Task<ContentItem?> GetBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var normalizedSlug = NormalizeSlug(slug);
        var key = BuildSlugKey(normalizedSlug);
        var cached = await GetFromCacheAsync<ContentItem>(key, cancellationToken);

        if (cached is not null)
        {
            return cached;
        }

        _logger.LogDebug("Cache miss for content slug '{Slug}'", normalizedSlug);

        var item = await _repository.GetBySlugAsync(normalizedSlug, cancellationToken);
        if (item is null)
        {
            return null;
        }

        await SetToCacheAsync(key, item, _itemCacheEntryOptions, cancellationToken);
        await SetToCacheAsync(BuildItemKey(item.Id), item, _itemCacheEntryOptions, cancellationToken);

        return item;
    }

    public async Task<PagedResult<ContentItem>> ListAsync(ContentListQuery query, CancellationToken cancellationToken)
    {
        var sanitized = Sanitize(query);
        var generation = await GetListGenerationAsync(cancellationToken);
        var key = BuildListKey(generation, sanitized);

        var cached = await GetFromCacheAsync<PagedResult<ContentItem>>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var result = await _repository.ListAsync(sanitized, cancellationToken);
        await SetToCacheAsync(key, result, _listCacheEntryOptions, cancellationToken);

        return result;
    }

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

            await SetToCacheAsync(BuildItemKey(created.Id), created, _itemCacheEntryOptions, cancellationToken);
            await SetToCacheAsync(BuildSlugKey(created.Slug), created, _itemCacheEntryOptions, cancellationToken);
            await BumpListGenerationAsync(cancellationToken);

            _logger.LogInformation("Content item created: {Id} (slug: '{Slug}')", created.Id, created.Slug);
            return created;
        }
        catch (InvalidOperationException)
        {
            throw new DuplicateSlugException(normalizedSlug);
        }
    }

    public async Task<ContentItem?> UpdateAsync(Guid id, string slug, string title, string body, string[] tags, long? expectedVersion, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (expectedVersion.HasValue && existing.Version != expectedVersion.Value)
        {
            _logger.LogWarning(
                "Concurrency conflict on content {Id}: expected version {Expected}, found {Actual}",
                id, expectedVersion.Value, existing.Version);
            throw new ConcurrencyConflictException(expectedVersion.Value, existing.Version);
        }

        var previousSlug = existing.Slug;
        var normalizedSlug = NormalizeSlug(slug);

        existing.Slug = normalizedSlug;
        existing.Title = title.Trim();
        existing.Body = body.Trim();
        existing.Tags = NormalizeTags(tags);
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        existing.Version += 1;

        try
        {
            var updated = await _repository.UpdateAsync(existing, cancellationToken);
            if (updated is null)
            {
                return null;
            }

            await _cache.RemoveAsync(BuildSlugKey(previousSlug), cancellationToken);
            await SetToCacheAsync(BuildItemKey(updated.Id), updated, _itemCacheEntryOptions, cancellationToken);
            await SetToCacheAsync(BuildSlugKey(updated.Slug), updated, _itemCacheEntryOptions, cancellationToken);
            await BumpListGenerationAsync(cancellationToken);

            _logger.LogInformation("Content item updated: {Id} (slug: '{Slug}', version: {Version})", updated.Id, updated.Slug, updated.Version);
            return updated;
        }
        catch (InvalidOperationException)
        {
            throw new DuplicateSlugException(normalizedSlug);
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var removed = await _repository.DeleteAsync(id, cancellationToken);
        if (removed is null)
        {
            return false;
        }

        await _cache.RemoveAsync(BuildItemKey(id), cancellationToken);
        await _cache.RemoveAsync(BuildSlugKey(removed.Slug), cancellationToken);
        await BumpListGenerationAsync(cancellationToken);

        _logger.LogInformation("Content item deleted: {Id} (slug: '{Slug}')", id, removed.Slug);
        return true;
    }

    private async Task<T?> GetFromCacheAsync<T>(string key, CancellationToken cancellationToken)
    {
        var bytes = await _cache.GetAsync(key, cancellationToken);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes, JsonOptions);
    }

    private Task SetToCacheAsync<T>(
        string key,
        T value,
        DistributedCacheEntryOptions options,
        CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        return _cache.SetAsync(key, bytes, options, cancellationToken);
    }

    private async Task<string> GetListGenerationAsync(CancellationToken cancellationToken)
    {
        var generationBytes = await _cache.GetAsync(CacheKeys.ListGeneration, cancellationToken);
        if (generationBytes is null)
        {
            await _cache.SetStringAsync(CacheKeys.ListGeneration, "1", _itemCacheEntryOptions, cancellationToken);
            return "1";
        }

        var generation = Encoding.UTF8.GetString(generationBytes);
        return string.IsNullOrWhiteSpace(generation) ? "1" : generation;
    }

    private async Task BumpListGenerationAsync(CancellationToken cancellationToken)
    {
        var currentGeneration = await GetListGenerationAsync(cancellationToken);
        var current = long.TryParse(currentGeneration, out var parsed) ? parsed : 1L;
        var next = current + 1;
        await _cache.SetStringAsync(CacheKeys.ListGeneration, next.ToString(), _itemCacheEntryOptions, cancellationToken);
    }

    private static ContentListQuery Sanitize(ContentListQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var tag = string.IsNullOrWhiteSpace(query.Tag) ? null : query.Tag.Trim();
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        return new ContentListQuery(page, pageSize, tag, search);
    }

    private static string[] NormalizeTags(string[] tags)
    {
        return tags
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();

    private static string BuildItemKey(Guid id) => $"cms:content:item:{id:N}";

    private static string BuildSlugKey(string slug) => $"cms:content:slug:{slug}";

    private static string BuildListKey(string generation, ContentListQuery query)
    {
        var raw = $"{query.Page}:{query.PageSize}:{query.Tag}:{query.Search}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        return $"cms:content:list:{generation}:{hash}";
    }

    private static class CacheKeys
    {
        public const string ListGeneration = "cms:content:list:generation";
    }
}

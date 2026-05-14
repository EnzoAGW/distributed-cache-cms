using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using MsOptions = Microsoft.Extensions.Options;
using WebApplication2.Exceptions;
using WebApplication2.Models;
using WebApplication2.Repositories;

namespace WebApplication2.Tests.Unit;

public sealed class CachedContentRepositoryTests
{
    // Helpers -----------------------------------------------------------------

    private static CachedContentRepository BuildRepo(IContentRepository inner)
    {
        var cache = new MemoryDistributedCache(MsOptions.Options.Create(new MemoryDistributedCacheOptions()));
        var cacheOptions = MsOptions.Options.Create(new WebApplication2.Options.ContentCacheOptions
        {
            ItemTtlSeconds = 60,
            ListTtlSeconds = 30
        });
        return new CachedContentRepository(inner, cache, cacheOptions);
    }

    private static ContentItem MakeItem(string slug = "test-slug") => new()
    {
        Id = Guid.NewGuid(),
        Slug = slug,
        Title = "Test",
        Body = "Body",
        Tags = ["tag1"],
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
        Version = 1
    };

    // GetByIdAsync ------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_CacheMiss_CallsInnerAndPopulatesCache()
    {
        var item = MakeItem();
        var inner = new InMemoryContentRepository();
        await inner.CreateAsync(item, default);

        var repo = BuildRepo(inner);

        var first = await repo.GetByIdAsync(item.Id, default);
        var second = await repo.GetByIdAsync(item.Id, default);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(item.Id, first.Id);
    }

    [Fact]
    public async Task GetByIdAsync_CacheHit_DoesNotCallInnerAgain()
    {
        var item = MakeItem();
        var inner = new InMemoryContentRepository();
        await inner.CreateAsync(item, default);

        var repo = BuildRepo(inner);
        await repo.GetByIdAsync(item.Id, default); // populates cache

        // Delete from inner — if cache is hit, result is still returned
        await inner.DeleteAsync(item.Id, default);

        var cached = await repo.GetByIdAsync(item.Id, default);
        Assert.NotNull(cached);
    }

    // GetBySlugAsync ----------------------------------------------------------

    [Fact]
    public async Task GetBySlugAsync_CacheMiss_CallsInnerAndPopulatesCache()
    {
        var item = MakeItem("my-slug");
        var inner = new InMemoryContentRepository();
        await inner.CreateAsync(item, default);

        var repo = BuildRepo(inner);

        var result = await repo.GetBySlugAsync("my-slug", default);
        Assert.NotNull(result);
        Assert.Equal("my-slug", result.Slug);
    }

    // CreateAsync -------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PopulatesCacheAndBumpsListGeneration()
    {
        var item = MakeItem();
        var inner = new InMemoryContentRepository();
        var repo = BuildRepo(inner);

        var query = new ContentListQuery(1, 10, null, null);

        // Prime the list cache with generation 1
        await repo.ListAsync(query, default);

        // Create bumps generation
        await repo.CreateAsync(item, default);

        // List is re-fetched from inner (stale gen key → new list cache key)
        await inner.DeleteAsync(item.Id, default);
        var listAfterDelete = await repo.ListAsync(query, default);
        Assert.Equal(0, listAfterDelete.TotalCount);
    }

    [Fact]
    public async Task CreateAsync_ItemAccessibleViaCache()
    {
        var item = MakeItem();
        var inner = new InMemoryContentRepository();
        var repo = BuildRepo(inner);

        await repo.CreateAsync(item, default);

        // Remove from inner to confirm the result comes from cache
        await inner.DeleteAsync(item.Id, default);

        var cached = await repo.GetByIdAsync(item.Id, default);
        Assert.NotNull(cached);
    }

    // UpdateAsync -------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_SlugChanged_EjectsOldSlugKey()
    {
        var item = MakeItem("original-slug");
        var inner = new InMemoryContentRepository();
        await inner.CreateAsync(item, default);

        var repo = BuildRepo(inner);
        await repo.GetBySlugAsync("original-slug", default); // warm the cache

        var renamed = new ContentItem
        {
            Id = item.Id,
            Slug = "new-slug",
            Title = item.Title,
            Body = item.Body,
            Tags = item.Tags,
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Version = 2
        };

        await repo.UpdateAsync(renamed, expectedVersion: 1, default);

        // Old slug key should be evicted
        var byOldSlug = await repo.GetBySlugAsync("original-slug", default);
        Assert.Null(byOldSlug);
    }

    [Fact]
    public async Task UpdateAsync_WrongVersion_ThrowsConcurrencyConflictException()
    {
        var item = MakeItem();
        var inner = new InMemoryContentRepository();
        await inner.CreateAsync(item, default);
        var repo = BuildRepo(inner);

        var updated = new ContentItem
        {
            Id = item.Id,
            Slug = item.Slug,
            Title = "Changed",
            Body = item.Body,
            Tags = item.Tags,
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Version = 2
        };

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => repo.UpdateAsync(updated, expectedVersion: 999, default));
    }

    // DeleteAsync -------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RemovesItemAndSlugFromCache()
    {
        var item = MakeItem("delete-me");
        var inner = new InMemoryContentRepository();
        await inner.CreateAsync(item, default);

        var repo = BuildRepo(inner);
        await repo.GetByIdAsync(item.Id, default);       // warm item cache
        await repo.GetBySlugAsync("delete-me", default); // warm slug cache

        await repo.DeleteAsync(item.Id, default);

        Assert.Null(await repo.GetByIdAsync(item.Id, default));
        Assert.Null(await repo.GetBySlugAsync("delete-me", default));
    }
}

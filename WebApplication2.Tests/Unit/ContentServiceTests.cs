using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WebApplication2.Exceptions;
using WebApplication2.Models;
using WebApplication2.Repositories;
using WebApplication2.Services;

namespace WebApplication2.Tests.Unit;

public sealed class ContentServiceTests
{
    // Helpers -----------------------------------------------------------------

    private static ContentService BuildService(IContentRepository repository) =>
        new(repository, NullLogger<ContentService>.Instance);

    private static ContentItem MakeItem(string slug = "test-slug") => new()
    {
        Id = Guid.NewGuid(),
        Slug = slug,
        Title = "Test Title",
        Body = "Test body content.",
        Tags = ["tag1", "tag2"],
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
        Version = 1
    };

    // GetByIdAsync ------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_ItemExists_ReturnsItem()
    {
        var item = MakeItem();
        var repo = new Mock<IContentRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, default)).ReturnsAsync(item);

        var result = await BuildService(repo.Object).GetByIdAsync(item.Id, default);

        Assert.NotNull(result);
        Assert.Equal(item.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ItemMissing_ReturnsNull()
    {
        var repo = new Mock<IContentRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((ContentItem?)null);

        var result = await BuildService(repo.Object).GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(result);
    }

    // GetBySlugAsync ----------------------------------------------------------

    [Fact]
    public async Task GetBySlugAsync_NormalizesSlugBeforeLookup()
    {
        var item = MakeItem("my-article");
        var repo = new Mock<IContentRepository>();
        repo.Setup(r => r.GetBySlugAsync("my-article", default)).ReturnsAsync(item);

        // Pass uppercase — service should normalize to lowercase before hitting repo
        var result = await BuildService(repo.Object).GetBySlugAsync("MY-ARTICLE", default);

        Assert.NotNull(result);
        repo.Verify(r => r.GetBySlugAsync("my-article", default), Times.Once);
    }

    // CreateAsync -------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ValidInput_ReturnsCreatedItem()
    {
        var repo = new Mock<IContentRepository>();
        repo.Setup(r => r.CreateAsync(It.IsAny<ContentItem>(), default))
            .ReturnsAsync((ContentItem i, CancellationToken _) => i);

        var result = await BuildService(repo.Object).CreateAsync("new-slug", "Title", "Body", ["tag"], default);

        Assert.Equal("new-slug", result.Slug);
        Assert.Equal(1, result.Version);
    }

    [Fact]
    public async Task CreateAsync_NormalizesSlugToLowercase()
    {
        var repo = new Mock<IContentRepository>();
        repo.Setup(r => r.CreateAsync(It.IsAny<ContentItem>(), default))
            .ReturnsAsync((ContentItem i, CancellationToken _) => i);

        var result = await BuildService(repo.Object).CreateAsync("MY-SLUG", "Title", "Body", [], default);

        Assert.Equal("my-slug", result.Slug);
    }

    [Fact]
    public async Task CreateAsync_DuplicateSlug_ThrowsDuplicateSlugException()
    {
        var repo = new Mock<IContentRepository>();
        repo.Setup(r => r.CreateAsync(It.IsAny<ContentItem>(), default))
            .ThrowsAsync(new InvalidOperationException("Slug already exists."));

        await Assert.ThrowsAsync<DuplicateSlugException>(
            () => BuildService(repo.Object).CreateAsync("taken-slug", "Title", "Body", [], default));
    }

    [Fact]
    public async Task CreateAsync_DeduplicatesTags()
    {
        var repo = new Mock<IContentRepository>();
        repo.Setup(r => r.CreateAsync(It.IsAny<ContentItem>(), default))
            .ReturnsAsync((ContentItem i, CancellationToken _) => i);

        var result = await BuildService(repo.Object).CreateAsync("slug", "Title", "Body", ["Tag", "tag", "TAG"], default);

        Assert.Single(result.Tags);
    }

    // UpdateAsync -------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ItemNotFound_ReturnsNull()
    {
        var repo = new Mock<IContentRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((ContentItem?)null);

        var result = await BuildService(repo.Object).UpdateAsync(Guid.NewGuid(), "slug", "Title", "Body", [], null, default);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_CorrectExpectedVersion_Succeeds()
    {
        var item = MakeItem();
        var repo = new Mock<IContentRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, default)).ReturnsAsync(item);
        repo.Setup(r => r.UpdateAsync(It.IsAny<ContentItem>(), It.IsAny<long?>(), default))
            .ReturnsAsync((ContentItem i, long? _, CancellationToken _2) => i);

        var result = await BuildService(repo.Object).UpdateAsync(item.Id, item.Slug, "New Title", item.Body, item.Tags, expectedVersion: 1, default);

        Assert.NotNull(result);
        Assert.Equal(2, result.Version);
    }

    [Fact]
    public async Task UpdateAsync_WrongExpectedVersion_ThrowsConcurrencyConflictException()
    {
        var item = MakeItem(); // Version = 1
        var repo = new Mock<IContentRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, default)).ReturnsAsync(item);
        repo.Setup(r => r.UpdateAsync(It.IsAny<ContentItem>(), 99L, default))
            .ThrowsAsync(new ConcurrencyConflictException(99, 1));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => BuildService(repo.Object).UpdateAsync(item.Id, item.Slug, "Title", item.Body, item.Tags, expectedVersion: 99, default));
    }

    [Fact]
    public async Task UpdateAsync_NoExpectedVersion_SkipsConcurrencyCheck()
    {
        var item = MakeItem();
        var repo = new Mock<IContentRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, default)).ReturnsAsync(item);
        repo.Setup(r => r.UpdateAsync(It.IsAny<ContentItem>(), It.IsAny<long?>(), default))
            .ReturnsAsync((ContentItem i, long? _, CancellationToken _2) => i);

        // expectedVersion: null — should never throw ConcurrencyConflictException
        var result = await BuildService(repo.Object).UpdateAsync(item.Id, item.Slug, "Title", item.Body, item.Tags, expectedVersion: null, default);

        Assert.NotNull(result);
    }

    // DeleteAsync -------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_ItemExists_ReturnsTrue()
    {
        var item = MakeItem();
        var repo = new Mock<IContentRepository>();
        repo.Setup(r => r.DeleteAsync(item.Id, default)).ReturnsAsync(item);

        var result = await BuildService(repo.Object).DeleteAsync(item.Id, default);

        Assert.True(result);
    }

    [Fact]
    public async Task DeleteAsync_ItemNotFound_ReturnsFalse()
    {
        var repo = new Mock<IContentRepository>();
        repo.Setup(r => r.DeleteAsync(It.IsAny<Guid>(), default)).ReturnsAsync((ContentItem?)null);

        var result = await BuildService(repo.Object).DeleteAsync(Guid.NewGuid(), default);

        Assert.False(result);
    }
}

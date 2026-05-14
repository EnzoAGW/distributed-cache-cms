using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WebApplication2.Dtos;
using WebApplication2.Models;

namespace WebApplication2.Tests.Integration;

public sealed class ContentControllerTests : IClassFixture<CmsWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CmsWebApplicationFactory _factory;

    public ContentControllerTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() { _factory.ResetData(); return Task.CompletedTask; }
    public Task DisposeAsync() => Task.CompletedTask;

    // Helpers -----------------------------------------------------------------

    private void Authenticate() =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.GenerateToken());

    private void ClearAuth() =>
        _client.DefaultRequestHeaders.Authorization = null;

    private static string UniqueSlug() => $"test-{Guid.NewGuid():N}";

    // GET /api/content --------------------------------------------------------

    [Fact]
    public async Task List_NoAuth_Returns200WithPagedResult()
    {
        var response = await _client.GetAsync("/api/content");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PagedResult<ContentResponse>>();
        Assert.NotNull(body);
        Assert.True(body.Page >= 1);
        Assert.True(body.PageSize >= 1);
    }

    [Fact]
    public async Task List_WithPaginationParams_Returns200()
    {
        var response = await _client.GetAsync("/api/content?page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // GET /api/content/{id} ---------------------------------------------------

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/content/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingItem_Returns200WithEtag()
    {
        Authenticate();
        var created = await CreateContentAsync(UniqueSlug());

        ClearAuth();
        var response = await _client.GetAsync($"/api/content/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
    }

    [Fact]
    public async Task GetById_WithMatchingEtag_Returns304()
    {
        Authenticate();
        var created = await CreateContentAsync(UniqueSlug());

        ClearAuth();
        var firstResponse = await _client.GetAsync($"/api/content/{created.Id}");
        var etag = firstResponse.Headers.ETag!.Tag;

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/content/{created.Id}");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag));
        var secondResponse = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, secondResponse.StatusCode);
    }

    // GET /api/content/slug/{slug} --------------------------------------------

    [Fact]
    public async Task GetBySlug_NonExistent_Returns404()
    {
        var response = await _client.GetAsync("/api/content/slug/slug-that-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBySlug_ExistingSlug_Returns200()
    {
        var slug = UniqueSlug();
        Authenticate();
        await CreateContentAsync(slug);

        ClearAuth();
        var response = await _client.GetAsync($"/api/content/slug/{slug}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // POST /api/content -------------------------------------------------------

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        ClearAuth();
        var response = await _client.PostAsJsonAsync("/api/content", new
        {
            slug = UniqueSlug(),
            title = "Title",
            body = "Body",
            tags = Array.Empty<string>()
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithToken_Returns201WithLocation()
    {
        Authenticate();
        var slug = UniqueSlug();

        var response = await _client.PostAsJsonAsync("/api/content", new
        {
            slug,
            title = "My Article",
            body = "Article content.",
            tags = new[] { "dotnet", "cms" }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<ContentResponse>();
        Assert.NotNull(body);
        Assert.Equal(slug, body.Slug);
        Assert.Equal(1, body.Version);
    }

    [Fact]
    public async Task Create_DuplicateSlug_Returns409()
    {
        Authenticate();
        var slug = UniqueSlug();

        await CreateContentAsync(slug);

        var response = await _client.PostAsJsonAsync("/api/content", new
        {
            slug,
            title = "Duplicate",
            body = "Body",
            tags = Array.Empty<string>()
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // PUT /api/content/{id} ---------------------------------------------------

    [Fact]
    public async Task Update_WithoutToken_Returns401()
    {
        ClearAuth();
        var response = await _client.PutAsJsonAsync($"/api/content/{Guid.NewGuid()}", new
        {
            slug = UniqueSlug(),
            title = "T",
            body = "B",
            tags = Array.Empty<string>()
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_ExistingItem_Returns200WithIncrementedVersion()
    {
        Authenticate();
        var created = await CreateContentAsync(UniqueSlug());

        var response = await _client.PutAsJsonAsync($"/api/content/{created.Id}", new
        {
            slug = created.Slug,
            title = "Updated Title",
            body = "Updated body.",
            tags = new[] { "updated" },
            expectedVersion = created.Version
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ContentResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.Version + 1, body.Version);
        Assert.Equal("Updated Title", body.Title);
    }

    [Fact]
    public async Task Update_WrongExpectedVersion_Returns409()
    {
        Authenticate();
        var created = await CreateContentAsync(UniqueSlug());

        var response = await _client.PutAsJsonAsync($"/api/content/{created.Id}", new
        {
            slug = created.Slug,
            title = "T",
            body = "B",
            tags = Array.Empty<string>(),
            expectedVersion = 999L
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_NonExistentItem_Returns404()
    {
        Authenticate();
        var response = await _client.PutAsJsonAsync($"/api/content/{Guid.NewGuid()}", new
        {
            slug = UniqueSlug(),
            title = "T",
            body = "B",
            tags = Array.Empty<string>()
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DELETE /api/content/{id} ------------------------------------------------

    [Fact]
    public async Task Delete_WithoutToken_Returns401()
    {
        ClearAuth();
        var response = await _client.DeleteAsync($"/api/content/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingItem_Returns204()
    {
        Authenticate();
        var created = await CreateContentAsync(UniqueSlug());

        var response = await _client.DeleteAsync($"/api/content/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ThenGetById_Returns404()
    {
        Authenticate();
        var created = await CreateContentAsync(UniqueSlug());

        await _client.DeleteAsync($"/api/content/{created.Id}");

        ClearAuth();
        var getResponse = await _client.GetAsync($"/api/content/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistentItem_Returns404()
    {
        Authenticate();
        var response = await _client.DeleteAsync($"/api/content/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // GET /api/content — filtering & search ----------------------------------

    [Fact]
    public async Task List_WithTagFilter_ReturnsOnlyMatchingItems()
    {
        Authenticate();
        var taggedSlug = UniqueSlug();
        var otherSlug = UniqueSlug();

        await CreateContentAsync(taggedSlug, tags: ["dotnet", "cms"]);
        await CreateContentAsync(otherSlug, tags: ["react"]);

        ClearAuth();
        var response = await _client.GetAsync("/api/content?tag=dotnet");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PagedResult<ContentResponse>>();
        Assert.NotNull(body);
        Assert.All(body.Items, item => Assert.Contains("dotnet", item.Tags));
        Assert.DoesNotContain(body.Items, item => item.Slug == otherSlug);
    }

    [Fact]
    public async Task List_WithTagFilter_NoMatches_ReturnsEmptyItems()
    {
        Authenticate();
        await CreateContentAsync(UniqueSlug(), tags: ["dotnet"]);

        ClearAuth();
        var response = await _client.GetAsync("/api/content?tag=nonexistent-tag");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PagedResult<ContentResponse>>();
        Assert.NotNull(body);
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task List_WithSearch_ReturnsMatchingItems()
    {
        Authenticate();
        var matchSlug = UniqueSlug();
        var noMatchSlug = UniqueSlug();

        await CreateContentAsync(matchSlug, title: "Advanced Caching Strategies");
        await CreateContentAsync(noMatchSlug, title: "Getting Started With React");

        ClearAuth();
        var response = await _client.GetAsync("/api/content?search=caching");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PagedResult<ContentResponse>>();
        Assert.NotNull(body);
        Assert.Contains(body.Items, item => item.Slug == matchSlug);
        Assert.DoesNotContain(body.Items, item => item.Slug == noMatchSlug);
    }

    // Helper ------------------------------------------------------------------

    private async Task<ContentResponse> CreateContentAsync(
        string slug,
        string title = "Test Article",
        string[]? tags = null)
    {
        var response = await _client.PostAsJsonAsync("/api/content", new
        {
            slug,
            title,
            body = "Test body content.",
            tags = tags ?? new[] { "test" }
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContentResponse>())!;
    }
}

using System.Net;
using System.Net.Http.Json;
using WebApplication2.Dtos;

namespace WebApplication2.Tests.Integration;

public sealed class AuthControllerTests : IClassFixture<CmsWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CmsWebApplicationFactory _factory;

    public AuthControllerTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() { _factory.ResetData(); return Task.CompletedTask; }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Token_ValidCredentials_Returns200WithToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/token",
            new { username = "admin", password = "admin123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Token_InvalidPassword_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/token",
            new { username = "admin", password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_UnknownUser_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/token",
            new { username = "nobody", password = "admin123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_MissingBody_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/token",
            new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

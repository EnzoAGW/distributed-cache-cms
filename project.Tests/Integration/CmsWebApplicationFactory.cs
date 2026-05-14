using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MsOptions = Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebApplication2.Repositories;

namespace WebApplication2.Tests.Integration;

public sealed class CmsWebApplicationFactory : WebApplicationFactory<Program>
{
    // Fixed key used across all integration tests — never commit a real secret here
    public const string TestJwtKey = "integration-test-secret-key-32chars!";

    private readonly ResettableDistributedCache _cache = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:Issuer"] = "WebApplication2",
                ["Jwt:Audience"] = "WebApplication2",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Admin:Username"] = "admin",
                ["Admin:Password"] = "admin123",
                ["ContentCache:ItemTtlSeconds"] = "10",
                ["ContentCache:ListTtlSeconds"] = "5",
                ["ConnectionStrings:Redis"] = "",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IDistributedCache>(_cache);
        });
    }

    /// <summary>Resets all in-memory state between tests.</summary>
    public void ResetData()
    {
        Services.GetRequiredService<InMemoryContentRepository>().Reset();
        _cache.Reset();
    }

    /// <summary>Generates a signed JWT token accepted by the test server.</summary>
    public string GenerateToken(string username = "admin")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "WebApplication2",
            audience: "WebApplication2",
            claims: [new Claim(ClaimTypes.Name, username)],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// IDistributedCache wrapper whose internal store can be swapped atomically between tests.
/// </summary>
internal sealed class ResettableDistributedCache : IDistributedCache
{
    private volatile IDistributedCache _inner = CreateInner();

    public void Reset() => _inner = CreateInner();

    private static MemoryDistributedCache CreateInner() =>
        new(MsOptions.Options.Create(new MemoryDistributedCacheOptions()));

    public byte[]? Get(string key) => _inner.Get(key);
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => _inner.GetAsync(key, token);
    public void Refresh(string key) => _inner.Refresh(key);
    public Task RefreshAsync(string key, CancellationToken token = default) => _inner.RefreshAsync(key, token);
    public void Remove(string key) => _inner.Remove(key);
    public Task RemoveAsync(string key, CancellationToken token = default) => _inner.RemoveAsync(key, token);
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _inner.Set(key, value, options);
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        => _inner.SetAsync(key, value, options, token);
}

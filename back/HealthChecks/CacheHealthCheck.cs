using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WebApplication2.HealthChecks;

internal sealed class CacheHealthCheck(IDistributedCache cache) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.SetStringAsync(
                "health-probe",
                "ok",
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5) },
                cancellationToken);

            var value = await cache.GetStringAsync("health-probe", cancellationToken);

            return value == "ok"
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded("Cache returned an unexpected value.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cache is unreachable.", ex);
        }
    }
}

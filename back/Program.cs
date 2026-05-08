using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebApplication2.Exceptions;
using WebApplication2.HealthChecks;
using WebApplication2.Options;
using WebApplication2.Persistence;
using WebApplication2.Repositories;
using WebApplication2.Services;

var builder = WebApplication.CreateBuilder(args);

// Controllers + compression
builder.Services.AddControllers();
builder.Services.AddResponseCompression();
builder.Services.AddHttpLogging(_ => { });

// CORS — origins read from config so docker-compose / production can override without code changes
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Options
builder.Services.Configure<ContentCacheOptions>(
    builder.Configuration.GetSection(ContentCacheOptions.SectionName));

// Validate JWT config at startup — fail fast if key is missing or still a placeholder
builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(
        o => !string.IsNullOrWhiteSpace(o.Key) && !o.Key.StartsWith("REPLACE_WITH"),
        "Jwt:Key is not configured. In development run: dotnet user-secrets set \"Jwt:Key\" \"<your-secret>\"")
    .ValidateOnStart();

// Validate Admin credentials at startup — must be overridden via user-secrets or environment variables
builder.Services.AddOptions<AdminOptions>()
    .BindConfiguration(AdminOptions.SectionName)
    .Validate(
        o => !string.IsNullOrWhiteSpace(o.Username) && !o.Username.StartsWith("REPLACE_WITH")
          && !string.IsNullOrWhiteSpace(o.Password) && !o.Password.StartsWith("REPLACE_WITH"),
        "Admin credentials are not configured. In development run: " +
        "dotnet user-secrets set \"Admin:Username\" \"<user>\" && dotnet user-secrets set \"Admin:Password\" \"<pass>\"")
    .ValidateOnStart();

// Rate limiting — fixed window on the auth endpoint to slow brute-force attempts
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth-fixed", o =>
    {
        o.PermitLimit = 10;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Distributed cache (Redis if configured, otherwise in-memory)
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddDistributedMemoryCache();
}
else
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "webapplication2-cms";
    });
}

// Repository: EF Core (Postgres) if a connection string is provided, otherwise in-memory
var pgConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(pgConnectionString))
{
    builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(pgConnectionString));
    builder.Services.AddScoped<EfContentRepository>();
    builder.Services.AddScoped<IContentRepository>(sp => new CachedContentRepository(
        sp.GetRequiredService<EfContentRepository>(),
        sp.GetRequiredService<IDistributedCache>(),
        sp.GetRequiredService<IOptions<ContentCacheOptions>>()));
}
else
{
    builder.Services.AddSingleton<InMemoryContentRepository>();
    builder.Services.AddSingleton<IContentRepository>(sp => new CachedContentRepository(
        sp.GetRequiredService<InMemoryContentRepository>(),
        sp.GetRequiredService<IDistributedCache>(),
        sp.GetRequiredService<IOptions<ContentCacheOptions>>()));
}

builder.Services.AddScoped<IContentService, ContentService>();

// JWT authentication
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["Key"] ?? string.Empty))
        };
    });

builder.Services.AddAuthorization();

// Health checks — cache probe works for both in-memory (dev) and Redis (prod)
var healthChecks = builder.Services.AddHealthChecks()
    .AddCheck<CacheHealthCheck>("cache");

if (!string.IsNullOrWhiteSpace(pgConnectionString))
{
    healthChecks.AddDbContextCheck<AppDbContext>("database");
}

// OpenAPI / Swagger
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply pending EF migrations automatically on startup (Postgres mode only)
if (!string.IsNullOrWhiteSpace(pgConnectionString))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Global exception handler — maps domain exceptions to ProblemDetails
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = feature?.Error;

        var (status, title, detail) = exception switch
        {
            DuplicateSlugException ex => (StatusCodes.Status409Conflict, "Duplicate slug", ex.Message),
            ConcurrencyConflictException ex => (StatusCodes.Status409Conflict, "Concurrency conflict", ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred",
                  app.Environment.IsDevelopment() ? exception?.Message : null)
        };

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = status
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpLogging();
app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseCors();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }

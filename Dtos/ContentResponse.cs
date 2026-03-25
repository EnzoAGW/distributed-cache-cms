namespace WebApplication2.Dtos;

public sealed record ContentResponse(
    Guid Id,
    string Slug,
    string Title,
    string Body,
    string[] Tags,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    long Version
);

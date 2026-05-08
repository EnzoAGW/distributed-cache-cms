namespace WebApplication2.Models;

public sealed class ContentItem
{
    public Guid Id { get; init; }

    public string Slug { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public string[] Tags { get; init; } = [];

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public long Version { get; init; }
}

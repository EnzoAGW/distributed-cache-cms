namespace WebApplication2.Models;

public sealed class ContentItem
{
    public Guid Id { get; init; }

    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string[] Tags { get; set; } = [];

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public long Version { get; set; }
}

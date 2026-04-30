namespace WebApplication2.Options;

public sealed class ContentCacheOptions
{
    public const string SectionName = "ContentCache";

    public int ItemTtlSeconds { get; init; } = 300;

    public int ListTtlSeconds { get; init; } = 45;
}

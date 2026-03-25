namespace WebApplication2.Models;

public sealed record ContentListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Tag = null,
    string? Search = null
);

namespace WebApplication2.Exceptions;

public sealed class DuplicateSlugException : Exception
{
    public string Slug { get; }

    public DuplicateSlugException(string slug)
        : base($"A content item with slug '{slug}' already exists.")
    {
        Slug = slug;
    }
}

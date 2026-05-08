using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Dtos;

public sealed class CreateContentRequest : IValidatableObject
{
    [Required]
    [MaxLength(120)]
    [RegularExpression(@"^[a-zA-Z0-9]+(?:-[a-zA-Z0-9]+)*$",
        ErrorMessage = "Slug must contain only letters, numbers, and hyphens (e.g. my-article-title).")]
    public string Slug { get; init; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(20_000)]
    public string Body { get; init; } = string.Empty;

    [MaxLength(20)]
    public string[] Tags { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var tag in Tags)
        {
            if (tag.Length > 50)
                yield return new ValidationResult(
                    "Each tag must be at most 50 characters.", [nameof(Tags)]);
        }
    }
}

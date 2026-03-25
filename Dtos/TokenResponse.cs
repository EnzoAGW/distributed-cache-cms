namespace WebApplication2.Dtos;

public sealed record TokenResponse(string Token, DateTimeOffset ExpiresAt);

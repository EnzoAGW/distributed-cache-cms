using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebApplication2.Dtos;
using WebApplication2.Options;

namespace WebApplication2.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly JwtOptions _jwtOptions;
    private readonly AdminOptions _adminOptions;

    public AuthController(IOptions<JwtOptions> jwtOptions, IOptions<AdminOptions> adminOptions)
    {
        _jwtOptions = jwtOptions.Value;
        _adminOptions = adminOptions.Value;
    }

    [EnableRateLimiting("auth-fixed")]
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public ActionResult<TokenResponse> Token([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var usernameMatch = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(request.Username),
            Encoding.UTF8.GetBytes(_adminOptions.Username));
        var passwordMatch = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(request.Password),
            Encoding.UTF8.GetBytes(_adminOptions.Password));

        if (!usernameMatch || !passwordMatch)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid credentials",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, request.Username),
            ],
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return Ok(new TokenResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAt));
    }
}

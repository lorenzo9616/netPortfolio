using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace OcrApi.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration config, ILogger<AuthController> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>Validates credentials and returns a short-lived JWT.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var expectedUsername = _config["Auth:Username"] ?? string.Empty;
        var expectedPassword = _config["Auth:Password"] ?? string.Empty;

        if (!string.Equals(request.Username, expectedUsername, StringComparison.Ordinal)
            || !string.Equals(request.Password, expectedPassword, StringComparison.Ordinal))
        {
            _logger.LogWarning("Failed login attempt for username '{Username}'", request.Username);
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var jwtSecret   = _config["Auth:JwtSecret"]!;
        var expiryHours = int.Parse(_config["Auth:JwtExpiryHours"] ?? "24");
        var expiresAt   = DateTime.UtcNow.AddHours(expiryHours);
        var token       = GenerateToken(jwtSecret, expiresAt);

        _logger.LogInformation("Successful login for username '{Username}'", request.Username);

        return Ok(new LoginResponse { Token = token, ExpiresAt = expiresAt });
    }

    /// <summary>No-op server-side — client discards the token.</summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    public IActionResult Logout() => Ok();

    private static string GenerateToken(string secret, DateTime expiresAt)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims:             new[] { new Claim(ClaimTypes.Name, "admin") },
            expires:            expiresAt,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record LoginRequest(string Username, string Password);

public record LoginResponse
{
    public string   Token     { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}

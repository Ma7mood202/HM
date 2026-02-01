using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HM.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HM.Infrastructure.Services;

/// <summary>
/// Generates JWT tokens for AuthResponse. Configuration from Jwt:Secret, Jwt:Issuer, Jwt:Audience, Jwt:ExpirationMinutes.
/// </summary>
public sealed class JwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime Expiration) Generate(Guid userId, string phoneNumber, string email, UserType userType)
    {
        var secret = _configuration["Jwt:Secret"] ?? "default-secret-min-32-chars-for-hmac-sha256!!";
        var issuer = _configuration["Jwt:Issuer"] ?? "HM";
        var audience = _configuration["Jwt:Audience"] ?? "HM";
        var expirationMinutes = int.TryParse(_configuration["Jwt:ExpirationMinutes"], out var m) ? m : 60;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiration = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, userType.ToString()),
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (!string.IsNullOrEmpty(email))
            claims.Add(new Claim(ClaimTypes.Email, email));
        if (!string.IsNullOrEmpty(phoneNumber))
            claims.Add(new Claim(ClaimTypes.MobilePhone, phoneNumber));

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: expiration,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return (tokenString, expiration);
    }
}

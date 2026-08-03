using Dsw2026Tpi.Data.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Dsw2026Tpi.Application.Services;

public class JwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(
        ApplicationUser user,
        string role)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.Id == Guid.Empty)
        {
            throw new ArgumentException(
                "El usuario debe tener un identificador válido.",
                nameof(user));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        var jwtConfig = _config.GetSection("Jwt");

        var keyText = jwtConfig["Key"] ??
            throw new ArgumentNullException("Jwt Key");

        var issuer = jwtConfig["Issuer"] ??
            throw new ArgumentNullException("Jwt Issuer");

        var audience = jwtConfig["Audience"] ??
            throw new ArgumentNullException("Jwt Audience");

        var expiresIn = int.Parse(
            jwtConfig["ExpiresInMinutes"] ?? "60");

        var email = user.Email ?? user.UserName ??
            throw new InvalidOperationException(
                "El usuario no tiene un email configurado.");

        var userId = user.Id.ToString();

        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                userId),

            new Claim(
                ClaimTypes.NameIdentifier,
                userId),

            new Claim(
                ClaimTypes.Name,
                email),

            new Claim(
                ClaimTypes.Role,
                role)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(keyText));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresIn),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}

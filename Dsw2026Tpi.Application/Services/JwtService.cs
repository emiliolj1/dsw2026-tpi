using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dsw2026Tpi.Application.Options;
using Dsw2026Tpi.Data.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dsw2026Tpi.Application.Services;

public class JwtService
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;

    public JwtService(
        IOptions<JwtOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public string GenerateToken(ApplicationUser user, string role)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.Id == Guid.Empty)
        {
            throw new ArgumentException(
                "El usuario debe tener un identificador válido.",
                nameof(user));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        var email = user.Email ?? user.UserName
            ?? throw new InvalidOperationException(
                "El usuario no tiene un email configurado.");

        var now = _timeProvider.GetUtcNow();
        var userId = user.Id.ToString();

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString()),
            new Claim(
                JwtRegisteredClaimNames.Iat,
                now.ToUnixTimeSeconds().ToString(
                    CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(
                _options.ExpiresInMinutes).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

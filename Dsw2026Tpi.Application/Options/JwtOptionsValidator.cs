using System.Text;
using Microsoft.Extensions.Options;

namespace Dsw2026Tpi.Application.Options;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Key) ||
            Encoding.UTF8.GetByteCount(options.Key) < 32)
        {
            errors.Add(
                "Jwt:Key debe contener al menos 32 bytes UTF-8 " +
                "para firmar con HMAC-SHA256.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            errors.Add("Jwt:Issuer es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            errors.Add("Jwt:Audience es obligatorio.");
        }

        if (options.ExpiresInMinutes <= 0)
        {
            errors.Add("Jwt:ExpiresInMinutes debe ser mayor que cero.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}

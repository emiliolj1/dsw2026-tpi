using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Options;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.Data.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dsw2026Tpi.Application.Services;

public sealed class IdentitySeeder : IInitialAdminSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly InitialAdminOptions _options;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<InitialAdminOptions> options,
        ILogger<IdentitySeeder> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Los roles se verifican independientemente del alta del admin.
        await EnsureRoleExistsAsync(Roles.Administrator);
        await EnsureRoleExistsAsync(Roles.Patient);

        cancellationToken.ThrowIfCancellationRequested();

        var email = _options.Email?.Trim() ?? string.Empty;
        var password = _options.Password;

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Configuración incompleta del administrador inicial. " +
                "Configurá InitialAdmin:Email e InitialAdmin:Password " +
                "mediante secretos locales o variables de entorno.");
        }

        var user = await _userManager.FindByEmailAsync(email);
        // Una identidad existente debe ser un administrador utilizable.
        // No promover pacientes ni reutilizar cuentas dadas de baja.
        if (user is not null &&
            (user.Deleted ||
             !await _userManager.IsInRoleAsync(
                 user, Roles.Administrator) ||
             !await _userManager.HasPasswordAsync(user)))
        {
            throw new InvalidOperationException(
                "InitialAdmin:Email corresponde a una identidad " +
                "incompatible, eliminada o sin contraseña administrativa.");
        }
        var userCreated = false;

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(
                user,
                password);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo crear el administrador inicial: " +
                    FormatErrors(createResult));
            }

            userCreated = true;
        }

        if (!await _userManager.IsInRoleAsync(
                user,
                Roles.Administrator))
        {
            var roleResult = await _userManager.AddToRoleAsync(
                user,
                Roles.Administrator);

            if (!roleResult.Succeeded)
            {
                if (userCreated)
                {
                    var deleteResult =
                        await _userManager.DeleteAsync(user);

                    if (!deleteResult.Succeeded)
                    {
                        _logger.LogCritical(
                            "No se pudo eliminar el administrador incompleto.");
                    }
                }

                throw new InvalidOperationException(
                    $"No se pudo asignar el rol de administrador: " +
                    FormatErrors(roleResult));
            }
        }

        _logger.LogInformation(
            "Administrador inicial verificado correctamente.");
    }

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (await _roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await _roleManager.CreateAsync(
            new IdentityRole<Guid>(roleName));

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo crear el rol '{roleName}': " +
                FormatErrors(result));
        }
    }

    private static string FormatErrors(IdentityResult result)
    {
        return string.Join(
            "; ",
            result.Errors.Select(
                error => $"{error.Code}: {error.Description}"));
    }
}

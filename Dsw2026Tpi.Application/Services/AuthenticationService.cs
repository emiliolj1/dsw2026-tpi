using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Helpers;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.CrossCutting.Resources;
using Dsw2026Tpi.Data.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;


namespace Dsw2026Tpi.Application.Services;


public class AuthenticationService : IAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISignInService _signInManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly JwtService _jwtService;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly IPersistence _persistence;

    public AuthenticationService(UserManager<ApplicationUser> userManager,
        ISignInService signInManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        JwtService jwtService,
        ILogger<AuthenticationService> logger,
        IPersistence persistence)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _jwtService = jwtService;
        _logger = logger;
        _persistence = persistence;
    }

    public async Task<LoginAdminModel.Response> LoginAdmin(
    LoginAdminModel.Request request)
    {
        if (!request.Email.IsEmailValid() ||
     string.IsNullOrWhiteSpace(request.Password) ||
     request.Password.Length < 8)
        {
            throw new ValidationException();
        }

        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null || user.Deleted)
        {
            _logger.LogWarning(
                "Intento fallido de login administrativo.");

            throw new AuthenticationException();
        }

        var passwordIsValid =
            await _signInManager.CheckPassword(
                user,
                request.Password);

        var isAdministrator =
            await _userManager.IsInRoleAsync(
                user,
                Roles.Administrator);

        if (!passwordIsValid || !isAdministrator)
        {
            _logger.LogWarning(
                "Intento fallido de login administrativo para el usuario {UserId}.",
                user.Id);

            throw new AuthenticationException();
        }

        var token = _jwtService.GenerateToken(
            user,
            Roles.Administrator);

        _logger.LogInformation(
            "Login administrativo exitoso para el usuario {UserId}.",
            user.Id);

        return new LoginAdminModel.Response(
            token,
            Roles.Administrator.ToUpperInvariant());
    }

    public async Task<LoginPatientModel.Response> LoginPatient(
    LoginPatientModel.Request request)
    {
        if (!request.Email.IsEmailValid() ||
            request.Dni is < 1_000_000 or > 99_999_999)
        {
            throw new ValidationException();
        }

        var email = request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);

        if (user?.Deleted == true)
        {
            _logger.LogWarning(
                "Intento fallido de login de paciente para el usuario {UserId}.",
                user.Id);

            throw new AuthenticationException();
        }

        var patientByUser = user is null
            ? null
            : await _persistence.First<Patient>(
                patient => patient.UserId == user.Id);

        var patientByDni = await _persistence.First<Patient>(
            patient => patient.Dni == request.Dni);

        var credentialsMismatch =
            (patientByUser is not null &&
             patientByUser.Dni != request.Dni) ||
            (patientByDni is not null &&
             (user is null || patientByDni.UserId != user.Id));

        if (credentialsMismatch)
        {
            _logger.LogWarning(
                "Intento fallido de login de paciente para el usuario {UserId}.",
                user?.Id);

            throw new AuthenticationException();
        }

        if (!await _roleManager.RoleExistsAsync(Roles.Patient))
        {
            throw new InvalidOperationException(
                "El rol requerido para pacientes no está configurado.");
        }

        var userCreated = false;
        var roleAdded = false;

        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createResult =
                await _userManager.CreateAsync(user);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "No se pudo crear la identidad del paciente.");
            }

            userCreated = true;
        }

        var currentRoles =
            await _userManager.GetRolesAsync(user);

        var hasPatientRole = currentRoles.Contains(
            Roles.Patient,
            StringComparer.OrdinalIgnoreCase);

        if (currentRoles.Count > 0 && !hasPatientRole)
        {
            await CompensatePatientIdentity(
                user,
                userCreated,
                roleAdded);

            throw new AuthenticationException();
        }

        if (!hasPatientRole)
        {
            var roleResult = await _userManager.AddToRoleAsync(
                user,
                Roles.Patient);

            if (!roleResult.Succeeded)
            {
                await CompensatePatientIdentity(
                    user,
                    userCreated,
                    roleAdded);

                throw new InvalidOperationException(
                    "No se pudo asignar el rol del paciente.");
            }

            roleAdded = true;
        }

        if (patientByUser is null)
        {
            try
            {
                var patient = new Patient(
                    user.Id,
                    request.Dni);

                await _persistence.Add(patient);
            }
            catch
            {
                await CompensatePatientIdentity(
                    user,
                    userCreated,
                    roleAdded);

                throw;
            }
        }

        var token = _jwtService.GenerateToken(
            user,
            Roles.Patient);

        _logger.LogInformation(
            "Login de paciente exitoso para el usuario {UserId}.",
            user.Id);

        return new LoginPatientModel.Response(
            token,
            Roles.Patient.ToUpperInvariant());
    }
    private async Task CompensatePatientIdentity(
    ApplicationUser user,
    bool userCreated,
    bool roleAdded)
    {
        IdentityResult? compensationResult = null;

        if (userCreated)
        {
            compensationResult = await _userManager.DeleteAsync(user);
        }
        else if (roleAdded)
        {
            compensationResult = await _userManager.RemoveFromRoleAsync(
                user,
                Roles.Patient);
        }

        if (compensationResult is not null &&
            !compensationResult.Succeeded)
        {
            _logger.LogCritical(
                "No se pudo compensar una identidad de paciente incompleta.");
        }
    }
}

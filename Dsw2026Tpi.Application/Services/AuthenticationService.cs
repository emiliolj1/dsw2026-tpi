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
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly JwtService _jwtService;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly IPersistence _persistence;

    public AuthenticationService(UserManager<ApplicationUser> userManager,
        ISignInService signInManager,
        RoleManager<IdentityRole> roleManager,
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

    public async Task<LoginAdminModel.Response> LoginAdmin(LoginAdminModel.Request request)
    {
        if (!request.Email.IsEmailValid()) throw new AuthenticationException();
        var user = await _userManager.FindByEmailAsync(request.Email) ?? throw new AuthenticationException();
        var result = await _signInManager.CheckPassword(user, request.Password);

        if (!result)
        {
            _logger.LogError("Intento de login fallido para: {Email}", request.Email);
            throw new AuthenticationException();
        }

        var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault();

        var token  = _jwtService.GenerateToken(user.UserName!, role);

        return new LoginAdminModel.Response(
            token,
            role
        );
    }

    public async Task<LoginPatientModel.Response> LoginPatient(LoginPatientModel.Request request)
    {
        if (!request.Email.IsEmailValid() ||
            request.Dni is < 1_000_000 or > 99_999_999)
        {
            throw new ValidationException();
        }

        var normalizedEmail = Patient.NormalizeEmail(request.Email);

        var patientByEmail = await _persistence.First<Patient>(
            patient => patient.NormalizedEmail == normalizedEmail);

        var patientByDni = await _persistence.First<Patient>(
            patient => patient.Dni == request.Dni);

        if (patientByEmail is not null || patientByDni is not null)
        {
            var credentialsMatch =
                patientByEmail is not null &&
                patientByDni is not null &&
                patientByEmail.Id == patientByDni.Id;

            if (!credentialsMatch)
            {
                throw new AuthenticationException();
            }
        }

        if (!await _roleManager.RoleExistsAsync(Roles.Patient))
        {
            throw new InvalidOperationException(
                "El rol requerido para pacientes no está configurado.");
        }

        var email = patientByEmail?.Email ?? request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);

        var userCreated = false;
        var roleAdded = false;

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "No se pudo crear la identidad del paciente.");
            }

            userCreated = true;
        }

        var currentRoles = await _userManager.GetRolesAsync(user);

        var hasPatientRole = currentRoles.Contains(
            Roles.Patient,
            StringComparer.OrdinalIgnoreCase);

        if (currentRoles.Count > 0 && !hasPatientRole)
        {
            await CompensatePatientIdentity(user, userCreated, roleAdded);
            throw new AuthenticationException();
        }

        if (!hasPatientRole)
        {
            var roleResult = await _userManager.AddToRoleAsync(
                user,
                Roles.Patient);

            if (!roleResult.Succeeded)
            {
                await CompensatePatientIdentity(user, userCreated, roleAdded);

                throw new InvalidOperationException(
                    "No se pudo asignar el rol del paciente.");
            }

            roleAdded = true;
        }

        if (patientByEmail is null)
        {
            try
            {
                var patient = new Patient(email, request.Dni);
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
            user.UserName!,
            Roles.Patient);

        return new LoginPatientModel.Response(
            token,
            Roles.Patient.ToUpperInvariant());
    }

    public async Task<RegisterModel.Response> Register(RegisterModel.Request request)
    {
        if (!request.Email.IsEmailValid()) throw new ValidationException(ErrorCodes.REGISTER_USER_INVALID,
            nameof(ErrorCodes.REGISTER_USER_INVALID));

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded) throw new ConflictException(nameof(ErrorCodes.REGISTER_USER_CONFLICT),
            ErrorCodes.REGISTER_USER_CONFLICT)
                .WithDetail(result.Errors.Select(e => (e.Code, e.Description)));
       
        _ = await _userManager.AddToRoleAsync(user, Roles.Administrator);

        _logger.LogInformation("Usuario registrado: {Email}", request.Email);

        return new RegisterModel.Response(request.Email);
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

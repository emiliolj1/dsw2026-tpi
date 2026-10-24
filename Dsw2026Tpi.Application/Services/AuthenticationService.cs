using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Helpers;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.Data;
using Dsw2026Tpi.Data.Identity;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Dsw2026Tpi.Application.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISignInService _signInManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly JwtService _jwtService;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly IPersistence _persistence;
    private readonly IClinicalWriteScopeFactory _writeScopeFactory;
    private readonly Dsw2026TpiDbContext _applicationContext;
    private readonly AuthenticationDbContext _identityContext;

    public AuthenticationService(
        UserManager<ApplicationUser> userManager,
        ISignInService signInManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        JwtService jwtService,
        ILogger<AuthenticationService> logger,
        IPersistence persistence,
        IClinicalWriteScopeFactory writeScopeFactory,
        Dsw2026TpiDbContext applicationContext,
        AuthenticationDbContext identityContext)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _jwtService = jwtService;
        _logger = logger;
        _persistence = persistence;
        _writeScopeFactory = writeScopeFactory;
        _applicationContext = applicationContext;
        _identityContext = identityContext;
    }

    public async Task<LoginAdminModel.Response> LoginAdmin(
        LoginAdminModel.Request request)
    {
        if (request is null ||
            !request.Email.IsEmailValid() ||
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

        var passwordIsValid = await _signInManager.CheckPassword(
            user,
            request.Password);

        var isAdministrator = await _userManager.IsInRoleAsync(
            user,
            Roles.Administrator);

        if (!passwordIsValid || !isAdministrator)
        {
            _logger.LogWarning(
                "Intento fallido de login administrativo para {UserId}.",
                user.Id);

            throw new AuthenticationException();
        }

        var token = _jwtService.GenerateToken(
            user,
            Roles.Administrator);

        _logger.LogInformation(
            "Login administrativo exitoso para {UserId}.",
            user.Id);

        return new LoginAdminModel.Response(
            token,
            Roles.Administrator.ToUpperInvariant());
    }

    public async Task<LoginPatientModel.Response> LoginPatient(
        LoginPatientModel.Request request)
    {
        if (request is null ||
            !request.Email.IsEmailValid() ||
            request.Dni is < 1_000_000 or > 99_999_999)
        {
            throw new ValidationException();
        }

        if (_identityContext.Database.CurrentTransaction is not null ||
            _identityContext.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException(
                "El login debe comenzar sin una transacción " +
                "ni cambios pendientes en Identity.");
        }

        // Adquiere el bloqueo SQL antes de las lecturas decisorias.
        await using var scope = await _writeScopeFactory.BeginAsync();

        var transaction =
            _applicationContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "El alcance no proporcionó una transacción SQL.");

        // Ambos contextos utilizan la misma base en este proyecto.
        // Application conserva la propiedad de conexión y transacción.
        _identityContext.Database.SetDbConnection(
            _applicationContext.Database.GetDbConnection(),
            contextOwnsConnection: false);

        await using var identityTransaction =
            await _identityContext.Database.UseTransactionAsync(
                transaction.GetDbTransaction());

        _identityContext.ChangeTracker.Clear();

        try
        {
            var result = await LoginPatientWithinScope(request);

            await scope.CompleteAsync();

            _logger.LogInformation(
                "Login de paciente confirmado.");

            return result;
        }
        catch
        {
            // El alcance revierte SQL; no conservar entidades fallidas.
            _identityContext.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<LoginPatientModel.Response> LoginPatientWithinScope(
        LoginPatientModel.Request request)
    {
        var email = request.Email.Trim();

        // Estas consultas se ejecutan después de adquirir el bloqueo.
        var user = await _userManager.FindByEmailAsync(email);

        if (user?.Deleted == true)
        {
            throw new AuthenticationException();
        }

        // También se consultan bajas lógicas para no recrear vínculos
        // eliminados ni colisionar con sus índices únicos.
        var patients = _applicationContext.Set<Patient>()
            .IgnoreQueryFilters();

        var patientByUser = user is null
            ? null
            : await patients.SingleOrDefaultAsync(
                patient => patient.UserId == user.Id);

        var patientByDni = await patients.SingleOrDefaultAsync(
            patient => patient.Dni == request.Dni);

        var credentialsMismatch =
            patientByUser?.Deleted == true ||
            patientByDni?.Deleted == true ||
            (patientByUser is not null &&
                patientByUser.Dni != request.Dni) ||
            (patientByDni is not null &&
                (user is null || patientByDni.UserId != user.Id));

        if (credentialsMismatch)
        {
            throw new AuthenticationException();
        }

        if (!await _roleManager.RoleExistsAsync(Roles.Patient))
        {
            throw new InvalidOperationException(
                "El rol requerido para pacientes no está configurado.");
        }

        if (user is null)
        {
            var now = DateTime.UtcNow;

            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                CreatedAt = now,
                UpdatedAt = now
            };

            var createResult = await _userManager.CreateAsync(user);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "No se pudo crear la identidad del paciente.");
            }
        }

        var currentRoles = await _userManager.GetRolesAsync(user);

        // No convertir una identidad con otro rol en paciente.
        if (currentRoles.Any(role =>
            !string.Equals(
                role,
                Roles.Patient,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new AuthenticationException();
        }

        var hasPatientRole = currentRoles.Contains(
            Roles.Patient,
            StringComparer.OrdinalIgnoreCase);

        if (!hasPatientRole)
        {
            var roleResult = await _userManager.AddToRoleAsync(
                user,
                Roles.Patient);

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "No se pudo asignar el rol del paciente.");
            }
        }

        if (patientByUser is null)
        {
            await _persistence.Add(
                new Patient(user.Id, request.Dni));
        }

        // Generar el token antes de confirmar permite revertir también
        // el alta si existe un error de configuración al emitirlo.
        var token = _jwtService.GenerateToken(user, Roles.Patient);

        return new LoginPatientModel.Response(
            token,
            Roles.Patient.ToUpperInvariant());
    }
}

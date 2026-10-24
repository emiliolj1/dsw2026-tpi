using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.Data.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Dsw2026Tpi.Domain.Entities;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class AuthenticationSqlTests
    : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public AuthenticationSqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RepeatedSeedingPreservesUsersRolesAndAssignments()
    {
        var before = await ReadIdentityState();

        // Cada ejecución utiliza servicios y contextos nuevos.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var scope =
                _fixture.Factory.Services.CreateAsyncScope();

            var seeder = scope.ServiceProvider
                .GetRequiredService<IInitialAdminSeeder>();

            await seeder.SeedAsync();
        }

        var after = await ReadIdentityState();

        Assert.Equal(before.Users, after.Users);
        Assert.Equal(before.Roles, after.Roles);
        Assert.Equal(before.Assignments, after.Assignments);

        await using var verification =
            _fixture.Factory.Services.CreateAsyncScope();

        var users = verification.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var roles = verification.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        Assert.True(await roles.RoleExistsAsync(Roles.Administrator));
        Assert.True(await roles.RoleExistsAsync(Roles.Patient));

        var admin = await users.FindByEmailAsync(
            SqlServerWebApplicationFactory.AdminEmail);

        Assert.NotNull(admin);
        Assert.False(admin.Deleted);

        Assert.True(await users.CheckPasswordAsync(
            admin,
            SqlServerWebApplicationFactory.AdminPassword));

        Assert.True(await users.IsInRoleAsync(
            admin,
            Roles.Administrator));
    }

    [Fact]
    public async Task AdministratorLoginAndLogoutRevokeOnlyTheUsedToken()
    {
        using (var anonymousRequest =
            new HttpRequestMessage(HttpMethod.Get, "/health-check"))
        using (var anonymousResponse =
            await _fixture.Client.SendAsync(anonymousRequest))
        {
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                anonymousResponse.StatusCode);
        }

        // Tres solicitudes de login: por debajo del límite de cinco.
        using (var invalidLogin = await _fixture.Client.PostAsJsonAsync(
            "/api/auth/admin/login",
            new
            {
                email = SqlServerWebApplicationFactory.AdminEmail,
                password = "IncorrectPassword2026!"
            }))
        {
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                invalidLogin.StatusCode);
        }

        var firstToken = await LoginAdministrator();
        var secondToken = await LoginAdministrator();

        Assert.NotEqual(firstToken, secondToken);

        Assert.Equal(
            HttpStatusCode.OK,
            await AccessProtectedResource(firstToken));

        using (var logoutRequest =
            new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout"))
        {
            logoutRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", firstToken);

            using var logoutResponse =
                await _fixture.Client.SendAsync(logoutRequest);

            Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

            Assert.Equal(
                "text/plain",
                logoutResponse.Content.Headers.ContentType?.MediaType);

            Assert.Equal(
                "ok",
                await logoutResponse.Content.ReadAsStringAsync());
        }

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            await AccessProtectedResource(firstToken));

        Assert.Equal(
            HttpStatusCode.OK,
            await AccessProtectedResource(secondToken));
    }

    [Fact]
    public async Task PatientLoginReusesIdentityAndRejectsIncompatibleCredentials()
    {
        var email = $"patient-{Guid.NewGuid():N}@example.test";
        var otherEmail = $"other-{Guid.NewGuid():N}@example.test";
        const long dni = 73_500_001;
        const long otherDni = 73_500_002;

        int initialUsers;
        int initialPatients;

        await using (var identity =
            _fixture.CreateAuthenticationContext())
        await using (var application =
            _fixture.CreateApplicationContext())
        {
            initialUsers = await identity.Users.CountAsync();
            initialPatients =
                await application.Set<Patient>().CountAsync();
        }

        var firstToken = await LoginPatient(email, dni);
        var secondToken = await LoginPatient(email, dni);

        Assert.NotEqual(firstToken, secondToken);

        Assert.Equal(
            HttpStatusCode.OK,
            await AccessProtectedResource(firstToken));

        Assert.Equal(
            HttpStatusCode.OK,
            await AccessProtectedResource(secondToken));

        // Email existente con otro DNI.
        // DNI existente con otro email.
        // Identidad administrativa usada como paciente.
        var incompatibleCredentials = new[]
        {
            new { email, dni = otherDni },
            new { email = otherEmail, dni },
            new
            {
                email = SqlServerWebApplicationFactory.AdminEmail,
                dni = otherDni
            }
        };

        foreach (var credentials in incompatibleCredentials)
        {
            using var response =
                await _fixture.Client.PostAsJsonAsync(
                    "/api/auth/patient/login",
                    credentials);

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                response.StatusCode);
        }

        await using var verification =
            _fixture.Factory.Services.CreateAsyncScope();

        var users = verification.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await users.FindByEmailAsync(email);

        Assert.NotNull(user);
        Assert.True(await users.IsInRoleAsync(user, Roles.Patient));
        Assert.False(
            await users.IsInRoleAsync(user, Roles.Administrator));

        Assert.Null(await users.FindByEmailAsync(otherEmail));

        await using var identityVerification =
            _fixture.CreateAuthenticationContext();

        await using var applicationVerification =
            _fixture.CreateApplicationContext();

        Assert.Equal(
            initialUsers + 1,
            await identityVerification.Users.CountAsync());

        Assert.Equal(
            initialPatients + 1,
            await applicationVerification.Set<Patient>().CountAsync());

        var patient = await applicationVerification.Set<Patient>()
            .AsNoTracking()
            .SingleAsync(current => current.Dni == dni);

        Assert.Equal(user.Id, patient.UserId);

        Assert.Equal(
            1,
            await applicationVerification.Set<Patient>()
                .CountAsync(current => current.UserId == user.Id));

        Assert.False(
            await applicationVerification.Set<Patient>()
                .AnyAsync(current => current.Dni == otherDni));
    }

    [Theory]
    [InlineData("user")]
    [InlineData("patient")]
    public async Task DeletedUserOrPatientCannotLoginOrBeRecreated(
    string deletedEntity)
    {
        var email = $"deleted-{Guid.NewGuid():N}@example.test";
        var dni = deletedEntity == "user"
            ? 76_000_001L
            : 76_000_002L;

        await LoginPatient(email, dni);

        Guid userId;
        Guid patientId;

        await using (var identity =
            _fixture.CreateAuthenticationContext())
        await using (var application =
            _fixture.CreateApplicationContext())
        {
            var user = await identity.Users.SingleAsync(
                current =>
                    current.NormalizedEmail == email.ToUpperInvariant());

            var patient = await application.Set<Patient>()
                .SingleAsync(current => current.Dni == dni);

            userId = user.Id;
            patientId = patient.Id;

            if (deletedEntity == "user")
            {
                user.Deleted = true;
                await identity.SaveChangesAsync();
            }
            else
            {
                patient.Deleted = true;
                await application.SaveChangesAsync();
            }
        }

        var before = await ReadIdentityState();

        using var response = await _fixture.Client.PostAsJsonAsync(
            "/api/auth/patient/login",
            new { email, dni });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        var after = await ReadIdentityState();

        Assert.Equal(before.Users, after.Users);
        Assert.Equal(before.Roles, after.Roles);
        Assert.Equal(before.Assignments, after.Assignments);

        await using var identityVerification =
            _fixture.CreateAuthenticationContext();

        await using var applicationVerification =
            _fixture.CreateApplicationContext();

        var persistedUser = await identityVerification.Users
            .AsNoTracking()
            .SingleAsync(current =>
                current.NormalizedEmail == email.ToUpperInvariant());

        var persistedPatient = await applicationVerification.Set<Patient>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(current => current.Dni == dni);

        Assert.Equal(userId, persistedUser.Id);
        Assert.Equal(patientId, persistedPatient.Id);
        Assert.Equal(userId, persistedPatient.UserId);

        Assert.Equal(
            deletedEntity == "user",
            persistedUser.Deleted);

        Assert.Equal(
            deletedEntity == "patient",
            persistedPatient.Deleted);
    }

    private async Task<string> LoginAdministrator()
    {
        using var response = await _fixture.Client.PostAsJsonAsync(
            "/api/auth/admin/login",
            new
            {
                email = SqlServerWebApplicationFactory.AdminEmail,
                password = SqlServerWebApplicationFactory.AdminPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            Roles.Administrator.ToUpperInvariant(),
            json.RootElement.GetProperty("role").GetString());

        var token = json.RootElement.GetProperty("token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));

        return token!;
    }

    private async Task<HttpStatusCode> AccessProtectedResource(
        string token)
    {
        using var request =
            new HttpRequestMessage(HttpMethod.Get, "/health-check");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await _fixture.Client.SendAsync(request);

        return response.StatusCode;
    }

    private async Task<IdentityState> ReadIdentityState()
    {
        await using var context =
            _fixture.CreateAuthenticationContext();

        var users = await context.Users
            .AsNoTracking()
            .OrderBy(user => user.Id)
            .Select(user => user.Id)
            .ToArrayAsync();

        var roles = await context.Roles
            .AsNoTracking()
            .OrderBy(role => role.Id)
            .Select(role => role.Id)
            .ToArrayAsync();

        var assignments = await context.UserRoles
            .AsNoTracking()
            .OrderBy(assignment => assignment.UserId)
            .ThenBy(assignment => assignment.RoleId)
            .Select(assignment => new
            {
                assignment.UserId,
                assignment.RoleId
            })
            .ToArrayAsync();

        return new IdentityState(
            users,
            roles,
            assignments
                .Select(assignment =>
                    $"{assignment.UserId:N}:{assignment.RoleId:N}")
                .ToArray());
    }

    private async Task<string> LoginPatient(string email, long dni)
    {
        using var response = await _fixture.Client.PostAsJsonAsync(
            "/api/auth/patient/login",
            new { email, dni });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            Roles.Patient.ToUpperInvariant(),
            json.RootElement.GetProperty("role").GetString());

        var token = json.RootElement.GetProperty("token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));

        return token!;
    }

    private sealed record IdentityState(
        Guid[] Users,
        Guid[] Roles,
        string[] Assignments);
}
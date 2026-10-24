using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class PatientLoginConcurrencySqlTests
    : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public PatientLoginConcurrencySqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("same-identity")]
    [InlineData("same-email")]
    [InlineData("same-dni")]
    public async Task ConcurrentFirstLoginsPreserveOneValidIdentity(
        string scenario)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var first = new Credentials(
            $"first-{suffix}@example.test",
            scenario switch
            {
                "same-identity" => 74_000_001,
                "same-email" => 74_000_011,
                _ => 74_000_021
            });

        var second = scenario switch
        {
            "same-identity" => first,

            "same-email" => new Credentials(
                first.Email,
                first.Dni + 1),

            "same-dni" => new Credentials(
                $"second-{suffix}@example.test",
                first.Dni),

            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

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

        // Las dos solicitudes esperan la misma señal de salida.
        // Cada solicitud HTTP obtiene su propio scope y sus DbContext.
        var start = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<HttpResponseMessage> Login(Credentials credentials)
        {
            await start.Task;

            return await _fixture.Client.PostAsJsonAsync(
                "/api/auth/patient/login",
                new
                {
                    email = credentials.Email,
                    dni = credentials.Dni
                });
        }

        var firstTask = Login(first);
        var secondTask = Login(second);

        start.SetResult(true);

        var responses = await Task.WhenAll(firstTask, secondTask);

        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        var statuses = responses
            .Select(response => response.StatusCode)
            .ToArray();

        var diagnostic =
            $"Escenario: {scenario}. " +
            $"Respuestas: {(int)statuses[0]}, {(int)statuses[1]}.";

        if (scenario == "same-identity")
        {
            Assert.True(
                statuses.All(status => status == HttpStatusCode.OK),
                diagnostic);
        }
        else
        {
            Assert.True(
                statuses.Count(status => status == HttpStatusCode.OK) == 1
                && statuses.Count(status =>
                    status == HttpStatusCode.Unauthorized) == 1,
                diagnostic);
        }

        foreach (var response in responses.Where(
            response => response.StatusCode == HttpStatusCode.OK))
        {
            using var json = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

            Assert.Equal(
                Roles.Patient.ToUpperInvariant(),
                json.RootElement.GetProperty("role").GetString());

            Assert.False(string.IsNullOrWhiteSpace(
                json.RootElement.GetProperty("token").GetString()));
        }

        var winner = firstResponse.StatusCode == HttpStatusCode.OK
            ? first
            : second;

        // Se verifica el estado persistido desde contextos nuevos.
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

        var patients = await applicationVerification.Set<Patient>()
            .AsNoTracking()
            .Where(patient =>
                patient.Dni == first.Dni || patient.Dni == second.Dni)
            .ToListAsync();

        var patient = Assert.Single(patients);

        var user = await identityVerification.Users
            .AsNoTracking()
            .SingleAsync(current => current.Id == patient.UserId);

        Assert.Equal(winner.Dni, patient.Dni);
        Assert.Equal(winner.Email, user.Email);
        Assert.False(user.Deleted);

        var assignedRoles = await (
            from assignment in identityVerification.UserRoles
            join role in identityVerification.Roles
                on assignment.RoleId equals role.Id
            where assignment.UserId == user.Id
            select role.Name)
            .ToArrayAsync();

        Assert.Equal(new[] { Roles.Patient }, assignedRoles);
    }

    private sealed record Credentials(string Email, long Dni);
}

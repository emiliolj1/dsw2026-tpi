using System.Net;
using System.Net.Http.Json;
using Dsw2026Tpi.Data;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class PatientLoginRollbackSqlTests
    : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public PatientLoginRollbackSqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FailureAfterPatientInsertRollsBackIdentityRoleAndPatient()
    {
        var email = $"rollback-{Guid.NewGuid():N}@example.test";
        const long dni = 75_000_001;

        var before = await ReadCounts();
        var failure = new FailAfterPatientSave(dni);

        // Host de prueba sobre la misma base exclusiva del fixture.
        // Solo agrega un fallo controlado para este DNI.
        await using (var failingFactory =
            _fixture.Factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddDbContext<Dsw2026TpiDbContext>(
                        options => options.AddInterceptors(failure));
                });
            }))
        {
            using var client = failingFactory.CreateClient();

            using var response = await client.PostAsJsonAsync(
                "/api/auth/patient/login",
                new { email, dni });

            Assert.True(
                failure.Triggered,
                "El fallo debe ocurrir después del guardado del paciente.");

            Assert.Equal(
                HttpStatusCode.InternalServerError,
                response.StatusCode);

            Assert.Equal(
                "application/json",
                response.Content.Headers.ContentType?.MediaType);

            var error = await response.Content
                .ReadFromJsonAsync<JsonElement>();

            Assert.Equal(
                "UNHANDLED_ERROR",
                error.GetProperty("errorCode").GetString());

            Assert.False(string.IsNullOrWhiteSpace(
                error.GetProperty("message").GetString()));

            Assert.DoesNotContain(
                "Fallo artificial",
                error.GetRawText());
        }

        Assert.Equal(before, await ReadCounts());

        await using (var identity =
            _fixture.CreateAuthenticationContext())
        await using (var application =
            _fixture.CreateApplicationContext())
        {
            Assert.False(await identity.Users.AnyAsync(
                user => user.NormalizedEmail == email.ToUpperInvariant()));

            Assert.False(await application.Set<Patient>()
                .IgnoreQueryFilters()
                .AnyAsync(patient => patient.Dni == dni));
        }

        // Sin el fallo, las mismas credenciales deben poder registrarse.
        // También comprueba que el bloqueo anterior fue liberado.
        using var retry = await _fixture.Client.PostAsJsonAsync(
            "/api/auth/patient/login",
            new { email, dni });

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

        var afterRetry = await ReadCounts();

        Assert.Equal(before.Users + 1, afterRetry.Users);
        Assert.Equal(before.Patients + 1, afterRetry.Patients);
        Assert.Equal(before.Assignments + 1, afterRetry.Assignments);

        await using var identityVerification =
            _fixture.CreateAuthenticationContext();

        await using var applicationVerification =
            _fixture.CreateApplicationContext();

        var user = await identityVerification.Users
            .AsNoTracking()
            .SingleAsync(current =>
                current.NormalizedEmail == email.ToUpperInvariant());

        var patient = await applicationVerification.Set<Patient>()
            .AsNoTracking()
            .SingleAsync(current => current.Dni == dni);

        Assert.Equal(user.Id, patient.UserId);
    }

    private async Task<(int Users, int Patients, int Assignments)>
        ReadCounts()
    {
        await using var identity =
            _fixture.CreateAuthenticationContext();

        await using var application =
            _fixture.CreateApplicationContext();

        return (
            await identity.Users.CountAsync(),
            await application.Set<Patient>()
                .IgnoreQueryFilters()
                .CountAsync(),
            await identity.UserRoles.CountAsync());
    }

    private sealed class FailAfterPatientSave : SaveChangesInterceptor
    {
        private readonly long _dni;

        public FailAfterPatientSave(long dni)
        {
            _dni = dni;
        }

        public bool Triggered { get; private set; }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is Dsw2026TpiDbContext context &&
                context.ChangeTracker.Entries<Patient>()
                    .Any(entry => entry.Entity.Dni == _dni))
            {
                Triggered = true;

                throw new InvalidOperationException(
                    "Fallo artificial después del INSERT de paciente.");
            }

            return ValueTask.FromResult(result);
        }
    }
}

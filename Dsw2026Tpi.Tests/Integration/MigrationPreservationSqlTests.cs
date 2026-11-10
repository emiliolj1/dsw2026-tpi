using System.Data;
using Dsw2026Tpi.Data;
using Dsw2026Tpi.Data.Identity;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dsw2026Tpi.Tests.Integration;

/// <summary>
/// N04: prueba de migración con datos sintéticos de un esquema compatible,
/// siempre sobre una base SQL nueva exclusiva con el prefijo del fixture C01.
/// No intenta actualizar ni convertir una base Identity anterior a GUID.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class MigrationPreservationSqlTests
{
    private const string MigrationBeforeMonthlyRules =
        "20260803153241_LinkPatientToIdentityUser";

    [Fact]
    public async Task AddMonthlyAvailabilityRules_PreservesBookedAppointmentAndIdentity()
    {
        // C01 genera un catálogo nuevo con GUID, independiente de desarrollo.
        var isolated = SqlServerTestConnection.Create();
        var appOptions = new DbContextOptionsBuilder<Dsw2026TpiDbContext>()
            .UseSqlServer(isolated.ConnectionString).Options;
        var identityOptions = new DbContextOptionsBuilder<AuthenticationDbContext>()
            .UseSqlServer(isolated.ConnectionString).Options;

        var specialtyId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        byte[] oldRowVersion;

        try
        {
            // En base nueva, Identity migra hasta el esquema GUID actual.
            await using (var identity = new AuthenticationDbContext(identityOptions))
            {
                await identity.Database.MigrateAsync();
                identity.Users.Add(new ApplicationUser
                {
                    Id = userId,
                    UserName = $"n04-{userId:N}@example.test",
                    NormalizedUserName = $"N04-{userId:N}@EXAMPLE.TEST",
                    Email = $"n04-{userId:N}@example.test",
                    NormalizedEmail = $"N04-{userId:N}@EXAMPLE.TEST",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                identity.Roles.Add(new IdentityRole<Guid>
                {
                    Id = roleId,
                    Name = "N04_TEST_PATIENT",
                    NormalizedName = "N04_TEST_PATIENT"
                });
                identity.UserRoles.Add(new IdentityUserRole<Guid>
                {
                    UserId = userId,
                    RoleId = roleId
                });
                await identity.SaveChangesAsync();
            }

            // Aplicar solo hasta la migración inmediatamente ANTERIOR a N01.
            await using (var before = new Dsw2026TpiDbContext(appOptions))
            {
                await before.Database.GetService<IMigrator>()
                    .MigrateAsync(MigrationBeforeMonthlyRules);

                // SQL parametrizado: el modelo C# actual ya tiene AvailabilityRuleId,
                // columna que todavía NO existe en la tabla de este esquema.
                await before.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO dbo.Specialities
                      (Id, Name, Description, CreatedAt, UpdatedAt, Deleted)
                    VALUES ({specialtyId}, N'N04 sintética', N'Prueba aislada',
                      SYSUTCDATETIME(), SYSUTCDATETIME(), 0);
                    """);

                await before.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO dbo.Doctors
                      (Id, Name, LicenseNumber, IsActive, SpecialityId,
                       CreatedAt, UpdatedAt, Deleted)
                    VALUES ({doctorId}, N'Médico N04', N'N04-TEST', 1, {specialtyId},
                      SYSUTCDATETIME(), SYSUTCDATETIME(), 0);
                    """);

                await before.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO dbo.Patients
                      (Id, UserId, Dni, FullName, CreatedAt, UpdatedAt, Deleted)
                    VALUES ({patientId}, {userId}, 980000001, N'Paciente N04',
                      SYSUTCDATETIME(), SYSUTCDATETIME(), 0);
                    """);

                await before.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO dbo.Availabilities
                      (Id, DoctorId, Date, StartTime, EndTime, Status,
                       CreatedAt, UpdatedAt, Deleted)
                    VALUES ({slotId}, {doctorId}, '2026-10-01', '09:00:00',
                      '09:30:00', 'Booked', SYSUTCDATETIME(), SYSUTCDATETIME(), 0);
                    """);

                await before.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO dbo.Appointments
                      (Id, DoctorId, AvailabilityId, PatientId, Reason, Status,
                       CreatedAt, UpdatedAt, Deleted)
                    VALUES ({appointmentId}, {doctorId}, {slotId}, {patientId},
                      N'Control sintético N04', 'BOOKED',
                      SYSUTCDATETIME(), SYSUTCDATETIME(), 0);
                    """);
            }

            oldRowVersion = await ReadSlotRowVersionAsync(
                isolated.ConnectionString, slotId);

            // N01 tiene que ser aditiva; reejecutar Migrate es idempotente.
            await using (var migrate = new Dsw2026TpiDbContext(appOptions))
            {
                await migrate.Database.MigrateAsync();
                await migrate.Database.MigrateAsync();
            }

            await using (var check = new Dsw2026TpiDbContext(appOptions))
            {
                var doctor = await check.Set<Doctor>().AsNoTracking()
                    .SingleAsync(d => d.Id == doctorId);
                var patient = await check.Set<Patient>().AsNoTracking()
                    .SingleAsync(p => p.Id == patientId);
                var slot = await check.Set<Availability>().AsNoTracking()
                    .SingleAsync(s => s.Id == slotId);
                var appointment = await check.Set<Appointment>().AsNoTracking()
                    .SingleAsync(a => a.Id == appointmentId);

                Assert.Equal(specialtyId, doctor.SpecialityId);
                Assert.Equal(userId, patient.UserId);
                Assert.Equal(doctorId, slot.DoctorId);
                Assert.Null(slot.AvailabilityRuleId); // Slot histórico, no inventar regla.
                Assert.Equal(AvailabilityStatus.Booked, slot.Status);
                Assert.Equal(oldRowVersion, slot.RowVersion);
                Assert.Equal(doctorId, appointment.DoctorId);
                Assert.Equal(patientId, appointment.PatientId);
                Assert.Equal(slotId, appointment.AvailabilityId);
                Assert.Equal(AppointmentStatus.BOOKED, appointment.Status);
                Assert.False(appointment.Deleted);
                Assert.Empty(await check.Set<AvailabilityRule>().ToListAsync());

                // La nueva FK debe ser opcional y nunca cascada.
                var fk = await check.Database.SqlQueryRaw<int>("""
                    SELECT CAST(COUNT_BIG(*) AS int) AS [Value]
                    FROM sys.foreign_keys fk
                    WHERE fk.name = N'FK_Availabilities_AvailabilityRules_AvailabilityRuleId'
                      AND fk.delete_referential_action = 0
                    """).SingleAsync();
                var nullableColumn = await check.Database.SqlQueryRaw<int>("""
                    SELECT CAST(COUNT_BIG(*) AS int) AS [Value]
                    FROM sys.columns c
                    WHERE c.object_id = OBJECT_ID(N'dbo.Availabilities')
                      AND c.name = N'AvailabilityRuleId' AND c.is_nullable = 1
                    """).SingleAsync();
                Assert.Equal(1, fk);
                Assert.Equal(1, nullableColumn);
            }

            await using (var verifyIdentity = new AuthenticationDbContext(identityOptions))
            {
                Assert.True(await verifyIdentity.Users.AsNoTracking()
                    .AnyAsync(u => u.Id == userId));
                Assert.True(await verifyIdentity.UserRoles.AsNoTracking()
                    .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId));
            }
        }
        finally
        {
            // Borrado SOLO de la base exclusiva generada por C01.
            // Si no coincide prefijo + GUID, fallar antes de borrar.
            var builder = new SqlConnectionStringBuilder(isolated.ConnectionString);
            var databaseName = isolated.DatabaseName;
            var prefix = SqlServerTestConnection.DatabasePrefix;
            if (!string.Equals(builder.InitialCatalog, databaseName,
                    StringComparison.Ordinal) ||
                !databaseName.StartsWith(prefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(databaseName[prefix.Length..], "N", out _))
            {
                throw new InvalidOperationException(
                    "N04: limpieza bloqueada porque el destino no es una base aislada.");
            }

            await using var cleanup = new Dsw2026TpiDbContext(appOptions);
            if (await cleanup.Database.CanConnectAsync())
                await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<byte[]> ReadSlotRowVersionAsync(
        string connectionString, Guid slotId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT [RowVersion] FROM dbo.Availabilities WHERE [Id] = @id";
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier)
        {
            Value = slotId
        });
        return (byte[])(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("No se encontró el slot original."));
    }
}


using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.Data.Identity;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Application.Dtos;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class CatalogRelationsSqlTests
    : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;
    private static int _nextPatientDni = 30_000_000;

    public CatalogRelationsSqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(true, false, HttpStatusCode.Conflict)]
    [InlineData(false, false, HttpStatusCode.OK)]
    [InlineData(true, true, HttpStatusCode.OK)]
    public async Task DeleteSpecialty_RespectsDoctorRelations(
        bool isActive,
        bool isDeleted,
        HttpStatusCode expectedStatus)
    {
        var speciality = new Speciality(
            "Cardiologia",
            "Especialidad para pruebas de relaciones SQL");

        var doctor = new Doctor(
            "Ana Perez",
            $"MP-{Guid.NewGuid():N}"[..15],
            speciality)
        {
            Deleted = isDeleted
        };

        if (!isActive)
        {
            doctor.Deactivate();
        }

        var originalTime = new DateTime(
            2026, 10, 1, 12, 0, 0,
            DateTimeKind.Utc);

        speciality.CreatedAt = originalTime;
        speciality.UpdatedAt = originalTime;
        doctor.CreatedAt = originalTime;
        doctor.UpdatedAt = originalTime;

        await using (var context = _fixture.CreateApplicationContext())
        {
            context.AddRange(speciality, doctor);
            await context.SaveChangesAsync();
        }

        var client = await GetAdminClient();

        using var response = await client.DeleteAsync(
            $"/api/specialties/{speciality.Id}");

        Assert.Equal(expectedStatus, response.StatusCode);

        if (expectedStatus == HttpStatusCode.Conflict)
        {
            var error =
                await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(
                "SPECIALTY_IN_USE",
                error.GetProperty("errorCode").GetString());
        }
        else
        {
            Assert.Equal(
                "text/plain",
                response.Content.Headers.ContentType?.MediaType);

            Assert.Equal(
                "ok",
                await response.Content.ReadAsStringAsync());
        }

        await using var verification =
            _fixture.CreateApplicationContext();

        var storedSpeciality = await verification.Set<Speciality>()
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == speciality.Id);

        var storedDoctor = await verification.Set<Doctor>()
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == doctor.Id);

        Assert.Equal(
            expectedStatus == HttpStatusCode.OK,
            storedSpeciality.Deleted);

        Assert.Equal(originalTime, storedSpeciality.CreatedAt);

        if (expectedStatus == HttpStatusCode.Conflict)
        {
            Assert.Equal(originalTime, storedSpeciality.UpdatedAt);
        }

        Assert.Equal(isActive, storedDoctor.IsActive);
        Assert.Equal(isDeleted, storedDoctor.Deleted);
        Assert.Equal(speciality.Id, storedDoctor.SpecialityId);
        Assert.Equal(originalTime, storedDoctor.CreatedAt);
        Assert.Equal(originalTime, storedDoctor.UpdatedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DoctorWithFutureBooking_RejectsDeleteOrReassignment(
    bool changeSpecialty)
    {
        var clock = _fixture.Factory.Services
            .GetRequiredService<IClinicClock>();

        var tomorrow = DateOnly.FromDateTime(
            clock.GetCurrentLocalDateTime().DateTime).AddDays(1);

        var data = await SeedAppointment(
            tomorrow,
            new TimeOnly(10, 0),
            AppointmentStatus.BOOKED);

        var client = await GetAdminClient();

        using var response = changeSpecialty
            ? await client.PutAsJsonAsync(
                $"/api/doctors/{data.Doctor.Id}",
                new
                {
                    name = "Nombre modificado",
                    licenseNumber = "MP-MODIFICADA",
                    specialtyId = data.Replacement.Id
                })
            : await client.DeleteAsync(
                $"/api/doctors/{data.Doctor.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "DOCTOR_HAS_FUTURE_APPOINTMENTS",
            error.GetProperty("errorCode").GetString());

        await using var verification =
            _fixture.CreateApplicationContext();

        var storedDoctor = await verification.Set<Doctor>()
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == data.Doctor.Id);

        Assert.True(storedDoctor.IsActive);
        Assert.False(storedDoctor.Deleted);
        Assert.Equal(data.Specialty.Id, storedDoctor.SpecialityId);
        Assert.Equal(data.Doctor.Name, storedDoctor.Name);
        Assert.Equal(data.Doctor.LicenseNumber, storedDoctor.LicenseNumber);
        Assert.Equal(data.Doctor.CreatedAt, storedDoctor.CreatedAt);
        Assert.Equal(data.Doctor.UpdatedAt, storedDoctor.UpdatedAt);

        var storedAppointment = await verification.Set<Appointment>()
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == data.Appointment.Id);

        Assert.Equal(AppointmentStatus.BOOKED, storedAppointment.Status);
        Assert.False(storedAppointment.Deleted);
        Assert.Equal(
            data.Appointment.UpdatedAt,
            storedAppointment.UpdatedAt);

        var storedSlot = await verification.Set<Availability>()
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == data.Slot.Id);

        Assert.Equal(AvailabilityStatus.Booked, storedSlot.Status);
        Assert.Equal(data.Slot.UpdatedAt, storedSlot.UpdatedAt);
    }

    private async Task<(
    Doctor Doctor,
    Speciality Specialty,
    Speciality Replacement,
    Availability Slot,
    Patient Patient,
    Appointment Appointment)> SeedAppointment(
        DateOnly date,
        TimeOnly startTime,
        AppointmentStatus status)
    {
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"patient-{userId:N}@example.test";

        await using (var scope =
            _fixture.Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = email,
                Email = email,
                CreatedAt = now,
                UpdatedAt = now
            };

            var created = await users.CreateAsync(
                user,
                "SqlTests2026!Patient");

            Assert.True(
                created.Succeeded,
                string.Join("; ", created.Errors.Select(error => error.Description)));

            var assigned = await users.AddToRoleAsync(user, Roles.Patient);

            Assert.True(
                assigned.Succeeded,
                string.Join("; ", assigned.Errors.Select(error => error.Description)));
        }

        var specialty = new Speciality(
            "Cardiologia",
            "Especialidad original para pruebas SQL");

        var replacement = new Speciality(
            "Neurologia",
            "Especialidad alternativa para pruebas SQL");

        var doctor = new Doctor(
            "Ana Perez",
            $"MP-{userId:N}"[..15],
            specialty);

        var slot = new Availability(doctor.Id, date, startTime);
        slot.Book();

        var patient = new Patient(
            userId,
            Interlocked.Increment(ref _nextPatientDni),
            "Paciente SQL");

        var appointment = new Appointment(
            doctor.Id,
            slot.Id,
            patient.Id,
            "Consulta de control SQL");

        switch (status)
        {
            case AppointmentStatus.CANCELLED:
                appointment.Cancel();
                slot.Release();
                break;

            case AppointmentStatus.ATTENDED:
                appointment.MarkAsAttended();
                break;

            case AppointmentStatus.NO_SHOW:
                appointment.MarkAsNoShow();
                break;

            case AppointmentStatus.BOOKED:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        EntityBase[] entities =
        [
            specialty,
        replacement,
        doctor,
        slot,
        patient,
        appointment
        ];

        foreach (var entity in entities)
        {
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
        }

        await using var context = _fixture.CreateApplicationContext();

        context.AddRange(entities);
        await context.SaveChangesAsync();

        return (
            doctor,
            specialty,
            replacement,
            slot,
            patient,
            appointment);
    }

    [Theory]
    [InlineData(-1, 0, AppointmentStatus.BOOKED, false, false)]
    [InlineData(0, -1, AppointmentStatus.BOOKED, false, false)]
    [InlineData(0, 0, AppointmentStatus.BOOKED, false, true)]
    [InlineData(0, 1, AppointmentStatus.BOOKED, false, true)]
    [InlineData(1, 0, AppointmentStatus.BOOKED, false, true)]
    [InlineData(1, 0, AppointmentStatus.CANCELLED, false, false)]
    [InlineData(-1, 0, AppointmentStatus.ATTENDED, false, false)]
    [InlineData(-1, 0, AppointmentStatus.NO_SHOW, false, false)]
    [InlineData(1, 0, AppointmentStatus.BOOKED, true, false)]
    public async Task DeleteDoctor_UsesClinicTimeAndAppointmentState(
    int dayOffset,
    int minuteOffset,
    AppointmentStatus status,
    bool appointmentDeleted,
    bool shouldConflict)
    {
        var fixedNow = new DateTimeOffset(
            2026, 10, 20, 10, 0, 0,
            TimeSpan.FromHours(-3));

        var date = DateOnly.FromDateTime(fixedNow.DateTime)
            .AddDays(dayOffset);

        var startTime = new TimeOnly(10, 0)
            .AddMinutes(minuteOffset);

        var data = await SeedAppointment(date, startTime, status);

        if (appointmentDeleted)
        {
            await using var setup = _fixture.CreateApplicationContext();

            var appointment = await setup.Set<Appointment>()
                .SingleAsync(entity => entity.Id == data.Appointment.Id);

            appointment.Deleted = true;

            await setup.SaveChangesAsync();
        }

        await using (var scope =
            _fixture.Factory.Services.CreateAsyncScope())
        {
            var service = ActivatorUtilities.CreateInstance<DoctorService>(
                scope.ServiceProvider,
                new FixedClinicClock(fixedNow));

            if (shouldConflict)
            {
                await Assert.ThrowsAsync<ConflictException>(
                    () => service.Delete(data.Doctor.Id));
            }
            else
            {
                await service.Delete(data.Doctor.Id);
            }
        }

        await using var verification =
            _fixture.CreateApplicationContext();

        var storedDoctor = await verification.Set<Doctor>()
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == data.Doctor.Id);

        Assert.Equal(shouldConflict, storedDoctor.IsActive);
        Assert.Equal(!shouldConflict, storedDoctor.Deleted);
        Assert.Equal(data.Doctor.CreatedAt, storedDoctor.CreatedAt);

        if (shouldConflict)
        {
            Assert.Equal(data.Doctor.UpdatedAt, storedDoctor.UpdatedAt);
        }

        var storedAppointment = await verification.Set<Appointment>()
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == data.Appointment.Id);

        Assert.Equal(status, storedAppointment.Status);
        Assert.Equal(appointmentDeleted, storedAppointment.Deleted);
        Assert.Equal(
            data.Appointment.CreatedAt,
            storedAppointment.CreatedAt);
        Assert.Equal(
            data.Appointment.UpdatedAt,
            storedAppointment.UpdatedAt);
    }

    [Theory]
    [InlineData(AppointmentStatus.BOOKED)]
    [InlineData(AppointmentStatus.CANCELLED)]
    [InlineData(AppointmentStatus.ATTENDED)]
    [InlineData(AppointmentStatus.NO_SHOW)]
    public async Task ValidDeletions_PreserveHistoryAndRejectNewBookings(
    AppointmentStatus status)
    {
        var clock = _fixture.Factory.Services
            .GetRequiredService<IClinicClock>();

        var today = DateOnly.FromDateTime(
            clock.GetCurrentLocalDateTime().DateTime);

        var data = await SeedAppointment(
            today.AddDays(-1),
            new TimeOnly(10, 0),
            status);

        var freeSlot = new Availability(
            data.Doctor.Id,
            today.AddDays(1),
            new TimeOnly(14, 0))
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await using (var setup = _fixture.CreateApplicationContext())
        {
            setup.Add(freeSlot);
            await setup.SaveChangesAsync();
        }

        var client = await GetAdminClient();

        using var doctorDeletion = await client.DeleteAsync(
            $"/api/doctors/{data.Doctor.Id}");

        Assert.Equal(HttpStatusCode.OK, doctorDeletion.StatusCode);
        Assert.Equal("ok", await doctorDeletion.Content.ReadAsStringAsync());

        using var specialtyDeletion = await client.DeleteAsync(
            $"/api/specialties/{data.Specialty.Id}");

        Assert.Equal(HttpStatusCode.OK, specialtyDeletion.StatusCode);
        Assert.Equal("ok", await specialtyDeletion.Content.ReadAsStringAsync());

        using var searchResponse = await client.GetAsync(
            "/api/appointments/search" +
            $"?doctorId={data.Doctor.Id}&pageSize=10&pageIndex=0");

        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var history = await searchResponse.Content
            .ReadFromJsonAsync<AppointmentModel.PagedResponse>();

        Assert.NotNull(history);
        Assert.Equal(1, history.Total);

        var historicalAppointment = Assert.Single(history.Data);

        Assert.Equal(data.Appointment.Id, historicalAppointment.Id);
        Assert.Equal(status.ToString(), historicalAppointment.Status);
        Assert.Equal(data.Doctor.Id, historicalAppointment.Doctor.Id);
        Assert.Equal(data.Doctor.Name, historicalAppointment.Doctor.Name);
        Assert.Equal(data.Specialty.Id, historicalAppointment.Specialty.Id);
        Assert.Equal(
            data.Specialty.Name,
            historicalAppointment.Specialty.Name);

        using var patientClient = _fixture.Factory.CreateClient();

        using var patientLogin = await patientClient.PostAsJsonAsync(
          "/api/auth/patient/login",
             new
             {
                       email = $"patient-{data.Patient.UserId:N}@example.test",
                       dni = data.Patient.Dni
             });

        Assert.Equal(HttpStatusCode.OK, patientLogin.StatusCode);

        var patientSession = await patientLogin.Content
            .ReadFromJsonAsync<JsonElement>();

        var patientToken = patientSession.GetProperty("token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(patientToken));

        patientClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", patientToken);

        using var slotsResponse = await patientClient.GetAsync(
            $"/api/availabilities?doctorId={data.Doctor.Id}");

        Assert.Equal(HttpStatusCode.NotFound, slotsResponse.StatusCode);

        await using (var scope =
            _fixture.Factory.Services.CreateAsyncScope())
        {
            var appointments = scope.ServiceProvider
                .GetRequiredService<IAppointmentService>();

            await Assert.ThrowsAsync<EntityNotFoundException>(
                () => appointments.Create(
                    new AppointmentModel.Request(
                        data.Doctor.Id,
                        freeSlot.Id,
                        new AppointmentModel.PatientRequest(data.Patient.Dni),
                        "Nueva reserva rechazada"),
                    data.Patient.UserId));
        }

        await using var verification =
            _fixture.CreateApplicationContext();

        Assert.False(await verification.Set<Appointment>()
            .AnyAsync(appointment => appointment.AvailabilityId == freeSlot.Id));

        var storedSlot = await verification.Set<Availability>()
            .AsNoTracking()
            .SingleAsync(slot => slot.Id == freeSlot.Id);

        Assert.Equal(AvailabilityStatus.Available, storedSlot.Status);
        Assert.False(storedSlot.Deleted);

        var storedHistory = await verification.Set<Appointment>()
            .AsNoTracking()
            .SingleAsync(appointment => appointment.Id == data.Appointment.Id);

        Assert.Equal(status, storedHistory.Status);
        Assert.False(storedHistory.Deleted);
        Assert.Equal(data.Appointment.CreatedAt, storedHistory.CreatedAt);
        Assert.Equal(data.Appointment.UpdatedAt, storedHistory.UpdatedAt);
    }

    [Fact]
    public async Task ReassignmentWithoutFutureBookings_AllowsDeletingOldSpecialty()
    {
        var clock = _fixture.Factory.Services
            .GetRequiredService<IClinicClock>();

        var tomorrow = DateOnly.FromDateTime(
            clock.GetCurrentLocalDateTime().DateTime).AddDays(1);

        var data = await SeedAppointment(
            tomorrow,
            new TimeOnly(10, 0),
            AppointmentStatus.CANCELLED);

        var client = await GetAdminClient();

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/doctors/{data.Doctor.Id}",
            new
            {
                name = data.Doctor.Name,
                licenseNumber = data.Doctor.LicenseNumber,
                specialtyId = data.Replacement.Id
            });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content
            .ReadFromJsonAsync<DoctorModel.Response>();

        Assert.NotNull(updated);
        Assert.Equal(data.Replacement.Id, updated.Specialty?.Id);

        using var deletion = await client.DeleteAsync(
            $"/api/specialties/{data.Specialty.Id}");

        Assert.Equal(HttpStatusCode.OK, deletion.StatusCode);

        await using var verification =
            _fixture.CreateApplicationContext();

        var doctor = await verification.Set<Doctor>()
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == data.Doctor.Id);

        Assert.True(doctor.IsActive);
        Assert.False(doctor.Deleted);
        Assert.Equal(data.Replacement.Id, doctor.SpecialityId);
        Assert.Equal(data.Doctor.CreatedAt, doctor.CreatedAt);

        var appointment = await verification.Set<Appointment>()
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == data.Appointment.Id);

        Assert.Equal(AppointmentStatus.CANCELLED, appointment.Status);
        Assert.Equal(data.Doctor.Id, appointment.DoctorId);
        Assert.Equal(data.Appointment.UpdatedAt, appointment.UpdatedAt);
    }
    private async Task<HttpClient> GetAdminClient()
    {
        // Los tests de esta clase comparten fixture y se ejecutan
        // secuencialmente. El login se realiza una vez para ese host.
        var client = _fixture.Client;

        if (client.DefaultRequestHeaders.Authorization is not null)
        {
            return client;
        }

        using var response = await client.PostAsJsonAsync(
            "/api/auth/admin/login",
            new
            {
                email = SqlServerWebApplicationFactory.AdminEmail,
                password = SqlServerWebApplicationFactory.AdminPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        var token = body.GetProperty("token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private sealed class FixedClinicClock : IClinicClock
    {
        private readonly DateTimeOffset _now;

        public FixedClinicClock(DateTimeOffset now)
        {
            _now = now;
        }

        public DateTimeOffset GetCurrentLocalDateTime()
        {
            return _now;
        }
    }
}
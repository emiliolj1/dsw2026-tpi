using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using System.Globalization;

namespace Dsw2026Tpi.Application.Services;

public class AppointmentService : IAppointmentService
{
    private const int MaxPageSize = 100;

    private static readonly string[] AppointmentIncludes = [nameof(Appointment.Doctor), nameof(Appointment.Availability), nameof(Appointment.Patient), $"{nameof(Appointment.Doctor)}.{nameof(Doctor.Speciality)}"];

    private readonly IPersistence _persistence;
    private readonly IAppointmentPersistence _appointmentPersistence;

    public AppointmentService(IPersistence persistence, IAppointmentPersistence appointmentPersistence)
    {
        _persistence = persistence;
        _appointmentPersistence = appointmentPersistence;
    }

    public async Task Create(AppointmentModel.Request request, string patientEmail)
    {
        var reason = ValidateRequest(request);

        var patient = await GetAuthenticatedPatient(patientEmail);

        if (patient.Dni != request.Patient.Dni)
        {
            throw new AuthorizationException();
        }

        var doctor = await _persistence.GetById<Doctor>(request.DoctorId);

        if (doctor is null)
            throw new EntityNotFoundException(nameof(Doctor));

        if (!doctor.IsActive)
            throw new ValidationException().WithDetail(nameof(request.DoctorId), "El médico no se encuentra activo.");

        var availability = await _persistence.GetById<Availability>(request.AvailabilityId);

        if (availability is null)
            throw new EntityNotFoundException(nameof(Availability));

        if (availability.DoctorId != doctor.Id)
            throw new ValidationException().WithDetail(nameof(request.AvailabilityId), "La disponibilidad no corresponde al médico indicado.");

        if (availability.Status != AvailabilityStatus.Available)
            throw new ConflictException("La disponibilidad no está disponible.", "APPOINTMENT_AVAILABILITY_CONFLICT");

        EnsureAvailabilityIsNotPast(availability);

        var appointment = new Appointment(doctor.Id, availability.Id, patient.Id, reason);

        var created = await _appointmentPersistence.TryCreate(appointment);

        if (!created)
            throw new ConflictException("No se pudo crear la cita", "APPOINTMENT_CREATION_CONFLICT");
     }

    public async Task<IEnumerable<AppointmentModel.Response>>GetActiveByPatient(long dni, string patientEmail)
    {
        ValidateDni(dni, nameof(dni));

        var patient = await GetAuthenticatedPatient(patientEmail);

        if (patient.Dni != dni)
        {
            throw new AuthorizationException();
        }

        var appointments =
            await _persistence.GetFiltered<Appointment>(
                appointment =>
                    appointment.PatientId == patient.Id &&
                    appointment.Status == AppointmentStatus.BOOKED,
                AppointmentIncludes)
            ?? [];

        return appointments
            .OrderBy(appointment =>
                appointment.Availability!.Date)
            .ThenBy(appointment =>
                appointment.Availability!.StartTime)
            .Select(ToResponse);
    }

    public async Task Cancel(Guid appointmentId, string patientEmail)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new ValidationException()
                .WithDetail(
                    nameof(appointmentId),
                    "Es obligatorio.");
        }

        var patient = await GetAuthenticatedPatient(patientEmail);

        var appointment =
            await _persistence.First<Appointment>(
                current =>
                    current.Id == appointmentId &&
                    current.PatientId == patient.Id);

        if (appointment is null)
        {
            throw new EntityNotFoundException(nameof(Appointment));
        }

        if (appointment.Status != AppointmentStatus.BOOKED)
        {
            throw new ConflictException(
                "Solo se puede cancelar una cita en estado BOOKED.",
                "APPOINTMENT_STATUS_CONFLICT");
        }

        var cancelled =
            await _appointmentPersistence.TryCancel(appointmentId);

        if (!cancelled)
        {
            throw new ConflictException(
                "La cita no pudo cancelarse porque fue modificada.",
                "APPOINTMENT_CONCURRENCY_CONFLICT");
        }
    }

    public async Task<IEnumerable<AppointmentModel.Response>> GetByDate(DateOnly date)
    {
        if (date == default)
        {
            throw new ValidationException().WithDetail(nameof(date), "Es obligatoria.");
        }

        var appointments = await _persistence.GetFiltered<Appointment>(appointment => appointment.Availability != null && appointment.Availability.Date == date, AppointmentIncludes) ?? [];

        return appointments.OrderBy(appointment => appointment.Availability!.StartTime).Select(ToResponse);
    }

    public async Task<AppointmentModel.PagedResponse> Search(AppointmentModel.SearchRequest request)
    {
        ValidateSearch(request);

        var appointments = await _persistence.Paginate<Appointment, DateOnly>( request.PageSize, request.PageIndex,
            appointment =>(!request.SpecialityId.HasValue || appointment.Doctor!.SpecialityId == request.SpecialityId.Value)
            && (!request.DoctorId.HasValue || appointment.DoctorId == request.DoctorId.Value)
            && (!request.Dni.HasValue || appointment.Patient!.Dni == request.Dni.Value)
            && (!request.Date.HasValue || appointment.Availability!.Date == request.Date.Value),
            appointment => appointment.Availability!.Date, AppointmentIncludes);

        return new AppointmentModel.PagedResponse(appointments.Data.Select(ToResponse), appointments.Total, appointments.PageSize, appointments.PageIndex);
    }

    private async Task<Patient> GetAuthenticatedPatient(string patientEmail)
    {
        if (string.IsNullOrWhiteSpace(patientEmail))
        {
            throw new AuthenticationException();
        }

        var normalizedEmail =
            Patient.NormalizeEmail(patientEmail);

        return await _persistence.First<Patient>(
            patient =>
                patient.NormalizedEmail == normalizedEmail)
            ?? throw new AuthenticationException();
    }

    private static string ValidateRequest(AppointmentModel.Request request)
    {
        if (request is null)
            throw new ValidationException().WithDetail(nameof(request), "El cuerpo de la solicitud es obligatorio.");

        var errors = new List<(string Field, string Issue)>();
        var reason = request.Reason?.Trim() ?? string.Empty;

        if (request.DoctorId == Guid.Empty)
            errors.Add((nameof(request.DoctorId), "Es obligatorio."));

        if (request.AvailabilityId == Guid.Empty)
            errors.Add((nameof(request.AvailabilityId), "Es obligatoria."));

        if (request.Patient is null)
            errors.Add((nameof(request.Patient), "Es obligatorio."));
        else
            AddDniError(request.Patient.Dni, "patient.dni", errors);

        if (string.IsNullOrWhiteSpace(request.Reason))
            errors.Add((nameof(request.Reason), "Es obligatorio."));
        else if (reason.Length < 5)
            errors.Add((nameof(request.Reason), "Debe contener al menos 5 caracteres."));

        ThrowIfInvalid(errors);

        return reason;
    }

    private static void ValidateSearch(AppointmentModel.SearchRequest request)
    {
        if (request is null)
            throw new ValidationException().WithDetail(nameof(request), "Los parámetros de búsqueda son obligatorios.");

        var errors = new List<(string Field, string Issue)>();

        if (request.PageSize < 1 || request.PageSize > MaxPageSize)
            errors.Add((nameof(request.PageSize), $"Debe estar entre 1 y {MaxPageSize}."));

        if (request.PageIndex < 0)
            errors.Add((nameof(request.PageIndex), "Debe ser mayor o igual a 0."));

        if (request.SpecialityId == Guid.Empty)
            errors.Add((nameof(request.SpecialityId), "No puede ser un Guid vacío."));

        if (request.DoctorId == Guid.Empty)
            errors.Add((nameof(request.DoctorId), "No puede ser un Guid vacío."));

        if (request.Dni.HasValue)
            AddDniError(request.Dni.Value, nameof(request.Dni), errors);

        ThrowIfInvalid(errors);
    }

    private static void ValidateDni(long dni, string field)
    {
        var errors = new List<(string Field, string Issue)>();

        AddDniError(dni, field, errors);
        ThrowIfInvalid(errors);
    }

    private static void AddDniError(long dni, string field, ICollection<(string Field, string Issue)> errors)
    {
        if (dni is < 1_000_000 or > 9_999_999_999)
            errors.Add((field, "Debe contener entre 7 y 10 dígitos."));
    }

    private static void EnsureAvailabilityIsNotPast(Availability availability)
    {
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);

        var isPast = availability.Date < today || availability.Date == today && availability.StartTime < currentTime;

        if (isPast)
            throw new ValidationException().WithDetail(nameof(Availability), "No se permiten turnos en el pasado.");
    }

    private static AppointmentModel.Response ToResponse(Appointment appointment)
    {
        var doctor = appointment.Doctor ?? throw new InvalidOperationException("No se cargó el médico de la cita.");

        var speciality = doctor.Speciality ?? throw new InvalidOperationException("No se cargó la especialidad del médico.");

        var patient = appointment.Patient ?? throw new InvalidOperationException("No se cargó el paciente de la cita.");

        var availability = appointment.Availability ?? throw new InvalidOperationException("No se cargó la disponibilidad de la cita.");

        var availableTime = $"{availability.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture)}-" +  $"{availability.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture)}";

        return new AppointmentModel.Response(appointment.Id,availability.Id,
            new AppointmentModel.SpecialityResponse(speciality.Id, speciality.Name),
            new AppointmentModel.DoctorResponse(doctor.Id, doctor.Name),
            patient.Dni,
            availability.Date,
            availableTime,
            appointment.Reason,
            appointment.Status.ToString());
    }
    private static void ThrowIfInvalid(
        IEnumerable<(string Field, string Issue)> errors)
    {
        var errorList = errors.ToList();

        if (errorList.Count > 0)
            throw new ValidationException().WithDetail(errorList);
    }
}

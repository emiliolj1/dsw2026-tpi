using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dsw2026Tpi.Application.Services;

public class AppointmentService : IAppointmentService
{
    private const int MaxPageSize = 100;

    private static readonly string[] AppointmentIncludes =
        [nameof(Appointment.Doctor),
         nameof(Appointment.Availability),
         nameof(Appointment.Patient),
         $"{nameof(Appointment.Doctor)}.{nameof(Doctor.Speciality)}"];

    private readonly IPersistence _persistence;
    private readonly IAppointmentPersistence _appointmentPersistence;
    private readonly IClinicClock _clock;
    private readonly IClinicalWriteScopeFactory _writeScopeFactory;
    private readonly ILogger<AppointmentService> _logger;

    public AppointmentService(
        IPersistence persistence,
        IAppointmentPersistence appointmentPersistence,
        IClinicClock clock,
        IClinicalWriteScopeFactory writeScopeFactory,
        ILogger<AppointmentService> logger)
    {
        _persistence = persistence;
        _appointmentPersistence = appointmentPersistence;
        _clock = clock;
        _writeScopeFactory = writeScopeFactory;
        _logger = logger;
    }

    public async Task<AppointmentModel.Response> Create(
        AppointmentModel.Request request,
        Guid patientUserId)
    {
        // Validaciones de formato: no necesitan base de datos ni bloqueo.
        var reason = ValidateRequest(request);

        // El alcance se abre ANTES de las lecturas que deciden la reserva.
        // Si algo falla y no se llama a CompleteAsync, todo se deshace.
        await using var scope = await _writeScopeFactory.BeginAsync();

        var patient = await GetAuthenticatedPatient(patientUserId);

        if (patient.Dni != request.Patient.Dni)
        {
            throw new AuthorizationException();
        }

        var doctor = await _persistence.GetById<Doctor>(request.DoctorId);
        if (doctor is null) throw new EntityNotFoundException(nameof(Doctor));
        if (!doctor.IsActive)
            throw new ValidationException().WithDetail(
                nameof(request.DoctorId), "El médico no se encuentra activo.");

        var availability = await _persistence.GetById<Availability>(request.AvailabilityId);
        if (availability is null)
            throw new EntityNotFoundException(nameof(Availability));
        if (availability.DoctorId != doctor.Id)
            throw new ValidationException().WithDetail(
                nameof(request.AvailabilityId),
                "La disponibilidad no corresponde al médico indicado.");
        if (availability.Status != AvailabilityStatus.Available)
        {
            _logger.LogWarning(
                "Reserva rechazada: el slot {SlotId} no está disponible.",
                availability.Id);

            throw new ConflictException(
                "La disponibilidad no está disponible.",
                "APPOINTMENT_AVAILABILITY_CONFLICT");
        }

        EnsureAvailabilityIsNotPast(availability);

        var appointment = new Appointment(
            doctor.Id, availability.Id, patient.Id, reason);

        var created = await _appointmentPersistence.TryCreate(appointment);
        if (!created)
        {
            _logger.LogWarning(
                "Reserva rechazada por conflicto al guardar el slot {SlotId}.",
                availability.Id);

            throw new ConflictException(
                "No se pudo crear la cita",
                "APPOINTMENT_CREATION_CONFLICT");
        }

        // Se arma la respuesta dentro del alcance: si falla, no se confirma nada.
        var persisted = await _persistence.First<Appointment>(
            current => current.Id == appointment.Id,
            AppointmentIncludes)
            ?? throw new InvalidOperationException(
                "No se pudo recuperar la cita creada.");

        var response = ToResponse(persisted);

        await scope.CompleteAsync();

        // Solo IDs: sin motivo clínico, DNI completo ni tokens.
        _logger.LogInformation(
            "Cita {AppointmentId} reservada (slot {SlotId}, médico {DoctorId}).",
            appointment.Id, availability.Id, doctor.Id);

        return response;
    }

    public async Task<IEnumerable<AppointmentModel.Response>> GetActiveByPatient(
        long dni,
        Guid patientUserId)
    {
        ValidateDni(dni, nameof(dni));
        var patient = await GetAuthenticatedPatient(patientUserId);

        if (patient.Dni != dni)
        {
            throw new AuthorizationException();
        }

        var now = _clock.GetCurrentLocalDateTime();
        var today = DateOnly.FromDateTime(now.DateTime);
        var currentTime = TimeOnly.FromDateTime(now.DateTime);

        var appointments = await _persistence.GetFiltered<Appointment>(
            appointment =>
                appointment.PatientId == patient.Id
                && appointment.Status == AppointmentStatus.BOOKED
                && appointment.Availability != null
                && (appointment.Availability.Date > today
                    || (appointment.Availability.Date == today
                        && appointment.Availability.StartTime >= currentTime)),
            AppointmentIncludes) ?? [];

        return appointments
            .OrderBy(appointment => appointment.Availability!.Date)
            .ThenBy(appointment => appointment.Availability!.StartTime)
            .ThenBy(appointment => appointment.Id)
            .Select(ToResponse);
    }

    public async Task Cancel(
        Guid appointmentId,
        Guid patientUserId)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new ValidationException()
                .WithDetail(nameof(appointmentId), "Es obligatorio.");
        }

        await using var scope = await _writeScopeFactory.BeginAsync();

        var patient = await GetAuthenticatedPatient(patientUserId);

        var appointment = await _persistence.First<Appointment>(
            current => current.Id == appointmentId
                && current.PatientId == patient.Id);

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

        var cancelled = await _appointmentPersistence.TryCancel(appointmentId);
        if (!cancelled)
        {
            _logger.LogWarning(
                "Cancelación rechazada por conflicto: cita {AppointmentId}.",
                appointmentId);

            throw new ConflictException(
                "La cita no pudo cancelarse porque fue modificada.",
                "APPOINTMENT_CONCURRENCY_CONFLICT");
        }

        await scope.CompleteAsync();

        _logger.LogInformation(
            "Cita {AppointmentId} cancelada.",
            appointmentId);
    }
    public async Task<AppointmentModel.Response> UpdateStatus(
    Guid appointmentId,
    AppointmentModel.UpdateStatusRequest request)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new ValidationException()
                .WithDetail(nameof(appointmentId), "Es obligatorio.");
        }

        var newStatus = ParseStatus(request?.Status);
        if (newStatus is not (AppointmentStatus.ATTENDED or AppointmentStatus.NO_SHOW))
        {
            throw new ValidationException().WithDetail(
                "status", "Debe ser ATTENDED o NO_SHOW.");
        }

        await using var scope = await _writeScopeFactory.BeginAsync();

        var appointment = await _persistence.First<Appointment>(
            current => current.Id == appointmentId,
            AppointmentIncludes)
            ?? throw new EntityNotFoundException(nameof(Appointment));

        if (appointment.Status != AppointmentStatus.BOOKED)
        {
            throw new ConflictException(
                "Solo se puede cambiar el estado de una cita BOOKED.",
                "APPOINTMENT_STATUS_CONFLICT");
        }

        var availability = appointment.Availability
            ?? throw new InvalidOperationException(
                "No se cargó la disponibilidad de la cita.");

        // Solo cuando el intervalo del turno ya terminó (hora del centro).
        var now = _clock.GetCurrentLocalDateTime();
        var today = DateOnly.FromDateTime(now.DateTime);
        var currentTime = TimeOnly.FromDateTime(now.DateTime);

        var hasEnded = availability.Date < today
            || (availability.Date == today && availability.EndTime <= currentTime);

        if (!hasEnded)
        {
            throw new ConflictException(
                "El turno todavía no terminó.",
                "APPOINTMENT_NOT_FINISHED");
        }

        var updated = await _appointmentPersistence
            .TryUpdateStatus(appointmentId, newStatus.Value);

        if (!updated)
        {
            throw new ConflictException(
                "La cita fue modificada por otra operación.",
                "APPOINTMENT_CONCURRENCY_CONFLICT");
        }

        var persisted = await _persistence.First<Appointment>(
            current => current.Id == appointmentId,
            AppointmentIncludes)
            ?? throw new InvalidOperationException(
                "No se pudo recuperar la cita actualizada.");

        var response = ToResponse(persisted);

        await scope.CompleteAsync();

        _logger.LogInformation(
            "Cita {AppointmentId} pasó a {Status}.",
            appointmentId, newStatus.Value);

        return response;
    }
    public Task<AppointmentModel.PagedResponse> GetByDate(
        DateOnly date,
        int pageSize = 10,
        int pageIndex = 0,
        string? status = null)
    {
        if (date == default)
        {
            throw new ValidationException().WithDetail(nameof(date), "Es obligatoria.");
        }

        var request = new AppointmentModel.SearchRequest(
            null, null, null, date, pageSize, pageIndex, status);

        return Search(request);
    }

    public async Task<AppointmentModel.PagedResponse> Search(
        AppointmentModel.SearchRequest request)
    {
        ValidateSearch(request);
        var status = ParseStatus(request.Status);

        var appointments = await _persistence.Paginate<Appointment, int>(
            request.PageSize,
            request.PageIndex,
            appointment =>
                (!request.SpecialityId.HasValue
                    || appointment.Doctor!.SpecialityId == request.SpecialityId.Value)
                && (!request.DoctorId.HasValue
                    || appointment.DoctorId == request.DoctorId.Value)
                && (!request.Dni.HasValue
                    || appointment.Patient!.Dni == request.Dni.Value)
                && (!request.Date.HasValue
                    || appointment.Availability!.Date == request.Date.Value)
                && (!status.HasValue
                    || appointment.Status == status.Value),
            appointment =>
                appointment.Availability!.Date.DayNumber * 1440
                + appointment.Availability!.StartTime.Hour * 60
                + appointment.Availability!.StartTime.Minute,
            AppointmentIncludes);

        return new AppointmentModel.PagedResponse(
            appointments.Data.Select(ToResponse),
            appointments.Total,
            appointments.PageSize,
            appointments.PageIndex);
    }

    private async Task<Patient> GetAuthenticatedPatient(Guid patientUserId)
    {
        if (patientUserId == Guid.Empty)
        {
            throw new AuthenticationException();
        }

        return await _persistence.First<Patient>(
            patient => patient.UserId == patientUserId)
            ?? throw new AuthenticationException();
    }

    private static string ValidateRequest(AppointmentModel.Request request)
    {
        if (request is null)
            throw new ValidationException().WithDetail(
                nameof(request), "El cuerpo de la solicitud es obligatorio.");

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
            throw new ValidationException().WithDetail(
                nameof(request), "Los parámetros de búsqueda son obligatorios.");

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

    private static AppointmentStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;

        var name = Enum.GetNames<AppointmentStatus>().FirstOrDefault(
            candidate => string.Equals(
                candidate, status.Trim(), StringComparison.OrdinalIgnoreCase));

        if (name is null)
        {
            throw new ValidationException().WithDetail(
                "status", "Debe ser BOOKED, CANCELLED, ATTENDED o NO_SHOW.");
        }

        return Enum.Parse<AppointmentStatus>(name);
    }

    private static void ValidateDni(long dni, string field)
    {
        var errors = new List<(string Field, string Issue)>();
        AddDniError(dni, field, errors);
        ThrowIfInvalid(errors);
    }

    private static void AddDniError(
        long dni, string field, ICollection<(string Field, string Issue)> errors)
    {
        if (dni is < 1_000_000 or > 9_999_999_999)
            errors.Add((field, "Debe contener entre 7 y 10 dígitos."));
    }

    private void EnsureAvailabilityIsNotPast(Availability availability)
    {
        // El instante se toma después de adquirir el bloqueo.
        var now = _clock.GetCurrentLocalDateTime();
        var today = DateOnly.FromDateTime(now.DateTime);
        var currentTime = TimeOnly.FromDateTime(now.DateTime);

        var isPast = availability.Date < today
            || (availability.Date == today && availability.StartTime < currentTime);

        if (isPast)
            throw new ValidationException().WithDetail(
                nameof(Availability), "No se permiten turnos en el pasado.");
    }

    private static AppointmentModel.Response ToResponse(Appointment appointment)
    {
        var doctor = appointment.Doctor
            ?? throw new InvalidOperationException("No se cargó el médico de la cita.");
        var speciality = doctor.Speciality
            ?? throw new InvalidOperationException("No se cargó la especialidad del médico.");
        var patient = appointment.Patient
            ?? throw new InvalidOperationException("No se cargó el paciente de la cita.");
        var availability = appointment.Availability
            ?? throw new InvalidOperationException("No se cargó la disponibilidad de la cita.");

        var startTime = availability.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        var endTime = availability.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture);

        return new AppointmentModel.Response(
            appointment.Id,
            appointment.Status.ToString(),
            availability.Id,
            new AppointmentModel.PatientResponse(patient.Dni, patient.FullName ?? string.Empty),
            new AppointmentModel.DoctorResponse(
                doctor.Id,
                doctor.Name,
                new AppointmentModel.SpecialtyResponse(speciality.Id, speciality.Name)),
            availability.Date,
            startTime,
            endTime,
            $"{startTime}-{endTime}",
            appointment.Reason);
    }

    private static void ThrowIfInvalid(IEnumerable<(string Field, string Issue)> errors)
    {
        var errorList = errors.ToList();
        if (errorList.Count > 0)
            throw new ValidationException().WithDetail(errorList);
    }
}
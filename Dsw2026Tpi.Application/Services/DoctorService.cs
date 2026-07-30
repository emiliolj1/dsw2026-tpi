using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;

namespace Dsw2026Tpi.Application.Services;

public class DoctorService : IDoctorService
{
    private const int MaxPageSize = 100;
    private readonly IPersistence _persistence;

    public DoctorService(IPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task<Pagination<DoctorModel.Response>> GetAll(
        int pageSize,
        int pageIndex,
        string? name = null)
    {
        ValidatePagination(pageSize, pageIndex);

        var normalizedName = ValidateOptionalName(name);

        var doctors = await _persistence.Paginate<Doctor, string>(
            pageSize,
            pageIndex,
            doctor =>
                doctor.IsActive &&
                (normalizedName == null ||
                 doctor.Name.Contains(normalizedName)),
            doctor => doctor.Name,
            nameof(Doctor.Speciality));

        return doctors.Map(ToResponse);
    }

    public async Task<DoctorModel.Response> Create(
        DoctorModel.Request request)
    {
        var (name, licenseNumber, specialityId) =
            ValidateRequest(request);

        var speciality = await GetActiveSpeciality(specialityId);

        var doctor = new Doctor(
            name,
            licenseNumber,
            speciality);

        var createdDoctor = await _persistence.Add(doctor);

        return ToResponse(createdDoctor);
    }

    public async Task<DoctorModel.Response> Update(
        Guid id,
        DoctorModel.Request request)
    {
        var (name, licenseNumber, specialityId) =
            ValidateRequest(request);

        var doctor = await GetActiveDoctor(id);
        var speciality = await GetActiveSpeciality(specialityId);

        doctor.Update(
            name,
            licenseNumber,
            speciality);

        var updatedDoctor = await _persistence.Update(doctor);

        return ToResponse(updatedDoctor);
    }

    public async Task Delete(Guid id)
    {
        var doctor = await GetActiveDoctor(id);

        doctor.Deactivate();

        await _persistence.Delete(doctor);
    }

    private async Task<Doctor> GetActiveDoctor(Guid id)
    {
        return await _persistence.GetById<Doctor>(id)
            ?? throw new EntityNotFoundException(nameof(Doctor));
    }

    private async Task<Speciality> GetActiveSpeciality(
        Guid specialityId)
    {
        return await _persistence.GetById<Speciality>(specialityId)
            ?? throw new EntityNotFoundException(nameof(Speciality));
    }

    private static void ValidatePagination(
        int pageSize,
        int pageIndex)
    {
        var errors = new List<(string Field, string Issue)>();

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            errors.Add((
                nameof(pageSize),
                $"Debe estar entre 1 y {MaxPageSize}."));
        }

        if (pageIndex < 0)
        {
            errors.Add((
                nameof(pageIndex),
                "Debe ser mayor o igual a 0."));
        }

        ThrowIfInvalid(errors);
    }

    private static string? ValidateOptionalName(string? name)
    {
        if (name is null)
        {
            return null;
        }

        var normalizedName = name.Trim();

        if (normalizedName.Length is < 3 or > 100)
        {
            throw new ValidationException()
                .WithDetail(
                    nameof(name),
                    "Debe tener entre 3 y 100 caracteres.");
        }

        return normalizedName;
    }

    private static (
        string Name,
        string LicenseNumber,
        Guid SpecialityId) ValidateRequest(
            DoctorModel.Request request)
    {
        if (request is null)
        {
            throw new ValidationException()
                .WithDetail(
                    nameof(request),
                    "El cuerpo de la solicitud es obligatorio.");
        }

        var name = request.Name?.Trim() ?? string.Empty;
        var licenseNumber =
            request.LicenseNumber?.Trim() ?? string.Empty;

        var errors = new List<(string Field, string Issue)>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add((
                nameof(request.Name),
                "Es obligatorio."));
        }
        else if (name.Length is < 3 or > 100)
        {
            errors.Add((
                nameof(request.Name),
                "Debe tener entre 3 y 100 caracteres."));
        }

        if (string.IsNullOrWhiteSpace(request.LicenseNumber))
        {
            errors.Add((
                nameof(request.LicenseNumber),
                "Es obligatoria."));
        }

        if (request.SpecialityId == Guid.Empty)
        {
            errors.Add((
                nameof(request.SpecialityId),
                "Es obligatoria."));
        }

        ThrowIfInvalid(errors);

        return (
            name,
            licenseNumber,
            request.SpecialityId);
    }

    private static void ThrowIfInvalid(
        IEnumerable<(string Field, string Issue)> errors)
    {
        var errorList = errors.ToList();

        if (errorList.Count > 0)
        {
            throw new ValidationException()
                .WithDetail(errorList);
        }
    }

    private static DoctorModel.Response ToResponse(
        Doctor doctor)
    {
        return new DoctorModel.Response(
            doctor.Id,
            doctor.Name,
            doctor.LicenseNumber,
            new DoctorModel.SpecialityDto(
                doctor.Speciality?.Id,
                doctor.Speciality?.Name));
    }
}

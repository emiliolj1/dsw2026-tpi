using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;

namespace Dsw2026Tpi.Application.Services;

public class SpecialityService : ISpecialityService
{
    private const int MaxPageSize = 100;
    private readonly IPersistence _persistence;

    public SpecialityService(IPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task<Pagination<SpecialityModel.Response>> GetAll(
        int pageSize,
        int pageIndex,
        string? name = null)
    {
        ValidatePagination(pageSize, pageIndex);

        var normalizedName = ValidateOptionalName(name);

        var specialities = await _persistence.Paginate<Speciality, string>(
            pageSize,
            pageIndex,
            speciality => normalizedName == null ||
                speciality.Name.Contains(normalizedName),
            speciality => speciality.Name);

        return specialities.Map(ToResponse);
    }

    public async Task<SpecialityModel.Response> Create(
        SpecialityModel.Request request)
    {
        var (name, description) = ValidateRequest(request);
        var speciality = new Speciality(name, description);

        var createdSpeciality = await _persistence.Add(speciality);

        return ToResponse(createdSpeciality);
    }

    public async Task<SpecialityModel.Response> Update(
        Guid id,
        SpecialityModel.Request request)
    {
        var (name, description) = ValidateRequest(request);
        var speciality = await GetActiveSpeciality(id);

        speciality.Update(name, description);

        var updatedSpeciality = await _persistence.Update(speciality);

        return ToResponse(updatedSpeciality);
    }

    public async Task Delete(Guid id)
    {
        var speciality = await GetActiveSpeciality(id);

        await _persistence.Delete(speciality);
    }

    private async Task<Speciality> GetActiveSpeciality(Guid id)
    {
        return await _persistence.GetById<Speciality>(id)
            ?? throw new EntityNotFoundException(nameof(Speciality));
    }

    private static void ValidatePagination(int pageSize, int pageIndex)
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

    private static (string Name, string Description) ValidateRequest(
        SpecialityModel.Request request)
    {
        if (request is null)
        {
            throw new ValidationException()
                .WithDetail(
                    nameof(request),
                    "El cuerpo de la solicitud es obligatorio.");
        }

        var name = request.Name?.Trim() ?? string.Empty;
        var description = request.Description?.Trim() ?? string.Empty;
        var errors = new List<(string Field, string Issue)>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add((nameof(request.Name), "Es obligatorio."));
        }
        else if (name.Length is < 3 or > 100)
        {
            errors.Add((
                nameof(request.Name),
                "Debe tener entre 3 y 100 caracteres."));
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors.Add((nameof(request.Description), "Es obligatoria."));
        }
        else if (description.Length is < 10 or > 100)
        {
            errors.Add((
                nameof(request.Description),
                "Debe tener entre 10 y 100 caracteres."));
        }

        ThrowIfInvalid(errors);

        return (name, description);
    }

    private static void ThrowIfInvalid(
        IEnumerable<(string Field, string Issue)> errors)
    {
        var errorList = errors.ToList();

        if (errorList.Count > 0)
        {
            throw new ValidationException().WithDetail(errorList);
        }
    }

    private static SpecialityModel.Response ToResponse(
        Speciality speciality)
    {
        return new SpecialityModel.Response(
            speciality.Id,
            speciality.Name,
            speciality.Description);
    }
}
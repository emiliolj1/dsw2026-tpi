using System.Text.Json.Serialization;

namespace Dsw2026Tpi.Application.Dtos;

public record DoctorModel
{
    public record Request(
        string Name,
        string LicenseNumber,
        [property: JsonPropertyName("specialtyId")]
        Guid SpecialityId);

    public record Response(
        Guid Id,
        string Name,
        string LicenseNumber,
        SpecialityDto? Specialty);

    public record SpecialityDto(
        Guid? Id,
        string? Name);
}

using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Domain.Entities;

namespace Dsw2026Tpi.Application.Interfaces;

public interface ISpecialityService
{
    Task<Pagination<SpecialityModel.Response>> GetAll(
        int pageSize,
        int pageIndex,
        string? name = null);

    Task<SpecialityModel.Response> Create(
        SpecialityModel.Request request);

    Task<SpecialityModel.Response> Update(
        Guid id,
        SpecialityModel.Request request);

    Task Delete(Guid id);
}
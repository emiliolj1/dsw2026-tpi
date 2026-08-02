using Dsw2026Tpi.Application.Dtos;

namespace Dsw2026Tpi.Application.Interfaces;

public interface IAppointmentService
{
    Task Create(AppointmentModel.Request request);

    Task<IEnumerable<AppointmentModel.Response>> GetActiveByPatient(long dni);

    Task Cancel(Guid appointmentId);

    Task<IEnumerable<AppointmentModel.Response>> GetByDate(DateOnly date);

    Task<AppointmentModel.PagedResponse> Search(AppointmentModel.SearchRequest request);
}

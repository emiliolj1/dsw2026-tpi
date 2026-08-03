using Dsw2026Tpi.Application.Dtos;

namespace Dsw2026Tpi.Application.Interfaces;

public interface IAppointmentService
{
    Task Create(
    AppointmentModel.Request request,
    Guid patientUserId);

    Task<IEnumerable<AppointmentModel.Response>> GetActiveByPatient(
    long dni,
    Guid patientUserId);

    Task Cancel(
    Guid appointmentId,
    Guid patientUserId);

    Task<IEnumerable<AppointmentModel.Response>> GetByDate(DateOnly date);

    Task<AppointmentModel.PagedResponse> Search(AppointmentModel.SearchRequest request);
}
